using jNet.RPC.CommonLib;
using jNet.RPC.Server;
using System.Collections.Generic;

namespace jNet.RPC.ServerLib.Level1.Level2.Level3
{
    public class MockRoot : ServerObjectBase, IMockRoot
    {
        private List<IMockMember> _members;
        private string _simpleProperty;
        private IMockMember _singleMember;

        [DtoMember]
        public IMockMember SingleMember { get => _singleMember; set => SetField(ref _singleMember, value); }
        [DtoMember]
        public List<IMockMember> Members => _members;
        [DtoMember]
        public string SimpleProperty { get => _simpleProperty; set => SetField(ref _simpleProperty, value); }

        public MockRoot()
        {
            _members = new List<IMockMember>
            {
                new MockMember { ValueString="Mock Object 1", ValueInt = 1},
                new MockMember { ValueString="Mock Object 2", ValueInt = 2},
                new MockMember { ValueString="Mock Object 3", ValueInt = 3},
            };
            SingleMember = new MockMember { ValueString = "Mock Object", ValueInt = 0 };
            SimpleProperty = "Test Value";
        }
    }
}
