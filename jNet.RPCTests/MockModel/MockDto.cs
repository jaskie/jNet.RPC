using jNet.RPC;
using jNet.RPC.Server;

namespace jNet.RPCTests.MockModel
{
    public class MockDto : ServerObjectBase, IMockObject
    {
        [DtoField]
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
