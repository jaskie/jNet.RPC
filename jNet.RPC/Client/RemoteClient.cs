using Newtonsoft.Json.Serialization;
using System;
using System.Reflection;

namespace jNet.RPC.Client
{
    public class RemoteClient : ClientSession
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public RemoteClient() : base()
        {
            Serializer.SerializationBinder = new SerializationBinder();
        }

        public RemoteClient(ISerializationBinder serializationBinder) : base()
        {
            Serializer.SerializationBinder = serializationBinder;
        }

        public void AddProxyAssembly(Assembly assembly)
        {
            var binder = Serializer.SerializationBinder as SerializationBinder ?? throw new ApplicationException($"SerializationBinder is not {typeof(SerializationBinder)}");
            binder.AddProxyAssembly(assembly);
        }

        public T GetRootObject<T>()
        {
            try
            {
                var queryMessage = SocketMessage.Create(SocketMessage.SocketMessageType.RootQuery, null, null, 0, null);
                var response = SendAndGetResponse<T>(queryMessage);                
                return response;
            }
            catch (Exception e)
            {
                Logger.Error(e, $"From {nameof(GetRootObject)}:");
                throw;
            }
        }

        public T Query<T>(ProxyObjectBase dto, string methodName, params object[] parameters)
        {
            try
            {
                var queryMessage = SocketMessage.Create(
                    SocketMessage.SocketMessageType.Query,
                    dto,
                    methodName,
                    parameters.Length,
                    new SocketMessageArrayValue { Value = parameters });
                return SendAndGetResponse<T>(queryMessage);                
            }
            catch (Exception e)
            {
                Logger.Error("From Query for {0}: {1}", dto, e);
                throw;
            }
        }

        public T Get<T>(ProxyObjectBase dto, string propertyName)
        {
            try
            {
                var queryMessage = SocketMessage.Create(
                    SocketMessage.SocketMessageType.Get,
                    dto,
                    propertyName,
                    0,
                    null
                );
                return SendAndGetResponse<T>(queryMessage);
            }
            catch (Exception e)
            {
                Logger.Error("From Get {0}: {1}", dto, e);
                throw;
            }
        }

        public void Invoke(ProxyObjectBase dto, string methodName, params object[] parameters)
        {
            try
            {
                var queryMessage = SocketMessage.Create(
                    SocketMessage.SocketMessageType.Query,
                    dto,
                    methodName,
                    parameters.Length,
                    new SocketMessageArrayValue { Value = parameters });
                SendAndGetResponse<object>(queryMessage);
            }
            catch (Exception e)
            {
                Logger.Error("From Invoke {0}: {1}", dto, e);
                throw;
            }
        }

        public void Set(ProxyObjectBase dto, object value, string propertyName)
        {
            try
            {
                var queryMessage = SocketMessage.Create(
                SocketMessage.SocketMessageType.Set,
                dto,
                propertyName,
                1,
                value);
                SendAndGetResponse<object>(queryMessage);
            }
            catch (Exception e)
            {
                Logger.Error("From Set {0}: {1}", dto, e);
                throw;
            }
        }

        public void EventAdd(ProxyObjectBase dto, string eventName)
        {
            try
            {
                var queryMessage = SocketMessage.Create(
                SocketMessage.SocketMessageType.EventAdd,
                dto,
                eventName,
                0,
                null);
                SendAndGetResponse<object>(queryMessage);
            }
            catch (Exception e)
            {
                Logger.Error("From Invoke {0}: {1}", dto, e);
                throw;
            }
        }

        public void EventRemove(ProxyObjectBase dto, string eventName)
        {
            try
            {
                var queryMessage = SocketMessage.Create(
                SocketMessage.SocketMessageType.EventRemove,
                dto,
                eventName,
                0,
                null);
                SendAndGetResponse<object>(queryMessage);
            }
            catch (Exception e)
            {
                Logger.Error("From Invoke {0}: {1}", dto, e);
                throw;
            }
        }        
    }
}
