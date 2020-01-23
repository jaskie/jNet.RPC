using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
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
            if (!_knownDtos.TryAdd(id, new WeakReference<ProxyBase>(proxy)))
                Logger.Warn($"Reference {id} already exists in knownDtos.");
            
            proxy.Finalized += Proxy_Finalized;
            Logger.Trace("AddReference {0} for {1}", reference, value);
        }

        public string GetReference(object context, object value)
        {
            if (!(value is ProxyBase proxy)) return
                string.Empty;

            if (IsReferenced(context, value))
                return proxy.DtoGuid.ToString();
            _knownDtos[proxy.DtoGuid] = new WeakReference<ProxyBase>(proxy);

            proxy.Finalized += Proxy_Finalized;
            Logger.Warn("GetReference added {0} for {1}", proxy.DtoGuid, value);
            return proxy.DtoGuid.ToString();
        }


        public bool IsReferenced(object context, object value)
        {
            if (!(value is IDto dto))
                return false;

            if (_knownDtos.TryRemove(dto.DtoGuid, out var reference))
            { 
                if (reference.TryGetTarget(out _))
                    return true;
                _knownDtos.TryRemove(dto.DtoGuid, out _);
            }
            return false;
        }

        public object ResolveReference(object context, string reference)
        {
            var id = new Guid(reference);

            if (!_knownDtos.TryGetValue(id, out var value))
                return UnreferencedObjectFinder(id);

            Logger.Trace("Resolved reference {0} with {1}", reference, value);
            if (value.TryGetTarget(out var target))
                return target;

            _knownDtos.TryRemove(id, out _);
            return null;
        }

        #endregion //IReferenceResolver

        internal event EventHandler<ProxyBaseEventArgs> ReferenceFinalized;
        internal Func<Guid, ProxyBase> UnreferencedObjectFinder;       

        internal ProxyBase ResolveReference(Guid reference)
        {
            if (!_knownDtos.TryGetValue(reference, out var p))
                return UnreferencedObjectFinder(reference);
            if (p.TryGetTarget(out var target))
                return target;
            _knownDtos.TryRemove(reference, out _);
            return null;
        }

        private void Proxy_Finalized(object sender, EventArgs e)
        {
            Debug.Assert(sender is ProxyBase);
            ((ProxyBase)sender).Finalized -= Proxy_Finalized;
            try
            {
                if (_knownDtos.TryRemove(((ProxyBase)sender).DtoGuid, out _))
                {
                    Logger.Trace("Reference resolver - object {0} disposed, generation is {1}", sender,
                        GC.GetGeneration(sender));
                }
                ReferenceFinalized?.Invoke(this, new ProxyBaseEventArgs((ProxyBase) sender));
            }
            catch
            {
                // ignored because invoked in garbage collector thread
            }
        }

    }

}
