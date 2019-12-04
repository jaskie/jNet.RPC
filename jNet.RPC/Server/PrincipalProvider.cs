using System.Net.Sockets;
using System.Security.Principal;

namespace jNet.RPC.Server
{
    public class PrincipalProvider : IPrincipalProvider
    {
        protected PrincipalProvider() { }

        public static IPrincipalProvider Default { get; } = new PrincipalProvider();

        public virtual IPrincipal GetPrincipal(TcpClient client)
        {
            return new GenericPrincipal(new GenericIdentity("Generic Identity"), new string[0]);
        }
    }
}
