using Newtonsoft.Json;
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

        public ClientSession(string address) : base(address)
        {
            ClientConnectionState = IsCancelled ? ClientConnectionState.Disconnected : ClientConnectionState.Connecting;
            _referenceResolver.SetCallbacks(Resolver_ReferenceFinalized, Resolver_ReferenceResurrected, Resolver_ReferenceMissing);
            _notificationExecutor = new NotificationExecutor();
            if (ClientConnectionState == ClientConnectionState.Connecting)
                StartThreads();
        }

        public ClientConnectionState ClientConnectionState { get; protected set; }

        protected override IReferenceResolver GetReferenceResolver() => _referenceResolver;

        protected override void OnDispose()
        {
            _referenceResolver.Dispose();
            _notificationExecutor.Dispose();
            base.OnDispose();
            ClientConnectionState = ClientConnectionState.Disposed;
        }

        private void Resolver_ReferenceFinalized(Guid guid)
        {
            Send(new SocketMessage(
                SocketMessageType.ProxyFinalized,
                guid,
                string.Empty,
                0,
                null));
        }

        private void Resolver_ReferenceResurrected(Guid guid)
        {
            Send(new SocketMessage(
                SocketMessageType.ProxyResurrected,
                guid,
                string.Empty,
                0,
                null));
        }

        private IDto Resolver_ReferenceMissing(Guid reference)
        {
            var dto = SendAndGetResponse<IDto>(new SocketMessage(SocketMessageType.ProxyMissing, reference, string.Empty, 0, null));
            if (dto == null)
                Logger.Warn("Dto {0} not found on server", reference);
            else
                Logger.Debug("Dto {0} restored from server as {1}", reference, dto);
            return dto;
        }

        private protected T SendAndGetResponse<T>(SocketMessage query)
        {
            if (IsCancelled)
                return default;
            try
            {
                using (var messageRequest = new MessageRequest())
                {
                    _requests.TryAdd(query.MessageGuid, messageRequest);
                    Send(query);
                    var response = messageRequest.WaitForResult(CancellationToken);

                    if (!_requests.TryRemove(query.MessageGuid, out var _))
                    {
                        Logger.Error("SendAndGetResponse client trapped for message {0}", response);
                        return default;
                    }
                    if (response is null)
                        return default;
                    if (messageRequest.MessageType == SocketMessageType.Exception)
                        throw (Exception)response;
                    if (typeof(T).IsEnum)
                    {
                        return (T)Enum.ToObject(typeof(T), response);
                    }
                    return (T)response;
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
            while (!IsCancelled)
            {
                try
                {
                    var message = TakeNextMessage();
                    Logger.Trace("Processing message: {0}", message);
                    switch (message.MessageType)
                    {
                        case SocketMessageType.ProxyFinalized:
                            _referenceResolver.DeleteReference(message.DtoGuid);
                            break;

                        case SocketMessageType.EventNotification:
                            var notifyObject = _referenceResolver.ResolveReference(message.DtoGuid);
                            var eventArgs = Deserialize(message) as EventArgs;
                            if (notifyObject != null && eventArgs != null)
                                _notificationExecutor.Queue(() => notifyObject.OnEventNotification(message.MemberName, eventArgs));
                            else
                            {
                                if (notifyObject is null)
                                    Logger.Debug("Proxy to notify not found for message: {0}, notification was {1}", message, eventArgs);
                                if (eventArgs is null)
                                    Logger.Debug("EventArgs to notify {0} was empty for message: {1}", notifyObject, message);
                            }
                            break;

                        default:
                            var deserialized = Deserialize(message);
                            if (_requests.TryGetValue(message.MessageGuid, out var request))
                                request.SetResult(message.MessageType, deserialized);
                            else if (message.MessageType == SocketMessageType.Exception
                                    && message.DtoGuid == Guid.Empty
                                    && deserialized is UnauthorizedAccessException unauthorizedAccessException)
                                throw unauthorizedAccessException; // handle invalid identity on connection (before first request is sent)
                            else
                                Logger.Warn("Unexpected message arrived from server: {0}", message);
                            break;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    ClientConnectionState = ClientConnectionState.Rejected;
                    Logger.Warn("Connection rejected by server: {0}", this);
                    Shutdown();
                    return;
                }
                catch (OperationCanceledException)
                {
                    ClientConnectionState = ClientConnectionState.Disconnected;
                    Shutdown();
                    return;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Unexpected error in MessageHandlerProc");
                }
            }
        }

        internal object Deserialize(SocketMessage message)
        {
            using (var valueStream = message.GetValueStream())
            {
                if (valueStream == null)
                    return default;
                using (var reader = new StreamReader(valueStream, Encoding.Default, false))
                using (var jsonReader = new JsonTextReader(reader))
                {
#if DEBUG
                    Logger.Trace("Deserializing message:\n{0}", message.ValueString);
#endif
                    var deserialized = Serializer.Deserialize(jsonReader);
                    if (deserialized == null)
                        return default;
                    if (deserialized is ProxyObjectBase deserializedProxy && _referenceResolver.TryTakeProxyToPopulate(deserializedProxy.DtoGuid, out var proxyToPopulate))
                        try
                        {
                            reader.BaseStream.Position = 0;
                            Serializer.Populate(reader, proxyToPopulate);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "Error when populating {0}", deserializedProxy.DtoGuid);
                        }
                    return deserialized;
                }
            }
        }
    }

    public enum ClientConnectionState
    {
        Disconnected,
        Connecting,
        Rejected,
        Connected,
        Disposed,
    }
}
