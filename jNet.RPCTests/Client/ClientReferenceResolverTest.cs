using jNet.RPC.Client;
using jNet.RPCTests.MockModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace jNet.RPCTests.Client
{
    [TestClass]
    public class ClientReferenceResolverTest
    {              
        [TestMethod]
        public void ResolveReferenceGuid_Referenced_ReturnProxy()
        {
            ClientReferenceResolver clientReferenceResolver = new ClientReferenceResolver();;
            PrivateObject po = new PrivateObject(clientReferenceResolver);
            var mockObject = new MockProxy();

            clientReferenceResolver.AddReference(this, mockObject.DtoGuid.ToString(), mockObject);
            var proxy = clientReferenceResolver.ResolveReference(mockObject.DtoGuid);
            Assert.AreEqual(mockObject, proxy, "Object are not the same!");
        }

        [TestMethod]
        public void ResolveReferenceGuid_NonReferenced_ReturnNull()
        {
            ClientReferenceResolver clientReferenceResolver = new ClientReferenceResolver();
            PrivateObject po = new PrivateObject(clientReferenceResolver);
            var mockObject = new MockProxy();
           
            var proxy = clientReferenceResolver.ResolveReference(mockObject.DtoGuid);
            Assert.IsNull(proxy, "Proxy not null!");
        }

        [TestMethod]
        public void AddReference_NotExisting_AddToKnown()
        {
            ClientReferenceResolver clientReferenceResolver = new ClientReferenceResolver();
            PrivateObject po = new PrivateObject(clientReferenceResolver);
            var mockObject = new MockProxy();
            var knownDtos = ((Dictionary<Guid, WeakReference<ProxyObjectBase>>)po.GetField("_knownDtos"));


            var knownDtosInitialCount = knownDtos.Count;
            clientReferenceResolver.AddReference(this, mockObject.DtoGuid.ToString(), mockObject);
                        
            Assert.AreEqual(knownDtosInitialCount + 1, knownDtos.Count);
            knownDtos[mockObject.DtoGuid].TryGetTarget(out var target);
            Assert.AreEqual(mockObject, target);
        }

        [TestMethod]
        public void AddReference_Existing_Populate()
        {
            ClientReferenceResolver clientReferenceResolver = new ClientReferenceResolver();            
            PrivateObject po = new PrivateObject(clientReferenceResolver);
            var mockObject = new MockProxy();
            var knownDtos = ((Dictionary<Guid, WeakReference<ProxyObjectBase>>)po.GetField("_knownDtos"));

            clientReferenceResolver.AddReference(this, mockObject.DtoGuid.ToString(), mockObject);

            var knownDtosInitialCount = knownDtos.Count;
            clientReferenceResolver.AddReference(this, mockObject.DtoGuid.ToString(), mockObject);

            Assert.AreEqual(clientReferenceResolver.ProxiesToPopulate[0], mockObject, "Wrong object added to population.");
            Assert.AreEqual(knownDtosInitialCount, knownDtos.Count, "KnownDtos shouldn't increased!");
        }

        [TestMethod]
        public void GetReference_AnyDto_ReturnGuid()
        {
            ClientReferenceResolver clientReferenceResolver = new ClientReferenceResolver();            
            var mockObject = new MockProxy();

            var guid = clientReferenceResolver.GetReference(this, mockObject);
            Assert.AreEqual(mockObject.DtoGuid.ToString(), guid);
        }

        [TestMethod]
        public void GetReference_NonDto_ReturnGuid()
        {
            ClientReferenceResolver clientReferenceResolver = new ClientReferenceResolver();
            var mockObject = new object();

            var referenced = clientReferenceResolver.IsReferenced(this, mockObject);
            Assert.IsFalse(referenced);
        }

        [TestMethod]
        public void IsReferenced_AnyDto_ReturnTrue()
        {
            ClientReferenceResolver clientReferenceResolver = new ClientReferenceResolver();
            var mockObject = new MockProxy();

            var referenced = clientReferenceResolver.IsReferenced(this, mockObject);
            Assert.IsTrue(referenced);
        }

        [TestMethod]
        public void IsReferenced_NotDto_ReturnTrue()
        {
            ClientReferenceResolver clientReferenceResolver = new ClientReferenceResolver();
            var mockObject = new object();

            var referenced = clientReferenceResolver.IsReferenced(this, mockObject);
            Assert.IsFalse(referenced);
        }        

        [TestMethod]
        public void ResolveReference_ReferencedAlive_ReturnDto()
        {
            ClientReferenceResolver clientReferenceResolver = new ClientReferenceResolver();
            var mockObject = new MockProxy();
            clientReferenceResolver.AddReference(this, mockObject.DtoGuid.ToString(), mockObject);

            var proxy = clientReferenceResolver.ResolveReference(this, mockObject.DtoGuid.ToString());
            Assert.AreEqual(mockObject, proxy);
        }

        [TestMethod]
        public void ResolveReference_ReferenceFinalized_ReturnDto()
        {
            ClientReferenceResolver clientReferenceResolver = new ClientReferenceResolver();
            WeakReference<ProxyObjectBase> weakReference = null;
            Guid guid = Guid.Empty;


            new Action(() =>
            {
                var mockObject = new MockProxy { DtoGuid = Guid.NewGuid() };
                weakReference = new WeakReference<ProxyObjectBase>(mockObject, true);
                clientReferenceResolver.AddReference(this, mockObject.DtoGuid.ToString(), mockObject);
                guid = new Guid(mockObject.DtoGuid.ToString());
            })();


            GC.Collect();
            GC.WaitForPendingFinalizers();

            if (ProxyObjectBase.FinalizeRequested.Count < 1)
                Assert.Fail("Object did not finalize or finalized instantly");

            new Action(() =>
            {
                ProxyObjectBase.FinalizeRequested.TryGetValue(guid, out var target);
                if (target == null)
                    Assert.Fail("Guid mismatch");
                target.Resurrect();
                var proxy = clientReferenceResolver.ResolveReference(guid);
                Assert.AreEqual(target, proxy, "Proxy did not resurrected properly");
            })();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            new Action(() =>
            {
                if (ProxyObjectBase.FinalizeRequested.ContainsKey(guid))
                {
                    ProxyObjectBase.FinalizeRequested.TryGetValue(guid, out var target);
                    target.FinalizeProxy();
                }
                else
                    Assert.Fail("Could not final finalize. Object not found in FinalizeRequested collection.");
            })();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            if (ProxyObjectBase.FinalizeRequested.Count > 0 && !weakReference.TryGetTarget(out _))
                Assert.Fail("Object did not finalize properly.");

        }

        [TestMethod]
        public void ResolveReference_NonReferenced_ReturnNull()
        {
            ClientReferenceResolver clientReferenceResolver = new ClientReferenceResolver();
            var mockObject = new MockProxy();            

            var proxy = clientReferenceResolver.ResolveReference(this, mockObject.DtoGuid.ToString());
            Assert.IsNull(proxy);
        }
    }
}
