using System;
using System.Collections.Generic;
using jNet.RPC.Server;
using jNet.RPC.UnitTests.MockModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace jNet.RPC.UnitTests.Server
{
    [TestClass]
    public class ServerReferenceResolverTests
    {
        #region GetReference
        [TestMethod]
        public void GetReferenceTest_Referenced_ReturnDto()
        {
            var serverReferenceResolver = new ReferenceResolver();
            var _knownDtos = serverReferenceResolver.KnownDtos;
            var mockObject = new MockServerObject("TestValue");
            _knownDtos.Add(mockObject.DtoGuid, mockObject);

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
            var serverReferenceResolver = new ReferenceResolver();
            var result = serverReferenceResolver.GetReference(this, mockObject);
            Assert.AreEqual(result, String.Empty, "");
        }

        [TestMethod]
        public void GetReferenceTest_NonReferenced_ReturnDto()
        {
            var serverReferenceResolver = new ReferenceResolver();
            var mockObject = new MockServerObject("TestValue");
            var _knownDtos = serverReferenceResolver.KnownDtos;


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
            var mockObject = new MockServerObject("TestValue");
            var serverReferenceResolver = new ReferenceResolver();
            serverReferenceResolver.GetReference(this, mockObject);
            Assert.IsTrue(serverReferenceResolver.IsReferenced(this, mockObject));
        }

        [TestMethod]
        public void IsReferencedTest_NonReferenced_ReturnFalse()
        {
            var mockObject = new MockServerObject("TestValue");
            var serverReferenceResolver = new ReferenceResolver();
            Assert.IsFalse(serverReferenceResolver.IsReferenced(this, mockObject));
        }
        public void IsReferencedTest_NonDto_ReturnGuidEmpty()
        {
            var obj = new object();
            var serverReferenceResolver = new ReferenceResolver();
            var result = serverReferenceResolver.IsReferenced(this, obj);
            Assert.AreEqual(result, Guid.Empty);
        }
        #endregion

        #region ResolveReference
        [TestMethod]
        public void ResolveReferenceTest_UnknownGuid_ReturnNull()
        {
            var serverReferenceResolver = new ReferenceResolver();
            
            var dto = serverReferenceResolver.ResolveReference(new Guid());

            Assert.IsNull(dto, "dto not null!");
        }

        [TestMethod]
        public void ResolveReferenceTest_Referenced_ReturnDto()
        {
            var serverReferenceResolver = new ReferenceResolver();
            var mockObject = new MockServerObject("TestValue");
            var _knownDtos = serverReferenceResolver.KnownDtos;
            _knownDtos.Add(mockObject.DtoGuid, mockObject);

            var _knownDtosCount = _knownDtos.Count;

            var stringGuid = serverReferenceResolver.GetReference(this, mockObject);
            var dto = serverReferenceResolver.ResolveReference(new Guid(stringGuid));

            Assert.IsNotNull(dto);
            Assert.AreEqual(_knownDtosCount, _knownDtos.Count);
        }

        [TestMethod]
        public void ResolveReferenceTest_NonReferenced_ReturnDto()
        {
            var serverReferenceResolver = new ReferenceResolver();
            var mockObject = new MockServerObject("TestValue");
            var _knownDtos = serverReferenceResolver.KnownDtos;

            var _knownDtosCount = _knownDtos.Count;

            var stringGuid = serverReferenceResolver.GetReference(this, mockObject);
            var dto = serverReferenceResolver.ResolveReference(new Guid(stringGuid));

            Assert.IsNotNull(dto);
            Assert.AreEqual(_knownDtosCount+1, _knownDtos.Count);
        }
        #endregion
    }
}
