﻿using jNet.RPCTests.MockModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace jNet.RPC.Server.Tests
{
    [TestClass()]
    public class DtoBaseTests
    {              
        [TestMethod]
        public void DtosStoreTest_CreateAndStoreGUID_GUIDFound()
        {
            var _mockObject = new MockObject("TestValue");            
            Assert.IsNotNull(DtoBase.FindDto(_mockObject.DtoGuid), "Guid not found in know dtos after object creation.");
        }      

        [TestMethod]
        public void DtosStoreTest_CreateAndStoreGUIDs_GUIDsFound()
        {
            int maxI = 100; //how many instances
            List<MockObject> mockObjects = new List<MockObject>();
            
            for (int i = 0; i < maxI; ++i)
                mockObjects.Add(new MockObject(i.ToString()));

            for (int i = 0; i < maxI; ++i)
                Assert.IsNotNull(DtoBase.FindDto(mockObjects[i].DtoGuid), "Guid not found in known dtos.");
            
            mockObjects.Clear();
        }       
    }
}