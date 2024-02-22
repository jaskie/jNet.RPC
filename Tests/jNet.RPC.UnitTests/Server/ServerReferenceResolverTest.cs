using System;
using System.Collections.Generic;
using jNet.RPC.Server;
using jNet.RPCTests.MockModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace jNet.RPCTests.Server
{
    [TestClass]
    public class ReferenceResolverTest
    {
        #region GetReference
        [TestMethod]
        public void GetReferenceTest_Referenced_ReturnDto()
        {
            var ReferenceResolver = new ReferenceResolver();
            PrivateObject po = new PrivateObject(ReferenceResolver);
            var _knownDtos = ((Dictionary<Guid, ServerObjectBase>)po.GetField("_knownDtos"));
            var mockObject = new MockDto("TestValue");
            _knownDtos.Add(mockObject.DtoGuid, mockObject);

            var _knownDtosCount = _knownDtos.Count;

            var stringGuid = ReferenceResolver.GetReference(this, mockObject);
            var dto = ReferenceResolver.ResolveReference(new Guid(stringGuid));

            Assert.IsNotNull(dto);
            Assert.AreEqual(_knownDtosCount, _knownDtos.Count, "_knownDtos shouldn't increase!");
        }

        [TestMethod]
        public void GetReferenceTest_NonDtoObject_ReturnEmpty()
        {
            var mockObject = new object();
            var ReferenceResolver = new ReferenceResolver();
            var result = ReferenceResolver.GetReference(this, mockObject);
            Assert.AreEqual(result, String.Empty, "");
        }

        [TestMethod]
        public void GetReferenceTest_NonReferenced_ReturnDto()
        {
            var ReferenceResolver = new ReferenceResolver();
            PrivateObject po = new PrivateObject(ReferenceResolver);
            var mockObject = new MockDto("TestValue");
            var _knownDtos = ((Dictionary<Guid, ServerObjectBase>)po.GetField("_knownDtos"));


            var _knownDtosCount = _knownDtos.Count;

            var stringGuid = ReferenceResolver.GetReference(this, mockObject);
            var dto = ReferenceResolver.ResolveReference(new Guid(stringGuid));

            Assert.IsNotNull(dto);
            Assert.AreEqual(_knownDtosCount+1, _knownDtos.Count);
        }
        #endregion

        #region IsReferenced
        [TestMethod]
        public void IsReferencedTest_Referenced_ReturnTrue()
        {
            var mockObject = new MockDto("TestValue");
            var ReferenceResolver = new ReferenceResolver();
            ReferenceResolver.GetReference(this, mockObject);
            Assert.IsTrue(ReferenceResolver.IsReferenced(this, mockObject));
        }

        [TestMethod]
        public void IsReferencedTest_NonReferenced_ReturnFalse()
        {
            var mockObject = new MockDto("TestValue");
            var ReferenceResolver = new ReferenceResolver();
            Assert.IsFalse(ReferenceResolver.IsReferenced(this, mockObject));
        }
        public void IsReferencedTest_NonDto_ReturnGuidEmpty()
        {
            var obj = new object();
            var ReferenceResolver = new ReferenceResolver();
            var result = ReferenceResolver.IsReferenced(this, obj);
            Assert.AreEqual(result, Guid.Empty);
        }
        #endregion

        #region ResolveReference
        [TestMethod]
        public void ResolveReferenceTest_UnknownGuid_ReturnNull()
        {
            var ReferenceResolver = new ReferenceResolver();
            
            var dto = ReferenceResolver.ResolveReference(new Guid());

            Assert.IsNull(dto, "dto not null!");
        }

        [TestMethod]
        public void ResolveReferenceTest_Referenced_ReturnDto()
        {
            var ReferenceResolver = new ReferenceResolver();
            PrivateObject po = new PrivateObject(ReferenceResolver);
            var mockObject = new MockDto("TestValue");
            var _knownDtos = ((Dictionary<Guid, ServerObjectBase>)po.GetField("_knownDtos"));
            _knownDtos.Add(mockObject.DtoGuid, mockObject);

            var _knownDtosCount = _knownDtos.Count;

            var stringGuid = ReferenceResolver.GetReference(this, mockObject);
            var dto = ReferenceResolver.ResolveReference(new Guid(stringGuid));

            Assert.IsNotNull(dto);
            Assert.AreEqual(_knownDtosCount, _knownDtos.Count);
        }

        [TestMethod]
        public void ResolveReferenceTest_NonReferenced_ReturnDto()
        {
            var ReferenceResolver = new ReferenceResolver();
            PrivateObject po = new PrivateObject(ReferenceResolver);
            var mockObject = new MockDto("TestValue");
            var _knownDtos = ((Dictionary<Guid, ServerObjectBase>)po.GetField("_knownDtos"));

            var _knownDtosCount = _knownDtos.Count;

            var stringGuid = ReferenceResolver.GetReference(this, mockObject);
            var dto = ReferenceResolver.ResolveReference(new Guid(stringGuid));

            Assert.IsNotNull(dto);
            Assert.AreEqual(_knownDtosCount+1, _knownDtos.Count);
        }
        #endregion
    }
}
