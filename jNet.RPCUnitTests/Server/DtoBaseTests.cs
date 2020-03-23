using jNet.RPCUnitTests.MockModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace jNet.RPC.Server.Tests
{
    [TestClass()]
    public class ServerObjectBaseTests
    {              
        [TestMethod]
        public void DtosStoreTest_CreateAndStoreGUID_GUIDFound()
        {
            var _mockObject = new MockServerObject("TestValue");            
            Assert.IsNotNull(ServerObjectBase.FindDto(_mockObject.DtoGuid), "Guid not found in know dtos after object creation.");
        }      

        [TestMethod]
        public void DtosStoreTest_CreateAndStoreGUIDs_GUIDsFound()
        {
            int maxI = 100; //how many instances
            List<MockServerObject> mockObjects = new List<MockServerObject>();
            
            for (int i = 0; i < maxI; ++i)
                mockObjects.Add(new MockServerObject(i.ToString()));

            for (int i = 0; i < maxI; ++i)
                Assert.IsNotNull(ServerObjectBase.FindDto(mockObjects[i].DtoGuid), "Guid not found in known dtos.");
            
            mockObjects.Clear();
        }       
    }
}