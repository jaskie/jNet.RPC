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

            if (_knownDtos.TryGetValue(id, out var wRef) && wRef.TryGetTarget(out var target))
            {
                _knownDtos[id] = new WeakReference<ProxyBase>(proxy, true);
                proxy.Finalized += Proxy_FinalizedChanged;
                proxy.Resurrected += Proxy_ResurrectedChanged;

                target.Finalized -= Proxy_FinalizedChanged;
                target.Resurrected -= Proxy_ResurrectedChanged;
                target.FinalizeProxy();

                Logger.Debug("AddReference updated {0} for {1}", reference, value);
            }
            else if (_knownDtos.TryAdd(id, new WeakReference<ProxyBase>(proxy, true)))
            {
                proxy.Finalized += Proxy_FinalizedChanged;
                proxy.Resurrected += Proxy_ResurrectedChanged;
                Logger.Debug("AddReference {0} for {1}", reference, value);
            }
            
            else
                Logger.Warn("AddReference error {0}", id);
        }        

        public string GetReference(object context, object value)
        {
            if (!(value is ProxyBase proxy)) 
                return string.Empty;
            
            if (IsReferenced(context, value))
            {
                Logger.Debug("GetReference returning existsing dto. {0}", proxy.DtoGuid);
                return proxy.DtoGuid.ToString();
            }

            _knownDtos[proxy.DtoGuid] = new WeakReference<ProxyBase>(proxy, true);            
            proxy.Finalized += Proxy_FinalizedChanged;
            
            Logger.Warn("GetReference added {0} for {1}", proxy.DtoGuid, value);
            return proxy.DtoGuid.ToString();                      
        }


        public bool IsReferenced(object context, object value)
        {
            if (!(value is IDto dto))
                return false;
            
            if (ProxyBase.FinalizeRequested.TryGetValue(dto.DtoGuid, out var proxy))
            {
                proxy.Resurrect();               
                return true;
            }

            else if (_knownDtos.TryGetValue(dto.DtoGuid, out var reference))
            {
                if (!reference.TryGetTarget(out _))
                    Logger.Warn("Referenced found but failed to retrieve target! dto: {0}", dto.DtoGuid);

                return true;
            }

            return false;
        }

        public object ResolveReference(object context, string reference)
        {
            var id = new Guid(reference);

            if (_knownDtos.TryGetValue(id, out var value) && value.TryGetTarget(out var target))
            {
                Logger.Trace("Resolved reference {0} with {1}", reference, value);
                //Debug.Assert(value == null);
                return target;
            }

            else if (ProxyBase.FinalizeRequested.TryGetValue(id, out target))
            {
                target.Resurrect();
                return target;
            }
                  
            else
            {
                Logger.Debug("Unknown reference: {0}", reference);
            }
                
            return null;
        }

        #endregion //IReferenceResolver

        internal event EventHandler<ProxyBaseEventArgs> ReferenceFinalized;        
        internal Func<Guid, Task<ProxyBase>> UnreferencedObjectFinder;       

        internal ProxyBase ResolveReference(Guid reference)
        {
            if (_knownDtos.TryGetValue(reference, out var p) && p.TryGetTarget(out var target))
                return target;

            //else if (ProxyBase.FinalizeRequested.TryGetValue(reference, out target))
            //{
            //    target.Resurrect();
            //    return target;
            //}

            return null;            
        }

        public void DeleteReference(Guid reference)
        {
            if (ProxyBase.FinalizeRequested.TryGetValue(reference, out var proxy))
            {
                proxy.Finalized -= Proxy_FinalizedChanged;
                proxy.Resurrected -= Proxy_ResurrectedChanged;
                proxy.FinalizeProxy();                
                return;
            }
            Logger.Warn("Could not finalize resurrected proxy {0}", reference.ToString());
        }

        private void Proxy_ResurrectedChanged(object sender, EventArgs e)
        {
            if (!(sender is ProxyBase proxy))
                return;

            if (!_knownDtos.TryAdd(proxy.DtoGuid, new WeakReference<ProxyBase>(proxy)))
                Logger.Debug("Could not restore to knownDto list {0}", proxy.DtoGuid);            

            Logger.Debug("Proxy resurrected {0}", proxy.DtoGuid);
        }

        private void Proxy_FinalizedChanged(object sender, EventArgs e)
        {            
            if (!(sender is ProxyBase proxy))
                return;
            Logger.Debug("Deleting from knowndtos {0}", proxy.DtoGuid);
            _knownDtos.TryRemove(proxy.DtoGuid, out _);
            ReferenceFinalized?.Invoke(this, new ProxyBaseEventArgs(proxy));                       
        }

    }

}
