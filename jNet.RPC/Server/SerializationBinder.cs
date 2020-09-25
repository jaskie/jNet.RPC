using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace jNet.RPC.Server
{
    public class SerializationBinder : ISerializationBinder
    {
        private readonly ConcurrentDictionary<Tuple<string, string>, Type> _typeCache = new ConcurrentDictionary<Tuple<string, string>, Type>();

        public void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            var dtoClassAttribute = serializedType.GetCustomAttribute<DtoTypeAttribute>(false);
            typeName = dtoClassAttribute?.TypeName ?? serializedType.FullName;
            assemblyName = dtoClassAttribute?.AssemblyName ?? serializedType.Assembly.GetName().Name;
        }

        public Type BindToType(string assemblyName, string typeName)
        {
            var key = new Tuple<string, string>(assemblyName, typeName);
            if (_typeCache.TryGetValue(key, out var type))
                return type;
            type = Type.GetType($"{typeName}, {assemblyName}", true);
            _typeCache.TryAdd(key, type);
            return type;
        }
    }
}
