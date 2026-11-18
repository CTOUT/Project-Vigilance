using System.Reflection;
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
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        Log.Message($"[Project Vigilance] Initialized Module 1: Geoscape & Radar Grid ({FormatBuildDetails()}). Harmony patches applied.");
    }

    public static string FormatBuildDetails()
    {
        var asm = typeof(GeoscapeMod).Assembly;
        var infoVer = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(infoVer) && infoVer.Contains('+'))
        {
            var parts = infoVer.Split('+');
            string ver = parts[0];
            string hash = parts[1];
            if (hash.Length > 7)
            {
                hash = hash.Substring(0, 7);
            }
            infoVer = $"{ver}+{hash}";
        }

        string versionStr = !string.IsNullOrEmpty(infoVer) ? $"v{infoVer}" : $"v{BuildInfo.Version}";
        return $"{versionStr} | {BuildInfo.Configuration} | Built {BuildInfo.BuildDate}";
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Settings.DoWindowContents(inRect);
    }

    public override string SettingsCategory() => "Project Vigilance";
}

