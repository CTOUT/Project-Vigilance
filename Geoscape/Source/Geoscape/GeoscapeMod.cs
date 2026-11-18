using System;
using System.Reflection;
using HarmonyLib;
using ProjectVigilance.Core.Modules;
using ProjectVigilance.Geoscape.Rules;
using ProjectVigilance.Geoscape.Sensory;
using ProjectVigilance.Geoscape.Settings;
using ProjectVigilance.Geoscape.World;
using RimWorld;
using RimWorld.Planet;
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
        Settings.EnsureBuiltinTabsRegistered();
        VigilanceSettingsRegistry.OnWriteAllSettings += WriteSettings;
        RegisterModuleHooks();

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

    public static void RegisterModuleHooks()
    {
        VigilanceModuleManager.RegisterActiveModule(VigilanceModuleId.Geoscape);
        VigilanceModuleManager.DoUnifiedSettingsWindowContentsHook = static rect => Settings.DoWindowContents(rect);
        VigilanceModuleManager.GetVisibilityModeHook = static () => GeoscapeRules.VisibilityMode;
        VigilanceModuleManager.HasGroundRadarCoveringTileHook = HasGroundRadarCoveringTile;
        VigilanceModuleManager.RegisterDynamicSensorHook = SensoryManager.RegisterDynamicSensor;
        VigilanceModuleManager.DeregisterDynamicSensorHook = SensoryManager.UnregisterDynamicSensor;
        VigilanceModuleManager.PerformSensorySweepHook = static () => SensoryManager.Instance?.PerformSensorySweep();
        VigilanceModuleManager.SetRadarLayerDirtyHook = static () => WorldDrawLayer_RadarCoverage.Instance?.SetDirty();
        VigilanceModuleManager.DeploySonarOutpostHook = DeploySonarOutpost;
        VigilanceModuleManager.HasPriorGeoscapeSaveDataHook = static () =>
            (SensoryManager.Instance?.LoadedFromSave == true) ||
            (ContactRegistry.Instance?.LoadedFromSave == true) ||
            (HazeReconnaissanceManager.Instance?.LoadedFromSave == true);
        VigilanceModuleManager.RecordModuleMigrationHook = static note =>
            GeoscapeCampaignRules.Active?.Record(RuleChangeKind.ModuleMigration, note: note);
    }

    private static bool HasGroundRadarCoveringTile(Map map, PlanetTile targetTile)
    {
        if (map?.listerBuildings?.allBuildingsColonist == null || !map.Tile.Valid || !targetTile.Valid)
        {
            return false;
        }

        if (Find.World?.grid != null && map.Tile.Layer != targetTile.Layer)
        {
            return false;
        }

        var buildings = map.listerBuildings.allBuildingsColonist;
        for (int i = 0; i < buildings.Count; i++)
        {
            Building b = buildings[i];
            if (b == null || b.Destroyed)
            {
                continue;
            }

            CompRadar? radar = b.GetComp<CompRadar>();
            if (radar is not { IsSensorActive: true, IsTechnological: true, IsOceanicSonar: false })
            {
                continue;
            }

            int maxSteps = Mathf.Max(1, Mathf.RoundToInt(radar.DetectionRadiusInTiles));
            int dist = VigilanceModuleManager.DistanceCalculationOverrideHook != null
                ? VigilanceModuleManager.DistanceCalculationOverrideHook(map.Tile, targetTile, maxSteps)
                : (Find.World?.grid != null
                    ? Find.World.grid.TraversalDistanceBetween(map.Tile, targetTile, passImpassable: true, maxDist: maxSteps)
                    : Math.Abs(map.Tile.tileId - targetTile.tileId));

            if (dist <= maxSteps)
            {
                return true;
            }
        }

        return false;
    }

    private static void DeploySonarOutpost(PlanetTile targetTile)
    {
        var def = DefDatabase<WorldObjectDef>.GetNamedSilentFail("Vigilance_ListeningPost_Sonar")
            ?? DefDatabase<WorldObjectDef>.GetNamed("Vigilance_ListeningPost_Automated");

        var post = (WorldObject_ListeningPost)WorldObjectMaker.MakeWorldObject(def);
        post.Tile = targetTile;
        post.SetFaction(Faction.OfPlayer);
        post.Kind = ListeningPostKind.AutomatedSonar;
        Find.WorldObjects.Add(post);
        SensoryManager.Instance?.PerformSensorySweep();
        WorldDrawLayer_RadarCoverage.Instance?.SetDirty();

        Messages.Message($"Deployed operational Sonar Station at tile {targetTile}.", MessageTypeDefOf.PositiveEvent, false);
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Settings.DoWindowContents(inRect);
    }

    public override string SettingsCategory() => string.Empty;
}

[StaticConstructorOnStartup]
internal static class GeoscapeStartup
{
    static GeoscapeStartup()
    {
        GeoscapeMod.RegisterModuleHooks();
    }
}

