using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using jNet.RPC.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace jNet.RPC.UnitTests.Client
{
    public interface IBuildedInterface
    {
        int IntValueGetOnlyProperty { get; }
        int IntValueSetOnlyProperty { set; }
        int IntValueFullProperty { get; set; }
    }

    public class PropertyNotFoundException : Exception
    {
        public PropertyNotFoundException() : base("Property not found") { }
        public PropertyNotFoundException(string message) : base(message) { }
    }

    public abstract class ProxyBase: IDisposable
    {
        private readonly Dictionary<string, object> _propertyValues = new Dictionary<string, object>();

        public void Dispose()
        {
            
        }

        protected T Get<T>(string propertyName)
        {
            if (_propertyValues.TryGetValue(propertyName, out var value))
                return (T)value;
            throw new PropertyNotFoundException("Property is not set yet");
        }

        protected void Set<T>(T value, string propertyName)
        {
            _propertyValues[propertyName] = value;
        }

        protected void Invoke(string methodName, params object[] parameters)
        {

        }

        protected T Query<T>(string methodName, params object[] parameters)
        {
            return default;
        }

        internal T GetPropertyValue<T>(string propertyName)
        {
            if (_propertyValues.TryGetValue(propertyName, out var value))
                return (T)value;
            throw new PropertyNotFoundException("Property is not set yet");
        }
    }




    [TestClass]
    public class ProxyBuilderTests
    {
        private ProxyBuilder _proxyBuilder = new ProxyBuilder(typeof(ProxyBase));
        private Type _proxyType;

        [TestInitialize]
        public void Initialize()
        {
            _proxyType = _proxyBuilder.GetProxyTypeFor(typeof(IBuildedInterface));
        }

        [TestMethod]
        public void PropertiesCheckAttributes()
        {
            Assert.IsTrue(_proxyType.GetProperty(nameof(IBuildedInterface.IntValueFullProperty)).CanRead);
            Assert.IsTrue(_proxyType.GetProperty(nameof(IBuildedInterface.IntValueFullProperty)).CanWrite);
            Assert.IsTrue(_proxyType.GetProperty(nameof(IBuildedInterface.IntValueGetOnlyProperty)).CanRead);
            Assert.IsFalse(_proxyType.GetProperty(nameof(IBuildedInterface.IntValueGetOnlyProperty)).CanWrite);
            Assert.IsFalse(_proxyType.GetProperty(nameof(IBuildedInterface.IntValueSetOnlyProperty)).CanRead);
            Assert.IsTrue(_proxyType.GetProperty(nameof(IBuildedInterface.IntValueSetOnlyProperty)).CanWrite);
        }


        [TestMethod]
        public void PropertySet()
        {
            var proxy = Activator.CreateInstance(_proxyType) as IBuildedInterface;
            var generator = new Random();
            for (int i = 0; i < 1000; i++)
            {
                var nb = generator.Next();
                proxy.IntValueSetOnlyProperty = nb;
                Assert.AreEqual(((ProxyBase)proxy).GetPropertyValue<int>(nameof(IBuildedInterface.IntValueSetOnlyProperty)), nb);
            }
            
        }

        [TestMethod]
        public void PropertyGetAndSet()
        {
            var proxy = Activator.CreateInstance(_proxyType) as IBuildedInterface;
            var generator = new Random();
            for (int i = 0; i < 1000; i++)
            {
                var nb = generator.Next();
                proxy.IntValueFullProperty = nb;
                Assert.AreEqual(nb, proxy.IntValueFullProperty);
            }
        }
    }
}
