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
            var typeName = $"{GeneratedAssemblyName}.{interfaceType.FullName}";
            TypeBuilder typeBuilder = _moduleBuilder.DefineType(typeName, TypeAttributes.Class | TypeAttributes.Public, _proxyBaseType);
            typeBuilder.AddInterfaceImplementation(interfaceType);
            var implementedInterfaces = (new Type[] { interfaceType }).Concat(interfaceType.GetInterfaces()).ToArray();
            var interfaceProperties = implementedInterfaces.SelectMany(i => i.GetProperties()).Distinct().ToArray();
            foreach (var property in interfaceProperties)
                AddProperty(typeBuilder, property);
            foreach (var method in implementedInterfaces.SelectMany(i => i.GetMethods().Where(m => !m.IsSpecialName)).Distinct())
                AddMethod(typeBuilder, method);
            var events = implementedInterfaces.SelectMany(i => i.GetEvents()).Distinct()
                .Where(i => _proxyBaseType.GetEvent(i.Name) == null) // only add events that are not already implemented in base class
                .ToArray();
            var fields = new FieldBuilder[events.Length];
            if (events.Length > 0)
            {
                for (int i = 0; i < events.Length; i++)
                    fields[i] = AddEvent(typeBuilder, events[i]);
                AddOnEventNotificationOverride(typeBuilder, events, fields);
            }
            return typeBuilder.CreateType();
        }

        private void AddOnEventNotificationOverride(TypeBuilder typeBuilder, EventInfo[] events, FieldInfo[] eventFields)
        {
            var baseMethod = _proxyBaseType.GetMethod(nameof(ProxyObjectBase.OnEventNotification), BindingFlags.Instance | BindingFlags.NonPublic);
            var onEventNotificationMethod = typeBuilder.DefineMethod(baseMethod.Name, MethodAttributes.Virtual | MethodAttributes.Family, typeof(void), new Type[] { typeof(string), typeof(EventArgs) });
            var stringEqualsMethod = typeof(string).GetMethod(nameof(string.Equals), BindingFlags.Static | BindingFlags.Public, null, new Type[] { typeof(string), typeof(string) }, null);
            var ilGen = onEventNotificationMethod.GetILGenerator();
            var caseLabels = events.Select(l => ilGen.DefineLabel()).ToArray();
            var retLabel = ilGen.DefineLabel();

            for (int i = 0; i < events.Length; i++)
            {
                ilGen.Emit(OpCodes.Ldarg_1);
                ilGen.Emit(OpCodes.Ldstr, events[i].Name);
                ilGen.Emit(OpCodes.Call, stringEqualsMethod); // compare event name with the one provided
                ilGen.Emit(OpCodes.Brtrue_S, caseLabels[i]);
            }

            ilGen.Emit(OpCodes.Br, retLabel); // jump to return if no event matches

            for (int i = 0; i < events.Length; i++)
            {
                var invokeLabel = ilGen.DefineLabel(); // define label to call when event field is not empty
                ilGen.MarkLabel(caseLabels[i]);
                ilGen.Emit(OpCodes.Ldarg_0);
                ilGen.Emit(OpCodes.Ldfld, eventFields[i]); // load the event field onto evaluation stack
                ilGen.Emit(OpCodes.Dup);
                ilGen.Emit(OpCodes.Brtrue_S, invokeLabel); // jump to execution when event field is not empty, otherwise proceed to cleanup and return
                ilGen.Emit(OpCodes.Pop); // actual cleanup
                ilGen.Emit(OpCodes.Br, retLabel);
                ilGen.MarkLabel(invokeLabel);
                ilGen.Emit(OpCodes.Ldarg_0); // load event handler sender
                ilGen.Emit(OpCodes.Ldarg_2); // load EventArgs onto stack

                var eventHandlerMethod = events[i].EventHandlerType.GetMethod(nameof(EventHandler.Invoke));
                ilGen.Emit(OpCodes.Callvirt, eventHandlerMethod); // call the event field with sender and args parameters from evaluation stack
                ilGen.Emit(OpCodes.Br, retLabel); // jump to return after handling the event
            }

            ilGen.MarkLabel(retLabel);
            ilGen.Emit(OpCodes.Ret);
            typeBuilder.DefineMethodOverride(onEventNotificationMethod, baseMethod);
        }

        private void AddProperty(TypeBuilder typeBuilder, PropertyInfo property)
        {
            if (_proxyBaseType.GetProperty(property.Name) != null)
                return;
            var fieldName = ToUnderscoreLowerCase(property.Name);
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
                var setterMethod = typeBuilder.BaseType.GetMethod(nameof(ProxyObjectBase.Set), BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(property.PropertyType);
                var ilGen = setterMethodBuilder.GetILGenerator();
                ilGen.Emit(OpCodes.Ldarg_0);
                ilGen.Emit(OpCodes.Ldarg_1);
                ilGen.Emit(OpCodes.Stfld, fieldBuilder);
                ilGen.Emit(OpCodes.Ldarg_0);
                ilGen.Emit(OpCodes.Ldarg_1);
                ilGen.Emit(OpCodes.Ldstr, property.Name);
                ilGen.Emit(OpCodes.Call, setterMethod);
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
            var methodBuilder = typeBuilder.DefineMethod(method.Name, MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Final, method.ReturnType, parameters.Select(p => p.ParameterType).ToArray());
            var ilGen = methodBuilder.GetILGenerator();
            ilGen.Emit(OpCodes.Ldarg_0);
            ilGen.Emit(OpCodes.Ldstr, method.Name);
            ilGen.Emit(OpCodes.Ldc_I4, parameters.Length);
            ilGen.Emit(OpCodes.Newarr, typeof(object));
            int parameterPosition = 0;
            foreach (var parameter in parameters)
            {
                methodBuilder.DefineParameter(parameterPosition, parameter.Attributes, parameter.Name);
                ilGen.Emit(OpCodes.Dup);
                ilGen.Emit(OpCodes.Ldc_I4, parameterPosition);
                ilGen.Emit(OpCodes.Ldarg, ++parameterPosition);
                if (parameter.ParameterType.IsValueType)
                    ilGen.Emit(OpCodes.Box, parameter.ParameterType);
                ilGen.Emit(OpCodes.Stelem_Ref);
            }
            var baseMethodToInvoke = method.ReturnType == typeof(void)
                ? _proxyBaseType.GetMethod(nameof(ProxyObjectBase.Invoke), BindingFlags.Instance | BindingFlags.NonPublic)
                : _proxyBaseType.GetMethod(nameof(ProxyObjectBase.Query), BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(method.ReturnType);

            ilGen.Emit(OpCodes.Call, baseMethodToInvoke);
            ilGen.Emit(OpCodes.Nop);
            ilGen.Emit(OpCodes.Ret);
            typeBuilder.DefineMethodOverride(methodBuilder, method);
        }

        private FieldBuilder AddEvent(TypeBuilder typeBuilder, EventInfo ev)
        {
            var eventType = ev.EventHandlerType;
            var field = typeBuilder.DefineField(ToUnderscoreLowerCase(ev.Name), eventType, FieldAttributes.Private);
            var eventInfo = typeBuilder.DefineEvent(ev.Name, EventAttributes.None, eventType);

            // adding add method
            var addMethod = typeBuilder.DefineMethod($"add_{ev.Name}",
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
                CallingConventions.Standard | CallingConventions.HasThis,
                typeof(void),
                new[] { eventType });
            var addMethodGenerator = addMethod.GetILGenerator();
            var combine = typeof(Delegate).GetMethod(nameof(Delegate.Combine), new[] { typeof(Delegate), typeof(Delegate) });
            var eventAddMethod = _proxyBaseType.GetMethod(nameof(ProxyObjectBase.EventAdd), BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(eventType);
            addMethodGenerator.Emit(OpCodes.Ldarg_0);
            addMethodGenerator.Emit(OpCodes.Ldarg_0);
            addMethodGenerator.Emit(OpCodes.Ldfld, field);
            addMethodGenerator.Emit(OpCodes.Ldstr, ev.Name);
            addMethodGenerator.Emit(OpCodes.Call, eventAddMethod);

            addMethodGenerator.Emit(OpCodes.Ldarg_0);
            addMethodGenerator.Emit(OpCodes.Ldarg_0);
            addMethodGenerator.Emit(OpCodes.Ldfld, field);
            addMethodGenerator.Emit(OpCodes.Ldarg_1);
            addMethodGenerator.Emit(OpCodes.Call, combine);
            addMethodGenerator.Emit(OpCodes.Castclass, eventType);
            addMethodGenerator.Emit(OpCodes.Stfld, field);
            addMethodGenerator.Emit(OpCodes.Ret);
            eventInfo.SetAddOnMethod(addMethod);

            var removeMethod = typeBuilder.DefineMethod($"remove_{ev.Name}",
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
                CallingConventions.Standard | CallingConventions.HasThis,
                typeof(void),
                new[] { eventType });
            var remove = typeof(Delegate).GetMethod(nameof(Delegate.Remove), new[] { typeof(Delegate), typeof(Delegate) });
            var removeMethodGenerator = removeMethod.GetILGenerator();
            removeMethodGenerator.Emit(OpCodes.Ldarg_0);
            removeMethodGenerator.Emit(OpCodes.Ldarg_0);
            removeMethodGenerator.Emit(OpCodes.Ldfld, field);
            removeMethodGenerator.Emit(OpCodes.Ldarg_1);
            removeMethodGenerator.Emit(OpCodes.Call, remove);
            removeMethodGenerator.Emit(OpCodes.Castclass, eventType);
            removeMethodGenerator.Emit(OpCodes.Stfld, field);

            var eventRemoveMethod = _proxyBaseType.GetMethod(nameof(ProxyObjectBase.EventRemove), BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(eventType);
            removeMethodGenerator.Emit(OpCodes.Ldarg_0);
            removeMethodGenerator.Emit(OpCodes.Ldarg_0);
            removeMethodGenerator.Emit(OpCodes.Ldfld, field);
            removeMethodGenerator.Emit(OpCodes.Ldstr, ev.Name);
            removeMethodGenerator.Emit(OpCodes.Call, eventRemoveMethod);

            removeMethodGenerator.Emit(OpCodes.Ret);
            eventInfo.SetRemoveOnMethod(removeMethod);
            return field;
        }

        private string ToUnderscoreLowerCase(string name)
        {
            return $"_{name.Substring(0, 1).ToLowerInvariant()}{name.Substring(1)}";
        }

    }
}
