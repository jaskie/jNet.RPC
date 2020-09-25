using System;

namespace Tests.CommonLibrary
{
    public interface IMockMember
    {
        string ValueString { get; set; }
        event EventHandler<int> EventFired;
        int ValueInt { get; set; }
    }
}
