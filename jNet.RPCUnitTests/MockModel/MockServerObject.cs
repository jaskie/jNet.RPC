using jNet.RPC;
using jNet.RPC.Server;
using Newtonsoft.Json;

namespace jNet.RPCUnitTests.MockModel
{
    public class MockServerObject : ServerObjectBase, IMockObject
    {
        [DtoMember]
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
