using AutoIMM.HarmonyPatches;
using HarmonyLib;
using InsanityLib.Auto.Config.IMM;
using InsanityLib.Generators.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

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

        var singlePlayer = api.Side == EnumAppSide.Client || (api is ICoreServerAPI sapi && !sapi.Server.IsDedicated);
        var handledConfigs = new List<string>();
        
        foreach((var path, var entry) in FoundConfigs)
        {
            if(!Path.Exists(Path.Combine(GamePaths.ModConfig, path))) continue; //Ignore files that don't exist yet at this point

            var mod = api.ModLoader.Mods.FirstOrDefault(mod => mod.Systems.FirstOrDefault()?.GetType().Assembly == entry.caller);
            mod ??= api.ModLoader.Mods.FirstOrDefault(mod => mod.Systems.FirstOrDefault()?.GetType().Assembly == entry.type.Assembly);
            if(mod is null)
            {
                //In singleplayer client side configs can already be found at this point (because the game loads the .dll file) but the mod itself might not be accessible yet.
                if(api.Side == EnumAppSide.Client || !singlePlayer) Mod.Logger.Warning("Unable to trace mod for config '{0}' of type '{1}'", path, entry.type.FullName);
                continue;
            }
            IMMConfigGenerator.GenerateAndAppend(api, mod, entry.type, path, mod.Info.Side == EnumAppSide.Client ? EnumAppSide.Client : EnumAppSide.Server);
            handledConfigs.Add(path);
        }

        foreach(var config in handledConfigs) FoundConfigs.Remove(config);
    }

    public override void Dispose()
    {
        AutoDispose();
        base.Dispose();
    }

}
