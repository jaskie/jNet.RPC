using System;
using System.Threading;

namespace jNet.RPC.Client
{
    internal class MessageRequest: IDisposable
    {
        private readonly ManualResetEvent _mutex = new ManualResetEvent(false);
        private object _result;
        private SocketMessage.SocketMessageType? _socketMessageType;

        public void Dispose()
        {
            _mutex.Dispose();
        }

        public void SetResult(SocketMessage.SocketMessageType socketMessageType, object result)
        {
            _socketMessageType = socketMessageType;
            _result = result;
            _mutex.Set();
        }

        public object WaitForResult(CancellationToken token)
        {
            WaitHandle.WaitAny(new[] { token.WaitHandle, _mutex });
            return _result;
        }

        public SocketMessage.SocketMessageType? MessageType => _socketMessageType;

    }
}
