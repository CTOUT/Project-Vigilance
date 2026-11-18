using ProjectVigilance.Core.Modules;
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

    public override string SettingsCategory() => string.Empty;
}

[StaticConstructorOnStartup]
internal static class AquaticsStartup
{
    static AquaticsStartup()
    {
        AquaticsMod.RegisterModuleHooks();
    }
}
