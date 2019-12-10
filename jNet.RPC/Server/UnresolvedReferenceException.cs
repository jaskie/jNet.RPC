using System;

namespace jNet.RPC.Server
{
    public class UnresolvedReferenceException: Exception
    {
        public Guid Guid { get; }

        public UnresolvedReferenceException(Guid guid)
        {
            Guid = guid;
        }
    }
}
