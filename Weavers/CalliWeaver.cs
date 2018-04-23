using Fody;
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
    public class CalliWeaver : BaseModuleWeaver
    {
        public override void Execute()
        {
            Dictionary<string, MethodDefinition> generatedList = new Dictionary<string, MethodDefinition>();
            HashSet<string> modifiedDelegateTypes = new HashSet<string>();
            List<Tuple<TypeDefinition, TypeDefinition>> removedTypes =
                new List<Tuple<TypeDefinition, TypeDefinition>>();
            HashSet<FieldDefinition> removedFields = new HashSet<FieldDefinition>();
            int instructionReplaced = 0;

            //Import all required types

            var objectType = ModuleDefinition.ImportReference(typeof(object));
            var intPtrType = ModuleDefinition.ImportReference(typeof(IntPtr));

            var ptrIntPtr = ModuleDefinition.ImportReference(intPtrType.MakePointerType());
            var pinnedPtrIntPtr = ModuleDefinition.ImportReference(new PinnedType(ptrIntPtr));

            //Wrapper class

            var newType = new TypeDefinition("LightDx", "FodyGenerated",
                TypeAttributes.Abstract | TypeAttributes.Sealed,
                objectType);

            //Find all types

            Dictionary<string, TypeDefinition> allTypeNames = new Dictionary<string, TypeDefinition>();
            foreach (var t in ModuleDefinition.GetAllTypes())
            {
                allTypeNames.Add(t.FullName, t);
            }
            
            //Generate new methods for each candidate fields

            foreach (var f in GetAllFields())
            {
                if (!allTypeNames.TryGetValue(f.FieldType.FullName, out var delegateType))
                {
                    //Type not in this module, not what we're looking for
                    continue;
                }

                //Confirm it's in top-level class
                if (f.DeclaringType.DeclaringType != null)
                {
                    throw new Exception();
                }

                var delegateInvoke = delegateType.Methods.Where(m => m.Name == "Invoke").Single();
                var retType = delegateInvoke.ReturnType;

                //Make a new method

                var methodName = f.DeclaringType.Name + "_" + f.Name;
                var method = new MethodDefinition(methodName,
                    MethodAttributes.Public | MethodAttributes.Static,
                    retType);

                foreach (var p in delegateInvoke.Parameters)
                {
                    method.Parameters.Add(new ParameterDefinition(p.Name, p.Attributes, p.ParameterType));
                }

                //Generate the body

                var id = GetFunctionIdInVTab(f);

                AOTCalliGenerator.Generate(ModuleDefinition, delegateInvoke, method, id, pinnedPtrIntPtr, ptrIntPtr);

                newType.Methods.Add(method);

                //Make some records to be used later

                generatedList.Add(methodName, method);
                modifiedDelegateTypes.Add(delegateType.FullName);

                removedFields.Add(f);
                removedTypes.Add(new Tuple<TypeDefinition, TypeDefinition>(delegateType.DeclaringType, delegateType));
                removedTypes.Add(new Tuple<TypeDefinition, TypeDefinition>(null, f.DeclaringType));
            }

            ModuleDefinition.Types.Add(newType);

            //Look through all instructions and replace the call from delegate to generated method

            Stack<MethodReference> methodStack = new Stack<MethodReference>();
            Dictionary<Instruction, Instruction> jumpMapping = new Dictionary<Instruction, Instruction>();
            foreach (var modify in ModuleDefinition.GetAllTypes().SelectMany(t => t.Methods))
            {
                jumpMapping.Clear();
                if (!modify.HasBody) continue;
                var instList = modify.Body.Instructions;

                //Change ldsfld + callvirt to nop + call
                for (int i = 0; i < instList.Count; ++i)
                {
                    if (instList[i].OpCode == OpCodes.Ldsfld)
                    {
                        var field = (FieldReference)instList[i].Operand;
                        //Save information to the stack
                        if (generatedList.TryGetValue(field.DeclaringType.Name + "_" + field.Name, out var generated))
                        {
                            var newInst = Instruction.Create(OpCodes.Nop);
                            jumpMapping.Add(instList[i], newInst);

                            instList.RemoveAt(i);
                            instList.Insert(i, newInst);

                            methodStack.Push(generated);
                        }
                    }
                    else if (instList[i].OpCode == OpCodes.Callvirt)
                    {
                        var method = (MethodReference)instList[i].Operand;
                        var type = method.DeclaringType;
                        if (modifiedDelegateTypes.Contains(type.FullName))
                        {
                            //TODO maybe we should confirm the signature is the same
                            var generated = methodStack.Pop();
                            var newInst = Instruction.Create(OpCodes.Call, generated);
                            jumpMapping.Add(instList[i], newInst);

                            instList.RemoveAt(i);
                            instList.Insert(i, newInst);

                            instructionReplaced += 1;
                        }
                    }
                }

                //Update jump dest
                for (int i = 0; i < instList.Count; ++i)
                {
                    if (instList[i].Operand is Instruction inst && jumpMapping.TryGetValue(inst, out var newInst))
                    {
                        instList[i].Operand = newInst;
                    }
                }

                //Confirm we are changing pairs
                if (methodStack.Count != 0)
                {
                    throw new Exception("Error in processing method " + modify.FullName);
                }
            }

            //Remove fields and types

            foreach (var removed in removedFields)
            {
                removed.DeclaringType.Fields.Remove(removed);
            }

            foreach (var removed in removedTypes.Distinct())
            {
                if (removed.Item1 != null)
                {
                    removed.Item1.NestedTypes.Remove(removed.Item2);
                }
                else
                {
                    ModuleDefinition.Types.Remove(removed.Item2);
                }
            }

            ModuleDefinition.Types.Remove(allTypeNames["LightDx.CalliGenerator"]);

            LogMessage($"LightDx calli weaver finished, replacing {generatedList.Count} methods " +
                $"and {instructionReplaced} instructions.", MessageImportance.High);
        }

        private int GetFunctionIdInVTab(FieldDefinition field)
        {
            var t = field.DeclaringType.Resolve();
            var m = t.Methods.Where(mm => mm.Name == ".cctor").Single();
            var instList = m.Body.Instructions;
            int lastIndex = 0;
            int lastInst = 0;
            foreach (var i in instList)
            {
                if (GetLdcI4(i, out var index))
                {
                    if (lastInst != 0) throw new Exception("Invalid instruction for " + field.FullName);
                    lastInst = 1;
                    lastIndex = index;
                }
                else if (i.OpCode == OpCodes.Call)
                {
                    if (lastInst != 1) throw new Exception("Invalid instruction for " + field.FullName);
                    lastInst = 2;
                }
                else if (i.OpCode == OpCodes.Stsfld)
                {
                    if (lastInst != 2) throw new Exception("Invalid instruction for " + field.FullName);
                    lastInst = 0;
                    var stfield = (FieldReference)i.Operand;
                    if (stfield.Resolve().FullName == field.FullName)
                    {
                        return lastIndex;
                    }
                }
                else if (i.OpCode == OpCodes.Ret)
                {
                    if (lastInst != 0) throw new Exception("Invalid instruction for " + field.FullName);
                    lastInst = -1;
                    break;
                }
            }
            throw new Exception("Index not found");
        }

        private static bool GetLdcI4(Instruction i, out int val)
        {
            if (i.OpCode == OpCodes.Ldc_I4_S)
            {
                val = (sbyte)i.Operand;
                return true;
            }
            else if (i.OpCode == OpCodes.Ldc_I4_0)
            {
                val = 0;
                return true;
            }
            else if (i.OpCode == OpCodes.Ldc_I4_1)
            {
                val = 1;
                return true;
            }
            else if (i.OpCode == OpCodes.Ldc_I4_2)
            {
                val = 2;
                return true;
            }
            else if (i.OpCode == OpCodes.Ldc_I4_3)
            {
                val = 3;
                return true;
            }
            else if (i.OpCode == OpCodes.Ldc_I4_4)
            {
                val = 4;
                return true;
            }
            else if (i.OpCode == OpCodes.Ldc_I4_5)
            {
                val = 5;
                return true;
            }
            else if (i.OpCode == OpCodes.Ldc_I4_6)
            {
                val = 6;
                return true;
            }
            else if (i.OpCode == OpCodes.Ldc_I4_7)
            {
                val = 7;
                return true;
            }
            else if (i.OpCode == OpCodes.Ldc_I4_8)
            {
                val = 8;
                return true;
            }
            val = 0;
            return false;
        }

        private IEnumerable<FieldDefinition> GetAllFields()
        {
            foreach (var type in ModuleDefinition.Types) //Using Types because all such types are top-level
            {
                foreach (var field in type.Fields.ToArray())
                {
                    var fieldType = field.FieldType.Resolve();
                    //Type and field must be declared in the same type
                    if (fieldType?.DeclaringType?.FullName != type.FullName)
                    {
                        continue;
                    }
                    //Must be a delegate
                    if (fieldType.BaseType.FullName == "System.MulticastDelegate")
                    {
                        yield return field;
                    }
                }
            }
        }

        public override IEnumerable<string> GetAssembliesForScanning()
        {
            yield return "mscorlib";
            yield return "System";
        }
    }
}
