using System.Reflection;
using HarmonyLib;
using ProjectVigilance.Core;
using ProjectVigilance.Core.Modules;
using ProjectVigilance.Geoscape.Settings;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Orbital;

/// <summary>
/// RimWorld Mod entry point for Project Vigilance - Orbital Logistics &amp; Satellites.
/// </summary>
public class OrbitalMod : Mod
{
    public const string PackageId = "ProjectVigilance.Orbital";

    public OrbitalMod(ModContentPack content) : base(content)
    {
        RegisterModuleHooks();
        try
        {
            var harmony = new Harmony(PackageId);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Log.Message("[Project Vigilance] Initialized Module: Orbital Logistics & Satellites.");
        }
        catch (System.Exception ex)
        {
            Log.Error($"[Project Vigilance] Failed to apply Orbital Harmony patches: {ex}");
        }
    }

    public static void RegisterModuleHooks()
    {
        VigilanceModuleManager.RegisterActiveModule(VigilanceModuleId.Orbital);
        VigilanceModuleManager.IsOrbitalEntityDetectedHook = OrbitalVisibilityUtility.IsOrbitalEntityDetected;
        VigilanceModuleManager.HasNavigationCoverageHook = tile => OrbitalManager.Instance?.HasNavigationCoverage(tile) == true;
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        VigilanceCoreMod.DrawUnifiedOptionsWindowForModule(PackageId, "Diagnostics", inRect);
    }

    public override void WriteSettings()
    {
        base.WriteSettings();
        VigilanceSettingsRegistry.NotifyWriteAllSettings();
    }

    public override string SettingsCategory() => "Project Vigilance — Orbital";
}

[StaticConstructorOnStartup]
internal static class OrbitalStartup
{
    static OrbitalStartup()
    {
        OrbitalMod.RegisterModuleHooks();
    }
}
