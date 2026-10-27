using HarmonyLib;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape;

public class GeoscapeMod : Mod
{
    public const string PackageId = "ProjectVigilance.Geoscape";
    private static GeoscapeSettings? settings;
    public static GeoscapeSettings Settings
    {
        get => settings ??= new GeoscapeSettings();
        set => settings = value;
    }

    public GeoscapeMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<GeoscapeSettings>();
        var harmony = new Harmony(PackageId);
        harmony.PatchAll();

        Log.Message("[Project Vigilance] Initialized Module 1: Geoscape & Radar Grid (1.7 Waylith / .NET 10). Harmony patches applied.");
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Settings.DoWindowContents(inRect);
    }

    public override string SettingsCategory() => "Project Vigilance: Geoscape";
}

