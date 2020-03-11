using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using Newtonsoft.Json.Serialization;

namespace jNet.RPC.Server
{
    internal class ServerReferenceResolver : IReferenceResolver, IDisposable
    {
        private readonly Dictionary<Guid, DtoBase> _knownDtos = new Dictionary<Guid, DtoBase>();        
        public static readonly object Sync = new object();
        private int _disposed;
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();


#if DEBUG
        ~ServerReferenceResolver()
        {
            Debug.WriteLine("Finalized: {0}", this);
        }
#endif
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != default(int))
                return;

            lock(Sync)
            {
                var allKeys = _knownDtos.Keys;
                foreach (var key in allKeys)
                {
                    if (!_knownDtos.TryGetValue(key, out var removed))
                        continue;
                    removed.PropertyChanged -= Dto_PropertyChanged;
                    _knownDtos.Remove(key);
                }
            }            
        }

        #region IReferenceResolver
        public void AddReference(object context, string reference, object value)
        {
            throw new InvalidOperationException(nameof(AddReference));
        }

        public string GetReference(object context, object value)
        {
            if (!(value is DtoBase dto)) 
                return string.Empty;

            lock(Sync)
            {
                if (IsReferenced(context, value))
                    return dto.DtoGuid.ToString();

                dto.PropertyChanged += Dto_PropertyChanged;
                _knownDtos[dto.DtoGuid] = dto;
                Logger.Trace("GetReference added {0} for {1}", dto.DtoGuid, value);
                return dto.DtoGuid.ToString();
            }            
        }


        public bool IsReferenced(object context, object value)
        {
            lock(Sync)
            {
                if (value is IDto p && !p.DtoGuid.Equals(Guid.Empty))
                    return _knownDtos.ContainsKey(p.DtoGuid);
                return false;
            }            
        }

        public object ResolveReference(object context, string reference)
        {
            var id = new Guid(reference);
            lock(Sync)
            {
                if (!_knownDtos.TryGetValue(id, out var value))
                {
                    var dto = DtoBase.FindDto(id);
                    if (dto == null)
                        return null;

                    _knownDtos[id] = value;
                    Logger.Warn("Reference not found in knownDtos, but found locally - adding to known. {0}", dto.DtoGuid);
                    return dto;
                }

                Logger.Trace("ResolveReference {0} with {1}", reference, value);
                return value;
            }
            
        }

        #endregion //IReferenceResolver

        public DtoBase ResolveReference(Guid reference)
        {
            lock (Sync)
            {
                if (!_knownDtos.TryGetValue(reference, out var p))
                    return DtoBase.FindDto(reference);

                return p;
            }
        }

        internal event EventHandler<WrappedEventArgs> ReferencePropertyChanged;

        private void Dto_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!(sender is DtoBase dto))
                throw new InvalidOperationException("Object provided is not DtoBase");
            ReferencePropertyChanged?.Invoke(this, new WrappedEventArgs(dto, e));
        }


        public void RemoveReference(IDto dto)
        {
            lock(Sync)
            {
                if (!_knownDtos.TryGetValue(dto.DtoGuid, out var removed))
                    return;

                removed.PropertyChanged -= Dto_PropertyChanged;
                _knownDtos.Remove(dto.DtoGuid);
            }                            
        }
    }

}
