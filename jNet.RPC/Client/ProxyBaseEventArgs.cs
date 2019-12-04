using System;

namespace jNet.RPC.Client
{
    internal class ProxyBaseEventArgs: EventArgs
    {
        public ProxyBaseEventArgs(ProxyBase proxy)
        {
            Proxy = proxy;
        }

        public ProxyBase Proxy { get; }
    }
}
