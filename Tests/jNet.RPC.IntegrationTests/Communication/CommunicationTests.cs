﻿using jNet.RPC.Client;
using jNet.RPC.Server;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using Tests.CommonLibrary;

namespace jNet.RPC.IntegrationTests.Communication
{    
    [TestClass]
    public class CommunicationTests
    {
        private static RemoteClient _client;        

        private static ServerHost _server;
        private static Tests.ServerLibrary.MockRoot  _mockObject;

        [ClassInitialize]
        public async static Task SetUpServerClient(TestContext context)
        {  
            _mockObject = new Tests.ServerLibrary.MockRoot();
            
            _server = new ServerHost(1100, _mockObject);            
            _server.Start();

            _client = new RemoteClient();
            await _client.ConnectAsync("127.0.0.1:1100");
        }

        [ClassCleanup]
        public static void CleanUp()
        {
            _client.Dispose();
            _server.Dispose();            
        }

        [TestMethod]
        public void GetRootObject_ProxyObjectBase()
        {                             
            IMockRoot proxy = _client.GetRootObject<IMockRoot>();
            Assert.IsNotNull(proxy, "Returned object is null!");
            Assert.IsTrue(proxy.SimpleProperty == _mockObject.SimpleProperty);
            Assert.IsTrue(proxy.Members.Count == _mockObject.Members.Count);
        }        
    }
}
