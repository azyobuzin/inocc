using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Inocc.Core
{
    public static class InterfaceCast
    {
        private static readonly Lazy<ModuleBuilder> moduleBuilder = new Lazy<ModuleBuilder>(() =>
            AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("InoccInterfaceWrapper"), AssemblyBuilderAccess.Run)
                .DefineDynamicModule("InoccInterfaceWrapper"));

        private static readonly ConcurrentDictionary<Type, Type> wrapperClasses = new ConcurrentDictionary<Type, Type>();

        public static Tuple<T, bool> Cast<T>(object source)
        {
            var key = source.GetType();
            if (key.IsGenericType && key.GetGenericTypeDefinition() == typeof(InterfaceWrapper<>))
            {
                var valueField = key.GetField("Value", BindingFlags.Public | BindingFlags.Instance);
                Debug.Assert(valueField != null);
                source = valueField.GetValue(source);
                key = source.GetType();
            }

            var t = wrapperClasses.GetOrAdd(key, x => CreateWrapperClass(x, typeof(T)));
            return t == null
                ? Tuple.Create(default(T), false)
                : Tuple.Create((T)Activator.CreateInstance(t, source), true);
        }

        private static Type CreateWrapperClass(Type source, Type target)
        {
            if (!target.IsInterface)
                throw new ArgumentException("Not an interface.", nameof(target));

            var interfaces = GetInterfaces(target);
            var interfaceMethods = interfaces.SelectMany(x => x.GetMembers())
                .Cast<MethodInfo>() // Assert only methods
                .ToDictionary(x => x.Name);

            var packageClass = GetPackageType(source);
            if (packageClass == null)
                throw new ArgumentException("Not in a package class.", nameof(source));

            var sourcea = new[] { source };
            var methods = new List<MethodPair>(interfaceMethods.Count);
            foreach (var m in packageClass.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                MethodInfo interfaceMethod;
                if (interfaceMethods.TryGetValue(m.Name, out interfaceMethod)
                    && m.IsDefined(typeof(ExtensionAttribute))
                    && m.ReturnType == interfaceMethod.ReturnType
                    && m.GetParameters().Select(x => x.ParameterType)
                        .SequenceEqual(sourcea.Concat(interfaceMethod.GetParameters().Select(x => x.ParameterType))))
                {
                    methods.Add(new MethodPair() { InterfaceMethod = interfaceMethod, ImplMethod = m });
                }
            }

            if (methods.Count != interfaceMethods.Count) return null;

            var baseType = typeof(InterfaceWrapper<>).MakeGenericType(source);
            var typ = moduleBuilder.Value.DefineType(
                string.Concat("Inocc.Private.", source.Name, "_", target.Name),
                TypeAttributes.Class | TypeAttributes.Sealed,
                baseType,
                interfaces);

            var ctor = typ.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, sourcea);
            var ctorIl = ctor.GetILGenerator();
            ctorIl.Emit(OpCodes.Ldarg_0);
            var baseTypeCtor = baseType.GetConstructor(sourcea);
            Debug.Assert(baseTypeCtor != null);
            ctorIl.Emit(OpCodes.Call, baseTypeCtor);
            ctorIl.Emit(OpCodes.Ret);

            var valueField = baseType.GetField("Value", BindingFlags.Public | BindingFlags.Instance);
            Debug.Assert(valueField != null);

            foreach (var m in methods)
            {
                var parameters = m.InterfaceMethod.GetParameters();
                Debug.Assert(m.InterfaceMethod.DeclaringType != null);
                var builder = typ.DefineMethod(
                    string.Concat(m.InterfaceMethod.DeclaringType.Name, "_", m.InterfaceMethod.Name),
                    MethodAttributes.Private | MethodAttributes.Final | MethodAttributes.Virtual | MethodAttributes.NewSlot,
                    m.InterfaceMethod.ReturnType,
                    parameters.Select(x => x.ParameterType).ToArray());
                for (var i = 0; i < parameters.Length; i++)
                {
                    var p = parameters[i];
                    builder.DefineParameter(i + 1, p.Attributes, p.Name);
                }

                var il = builder.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, valueField);
                for (var i = 1; i <= parameters.Length; i++)
                    il.EmitLdarg(i);
                il.Emit(OpCodes.Call, m.ImplMethod);
                il.Emit(OpCodes.Ret);

                typ.DefineMethodOverride(builder, m.InterfaceMethod);
            }

            return typ.CreateType();
        }

        private static Type[] GetInterfaces(Type interfaceType)
        {
            var tmp = interfaceType.GetInterfaces();
            if (tmp.Length == 0) return new[] { interfaceType };
            var result = new Type[tmp.Length + 1];
            result[0] = interfaceType;
            Array.Copy(tmp, 0, result, 1, tmp.Length);
            return result;
        }

        private static Type GetPackageType(Type t)
        {
            while (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(GoPointer<>))
                t = t.GetGenericArguments()[0];
            return t.DeclaringType;
        }

        private struct MethodPair
        {
            public MethodInfo InterfaceMethod;
            public MethodInfo ImplMethod;
        }

        private static void EmitLdarg(this ILGenerator il, int index)
        {
            Debug.Assert(index >= 0 && index <= short.MaxValue);
            switch (index)
            {
                case 0:
                    il.Emit(OpCodes.Ldarg_0);
                    break;
                case 1:
                    il.Emit(OpCodes.Ldarg_1);
                    break;
                case 2:
                    il.Emit(OpCodes.Ldarg_2);
                    break;
                case 3:
                    il.Emit(OpCodes.Ldarg_3);
                    break;
                default:
                    if (index <= 255)
                        il.Emit(OpCodes.Ldarg_S, (byte)index);
                    else
                        il.Emit(OpCodes.Ldarg, (short)index);
                    break;
            }
        }
    }
}
