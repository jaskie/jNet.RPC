using System;

namespace jNet.RPC
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DtoTypeAttribute : Attribute
    {
        public string TypeName { get; }
        public string AssemblyName { get; }
        public DtoTypeAttribute(Type type)
        {
            TypeName = type.FullName;
            AssemblyName = type.Assembly.GetName().Name;
        }
        public DtoTypeAttribute(string assemblyName, string typeName)
        {
            TypeName = typeName;
            AssemblyName = assemblyName;
        }
    }
}
