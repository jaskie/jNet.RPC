using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace jNet.RPC.Client
{
    internal class ProxyBuilder
    {
        const string GeneratedAssemblyName = "jNet.RPC.Client.GeneratedProxies";

        private readonly ModuleBuilder _moduleBuilder;
        private readonly AssemblyBuilder _asmBuilder;
        private readonly Type _proxyBaseType;


        public ProxyBuilder(Type proxyBaseType)
        {
            AssemblyName asmName = new AssemblyName(GeneratedAssemblyName);
            _asmBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.RunAndSave);
            _moduleBuilder = _asmBuilder.DefineDynamicModule(GeneratedAssemblyName, $"{GeneratedAssemblyName}.dll");
            _proxyBaseType = proxyBaseType;
        }

        public Type GetProxyTypeFor(Type interfaceType)
        {
            var typeName = $"{GeneratedAssemblyName}.{(interfaceType.Name.StartsWith("I") ? interfaceType.Name.Substring(1) : interfaceType.Name)}";
            TypeBuilder typeBuilder = _moduleBuilder.DefineType(typeName, TypeAttributes.Class | TypeAttributes.Public, _proxyBaseType);
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

        private void AddMethodOnEventNotification(TypeBuilder typeBuilder)
        {
            var methodBuilderOnEventNotification = typeBuilder.DefineMethod("OnEventNotification", MethodAttributes.Virtual | MethodAttributes.Family, typeof(void), new Type[] { typeof(SocketMessage) });
            var ilGen = methodBuilderOnEventNotification.GetILGenerator();
            ilGen.Emit(OpCodes.Ldarg_0);
            ilGen.Emit(OpCodes.Ret);
        }

        private void AddProperty(TypeBuilder typeBuilder, PropertyInfo property)
        {
            if (_proxyBaseType.GetProperty(property.Name) != null)
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
                var setterMethod = typeBuilder.BaseType.GetMethod("Set", BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(property.PropertyType);
                var ilGen = setterMethodBuilder.GetILGenerator();
                ilGen.Emit(OpCodes.Ldarg_0);
                ilGen.Emit(OpCodes.Ldarg_1); 
                //ilGen.Emit(OpCodes.Dup);
                ilGen.Emit(OpCodes.Stfld, fieldBuilder);
                //ilGen.Emit(OpCodes.Ldstr, property.Name);
                //ilGen.Emit(OpCodes.Call, setterMethod);
                ilGen.Emit(OpCodes.Ret);
                typeBuilder.DefineMethodOverride(setterMethodBuilder, property.GetSetMethod());
                propertyBuilder.SetSetMethod(setterMethodBuilder);
            }
            
        }

        private void AddMethod(TypeBuilder typeBuilder, MethodInfo method)
        {
            if (_proxyBaseType.GetMethod(method.Name) != null)
                return;
            var parameters = method.GetParameters();
            var methodBuilder = typeBuilder.DefineMethod(method.Name, MethodAttributes.Public| MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Final, method.ReturnType, parameters.Select(p => p.ParameterType).ToArray());
            var ilGen = methodBuilder.GetILGenerator();
            ilGen.Emit(OpCodes.Ldarg_0);
            ilGen.Emit(OpCodes.Ldstr, method.Name);
            ilGen.Emit(OpCodes.Ldc_I4_0);
            ilGen.Emit(OpCodes.Newarr, typeof(object));
//            ilGen.Emit(OpCodes.Dup);
//            ilGen.Emit(OpCodes.Ldc_I4_0);
//            ilGen.Emit(OpCodes.Ldarg_1);
//            ilGen.Emit(OpCodes.Stelem_Ref);
            //int parameterPosition = 0;
            //foreach (var parameter in parameters)
            //{
            //    methodBuilder.DefineParameter(parameterPosition, parameter.Attributes, parameter.Name);
            //    ilGen.Emit(OpCodes.Ldarg, parameterPosition++);
            //}
            var baseMethodToInvoke = typeBuilder.BaseType.GetMethod(method.ReturnType == typeof(void) ? "Invoke" : "Query", BindingFlags.Instance | BindingFlags.NonPublic);
            //parameterPosition = 0;
            //foreach (var parameter in parameters)
            //    ilGen.Emit(OpCodes.Starg_S, parameterPosition++);
            ilGen.Emit(OpCodes.Call, baseMethodToInvoke);
            ilGen.Emit(OpCodes.Ret);
            typeBuilder.DefineMethodOverride(methodBuilder, method);
        }

        private void AddEvent(TypeBuilder typeBuilder, EventInfo @event)
        {
            if (_proxyBaseType.GetEvent(@event.Name) != null)
                return;
            throw new NotImplementedException();
        }

    }
}
