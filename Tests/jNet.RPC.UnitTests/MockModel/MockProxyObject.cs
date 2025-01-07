using jNet.RPC;
using jNet.RPC.Client;

namespace jNet.RPC.UnitTests.MockModel
{
    public class MockProxyObject : ProxyObjectBase
    {
#pragma warning disable CS0649
        private string _value;
#pragma warning restore
        public string Value { get => _value; set => Set(value); }
    }
}
