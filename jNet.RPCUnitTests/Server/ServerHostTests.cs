using Microsoft.VisualStudio.TestTools.UnitTesting;
using jNet.RPC.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using jNet.RPCUnitTests.MockModel;

namespace jNet.RPC.Server.Tests
{
    [TestClass()]
    public class ServerHostTests
    {        
        [TestMethod()]
        public void InitializeTest_PortAbove1023()
        {
            var _serverHost = new ServerHost(1024, new MockServerObject());                        
            Assert.IsTrue(_serverHost.Start(), "ServerHost did not initiated correctly");
        }

        [TestMethod()]
        public void InitializeTest_PortUnder1024()
        {
            var _serverHost = new ServerHost(1023, new MockServerObject());
            Assert.IsFalse(_serverHost.Start(), "ServerHost initiated with port <1024");
        }
    }
}