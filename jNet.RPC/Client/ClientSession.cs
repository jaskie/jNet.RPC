using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace jNet.RPC.Client
{
    public abstract class ClientSession : SocketConnection
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly ConcurrentDictionary<Guid, MessageRequest> _requests = new ConcurrentDictionary<Guid, MessageRequest>();
        private readonly ReferenceResolver _referenceResolver = new ReferenceResolver();
        private readonly NotificationExecutor _notificationExecutor;
        private readonly object _deserializeLock = new object();
        private const int DeserializeLockTimeout = 100;

        public ClientSession(string address) : base(address)
        {
            _referenceResolver.SetCallbacks(Resolver_ReferenceFinalized, Resolver_ReferenceResurrected, Resolver_ReferenceMissing);
            _notificationExecutor = new NotificationExecutor();
            StartThreads();
        }

        protected override IReferenceResolver GetReferenceResolver() => _referenceResolver;

        protected override void OnDispose()
        {
            _referenceResolver.Dispose();
            _notificationExecutor.Dispose();
            base.OnDispose();
        }

        private void Resolver_ReferenceFinalized(ProxyObjectBase proxy)
        {
            Send(new SocketMessage(
                SocketMessage.SocketMessageType.ProxyFinalized,
                proxy.DtoGuid,
                string.Empty,
                0,
                null));
        }

        private void Resolver_ReferenceResurrected(ProxyObjectBase proxy)
        {
            Send(new SocketMessage(
                SocketMessage.SocketMessageType.ProxyResurrected,
                proxy.DtoGuid,
                string.Empty,
                0,
                null));
        }

        private IDto Resolver_ReferenceMissing(Guid reference)
        {
            var dto = SendAndGetResponse<IDto>(new SocketMessage(SocketMessage.SocketMessageType.ProxyMissing, reference, string.Empty, 0, null));
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
                using (var reader = new StreamReader(valueStream, Encoding.Default, false))
                {
                    T obj = default;
                    var acquiredLock = false;
                    try
                    {
                        // There are cases when the deserialization is called same time from message handler and notification threads simultaneously,
                        // and the notification call is deserialized before the actual object is deserialized.
                        // That may lead to unnecessary ProxyMissing communication with the server.
                        // From other side, it should be possible to call deserialization the above threads to avoid deadlocks when notification passes object during finalization.
                        // It's better to wait a while instead of starting the comminication; however, it may still happen in edge cases.
                        Monitor.TryEnter(_deserializeLock, DeserializeLockTimeout, ref acquiredLock);
                        obj = (T)_serializer.Deserialize(reader, typeof(T));
                        if (obj is ProxyObjectBase deserializedProxy && _referenceResolver.TryTakeProxyToPopulate(deserializedProxy.DtoGuid, out var proxyToPopulate))
                            try
                            {
                                reader.BaseStream.Position = 0;
                                _serializer.Populate(reader, proxyToPopulate);
                            }
                            catch (Exception ex)
                            {
                                Logger.Error(ex, "Error when populating {0}", deserializedProxy.DtoGuid);
                            }
                    }
                    finally
                    {
                        if (acquiredLock)
                            Monitor.Exit(_deserializeLock);
                    }
                    return obj;
                }
            }
        }
    }
}
