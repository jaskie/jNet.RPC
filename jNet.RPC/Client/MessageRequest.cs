using System;
using System.Threading;

namespace jNet.RPC.Client
{
    internal class MessageRequest: IDisposable
    {
        private readonly ManualResetEventSlim _mutex = new ManualResetEventSlim();
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

        public SocketMessage WaitForResult()
        {
            _mutex.Wait();
            return _result;
        }

    }
}
