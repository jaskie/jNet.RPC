using System;
using System.Threading;

namespace jNet.RPC.Client
{
    internal class MessageRequest: IDisposable
    {
        private readonly ManualResetEvent _mutex = new ManualResetEvent(false);
        private SocketMessage _result;

        public void Dispose()
        {
            _mutex.Dispose();
        }

        public void SetResult(SocketMessage message)
        {
            _result = message;
            _mutex.Set();
        }

        public SocketMessage WaitForResult(CancellationToken token)
        {
            WaitHandle.WaitAny(new[] { token.WaitHandle, _mutex });
            return _result;
        }

    }
}
