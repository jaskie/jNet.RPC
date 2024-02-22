using jNet.RPC;
using jNet.RPC.Server;
using Tests.CommonLibrary;

namespace jNet.RPCTests.MockModel
{
    public class MockDto : ServerObjectBase, IMockObject
    {
        [DtoMember]
        public string Value { get; set; }

        public MockDto(string value)
        {
            Value = value;
        }
    }
}
