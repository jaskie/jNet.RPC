using jNet.RPC;
using jNet.RPC.Server;
using SharedInterfaces;
using System;
using System.Collections.Generic;

namespace ServerApp
{
    [DtoType(typeof(IRootElement))]
    class RootElement : ServerObjectBase, IRootElement
    {
        private List<IChildElement> _childElements = new List<IChildElement>();
        private string _name = "A root element name property";
        private readonly object _sync = new(); //required as request may come from many client threads

        public RootElement()
        {
            _childElements.Add(new ChildElement { Value = 50 });
        }

        [DtoMember]
        public string Name { get => _name; set => SetField(ref _name, value); }

        public event EventHandler<ChildEventArgs> ChildAdded;

        public event EventHandler<ChildEventArgs> ChildRemoved;

        public IChildElement AddChild()
        {
            var newChild = new ChildElement();
            lock (_sync)
                _childElements.Add(newChild);
            ChildAdded?.Invoke(this, new ChildEventArgs(newChild));
            return newChild;
        }

        public IChildElement[] GetChildrens()
        {
            lock (_sync)
                return _childElements.ToArray();
        }

        public bool RemoveChild(IChildElement childElement)
        {
            lock (_sync)
                if (!_childElements.Remove(childElement))
                    return false;
            ChildRemoved?.Invoke(this, new ChildEventArgs(childElement));
            return true;
        }


    }
}
