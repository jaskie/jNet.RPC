using System;
using System.Threading;

namespace jNet.RPC.Client
{
    internal class MessageRequest: IDisposable
    {
        private readonly ManualResetEvent _mutex = new ManualResetEvent(false);
        private object _result;
        private SocketMessageType? _socketMessageType;

        public void Dispose()
        {
            _mutex.Dispose();
        }

        public void SetResult(SocketMessageType socketMessageType, object result)
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

        public SocketMessageType? MessageType => _socketMessageType;

    }
}
