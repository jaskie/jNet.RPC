using System;

namespace jNet.RPC.CommonLib
{
    public interface IMockMember
    {
        string ValueString { get; set; }
        event EventHandler<int> EventFired;
        int ValueInt { get; set; }
    }
}
