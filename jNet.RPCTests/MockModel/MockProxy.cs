using jNet.RPC;
using jNet.RPC.Client;
using Newtonsoft.Json;

namespace jNet.RPCTests.MockModel
{
    public class MockProxy : ProxyBase, IMockObject
    {
        [JsonProperty(nameof(IMockObject.Value))]
        private string _value;
        public string Value { get => _value; set => Set(value); }

        protected override void OnEventNotification(SocketMessage message)
        {
            
        }
    }
}
