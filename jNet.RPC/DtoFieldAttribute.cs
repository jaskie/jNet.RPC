using System;

namespace jNet.RPC
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class DtoFieldAttribute : Attribute
    {
        public DtoFieldAttribute(string propertyName = null)
        {
            PropertyName = propertyName;
        }
        public string PropertyName { get; set; }
    }
}
