using Newtonsoft.Json.Serialization;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace jNet.RPC.Server
{
    public class DefaultSerializationBinder : ISerializationBinder
    {        
        public void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            var dtoClassAttribute = serializedType.GetCustomAttribute<DtoClassAttribute>(false);            
            
            typeName = dtoClassAttribute?.Name ?? serializedType.FullName;                           
            assemblyName = serializedType.Assembly.FullName;
        }

        public Type BindToType(string assemblyName, string typeName)
        {
            return Type.GetType($"{typeName}, {assemblyName}", true);
        }
    }
}
