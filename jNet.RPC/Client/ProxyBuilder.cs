using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace jNet.RPC.Client
{
    internal static class ProxyBuilder
    {
        const string GeneratedAssemblyName = "jNet.RPC.Client.GeneratedProxies";

        static readonly ModuleBuilder ModuleBuilder;

        static ProxyBuilder()
        {
            AssemblyName asmName = new AssemblyName(GeneratedAssemblyName);
            AssemblyBuilder asmBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.RunAndSave);
            ModuleBuilder = asmBuilder.DefineDynamicModule(GeneratedAssemblyName, $"{GeneratedAssemblyName}.dll");
        }

        public static Type GetProxyTypeFor(Type interfaceType)
        {
            var typeName = $"{GeneratedAssemblyName}.{(interfaceType.Name.StartsWith("I") ? interfaceType.Name.Substring(1) : interfaceType.Name)}";
            TypeBuilder typeBuilder = ModuleBuilder.DefineType(typeName, TypeAttributes.Class | TypeAttributes.Public, typeof(ProxyObjectBase));
            typeBuilder.AddInterfaceImplementation(interfaceType);
            AddMethodOnEventNotification(typeBuilder);
            var implementedInterfaces = (new Type[] { interfaceType }).Concat(interfaceType.GetInterfaces()).ToArray();
            var interfaceProperties = implementedInterfaces.SelectMany(i => i.GetProperties()).Distinct().ToArray();
            foreach (var property in interfaceProperties)
                AddProperty(typeBuilder, property);
            foreach (var method in implementedInterfaces.SelectMany(i => i.GetMethods().Where(m => !m.IsSpecialName)).Distinct())
                AddMethod(typeBuilder, method);
            foreach (var @event in implementedInterfaces.SelectMany(i => i.GetEvents()).Distinct())
                AddEvent(typeBuilder, @event);
            return typeBuilder.CreateType();
        }

        private static void AddMethodOnEventNotification(TypeBuilder typeBuilder)
        {
            var methodBuilderOnEventNotification = typeBuilder.DefineMethod("OnEventNotification", MethodAttributes.Virtual | MethodAttributes.Family, typeof(void), new Type[] { typeof(SocketMessage) });
            var ilGen = methodBuilderOnEventNotification.GetILGenerator();
            ilGen.Emit(OpCodes.Ldarg_0);
            ilGen.Emit(OpCodes.Ret);
        }

        private static void AddProperty(TypeBuilder typeBuilder, PropertyInfo property)
        {
            if (typeof(ProxyObjectBase).GetProperty(property.Name) != null)
                return;
            var fieldName = $"_{property.Name[0].ToString().ToLowerInvariant()}{property.Name.Substring(1)}";
            var fieldBuilder = typeBuilder.DefineField(fieldName, property.PropertyType, FieldAttributes.Private);
            ConstructorInfo fieldDtoMemberAttrInfo = typeof(DtoMemberAttribute).GetConstructor(new Type[] { typeof(string) });
            CustomAttributeBuilder fieldDtoMemberAttributeBuilder = new CustomAttributeBuilder(fieldDtoMemberAttrInfo, new object[] { property.Name });
            fieldBuilder.SetCustomAttribute(fieldDtoMemberAttributeBuilder);
            var propertyBuilder = typeBuilder.DefineProperty(property.Name, PropertyAttributes.HasDefault, property.PropertyType, null);
            if (property.CanRead)
            {
                var getterMethodBuilder = typeBuilder.DefineMethod(property.GetGetMethod().Name, MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Final | MethodAttributes.SpecialName);
                getterMethodBuilder.SetReturnType(property.PropertyType);
                var ilGen = getterMethodBuilder.GetILGenerator();
                ilGen.Emit(OpCodes.Ldarg_0);
                ilGen.Emit(OpCodes.Ldfld, fieldBuilder);
                ilGen.Emit(OpCodes.Ret);
                typeBuilder.DefineMethodOverride(getterMethodBuilder, property.GetGetMethod());
                propertyBuilder.SetGetMethod(getterMethodBuilder);
            }
            if (property.CanWrite)
            {
                var setterMethodBuilder = typeBuilder.DefineMethod(property.GetSetMethod().Name, MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Final | MethodAttributes.SpecialName);
                setterMethodBuilder.SetParameters(property.PropertyType);
                var setterMethod = typeBuilder.BaseType.GetMethod("Set", BindingFlags.Instance | BindingFlags.NonPublic);
                var ilGen = setterMethodBuilder.GetILGenerator();
                ilGen.Emit(OpCodes.Ldarg_0);
                ilGen.Emit(OpCodes.Ldstr, property.Name);
                ilGen.Emit(OpCodes.Call, setterMethod);
                ilGen.Emit(OpCodes.Ret);
                typeBuilder.DefineMethodOverride(setterMethodBuilder, property.GetSetMethod());
                propertyBuilder.SetSetMethod(setterMethodBuilder);
            }
            
        }

        private static void AddMethod(TypeBuilder typeBuilder, MethodInfo method)
        {
            if (typeof(ProxyObjectBase).GetMethod(method.Name) != null)
                return;
            var parameters = method.GetParameters();
            var methodBuilder = typeBuilder.DefineMethod(method.Name, MethodAttributes.Public | MethodAttributes.Virtual, method.ReturnType, parameters.Select(p => p.ParameterType).ToArray());
            int parameterPosition = 0;
            foreach (var parameter in parameters)
                methodBuilder.DefineParameter(parameterPosition++, parameter.Attributes, parameter.Name);
            var ilGen = methodBuilder.GetILGenerator();
            var baseMethodToInvoke = typeBuilder.BaseType.GetMethod(method.ReturnType == typeof(void) ? "Invoke" : "Query", BindingFlags.Instance | BindingFlags.NonPublic);
            ilGen.Emit(OpCodes.Ldarg_0);
            ilGen.Emit(OpCodes.Starg, method.Name);
            parameterPosition = 0;
            foreach (var parameter in parameters)
                ilGen.Emit(OpCodes.Starg_S, parameterPosition++);
            ilGen.Emit(OpCodes.Call, baseMethodToInvoke);
            ilGen.Emit(OpCodes.Ret);
        }

        private static void AddEvent(TypeBuilder typeBuilder, EventInfo @event)
        {
            if (typeof(ProxyObjectBase).GetEvent(@event.Name) != null)
                return;
            throw new NotImplementedException();
        }

    }
}
