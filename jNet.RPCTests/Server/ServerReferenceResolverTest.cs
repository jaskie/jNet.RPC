using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using jNet.RPC.Client;
using jNet.RPC.Server;
using jNet.RPCTests.MockModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace jNet.RPCTests.Server
{
    [TestClass]
    public class ServerReferenceResolverTest
    {
        #region GetReference
        [TestMethod]
        public void GetReferenceTest_Referenced_ReturnDto()
        {
            var serverReferenceResolver = new ServerReferenceResolver();
            PrivateObject po = new PrivateObject(serverReferenceResolver);
            var _knownDtos = ((ConcurrentDictionary<Guid, DtoBase>)po.GetField("_knownDtos"));
            var mockObject = new MockDto("TestValue");
            _knownDtos.TryAdd(mockObject.DtoGuid, mockObject);

            var _knownDtosCount = _knownDtos.Count;

            var stringGuid = serverReferenceResolver.GetReference(this, mockObject);
            var dto = serverReferenceResolver.ResolveReference(new Guid(stringGuid));

            Assert.IsNotNull(dto);
            Assert.AreEqual(_knownDtosCount, _knownDtos.Count, "_knownDtos shouldn't increase!");
        }

        [TestMethod]
        public void GetReferenceTest_NonDtoObject_ReturnEmpty()
        {
            var mockObject = new object();
            var serverReferenceResolver = new ServerReferenceResolver();
            var result = serverReferenceResolver.GetReference(this, mockObject);
            Assert.AreEqual(result, String.Empty, "");
        }

        [TestMethod]
        public void GetReferenceTest_NonReferenced_ReturnDto()
        {
            var serverReferenceResolver = new ServerReferenceResolver();
            PrivateObject po = new PrivateObject(serverReferenceResolver);
            var mockObject = new MockDto("TestValue");
            var _knownDtos = ((ConcurrentDictionary<Guid, DtoBase>)po.GetField("_knownDtos"));


            var _knownDtosCount = _knownDtos.Count;

            var stringGuid = serverReferenceResolver.GetReference(this, mockObject);
            var dto = serverReferenceResolver.ResolveReference(new Guid(stringGuid));

            Assert.IsNotNull(dto);
            Assert.AreEqual(_knownDtosCount+1, _knownDtos.Count);
        }
        #endregion

        #region IsReferenced
        [TestMethod]
        public void IsReferencedTest_Referenced_ReturnTrue()
        {
            var mockObject = new MockDto("TestValue");
            var serverReferenceResolver = new ServerReferenceResolver();
            serverReferenceResolver.GetReference(this, mockObject);
            Assert.IsTrue(serverReferenceResolver.IsReferenced(this, mockObject));
        }

        [TestMethod]
        public void IsReferencedTest_NonReferenced_ReturnFalse()
        {
            var mockObject = new MockDto("TestValue");
            var serverReferenceResolver = new ServerReferenceResolver();
            Assert.IsFalse(serverReferenceResolver.IsReferenced(this, mockObject));
        }
        public void IsReferencedTest_NonDto_ReturnGuidEmpty()
        {
            var obj = new object();
            var serverReferenceResolver = new ServerReferenceResolver();
            var result = serverReferenceResolver.IsReferenced(this, obj);
            Assert.AreEqual(result, Guid.Empty);
        }
        #endregion

        #region ResolveReference
        [TestMethod]
        public void ResolveReferenceTest_UnknownGuid_ReturnDto()
        {
            var serverReferenceResolver = new ServerReferenceResolver();            
            
            var dto = serverReferenceResolver.ResolveReference(new Guid());

            Assert.IsNull(dto, "dto not null!");            
        }

        [TestMethod]
        public void ResolveReferenceTest_Referenced_ReturnDto()
        {
            var serverReferenceResolver = new ServerReferenceResolver();
            PrivateObject po = new PrivateObject(serverReferenceResolver);
            var mockObject = new MockDto("TestValue");
            var _knownDtos = ((ConcurrentDictionary<Guid, DtoBase>)po.GetField("_knownDtos"));
            _knownDtos.TryAdd(mockObject.DtoGuid, mockObject);

            var _knownDtosCount = _knownDtos.Count;

            var stringGuid = serverReferenceResolver.GetReference(this, mockObject);
            var dto = serverReferenceResolver.ResolveReference(new Guid(stringGuid));

            Assert.IsNotNull(dto);
            Assert.AreEqual(_knownDtosCount, _knownDtos.Count);
        }

        [TestMethod]
        public void ResolveReferenceTest_NonReferenced_ReturnDto()
        {
            var serverReferenceResolver = new ServerReferenceResolver();
            PrivateObject po = new PrivateObject(serverReferenceResolver);
            var mockObject = new MockDto("TestValue");
            var _knownDtos = ((ConcurrentDictionary<Guid, DtoBase>)po.GetField("_knownDtos"));

            var _knownDtosCount = _knownDtos.Count;

            var stringGuid = serverReferenceResolver.GetReference(this, mockObject);
            var dto = serverReferenceResolver.ResolveReference(new Guid(stringGuid));

            Assert.IsNotNull(dto);
            Assert.AreEqual(_knownDtosCount+1, _knownDtos.Count);
        }
        #endregion
    }
}
