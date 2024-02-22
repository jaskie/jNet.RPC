using jNet.RPC.Client;
using jNet.RPCTests.MockModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace jNet.RPCTests.Client
{
    [TestClass]
    public class ProxyBaseTest
    {
        [TestMethod]
        public void Finalizer_Any()
        {
            WeakReference<ProxyObjectBase> weakReference = null;
            new Action(() => 
            {
                var proxy = new MockProxy();
                weakReference = new WeakReference<ProxyObjectBase>(proxy, true);
            })();

            GC.Collect(); //first finalization (mark as finalized)
            GC.WaitForPendingFinalizers();

            new Action(() =>
            {
                weakReference.TryGetTarget(out var target);
            })();

            GC.Collect(); //first finalization (mark as finalized)
            GC.WaitForPendingFinalizers();
            GC.Collect(); //collect finalized
            GC.WaitForPendingFinalizers();

            Assert.IsFalse(weakReference.TryGetTarget(out _), "GetTarget should not return anything!");
        }
    }
}
