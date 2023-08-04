using System;
using System.Collections.Concurrent;
using System.IO;

namespace jNet.RPC.Client
{
    public abstract class ClientSession : SocketConnection
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();       
        private readonly ConcurrentDictionary<Guid, MessageRequest> _requests = new ConcurrentDictionary<Guid, MessageRequest>();
        private readonly ReferenceResolver _referenceResolver;
        private readonly NotificationExecutor _notificationExecutor;

        public ClientSession(string address) : base(address, new ReferenceResolver())
        {
            _referenceResolver = ReferenceResolver as ReferenceResolver ?? throw new ApplicationException("Invalid reference resolver");
            _referenceResolver.ReferenceFinalized += Resolver_ReferenceFinalized;
            _referenceResolver.ReferenceResurected += Resolver_ReferenceResurrected;
            _referenceResolver.OnReferenceMissing = Resolver_ReferenceMissing;
            _notificationExecutor = new NotificationExecutor();
            StartThreads();
        }


        protected override void OnDispose()
        {
            base.OnDispose();
            _referenceResolver.ReferenceFinalized -= Resolver_ReferenceFinalized;
            _referenceResolver.ReferenceResurected -= Resolver_ReferenceResurrected;
            _referenceResolver.OnReferenceMissing = null;
            _referenceResolver.Dispose();
            _notificationExecutor.Dispose();
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

        private IDto Resolver_ReferenceMissing(Guid reference)
        {
            var dto = SendAndGetResponse<IDto>(new SocketMessage()
            {
                MessageType = SocketMessage.SocketMessageType.ProxyMissing,
                DtoGuid = reference
            });
            if (dto == null)
                Logger.Warn("Dto {0} not found on server", reference);
            else
                Logger.Debug("Dto {0} restored from server as {1}", reference, dto);
            return dto;
        }


        internal T SendAndGetResponse<T>(SocketMessage query)
        {
            if (DisconnectTokenSource.IsCancellationRequested)
                return default;
            try
            {
                using (var messageRequest = new MessageRequest())
                {
                    _requests.TryAdd(query.MessageGuid, messageRequest);
                    Send(query);
                    var response = messageRequest.WaitForResult(DisconnectTokenSource.Token);

                    if (!_requests.TryRemove(query.MessageGuid, out var _))
                    {
                        Logger.Warn("SendAndGetResponse client trapped for message {0}", response);
                        return default;
                    }
                    if (response is null)
                        return default;
                    if (response.MessageType == SocketMessage.SocketMessageType.Exception)
                        throw Deserialize<Exception>(response);
                    return Deserialize<T>(response);
                }
            }
            catch (Exception e) when (e is OperationCanceledException || e is ObjectDisposedException)
            {
                Shutdown();
                return default;
            }
        }

        protected override void MessageHandlerProc()
        {
            while (!DisconnectTokenSource.IsCancellationRequested)
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
                            _notificationExecutor.Queue(() =>
                            {
                                var notifyObject = _referenceResolver.ResolveReference(message.DtoGuid);
                                if (notifyObject == null)
                                    Logger.Debug("Proxy to notify not found for message: {0}", message);
                                else
                                    notifyObject.OnNotificationMessage(message);
                            });
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

        internal T Deserialize<T>(SocketMessage message)
        {
            using (var valueStream = message.GetValueStream())
            {
                if (valueStream == null)
                    return default;

                lock (this)
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
                                reader.BaseStream.Position = 0;
                                Serializer.Populate(reader, target);
                            }
                            catch (Exception ex)
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
