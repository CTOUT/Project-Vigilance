using System.Reflection;
using HarmonyLib;
using ProjectVigilance.Core.Modules;
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
        var harmony = new Harmony(PackageId);
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        Log.Message("[Project Vigilance] Initialized Module: Orbital Logistics & Satellites.");
    }

    public static void RegisterModuleHooks()
    {
        VigilanceModuleManager.RegisterActiveModule(VigilanceModuleId.Orbital);
        VigilanceModuleManager.IsOrbitalEntityDetectedHook = OrbitalVisibilityUtility.IsOrbitalEntityDetected;
        VigilanceModuleManager.HasNavigationCoverageHook = tile => OrbitalManager.Instance?.HasNavigationCoverage(tile) == true;
    }

    public override string SettingsCategory() => string.Empty;
}

[StaticConstructorOnStartup]
internal static class OrbitalStartup
{
    static OrbitalStartup()
    {
        OrbitalMod.RegisterModuleHooks();
    }
}
