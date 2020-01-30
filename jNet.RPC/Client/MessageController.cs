using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace jNet.RPC.Client
{
    public abstract class MessageController : SocketConnection
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly ConcurrentQueue<SocketMessage> _notificationQueue = new ConcurrentQueue<SocketMessage>();
        private readonly ConcurrentDictionary<Guid, SocketMessage> _messageQueue = new ConcurrentDictionary<Guid, SocketMessage>();
        private int _requestsCount = 0;
        private object _requestsLock = new object();    
        private readonly SemaphoreSlim _sendAndResponseSemaphore = new SemaphoreSlim(0);
        private readonly SemaphoreSlim _notificationSemaphore = new SemaphoreSlim(0);
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public MessageController(string address) : base(address, new ClientReferenceResolver())
        {
            ((ClientReferenceResolver)ReferenceResolver).ReferenceFinalized += Resolver_ReferenceFinalized;
            ((ClientReferenceResolver)ReferenceResolver).UnreferencedObjectFinder = UnreferencedObjectFinder;
            StartThreads();            
            _ = NotificationHandlerProc();
        }

        protected override void OnDispose()
        {
            base.OnDispose();
            _cancellationTokenSource.Cancel();
            ((ClientReferenceResolver)ReferenceResolver).Dispose();
        }

        private void Resolver_ReferenceFinalized(object sender, ProxyBaseEventArgs e)
        {
            Send(WebSocketMessageCreate(
                SocketMessage.SocketMessageType.ProxyFinalized,
                e.Proxy,
                string.Empty,
                0,
                null));
        }

        private async Task<ProxyBase> UnreferencedObjectFinder(Guid guid)
        {
            //Logger.Debug($"Unresolved reference! {guid}");
            var proxy = await SendAndGetResponse<ProxyBase>(new SocketMessage((object)null)
            {
                MessageType = SocketMessage.SocketMessageType.UnresolvedReference,
                DtoGuid = guid
            }).ConfigureAwait(false);

            Logger.Debug($"Unresolved reference restored: {guid} : {proxy}");
            return proxy;
        }

        protected async Task<T> SendAndGetResponse<T>(SocketMessage query)
        {
            lock (_requestsLock)
                ++_requestsCount;
            Send(query);
            while (IsConnected)
            {
                try
                {
                    await _sendAndResponseSemaphore.WaitAsync(_cancellationTokenSource.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (ex.GetType() == typeof(OperationCanceledException))
                        break;

                    Logger.Error(ex, "Unexpected error in SendAndGetResponseProc");
                }

                SocketMessage response;

                if (!_messageQueue.TryRemove(query.MessageGuid, out response))
                    continue;

                lock (_requestsLock)
                    --_requestsCount;

                //Logger.Debug($"SAGR client satisified: {response.MessageGuid}");

                if (response.MessageType == SocketMessage.SocketMessageType.UnresolvedReferenceServer)
                    return default(T);

                if (response.MessageType == SocketMessage.SocketMessageType.Exception)
                    throw Deserialize<Exception>(response);

                var result = Deserialize<T>(response);
                return result;
            }
            return default(T);
        }

        protected SocketMessage WebSocketMessageCreate(SocketMessage.SocketMessageType socketMessageType, IDto dto, string memberName, int paramsCount, object value)
        {
            return new SocketMessage(value)
            {
                MessageType = socketMessageType,
                DtoGuid = dto?.DtoGuid ?? Guid.Empty,
                MemberName = memberName,
                ParametersCount = paramsCount
            };
        }

        private T Deserialize<T>(SocketMessage message)
        {
            using (var valueStream = message.ValueStream)
            {
                if (valueStream == null)
                    return default(T);
                using (var reader = new StreamReader(valueStream))
                    return (T)Serializer.Deserialize(reader, typeof(T));
            }
        }

        protected override async Task MessageHandlerProc()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                if (!_receiveQueue.TryDequeue(out var message))
                {
                    try
                    {
                        await _messageHandlerSempahore.WaitAsync(_cancellationTokenSource.Token);
                    }
                    catch (Exception ex)
                    {
                        if (ex.GetType() == typeof(OperationCanceledException))
                            break;

                        Logger.Error(ex, "Unexpected error in MessageHandler");
                    }
                    continue;
                }

                if (_cancellationTokenSource.IsCancellationRequested)
                    break;


                //if (message.MessageType != SocketMessage.SocketMessageType.RootQuery && _initialObject == null)
                //{
                //    _receiveQueue.Enqueue(message);
                //    continue;
                //}


                switch (message.MessageType)
                {
                    case SocketMessage.SocketMessageType.EventNotification:
                        _notificationQueue.Enqueue(message);
                        if (_notificationSemaphore.CurrentCount == 0)
                            _notificationSemaphore.Release();
                        break;
                    default:
                        _messageQueue[message.MessageGuid] = message;

                        lock (_requestsLock)
                            if (_requestsCount > 0)
                                _sendAndResponseSemaphore.Release(_requestsCount);
                        break;
                }
            }
        }
        private async Task NotificationHandlerProc()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                if (!_notificationQueue.TryDequeue(out var message))
                {
                    try
                    {
                        await _notificationSemaphore.WaitAsync(_cancellationTokenSource.Token);
                    }
                    catch (Exception ex)
                    {
                        if (ex.GetType() == typeof(OperationCanceledException))
                            break;

                        Logger.Error(ex, "Unexpected error in NotificationHandlerProc");
                    }
                    continue;
                }

                if (_cancellationTokenSource.IsCancellationRequested)
                    break;

                var notifyObject = await ((ClientReferenceResolver)ReferenceResolver).ResolveReference(message.DtoGuid);
                notifyObject?.OnEventNotificationMessage(message);
            }
        }
    }
}
