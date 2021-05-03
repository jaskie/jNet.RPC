using jNet.RPC.Server;
using SharedInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerApp
{
    class ChildElement : ServerObjectBase, IChildElement
    {
        private double _value;
        public double Value { get => _value; set => SetField(ref _value, value); }
    }
}
