using jNet.RPC.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace jNet.RPC.UnitTests.Client
{
    [TestClass]
    public class DefaultSerializationBinderTests
    {
        private DefaultSerializationBinder _binder;
        [ClassInitialize]
        public void Initialize()
        {
            _binder = new DefaultSerializationBinder(Assembly.GetExecutingAssembly());
        }

        [ClassCleanup]
        public void CleanUp()
        {

        }
    }
}
