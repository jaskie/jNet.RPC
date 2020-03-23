using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace jNet.RPC
{
    class SerializationContractResolver : DefaultContractResolver
    {
        protected override List<MemberInfo> GetSerializableMembers(Type objectType)
        {
            if (typeof(IDto).IsAssignableFrom(objectType))
                return GetAllDtoFieldMembers(objectType).ToList();
            else
                return base.GetSerializableMembers(objectType);
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            if (typeof(IDto).IsAssignableFrom(member.DeclaringType))
            {
                var property = new JsonProperty();
                property.DeclaringType = member.DeclaringType;
                property.PropertyType = GetMemberUnderlyingType(member);
                property.ValueProvider = new ReflectionValueProvider(member);
                property.AttributeProvider = new ReflectionAttributeProvider(member);
                property.Readable = true;
                property.Writable = true;
                property.Ignored = false;
                property.HasMemberAttribute = true;
                property.UnderlyingName = member.Name;
                var name = member.GetCustomAttribute<DtoMemberAttribute>()?.PropertyName;
                property.PropertyName = name ?? member.Name;
                return property;
            }
            var defaultProperty = base.CreateProperty(member, memberSerialization);
            defaultProperty.TypeNameHandling = TypeNameHandling.Objects | TypeNameHandling.Arrays;
            return defaultProperty;
        }

        private static Type GetMemberUnderlyingType(MemberInfo member)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    return ((FieldInfo)member).FieldType;
                case MemberTypes.Property:
                    return ((PropertyInfo)member).PropertyType;
                case MemberTypes.Event:
                    return ((EventInfo)member).EventHandlerType;
                case MemberTypes.Method:
                    return ((MethodInfo)member).ReturnType;
                default:
                    throw new ArgumentException("MemberInfo must be of type FieldInfo, PropertyInfo, EventInfo or MethodInfo", nameof(member));
            }
        }

        private static IEnumerable<MemberInfo> GetAllDtoFieldMembers(Type type)
        {
            var rootType = type;
            while (type != null)
            {
                foreach (var member in type.GetMembers(
                    type == rootType 
                    ? BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public 
                    : BindingFlags.Instance | BindingFlags.NonPublic)
                    .Where(m => m.GetCustomAttribute<DtoMemberAttribute>(false) != null))
                    yield return member;
                type = type.BaseType;
            }
        }

        protected override JsonObjectContract CreateObjectContract(Type objectType)
        {
            var contract = base.CreateObjectContract(objectType);
            if (typeof(IDto).IsAssignableFrom(objectType))
            {
                contract.IsReference = true;
                contract.MemberSerialization = MemberSerialization.OptIn;
            }
            return contract;
        }

        protected override JsonStringContract CreateStringContract(Type objectType)
        {
            var contract = base.CreateStringContract(objectType);
            if (typeof(System.Drawing.Bitmap).IsAssignableFrom(objectType))
                contract.Converter = new BitmapJsonConverter();
            return contract;
        }


    }

}
