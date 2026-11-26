using ProjectVigilance.Core;
using ProjectVigilance.Core.Modules;
using ProjectVigilance.Geoscape.Settings;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// RimWorld Mod entry point for Project Vigilance - Aquatic Operations &amp; Sonar.
/// </summary>
public class AquaticsMod : Mod
{
    public const string PackageId = "ProjectVigilance.Aquatics";

    public AquaticsMod(ModContentPack content) : base(content)
    {
        RegisterModuleHooks();
        Log.Message("[Project Vigilance] Initialized Module: Aquatic Operations & Sonar.");
    }

    public static void RegisterModuleHooks()
    {
        VigilanceModuleManager.RegisterActiveModule(VigilanceModuleId.Aquatics);
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        VigilanceCoreMod.DrawUnifiedOptionsWindowForModule(PackageId, "Radar", inRect);
    }

    public override void WriteSettings()
    {
        base.WriteSettings();
        VigilanceSettingsRegistry.NotifyWriteAllSettings();
    }

    public override string SettingsCategory() => "Project Vigilance — Aquatics";
}

[StaticConstructorOnStartup]
internal static class AquaticsStartup
{
    static AquaticsStartup()
    {
        AquaticsMod.RegisterModuleHooks();
    }
}
