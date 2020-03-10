using jNet.RPC.Server;
using Newtonsoft.Json;

namespace jNet.RPCTests.MockModel
{
    public class MockDto : DtoBase, IMockObject
    {
        [JsonProperty(nameof(IMockObject.Value))]
        public string Value { get; set; }

        public MockDto()
        {

        }
        
        public MockDto(string value)
        {
            Value = value;
        }
    }
}
