using HarmonyLib;
using InsanityLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;

namespace AutoIMM.HarmonyPatches;

public static class PatchConfigLoadingCode
{
    public static IEnumerable<Assembly> GetTargetAssemblies() => AppDomain.CurrentDomain.GetAssemblies()
        //Skip any dynamic assembly (cause I don't know how to read the assembly definition of these)
        .Where(assembly => !string.IsNullOrEmpty(assembly.Location))
        //Skip anything that does not reference vintage story
        .Where(assembly => Array.Exists(assembly.GetReferencedAssemblies(), assembly => assembly.Name == typeof(ModSystem).Assembly.GetName().Name))
        //Skip InsanityLib and IMM
        .Where(assembly => assembly != typeof(InsanityLibModSystem).Assembly && assembly.GetName().Name != "integratedmodmanager")
        //Skip anything that references InsanityLib (As they should have a manual implementation)
        .Where(assembly => !Array.Exists(assembly.GetReferencedAssemblies(), assembly => assembly.Name == typeof(InsanityLibModSystem).Assembly.GetName().Name));

    public static void FindAndPatchMethods(Harmony harmony, ILogger logger)
    {
        var assembliesToScan = GetTargetAssemblies()
        .Select(assembly => (assembly, AssemblyDefinition.ReadAssembly(assembly.Location)));

        foreach ((var assembly, var monoAssembly) in assembliesToScan)
        {
            try
            {
                ScanAndPatchAssembly(harmony, assembly, monoAssembly, logger);
            }
            catch (Exception ex)
            {
                logger.Error("Unexpected error occured while patching '{0}' patching, exception: {1}", assembly.FullName, ex);
            }
        }
    }

    private static void ScanAndPatchAssembly(Harmony harmony, Assembly assembly, AssemblyDefinition monoAssembly, ILogger logger)
    {
        foreach (var type in monoAssembly.Modules.SelectMany(module => module.Types))
        {
            foreach (var method in type.Methods)
            {
                if (!method.HasBody) continue;

                if (method.HasGenericParameters)
                {
                    //TODO detect wrapper method
                    continue;
                }

                // Access and print the method body
                for (int i = 0; i < method.Body.Instructions.Count; i++)
                {
                    Instruction? instruction = method.Body.Instructions[i];
                    if (instruction.OpCode != OpCodes.Callvirt || instruction.Operand is not GenericInstanceMethod methodRef || !methodRef.IsGenericInstance || methodRef.Name != nameof(ICoreAPICommon.LoadModConfig)) continue;


                    if(i > 0 && method.Body.Instructions[i - 1] is { } previousInstruction && previousInstruction.OpCode == OpCodes.Ldstr && previousInstruction.Operand is string relativePath)
                    {
                        var configType = methodRef.GenericArguments[0];
                        var realType = assembly.GetType(configType.FullName);
                        if(realType is not null)
                        {
                            if(!AutoIMMModSystem.FoundConfigs.ContainsKey(relativePath))
                            {
                                AutoIMMModSystem.FoundConfigs[relativePath] = (assembly, realType);
                            }
                            continue;
                        }
                    }

                    var realMethods = assembly.GetTypes()
                        .First(realType => realType.Name == type.Name)
                        .GetMethods(AccessTools.allDeclared)
                        .Where(realMethod => realMethod.Name == method.Name)
                        .ToList();
                    var realMethod = realMethods.FirstOrDefault();
                    if (realMethods.Count != 1)
                    {
                        logger.Warning("Failed to find correct method for '{0}', '{1}'", assembly.FullName, method.FullName);
                        break;
                    }

                    try
                    {
                        harmony.Patch(realMethod, transpiler: new HarmonyMethod(AccessTools.Method(typeof(ConfigInterception), nameof(ConfigInterception.Transpiler))));
                    }
                    catch
                    {
                        logger.Warning("Failed to inject loading for '{0}', '{1}'", assembly.FullName, method.FullName);
                    }

                    break;
                }
            }
        }
    }
}