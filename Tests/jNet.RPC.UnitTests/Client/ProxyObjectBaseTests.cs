using jNet.RPC.Client;
using jNet.RPC.UnitTests.MockModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace jNet.RPC.UnitTests.Client
{
    [TestClass]
    public class ProxyObjectBaseTest
    {
        private MockProxyObject _mockProxy;
        
        [TestInitialize]
        public void Initialize()
        {
            _mockProxy = new MockProxyObject { DtoGuid = Guid.NewGuid() };
        }

        [TestMethod]
        public void FinalizerTest()
        {
            WeakReference<ProxyObjectBase> weakRefernce = null;
            Guid checkGuid = Guid.Empty;
            new Action(()=> 
            {
                var mockProxy = new MockProxyObject { DtoGuid = Guid.NewGuid() };
                checkGuid = mockProxy.DtoGuid;
                weakRefernce = new WeakReference<ProxyObjectBase>(mockProxy, true);
            })();

            GC.Collect(); //first collect (save)
            GC.WaitForPendingFinalizers();

            new Action(() =>
            {
                weakRefernce.TryGetTarget(out var target);
                var priv = new PrivateObject(target, new PrivateType(typeof(ProxyObjectBase)));
                Assert.IsFalse((int)priv.GetField("_isFinalizeRequested") == default, "Object not prepared for collection!");
            })();

            GC.Collect(); //first collect (mark as finalized)
            GC.WaitForPendingFinalizers();
            GC.Collect(); //second collect (collection)
            GC.WaitForPendingFinalizers();

            Assert.IsFalse(weakRefernce.TryGetTarget(out _), "Object is still alive...");
        }

    }
}
