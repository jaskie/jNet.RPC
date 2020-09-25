using System;

namespace jNet.RPC
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class ProxyNamespaceAttribute: Attribute
    {
        public ProxyNamespaceAttribute(string serverNamespace, string clientNamespace = null)
        {
            ServerNamespace = serverNamespace;
            ClientNamespace = clientNamespace;
        }
        
        public string ServerNamespace { get; }
        public string ClientNamespace { get; }
    }
}
