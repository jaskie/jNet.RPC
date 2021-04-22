//#undef DEBUG

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Xml.Serialization;

namespace jNet.RPC.Server
{
    public abstract class ServerObjectBase: IDto
    {
        private static readonly ConcurrentDictionary<Guid, WeakReference<ServerObjectBase>> AllDtos = new ConcurrentDictionary<Guid, WeakReference<ServerObjectBase>>();        

        internal static ServerObjectBase FindDto(Guid guid)
        {
            if (AllDtos.TryGetValue(guid, out var reference) && reference.TryGetTarget(out var result))
                return result;

            return null;
        }

        protected ServerObjectBase()
        {
            DtoGuid = Guid.NewGuid();
            AllDtos.TryAdd(DtoGuid, new WeakReference<ServerObjectBase>(this));
        }

        [XmlIgnore]
        public Guid DtoGuid { get; }

        private int _disposed;

        ~ServerObjectBase()
        {
            AllDtos.TryRemove(DtoGuid, out var _);
            Debug.WriteLine(this, $"{GetType().FullName} Finalized");
        }

        protected virtual bool SetField<T>(ref T field, T value, [CallerMemberName]string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            NotifyPropertyChanged(propertyName);
            return true;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != default)
                return;
            DoDispose();
        }

        protected bool IsDisposed => _disposed != default;

        protected virtual void DoDispose()
        {
            
        }

        protected virtual void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


    }


}
