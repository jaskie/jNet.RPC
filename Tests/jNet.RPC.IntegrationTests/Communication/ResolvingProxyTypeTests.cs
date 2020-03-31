using jNet.RPC.Client;
using jNet.RPC.Server;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace jNet.RPC.IntegrationTests.Communication
{
    [TestClass]
    public class ResolvingProxyTypeTests
    {
        public static IEnumerable<object[]> GetServerClientPair()
        {
            yield return new object[] { new ServerHost(1024, new Tests.ServerLibrary.MockRoot()), new RemoteClient(),  typeof(Tests.ClientLibrary.MockRoot)};
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
