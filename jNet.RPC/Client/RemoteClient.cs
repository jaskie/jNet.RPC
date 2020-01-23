//#undef DEBUG

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Serialization;

namespace jNet.RPC.Client
{
    public class RemoteClient: SocketConnection
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private object _initialObject;
        private readonly ConcurrentDictionary<Guid, SocketMessage> _receivedMessages = new ConcurrentDictionary<Guid, SocketMessage>();

        private readonly SemaphoreSlim _sendAndResponseSemaphore = new SemaphoreSlim(0); 
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private int _requestsCount = 0;
        private object _requestsLock = new object();

        public RemoteClient(string address): base(address, new ClientReferenceResolver())
        {
            ((ClientReferenceResolver)ReferenceResolver).ReferenceFinalized += Resolver_ReferenceFinalized;
            ((ClientReferenceResolver)ReferenceResolver).UnreferencedObjectFinder = UnreferencedObjectFinder;
<<<<<<< HEAD
            StartThreads();           
=======
            StartThreads();
>>>>>>> 9536912ffdffd1251ba38664caafec2497855ae9
        }

        protected override void OnDispose()
        {
            base.OnDispose();
            ((ClientReferenceResolver)ReferenceResolver).Dispose();
        }
        
        public ISerializationBinder Binder { set => SetBinder(value); }

        public T GetRootObject<T>()
        {
            try
            {
                var queryMessage = WebSocketMessageCreate(SocketMessage.SocketMessageType.RootQuery, null, null, 0, null);
                var response = SendAndGetResponse<T>(queryMessage).Result;
                _initialObject = response;
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
                var queryMessage = WebSocketMessageCreate(
                    SocketMessage.SocketMessageType.Query,
                    dto,
                    methodName,
                    parameters.Length,
                    new SocketMessageArrayValue { Value = parameters });
<<<<<<< HEAD
                return SendAndGetResponse<T>(queryMessage).Result;
=======
                return SendAndGetResponse<T>(queryMessage);
>>>>>>> 9536912ffdffd1251ba38664caafec2497855ae9
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
                var queryMessage = WebSocketMessageCreate(
                    SocketMessage.SocketMessageType.Get,
                    dto,
                    propertyName,
                    0,
                    null
                );
<<<<<<< HEAD
                return SendAndGetResponse<T>(queryMessage).Result;
=======
                return SendAndGetResponse<T>(queryMessage);
>>>>>>> 9536912ffdffd1251ba38664caafec2497855ae9
            }
            catch (Exception e)
            {
                Logger.Error("From Get {0}: {1}", dto, e);
                throw;
            }
        }

        public void Invoke(ProxyBase dto, string methodName, params object[] parameters)
        {
            Send(WebSocketMessageCreate(
                SocketMessage.SocketMessageType.Invoke,
                dto,
                methodName,
                parameters.Length,
                new SocketMessageArrayValue{Value = parameters}));
        }

        public void Set(ProxyBase dto, object value, string propertyName)
        {
            Send(WebSocketMessageCreate(
                SocketMessage.SocketMessageType.Set,
                dto,
                propertyName,
                1,
                value));
        }

        public void EventAdd(ProxyBase dto, string eventName)
        {
            Send(WebSocketMessageCreate(
                SocketMessage.SocketMessageType.EventAdd,
                dto,
                eventName,
                0,
                null));
        }

        public void EventRemove(ProxyBase dto, string eventName)
        {
            Send(WebSocketMessageCreate(
                SocketMessage.SocketMessageType.EventRemove,
                dto,
                eventName,
                0,
                null));
        }

        protected override async Task MessageHandlerProc()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                if (_receiveQueue.IsEmpty)
                    await _messageHandlerSempahore.WaitAsync();

                if (_cancellationTokenSource.IsCancellationRequested)
                    break;

                if (!_receiveQueue.TryDequeue(out var message))
                {
                    await Task.Delay(5);
                    continue;
                }

                if (message.MessageType != SocketMessage.SocketMessageType.RootQuery && _initialObject == null)
                {
                    _receiveQueue.Enqueue(message);
                    continue;
                }
                    

                switch (message.MessageType)
                {                   
                    case SocketMessage.SocketMessageType.EventNotification:
                        _ = Task.Run(() => 
                        {
                            var notifyObject = ((ClientReferenceResolver)ReferenceResolver).ResolveReference(message.DtoGuid);
                            notifyObject?.OnEventNotificationMessage(message);
                        });                        
                        break;
                    default:
                        _receivedMessages[message.MessageGuid] = message;                        

                        if (_requestsCount>0)
                            lock (_requestsLock)
                                _sendAndResponseSemaphore.Release(_requestsCount);
                        break;
                }
            }
                
        }

        private SocketMessage WebSocketMessageCreate(SocketMessage.SocketMessageType socketMessageType, IDto dto, string memberName, int paramsCount, object value)
        {
            return new SocketMessage(value)
            {
                MessageType = socketMessageType,
                DtoGuid = dto?.DtoGuid ?? Guid.Empty,
                MemberName = memberName,
                ParametersCount = paramsCount
            };
        }

        private async Task<T> SendAndGetResponse<T>(SocketMessage query)
        {
            lock (_requestsLock)
                ++_requestsCount;
            Send(query);
            while (IsConnected)
            {
                await _sendAndResponseSemaphore.WaitAsync().ConfigureAwait(false);
                SocketMessage response;

                if (!_receivedMessages.TryRemove(query.MessageGuid, out response))
                    continue;                

                lock (_requestsLock)
                    --_requestsCount;

                //Logger.Debug($"SAGR client satisified: {response.MessageGuid}");

                if (response.MessageType == SocketMessage.SocketMessageType.UnresolvedReferenceServer)
                    return default(T);

                if (response.MessageType == SocketMessage.SocketMessageType.Exception)
                    throw Deserialize<Exception>(response);

                var result = Deserialize<T>(response);
                return result;
            }
            return default(T);
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

        private T Deserialize<T>(SocketMessage message)
        {
            using (var valueStream = message.ValueStream)
            {
                if (valueStream == null)
                    return default(T);
                using (var reader = new StreamReader(valueStream))
<<<<<<< HEAD
                    return (T)Serializer.Deserialize(reader, typeof(T));
=======
                    return (T) Serializer.Deserialize(reader, typeof(T));
>>>>>>> 9536912ffdffd1251ba38664caafec2497855ae9
            }
        }

        private ProxyBase UnreferencedObjectFinder(Guid guid)
        {
<<<<<<< HEAD
            Logger.Debug($"Unresolved reference! {guid}");
            var proxy = SendAndGetResponse<ProxyBase>(new SocketMessage((object)null)
            {
                MessageType = SocketMessage.SocketMessageType.UnresolvedReference,
                DtoGuid = guid
            }).Result;
            Logger.Debug($"Unresolved reference restored: {guid}");
            Logger.Trace("Unresolved reference {0} was restored: {1}", guid, proxy);
            return proxy;
        }       
=======
            var proxy =  SendAndGetResponse<ProxyBase>(new SocketMessage((object)null)
            {
                MessageType = SocketMessage.SocketMessageType.UnresolvedReference,
                DtoGuid = guid
            });
            Logger.Trace("Unresolved reference {0} was restored: {1}", guid, proxy);
            return proxy;
        }
        
>>>>>>> 9536912ffdffd1251ba38664caafec2497855ae9
    }
}
