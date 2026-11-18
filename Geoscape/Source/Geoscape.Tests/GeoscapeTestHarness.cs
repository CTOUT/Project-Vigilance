using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using ProjectVigilance.Core;
using ProjectVigilance.Core.Migration;
using ProjectVigilance.Core.Modules;
using ProjectVigilance.Geoscape;
using ProjectVigilance.Geoscape.Orbital;
using ProjectVigilance.Geoscape.Patches;
using ProjectVigilance.Geoscape.Rules;
using ProjectVigilance.Geoscape.Sensory;
using ProjectVigilance.Geoscape.Thermal;
using ProjectVigilance.Geoscape.World;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Geoscape.Tests;

public sealed class MockSensorProvider : ISensorProvider
{
    public PlanetTile SensorTile { get; set; } = PlanetTile.Invalid;
    public float DetectionRadiusInTiles { get; set; } = 24f;
    public int IdentificationRadiusInTiles { get; set; } = 10;
    public IntelligenceTier MaxIntelligenceTier { get; set; } = IntelligenceTier.Classified;
    public bool IsSensorActive { get; set; } = true;
    public bool IsTechnological { get; set; } = true;
    public bool IsStationary { get; set; } = true;
    public bool PenetratesTerrainMasking { get; set; } = false;
    public int SigintPenetrationRadiusInTiles { get; set; } = 0;
    public bool IsOceanicSonar { get; set; } = false;
    public string SensorLabel { get; set; } = "Early-Warning Radar";
    public WorldObject? Source { get; set; } = null;
    public Func<IDetectableTarget, (bool detected, int distance)>? DetectionDistanceOverride { get; set; }

    public bool TryGetDetectionDistance(IDetectableTarget target, out int distance)
    {
        if (DetectionDistanceOverride != null)
        {
            var result = DetectionDistanceOverride(target);
            distance = result.distance;
            return result.detected;
        }

        if (SensoryManager.DistanceCalculationOverride != null && SensorTile.Valid && target.CurrentTile.Valid)
        {
            int maxSteps = (int)DetectionRadiusInTiles;
            distance = SensoryManager.DistanceCalculationOverride(SensorTile, target.CurrentTile, maxSteps);
            return distance <= maxSteps;
        }

        distance = 1;
        return true;
    }

    public bool CanDetect(IDetectableTarget target) => TryGetDetectionDistance(target, out _);
}

public static class GeoscapeTestHarness
{
    private static readonly FieldInfo? WorldObjectTileField =
        AccessTools.Field(typeof(WorldObject), "tile");

    private static readonly FieldInfo? WorldObjectFactionField =
        AccessTools.Field(typeof(WorldObject), "factionInt");

    private static readonly FieldInfo? CreatingWorldField =
        typeof(Current).GetField("creatingWorldInt", BindingFlags.Static | BindingFlags.NonPublic);

    public static readonly PlanetLayer DefaultLayer =
        (PlanetLayer)RuntimeHelpers.GetUninitializedObject(typeof(PlanetLayer));

    public static PlanetTile MakeTile(int id, PlanetLayer? layer = null) =>
        new(id, layer ?? DefaultLayer);

    public static void EnsureEnvironmentInitialised()
    {
        var prefsField = typeof(Prefs).GetField("data", BindingFlags.Static | BindingFlags.NonPublic);
        if (prefsField != null && prefsField.GetValue(null) == null)
        {
            var prefsDataType = typeof(Prefs).Assembly.GetType("Verse.PrefsData");
            if (prefsDataType != null)
            {
                prefsField.SetValue(null, Activator.CreateInstance(prefsDataType));
            }
        }

        if (LanguageDatabase.activeLanguage == null)
        {
            var lang = new LoadedLanguage("English");
            var infoField = typeof(LoadedLanguage).GetField("info");
            if (infoField != null)
            {
                infoField.SetValue(lang, Activator.CreateInstance(infoField.FieldType));
            }

            LanguageDatabase.activeLanguage = lang;
        }

        RegisterAllSuiteModulesForTesting();
    }

    public static void RegisterAllSuiteModulesForTesting()
    {
        VigilanceCoreMod.RegisterBackCompatibilityConverter();
        GeoscapeMod.RegisterModuleHooks();
        OrbitalMod.RegisterModuleHooks();
        AquaticsMod.RegisterModuleHooks();
        ThermalMod.RegisterModuleHooks();
    }

    public static void InitMockWorld(out PlanetLayer surfaceLayer)
    {
        var world = (RimWorld.Planet.World)RuntimeHelpers.GetUninitializedObject(typeof(RimWorld.Planet.World));
        var grid = (WorldGrid)RuntimeHelpers.GetUninitializedObject(typeof(WorldGrid));
        var surfaceType = typeof(WorldGrid).Assembly.GetType("RimWorld.SurfaceLayer") ?? typeof(PlanetLayer);
        surfaceLayer = (PlanetLayer)RuntimeHelpers.GetUninitializedObject(surfaceType);
        var layers = new Dictionary<int, PlanetLayer> { [0] = surfaceLayer };

        AccessTools.Field(typeof(WorldGrid), "surface")?.SetValue(grid, surfaceLayer);
        AccessTools.Field(typeof(WorldGrid), "planetLayers")?.SetValue(grid, layers);
        world.grid = grid;
        world.components = new List<WorldComponent>();

        CreatingWorldField?.SetValue(null, world);
    }

    public static void ClearMockWorld()
    {
        CreatingWorldField?.SetValue(null, null);
    }

    public static Map CreateMockMap() =>
        (Map)RuntimeHelpers.GetUninitializedObject(typeof(Map));

    public static Faction CreatePlayerFaction() =>
        new() { def = new FactionDef { isPlayer = true } };

    public static Faction CreateNpcFaction(
        string defName = "Pirates",
        string label = "pirates",
        string name = "The Red Corsairs",
        Color? color = null)
    {
        var faction = new Faction
        {
            def = new FactionDef
            {
                defName = defName,
                label = label,
                colorSpectrum = color.HasValue ? new List<Color> { color.Value } : null
            }
        };
        if (color.HasValue)
        {
            faction.color = color.Value;
        }

        AccessTools.Field(typeof(Faction), "name")?.SetValue(faction, name);
        return faction;
    }

    public static Caravan CreateCaravan(
        PlanetTile tile,
        Faction? faction = null,
        string? name = null,
        PlanetTile? destTile = null,
        bool moving = false,
        bool withPather = false)
    {
        var caravan = (Caravan)RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
        WorldObjectTileField?.SetValue(caravan, tile);
        if (faction != null)
        {
            WorldObjectFactionField?.SetValue(caravan, faction);
        }

        if (name != null)
        {
            Traverse.Create(caravan).Field("nameInt").SetValue(name);
        }

        if (destTile.HasValue || moving || withPather)
        {
            var pather = (Caravan_PathFollower)RuntimeHelpers.GetUninitializedObject(typeof(Caravan_PathFollower));
            Traverse.Create(caravan).Field("pather").SetValue(pather);
            Traverse.Create(pather).Field("caravan").SetValue(caravan);
            Traverse.Create(pather).Field("moving").SetValue(moving);
            if (destTile.HasValue)
            {
                Traverse.Create(pather).Field("destTile").SetValue(destTile.Value);
            }
        }

        return caravan;
    }

    public static Settlement CreateSettlement(
        PlanetTile tile,
        Faction? faction = null,
        string? name = null,
        int id = 1000,
        Map? map = null,
        WorldObjectDef? def = null)
    {
        var settlement = new Settlement { ID = id, def = def };
        WorldObjectTileField?.SetValue(settlement, tile);
        if (faction != null)
        {
            WorldObjectFactionField?.SetValue(settlement, faction);
        }

        if (name != null)
        {
            Traverse.Create(settlement).Field("nameInt").SetValue(name);
        }

        if (map != null)
        {
            Traverse.Create(settlement).Field("mapInt").SetValue(map);
        }

        return settlement;
    }

    public static void ResetAllStaticState()
    {
        SensoryManager.DistanceCalculationOverride = null;
        SensoryManager.IsExploredOverride = null;
        SensoryManager.IsIdentifiedOverride = null;
        SensoryManager.TileNeighborsOverride = null;
        SensoryManager.ActiveSensorsOverride = null;
        SensoryManager.Instance = null;

        SensoryTerrainUtility.TargetMaskedOverride = null;
        SensoryTerrainUtility.VantageBonusOverride = null;

        SensoryDisruptionUtility.SolarFlareOverride = null;
        SensoryDisruptionUtility.EclipseOverride = null;

        SonarTraversalUtility.ResetTestOverrides();

        WorldDrawLayer_RadarCoverage.SubMeshMaterialOverride = null;
        WorldDrawLayer_RadarCoverage.RenderQueueMaterialOverride = null;
        WorldDrawLayer_RadarCoverage.TileVerticesOverride = null;
        WorldDrawLayer_RadarCoverage.TileCenterOverride = null;
        WorldDrawLayer_RadarCoverage.TileNeighborsOverride = null;

        WorldDrawLayer_HazeOfWar.SubMeshMaterialOverride = null;
        WorldDrawLayer_HazeOfWar.RenderQueueMaterialOverride = null;
        WorldDrawLayer_HazeOfWar.IsInMainThreadOverride = null;

        HazeReconnaissanceManager.WorldObjectLookupOverride = null;
        HazeReconnaissanceManager.Instance = null;
        ContactRegistry.Instance = null;

        InterFactionEncounterManager.IncursionProviderOverride = null;
        InterFactionEncounterManager.Instance = null;
        IncursionOriginResolver.StepMovementDifficultyOverride = null;

        OptimisticPathingUtility.IsTileUnexploredOverride = null;
        OptimisticPathingUtility.IsTileImpassableOverride = null;
        OptimisticPathingUtility.OnCaravanReroutedForTesting = null;
        OptimisticPathingUtility.OnCaravanHaltedForTesting = null;
        OptimisticPathingUtility.Active = false;

        CaravanExpeditionUtility.ClearAllPendingDirectives();
        CaravanExpeditionUtility.BeaconMaterialsOverride = null;

        OutpostTileOccupancyUtility.BlockingObjectAtOverride = null;
        OutpostTileOccupancyUtility.ListeningPostAtOverride = null;

        OutpostAssemblySpeedUtility.LocalHourOverride = null;
        OutpostAssemblySpeedUtility.BuilderAwakeOverride = null;
        OutpostAssemblySpeedUtility.CapableBuilderOverride = null;
        OutpostAssemblySpeedUtility.BuilderSpeedOverride = null;

        WorldObject_ListeningPost.PlayerCaravanPresenceOverride = null;
        WorldObject_ListeningPost.CaravanPawnsOverride = null;

        Caravan_NightResting_Patch.NightOwlPredicateOverride = null;
        Caravan_PathFollower_StartPath_Patch.ActivePostFinderOverride = null;
        Caravan_PathFollower_StartPath_Patch.DepartureDialogOverride = null;
        Caravan_PathFollower_StartPath_Patch.SuppressDeparturePrompt = false;

        WorldSelector_HandleWorldClicks_Patch.MouseTileOverride = null;
        WorldSelector_HandleWorldClicks_Patch.SafeMapOverride = null;
        WorldSelector_SelectFirstOrNextFrom_Patch.ShiftHeldOverride = null;

        FloatMenuMakerWorld_ChoicesAtFor_Patch.MouseTileOverride = null;
        FloatMenuMakerWorld_ChoicesAtFor_Patch.SettlementAtOverride = null;

        TileHighlightVisibilityUtility.IsPlayingOverride = null;
        WorldObject_DrawPos_Patch.IsSpaceObjectOverride = null;
        SpaceObject_InspectString_Patch.IsSpaceObjectOverride = null;
        WorldRoutePlanner_Start_Patch.OnWaypointAddedForTesting = null;
        ProjectVigilance.Geoscape.Debug.DebugActionsVigilance.TargetTileOverride = null;

        GeoscapeCampaignRules.ResetStaticDataForTesting();
        UnsafeRuleOverride.ResetForTesting();
        NewCampaignRuleDraft.Clear();

        VigilanceModuleManager.ResetForTesting();
        VigilanceMigrationTracker.ClearPendingInterceptions();
        ThermalMod.Settings.enableThermalIntegrityAudit = true;
        RegisterAllSuiteModulesForTesting();

        GeoscapeMod.Settings.ResetToDefaults();
        ClearMockWorld();
    }
}
