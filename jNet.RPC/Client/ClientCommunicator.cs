using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace jNet.RPC.Client
{
    public abstract class ClientCommunicator : SocketConnection
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();       
        protected readonly ConcurrentQueue<SocketMessage> _receiveQueue = new ConcurrentQueue<SocketMessage>();
        private readonly ConcurrentDictionary<Guid, MessageRequest> _requests = new ConcurrentDictionary<Guid, MessageRequest>();        
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly SemaphoreSlim _messageHandledSemaphore = new SemaphoreSlim(1);

        public ClientCommunicator() : base(new ClientReferenceResolver())
        {
            ((ClientReferenceResolver)ReferenceResolver).ReferenceFinalized += Resolver_ReferenceFinalized;
            ((ClientReferenceResolver)ReferenceResolver).UnreferencedObjectFinder = UnreferencedObjectFinder;                                   
        }

        protected override void OnDispose()
        {
            _cancellationTokenSource.Cancel();
            ((ClientReferenceResolver)ReferenceResolver).Dispose();
            base.OnDispose();
        }

        private void Resolver_ReferenceFinalized(object sender, ProxyBaseEventArgs e)
        {
            Send(SocketMessage.Create(
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

            Logger.Trace("Unresolved reference restored: {0}:{1}", guid, proxy);
            return proxy;
        }

        protected override void EnqueueMessage(SocketMessage message)
        {
            if (message.MessageType == SocketMessage.SocketMessageType.UnresolvedReference || message.MessageType == SocketMessage.SocketMessageType.UnresolvedReferenceServer)
            {
                if (!_requests.TryGetValue(message.MessageGuid, out var request))
                {
                    Logger.Debug("Message consumer not found!");
                    return;
                }

                request.Message = message;
                request.Semaphore.Release();               
            }
            else
            {
                _receiveQueue.Enqueue(message);
                if (_messageReceivedSempahore.CurrentCount == 0)
                    _messageReceivedSempahore.Release();
            }                
        }

        protected async Task<T> SendAndGetResponse<T>(SocketMessage query)
        {
            _requests.TryAdd(query.MessageGuid, new MessageRequest());
            Send(query);
            while (IsConnected)
            {
                try
                {
                    await _requests[query.MessageGuid].Semaphore.WaitAsync(_cancellationTokenSource.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (ex.GetType() == typeof(OperationCanceledException))
                        break;

                    Logger.Error(ex, "Unexpected error in SendAndGetResponseProc");
                }
               
                if (!_requests.TryRemove(query.MessageGuid, out var response))
                {
                    Logger.Warn("SendAndGetResponse client trapped! {0}:{1}", response.Message.MessageGuid, response.Message.MessageType);
                    continue;
                }
                
                if (response.Message.MessageType == SocketMessage.SocketMessageType.UnresolvedReferenceServer)
                    return default(T);

                if (response.Message.MessageType == SocketMessage.SocketMessageType.Exception)
                    throw Deserialize<Exception>(response.Message);

                var result = Deserialize<T>(response.Message);
                return result;
            }
            return default(T);
        }                       

        protected override async Task MessageHandlerProc()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                if (!_receiveQueue.TryDequeue(out var message))
                {
                    try
                    {
                        await _messageReceivedSempahore.WaitAsync(_cancellationTokenSource.Token);
                    }
                    catch (Exception ex)
                    {
                        if (ex.GetType() == typeof(OperationCanceledException))
                            break;

                        Logger.Error(ex, "Unexpected error in MessageHandler");
                    }
                    continue;
                }

                await _messageHandledSemaphore.WaitAsync(_cancellationTokenSource.Token);

                if (_cancellationTokenSource.IsCancellationRequested)
                    break;

                if (message.MessageType != SocketMessage.SocketMessageType.EventNotification)
                    Logger.Debug("Processing message: {0}:{1}", message.MessageGuid, message.DtoGuid);

                switch (message.MessageType)
                {
                    case SocketMessage.SocketMessageType.ProxyFinalized:
                        ((ClientReferenceResolver)ReferenceResolver).DeleteReference(message.DtoGuid);                        
                        break;

                    case SocketMessage.SocketMessageType.EventNotification:
                        var notifyObject = ((ClientReferenceResolver)ReferenceResolver).ResolveReference(message.DtoGuid);
                        if (notifyObject == null)
                            Logger.Debug("NotifyObject null: {0}:{1}", message.MessageGuid, message.DtoGuid);

                        notifyObject?.OnNotificationMessage(message);                        
                        break;
                                            
                    default:
                        if (!_requests.TryGetValue(message.MessageGuid, out var request))
                        {
                            Logger.Debug("Message consumer not found!");
                            break;
                        }

                        request.Message = message;
                        request.Semaphore.Release();                        
                        break;
                }
                _messageHandledSemaphore.Release();
            }
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
    }
}
