using System.Collections.Generic;

namespace Tests.CommonLibrary
{
    public interface IMockRoot
    {
        IMockMember SingleMember { get; set; }
        List<IMockMember> Members { get; }
        string SimpleProperty { get; set; }
    }
}
