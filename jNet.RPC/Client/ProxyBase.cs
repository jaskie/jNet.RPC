//#undef DEBUG

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using Newtonsoft.Json;

namespace jNet.RPC.Client
{
    [JsonObject(IsReference = true, MemberSerialization = MemberSerialization.OptIn)]
    public abstract class ProxyBase : IDto
    {
        private int _isDisposed;
        private bool _hasFinalized;
        private RemoteClient _client;
        private const int DisposedValue = 1;
        private static readonly ConcurrentDictionary<Guid, ProxyBase> _finalizeRequestedDtos = new ConcurrentDictionary<Guid, ProxyBase>();
       
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, DisposedValue) == default(int))
                DoDispose();
        }
        
        ~ProxyBase()
        {
            if (!_hasFinalized) //first finalization will send request to server; on response hard reference would be deleted and object collected in next GC run
            {
                _finalizeRequestedDtos.TryAdd(DtoGuid, this);
                _hasFinalized = true;
                GC.ReRegisterForFinalize(this);

                Finalized?.Invoke(this, EventArgs.Empty);
            }            
        }

        public void FinalizeProxy()
        {
            Debug.WriteLine("Proxy removed from finalizedDict {0}", DtoGuid);
            _finalizeRequestedDtos.TryRemove(DtoGuid, out _);
            Debug.WriteLine(_finalizeRequestedDtos.Count);
        }

        public static ProxyBase GetFinalizeRequestedProxy(Guid guid)
        {
            _finalizeRequestedDtos.TryGetValue(guid, out var proxy);
            return proxy;
        }

        public bool IsFinalizeRequested()
        {
            return _finalizeRequestedDtos.ContainsKey(DtoGuid);
        }

        public Guid DtoGuid { get; internal set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler Disposed;

        internal event EventHandler Finalized;        

        protected T Get<T>([CallerMemberName] string propertyName = null)
        {
            if (_isDisposed == DisposedValue)
                return default(T);
            if (string.IsNullOrEmpty(propertyName))
                return default(T);
            var result = _client.Get<T>(this, propertyName);
            Debug.WriteLine($"Get:{result} for property {propertyName} of {this}");
            return result;
        }

        protected void Set<T>(T value, [CallerMemberName] string propertyName = null)
        {
            if (_isDisposed == DisposedValue)
                return;
            Type type = GetType();
            FieldInfo field = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .FirstOrDefault(p =>
                    p.GetCustomAttributes(typeof(JsonPropertyAttribute), true)
                        .Any(a => ((JsonPropertyAttribute) a).PropertyName == propertyName));
            if (field != null)
            {
                var currentValue = (T)field.GetValue(this);
                if (EqualityComparer<T>.Default.Equals(value, currentValue))
                    return;
            }
            _client.Set(this, value, propertyName);
        }

        protected void Invoke([CallerMemberName] string methodName = null, params object[] parameters)
        {
            if (_isDisposed == DisposedValue)
                return;
            _client.Invoke(this, methodName, parameters);
        }

        protected T Query<T>([CallerMemberName] string methodName = null, params object[] parameters)
        {
            if (_isDisposed == DisposedValue)
                return default(T);
            return _client.Query<T>(this, methodName, parameters);
        }

        protected void EventAdd<T>(T handler, [CallerMemberName] string eventName = null)
        {
            if (_isDisposed == DisposedValue)
                return;
            if (handler == null && !DtoGuid.Equals(Guid.Empty))
                _client?.EventAdd(this, eventName);
        }

        protected void EventRemove<T>(T handler, [CallerMemberName] string eventName = null)
        {
            if (_isDisposed == DisposedValue)
                return;
            if (handler == null && !DtoGuid.Equals(Guid.Empty))
            {
                _client?.EventRemove(this, eventName);
            }
        }

        protected abstract void OnEventNotification(SocketMessage message);

        protected void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void DoDispose()
        {
            _client = null;
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        [OnDeserialized]
        internal void OnDeserialized(StreamingContext context)
        {
            _client = (RemoteClient)context.Context;
        }

        protected T Deserialize<T>(SocketMessage message)
        {
            if (_client == null)
                return default(T);
            using (var valueStream = message.ValueStream)
            {
                if (valueStream == null)
                    return default(T);
                using (var reader = new StreamReader(valueStream))
                    return (T) _client.Serializer.Deserialize(reader, typeof(T));
            }
        }

        internal void OnNotificationMessage(SocketMessage message)
        {
            if (message.MemberName == nameof(INotifyPropertyChanged.PropertyChanged))
            {
                var eav = Deserialize<PropertyChangedWithDataEventArgs>(message);
                if (eav == null)
                    return;
                Debug.WriteLine($"{this}: property notified {eav.PropertyName}, value {eav.Value}");
                var type = GetType();
                var field = GetField(type, eav.PropertyName);
                if (field == null)
                {
                    var property = type.GetProperty(eav.PropertyName);
                    if (property != null)
                    {
                        var value = eav.Value;
                        MethodParametersAlignment.AlignType(ref value, property.PropertyType);
                        property.SetValue(this, value);
                    }
                }
                else
                {
                    var value = eav.Value;
                    MethodParametersAlignment.AlignType(ref value, field.FieldType);
                    field.SetValue(this, value);
                }
                NotifyPropertyChanged(eav.PropertyName);
            }
            else if (message.MessageType == SocketMessage.SocketMessageType.ProxyFinalized)
                FinalizeProxy();

            else OnEventNotification(message);
        }

        protected FieldInfo GetField(Type t, string fieldName)
        {
            if (t == null)
                return null;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            var foundField = t.GetFields(flags).FirstOrDefault(f => f.GetCustomAttributes(typeof(JsonPropertyAttribute), true).Any(a =>((JsonPropertyAttribute)a).PropertyName == fieldName));
            return foundField ?? GetField(t.BaseType, fieldName);
        }


    }
}
