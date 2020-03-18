using System;

namespace jNet.RPC.Client
{
    internal class ProxyObjectBaseEventArgs: EventArgs
    {
        public ProxyObjectBaseEventArgs(ProxyObjectBase proxy)
        {
            Proxy = proxy;
        }

        public ProxyObjectBase Proxy { get; }
    }
}
