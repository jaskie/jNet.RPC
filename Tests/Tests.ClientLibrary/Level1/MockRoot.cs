using jNet.RPC;
using jNet.RPC.Client;
using System.Collections.Generic;
using Tests.CommonLibrary;

namespace Tests.ClientLibrary.Level1
{
    [DtoClass(nameof(IMockRoot))]
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
