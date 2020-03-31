using jNet.RPC;
using jNet.RPC.Server;

namespace jNet.RPCUnitTests.MockModel
{
    public class MockServerObject : ServerObjectBase
    {        
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
