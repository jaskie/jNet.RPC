using System;

namespace SharedInterfaces
{
    public interface IRootElement
    {
        IChildElement AddChild();
        bool RemoveChild(IChildElement childElement);
        IChildElement[] GetChildrens();
        string Name { get; set; }
        event EventHandler<ChildEventArgs> ChildAdded;
        event EventHandler<ChildEventArgs> ChildRemoved;
    }
}
