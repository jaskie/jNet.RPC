using System;
using System.Collections.Concurrent;
using System.Threading;

namespace jNet.RPC.Client
{
    internal class NotificationExecutor: IDisposable
    {
        private BlockingCollection<Action> _executionQueue = new BlockingCollection<Action>();
        private Thread _exectionThread;

        public NotificationExecutor()
        {
            _exectionThread = new Thread(ExecutionThreadProc) { Name = $"{nameof(NotificationExecutor)} thread"};
            _exectionThread.Start();
        }

        private void ExecutionThreadProc()
        {
            Action action;
            do
            {
                action = _executionQueue.Take();
                action?.Invoke();
            }
            while (action != null);
            
        }

        public void Queue(Action action)
        {
            _executionQueue.Add(action);
        }

        public void Dispose()
        {
            _executionQueue.Add(null);
            _exectionThread?.Join();
            _exectionThread = null;
        }
    }
}
