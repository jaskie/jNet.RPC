using jNet.RPC.Client;
using jNet.RPCUnitTests.MockModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace jNet.RPCUnitTests.Client
{
    [TestClass]
    public class ProxyObjectBaseTest
    {
        private MockProxyObject _mockProxy;
        private PrivateObject _mockBase;
        
        [TestInitialize]
        public void Initialize()
        {
            _mockProxy = new MockProxyObject { DtoGuid = Guid.NewGuid() };
            _mockBase = new PrivateObject(_mockProxy, new PrivateType(typeof(ProxyObjectBase)));
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

                Assert.IsTrue(ProxyObjectBase.FinalizeRequested.ContainsKey(target.DtoGuid), "Object did not save itself!");            
                Assert.IsTrue((bool)priv.GetField("_finalizeRequested"), "Object not prepared for collection!");

                ProxyObjectBase.FinalizeRequested.TryRemove(target.DtoGuid, out _);
            })();

            GC.Collect(); //first collect (mark as finalized)
            GC.WaitForPendingFinalizers();
            GC.Collect(); //second collect (collection)
            GC.WaitForPendingFinalizers();

            Assert.IsFalse(ProxyObjectBase.FinalizeRequested.ContainsKey(checkGuid), "Object did not collect...");
            Assert.IsFalse(weakRefernce.TryGetTarget(out _), "Object is still alive...");
        }

        [TestMethod]
        public void FinalizeProxy_Void()
        {
            if (!ProxyObjectBase.FinalizeRequested.TryAdd(_mockProxy.DtoGuid, _mockProxy))
                Assert.Fail("Could not add object to Finalize Requests");
            
            _mockProxy.FinalizeProxy();
            Assert.IsFalse(ProxyObjectBase.FinalizeRequested.ContainsKey(_mockProxy.DtoGuid), "FinalizeRequests should not contain this element!");
            Assert.IsTrue((bool)_mockBase.GetField("_finalizeRequested")); 
        }

        [TestMethod]
        public void ResurrectTest_Void()
        {
            bool resurrectedInvoked = false;

            if (!ProxyObjectBase.FinalizeRequested.TryAdd(_mockProxy.DtoGuid, _mockProxy))
                Assert.Fail("Could not add object to Finalize Requests");
            
            _mockProxy.Resurrected += (s, e) => { resurrectedInvoked = true; };
            _mockProxy.Resurrect();

            Assert.IsTrue(resurrectedInvoked, "Object did not notified about resurrection!");
            Assert.IsFalse(ProxyObjectBase.FinalizeRequested.ContainsKey(_mockProxy.DtoGuid), "Object shouldn't be here after resurrection!");

        }
        
    }
}
