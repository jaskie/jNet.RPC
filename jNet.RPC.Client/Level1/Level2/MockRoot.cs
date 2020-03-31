using jNet.RPC.Client;
using jNet.RPC.CommonLib;
using System.Collections.Generic;

namespace jNet.RPC.ClientLib.Level1.Level2
{
    public class MockRoot : ProxyObjectBase, IMockRoot
    {
#pragma warning disable CS0649
        [DtoMember(nameof(IMockRoot.Members))]
        private List<IMockMember> _members;
        [DtoMember(nameof(IMockRoot.SimpleProperty))]
        private string _simpleProperty;
        [DtoMember(nameof(IMockRoot.SingleMember))]
        private IMockMember _singleMember;
#pragma warning restore

        public IMockMember SingleMember { get => _singleMember; set => Set(value); }
        public List<IMockMember> Members => _members;
        public string SimpleProperty { get => _simpleProperty; set => Set(value); }
        protected override void OnEventNotification(SocketMessage message)
        {            
        }
    }
}
