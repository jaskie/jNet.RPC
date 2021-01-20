using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace jNet.RPC.Client
{
    public abstract class ClientSession : SocketConnection
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();       
        private readonly ConcurrentDictionary<Guid, MessageRequest> _requests = new ConcurrentDictionary<Guid, MessageRequest>();
        private readonly ReferenceResolver _referenceResolver;

        public ClientSession() : base(new ReferenceResolver())
        {
            _referenceResolver = ReferenceResolver as ReferenceResolver ?? throw new ApplicationException("Invalid reference resolver");
            _referenceResolver.ReferenceFinalized += Resolver_ReferenceFinalized;
            _referenceResolver.ReferenceResurected += Resolver_ReferenceResurrected;
        }        

        protected override void OnDispose()
        {
            base.OnDispose();
            _referenceResolver.ReferenceFinalized -= Resolver_ReferenceFinalized;
            _referenceResolver.ReferenceResurected -= Resolver_ReferenceResurrected;
            _referenceResolver.Dispose();
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

        internal T SendAndGetResponse<T>(SocketMessage query)
        {
            try
            {
                using (var messageRequest = new MessageRequest())
                {
                    _requests.TryAdd(query.MessageGuid, messageRequest);
                    Send(query);
                    var response = messageRequest.WaitForResult(CancellationTokenSource.Token);

                    if (!_requests.TryRemove(query.MessageGuid, out var _))
                    {
                        Logger.Warn("SendAndGetResponse client trapped for message {0}", response);
                        return default;
                    }

                    if (response.MessageType == SocketMessage.SocketMessageType.Exception)
                        throw Deserialize<Exception>(response);
                    return Deserialize<T>(response);
                }
            }
            catch (Exception e) when (e is OperationCanceledException || e is ObjectDisposedException)
            {
                NotifyDisconnection();
                return default;
            }
        }

        protected override void MessageHandlerProc()
        {
            while (!CancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    var message = TakeNextMessage();
                    if (message.MessageType != SocketMessage.SocketMessageType.EventNotification)
                        Logger.Trace("Processing message: {0}", message);

                    switch (message.MessageType)
                    {
                        case SocketMessage.SocketMessageType.ProxyFinalized:
                            _referenceResolver.DeleteReference(message.DtoGuid);
                            break;

                        case SocketMessage.SocketMessageType.EventNotification:
                            var notifyObject = _referenceResolver.ResolveReference(message.DtoGuid);
                            if (notifyObject == null)
                                Logger.Debug("Proxy to notify not found for message: {0}", message);
                            else
                                notifyObject?.OnNotificationMessage(message);
                            break;

                        default:
                            if (_requests.TryGetValue(message.MessageGuid, out var request))
                                request.SetResult(message);
                            else
                                Logger.Warn("Unexpected message arrived from server: {0}", message);
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Unexpected error in MessageHandler");
                }
            }
        }

        private T Deserialize<T>(SocketMessage message)
        {
            using (var valueStream = message.ValueStream)
            {
                if (valueStream == null)
                    return default;
                    
                using (var reader = new StreamReader(valueStream))
                {
                    var obj = (T)Serializer.Deserialize(reader, typeof(T));
                    if (obj is ProxyObjectBase target)
                    {
                        var source = ((ReferenceResolver)ReferenceResolver).TakeProxyToPopulate(target.DtoGuid);
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
                    }
                    return obj;                    
                }
                    
            }
        }
    }
}
