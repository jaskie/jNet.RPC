using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace jNet.RPC.Client
{
    internal class ClientReferenceResolver : IReferenceResolver, IDisposable
    {
        private readonly Dictionary<Guid, WeakReference<ProxyBase>> _knownDtos = new Dictionary<Guid, WeakReference<ProxyBase>>();        
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

            lock(ProxyBase.Sync)
            {                
                if (!_knownDtos.ContainsKey(id))
                {
                    _knownDtos.Add(id, new WeakReference<ProxyBase>(proxy, true));
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
            if (!(value is ProxyBase proxy)) 
                return string.Empty;

            return proxy.DtoGuid.ToString();                       
        }


        public bool IsReferenced(object context, object value)
        {
            if (!(value is IDto dto))
                return false;
            
            return true;
            //lock (_knownDtos)
            //{
            //    if (ProxyBase.FinalizeRequested.TryGetValue(dto.DtoGuid, out var proxy))
            //    {
            //        proxy.Resurrect();
            //        return true;
            //    }

            //    else if (_knownDtos.TryGetValue(dto.DtoGuid, out var reference))
            //    {
            //        if (!reference.TryGetTarget(out _))
            //            Logger.Warn("Referenced found but failed to retrieve target! dto: {0}", dto.DtoGuid);

            //        return true;
            //    }
            //    return false;
            //}
        }

        public object ResolveReference(object context, string reference)
        {
            var id = new Guid(reference);

            lock(ProxyBase.Sync)
            {
                if (_knownDtos.TryGetValue(id, out var value))
                {
                    if (!value.TryGetTarget(out var target))
                    {
                        Logger.Debug("Could not get target {0}", id);
                        if (ProxyBase.FinalizeRequested.TryGetValue(id, out target))
                        {
                            target.Resurrect();
                        }
                    }
                    Logger.Trace("Resolved reference {0} with {1}", reference, value);                   
                    return target;
                }

                else if (ProxyBase.FinalizeRequested.TryGetValue(id, out var target))
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

        internal event EventHandler<ProxyBaseEventArgs> ReferenceFinalized;    

        internal ProxyBase ResolveReference(Guid reference)
        {
            lock(ProxyBase.Sync)
            {
                if (_knownDtos.TryGetValue(reference, out var p) && p.TryGetTarget(out var target))
                    return target;

                return null;
            }                     
        }

        public List<ProxyBase> ProxiesToPopulate { get; } = new List<ProxyBase>();

        public void DeleteReference(Guid reference)
        {
            lock(ProxyBase.Sync)
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
        }

        private void Proxy_ResurrectedChanged(object sender, EventArgs e)
        {
            if (!(sender is ProxyBase proxy))
                return;

            try
            {    
                lock(ProxyBase.Sync)
                {
                    _knownDtos.Add(proxy.DtoGuid, new WeakReference<ProxyBase>(proxy, true));
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
            if (!(sender is ProxyBase proxy))
                return;

            lock(ProxyBase.Sync)
            {
                Logger.Debug("Deleting from knowndtos {0}", proxy.DtoGuid);
                _knownDtos.Remove(proxy.DtoGuid);
                ReferenceFinalized?.Invoke(this, new ProxyBaseEventArgs(proxy));
            }            
        }

    }

}
