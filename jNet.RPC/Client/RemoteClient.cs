using System;
using Newtonsoft.Json.Serialization;

namespace jNet.RPC.Client
{
    public class RemoteClient : ClientCommunicator
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();               

        public ISerializationBinder Binder { set => SetBinder(value); }

        public T GetRootObject<T>()
        {
            try
            {
                var queryMessage = SocketMessage.Create(SocketMessage.SocketMessageType.RootQuery, null, null, 0, null);
                var response = SendAndGetResponse<T>(queryMessage).Result;                
                return response;
            }
            catch (Exception e)
            {
                Logger.Error(e, $"From {nameof(GetRootObject)}:");
                throw;
            }
        }

        public T Query<T>(ProxyBase dto, string methodName, params object[] parameters)
        {
            try
            {
                var queryMessage = SocketMessage.Create(
                    SocketMessage.SocketMessageType.Query,
                    dto,
                    methodName,
                    parameters.Length,
                    new SocketMessageArrayValue { Value = parameters });
                return SendAndGetResponse<T>(queryMessage).Result;                
            }
            catch (Exception e)
            {
                Logger.Error("From Query for {0}: {1}", dto, e);
                throw;
            }
        }

        public T Get<T>(ProxyBase dto, string propertyName)
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
                return SendAndGetResponse<T>(queryMessage).Result;
            }
            catch (Exception e)
            {
                Logger.Error("From Get {0}: {1}", dto, e);
                throw;
            }
        }

        public void Invoke(ProxyBase dto, string methodName, params object[] parameters)
        {
            try
            {
                var queryMessage = SocketMessage.Create(
                    SocketMessage.SocketMessageType.Query,
                    dto,
                    methodName,
                    parameters.Length,
                    new SocketMessageArrayValue { Value = parameters });
                SendAndGetResponse<object>(queryMessage).Wait();
            }
            catch (Exception e)
            {
                Logger.Error("From Invoke {0}: {1}", dto, e);
                throw;
            }
        }

        public void Set(ProxyBase dto, object value, string propertyName)
        {
            try
            {
                var queryMessage = SocketMessage.Create(
                SocketMessage.SocketMessageType.Set,
                dto,
                propertyName,
                1,
                value);
                SendAndGetResponse<object>(queryMessage).Wait();
            }
            catch (Exception e)
            {
                Logger.Error("From Set {0}: {1}", dto, e);
                throw;
            }
        }

        public void EventAdd(ProxyBase dto, string eventName)
        {
            try
            {
                var queryMessage = SocketMessage.Create(
                SocketMessage.SocketMessageType.EventAdd,
                dto,
                eventName,
                0,
                null);
                SendAndGetResponse<object>(queryMessage).Wait();
            }
            catch (Exception e)
            {
                Logger.Error("From Invoke {0}: {1}", dto, e);
                throw;
            }
        }

        public void EventRemove(ProxyBase dto, string eventName)
        {
            try
            {
                var queryMessage = SocketMessage.Create(
                SocketMessage.SocketMessageType.EventRemove,
                dto,
                eventName,
                0,
                null);
                SendAndGetResponse<object>(queryMessage).Wait();
            }
            catch (Exception e)
            {
                Logger.Error("From Invoke {0}: {1}", dto, e);
                throw;
            }
        }
    }
}
