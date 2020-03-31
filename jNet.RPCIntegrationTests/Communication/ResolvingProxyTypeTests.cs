using jNet.RPC.Client;
using jNet.RPC.Server;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace jNet.RPCIntegrationTests.Communication
{
    [TestClass]
    public class ResolvingProxyTypeTests
    {
        public static IEnumerable<object[]> GetServerClientPair()
        {
            yield return new object[] { new ServerHost(1024, new jNet.RPC.ServerLib.MockRoot()), new RemoteClient(),  typeof(jNet.RPC.ClientLib.MockRoot)};
            yield return new object[] { 12, 30, 42 };
            yield return new object[] { 14, 1, 15 };
        }

        [TestMethod]
        [DynamicData(nameof(GetServerClientPair), DynamicDataSourceType.Method)]
        public void ResolveProxyTypes_ProxyObjectBase(ServerHost server, RemoteClient client, Type expected)
        {

        }
    }
}
