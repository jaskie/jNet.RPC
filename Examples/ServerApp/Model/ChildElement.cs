using jNet.RPC;
using jNet.RPC.Server;
using SharedInterfaces;

namespace ServerApp.Model
{
    [DtoType(typeof(IChildElement))]
    class ChildElement : ServerObjectBase, IChildElement
    {
        private double _value;
        private string _name = "Child initial name";

        [DtoMember]
        public double Value { get => _value; set => SetField(ref _value, value); }

        [DtoMember]
        public string Name { get => _name; set => SetField(ref _name, value); }
    }
}
