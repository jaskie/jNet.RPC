using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace jNet.RPC.Client
{
    public class DefaultSerializationBinder : ISerializationBinder
    {
        //Tuple - first string is assembly name, second type name
        private readonly ConcurrentDictionary<Tuple<string, string>, Type> _typesDictionary = new ConcurrentDictionary<Tuple<string, string>, Type>();
        private readonly HashSet<string> _assemblies = new HashSet<string>();
        
        public DefaultSerializationBinder(Assembly callingAssembly)
        {            
            _assemblies.Add(callingAssembly.FullName);                        
        }

        public void AddProxyTypeAssignment(string sourceTypeFullName, Type targetType, string sourceAssemblyName = null)
        {
            _typesDictionary.TryAdd(new Tuple<string, string>(sourceAssemblyName, sourceTypeFullName), targetType);
        }

        public void AddProxyAssembly(string assemblyName)
        {
            _assemblies.Add(assemblyName);
        }         
        
        private Type GetPrefferedType(List<Type> types, string sourceNamespace)
        {
            int accuracy = 0;
            Type foundType = null;

            foreach (var type in types)
            {
                int localAccuracy = 0;
                var sourceNamespaces = sourceNamespace.Split('.').ToList();
                var targetNamespaces = type.FullName.Split('.').ToList();               

                for (int i = targetNamespaces.Count() - 1, j = sourceNamespaces.Count() - 1; i > -1 && j > -1; --i, --j)
                {
                    if (targetNamespaces[i] != sourceNamespaces[j])
                        break;

                    ++localAccuracy;
                }

                if (localAccuracy <= accuracy)
                    continue;

                accuracy = localAccuracy;
                foundType = type;
            }

            return foundType;
        }

        private Type FindType(Tuple<string,string> typeQualifiedName)
        {
            var assemblyName = typeQualifiedName.Item1;
            var typeFullName = typeQualifiedName.Item2;
            var sourceNamespaces = typeFullName.Split('.');

            List<Type> dtoTypes = new List<Type>();
            List<Type> proxyTypes = new List<Type>();

            foreach (var a in _assemblies)
            {
                Assembly assembly = Assembly.Load(a); //with ReflectionOnlyLoad IsAssignableFrom (in Newtonsoft InternalReader) needs both types to be in ReflectionOnly context: https://stackoverflow.com/questions/3008097/reflectiononlyload-and-getfield

                dtoTypes = dtoTypes.Concat(assembly.GetTypes().Where(t => t.GetCustomAttribute<DtoClassAttribute>(false)?.Name == typeFullName).ToList()).ToList();
                proxyTypes = proxyTypes.Concat(assembly.GetTypes().Where(t => typeof(ProxyObjectBase).IsAssignableFrom(t))
                                                                  .Where(t => t.FullName.Contains(sourceNamespaces.LastOrDefault())).ToList()).ToList();
            }           

            if (dtoTypes.Count > 0)
            {
                if (dtoTypes.Count == 1)
                    return dtoTypes.FirstOrDefault();

                return GetPrefferedType(dtoTypes, typeFullName);
            }
            else if (proxyTypes.Count > 0)
            {
                if (proxyTypes.Count == 1)
                    return proxyTypes.FirstOrDefault();

                return GetPrefferedType(proxyTypes, typeFullName);
            }
            else
            {
                foreach (var a in _assemblies)
                {
                    Assembly assembly = Assembly.Load(a); //with ReflectionOnlyLoad IsAssignableFrom (in Newtonsoft InternalReader) needs both types to be in ReflectionOnly context: https://stackoverflow.com/questions/3008097/reflectiononlyload-and-getfield

                    dtoTypes = dtoTypes.Concat(assembly.GetTypes().Where(t => typeof(ProxyObjectBase).IsAssignableFrom(t) &&
                                                                         t.GetInterfaces().FirstOrDefault(i => i.FullName == typeFullName) != null))
                                                                         .ToList();
                }

                if (dtoTypes.Count>0)
                    return dtoTypes.FirstOrDefault();
            }
            return Type.GetType($"{typeFullName}, {assemblyName}", true);            
        }

        public Type BindToType(string assemblyName, string typeName)
        {
            if (_typesDictionary.TryGetValue(new Tuple<string, string>(null, typeName), out var type))
                return type;

            return _typesDictionary.GetOrAdd(new Tuple<string, string>(assemblyName, typeName), (x) => FindType(x));                             
        }

        public void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            typeName = serializedType.FullName;
            assemblyName = serializedType.Assembly.FullName;
        }
    }
}
