using AutoIMM.HarmonyPatches;
using HarmonyLib;
using InsanityLib.Auto.Config.IMM;
using InsanityLib.Generators.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;

namespace AutoIMM;

public partial class AutoIMMModSystem : ModSystem
{
    public override double ExecuteOrder() => double.NegativeInfinity;

    partial void ManualPatches(Harmony harmony, ICoreAPI api)
    {
        PatchConfigLoadingCode.FindAndPatchMethods(harmony, Mod.Logger);
    }

    [AutoClear]
    public static Dictionary<string, (Assembly caller, Type type)> FoundConfigs { get; internal set; } = [];

    public override void StartPre(ICoreAPI api)
    {
        AutoSetup(api);
        base.StartPre(api);
    }

    public override void AssetsLoaded(ICoreAPI api)
    {
        AutoAssetsLoaded(api);
        base.AssetsLoaded(api);

        foreach((var path, var entry) in FoundConfigs)
        {
            var mod = api.ModLoader.Mods.FirstOrDefault(mod => mod.Systems.FirstOrDefault()?.GetType().Assembly == entry.caller);
            mod ??= api.ModLoader.Mods.FirstOrDefault(mod => mod.Systems.FirstOrDefault()?.GetType().Assembly == entry.type.Assembly);
            if(mod is null)
            {
                Mod.Logger.Warning("Unable to trace mod for config '{0}' of type '{1}'", path, entry.type.FullName);
                continue;
            }
            IMMConfigGenerator.GenerateAndAppend(api, mod, entry.type, path, mod.Info.Side == EnumAppSide.Client ? EnumAppSide.Client : EnumAppSide.Server);
        }
        FoundConfigs.Clear();
    }

    public override void Dispose()
    {
        AutoDispose();
        base.Dispose();
    }

}
