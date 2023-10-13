using jNet.RPC.Client;
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
        private static Tests.ServerLibrary.MockRoot _mockObject;

        [TestInitialize]
        public void SetUpServerClient()
        {
            _mockObject = new Tests.ServerLibrary.MockRoot();
            
            _server = new ServerHost(1100, _mockObject);

            _client = new RemoteClient("127.0.0.1:1100");
            _client.AddProxyAssembly(typeof(Tests.ClientLibrary.MockRoot).Assembly);
        }

        [TestCleanup]
        public void CleanUp()
        {
            _client.Dispose();
            _server.Dispose();
        }

        [TestMethod]
        public void GetRootObject_ProxyObjectBase()
        {
            IMockRoot proxy = _client.GetRootObject<IMockRoot>();
            Assert.IsNotNull(proxy, "Returned object is null!");
            Assert.AreEqual(_mockObject.SimpleProperty, proxy.SimpleProperty);
            Assert.AreEqual(_mockObject.Members.Count, proxy.Members.Count);
        }
    }
}
