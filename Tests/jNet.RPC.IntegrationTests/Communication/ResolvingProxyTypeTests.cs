using jNet.RPC.Client;
using jNet.RPC.Server;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using Tests.CommonLibrary;

namespace jNet.RPC.IntegrationTests.Communication
{
    [TestClass]
    public class ResolvingProxyTypeTests
    {

        public static IEnumerable<object[]> GetLocalTestData()
        {
            yield return new object[] { new ServerHost(1024, new Tests.ServerLibrary.MockRoot()), new RemoteClient("127.0.0.1:1024"), typeof(Tests.ClientLibrary.MockRoot) };
            yield return new object[] { new ServerHost(1025, new Tests.ServerLibrary.Level1.MockRoot()), new RemoteClient("127.0.0.1:1025"), typeof(Tests.ClientLibrary.Level1.MockRoot) };
            yield return new object[] { new ServerHost(1026, new Tests.ServerLibrary.Level1.Level2.MockRoot()), new RemoteClient("127.0.0.1:1026"), typeof(Tests.ClientLibrary.Level1.Level2.MockRoot) };            
        }

        [TestMethod]
        [DynamicData(nameof(GetLocalTestData), DynamicDataSourceType.Method)]
        public void ResolveProxyTypesLocal_ProxyObjectBase(ServerHost server, RemoteClient client, Type expectedType)
        {
            client.AddProxyAssembly(typeof(Tests.ClientLibrary.MockRoot).Assembly);

            var proxy = client.GetRootObject<IMockRoot>();

            client.Dispose();
            server.Dispose();

            Assert.IsNotNull(proxy, "GetRootObject returned null!");
            Assert.AreEqual(expectedType, proxy.GetType(), $"Returned type not expected! {proxy} : {expectedType}");
        }

        public static IEnumerable<object[]> GetStandardTestData()
        {
            yield return new object[] { new ServerHost(1028, new Tests.ServerLibrary.MockRoot()), new RemoteClient("127.0.0.1:1028"), typeof(Tests.ClientLibrary.MockRoot) };
            yield return new object[] { new ServerHost(1029, new Tests.ServerLibrary.Level1.MockRoot()), new RemoteClient("127.0.0.1:1029"), typeof(Tests.ClientLibrary.Level1.MockRoot) };
            yield return new object[] { new ServerHost(1030, new Tests.ServerLibrary.Level1.Level2.MockRoot()), new RemoteClient("127.0.0.1:1030"), typeof(Tests.ClientLibrary.Level1.Level2.MockRoot) };
            yield return new object[] { new ServerHost(1035, new Tests.ServerLibrary.Level1.Level2.Level3.MockRoot()), new RemoteClient("127.0.0.1:1035"), typeof(Tests.ClientLibrary.Level1.Level2.Level3.MockRoot) }; //DtoClass Interface TestData
        }

        [TestMethod]
        [DynamicData(nameof(GetStandardTestData), DynamicDataSourceType.Method)]
        public void ResolveProxyTypesAnotherAssembly_ProxyObjectBase(ServerHost server, RemoteClient client, Type expectedType)
        {
            client.AddProxyAssembly(typeof(Tests.ClientLibrary.MockRoot).Assembly);

            var proxy = client.GetRootObject<IMockRoot>();

            client.Dispose();
            server.Dispose();
            
            Assert.IsNotNull(proxy, "GetRootObject returned null!");
            Assert.AreEqual(expectedType, proxy.GetType(), $"Returned type not expected! {proxy} : {expectedType}");
        }

        public static IEnumerable<object[]> GetDefinedTestData()
        {
            yield return new object[] { new ServerHost(1032, new Tests.ServerLibrary.MockRoot()), new RemoteClient("127.0.0.1:1032"), typeof(Tests.ServerLibrary.MockRoot), typeof(Tests.ClientLibrary.MockRoot) };
            yield return new object[] { new ServerHost(1033, new Tests.ServerLibrary.Level1.MockRoot()), new RemoteClient("127.0.0.1:1033"), typeof(Tests.ServerLibrary.Level1.MockRoot), typeof(Tests.ClientLibrary.Level1.MockRoot) };
            yield return new object[] { new ServerHost(1034, new Tests.ServerLibrary.Level1.Level2.MockRoot()), new RemoteClient("127.0.0.1:1034"), typeof(Tests.ServerLibrary.Level1.Level2.MockRoot), typeof(Tests.ClientLibrary.Level1.Level2.MockRoot) };            
        }

        [TestMethod]
        [DynamicData(nameof(GetDefinedTestData), DynamicDataSourceType.Method)]
        public void ResolveProxyAssignedTypes_ProxyObjectBase(ServerHost server, RemoteClient client, Type rootObjectType, Type expectedType)
        {
            client.AddProxyAssembly(typeof(Tests.ClientLibrary.MockRoot).Assembly);

            var proxy = client.GetRootObject<IMockRoot>();

            client.Dispose();
            server.Dispose();

            Assert.IsNotNull(proxy, "GetRootObject returned null!");
            Assert.AreEqual(expectedType, proxy.GetType(), $"Returned wrong type! {proxy} : {expectedType}");
        }
    }
}
