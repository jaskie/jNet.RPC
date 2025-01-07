using System;
using System.Collections.Generic;
using System.ComponentModel;
using jNet.RPC.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace jNet.RPC.UnitTests.Client
{
    public interface IBuildedInterface: INotifyPropertyChanged
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
        event EventHandler<TestEventArgs> GenericEvent;
    }

    public class PropertyNotFoundException : Exception
    {
        public PropertyNotFoundException() : base("Property not found") { }
        public PropertyNotFoundException(string message) : base(message) { }
    }

    public abstract class ProxyBase: INotifyPropertyChanged
    {
        private readonly Dictionary<string, object> _propertyValues = new Dictionary<string, object>();
        private readonly Dictionary<string, object[]> _methodInvocationParameters = new Dictionary<string, object[]>();

        public event PropertyChangedEventHandler PropertyChanged;

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

        protected internal virtual void OnEventNotification(string eventName, EventArgs eventArgs)
        {
            if (eventName == nameof(INotifyPropertyChanged.PropertyChanged))
                PropertyChanged?.Invoke(this, (PropertyChangedEventArgs)eventArgs);
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

        internal void RaiseEvent(string eventName, EventArgs args)
        {
            var fieldName = $"_{eventName.Substring(0, 1).ToLowerInvariant()}{eventName.Substring(1)}";
            var fieldInfo = GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var delegateValue = fieldInfo.GetValue(this) as Delegate;
            if (delegateValue != null)
                delegateValue.Method.Invoke(delegateValue.Target, new object[] { this, args});
        }
    }


    public class TestEventArgs : EventArgs
    {
        public int IntValue { get; set; }
    }


    [TestClass]
    public class ProxyBuilderTests
    {
        private ProxyBuilder _sut = new ProxyBuilder(typeof(ProxyBase));
        private Type _proxyType;

        #region property tests
        [TestInitialize]
        public void Initialize()
        {
            _proxyType = _sut.GetProxyTypeFor(typeof(IBuildedInterface));
        }

        private IBuildedInterface CreateProxy()
        {
            return Activator.CreateInstance(_proxyType) as IBuildedInterface;
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
        public void ProxyBuilderTests_PropertyGetOnly()
        {
            var proxy = CreateProxy();
            var generator = new Random();
            for (int i = 0; i < 1000; i++)
            {
                var nb = generator.Next();
                ((ProxyBase)proxy).SetPropertyValue(nb, nameof(IBuildedInterface.IntValueGetOnlyProperty));
                Assert.AreEqual(proxy.IntValueGetOnlyProperty, nb);
            }
        }
        
        [TestMethod]
        public void ProxyBuilderTests_PropertySetOnly()
        {
            var proxy = CreateProxy();
            var generator = new Random();
            for (int i = 0; i < 1000; i++)
            {
                var nb = generator.Next();
                proxy.IntValueSetOnlyProperty = nb;
                Assert.AreEqual(((ProxyBase)proxy).GetPropertyValue<int>(nameof(IBuildedInterface.IntValueSetOnlyProperty)), nb);
            }
        }

        [TestMethod]
        public void ProxyBuilderTests_PropertyGetAndSet()
        {
            var proxy = CreateProxy();
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
        public void ProxyBuilderTests_ParameterPassing()
        {
            var proxy = CreateProxy();
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
        public void ProxyBuilderTests_MethodValueReturn()
        {
            var proxy = CreateProxy();
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
        public void ProxyBuilderTests_SimpleEventInvokation()
        {
            var proxy = CreateProxy();
            int i = 0;
            EventHandler eventHandler = new EventHandler((s, e) =>
            {
                i++;
            });
            proxy.SimpleEvent += eventHandler;
            ((ProxyBase)proxy).RaiseEvent(nameof(IBuildedInterface.SimpleEvent), EventArgs.Empty);
            Assert.AreEqual(1, i);
            ((ProxyBase)proxy).OnEventNotification(nameof(IBuildedInterface.SimpleEvent), EventArgs.Empty);
            Assert.AreEqual(2, i);
            proxy.SimpleEvent -= eventHandler;
            ((ProxyBase)proxy).RaiseEvent(nameof(IBuildedInterface.SimpleEvent), EventArgs.Empty);
            Assert.AreEqual(2, i);
        }
        
        [TestMethod]
        public void ProxyBuilderTests_GenericEventInvokation()
        {
            var proxy = CreateProxy();
            var args = new TestEventArgs { IntValue = 3243423 };
            int i = 0;
            EventHandler<TestEventArgs> eventHandler = new EventHandler<TestEventArgs>((s, e) =>
            {
                i += e.IntValue;
            });
            proxy.GenericEvent += eventHandler;
            ((ProxyBase)proxy).RaiseEvent(nameof(IBuildedInterface.GenericEvent), args);
            Assert.AreEqual(1 * args.IntValue, i);
            ((ProxyBase)proxy).OnEventNotification(nameof(IBuildedInterface.GenericEvent), args);
            Assert.AreEqual(2 * args.IntValue, i);
            proxy.GenericEvent -= eventHandler;
            ((ProxyBase)proxy).RaiseEvent(nameof(IBuildedInterface.GenericEvent), args);
            Assert.AreEqual(2 * args.IntValue, i);
        }

        [TestMethod]
        public void ProxyBuilderTests_PropertyChangedEventIsCalled()
        {
            var proxy = CreateProxy();
            int i = 0;
            proxy.PropertyChanged += (s, e) =>
            {
                i++;
            };
            ((ProxyBase)proxy).OnEventNotification(nameof(INotifyPropertyChanged.PropertyChanged), new PropertyChangedEventArgs(nameof(IBuildedInterface.IntValueFullProperty)));
            Assert.AreEqual(1, i);
        }
        
        [DataTestMethod]
        [DataRow("InvalidEventName")]
        [DataRow(nameof(IBuildedInterface.SimpleEvent))]
        public void ProxyBuilderTests_PropertyChangedEventIsNotCalled(string eventName)
        {
            var proxy = CreateProxy();
            int i = 0;
            proxy.PropertyChanged += (s, e) =>
            {
                i++;
            };
            ((ProxyBase)proxy).OnEventNotification(eventName, EventArgs.Empty);
            Assert.AreEqual(0, i);
        }

    }
}
