﻿#undef DEBUG

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading;

namespace jNet.RPC.Client
{
    public abstract class ProxyObjectBase : IDto
    {
        private int _isDisposed;
        private int _isFinalizeRequested;
        private RemoteClient _client;
        
        internal static readonly ConcurrentDictionary<Guid, ProxyObjectBase> FinalizeRequested = new ConcurrentDictionary<Guid, ProxyObjectBase>();


        internal static bool TryResurect(Guid dtoGuid, out ProxyObjectBase finalizeRequested)
        {
            if (FinalizeRequested.TryGetValue(dtoGuid, out finalizeRequested))
            {
                finalizeRequested.Resurrect();
                return true;
            }
            return false;
        }

        internal static bool TryGetFinalizeRequested(Guid dtoGuid, out ProxyObjectBase finalizeRequested)
        {
            return FinalizeRequested.TryGetValue(dtoGuid, out finalizeRequested);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) == default)
                DoDispose();
        }
        
        ~ProxyObjectBase()
        {
            if (Interlocked.Exchange(ref _isFinalizeRequested, 1) == default) //first finalization will send request to server; on response hard reference will be deleted and object collected in next GC run
            {
                if (!FinalizeRequested.TryAdd(DtoGuid, this))
                    Debug.WriteLine($"Could not save object {DtoGuid}");
                else
                    Debug.WriteLine($"Saving object {DtoGuid}");
                Finalized?.Invoke(this, EventArgs.Empty);
                GC.ReRegisterForFinalize(this);
            }
            else
            {
                Debug.WriteLine($"Proxy {DtoGuid} finalized");
            }                
        }


        public void Resurrect()
        {                        
            FinalizeRequested.TryRemove(DtoGuid, out _);
            _isFinalizeRequested = default;
            Debug.WriteLine($"Trying to resurrect {DtoGuid}");
            Resurrected?.Invoke(this, EventArgs.Empty);            
        }

        public void FinalizeProxy()
        {
            FinalizeRequested.TryRemove(DtoGuid, out _);
            Debug.WriteLine("Proxy strong reference delete {0}", DtoGuid);
            Debug.WriteLine(FinalizeRequested.Count);
        }                

        public Guid DtoGuid { get; internal set; }        

        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler Disposed;
        
        internal event EventHandler Resurrected;
        internal event EventHandler Finalized;        

        protected T Get<T>([CallerMemberName] string propertyName = null)
        {
            if (_isDisposed != default)
                return default;
            if (string.IsNullOrEmpty(propertyName))
                return default;
            var result = _client.Get<T>(this, propertyName);
            Debug.WriteLine($"Get:{result} for property {propertyName} of {this}");
            return result;
        }

        protected void Set<T>(T value, [CallerMemberName] string propertyName = null)
        {
            if (_isDisposed != default)
                return;
            Type type = typeof(T);
            FieldInfo field =  GetField(type, propertyName);
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
            if (_isDisposed != default)
                return;
            _client.Invoke(this, methodName, parameters);
        }

        protected T Query<T>([CallerMemberName] string methodName = null, params object[] parameters)
        {
            if (_isDisposed != default)
                return default;
            return _client.Query<T>(this, methodName, parameters);
        }

        protected void EventAdd<T>(T handler, [CallerMemberName] string eventName = null)
        {
            if (_isDisposed != default)
                return;
            if (handler == null && !DtoGuid.Equals(Guid.Empty))
                _client?.EventAdd(this, eventName);
        }

        protected void EventRemove<T>(T handler, [CallerMemberName] string eventName = null)
        {
            if (_isDisposed != default)
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
                return default;
            using (var valueStream = message.ValueStream)
            {
                if (valueStream == null)
                    return default;
                using (var reader = new StreamReader(valueStream))
                    return (T) _client.Serializer.Deserialize(reader, typeof(T));
            }
        }

        internal void OnNotificationMessage(SocketMessage message)
        {
            if (message.MemberName == nameof(INotifyPropertyChanged.PropertyChanged))
            {
                var eav = Deserialize<PropertyChangedWithValueEventArgs>(message);
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
            else OnEventNotification(message);
        }

        protected FieldInfo GetField(Type t, string fieldName)
        {
            if (t == null)
                return null;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            var foundField = t.GetFields(flags).FirstOrDefault(f => f.GetCustomAttributes(typeof(DtoMemberAttribute), true).Any(a =>((DtoMemberAttribute)a).PropertyName == fieldName));
            return foundField ?? GetField(t.BaseType, fieldName);
        }


    }
}
