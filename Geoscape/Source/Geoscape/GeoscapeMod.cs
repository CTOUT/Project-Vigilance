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
        VigilanceModuleManager.GetSensorySweepRevisionHook = static () => SensoryManager.Instance?.SweepRevision ?? 0;
        VigilanceModuleManager.GetMaxGroundRadarRangeHook = GetMaxGroundRadarRange;
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

    private static float GetMaxGroundRadarRange(Map map)
    {
        if (map == null || !map.Tile.Valid)
        {
            return 0f;
        }

        SensoryManager? sensory = SensoryManager.Instance;
        if (sensory != null)
        {
            return sensory.GetMaxGroundRadarRangeForMap(map);
        }

        if (map.listerBuildings?.allBuildingsColonist == null)
        {
            return 0f;
        }

        float maxRange = 0f;
        var buildings = map.listerBuildings.allBuildingsColonist;
        for (int i = 0; i < buildings.Count; i++)
        {
            Building b = buildings[i];
            if (b == null || b.Destroyed)
            {
                continue;
            }

            CompRadar? radar = b.GetComp<CompRadar>();
            if (radar is { IsSensorActive: true, IsTechnological: true, IsOceanicSonar: false })
            {
                float range = radar.DetectionRadiusInTiles;
                if (range > maxRange)
                {
                    maxRange = range;
                }
            }
        }

        return maxRange;
    }

    private static bool HasGroundRadarCoveringTile(Map map, PlanetTile targetTile)
    {
        if (map == null || !map.Tile.Valid || !targetTile.Valid)
        {
            return false;
        }

        if (Find.World?.grid != null && map.Tile.Layer != targetTile.Layer)
        {
            return false;
        }

        float maxRange = GetMaxGroundRadarRange(map);
        if (maxRange <= 0f)
        {
            return false;
        }

        int maxSteps = Mathf.Max(1, Mathf.RoundToInt(maxRange));
        if (VigilanceModuleManager.DistanceCalculationOverrideHook != null)
        {
            return VigilanceModuleManager.DistanceCalculationOverrideHook(map.Tile, targetTile, maxSteps) <= maxSteps;
        }

        if (Find.World?.grid != null)
        {
            return Find.World.grid.ApproxDistanceInTiles(map.Tile, targetTile) <= maxRange;
        }

        return Math.Abs(map.Tile.tileId - targetTile.tileId) <= maxSteps;
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
        ProjectVigilance.Core.VigilanceCoreMod.DrawUnifiedOptionsWindowForModule(PackageId, "Sensory", inRect);
    }

    public override void WriteSettings()
    {
        base.WriteSettings();
        VigilanceSettingsRegistry.NotifyWriteAllSettings();
    }

    public override string SettingsCategory() => "Project Vigilance — Geoscape";
}

[StaticConstructorOnStartup]
internal static class GeoscapeStartup
{
    static GeoscapeStartup()
    {
        GeoscapeMod.RegisterModuleHooks();
    }
}

