using jNet.RPC.Server;

namespace jNet.RPCTests.MockModel
{
    public class MockDto : DtoBase, IMockObject
    {
        [JsonProperty()]
        public string Value { get; }

        public MockDto()
        {

        }

        public MockDto(string value)
        {
            Value = value;
        }
    }
}
