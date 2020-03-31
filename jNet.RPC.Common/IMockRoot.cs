using System.Collections.Generic;

namespace jNet.RPC.CommonLib
{
    public interface IMockRoot
    {
        IMockMember SingleMember { get; set; }
        List<IMockMember> Members { get; }
        string SimpleProperty { get; set; }
    }
}
