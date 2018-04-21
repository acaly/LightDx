using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LightDx
{
    //This class is modified version of http://rogue-modron.blogspot.ca/2011/11/invoking-native.html
    internal class CalliGenerator
    {
        public static TDelegate GetCalliDelegate<TDelegate>(int offset)
            where TDelegate : class
        {
            var delegateType = typeof(TDelegate);
            var invokeInfo = delegateType.GetMethod("Invoke");
            
            ScanParameter(invokeInfo.GetParameters(),
                out var invokeTypes, out var calliTypes, out var lastRefIntPtr);

            var calliMethod = new DynamicMethod("CalliInvoke",
                invokeInfo.ReturnType, invokeTypes, typeof(CalliGenerator).Module, true);
            var generator = calliMethod.GetILGenerator();

            // Generate the pinned local
            generator.DeclareLocal(typeof(IntPtr*), true);

            // Pin the out IntPtr
            if (lastRefIntPtr != -1)
            {
                generator.Emit(OpCodes.Ldarg, lastRefIntPtr);
                generator.Emit(OpCodes.Stloc_0);
            }

            GeneratePushArguments(generator, calliTypes.Length, lastRefIntPtr, -1);
            GenerateGetVTable(generator, offset);
            generator.EmitCalli(OpCodes.Calli, CallingConvention.StdCall,
                invokeInfo.ReturnType, calliTypes);

            generator.Emit(OpCodes.Ret);

            return (TDelegate)(object)calliMethod.CreateDelegate(delegateType);
        }

        private static void ScanParameter(ParameterInfo[] parameters,
            out Type[] invokeTypes, out Type[] calliTypes, out int lastRefIntPtr)
        {
            invokeTypes = new Type[parameters.Length];
            calliTypes = new Type[parameters.Length];
            lastRefIntPtr = -1;

            for (int i = 0; i < parameters.Length; i++)
            {
                var type = parameters[i].ParameterType;
                invokeTypes[i] = type;
                calliTypes[i] = GetPointerTypeIfReference(type);
                if (type.IsByRef && type.GetElementType() == typeof(IntPtr))
                {
                    lastRefIntPtr = i;
                }
            }
        }

        private static Type GetPointerTypeIfReference(Type type)
        {
            if (type.IsByRef)
            {
                return type.GetElementType().MakePointerType();
            }
            return type;
        }

        private static void GeneratePushArguments(ILGenerator generator, int count, int pinnedRef, int pinnedRef2)
        {
            for (int i = 0; i < count; i++)
            {
                if (i == pinnedRef)
                {
                    generator.Emit(OpCodes.Ldloc_0);
                }
                else if (i == pinnedRef2)
                {
                    generator.Emit(OpCodes.Ldloc_1);
                }
                else if (i == 0)
                {
                    generator.Emit(OpCodes.Ldarg_0);
                }
                else if (i == 1)
                {
                    generator.Emit(OpCodes.Ldarg_1);
                }
                else if (i == 2)
                {
                    generator.Emit(OpCodes.Ldarg_2);
                }
                else if (i == 3)
                {
                    generator.Emit(OpCodes.Ldarg_3);
                }
                else
                {
                    generator.Emit(OpCodes.Ldarg, i);
                }
            }
        }

        private static void GenerateGetVTable(ILGenerator generator, int offset)
        {
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldind_I);
            generator.Emit(OpCodes.Ldc_I4, offset);
            generator.Emit(OpCodes.Conv_I);
            generator.Emit(OpCodes.Sizeof, typeof(void*));
            generator.Emit(OpCodes.Mul);
            generator.Emit(OpCodes.Add);
            generator.Emit(OpCodes.Ldind_I);
        }
    }
}
