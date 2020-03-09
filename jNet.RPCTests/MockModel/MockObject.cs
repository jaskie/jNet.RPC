using jNet.RPC.Server;

namespace jNet.RPCTests.MockModel
{
    public class MockObject : DtoBase
    {
        public string Value { get; }

        public MockObject()
        {

        }

        public MockObject(string value)
        {
            Value = value;
        }
    }
}
