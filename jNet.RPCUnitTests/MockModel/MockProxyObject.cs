using jNet.RPC;
using jNet.RPC.Client;

namespace jNet.RPCUnitTests.MockModel
{
    public class MockProxyObject : ProxyObjectBase, IMockObject
    {
        [DtoMember(nameof(IMockObject.Value))]
        private string _value;
        public string Value { get => _value; set => Set(value); }

        protected override void OnEventNotification(SocketMessage message)
        {
            
        }
    }
}
