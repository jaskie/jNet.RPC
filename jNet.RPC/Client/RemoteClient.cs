using Newtonsoft.Json.Serialization;
using System;
using System.Reflection;

namespace jNet.RPC.Client
{
    public class RemoteClient : ClientSession
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public RemoteClient(string address) : base(address)
        {
            _serializer.SerializationBinder = new SerializationBinder();
        }

        public RemoteClient(string address, ISerializationBinder serializationBinder) : base(address)
        {
            _serializer.SerializationBinder = serializationBinder;
        }

        public void AddProxyAssembly(Assembly assembly)
        {
            var binder = _serializer.SerializationBinder as SerializationBinder ?? throw new ApplicationException($"SerializationBinder is not {typeof(SerializationBinder)}");
            binder.AddProxyAssembly(assembly);
        }

        public T GetRootObject<T>()
        {
            try
            {
                var queryMessage = new SocketMessage(SocketMessage.SocketMessageType.RootQuery, null, null, 0, null);
                var response = SendAndGetResponse<T>(queryMessage);
                return response;
            }
            catch (Exception e)
            {
                Logger.Error(e, $"From {nameof(GetRootObject)}:{typeof(T)}");
                throw;
            }
        }

        public T Query<T>(ProxyObjectBase dto, string methodName, params object[] parameters)
        {
            try
            {
                var queryMessage = new SocketMessage(
                    SocketMessage.SocketMessageType.MethodExecute,
                    dto,
                    methodName,
                    parameters.Length,
                    new SocketMessageArrayValue { Value = parameters });
                return SendAndGetResponse<T>(queryMessage);
            }
            catch (Exception e)
            {
                Logger.Error(e, "From Query {0}:{1}", dto, methodName);
                throw;
            }
        }

        public T Get<T>(ProxyObjectBase dto, string propertyName)
        {
            try
            {
                var queryMessage = new SocketMessage(
                    SocketMessage.SocketMessageType.PropertyGet,
                    dto,
                    propertyName,
                    0,
                    null
                );
                return SendAndGetResponse<T>(queryMessage);
            }
            catch (Exception e)
            {
                Logger.Error(e, "From Get {0}:{1}", dto, propertyName);
                throw;
            }
        }

        public void Invoke(ProxyObjectBase dto, string methodName, params object[] parameters)
        {
            try
            {
                var queryMessage = new SocketMessage(
                    SocketMessage.SocketMessageType.MethodExecute,
                    dto,
                    methodName,
                    parameters.Length,
                    new SocketMessageArrayValue { Value = parameters });
                SendAndGetResponse<object>(queryMessage);
            }
            catch (Exception e)
            {
                Logger.Error(e, "From Invoke {0}:{1}", dto, methodName);
                throw;
            }
        }

        public void Set(ProxyObjectBase dto, object value, string propertyName)
        {
            try
            {
                var queryMessage = new SocketMessage(
                SocketMessage.SocketMessageType.PropertySet,
                dto,
                propertyName,
                1,
                value);
                SendAndGetResponse<object>(queryMessage);
            }
            catch (Exception e)
            {
                Logger.Error(e, "From Set {0}:{1}", dto, propertyName);
                throw;
            }
        }

        public void EventAdd(ProxyObjectBase dto, string eventName)
        {
            try
            {
                var queryMessage = new  SocketMessage(
                SocketMessage.SocketMessageType.EventAdd,
                dto,
                eventName,
                0,
                null);
                SendAndGetResponse<object>(queryMessage);
            }
            catch (Exception e)
            {
                Logger.Error(e, "From EventAdd {0}:{1}", dto, eventName);
                throw;
            }
        }

        public void EventRemove(ProxyObjectBase dto, string eventName)
        {
            try
            {
                var queryMessage = new  SocketMessage(
                SocketMessage.SocketMessageType.EventRemove,
                dto,
                eventName,
                0,
                null);
                SendAndGetResponse<object>(queryMessage);
            }
            catch (Exception e)
            {
                Logger.Error(e, "From EventRemove: {0}:{1}", dto, eventName);
                throw;
            }
        }
    }
}
