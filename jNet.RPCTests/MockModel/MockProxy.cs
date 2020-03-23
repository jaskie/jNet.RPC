using jNet.RPC;
using jNet.RPC.Client;

namespace jNet.RPCTests.MockModel
{
    public class MockProxy : ProxyObjectBase, IMockObject
    {
        [DtoField(nameof(IMockObject.Value))]
        private string _value;
        public string Value { get => _value; set => Set(value); }

        protected override void OnEventNotification(SocketMessage message)
        {
            
        }
    }
}
