﻿using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace jNet.RPC.Client
{
    public abstract class ClientSession : SocketConnection
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();       
        private readonly ConcurrentDictionary<Guid, MessageRequest> _requests = new ConcurrentDictionary<Guid, MessageRequest>();        

        public ClientSession() : base(new ClientReferenceResolver())
        {
            ((ClientReferenceResolver)ReferenceResolver).ReferenceFinalized += Resolver_ReferenceFinalized;                     
        }        

        protected override void OnDispose()
        {
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
                        Logger.Warn("SendAndGetResponse client trapped! {0}:{1}", response.MessageGuid, response.MessageType);
                        return default;
                    }

                    if (response.MessageType == SocketMessage.SocketMessageType.Exception)
                        throw Deserialize<Exception>(response);

                    var result = Deserialize<T>(response);

                    if (result == null)
                        Logger.Debug("Returned null. MessageGuid {0}:", query.MessageGuid);

                    return result;
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
                        Logger.Trace("Processing message: {0}:{1}:{2}:{3}", message.MessageGuid, message.DtoGuid, message.MemberName, message.ValueString);

                    switch (message.MessageType)
                    {
                        case SocketMessage.SocketMessageType.ProxyFinalized:
                            ((ClientReferenceResolver)ReferenceResolver).DeleteReference(message.DtoGuid);
                            break;

                        case SocketMessage.SocketMessageType.EventNotification:
                            var notifyObject = ((ClientReferenceResolver)ReferenceResolver).ResolveReference(message.DtoGuid);
                            if (notifyObject == null)
                                Logger.Debug("NotifyObject null: {0}:{1}", message.MessageGuid, message.DtoGuid);
                            else
                                Task.Run(() => notifyObject?.OnNotificationMessage(message)); //to not block calling thread
                            break;

                        default:
                            if (!_requests.TryGetValue(message.MessageGuid, out var request))
                            {
                                Logger.Warn("Message consumer not found");
                                break;
                            }
                            request.SetResult(message);
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

#if DEBUG
                    // TODO: remove
                    if (obj == null && message.MemberName.Contains("GetSucc"))
                    {
                        valueStream.Position = 0;
                        Logger.Debug("NULL ON DESERIALIZE! {0}:{1}:{2}", message.DtoGuid, message.ValueString, reader.ReadToEnd());
                    }
#endif                        

                    return obj;                    
                }
                    
            }
        }
    }
}