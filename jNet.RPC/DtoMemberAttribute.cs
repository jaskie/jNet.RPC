using System;

namespace jNet.RPC
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class DtoMemberAttribute : Attribute
    {
        public DtoMemberAttribute(string propertyName = null)
        {
            PropertyName = propertyName;
        }
        public string PropertyName { get; set; }
    }
}
