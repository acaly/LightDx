using Fody;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Weavers
{
    public class UnmanagedConstrainWeaver : BaseModuleWeaver
    {
        public override void Execute()
        {
            var valueType = ModuleDefinition.ImportReference(typeof(ValueType));

            int replacedType = 0, replacedMethod = 0;

            foreach (var t in ModuleDefinition.GetAllTypes())
            {
                if (t.HasGenericParameters && t.GenericParameters[0].HasConstraints)
                {
                    var g = t.GenericParameters[0];
                    if (g.Constraints[0].FullName.Contains("Unmanaged"))
                    {
                        g.Constraints[0] = valueType;
                        g.CustomAttributes.RemoveAt(0);
                        replacedType++;
                    }
                }
            }
            foreach (var m in ModuleDefinition.GetAllTypes().SelectMany(t => t.Methods))
            {
                var t = m;
                if (t.HasGenericParameters && t.GenericParameters[0].HasConstraints)
                {
                    var g = t.GenericParameters[0];
                    if (g.Constraints[0].FullName.Contains("Unmanaged"))
                    {
                        g.Constraints[0] = valueType;
                        g.CustomAttributes.RemoveAt(0);
                        replacedMethod++;
                    }
                }
            }

            LogMessage($"LightDx unmanaged constrain weaver finished, replacing {replacedType} types " +
                $"and {replacedMethod} methods.", MessageImportance.High);
        }

        public override IEnumerable<string> GetAssembliesForScanning()
        {
            yield break;
        }
    }
}
