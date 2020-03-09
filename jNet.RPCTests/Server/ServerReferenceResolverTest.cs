using System;
using jNet.RPC.Server;
using jNet.RPCTests.MockModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace jNet.RPCTests.Server
{
    [TestClass]
    public class ServerReferenceResolverTest
    {
        [TestMethod]
        public void GetReferenceTest_DtoObject_ReturnGUID()
        {
            var mockObject = new MockObject("TestValue");
            var serverReferenceResolver = new ServerReferenceResolver();
            var result = serverReferenceResolver.GetReference(this, mockObject);
            Assert.IsTrue(Guid.TryParse(result, out var _));
        }

        [TestMethod]
        public void GetReferenceTest_NonDtoObject_ReturnEmpty()
        {
            var mockObject = new object();
            var serverReferenceResolver = new ServerReferenceResolver();
            var result = serverReferenceResolver.GetReference(this, mockObject);
            Assert.AreEqual(result, String.Empty);
        }

        [TestMethod]
        public void IsReferencedTest_Referenced_ReturnTrue()
        {
            var mockObject = new MockObject("TestValue");
            var serverReferenceResolver = new ServerReferenceResolver();
            serverReferenceResolver.GetReference(this, mockObject);
            Assert.IsTrue(serverReferenceResolver.IsReferenced(this, mockObject));
        }

        [TestMethod]
        public void IsReferencedTest_NonReferenced_ReturnFalse()
        {
            var mockObject = new MockObject("TestValue");
            var serverReferenceResolver = new ServerReferenceResolver();            
            Assert.IsFalse(serverReferenceResolver.IsReferenced(this, mockObject));
        }

        [TestMethod]
        public void ResolveReferenceTest_Referenced_ReturnDto()
        {
            var mockObject = new MockObject("TestValue");
            var serverReferenceResolver = new ServerReferenceResolver();

            var stringGuid = serverReferenceResolver.GetReference(this, mockObject);
            var dto = serverReferenceResolver.ResolveReference(new Guid(stringGuid));

            Assert.IsNotNull(dto);
        }

        [TestMethod]
        public void ResolveReferenceTest_NonReferenced_ThrowException()
        {
            var mockObject = new MockObject("TestValue");
            var serverReferenceResolver = new ServerReferenceResolver();           
            Assert.ThrowsException<UnresolvedReferenceException>((Action)(() => { serverReferenceResolver.ResolveReference(Guid.NewGuid()); }));
        }

    }
}
