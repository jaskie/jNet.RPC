using System;
using System.Collections.Concurrent;
using System.Threading;

namespace jNet.RPC.Client
{
    internal class NotificationExecutor: IDisposable
    {
        private BlockingCollection<Action> _executionQueue = new BlockingCollection<Action>();
        private Thread _exectionThread;
        private static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private bool _isDisposed;

        public NotificationExecutor()
        {
            _exectionThread = new Thread(ExecutionThreadProc) { Name = $"{nameof(NotificationExecutor)} thread", IsBackground = true};
            _exectionThread.Start();
        }

        private void ExecutionThreadProc()
        {
            while (true)
            {
                Action action = _executionQueue.Take();
                try
                {
                    if (action is null)
                        break;
                    else
                        action.Invoke();
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Exception invoking event notification");
                }
            }
            Logger.Debug("NotificationExecutor thread finished");
        }

        public void Queue(Action action)
        {
            _executionQueue.Add(action);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;
            _isDisposed = true;
            _executionQueue.Add(null);
            _exectionThread.Join();
            _executionQueue.Dispose();
        }
    }
}
