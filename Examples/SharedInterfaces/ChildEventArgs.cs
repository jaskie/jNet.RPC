using System;

namespace SharedInterfaces
{
    public class ChildEventArgs : EventArgs
    {
        public ChildEventArgs(IChildElement childElement)
        {
            ChildElement = childElement;
        }

        public IChildElement ChildElement { get; }
    }
}