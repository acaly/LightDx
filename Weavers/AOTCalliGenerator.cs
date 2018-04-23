using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Weavers
{
    class AOTCalliGenerator
    {
        public static void Generate(ModuleDefinition module, MethodReference invokeInfo, MethodDefinition gen, int offset, TypeReference pinnedPtrIntPtr, TypeReference ptrVoid)
        {
            ScanParameter(invokeInfo.Parameters.ToArray(),
                out var invokeTypes, out var calliTypes, out var lastRefIntPtr);

            // Generate the pinned local
            var local0 = new VariableDefinition(pinnedPtrIntPtr);
            gen.Body.Variables.Add(local0);

            // Pin the out IntPtr
            if (lastRefIntPtr != -1)
            {
                gen.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_S, gen.Parameters[lastRefIntPtr]));
                gen.Body.Instructions.Add(Instruction.Create(OpCodes.Stloc_0));
            }

            GeneratePushArguments(gen, calliTypes.Length, lastRefIntPtr, -1, gen.Parameters);
            GenerateGetVTable(gen, offset, ptrVoid);
            
            var callsite = new CallSite(invokeInfo.ReturnType);
            callsite.CallingConvention = MethodCallingConvention.StdCall;
            foreach (var p in calliTypes)
            {
                callsite.Parameters.Add(new ParameterDefinition(p));
            }
            
            gen.Body.Instructions.Add(Instruction.Create(OpCodes.Calli, callsite));

            gen.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        }

        private static void ScanParameter(ParameterDefinition[] parameters,
            out TypeReference[] invokeTypes, out TypeReference[] calliTypes, out int lastRefIntPtr)
        {
            invokeTypes = new TypeReference[parameters.Length];
            calliTypes = new TypeReference[parameters.Length];
            lastRefIntPtr = -1;

            for (int i = 0; i < parameters.Length; i++)
            {
                var type = parameters[i].ParameterType;
                invokeTypes[i] = type;
                calliTypes[i] = GetPointerTypeIfReference(type); //TODO import?
                if (type.IsByReference && type.GetElementType().FullName == "System.IntPtr")
                {
                    lastRefIntPtr = i;
                }
            }
        }

        private static TypeReference GetPointerTypeIfReference(TypeReference type)
        {
            if (type.IsByReference)
            {
                return type.GetElementType().MakePointerType();
            }
            return type;
        }

        private static void GeneratePushArguments(MethodDefinition gen, int count, int pinnedRef, int pinnedRef2,  IList<ParameterDefinition> parameters)
        {
            for (int i = 0; i < count; i++)
            {
                if (i == pinnedRef)
                {
                    gen.Body.Instructions.Add(Instruction.Create(OpCodes.Ldloc_0));
                }
                else if (i == pinnedRef2)
                {
                    gen.Body.Instructions.Add(Instruction.Create(OpCodes.Ldloc_1));
                }
                else if (i == 0)
                {
                    gen.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                }
                else if (i == 1)
                {
                    gen.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_1));
                }
                else if (i == 2)
                {
                    gen.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_2));
                }
                else if (i == 3)
                {
                    gen.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_3));
                }
                else
                {
                    gen.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_S, parameters[i]));
                }
            }
        }

        private static void GenerateGetVTable(MethodDefinition gen, int offset, TypeReference ptrVoid)
        {
            gen.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
            gen.Body.Instructions.Add(Instruction.Create(OpCodes.Ldind_I));
            gen.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4, offset));
            gen.Body.Instructions.Add(Instruction.Create(OpCodes.Conv_I));
            gen.Body.Instructions.Add(Instruction.Create(OpCodes.Sizeof, ptrVoid));
            gen.Body.Instructions.Add(Instruction.Create(OpCodes.Mul));
            gen.Body.Instructions.Add(Instruction.Create(OpCodes.Add));
            gen.Body.Instructions.Add(Instruction.Create(OpCodes.Ldind_I));
        }
    }
}
