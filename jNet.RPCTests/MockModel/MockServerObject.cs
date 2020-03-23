using jNet.RPC.Server;
using Newtonsoft.Json;

namespace jNet.RPCUnitTests.MockModel
{
    public class MockServerObject : ServerObjectBase, IMockObject
    {
        [JsonProperty(nameof(IMockObject.Value))]
        public string Value { get; set; }

        public MockServerObject()
        {

        }
        
        public MockServerObject(string value)
        {
            Value = value;
        }
    }
}
