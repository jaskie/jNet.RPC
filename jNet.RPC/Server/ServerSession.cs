//#undef DEBUG

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Principal;
using System.Threading;

namespace jNet.RPC.Server
{
    internal class ServerSession : SocketConnection
    {
        private readonly Dictionary<DelegateKey, Delegate> _delegates = new Dictionary<DelegateKey, Delegate>();
        private readonly IPrincipal _sessionUser;
        private readonly IDto _initialObject;
        private readonly ReferenceResolver _referenceResolver;
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public ServerSession(TcpClient client, IDto initialObject, IPrincipalProvider principalProvider): base(client, new ReferenceResolver())
        {
            Serializer.SerializationBinder = new SerializationBinder();
            _initialObject = initialObject;
            _referenceResolver = ReferenceResolver as ReferenceResolver ?? throw new ApplicationException("Invalid reference resolver");
            if (!(client.Client.RemoteEndPoint is IPEndPoint))
                throw new UnauthorizedAccessException("Client RemoteEndpoint is invalid");
            _sessionUser = principalProvider.GetPrincipal(client);
            if (_sessionUser == null)
                throw new UnauthorizedAccessException($"Client {Client.Client.RemoteEndPoint} not allowed");
            Logger.Info("Client {0} from {1} successfully connected", _sessionUser.Identity, Client.Client.RemoteEndPoint);
            _referenceResolver.ReferencePropertyChanged += ReferenceResolver_ReferencePropertyChanged;
            StartThreads();    
        }

#if DEBUG
        ~ServerSession()
        {
            Debug.WriteLine("Finalized: {0} for {1}", this, _initialObject);
        }
#endif

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

            while (!CancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    var message = TakeNextMessage();
                    if (message.MessageType != SocketMessage.SocketMessageType.EventNotification)
                        Logger.Trace("Processing message: {0}", message);
                    if (message.MessageType == SocketMessage.SocketMessageType.RootQuery)
                        SendResponse(message, _initialObject);
                    if (message.MessageType == SocketMessage.SocketMessageType.ProxyMissing)
                    {
                        var dto = _referenceResolver.FindMissingProxy(message.DtoGuid);
                        _referenceResolver.RemoveReference(dto.DtoGuid);
                        SendResponse(message, dto);
                    }
                    else // method of particular object
                    {
                        var objectToInvoke = ((ReferenceResolver)ReferenceResolver).ResolveReference(message.DtoGuid);
                        if (objectToInvoke != null)
                        {
                            switch (message.MessageType)
                            {
                                case SocketMessage.SocketMessageType.Query:
                                    var objectToInvokeType = objectToInvoke.GetType();
                                    var methodToInvoke = objectToInvokeType.GetMethods()
                                        .FirstOrDefault(m => m.Name == message.MemberName &&
                                                             m.GetParameters().Length == message.ParametersCount);
                                    if (methodToInvoke != null)
                                    {
                                        var parameters = DeserializeDto<SocketMessageArrayValue>(message.ValueStream);
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
                                case SocketMessage.SocketMessageType.Get:
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
                                case SocketMessage.SocketMessageType.Set:
                                    var setProperty = objectToInvoke.GetType().GetProperty(message.MemberName);
                                    if (setProperty != null)
                                    {
                                        var parameter = DeserializeDto<object>(message.ValueStream);
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
                                case SocketMessage.SocketMessageType.EventAdd:
                                case SocketMessage.SocketMessageType.EventRemove:
                                    var ei = objectToInvoke.GetType().GetEvent(message.MemberName);
                                    if (ei != null)
                                    {
                                        if (message.MessageType == SocketMessage.SocketMessageType.EventAdd)
                                            AddDelegate(objectToInvoke, ei);
                                        else if (message.MessageType == SocketMessage.SocketMessageType.EventRemove)
                                            RemoveDelegate(objectToInvoke, ei);
                                    }
                                    else
                                        throw new ApplicationException(
                                            $"Server: unknown event: {objectToInvoke}:{message.MemberName}");
                                    SendResponse(message, null);
                                    break;
                                case SocketMessage.SocketMessageType.ProxyFinalized:
                                    _referenceResolver.RemoveReference(objectToInvoke.DtoGuid);
                                    SendResponse(message, null);
                                    break;
                                case SocketMessage.SocketMessageType.ProxyResurrected:
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
            message.MessageType = SocketMessage.SocketMessageType.Exception;
            SendResponse(message, new Exception(exception.Message, exception.InnerException == null ? null : new Exception(exception.InnerException.Message)));
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
        }

        private void SendResponse(SocketMessage message, object response)
        {            
            Send(new SocketMessage(message, response));
        }


        private void AddDelegate(IDto objectToInvoke, EventInfo ei)
        {
            var signature = new DelegateKey(objectToInvoke.DtoGuid, ei.Name);
            lock (((IDictionary) _delegates).SyncRoot)
            {
                if (_delegates.ContainsKey(signature))
                    return;
                var delegateToInvoke = ConvertDelegate((Action<object, EventArgs>) delegate(object o, EventArgs ea) { NotifyClient(objectToInvoke, ea, ei.Name); }, ei.EventHandlerType);
                Debug.WriteLine($"Server: added delegate {signature} on {objectToInvoke}");
                _delegates[signature] = delegateToInvoke;
                ei.AddEventHandler(objectToInvoke, delegateToInvoke);
            }
        }

        private void RemoveDelegate(IDto objectToInvoke, EventInfo ei)
        {
            var signature = new DelegateKey(objectToInvoke.DtoGuid, ei.Name);
            lock (((IDictionary) _delegates).SyncRoot)
            {
                var delegateToRemove = _delegates[signature];
                if (!_delegates.Remove(signature))
                    return;
                ei.RemoveEventHandler(objectToInvoke, delegateToRemove);
                Debug.WriteLine($"Server: removed delegate {signature} on {objectToInvoke}");
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
                        Debug.WriteLine(dto,
                            $"{GetType()}: Couldn't get value of {propertyChangedEventArgs.PropertyName}");
                    }
                    Debug.WriteLine($"Server: PropertyChanged {propertyChangedEventArgs.PropertyName} on {dto} sent");
                    Send(new SocketMessage(value)
                    {
                        MessageType = SocketMessage.SocketMessageType.EventNotification,
                        DtoGuid = dto.DtoGuid,
                        MemberName = eventName,
                    });
                }
                else
                    Send(new SocketMessage(e)
                    {
                        MessageType = SocketMessage.SocketMessageType.EventNotification,
                        DtoGuid = dto.DtoGuid,
                        MemberName = eventName
                    });
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

    }
}
