﻿using Newtonsoft.Json.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace jNet.RPC.Server
{
    internal sealed class ServerSession : SocketConnection
    {
        private readonly Dictionary<DelegateKey, Delegate> _delegates = new Dictionary<DelegateKey, Delegate>();
        private readonly IPrincipal _sessionUser;
        private readonly IPEndPoint _remoteAddress;
        private readonly IDto _rootObject;
        private readonly ReferenceResolver _referenceResolver = new ReferenceResolver();
        private readonly object _sendLock = new object();
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public ServerSession(TcpClient client, IDto rootObject, IPrincipal sessionUser): base(client)
        {
            _remoteAddress = client.Client.RemoteEndPoint as IPEndPoint ?? throw new ArgumentException("Client RemoteEndpoint is invalid");
            _sessionUser = sessionUser;
            Serializer.SerializationBinder = new SerializationBinder();
            _rootObject = rootObject;
            Logger.Info("Remote {0} from {1} successfully connected", _sessionUser.Identity, _remoteAddress);
            _referenceResolver.ReferencePropertyChanged += ReferenceResolver_ReferencePropertyChanged;
            StartThreads();
        }

#if DEBUG
        ~ServerSession()
        {
            Debug.WriteLine("Finalized: {0} for {1}", this, _rootObject);
        }
#endif

        protected override IReferenceResolver GetReferenceResolver() => _referenceResolver;

        protected override void ReadThreadProc()
        {
            Thread.CurrentPrincipal = _sessionUser;
            base.ReadThreadProc();
        }

        protected override void WriteThreadProc()
        {
            Thread.CurrentPrincipal = _sessionUser;
            base.WriteThreadProc();
        }

        protected override void MessageHandlerProc()
        {
            Thread.CurrentPrincipal = _sessionUser;
            while (!IsCancelled)
            {
                try
                {
                    var message = TakeNextMessage();
                    Logger.Trace("Processing message: {0}", message);
                    if (message.MessageType == SocketMessageType.RootQuery)
                    {
                        SendResponse(message, _rootObject);
                        continue;
                    }
                    if (message.MessageType == SocketMessageType.ProxyMissing)
                    {
                        var dto = _referenceResolver.FindMissingDto(message.DtoGuid);
                        SendResponse(message, dto);
                        continue;
                    }
                    // method of particular object
                    var objectToInvoke = _referenceResolver.ResolveReference(message.DtoGuid);
                    if (objectToInvoke != null)
                    {
                        switch (message.MessageType)
                        {
                            case SocketMessageType.MethodExecute:
                                var objectToInvokeType = objectToInvoke.GetType();
                                var methodToInvoke = objectToInvokeType.GetMethods()
                                    .FirstOrDefault(m => m.Name == message.MemberName &&
                                                         m.GetParameters().Length == message.ParametersCount);
                                if (methodToInvoke != null)
                                {
                                    var parameters = Deserialize<SocketMessageArrayValue>(message);
                                    var methodParameters = methodToInvoke.GetParameters();
                                    for (var i = 0; i < methodParameters.Length; i++)
                                        MethodParametersAlignment.AlignType(ref parameters.Value[i],
                                            methodParameters[i].ParameterType);
                                    object response = null;
                                    try
                                    {
                                        response = methodToInvoke.Invoke(objectToInvoke, parameters.Value);
                                    }
                                    catch (Exception e)
                                    {
                                        SendException(message, e);
                                        throw;
                                    }
                                    SendResponse(message, response);
                                }
                                else
                                    throw new ApplicationException(
                                        $"Server: unknown method: {objectToInvoke}:{message.MemberName}");
                                break;
                            case SocketMessageType.PropertyGet:
                                var getProperty = objectToInvoke.GetType().GetProperty(message.MemberName);
                                if (getProperty != null)
                                {
                                    object response;
                                    try
                                    {
                                        response = getProperty.GetValue(objectToInvoke, null);
                                    }
                                    catch (Exception e)
                                    {
                                        SendException(message, e);
                                        throw;
                                    }
                                    SendResponse(message, response);
                                }
                                else
                                    throw new ApplicationException(
                                        $"Server: unknown property: {objectToInvoke}:{message.MemberName}");
                                break;
                            case SocketMessageType.PropertySet:
                                var setProperty = objectToInvoke.GetType().GetProperty(message.MemberName);
                                if (setProperty != null)
                                {
                                    var parameter = Deserialize<object>(message);
                                    MethodParametersAlignment.AlignType(ref parameter, setProperty.PropertyType);
                                    try
                                    {
                                        setProperty.SetValue(objectToInvoke, parameter, null);
                                        SendResponse(message, null);
                                    }
                                    catch (Exception e)
                                    {
                                        SendException(message, e);
                                        throw;
                                    }
                                }
                                else
                                    throw new ApplicationException(
                                        $"Server: unknown property: {objectToInvoke}:{message.MemberName}");
                                break;
                            case SocketMessageType.EventAdd:
                            case SocketMessageType.EventRemove:
                                var ei = objectToInvoke.GetType().GetEvent(message.MemberName);
                                if (ei != null)
                                {
                                    if (message.MessageType == SocketMessageType.EventAdd)
                                        AddDelegate(objectToInvoke, ei);
                                    else if (message.MessageType == SocketMessageType.EventRemove)
                                        RemoveDelegate(objectToInvoke, ei);
                                }
                                else
                                    throw new ApplicationException(
                                        $"Server: unknown event: {objectToInvoke}:{message.MemberName}");
                                SendResponse(message, null);
                                break;
                            case SocketMessageType.ProxyFinalized:
                                _referenceResolver.RemoveReference(objectToInvoke.DtoGuid);
                                SendResponse(message, null);
                                break;
                            case SocketMessageType.ProxyResurrected:
                                _referenceResolver.RestoreReference(objectToInvoke);
                                break;
                        }
                    }
                    else
                    {
                        Logger.Warn("Dto send by client not found, message {0}", message);
                        SendResponse(message, null);
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
            }
        }

        private void SendException(SocketMessage message, Exception exception)
        {
            var value = new Exception(exception.Message, exception.InnerException == null ? null : new Exception(exception.InnerException.Message));
            var response = new SocketMessage(message.MessageGuid, SocketMessageType.Exception, message.DtoGuid, message.MemberName, value);
            Send(response);
        }

        protected override void OnDispose()
        {
            base.OnDispose();
            _referenceResolver.ReferencePropertyChanged -= ReferenceResolver_ReferencePropertyChanged;
            lock (((IDictionary) _delegates).SyncRoot)
            {
                foreach (var d in _delegates.Keys.ToArray())
                {
                    var havingDelegate = _referenceResolver.ResolveReference(d.DtoGuid);
                    if (havingDelegate == null)
                        throw new ApplicationException("Referenced object not found");
                    var ei = havingDelegate.GetType().GetEvent(d.EventName);
                    RemoveDelegate(havingDelegate, ei);
                }
            }
            _referenceResolver.Dispose();
            Logger.Info("Remote {0} from {1} disconnected", _sessionUser.Identity, _remoteAddress);
        }

        private void SendResponse(SocketMessage message, object response)
        {
            Send(new SocketMessage(message.MessageGuid, message.MessageType, message.DtoGuid, message.MemberName, response));
        }

        private protected override void Send(SocketMessage message)
        {
            lock (_sendLock)
                base.Send(message);
        }

        private void AddDelegate(IDto objectToInvoke, EventInfo ei)
        {
            var signature = new DelegateKey(objectToInvoke.DtoGuid, ei.Name);
            lock (((IDictionary) _delegates).SyncRoot)
            {
                if (_delegates.ContainsKey(signature))
                    return;
                var delegateToInvoke = ConvertDelegate((Action<object, EventArgs>) delegate(object o, EventArgs ea) { NotifyClient(objectToInvoke, ea, ei.Name); }, ei.EventHandlerType);
                _delegates[signature] = delegateToInvoke;
                ei.AddEventHandler(objectToInvoke, delegateToInvoke);
                Logger.Trace("Server: added delegate {0} on {1}", signature, objectToInvoke);
            }
        }

        private void RemoveDelegate(IDto objectToInvoke, EventInfo ei)
        {
            var signature = new DelegateKey(objectToInvoke.DtoGuid, ei.Name);
            lock (((IDictionary) _delegates).SyncRoot)
            {
                if (_delegates.TryGetValue(signature, out var delegateToRemove) &&
                    _delegates.Remove(signature))
                {
                    ei.RemoveEventHandler(objectToInvoke, delegateToRemove);
                    Logger.Trace("Server: removed delegate {0} on {1}", signature, objectToInvoke);
                }
            }
        }

        private static Delegate ConvertDelegate(Delegate originalDelegate, Type targetDelegateType)
        {
            return Delegate.CreateDelegate(
                targetDelegateType,
                originalDelegate.Target,
                originalDelegate.Method);
        }

        private void NotifyClient(IDto dto, EventArgs e, string eventName)
        {
            try
            {
                if (e is WrappedEventArgs ea
                    && ea.Args is PropertyChangedEventArgs propertyChangedEventArgs
                    && eventName == nameof(INotifyPropertyChanged.PropertyChanged))
                {
                    var p = dto.GetType().GetProperty(propertyChangedEventArgs.PropertyName);
                    PropertyChangedWithValueEventArgs value;
                    if (p?.CanRead == true)
                        value = new PropertyChangedWithValueEventArgs(propertyChangedEventArgs.PropertyName, p.GetValue(dto, null));
                    else
                    {
                        value = new PropertyChangedWithValueEventArgs(propertyChangedEventArgs.PropertyName, null);
                    }
                    Send(new SocketMessage(SocketMessageType.EventNotification, dto.DtoGuid, eventName, 0, value));
                }
                else
                    Send(new SocketMessage(SocketMessageType.EventNotification, dto.DtoGuid, eventName, 0, e));
            }
            catch (Exception exception)
            {
                Logger.Error(exception);
            }
        }

        private void ReferenceResolver_ReferencePropertyChanged(object sender, WrappedEventArgs e)
        {
            NotifyClient(e.Dto, e, nameof(INotifyPropertyChanged.PropertyChanged));
        }

        private T Deserialize<T>(SocketMessage message)
        {
            using (var stream = message.GetValueStream())
            {
                if (stream is null)
                    return default(T);
                using (var reader = new StreamReader(stream, Encoding.Default, false))
                    return (T)Serializer.Deserialize(reader, typeof(T));
            }
        }
    }
}
