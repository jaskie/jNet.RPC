using System;
using System.Collections.Generic;
using System.Threading;

namespace jNet.RPC.Client
{
    internal class ClientReferenceResolver : Newtonsoft.Json.Serialization.IReferenceResolver, IDisposable
    {
        private readonly Dictionary<Guid, WeakReference<ProxyObjectBase>> _knownDtos = new Dictionary<Guid, WeakReference<ProxyObjectBase>>();        
        private int _disposed;
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != default)
                return;

            _knownDtos.Clear();
        }

        #region IReferenceResolver
        public void AddReference(object context, string reference, object value)
        {
            if (!(value is ProxyObjectBase proxy))
                return;
            var id = new Guid(reference);
            proxy.DtoGuid = id;

            lock(ProxyObjectBase.Sync)
            {                
                if (!_knownDtos.ContainsKey(id))
                {
                    _knownDtos.Add(id, new WeakReference<ProxyObjectBase>(proxy, true));
                    proxy.Finalized += Proxy_FinalizedChanged;
                    proxy.Resurrected += Proxy_ResurrectedChanged;
                    Logger.Debug("AddReference {0} for {1}", reference, value);                    
                }
                else
                {                    
                    ProxiesToPopulate.Add(proxy);                    
                    Logger.Warn("AddReference already in knownDtos, will populate {0}:{1}:{2}", id, reference, proxy.DtoGuid);
                }                    
            }            
        }        

        public string GetReference(object context, object value)
        {
            if (!(value is ProxyObjectBase proxy)) 
                return string.Empty;

            return proxy.DtoGuid.ToString();                       
        }


        public bool IsReferenced(object context, object value)
        {
            if (!(value is IDto))
                return false;
            return true;            
        }

        public object ResolveReference(object context, string reference)
        {
            var id = new Guid(reference);

            lock(ProxyObjectBase.Sync)
            {
                if (_knownDtos.TryGetValue(id, out var value))
                {
                    if (!value.TryGetTarget(out var target))
                    {
                        Logger.Debug("Could not get target {0}", id);
                        if (ProxyObjectBase.FinalizeRequested.TryGetValue(id, out target))
                        {
                            target.Resurrect();
                        }
                    }
                    Logger.Trace("Resolved reference {0} with {1}", reference, value);

                    if (target == null)
                        Logger.Debug("NULL ON TARGET! {0}", reference);

                    return target;
                }

                else if (ProxyObjectBase.FinalizeRequested.TryGetValue(id, out var target))
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
        }

        #endregion //IReferenceResolver

        internal event EventHandler<ProxyObjectBaseEventArgs> ReferenceFinalized;
        internal event EventHandler<ProxyObjectBaseEventArgs> ReferenceResurrected;

        internal ProxyObjectBase ResolveReference(Guid reference)
        {
            lock(ProxyObjectBase.Sync)
            {
                if (_knownDtos.TryGetValue(reference, out var p) && p.TryGetTarget(out var target))
                    return target;

                return null;
            }                     
        }

        public List<ProxyObjectBase> ProxiesToPopulate { get; } = new List<ProxyObjectBase>();

        public void DeleteReference(Guid reference)
        {
            lock(ProxyObjectBase.Sync)
            {
                if (ProxyObjectBase.FinalizeRequested.TryGetValue(reference, out var proxy))
                {
                    proxy.Finalized -= Proxy_FinalizedChanged;
                    proxy.Resurrected -= Proxy_ResurrectedChanged;
                    proxy.FinalizeProxy();
                    return;
                }
                Logger.Warn("Could not finalize resurrected proxy {0}", reference.ToString());
            }            
        }

        private void Proxy_ResurrectedChanged(object sender, EventArgs e)
        {
            if (!(sender is ProxyObjectBase proxy))
                return;

            try
            {    
                lock(ProxyObjectBase.Sync)
                {
                    _knownDtos.Add(proxy.DtoGuid, new WeakReference<ProxyObjectBase>(proxy, true));
                    ReferenceResurrected?.Invoke(this, new ProxyObjectBaseEventArgs(proxy));
                }                
            }
            catch
            {
                Logger.Debug("Could not restore to knownDto list {0}", proxy.DtoGuid);
            }
                

            Logger.Debug("Proxy resurrected {0}", proxy.DtoGuid);
        }

        private void Proxy_FinalizedChanged(object sender, EventArgs e)
        {            
            if (!(sender is ProxyObjectBase proxy))
                return;

            lock(ProxyObjectBase.Sync)
            {
                Logger.Debug("Deleting from knowndtos {0}", proxy.DtoGuid);
                _knownDtos.Remove(proxy.DtoGuid);
                ReferenceFinalized?.Invoke(this, new ProxyObjectBaseEventArgs(proxy));
            }            
        }

    }

}
