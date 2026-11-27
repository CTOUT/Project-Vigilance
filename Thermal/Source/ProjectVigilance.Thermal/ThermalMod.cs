using ProjectVigilance.Core.Localization;
using ProjectVigilance.Core.Modules;
using ProjectVigilance.Core.UI;
using ProjectVigilance.Geoscape.Settings;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Thermal;

/// <summary>
/// Persistent settings for Project Vigilance - Thermal Integrity &amp; Materials.
/// </summary>
public class ThermalSettings : ModSettings
{
    public bool enableThermalIntegrityAudit = true;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref enableThermalIntegrityAudit, "enableThermalIntegrityAudit", true);
    }
}

/// <summary>
/// RimWorld Mod entry point for Project Vigilance - Thermal Integrity &amp; Materials.
/// </summary>
public class ThermalMod : Mod, ISettingsTabProvider
{
    public const string PackageId = "ProjectVigilance.Thermal";
    private static ThermalSettings? settings;

    public static ThermalSettings Settings
    {
        get => settings ??= new ThermalSettings();
        set => settings = value;
    }

    public string TabKey => "Thermal";
    public string TabLabelKey => "PV_Settings_Tab_Thermal";
    public string FallbackTabLabel => "Thermal";
    public int SortOrder => 60;

    public ThermalMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<ThermalSettings>();
        RegisterModuleHooks();
        VigilanceSettingsRegistry.RegisterTab(this);
        VigilanceSettingsRegistry.OnWriteAllSettings += WriteSettings;

        Log.Message("[Project Vigilance] Initialized Module: Thermal Integrity & Materials.");
    }

    public static void RegisterModuleHooks()
    {
        VigilanceModuleManager.RegisterActiveModule(VigilanceModuleId.Thermal);
        VigilanceModuleManager.IsThermalAuditEnabledHook = () => Settings.enableThermalIntegrityAudit;
    }

    public void DoTabContents(Listing_Standard listing, Rect inRect)
    {
        string header = VigilanceLoc.Tr("PV_Settings_Thermal_Header", "Thermal Integrity & Non-Combustible Materials");
        VigilanceUiWidgets.DrawSectionHeader(listing, header);

        string label = VigilanceLoc.Tr("PV_Settings_Thermal_Toggle", "Enable Thermal Integrity Post-Def Audit");
        string tooltip = VigilanceLoc.Tr(
            "PV_Settings_Thermal_Toggle_Desc",
            "Automatically enforces non-combustible properties on structural metals, stonework, Odyssey heavy bridges, machinery, and radar dishes.");

        listing.CheckboxLabeled(label, ref Settings.enableThermalIntegrityAudit, tooltip);
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        ProjectVigilance.Core.VigilanceCoreMod.DrawUnifiedOptionsWindowForModule(PackageId, "Thermal", inRect);
    }

    public override void WriteSettings()
    {
        base.WriteSettings();
        VigilanceSettingsRegistry.NotifyWriteAllSettings();
    }

    public override string SettingsCategory() => "Project Vigilance — Thermal";
}

[StaticConstructorOnStartup]
internal static class ThermalStartup
{
    static ThermalStartup()
    {
        ThermalMod.RegisterModuleHooks();
    }
}
