using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace jNet.RPC.Client
{
    public class SerializationBinder : ISerializationBinder
    {
        private class AssemblyNamespaceMapping
        {
            public AssemblyNamespaceMapping(Assembly assembly, string originalNamespace, string proxyNamespace)
            {
                Assembly = assembly;
                OriginalNamespace = originalNamespace;
                ProxyNamespace = proxyNamespace;
            }

            public Assembly Assembly { get; }
            public string OriginalNamespace { get; }
            public string ProxyNamespace { get; }
        }

        private readonly List<AssemblyNamespaceMapping> _assemblyNamespaceMappings = new List<AssemblyNamespaceMapping>();
        private readonly ConcurrentDictionary<Tuple<string, string>, Type> _typeCache = new ConcurrentDictionary<Tuple<string, string>, Type>();

        public void AddProxyAssembly(Assembly assembly)
        {
            var attributes = assembly.GetCustomAttributes<ProxyNamespaceAttribute>();
            foreach (var attribute in attributes)
                _assemblyNamespaceMappings.Add(new AssemblyNamespaceMapping(assembly, attribute.ServerNamespace, attribute.ClientNamespace));
        }         

        public Type BindToType(string assemblyName, string typeName)
        {
            var key = new Tuple<string, string>(assemblyName, typeName);
            if (_typeCache.TryGetValue(key, out var type))
                return type;
            type = FindType(assemblyName, typeName);
            Debug.Assert(type != null);
            _typeCache.TryAdd(key, type);
            return type;
        }

        private Type FindType(string assemblyName, string typeName)
        {
            var mappings = _assemblyNamespaceMappings.Where(m => typeName.StartsWith(m.OriginalNamespace));
            if (!mappings.Any())
                return Type.GetType($"{typeName}, {assemblyName}", true);
            Type type = null;
            foreach (var mapping in mappings)
            {
                type = FindTypeInMappedAssembly(mapping, assemblyName, typeName);
                if (type != null)
                    break;
            }
            if (type == null)
                return Type.GetType($"{typeName}, {assemblyName}", true);
            return type;
        }

        private Type FindTypeInMappedAssembly(AssemblyNamespaceMapping mapping, string assemblyName, string typeName)
        {
            var type = mapping.Assembly.GetTypes().FirstOrDefault(t =>
            {
                var a = t.GetCustomAttribute<DtoTypeAttribute>();
                if (a == null)
                    return false;
                if (a.AssemblyName == assemblyName && a.TypeName == typeName)
                    return true;
                return false;
            });
            if (type != null)
                return type;
            if (mapping.OriginalNamespace.Length >= typeName.Length)
                return null;
            var newTypeName = mapping.ProxyNamespace + typeName.Substring(mapping.OriginalNamespace.Length);
            return mapping.Assembly.GetType(newTypeName);
        }

        public void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            typeName = serializedType.FullName;
            assemblyName = serializedType.Assembly.GetName().Name;
        }
        
    }
}
