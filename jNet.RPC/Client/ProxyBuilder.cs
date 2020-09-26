using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

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
            foreach (var property in interfaceType.GetProperties())
                AddProperty(typeBuilder, property);
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
            var fieldName = $"_{property.Name[0].ToString().ToLowerInvariant()}{property.Name.Substring(1)}";
            var fieldBuilder = typeBuilder.DefineField(fieldName, property.PropertyType, FieldAttributes.Private);
            ConstructorInfo fieldDtoMemberAttrInfo = typeof(DtoMemberAttribute).GetConstructor(new Type[] { typeof(string) });
            CustomAttributeBuilder fieldDtoMemberAttributeBuilder = new CustomAttributeBuilder(fieldDtoMemberAttrInfo, new object[] { property.Name });
            fieldBuilder.SetCustomAttribute(fieldDtoMemberAttributeBuilder);
            var propertyBuilder = typeBuilder.DefineProperty(property.Name, PropertyAttributes.HasDefault, property.PropertyType, null);
            if (property.CanRead)
            {
                var getterMethodBuilder = typeBuilder.DefineMethod(property.GetGetMethod().Name, MethodAttributes.Virtual | MethodAttributes.Private);
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
                var setterMethodBuilder = typeBuilder.DefineMethod(property.GetSetMethod().Name, MethodAttributes.Virtual | MethodAttributes.Private);
                setterMethodBuilder.SetParameters(property.PropertyType);
                var ilGen = setterMethodBuilder.GetILGenerator();
                ilGen.Emit(OpCodes.Ldarg_0);
                ilGen.Emit(OpCodes.Ldarg_1);
                ilGen.Emit(OpCodes.Stfld, fieldBuilder);
                ilGen.Emit(OpCodes.Ret);
                typeBuilder.DefineMethodOverride(setterMethodBuilder, property.GetSetMethod());
                propertyBuilder.SetSetMethod(setterMethodBuilder);
            }
            
        }
    }
}
