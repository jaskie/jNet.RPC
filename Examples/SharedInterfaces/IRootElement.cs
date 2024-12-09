using System;
using System.ComponentModel;

namespace SharedInterfaces
{
    public interface IRootElement
    {
        void AddChild();
        bool RemoveChild(IChildElement childElement);
        IChildElement[] GetChildrens();
        string Name { get; set; }
        event EventHandler<ChildEventArgs> ChildAdded;
        event EventHandler<ChildEventArgs> ChildRemoved;
    }
}
