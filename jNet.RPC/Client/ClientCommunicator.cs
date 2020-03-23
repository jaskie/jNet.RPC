using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
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
        }        

        protected override void OnDispose()
        {
            _cancellationTokenSource.Cancel();
            ((ClientReferenceResolver)ReferenceResolver).Dispose();
            base.OnDispose();
        }

        private void Resolver_ReferenceFinalized(object sender, ProxyObjectBaseEventArgs e)
        {
            Send(SocketMessage.Create(
                SocketMessage.SocketMessageType.ProxyFinalized,
                e.Proxy,
                string.Empty,
                0,
                null));            
        }
        private void Resolver_ReferenceResurrected(object sender, ProxyObjectBaseEventArgs e)
        {
            Send(SocketMessage.Create(
                SocketMessage.SocketMessageType.ProxyResurrected,
                e.Proxy,
                string.Empty,
                0,
                null));
        }

        protected override void EnqueueMessage(SocketMessage message)
        {
            _receiveQueue.Enqueue(message);
            if (_messageReceivedSempahore.CurrentCount == 0)
                _messageReceivedSempahore.Release();
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
                    if (ex is OperationCanceledException)
                        break;

                    Logger.Error(ex, "Unexpected error in SendAndGetResponseProc");
                }
               
                if (!_requests.TryRemove(query.MessageGuid, out var response))
                {
                    Logger.Warn("SendAndGetResponse client trapped! {0}:{1}", response.Message.MessageGuid, response.Message.MessageType);
                    continue;
                }                                

                if (response.Message.MessageType == SocketMessage.SocketMessageType.Exception)
                    throw Deserialize<Exception>(response.Message);

                var result = Deserialize<T>(response.Message);
                
                if (result == null)
                    Logger.Debug("Returning NULL! MessageGuid {0}:", query.MessageGuid);
                
                _messageHandledSemaphore.Release();
                return result;
            }
            return default;
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
                        if (ex is OperationCanceledException)
                            break;
                        Logger.Error(ex, "Unexpected error in MessageHandler");
                    }
                    continue;
                }

                await _messageHandledSemaphore.WaitAsync(_cancellationTokenSource.Token);

                if (_cancellationTokenSource.IsCancellationRequested)
                    break;

                if (message.MessageType != SocketMessage.SocketMessageType.EventNotification)
                    Logger.Debug("Processing message: {0}:{1}:{2}:{3}", message.MessageGuid, message.DtoGuid, message.MemberName, message.ValueString);

                //System.Diagnostics.Debug.WriteLine(message.ValueString);

                switch (message.MessageType)
                {
                    case SocketMessage.SocketMessageType.ProxyFinalized:
                        ((ClientReferenceResolver)ReferenceResolver).DeleteReference(message.DtoGuid);
                        _messageHandledSemaphore.Release();
                        break;

                    case SocketMessage.SocketMessageType.EventNotification:
                        var notifyObject = ((ClientReferenceResolver)ReferenceResolver).ResolveReference(message.DtoGuid);
                        if (notifyObject == null)
                            Logger.Debug("NotifyObject null: {0}:{1}", message.MessageGuid, message.DtoGuid);

                        notifyObject?.OnNotificationMessage(message);
                        _messageHandledSemaphore.Release();                       
                        break;
                                            
                    default:
                        if (!_requests.TryGetValue(message.MessageGuid, out var request))
                        {
                            Logger.Debug("Message consumer not found!");
                            _messageHandledSemaphore.Release();
                            break;
                        }

                        request.Message = message;
                        request.Semaphore.Release();                        
                        break;
                }               
            }
        }        

        private T Deserialize<T>(SocketMessage message)
        {
            using (var valueStream = message.ValueStream)
            {
                if (valueStream == null)
                {
                    if (message.MessageType == SocketMessage.SocketMessageType.Query)
                        Logger.Debug("Value stream null! {0}", message.MessageGuid);
                    return default;
                }
                    
             
                using (var reader = new StreamReader(valueStream))
                {
                    var obj = (T)Serializer.Deserialize(reader, typeof(T));
                    if (obj is ProxyObjectBase target)
                    {
                        var source = ((ClientReferenceResolver)ReferenceResolver).ProxiesToPopulate.FirstOrDefault(p => p.DtoGuid == target.DtoGuid);
                        if (source == null)
                            return obj;
                        try
                        {
                            valueStream.Position = 0;
                            Serializer.Populate(reader, target);
                        }
                        catch(Exception ex)
                        {
                            Logger.Error(ex, "Error when populating {0}:{1}", source.DtoGuid, target.DtoGuid);
                        }
                        finally
                        {
                            ((ClientReferenceResolver)ReferenceResolver).ProxiesToPopulate.Remove(source);
                        }                        
                    }
                    if (obj == null && message.MemberName.Contains("GetSucc"))
                    {
                        valueStream.Position = 0;
                        Logger.Debug("NULL ON DESERIALIZE! {0}:{1}:{2}", message.DtoGuid, message.ValueString, reader.ReadToEnd());
                    }
                        

                    return obj;                    
                }
                    
            }
        }
    }
}
