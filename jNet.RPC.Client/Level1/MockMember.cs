using jNet.RPC.Client;
using jNet.RPC.CommonLib;
using System;

namespace jNet.RPC.ClientLib.Level1
{
    public class MockMember : ProxyObjectBase, IMockMember
    {
#pragma warning disable CS0649
        [DtoMember(nameof(IMockMember.ValueString))]
        private string _valueString;
        [DtoMember(nameof(IMockMember.ValueInt))]
        private int _valueInt;
#pragma warning restore
        public string ValueString { get => _valueString; set => Set(value); }
        public int ValueInt { get => _valueInt; set => Set(value); }

        private event EventHandler<int> _propertyChanged;

        public event EventHandler<int> EventFired
        {
            add
            {
                EventAdd(_propertyChanged);
                _propertyChanged += value;
            }
            remove
            {
                _propertyChanged -= value;
                EventRemove(_propertyChanged);
            }
        }

        protected override void OnEventNotification(SocketMessage message)
        {            
        }
    }
}
