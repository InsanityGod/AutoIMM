using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.Common;

namespace AutoIMM.HarmonyPatches;

public static class ConfigInterception
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase __originalMethod)
    {
        var matcher = new CodeMatcher(instructions, generator);
        var targetMethod = typeof(ICoreAPICommon).GetMethods().Single(method => method.Name == nameof(ICoreAPICommon.LoadModConfig) && method.IsGenericMethodDefinition);

        matcher.Start()
            .MatchStartForward(new CodeMatch(
                instruction => instruction.opcode == OpCodes.Callvirt && instruction.operand is MethodInfo { IsGenericMethod: true } method && method.GetGenericMethodDefinition() == targetMethod)
            );

        matcher.Repeat(match =>
        {
            match.InsertAndAdvance(
                new CodeInstruction(OpCodes.Dup),
                new CodeInstruction(OpCodes.Ldtoken, __originalMethod.DeclaringType ?? throw new InvalidOperationException($"method '{__originalMethod}' does not have a DeclaringType")),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Type), nameof(Type.GetTypeFromHandle))),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Type), nameof(Type.Assembly))),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ConfigInterception), nameof(RegisterFoundConfig)).MakeGenericMethod(((MethodInfo)match.Operand).GetGenericArguments()))
            );
            match.Advance();
        });

        return matcher.InstructionEnumeration();
    }

    public static void RegisterFoundConfig<T>(string relativePath, Assembly assembly) => AutoIMMModSystem.FoundConfigs[relativePath] = (assembly, typeof(T));
}
