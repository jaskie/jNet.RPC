﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
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
            var _serverHost = new ServerHost() { ListenPort = 1024 };                        
            Assert.IsTrue(_serverHost.Initialize(new MockServerObject(), PrincipalProvider.Default), "ServerHost did not initiated correctly");
        }

        [TestMethod()]
        public void InitializeTest_PortUnder1024()
        {
            var _serverHost = new ServerHost() { ListenPort = 1023 };
            Assert.IsFalse(_serverHost.Initialize(new MockServerObject(), PrincipalProvider.Default), "ServerHost initiated with port <1024");
        }
    }
}