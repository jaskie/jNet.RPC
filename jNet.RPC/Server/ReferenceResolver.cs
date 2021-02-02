using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Serialization;

namespace jNet.RPC.Server
{
    internal class ReferenceResolver : IReferenceResolver, IDisposable
    {
        private readonly Dictionary<Guid, ServerObjectBase> _knownDtos = new Dictionary<Guid, ServerObjectBase>();        
        public readonly object Sync = new object();
        private int _disposed;
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();


#if DEBUG
        ~ReferenceResolver()
        {
            Debug.WriteLine("Finalized: {0}", this);
        }
#endif
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != default)
                return;

            lock(Sync)
            {                
                foreach (var key in _knownDtos.Keys.ToList())
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
            if (!(value is ServerObjectBase dto)) 
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
                    var dto = ServerObjectBase.FindDto(id);
                    if (dto == null)
                        return null;

                    //_knownDtos[id] = value;
                    //Logger.Warn("Reference not found in knownDtos, but found locally - adding to known. {0}", dto.DtoGuid);
                    return dto;
                }

                Logger.Trace("ResolveReference {0} with {1}", reference, value);
                return value;
            }
            
        }

        #endregion //IReferenceResolver

        public ServerObjectBase ResolveReference(Guid reference)
        {
            lock (Sync)
            {
                if (!_knownDtos.TryGetValue(reference, out var p))
                    return ServerObjectBase.FindDto(reference);

                return p;
            }
        }

        public bool RestoreReference(IDto messageDto)
        {
            lock(Sync)
            {
                var dto = ServerObjectBase.FindDto(messageDto.DtoGuid);
                if (dto == null)
                {
                    Logger.Warn("Could not restore Dto (null on server side)! {0}", dto.DtoGuid);
                    return false;
                }
                if (_knownDtos.ContainsKey(dto.DtoGuid))
                {
                    Logger.Warn("Server already knows about this dto, nothing to restore. {0}", dto.DtoGuid);
                    return false;
                }
                _knownDtos.Add(dto.DtoGuid, dto);
                Logger.Trace("Object ressurection acknowledged {0}", dto.DtoGuid);
            }
            
            return true;
        }

        internal IDto FindMissingProxy(Guid dtoGuid)
        {
            lock (Sync)
            {
                if (_knownDtos.TryGetValue(dtoGuid, out var dto))
                    return dto;
                dto = ServerObjectBase.FindDto(dtoGuid);
                if (dto == null)
                {
                    Logger.Warn("Could not restore Dto (null on server side)! {0}", dto.DtoGuid);
                    return null;
                }
                _knownDtos[dtoGuid] = dto;
                return dto;
            }
        }


        internal event EventHandler<WrappedEventArgs> ReferencePropertyChanged;

        private void Dto_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!(sender is ServerObjectBase dto))
                throw new InvalidOperationException("Object provided is not DtoBase");
            ReferencePropertyChanged?.Invoke(this, new WrappedEventArgs(dto, e));
        }


        public void RemoveReference(Guid dtoGuid)
        {
            lock(Sync)
            {
                if (!_knownDtos.TryGetValue(dtoGuid, out var removed))
                    return;

                removed.PropertyChanged -= Dto_PropertyChanged;
                _knownDtos.Remove(dtoGuid);
            }                            
        }
    }

}
