using jNet.RPC.CommonLib;
using jNet.RPC.Server;
using System;

namespace jNet.RPC.ServerLib.Level1.Level2
{
    public class MockMember : ServerObjectBase, IMockMember
    {
        private string _valueString;
        private int _valueInt;

        [DtoMember]
        public string ValueString { get => _valueString; set => SetField(ref _valueString, value); }

        [DtoMember]
        public int ValueInt { get => _valueInt; set => SetField(ref _valueInt, value); }

        public event EventHandler<int> EventFired;
        public void RaisePropertyChanged(int x)
        {
            EventFired?.Invoke(this, x);
        }
    }
}
