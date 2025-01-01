using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading;

namespace jNet.RPC.Client
{
    public abstract class ProxyObjectBase : IDto
    {
        private int _isFinalizeRequested;
        private RemoteClient _client;
        private readonly static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        ~ProxyObjectBase()
        {
            if (Interlocked.Exchange(ref _isFinalizeRequested, 1) == default) //first finalization will send request to server; on response hard reference will be deleted and object collected in next GC run
            {

                Finalized?.Invoke(this, EventArgs.Empty);
                GC.ReRegisterForFinalize(this);
            }
            else
            {
                Logger.Trace("Proxy {0} finalized", DtoGuid);
            }
        }

        internal void Resurect()
        {
            if (Interlocked.Exchange(ref _isFinalizeRequested, default) == default)
                Logger.Warn("Proxy was not requested for finalization {0}", DtoGuid);
            GC.ReRegisterForFinalize(this);
        }

        /// <summary>
        /// Exposed internally for tests
        /// </summary>
        internal int IsFinalizeRequested => _isFinalizeRequested;

        public Guid DtoGuid { get; internal set; }

        public event PropertyChangedEventHandler PropertyChanged;
       
        internal event EventHandler Finalized;

        protected T Get<T>([CallerMemberName] string propertyName = null)
        {
            if (string.IsNullOrEmpty(propertyName))
                return default;
            var result = _client.Get<T>(this, propertyName);
            return result;
        }

        protected internal void Set<T>(T value, [CallerMemberName] string propertyName = null)
        {
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

        protected internal void Invoke([CallerMemberName] string methodName = null, params object[] parameters)
        {
            _client.Invoke(this, methodName, parameters);
        }

        protected internal T Query<T>([CallerMemberName] string methodName = null, params object[] parameters)
        {
            return _client.Query<T>(this, methodName, parameters);
        }

        protected internal void EventAdd<T>(T handler, [CallerMemberName] string eventName = null)
        {
            if (handler == null && !DtoGuid.Equals(Guid.Empty))
                _client?.EventAdd(this, eventName);
        }

        protected internal void EventRemove<T>(T handler, [CallerMemberName] string eventName = null)
        {
            if (handler == null && !DtoGuid.Equals(Guid.Empty))
            {
                _client?.EventRemove(this, eventName);
            }
        }

        protected void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        [OnDeserialized]
        internal void OnDeserialized(StreamingContext context)
        {
            _client = (RemoteClient)context.Context;
        }

        /// <summary>
        /// This method provides default PropertyChanged event handling. Override it to support more events.
        /// </summary>
        /// <param name="eventName">Event name. Event with this name have to be defined in interface.</param>
        /// <param name="eventArgs">Event args, deserialized according to type info. Cast it to specific derived type/</param>
        protected internal virtual void OnEventNotification(string eventName, EventArgs eventArgs)
        {
            Logger.Trace("On NotificationMessage {0}", eventName);
            if (eventName == nameof(INotifyPropertyChanged.PropertyChanged))
            {
                var eav = eventArgs as PropertyChangedWithValueEventArgs;
                if (eav == null)
                    return;
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
