using jNet.RPC.Client;
using jNet.RPC.CommonLib;
using jNet.RPC.Server;
using jNet.RPC.ServerLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.Threading.Tasks;

namespace jNet.RPCIntegrationTests.Communication
{    
    [TestClass]
    public class CommunicationTests
    {
        private static RemoteClient _client;        

        private static ServerHost _server;
        private static MockRoot  _mockObject;

        [ClassInitialize]
        public async static Task SetUpServerClient(TestContext context)
        {  
            _mockObject = new MockRoot();
            
            _server = new ServerHost(1024, _mockObject);            
            _server.Start();

            _client = new RemoteClient();
            await _client.Connect("127.0.0.1:1024");
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
