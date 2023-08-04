using jNet.RPC.Client;
using jNet.RPC.UnitTests.MockModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace jNet.RPC.UnitTests.Client
{
    [TestClass]
    public class ClientReferenceResolverTests
    {
        ReferenceResolver _clientReferenceResolver;
        Dictionary<Guid, WeakReference<ProxyObjectBase>> _knownDtos;
        MockProxyObject _mockObject;

        [TestInitialize]
        public void Initialize()
        {
            _mockObject = new MockProxyObject { DtoGuid = Guid.NewGuid() };
            _clientReferenceResolver = new ReferenceResolver();
            _knownDtos = _clientReferenceResolver.KnownDtos;
        }

        #region ResolveReference_Guid
        [TestMethod]
        public void ResolveReferenceGuid_Referenced_ReturnProxy()
        {
            _knownDtos.Add(_mockObject.DtoGuid, new WeakReference<ProxyObjectBase>(_mockObject));            
            var proxy = _clientReferenceResolver.ResolveReference(_mockObject.DtoGuid);
            Assert.AreEqual(_mockObject, proxy, "Objects are not the same!");
        }

        [TestMethod]
        public void ResolveReferenceGuid_NonReferenced_ReturnNull()
        {                       
            var proxy = _clientReferenceResolver.ResolveReference(_mockObject.DtoGuid);
            Assert.IsNull(proxy, "Proxy not null!");
        }
        #endregion

        #region AddReference
        [TestMethod]
        public void AddReference_NotExisting_AddToKnown()
        {                        
            var knownDtosInitialCount = _knownDtos.Count;
            _clientReferenceResolver.AddReference(this, _mockObject.DtoGuid.ToString(), _mockObject);
                        
            Assert.AreEqual(knownDtosInitialCount + 1, _knownDtos.Count);
            _knownDtos[_mockObject.DtoGuid].TryGetTarget(out var target);
            Assert.AreEqual(_mockObject, target);
        }

        [TestMethod]
        public void AddReference_Existing_Populate()
        {            
            _clientReferenceResolver.AddReference(this, _mockObject.DtoGuid.ToString(), _mockObject);

            var knownDtosInitialCount = _knownDtos.Count;
            _clientReferenceResolver.AddReference(this, _mockObject.DtoGuid.ToString(), _mockObject);
            var dict = _clientReferenceResolver.ProxiesToPopulate;
            Assert.AreEqual(dict[_mockObject.DtoGuid], _mockObject, "Wrong object added to population.");
            Assert.AreEqual(knownDtosInitialCount, _knownDtos.Count, "KnownDtos shouldn't increased!");
        }
        #endregion

        #region GetReference
        [TestMethod]
        public void GetReference_AnyDto_ReturnGuid()
        {           
            var guid = _clientReferenceResolver.GetReference(this, _mockObject);
            Assert.AreEqual(_mockObject.DtoGuid.ToString(), guid);
        }

        [TestMethod]
        public void GetReference_NonDto_ReturnEmpty()
        {            
            var referenced = _clientReferenceResolver.GetReference(this, new object());
            Assert.IsTrue(referenced == String.Empty);
        }
        #endregion

        #region IsReferenced
        [TestMethod]
        public void IsReferenced_AnyDto_ReturnTrue()
        {            
            var referenced = _clientReferenceResolver.IsReferenced(this, _mockObject);
            Assert.IsTrue(referenced);
        }

        [TestMethod]
        public void IsReferenced_NotDto_ReturnTrue()
        {            
            var referenced = _clientReferenceResolver.IsReferenced(this, new object());
            Assert.IsFalse(referenced);
        }
        #endregion

        #region ResolveReference
        [TestMethod]
        public void ResolveReference_ReferenceAlive_ReturnDto()
        {
            _knownDtos.Add(_mockObject.DtoGuid, new WeakReference<ProxyObjectBase>(_mockObject));

            var proxy = _clientReferenceResolver.ResolveReference(this, _mockObject.DtoGuid.ToString());
            Assert.AreEqual(_mockObject, proxy);
        }

        [TestMethod]
        public void ResolveReference_NonReferenced_ReturnNull()
        {            
            var proxy = _clientReferenceResolver.ResolveReference(this, _mockObject.DtoGuid.ToString());
            Assert.IsNull(proxy);
        }
        #endregion
    }
}
