using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace jNet.RPC.Client
{
    public class TypeNameBinder : ISerializationBinder
    {
        private readonly ConcurrentDictionary<string, Type> _typesDictionary = new ConcurrentDictionary<string, Type>();
        private readonly List<string> _assemblies = new List<string>();
        
        public TypeNameBinder(Assembly callingAssembly)
        {            
            _assemblies.Add(callingAssembly.FullName);                        
        }

        public void AddProxyTypeAssignment(string sourceTypeFullName, Type t)
        {
            var test = _typesDictionary.TryAdd(sourceTypeFullName, t);
        }

        public void AddProxyAssembly(string assemblyName)
        {
            _assemblies.Add(assemblyName);
        }

        private Type RegisterType(string typeFullName)
        {
            var sourceNamespaces = typeFullName.Split('.');
            int accuracy = 0;
            Type foundType = null;
            
            foreach (var a in _assemblies)
            {
                Assembly assembly = Assembly.Load(a); //with ReflectionOnlyLoad IsAssignableFrom (in Newtonsoft InternalReader) needs both types to be in ReflectionOnly context: https://stackoverflow.com/questions/3008097/reflectiononlyload-and-getfield
                List<Type> dtoTypes = assembly.GetTypes().Where(t => typeof(ProxyObjectBase).IsAssignableFrom(t))
                                                         .Where(t => t.FullName.Contains(sourceNamespaces.LastOrDefault())).ToList();

                if (dtoTypes.Count() == 1 && _assemblies.Count == 1)
                {
                    Debug.WriteLine($"Source: {typeFullName}");
                    Debug.WriteLine($"There is only one type found matching source type: {dtoTypes.FirstOrDefault()}");
                    return dtoTypes.FirstOrDefault();
                }                    
                else
                {                    
                    foreach (var type in dtoTypes)
                    {
                        var localNamespaces = type.FullName.Remove(0,type.Assembly.GetName().Name.Length+1)
                                                           .Split('.')
                                                           .ToList();
                        var localAccuracy = 0;
                        for (int i = localNamespaces.Count() - 1, j = sourceNamespaces.Count() - 1; i > -1 && j > -1; --i, --j)
                        {
                            if (localNamespaces[i] != sourceNamespaces[j])
                                break;

                            ++localAccuracy;                            
                        }
                                       
                        if (localAccuracy > accuracy)
                        {
                            accuracy = localAccuracy;
                            foundType = type;
                        }                                                    
                    }                                    
                }
            }
            
            if (foundType != null)
            {
                Debug.WriteLine($"Source: {typeFullName}");
                Debug.WriteLine($"Found proxy type: {foundType.FullName}, Accuracy: {accuracy}");
                return foundType;
            }
            else
            {
                Debug.WriteLine($"Could not find proxy type: {typeFullName}");
                return Type.GetType($"{typeFullName}", true);
            }            
        }

        public Type BindToType(string assemblyName, string typeName)
        {
            return _typesDictionary.GetOrAdd(typeName, RegisterType);                                    
        }

        public void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            typeName = serializedType.FullName;
            assemblyName = serializedType.Assembly.FullName;
        }
    }
}
