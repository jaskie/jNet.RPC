using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Serialization;

namespace jNet.RPC.Client
{
    internal class ClientReferenceResolver : IReferenceResolver, IDisposable
    {
        private readonly ConcurrentDictionary<Guid, WeakReference<ProxyBase>> _knownDtos = new ConcurrentDictionary<Guid, WeakReference<ProxyBase>>();        
        private int _disposed;
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != default(int))
                return;

            _knownDtos.Clear();
        }

        #region IReferenceResolver
        public void AddReference(object context, string reference, object value)
        {
            if (!(value is ProxyBase proxy))
                return;
            var id = new Guid(reference);
            proxy.DtoGuid = id;
            if (!_knownDtos.TryAdd(id, new WeakReference<ProxyBase>(proxy, true)))
                Logger.Warn("Reference {0} already exists in knownDtos.", id);
            
            proxy.FinalizedChanged += Proxy_FinalizedChanged;
            Logger.Debug("AddReference {0} for {1}", reference, value);
        }

        public string GetReference(object context, object value)
        {
            if (!(value is ProxyBase proxy)) 
                return string.Empty;

            return proxy.DtoGuid.ToString();

            //if (IsReferenced(context, value))
            //{
            //    Logger.Debug("GetReference returning existsing dto. {0}", proxy.DtoGuid);
            //    return proxy.DtoGuid.ToString();
            //}
            
            //_knownDtos[proxy.DtoGuid] = new WeakReference<ProxyBase>(proxy, true);
            //Logger.Warn("GetReference added {0} for {1}", proxy.DtoGuid, value);

            //proxy.FinalizedChanged += Proxy_FinalizedChanged;
            
            //return proxy.DtoGuid.ToString();
        }


        public bool IsReferenced(object context, object value)
        {
            if (!(value is IDto dto))
                return false;

            return true;

            //if (_knownDtos.TryGetValue(dto.DtoGuid, out var reference))
            //{
            //    if (!reference.TryGetTarget(out _))
            //        Logger.Warn("Referenced found but failed to retrieve target! dto: {0}", dto.DtoGuid);

            //    return true;
            //}

            //return false;
        }

        public object ResolveReference(object context, string reference)
        {
            var id = new Guid(reference);

            if (!_knownDtos.TryGetValue(id, out var value))
                return UnreferencedObjectFinder(id).Result;

            Logger.Trace("Resolved reference {0} with {1}", reference, value);
            if (value.TryGetTarget(out var target))
                return target;
           
            return UnreferencedObjectFinder(id).Result;
        }

        #endregion //IReferenceResolver

        internal event EventHandler<ProxyBaseEventArgs> ReferenceFinalized;
        internal Func<Guid, Task<ProxyBase>> UnreferencedObjectFinder;       

        internal ProxyBase ResolveReference(Guid reference)
        {
            if (_knownDtos.TryGetValue(reference, out var p) && p.TryGetTarget(out var target))
                return target;
            
            return null;            
        }

        public void DeleteReference(Guid reference)
        {
            if (_knownDtos.TryGetValue(reference, out var p) && p.TryGetTarget(out var proxy))
            {
                proxy.FinalizedChanged -= Proxy_FinalizedChanged;               
                proxy.FinalizeProxy();                
                return;
            }
            Logger.Warn("Could not finalize proxy {0}", reference.ToString());
        }

        private void Proxy_FinalizedChanged(object sender, EventArgs e)
        {            
            if (!(sender is ProxyBase proxy))
                return;

            if (proxy.IsFinalized)
            {
                _knownDtos.TryRemove(proxy.DtoGuid, out _);
                Logger.Debug("KnowDtosDeleted {0}", proxy.DtoGuid);
            }
            else
                ReferenceFinalized?.Invoke(this, new ProxyBaseEventArgs(proxy));                       
        }

    }

}
