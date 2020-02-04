using System.Threading;

namespace jNet.RPC.Client
{
    public class MessageRequest
    {
        public readonly SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(0);
        public SocketMessage Message;
    }
}
