using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace jNet.RPC.Client
{
    internal class ClientReferenceResolver : Newtonsoft.Json.Serialization.IReferenceResolver, IDisposable
    {
        private readonly Dictionary<Guid, WeakReference<ProxyObjectBase>> _knownDtos = new Dictionary<Guid, WeakReference<ProxyObjectBase>>();
        private readonly Dictionary<Guid, ProxyObjectBase> _proxiesToPopulate = new Dictionary<Guid, ProxyObjectBase>();
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

            lock(((IDictionary)_knownDtos).SyncRoot)
            {                
                if (!_knownDtos.ContainsKey(id))
                {
                    _knownDtos.Add(id, new WeakReference<ProxyObjectBase>(proxy, true));
                    proxy.Finalized += Proxy_Finalized;
                    proxy.Resurrected += Proxy_Resurrected;
                    Logger.Debug("AddReference {0} for {1}", reference, value);                    
                }
                else
                {
                    _proxiesToPopulate[proxy.DtoGuid] = proxy;
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

            lock (((IDictionary)_knownDtos).SyncRoot)
            {
                if (_knownDtos.TryGetValue(id, out var value))
                {
                    if (!value.TryGetTarget(out var target))
                    {
                        Logger.Debug("Could not get target {0}", id);
                        ProxyObjectBase.TryResurect(id, out _);
                    }
                    Logger.Trace("Resolved reference {0} with {1}", reference, value);

                    if (target == null)
                        Logger.Debug("NULL ON TARGET! {0}", reference);

                    return target;
                }
                else if (ProxyObjectBase.TryResurect(id, out var target))
                    return target;
                else
                {
                    Logger.Debug("Unknown reference: {0}", reference);
                }

                return null;
            }
        }

        #endregion //IReferenceResolver

        internal event EventHandler<ProxyObjectBaseEventArgs> ReferenceFinalized;

        internal ProxyObjectBase ResolveReference(Guid reference)
        {
            lock (((IDictionary)_knownDtos).SyncRoot)
            {
                if (_knownDtos.TryGetValue(reference, out var p) && p.TryGetTarget(out var target))
                    return target;

                return null;
            }                     
        }

        internal ProxyObjectBase TakeProxyToPopulate(Guid dtoGuid)
        {
            if (_proxiesToPopulate.TryGetValue(dtoGuid, out var result))
            {
                _proxiesToPopulate.Remove(dtoGuid);
                return result;
            }
            return null;
        }

        public void DeleteReference(Guid reference)
        {
            if (ProxyObjectBase.TryGetFinalizeRequested(reference, out var proxy))
            {
                proxy.Finalized -= Proxy_Finalized;
                proxy.Resurrected -= Proxy_Resurrected;
                proxy.FinalizeProxy();
                return;
            }
            Logger.Warn("Could not finalize resurrected proxy {0}", reference.ToString());
        }

        private void Proxy_Resurrected(object sender, EventArgs e)
        {
            if (!(sender is ProxyObjectBase proxy))
                return;

            try
            {
                lock (((IDictionary)_knownDtos).SyncRoot)
                {
                    _knownDtos.Add(proxy.DtoGuid, new WeakReference<ProxyObjectBase>(proxy, true));
                }                
            }
            catch
            {
                Logger.Debug("Could not restore to knownDto list {0}", proxy.DtoGuid);
            }
                

            Logger.Debug("Proxy resurrected {0}", proxy.DtoGuid);
        }

        private void Proxy_Finalized(object sender, EventArgs e)
        {            
            if (!(sender is ProxyObjectBase proxy))
                return;

            lock (((IDictionary)_knownDtos).SyncRoot)
            {
                Logger.Debug("Deleting from knowndtos {0}", proxy.DtoGuid);
                _knownDtos.Remove(proxy.DtoGuid);
                ReferenceFinalized?.Invoke(this, new ProxyObjectBaseEventArgs(proxy));
            }            
        }

    }

}
