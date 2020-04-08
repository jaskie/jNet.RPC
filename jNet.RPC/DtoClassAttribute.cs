using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jNet.RPC
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DtoClassAttribute : Attribute
    {
        public string Name { get; }
        public DtoClassAttribute(string dtoName = null)
        {
            Name = dtoName;
        }
    }
}
