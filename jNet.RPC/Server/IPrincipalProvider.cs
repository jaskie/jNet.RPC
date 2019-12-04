using System.Net.Sockets;
using System.Security.Principal;

namespace jNet.RPC.Server
{
    public interface IPrincipalProvider
    {
        IPrincipal GetPrincipal(TcpClient client);
    }
}