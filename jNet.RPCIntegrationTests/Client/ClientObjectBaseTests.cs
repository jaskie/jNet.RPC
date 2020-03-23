using jNet.RPC.Client;
using jNet.RPCUnitTests.MockModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace jNet.RPCIntegrationTests.Client
{
    [TestClass]
    class ClientObjectBaseTests
    {
        [TestMethod]
        public void Finalization_Any_Collect()
        {
            WeakReference<ProxyObjectBase> weakReference = null;
            new Action(() =>
            {
                var proxy = new MockProxyObject();
                weakReference = new WeakReference<ProxyObjectBase>(proxy, true);
            })();

            GC.Collect(); //first finalization (mark as finalized)
            GC.WaitForPendingFinalizers();

            new Action(() =>
            {
                weakReference.TryGetTarget(out var target);
                Assert.IsTrue(ProxyObjectBase.FinalizeRequested.ContainsKey(target.DtoGuid), "Object did not save itself!");
                target.FinalizeProxy();
            })();

            GC.Collect(); //first finalization (mark as finalized)
            GC.WaitForPendingFinalizers();
            GC.Collect(); //collect finalization
            GC.WaitForPendingFinalizers();

            Assert.IsTrue(ProxyObjectBase.FinalizeRequested.Count == 0, "There should be no Finalization Requests at this point!");
            Assert.IsFalse(weakReference.TryGetTarget(out _), "GetTarget should not return anything!");
        }

        [TestMethod]
        public void Finalization_Any_Resurrected()
        {
            WeakReference<ProxyObjectBase> weakReference = null;
            new Action(() =>
            {
                var proxy = new MockProxyObject();
                weakReference = new WeakReference<ProxyObjectBase>(proxy, true);
            })();

            GC.Collect(); //first finalization (mark as finalized)
            GC.WaitForPendingFinalizers();

            new Action(() =>
            {
                weakReference.TryGetTarget(out var target);
                Assert.IsTrue(ProxyObjectBase.FinalizeRequested.ContainsKey(target.DtoGuid), "Object did not save itself!");                
            })();

            GC.Collect(); //first finalization (mark as finalized)
            GC.WaitForPendingFinalizers();
            GC.Collect(); //collect finalization
            GC.WaitForPendingFinalizers();

            new Action(() =>
            {
                weakReference.TryGetTarget(out var target);
                target.Resurrect();
            })();

            GC.Collect(); //first finalization (mark as finalized)
            GC.WaitForPendingFinalizers();
            GC.Collect(); //collect finalization
            GC.WaitForPendingFinalizers();

            Assert.IsTrue(ProxyObjectBase.FinalizeRequested.Count == 0, "There should be no Finalization Requests at this point!");
            Assert.IsTrue(weakReference.TryGetTarget(out var final), "GetTarget should not return anything!");
            PrivateObject priv = new PrivateObject(final, new PrivateType(typeof(ProxyObjectBase)));
            Assert.IsFalse((bool)priv.GetField("_finalizedRequested"), "Object resurrected, but did not reset finalization properties!");
        }
    }
}
