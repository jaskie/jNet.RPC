using System;
using System.Collections.Generic;
using jNet.RPC.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace jNet.RPC.UnitTests.Client
{
    public interface IBuildedInterface
    {
        int IntValueGetOnlyProperty { get; }
        int IntValueSetOnlyProperty { set; }
        int IntValueFullProperty { get; set; }
        void VoidVoidMethod();
        void OneParamVoidMethod(int i);
        void TwoParamsVoidMethod(int i1, int i2);
        void ThreeParamsVoidMethod(int i, string s, IBuildedInterface buildedInterface);
        int OneParamIntMethod(int i);
        int TwoParamsIntMethod(int i1, int i2);
        event EventHandler SimpleEvent;
    }

    public class PropertyNotFoundException : Exception
    {
        public PropertyNotFoundException() : base("Property not found") { }
        public PropertyNotFoundException(string message) : base(message) { }
    }

    public abstract class ProxyBase
    {
        private readonly Dictionary<string, object> _propertyValues = new Dictionary<string, object>();
        private readonly Dictionary<string, object[]> _methodInvocationParameters = new Dictionary<string, object[]>();

        protected void Set<T>(T value, string propertyName)
        {
            _propertyValues[propertyName] = value;
        }

        protected void Invoke(string methodName, params object[] parameters)
        {
            _methodInvocationParameters[methodName] = parameters;
        }

        protected T Query<T>(string methodName, object[] parameters)
        {
            object v = parameters.Length == 1 ? (int)parameters[0] + 1 : (int)parameters[0] + (int)parameters[1];
            return (T)v;
        }

        protected void EventAdd<T>(T handler, string eventName)
        {

        }

        protected void EventRemove<T>(T handler, string eventName)
        {

        }

        protected abstract void OnEventNotification(SocketMessage message);

        protected T Deserialize<T>(SocketMessage message) where T: new()
        {
            return new T();
        }

        internal T GetPropertyValue<T>(string propertyName)
        {
            if (_propertyValues.TryGetValue(propertyName, out var value))
                return (T)value;
            throw new PropertyNotFoundException("Property is not set yet");
        }

        internal void SetPropertyValue(object value, string propertyName)
        {
            var type = GetType();
            var fieldName = $"_{propertyName.Substring(0, 1).ToLowerInvariant()}{propertyName.Substring(1)}";
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field.SetValue(this, value);
        }

        internal object[] GetMethodInvocationParameters(string methodName)
        {
            return _methodInvocationParameters[methodName];
        }

        internal void RaiseEvent(string eventName)
        {
            var fieldName = $"_{eventName.Substring(0, 1).ToLowerInvariant()}{eventName.Substring(1)}";
            var fieldInfo = GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var delegateValue = fieldInfo.GetValue(this) as Delegate;
            if (delegateValue != null)
                delegateValue.Method.Invoke(delegateValue.Target, new object[] { this, EventArgs.Empty});
        }

        internal void CallOnEventNotification(string eventName)
        {
            OnEventNotification(new SocketMessage((object)null) { MessageType = SocketMessage.SocketMessageType.EventNotification, MemberName = eventName });
        }

        
    }

    [TestClass]
    public class ProxyBuilderTests
    {
        private ProxyBuilder _proxyBuilder = new ProxyBuilder(typeof(ProxyBase));
        private Type _proxyType;

        #region property tests
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
        public void PropertyGetOnly()
        {
            var proxy = Activator.CreateInstance(_proxyType) as IBuildedInterface;
            var generator = new Random();
            for (int i = 0; i < 1000; i++)
            {
                var nb = generator.Next();
                ((ProxyBase)proxy).SetPropertyValue(nb, nameof(IBuildedInterface.IntValueGetOnlyProperty));
                Assert.AreEqual(proxy.IntValueGetOnlyProperty, nb);
            }            
        }
        
        [TestMethod]
        public void PropertySetOnly()
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
        #endregion // property tests

        [TestMethod]
        public void ParameterPassing()
        {
            var proxy = Activator.CreateInstance(_proxyType) as IBuildedInterface;
            proxy.VoidVoidMethod();
            Assert.AreEqual(((ProxyBase)proxy).GetMethodInvocationParameters(nameof(IBuildedInterface.VoidVoidMethod)).Length, 0);
            var generator = new Random();
            for (int i = 0; i < 100; i++)
            {
                var nb = generator.Next();
                proxy.OneParamVoidMethod(nb);
                Assert.AreEqual(nb, ((ProxyBase)proxy).GetMethodInvocationParameters(nameof(IBuildedInterface.OneParamVoidMethod))[0]);
            }
            for (int i = 0; i < 100; i++)
            {
                var nb = generator.Next();
                proxy.TwoParamsVoidMethod(nb, nb + 1);
                Assert.AreEqual(nb, ((ProxyBase)proxy).GetMethodInvocationParameters(nameof(IBuildedInterface.TwoParamsVoidMethod))[0]);
                Assert.AreEqual(nb + 1, ((ProxyBase)proxy).GetMethodInvocationParameters(nameof(IBuildedInterface.TwoParamsVoidMethod))[1]);
            }
            for (int i = 0; i < 100; i++)
            {
                var nb = generator.Next();
                var s = nb.ToString();

                proxy.ThreeParamsVoidMethod(nb, s, proxy);
                Assert.AreEqual(nb, ((ProxyBase)proxy).GetMethodInvocationParameters(nameof(IBuildedInterface.ThreeParamsVoidMethod))[0]);
                Assert.AreEqual(s, ((ProxyBase)proxy).GetMethodInvocationParameters(nameof(IBuildedInterface.ThreeParamsVoidMethod))[1]);
                Assert.AreEqual(proxy, ((ProxyBase)proxy).GetMethodInvocationParameters(nameof(IBuildedInterface.ThreeParamsVoidMethod))[2]);
            }
        }

        [TestMethod]
        public void MethodValueReturn()
        {
            var proxy = Activator.CreateInstance(_proxyType) as IBuildedInterface;
            var generator = new Random();
            
            for (int i = 0; i < 100; i++)
            {
                var nb = generator.Next();
                var inc = proxy.OneParamIntMethod(nb);
                Assert.AreEqual(nb+1, inc);
            }
            for (int i = 0; i < 100; i++)
            {
                var nb1 = generator.Next();
                var nb2 = generator.Next();
                var sum = proxy.TwoParamsIntMethod(nb1, nb2);
                Assert.AreEqual(nb1 + nb2, sum);
            }
        }

        [TestMethod]
        public void SimpleEventInvokation()
        {
            var proxy = Activator.CreateInstance(_proxyType) as IBuildedInterface;
            int i = 0;
            EventHandler eventHandler = new EventHandler((s, e) =>
            {
                i++;
            });
            proxy.SimpleEvent += eventHandler;
            ((ProxyBase)proxy).RaiseEvent(nameof(IBuildedInterface.SimpleEvent));
            Assert.AreEqual(1, i);
            ((ProxyBase)proxy).CallOnEventNotification(nameof(IBuildedInterface.SimpleEvent));
            Assert.AreEqual(2, i);
            proxy.SimpleEvent -= eventHandler;
            ((ProxyBase)proxy).RaiseEvent(nameof(IBuildedInterface.SimpleEvent));
            Assert.AreEqual(2, i);

        }

    }
}
