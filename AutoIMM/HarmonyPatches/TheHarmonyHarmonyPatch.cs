using HarmonyLib;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

namespace AutoIMM.HarmonyPatches;


[HarmonyPatch]
public static class TheHarmonyHarmonyPatch
{
    [HarmonyTargetMethods]
    static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Harmony), nameof(Harmony.PatchAll), []);
        yield return AccessTools.Method(typeof(Harmony), nameof(Harmony.PatchCategory), [typeof(string)]);
        yield return AccessTools.Method(typeof(Harmony), nameof(Harmony.PatchAllUncategorized), []);
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var matcher = new CodeMatcher(instructions);
        matcher.Start().MatchStartForward(
            CodeMatch.Calls(AccessTools.Method(typeof(StackFrame), nameof(StackFrame.GetMethod)))
        );
        
        if (matcher.IsValid)
        {
            matcher.Opcode = OpCodes.Call;
            matcher.Operand = AccessTools.Method(typeof(TheHarmonyHarmonyPatch), nameof(GetNonPatchedMethodFromFrame));
        }

        return matcher.InstructionEnumeration();
    }

    public static MethodBase GetNonPatchedMethodFromFrame(StackFrame frame)
    {
        var normalResult = frame.GetMethod();
        if(normalResult?.ReflectedType is null)
        {
            var nonHarmonyResult = Harmony.GetOriginalMethodFromStackframe(frame);
            return nonHarmonyResult;
        }
        return normalResult;
    }
}
