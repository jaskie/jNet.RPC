using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace jNet.RPC.Client
{
    internal class ReferenceResolver : Newtonsoft.Json.Serialization.IReferenceResolver, IDisposable
    {
        private readonly Dictionary<Guid, WeakReference<ProxyObjectBase>> _knownDtos = new Dictionary<Guid, WeakReference<ProxyObjectBase>>();
        private readonly Dictionary<Guid, ProxyObjectBase> _proxiesToPopulate = new Dictionary<Guid, ProxyObjectBase>();
        private readonly Dictionary<Guid, ProxyObjectBase> _finalizeRequested = new Dictionary<Guid, ProxyObjectBase>();

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
                    Logger.Trace("AddReference {0} for {1}", reference, value);                    
                }
                else
                {
                    _proxiesToPopulate[proxy.DtoGuid] = proxy;
                    Logger.Debug("AddReference already in knownDtos, will populate {0}", proxy);
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
            ProxyObjectBase proxy;
            lock (((IDictionary)_knownDtos).SyncRoot)
            {
                if (_knownDtos.TryGetValue(id, out var value))
                {
                    if (value.TryGetTarget(out proxy))
                    {
                        if (proxy == null)
                            Logger.Warn("Proxy reference {0} target is null", reference);
                        else
                            Logger.Trace("Resolved reference {0} with {1}", reference, proxy);
                        return proxy;
                    }
                }
            }
            if (TryResurect(id, out proxy))
                return proxy;
            Logger.Debug("Unknown reference: {0}, querying server", reference);
            return OnReferenceMissing?.Invoke(id);
        }

        #endregion //IReferenceResolver

        internal event EventHandler<ProxyObjectBaseEventArgs> ReferenceFinalized;
        internal event EventHandler<ProxyObjectBaseEventArgs> ReferenceResurected;
        internal Func<Guid, IDto> OnReferenceMissing;

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
            lock (((IDictionary)_finalizeRequested).SyncRoot)
            {
                if (_finalizeRequested.TryGetValue(reference, out var proxy))
                {
                    _finalizeRequested.Remove(reference);
                    proxy.Finalized -= Proxy_Finalized;
                    return;
                }
            }
            Logger.Debug("Could not finalize proxy {0}, probably resurected", reference);
        }

        private bool TryResurect(Guid dtoGuid, out ProxyObjectBase proxy)
        {
            Logger.Trace("Trying to resurect proxy {0}", dtoGuid);
            lock (((IDictionary)_finalizeRequested).SyncRoot)
            {
                if (!_finalizeRequested.TryGetValue(dtoGuid, out proxy))
                    return false;
                _finalizeRequested.Remove(dtoGuid);
            }
            lock (((IDictionary)_knownDtos).SyncRoot)
                _knownDtos.Add(proxy.DtoGuid, new WeakReference<ProxyObjectBase>(proxy, true));
            ReferenceResurected?.Invoke(this, new ProxyObjectBaseEventArgs(proxy));
            proxy.Resurect();
            Logger.Debug("Resurected proxy {0} with {1}", dtoGuid, proxy);
            return true;
        }

        private void Proxy_Finalized(object sender, EventArgs e)
        {            
            if (!(sender is ProxyObjectBase proxy))
                return;
            lock (((IDictionary)_finalizeRequested).SyncRoot)
            {
                if (_finalizeRequested.ContainsKey(proxy.DtoGuid))
                    Logger.Warn("Could not save proxy {0}", proxy.DtoGuid);
                else
                {
                    _finalizeRequested[proxy.DtoGuid] = proxy;
                    Logger.Trace("Saved proxy {0}", proxy.DtoGuid);
                }
            }
            lock (((IDictionary)_knownDtos).SyncRoot)
            {
                Logger.Trace("Deleting from knowndtos {0}", proxy.DtoGuid);
                _knownDtos.Remove(proxy.DtoGuid);
                ReferenceFinalized?.Invoke(this, new ProxyObjectBaseEventArgs(proxy));
            }            
        }

    }

}
