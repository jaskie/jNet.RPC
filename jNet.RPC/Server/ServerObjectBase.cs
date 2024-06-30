using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
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
            AllDtos.TryAdd(DtoGuid, new WeakReference<ServerObjectBase>(this));
        }

        [XmlIgnore]
        public Guid DtoGuid { get; } = Guid.NewGuid();

        ~ServerObjectBase()
        {
            AllDtos.TryRemove(DtoGuid, out var _);
        }

        protected virtual bool SetField<T>(ref T field, T value, [CallerMemberName]string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            NotifyPropertyChanged(propertyName);
            return true;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


    }


}
