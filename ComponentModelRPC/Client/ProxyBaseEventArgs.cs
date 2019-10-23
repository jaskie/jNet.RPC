using System;

namespace ComponentModelRPC.Client
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
