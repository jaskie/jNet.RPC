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
        private readonly ConcurrentDictionary<Guid, MessageRequest> _requests = new ConcurrentDictionary<Guid, MessageRequest>();
        private readonly SemaphoreSlim _notificationSemaphore = new SemaphoreSlim(0);
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        

        public MessageController() : base(new ClientReferenceResolver())
        {
            ((ClientReferenceResolver)ReferenceResolver).ReferenceFinalized += Resolver_ReferenceFinalized;
            ((ClientReferenceResolver)ReferenceResolver).UnreferencedObjectFinder = UnreferencedObjectFinder;            
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
            _requests.TryAdd(query.MessageGuid, new MessageRequest());
            Send(query);
            while (IsConnected)
            {
                try
                {
                    await _requests[query.MessageGuid].SemaphoreSlim.WaitAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (ex.GetType() == typeof(OperationCanceledException))
                        break;

                    Logger.Error(ex, "Unexpected error in SendAndGetResponseProc");
                }
               
                if (!_requests.TryRemove(query.MessageGuid, out var response))
                {
                    Logger.Warn($"SAGR client trapped! {response.Message.MessageGuid}:{response.Message.MessageType}");
                    continue;
                }
                    

                //Logger.Debug($"SAGR client satisified: {response.Message.MessageGuid}");
                
                if (response.Message.MessageType == SocketMessage.SocketMessageType.UnresolvedReferenceServer)
                    return default(T);

                if (response.Message.MessageType == SocketMessage.SocketMessageType.Exception)
                    throw Deserialize<Exception>(response.Message);

                var result = Deserialize<T>(response.Message);
                return result;
            }
            return default(T);
        }

        protected SocketMessage WebSocketMessageCreate(SocketMessage.SocketMessageType SocketMessageType, IDto dto, string memberName, int paramsCount, object value)
        {
            return new SocketMessage(value)
            {
                MessageType = SocketMessageType,
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

                switch (message.MessageType)
                {
                    case SocketMessage.SocketMessageType.EventNotification:
                        _notificationQueue.Enqueue(message);
                        if (_notificationSemaphore.CurrentCount == 0)
                            _notificationSemaphore.Release();
                        break;
                    default:
                        if (!_requests.TryGetValue(message.MessageGuid, out var request))
                        {
                            Logger.Debug("Message consumer not found!");
                            break;
                        }
                            
                        request.Message = message;
                        request.SemaphoreSlim.Release();                        
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
                        await _notificationSemaphore.WaitAsync(_cancellationTokenSource.Token).ConfigureAwait(false);
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
                
                var notifyObject = ((ClientReferenceResolver)ReferenceResolver).ResolveReference(message.DtoGuid);
                notifyObject?.OnEventNotificationMessage(message);               
            }
        }
    }
}
