using jNet.RPC;
using jNet.RPC.Client;
using Tests.CommonLibrary;

namespace jNet.RPCTests.MockModel
{
    public class MockProxy : ProxyObjectBase, IMockObject
    {
#pragma warning disable CS0649
        [DtoMember(nameof(IMockObject.Value))]
        private string _value;
#pragma warning restore
        public string Value { get => _value; set => Set(value); }
    }
}
