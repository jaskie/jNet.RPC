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
            yield return new object[] { new ServerHost(1024, new Tests.ServerLibrary.MockRoot()), new RemoteClient(), typeof(Model.Client.MockRoot) };
            yield return new object[] { new ServerHost(1025, new Tests.ServerLibrary.Level1.MockRoot()), new RemoteClient(), typeof(Model.Client.MockRoot) };
            yield return new object[] { new ServerHost(1026, new Tests.ServerLibrary.Level1.Level2.MockRoot()), new RemoteClient(), typeof(Model.Client.MockRoot) };
            yield return new object[] { new ServerHost(1027, new Tests.ServerLibrary.Level1.Level2.Level3.MockRoot()), new RemoteClient(), typeof(Model.Client.MockRoot) };
        }        

        [TestMethod]
        [DynamicData(nameof(GetLocalTestData), DynamicDataSourceType.Method)]
        public void ResolveProxyTypesLocal_ProxyObjectBase(ServerHost server, RemoteClient client, Type expectedType)
        {
            server.Start();           
            client.Connect($"127.0.0.1:{server.ListenPort}").Wait();

            var proxy = client.GetRootObject<IMockRoot>();
            Assert.IsNotNull(proxy, "GetRootObject returned null!");
            Assert.IsTrue(proxy.GetType() == expectedType, $"Returned type not expected! {proxy} : {expectedType}");
        }

        public static IEnumerable<object[]> GetStandardTestData()
        {
            yield return new object[] { new ServerHost(1028, new Tests.ServerLibrary.MockRoot()), new RemoteClient(), typeof(Model.Client.MockRoot) };
            yield return new object[] { new ServerHost(1029, new Tests.ServerLibrary.Level1.MockRoot()), new RemoteClient(), typeof(Tests.ClientLibrary.Level1.MockRoot) };
            yield return new object[] { new ServerHost(1030, new Tests.ServerLibrary.Level1.Level2.MockRoot()), new RemoteClient(), typeof(Tests.ClientLibrary.Level1.Level2.MockRoot) };
            yield return new object[] { new ServerHost(1031, new Tests.ServerLibrary.Level1.Level2.Level3.MockRoot()), new RemoteClient(), typeof(Tests.ClientLibrary.Level1.Level2.Level3.MockRoot) };
        }

        [TestMethod]
        [DynamicData(nameof(GetStandardTestData), DynamicDataSourceType.Method)]
        public void ResolveProxyTypesAnotherAssembly_ProxyObjectBase(ServerHost server, RemoteClient client, Type expectedType)
        {
            server.Start();
            
            client.DefaultBinder.AddProxyAssembly("Tests.ClientLibrary");
            client.Connect($"127.0.0.1:{server.ListenPort}").Wait();

            var proxy = client.GetRootObject<IMockRoot>();
            Assert.IsNotNull(proxy, "GetRootObject returned null!");
            Assert.IsTrue(proxy.GetType() == expectedType, $"Returned type not expected! {proxy} : {expectedType}");
        }

        public static IEnumerable<object[]> GetDefinedTestData()
        {
            yield return new object[] { new ServerHost(1032, new Tests.ServerLibrary.MockRoot()), new RemoteClient(), typeof(Tests.ServerLibrary.MockRoot), typeof(Model.Client.MockRoot) };
            yield return new object[] { new ServerHost(1033, new Tests.ServerLibrary.Level1.MockRoot()), new RemoteClient(), typeof(Tests.ServerLibrary.Level1.MockRoot), typeof(Tests.ClientLibrary.Level1.Level2.Level3.MockRoot) };
            yield return new object[] { new ServerHost(1034, new Tests.ServerLibrary.Level1.Level2.MockRoot()), new RemoteClient(), typeof(Tests.ServerLibrary.Level1.Level2.MockRoot), typeof(Tests.ClientLibrary.Level1.MockRoot) };
            yield return new object[] { new ServerHost(1035, new Tests.ServerLibrary.Level1.Level2.Level3.MockRoot()), new RemoteClient(), typeof(Tests.ServerLibrary.Level1.Level2.Level3.MockRoot), typeof(Tests.ClientLibrary.Level1.Level2.MockRoot) };
        }

        [TestMethod]
        [DynamicData(nameof(GetDefinedTestData), DynamicDataSourceType.Method)]
        public void ResolveProxyAssignedTypes_ProxyObjectBase(ServerHost server, RemoteClient client, Type rootObjectType, Type expectedType)
        {
            server.Start();
            
            client.DefaultBinder.AddProxyTypeAssignment(rootObjectType.FullName, expectedType);                                    
            client.Connect($"127.0.0.1:{server.ListenPort}").Wait();

            var proxy = client.GetRootObject<IMockRoot>();
            Assert.IsNotNull(proxy, "GetRootObject returned null!");
            Assert.IsTrue(proxy.GetType() == expectedType, $"Returned wrong type! {proxy} : {expectedType}");
        }
    }
}
