using jNet.RPC.Client;
using jNet.RPCTests.MockModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace jNet.RPCTests.Client
{
    [TestClass]
    public class ReferenceResolverTest
    {
        [TestMethod]
        public void ResolveReferenceGuid_Referenced_ReturnProxy()
        {
            ReferenceResolver ReferenceResolver = new ReferenceResolver();;
            PrivateObject po = new PrivateObject(ReferenceResolver);
            var mockObject = new MockProxy();

            ReferenceResolver.AddReference(this, mockObject.DtoGuid.ToString(), mockObject);
            var proxy = ReferenceResolver.ResolveReference(mockObject.DtoGuid);
            Assert.AreEqual(mockObject, proxy, "Object are not the same!");
        }

        [TestMethod]
        public void ResolveReferenceGuid_NonReferenced_ReturnNull()
        {
            ReferenceResolver ReferenceResolver = new ReferenceResolver();
            PrivateObject po = new PrivateObject(ReferenceResolver);
            var mockObject = new MockProxy();
           
            var proxy = ReferenceResolver.ResolveReference(mockObject.DtoGuid);
            Assert.IsNull(proxy, "Proxy not null!");
        }

        [TestMethod]
        public void AddReference_NotExisting_AddToKnown()
        {
            ReferenceResolver ReferenceResolver = new ReferenceResolver();
            PrivateObject po = new PrivateObject(ReferenceResolver);
            var mockObject = new MockProxy();
            var knownDtos = ((Dictionary<Guid, WeakReference<ProxyObjectBase>>)po.GetField("_knownDtos"));


            var knownDtosInitialCount = knownDtos.Count;
            ReferenceResolver.AddReference(this, mockObject.DtoGuid.ToString(), mockObject);
            Assert.AreEqual(knownDtosInitialCount + 1, knownDtos.Count);
            knownDtos[mockObject.DtoGuid].TryGetTarget(out var target);
            Assert.AreEqual(mockObject, target);
        }

        [TestMethod]
        public void AddReference_Existing_Populate()
        {
            ReferenceResolver ReferenceResolver = new ReferenceResolver();
            PrivateObject po = new PrivateObject(ReferenceResolver);
            var mockObject = new MockProxy();
            var knownDtos = ((Dictionary<Guid, WeakReference<ProxyObjectBase>>)po.GetField("_knownDtos"));

            ReferenceResolver.AddReference(this, mockObject.DtoGuid.ToString(), mockObject);

            var knownDtosInitialCount = knownDtos.Count;
            ReferenceResolver.AddReference(this, mockObject.DtoGuid.ToString(), mockObject);

            ReferenceResolver.ProxiesToPopulate.TryGetValue(mockObject.DtoGuid, out var proxy);
            Assert.AreEqual(proxy, mockObject, "Wrong object added to population.");
            Assert.AreEqual(knownDtosInitialCount, knownDtos.Count, "KnownDtos shouldn't increased!");
        }

        [TestMethod]
        public void GetReference_AnyDto_ReturnGuid()
        {
            ReferenceResolver ReferenceResolver = new ReferenceResolver();
            var mockObject = new MockProxy();

            var guid = ReferenceResolver.GetReference(this, mockObject);
            Assert.AreEqual(mockObject.DtoGuid.ToString(), guid);
        }

        [TestMethod]
        public void GetReference_NonDto_ReturnGuid()
        {
            ReferenceResolver ReferenceResolver = new ReferenceResolver();
            var mockObject = new object();

            var referenced = ReferenceResolver.IsReferenced(this, mockObject);
            Assert.IsFalse(referenced);
        }

        [TestMethod]
        public void IsReferenced_AnyDto_ReturnTrue()
        {
            ReferenceResolver ReferenceResolver = new ReferenceResolver();
            var mockObject = new MockProxy();

            var referenced = ReferenceResolver.IsReferenced(this, mockObject);
            Assert.IsTrue(referenced);
        }

        [TestMethod]
        public void IsReferenced_NotDto_ReturnTrue()
        {
            ReferenceResolver ReferenceResolver = new ReferenceResolver();
            var mockObject = new object();

            var referenced = ReferenceResolver.IsReferenced(this, mockObject);
            Assert.IsFalse(referenced);
        }        

        [TestMethod]
        public void ResolveReference_ReferencedAlive_ReturnDto()
        {
            ReferenceResolver ReferenceResolver = new ReferenceResolver();
            var mockObject = new MockProxy();
            ReferenceResolver.AddReference(this, mockObject.DtoGuid.ToString(), mockObject);

            var proxy = ReferenceResolver.ResolveReference(this, mockObject.DtoGuid.ToString());
            Assert.AreEqual(mockObject, proxy);
        }

        [TestMethod]
        public void ResolveReference_NonReferenced_ReturnNull()
        {
            ReferenceResolver ReferenceResolver = new ReferenceResolver();
            var mockObject = new MockProxy();            

            var proxy = ReferenceResolver.ResolveReference(this, mockObject.DtoGuid.ToString());
            Assert.IsNull(proxy);
        }
    }
}
