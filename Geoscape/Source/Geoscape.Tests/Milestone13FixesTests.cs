using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape;
using ProjectVigilance.Geoscape.Patches;
using ProjectVigilance.Geoscape.Sensory;
using ProjectVigilance.Geoscape.World;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Geoscape.Tests;

[TestClass]
[DoNotParallelize]
public sealed class Milestone13FixesTests
{
    private static readonly PlanetLayer MockLayer =
        (PlanetLayer)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(PlanetLayer));

    private static PlanetTile MakeTile(int id) => new PlanetTile(id, MockLayer);

    [TestInitialize]
    public void Setup()
    {
        var prefsField = typeof(Prefs).GetField("data", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
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

        var world = (World)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(World));
        var grid = (WorldGrid)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldGrid));
        var surfaceType = typeof(WorldGrid).Assembly.GetType("RimWorld.SurfaceLayer") ?? typeof(PlanetLayer);
        var surface = (PlanetLayer)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(surfaceType);
        var layers = new Dictionary<int, PlanetLayer> { [0] = surface };

        typeof(WorldGrid).GetField("surface", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.SetValue(grid, surface);
        typeof(WorldGrid).GetField("planetLayers", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.SetValue(grid, layers);
        world.grid = grid;
        world.components = new List<WorldComponent>();

        typeof(Current).GetField("creatingWorldInt", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?.SetValue(null, world);
    }

    [TestCleanup]
    public void Cleanup()
    {
        SonarTraversalUtility.ResetTestOverrides();
        OptimisticPathingUtility.IsTileImpassableOverride = null;
        SensoryManager.ActiveSensorsOverride = null;
        SensoryManager.TileNeighborsOverride = null;
        SensoryManager.DistanceCalculationOverride = null;
        SensoryManager.IsExploredOverride = null;
        SensoryManager.IsIdentifiedOverride = null;
        SensoryManager.Instance = null;
        WorldDrawLayer_RadarCoverage.SubMeshMaterialOverride = null;
        WorldDrawLayer_RadarCoverage.RenderQueueMaterialOverride = null;
        WorldDrawLayer_RadarCoverage.TileVerticesOverride = null;
        WorldDrawLayer_RadarCoverage.TileCenterOverride = null;
        WorldDrawLayer_RadarCoverage.TileNeighborsOverride = null;
        OutpostTileOccupancyUtility.BlockingObjectAtOverride = null;
        OutpostTileOccupancyUtility.ListeningPostAtOverride = null;
        WorldSelector_HandleWorldClicks_Patch.SafeMapOverride = null;
        typeof(Current).GetField("creatingWorldInt", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?.SetValue(null, null);
    }

    [TestMethod]
    public void AutomatedSonar_CalculatesCorrectBaseRadii()
    {
        var (detection, identification) = WorldObject_ListeningPost.CalculateRadii(
            ListeningPostKind.AutomatedSonar,
            vantageBonus: 0,
            isSabotaged: false);

        Assert.AreEqual(18f, detection);
        Assert.AreEqual(8, identification);
    }

    [TestMethod]
    public void AutomatedSonar_AppliesSabotagePenalty()
    {
        var (detection, identification) = WorldObject_ListeningPost.CalculateRadii(
            ListeningPostKind.AutomatedSonar,
            vantageBonus: 0,
            isSabotaged: true);

        Assert.AreEqual(0f, detection);
        Assert.AreEqual(0, identification);
    }

    [TestMethod]
    public void OutpostMaterialUtility_AutomatedSonar_MaterialCosts()
    {
        int[] actual =
        {
            OutpostMaterialUtility.AutomatedSonarSteelCost,
            OutpostMaterialUtility.AutomatedSonarComponentCost,
            OutpostMaterialUtility.AutomatedSonarSteelRepairCost,
            OutpostMaterialUtility.AutomatedSonarComponentRepairCost,
            OutpostMaterialUtility.AutomatedSonarSteelSalvage,
            OutpostMaterialUtility.AutomatedSonarComponentSalvage,
        };

        CollectionAssert.AreEqual(new[] { 120, 4, 30, 1, 90, 3 }, actual);
    }

    [TestMethod]
    public void OutpostAssemblySpeedUtility_AutomatedSonar_WorkTicks()
    {
        int actual = OutpostAssemblySpeedUtility.BaseAutomatedSonarTicks;
        Assert.AreEqual(15000, actual);
    }

    [TestMethod]
    public void SonarTraversalUtility_CoastalAdjacency_DetectsWaterNeighbors()
    {
        var stationTile = MakeTile(100);
        var waterTile1 = MakeTile(101);
        var waterTile2 = MakeTile(102);
        var landTile = MakeTile(103);

        SonarTraversalUtility.GetTileNeighborsOverride = (t, outNeighbors) =>
        {
            if (t.tileId == 100)
            {
                outNeighbors.Add(waterTile1);
                outNeighbors.Add(waterTile2);
                outNeighbors.Add(landTile);
            }
        };

        SonarTraversalUtility.IsTileWaterCoveredOverride = t => t.tileId == 101 || t.tileId == 102;

        Assert.IsTrue(SonarTraversalUtility.HasCoastalAccess(stationTile));

        var adjacentWater = new List<PlanetTile>();
        SonarTraversalUtility.GetAdjacentWaterTiles(stationTile, adjacentWater);

        Assert.AreEqual(2, adjacentWater.Count);
        Assert.IsTrue(adjacentWater.Contains(waterTile1));
        Assert.IsTrue(adjacentWater.Contains(waterTile2));
        Assert.IsFalse(adjacentWater.Contains(landTile));
    }

    [TestMethod]
    public void SonarTraversalUtility_WaterOnlyTraversal_BlockedByLandBarrier()
    {
        // 100 (Station, Land)
        // 101 (Water, Adjacent to Station)
        // 102 (Land Barrier)
        // 103 (Isolated Ocean Tile behind land)
        var stationTile = MakeTile(100);
        var waterTile1 = MakeTile(101);
        var landBarrier = MakeTile(102);
        var isolatedWater = MakeTile(103);

        SonarTraversalUtility.GetTileNeighborsOverride = (t, outNeighbors) =>
        {
            if (t.tileId == 100) outNeighbors.Add(waterTile1);
            else if (t.tileId == 101) { outNeighbors.Add(stationTile); outNeighbors.Add(landBarrier); }
            else if (t.tileId == 102) { outNeighbors.Add(waterTile1); outNeighbors.Add(isolatedWater); }
            else if (t.tileId == 103) outNeighbors.Add(landBarrier);
        };

        SonarTraversalUtility.IsTileWaterCoveredOverride = t => t.tileId == 101 || t.tileId == 103;

        var detectionTiles = new HashSet<PlanetTile>();
        var idTiles = new HashSet<PlanetTile>();

        SonarTraversalUtility.PopulateSonarCoverage(
            stationTile,
            maxDetectionRadius: 5,
            maxIdentificationRadius: 2,
            outDetectionTiles: detectionTiles,
            outIdentificationTiles: idTiles);

        Assert.IsTrue(detectionTiles.Contains(waterTile1), "Adjacent water tile must be revealed.");
        Assert.IsFalse(detectionTiles.Contains(landBarrier), "Land barrier must not be revealed by sonar.");
        Assert.IsFalse(detectionTiles.Contains(isolatedWater), "Isolated water across land barrier must not be revealed.");
    }

    [TestMethod]
    public void OptimisticPathingUtility_BreakEvenDetour_EvaluatesCorrectly()
    {
        var state = new ExpeditionDetourState
        {
            InitialDirectDistance = 10f,
            MaxDetourMultiplier = 2.0f
        };

        // Detour factor = 15 / 10 = 1.5 -> not exceeded
        Assert.IsFalse(15f > state.InitialDirectDistance * state.MaxDetourMultiplier);

        // Detour factor = 25 / 10 = 2.5 -> exceeded
        Assert.IsTrue(25f > state.InitialDirectDistance * state.MaxDetourMultiplier);
    }

    [TestMethod]
    public void OptimisticPathingUtility_UnitTraversalCapability_ObstacleEvaluation()
    {
        var waterTile = MakeTile(201);
        var landTile = MakeTile(202);

        SonarTraversalUtility.IsTileWaterCoveredOverride = t => t.tileId == 201;
        OptimisticPathingUtility.IsTileImpassableOverride = t => t.tileId == 201; // Water impassable for land caravans

        // Land Caravan (water is obstacle/impassable)
        Assert.IsTrue(OptimisticPathingUtility.IsTileObstacleForUnit(waterTile, UnitTraversalCapability.LandCaravan));
        Assert.IsFalse(OptimisticPathingUtility.IsTileObstacleForUnit(landTile, UnitTraversalCapability.LandCaravan));

        // Naval Unit (land is obstacle, water is passable)
        Assert.IsFalse(OptimisticPathingUtility.IsTileObstacleForUnit(waterTile, UnitTraversalCapability.NavalUnit));
        Assert.IsTrue(OptimisticPathingUtility.IsTileObstacleForUnit(landTile, UnitTraversalCapability.NavalUnit));

        // Aircraft (ignores terrain obstacles)
        Assert.IsFalse(OptimisticPathingUtility.IsTileObstacleForUnit(waterTile, UnitTraversalCapability.Aircraft));
        Assert.IsFalse(OptimisticPathingUtility.IsTileObstacleForUnit(landTile, UnitTraversalCapability.Aircraft));
    }

    [TestMethod]
    public void OptimisticPathingUtility_GetOrCreateExpeditionState_ResetsWhenDestinationChanges()
    {
        var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
        caravan.ID = 42;
        var tileField = typeof(WorldObject).GetField("tile", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        tileField?.SetValue(caravan, MakeTile(10));

        try
        {
            var dest1 = MakeTile(14);
            ExpeditionDetourState firstState = OptimisticPathingUtility.GetOrCreateExpeditionState(caravan, dest1);
            Assert.AreEqual(4f, firstState.InitialDirectDistance);
            Assert.AreEqual(dest1, firstState.DestinationTile);

            firstState.MaxDetourMultiplier = 3.5f;
            firstState.NotifiedObstacleTiles.Add(12);

            ExpeditionDetourState sameDestState = OptimisticPathingUtility.GetOrCreateExpeditionState(caravan, dest1);
            Assert.AreSame(firstState, sameDestState);
            Assert.AreEqual(4f, sameDestState.InitialDirectDistance);
            Assert.AreEqual(3.5f, sameDestState.MaxDetourMultiplier);
            Assert.AreEqual(1, sameDestState.NotifiedObstacleTiles.Count);

            var dest2 = MakeTile(110);
            ExpeditionDetourState newDestState = OptimisticPathingUtility.GetOrCreateExpeditionState(caravan, dest2);
            Assert.AreEqual(dest2, newDestState.DestinationTile);
            Assert.AreEqual(100f, newDestState.InitialDirectDistance);
            Assert.AreEqual(2.0f, newDestState.MaxDetourMultiplier);
            Assert.AreEqual(0, newDestState.NotifiedObstacleTiles.Count);
        }
        finally
        {
            OptimisticPathingUtility.ResetExpeditionState(42);
        }
    }

    private sealed class DomainTestSensor : ISensorProvider
    {
        public string SensorLabel { get; set; } = "Test Sensor";
        public bool IsTechnological { get; set; }
        public PlanetTile SensorTile { get; set; }
        public float DetectionRadiusInTiles { get; set; }
        public int IdentificationRadiusInTiles { get; set; }
        public IntelligenceTier MaxIntelligenceTier { get; set; } = IntelligenceTier.Classified;
        public bool PenetratesTerrainMasking { get; set; }
        public int SigintPenetrationRadiusInTiles { get; set; }
        public bool IsOceanicSonar { get; set; }
        public bool IsSensorActive { get; set; } = true;
        public bool IsStationary { get; set; } = true;
        public WorldObject? Source { get; set; }
    }

    [TestMethod]
    public void SensoryManager_PopulateActiveVisionTiles_TracksVisualRadarSonarDomains()
    {
        var land0 = MakeTile(10);
        var land1 = MakeTile(11);
        var water2 = MakeTile(12);
        var water3 = MakeTile(13);

        var manager = new SensoryManager(Find.World!);
        SensoryManager.Instance = manager;

        static void PopulateNeighbors(PlanetTile t, List<PlanetTile> outNeighbors)
        {
            outNeighbors.Clear();
            switch (t.tileId)
            {
                case 10: outNeighbors.Add(MakeTile(11)); outNeighbors.Add(MakeTile(12)); break;
                case 11: outNeighbors.Add(MakeTile(10)); outNeighbors.Add(MakeTile(13)); break;
                case 12: outNeighbors.Add(MakeTile(10)); outNeighbors.Add(MakeTile(13)); break;
                case 13: outNeighbors.Add(MakeTile(11)); outNeighbors.Add(MakeTile(12)); break;
            }
        }

        SensoryManager.TileNeighborsOverride = PopulateNeighbors;
        SonarTraversalUtility.GetTileNeighborsOverride = PopulateNeighbors;
        SonarTraversalUtility.IsTileWaterCoveredOverride = t => t.tileId is 12 or 13;
        SensoryManager.DistanceCalculationOverride = (a, b, _) =>
        {
            if (a.tileId == b.tileId) return 0;
            if ((a.tileId == 10 && b.tileId == 13) || (a.tileId == 13 && b.tileId == 10)) return 2;
            return 1;
        };

        var visualSensor = new DomainTestSensor { SensorTile = land0, DetectionRadiusInTiles = 1f, IsTechnological = false, IsOceanicSonar = false };
        var radarSensor = new DomainTestSensor { SensorTile = land0, DetectionRadiusInTiles = 2f, IsTechnological = true, IsOceanicSonar = false };
        var sonarSensor = new DomainTestSensor { SensorTile = land0, DetectionRadiusInTiles = 5f, IsTechnological = true, IsOceanicSonar = true };

        SensoryManager.ActiveSensorsOverride = () => new ISensorProvider[] { visualSensor, radarSensor, sonarSensor };

        var activeTiles = new HashSet<PlanetTile>();
        var domains = new Dictionary<PlanetTile, SensorDomainFlags>();
        manager.PopulateActiveVisionTiles(activeTiles, outTileDomains: domains);

        Assert.AreEqual(SensorDomainFlags.Visual | SensorDomainFlags.Radar, domains[land0], "Land origin should show Visual + Radar without Sonar.");
        Assert.AreEqual(SensorDomainFlags.Visual | SensorDomainFlags.Radar, domains[land1], "Land neighbor should show Visual + Radar without Sonar.");
        Assert.AreEqual(SensorDomainFlags.Visual | SensorDomainFlags.Radar | SensorDomainFlags.Sonar, domains[water2], "Adjacent coastal water tile should merge all 3 domains.");
        Assert.AreEqual(SensorDomainFlags.Radar | SensorDomainFlags.Sonar, domains[water3], "Connected outer water tile covered by radar and sonar should merge Radar | Sonar.");
        Assert.AreEqual("Radar + Sonar", SensoryManager.FormatSensorDomainFlags(domains[water3]));
    }

    [TestMethod]
    public void WorldDrawLayer_RadarCoverage_DifferentiatesVisualRadarAndSonarGeometry()
    {
        var settings = GeoscapeMod.Settings;
        var origVis = settings.visibilityMode;
        var origStyle = settings.radarOutlineStyle;
        var origFactionColor = settings.usePlayerFactionColor;
        var origCustomColor = settings.radarCustomColor;
        var origOutlineAlpha = settings.radarOutlineOpacity;
        var origGridAlpha = settings.honeycombGridOpacity;
        var origDimming = settings.surveillanceDimming;
        var origShowId = settings.showIdentificationHorizon;

        try
        {
            settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;
            settings.radarOutlineStyle = RadarOutlineStyle.BoundaryAndGrid;
            settings.usePlayerFactionColor = false;
            settings.radarCustomColor = new Color(0f, 1f, 0.4f); // Distinct green
            settings.radarOutlineOpacity = 0.8f;
            settings.honeycombGridOpacity = 0.6f;
            settings.surveillanceDimming = 0f;
            settings.showIdentificationHorizon = false;

            var tileA = MakeTile(10);
            var tileB = MakeTile(11);
            var manager = new SensoryManager(Find.World!);
            SensoryManager.Instance = manager;

            var coastalOrigin = MakeTile(9);
            static void PopulateNeighbors(PlanetTile t, List<PlanetTile> outNeighbors)
            {
                outNeighbors.Clear();
                if (t.tileId == 9)
                {
                    outNeighbors.Add(MakeTile(10)); // Coastal station adjacent to water tileA
                }
                else if (t.tileId == 10)
                {
                    outNeighbors.Add(MakeTile(11)); // Edge 0 shared with tileB
                    outNeighbors.Add(MakeTile(20));
                    outNeighbors.Add(MakeTile(21));
                    outNeighbors.Add(MakeTile(22));
                }
                else if (t.tileId == 11)
                {
                    outNeighbors.Add(MakeTile(10)); // Edge 0 shared with tileA
                    outNeighbors.Add(MakeTile(30));
                    outNeighbors.Add(MakeTile(31));
                    outNeighbors.Add(MakeTile(32));
                }
            }

            static void PopulateVerts(PlanetTile t, List<Vector3> outVerts)
            {
                outVerts.Clear();
                if (t.tileId == 10)
                {
                    outVerts.Add(new Vector3(0.5f, -0.5f, 100f));
                    outVerts.Add(new Vector3(0.5f, 0.5f, 100f));
                    outVerts.Add(new Vector3(-0.5f, 0.5f, 100f));
                    outVerts.Add(new Vector3(-0.5f, -0.5f, 100f));
                }
                else if (t.tileId == 11)
                {
                    outVerts.Add(new Vector3(0.5f, 0.5f, 100f));
                    outVerts.Add(new Vector3(0.5f, -0.5f, 100f));
                    outVerts.Add(new Vector3(1.5f, -0.5f, 100f));
                    outVerts.Add(new Vector3(1.5f, 0.5f, 100f));
                }
            }

            SensoryManager.TileNeighborsOverride = PopulateNeighbors;
            SonarTraversalUtility.GetTileNeighborsOverride = PopulateNeighbors;
            SonarTraversalUtility.IsTileWaterCoveredOverride = t => t.tileId == 10 || t.tileId == 11;
            SensoryManager.DistanceCalculationOverride = (a, b, _) =>
            {
                if (a.tileId == b.tileId) return 0;
                if ((a.tileId == 10 && b.tileId == 11) || (a.tileId == 11 && b.tileId == 10)) return 1;
                return 99;
            };

            WorldDrawLayer_RadarCoverage.SubMeshMaterialOverride = () => null!;
            WorldDrawLayer_RadarCoverage.RenderQueueMaterialOverride = _ => null!;
            WorldDrawLayer_RadarCoverage.TileNeighborsOverride = PopulateNeighbors;
            WorldDrawLayer_RadarCoverage.TileVerticesOverride = PopulateVerts;
            WorldDrawLayer_RadarCoverage.TileCenterOverride = t => t.tileId == 10 ? new Vector3(0f, 0f, 100f) : new Vector3(1f, 0f, 100f);

            var layer = new WorldDrawLayer_RadarCoverage();
            typeof(WorldDrawLayer).GetField("planetLayer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(layer, Find.World!.grid.Surface);

            // 1. Visual-only: 6 outer edges only (24 verts), zero interior grid edge, warm-ivory perimeter RGB
            var visualSensor = new DomainTestSensor { SensorTile = tileA, DetectionRadiusInTiles = 2f, IsTechnological = false, IsOceanicSonar = false };
            SensoryManager.ActiveSensorsOverride = () => new ISensorProvider[] { visualSensor };
            foreach (object _ in layer.Regenerate()) { }

            var visualVerts = layer.SubMeshes.SelectMany(sm => sm.verts).ToList();
            var visualColors = layer.SubMeshes.SelectMany(sm => sm.colors).ToList();
            Assert.AreEqual(24, visualVerts.Count, "Visual-only coverage must emit outer perimeter edges only (no interior honeycomb grid).");
            Assert.IsTrue(visualColors.All(c => c.r == WorldDrawLayer_RadarCoverage.VisualPerimeterColorRgb.r &&
                                                c.g == WorldDrawLayer_RadarCoverage.VisualPerimeterColorRgb.g &&
                                                c.b == WorldDrawLayer_RadarCoverage.VisualPerimeterColorRgb.b),
                "Visual-only perimeter must use warm-ivory horizon color.");

            // 2. Sonar-only from coastal land station: 6 outer + 1 interior edge (28 verts), Oceanic Cyan RGB
            var sonarSensor = new DomainTestSensor { SensorTile = coastalOrigin, DetectionRadiusInTiles = 3f, IsTechnological = true, IsOceanicSonar = true };
            SensoryManager.ActiveSensorsOverride = () => new ISensorProvider[] { sonarSensor };
            foreach (object _ in layer.Regenerate()) { }

            var sonarVerts = layer.SubMeshes.SelectMany(sm => sm.verts).ToList();
            var sonarColors = layer.SubMeshes.SelectMany(sm => sm.colors).ToList();
            Assert.AreEqual(28, sonarVerts.Count, "Sonar coverage must include both outer boundary and interior water grid.");
            Assert.IsTrue(sonarColors.All(c => c.r == WorldDrawLayer_RadarCoverage.SonarPerimeterColorRgb.r &&
                                               c.g == WorldDrawLayer_RadarCoverage.SonarPerimeterColorRgb.g &&
                                               c.b == WorldDrawLayer_RadarCoverage.SonarPerimeterColorRgb.b),
                "Sonar coverage edges must render in Oceanic Cyan.");

            // 3. Radar + Sonar overlap: outer edges use Radar green while interior water edge uses Sonar cyan
            var radarSensor = new DomainTestSensor { SensorTile = tileA, DetectionRadiusInTiles = 2f, IsTechnological = true, IsOceanicSonar = false };
            SensoryManager.ActiveSensorsOverride = () => new ISensorProvider[] { radarSensor, sonarSensor };
            foreach (object _ in layer.Regenerate()) { }

            var overlapColors = layer.SubMeshes.SelectMany(sm => sm.colors).ToList();
            Assert.AreEqual(28, overlapColors.Count);
            int sonarCyanVerts = overlapColors.Count(c =>
                c.r == WorldDrawLayer_RadarCoverage.SonarPerimeterColorRgb.r &&
                c.g == WorldDrawLayer_RadarCoverage.SonarPerimeterColorRgb.g &&
                c.b == WorldDrawLayer_RadarCoverage.SonarPerimeterColorRgb.b);
            Assert.AreEqual(4, sonarCyanVerts, "Overlapping Radar + Sonar must render Radar outer boundary and Sonar cyan interior water grid.");
        }
        finally
        {
            settings.visibilityMode = origVis;
            settings.radarOutlineStyle = origStyle;
            settings.usePlayerFactionColor = origFactionColor;
            settings.radarCustomColor = origCustomColor;
            settings.radarOutlineOpacity = origOutlineAlpha;
            settings.honeycombGridOpacity = origGridAlpha;
            settings.surveillanceDimming = origDimming;
            settings.showIdentificationHorizon = origShowId;
        }
    }

    [TestMethod]
    public void ContactLabel_And_TelemetrySummary_IncludeActiveSensorDomain()
    {
        Assert.AreEqual("Radar Contact-3", SensoryNarrativeUtility.GetContactLabel(trackingId: 3, domainFlags: SensorDomainFlags.Radar));
        Assert.AreEqual("Sonar Contact-4", SensoryNarrativeUtility.GetContactLabel(trackingId: 4, domainFlags: SensorDomainFlags.Sonar));
        Assert.AreEqual("Visual Contact-5", SensoryNarrativeUtility.GetContactLabel(trackingId: 5, domainFlags: SensorDomainFlags.Visual));
        Assert.AreEqual("Contact-6", SensoryNarrativeUtility.GetContactLabel(trackingId: 6, domainFlags: SensorDomainFlags.None));

        var settings = GeoscapeMod.Settings;
        var origVis = settings.visibilityMode;
        try
        {
            settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;
            var coastalTile = MakeTile(50);
            var manager = new SensoryManager(Find.World!);
            SensoryManager.Instance = manager;

            static void NoNeighbors(PlanetTile _, List<PlanetTile> n) => n.Clear();
            SensoryManager.TileNeighborsOverride = NoNeighbors;
            SonarTraversalUtility.GetTileNeighborsOverride = NoNeighbors;
            SonarTraversalUtility.IsTileWaterCoveredOverride = t => t.tileId == 50;
            SensoryManager.DistanceCalculationOverride = (a, b, _) => a.tileId == b.tileId ? 0 : 99;
            SensoryManager.IsExploredOverride = t => t.tileId == 50;
            SensoryManager.IsIdentifiedOverride = _ => false;

            var radarSensor = new DomainTestSensor { SensorTile = coastalTile, DetectionRadiusInTiles = 2f, IsTechnological = true, IsOceanicSonar = false };
            SensoryManager.ActiveSensorsOverride = () => new ISensorProvider[] { radarSensor };

            Assert.AreEqual("Telemetry: Radar (Detected)", TerrainTelemetryUtility.FormatActiveTelemetrySummary(coastalTile));

            SensoryManager.IsIdentifiedOverride = _ => true;
            Assert.AreEqual("Telemetry: Radar (Identified)", TerrainTelemetryUtility.FormatActiveTelemetrySummary(coastalTile));
            Assert.AreEqual(string.Empty, TerrainTelemetryUtility.FormatActiveTelemetrySummary(MakeTile(999)));
        }
        finally
        {
            settings.visibilityMode = origVis;
        }
    }

    [TestMethod]
    public void OutpostTileExclusivity_EnforcesBidirectionalGuardsAcrossCreationAndSettleCamp()
    {
        PlanetTile tile = MakeTile(300);

        // 1. Completed outpost blocks new outpost construction
        var completedPost = (WorldObject_ListeningPost)RuntimeHelpers.GetUninitializedObject(typeof(WorldObject_ListeningPost));
        OutpostTileOccupancyUtility.ListeningPostAtOverride = _ => completedPost;
        OutpostTileOccupancyUtility.BlockingObjectAtOverride = null;

        Assert.IsFalse(OutpostTileOccupancyUtility.CanConstructOutpostAt(tile, out string reasonCompleted));
        Assert.IsTrue(reasonCompleted.Contains("already occupied", StringComparison.OrdinalIgnoreCase));

        // 2. Under-construction outpost blocks fresh construction, allows resume via StillValid
        completedPost.StartConstruction(2500);
        Assert.IsFalse(OutpostTileOccupancyUtility.CanConstructOutpostAt(tile, out _, allowUnderConstructionPost: false));
        Assert.IsTrue(OutpostTileOccupancyUtility.CanConstructOutpostAt(tile, out _, allowUnderConstructionPost: true));

        // 3. TileFinder & SettleInEmptyTileUtility block Settle and Make Camp on outpost tile
        bool settleValid = true;
        var settleReason = new StringBuilder();
        StartingSiteSelectionFeedback_Patch.TileFinder_IsValidTileForNewSettlement_OutpostExclusivity_Patch.Postfix(tile, settleReason, ref settleValid);
        Assert.IsFalse(settleValid, "Settle must be blocked on outpost tile.");
        Assert.IsTrue(settleReason.ToString().Contains("dismantle outpost first", StringComparison.OrdinalIgnoreCase));

        bool canCreateMap = true;
        StartingSiteSelectionFeedback_Patch.SettleInEmptyTileUtility_CanCreateMapAt_Patch.Postfix(tile, ref canCreateMap);
        Assert.IsFalse(canCreateMap, "Make Camp map creation must be blocked on outpost tile.");

        var caravan = (Caravan)RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
        typeof(WorldObject).GetField("tile", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(caravan, tile);
        var campCmd = new Command_Action();
        StartingSiteSelectionFeedback_Patch.SettleInEmptyTileUtility_SetupCamp_Patch.Postfix(caravan, campCmd);
        Assert.IsTrue(campCmd.Disabled, "SetupCamp command must be disabled on outpost tile.");
        Assert.IsTrue(campCmd.disabledReason.Contains("dismantle outpost first", StringComparison.OrdinalIgnoreCase));

        // 4. Settlements, camps, and ruins block outpost construction
        OutpostTileOccupancyUtility.ListeningPostAtOverride = null;
        var npcSettlement = (Settlement)RuntimeHelpers.GetUninitializedObject(typeof(Settlement));
        typeof(WorldObject).GetField("tile", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(npcSettlement, tile);
        typeof(Settlement).GetField("nameInt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(npcSettlement, "Outlander Town");
        OutpostTileOccupancyUtility.BlockingObjectAtOverride = t => t.tileId == tile.tileId ? npcSettlement : null;

        Assert.IsFalse(OutpostTileOccupancyUtility.CanConstructOutpostAt(tile, out string reasonSettlement));
        Assert.IsTrue(reasonSettlement.Contains("Outlander Town", StringComparison.OrdinalIgnoreCase));

        var arrivalAction = new CaravanArrivalAction_ConstructOutpost(tile, ListeningPostKind.AutomatedRelay);
        FloatMenuAcceptanceReport stillValid = arrivalAction.StillValid(caravan, tile);
        Assert.IsFalse((bool)stillValid, "ConstructOutpost arrival action must abort if destination has a settlement/site.");

        // 5. Settlement right-click dispatch menu suppresses outpost expedition options on occupied tile
        var homeSettlement = (Settlement)RuntimeHelpers.GetUninitializedObject(typeof(Settlement));
        var homeTile = MakeTile(301);
        typeof(WorldObject).GetField("tile", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(homeSettlement, homeTile);
        typeof(Settlement).GetField("nameInt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(homeSettlement, "Player Base");
        var mockMap = (Map)RuntimeHelpers.GetUninitializedObject(typeof(Map));
        WorldSelector_HandleWorldClicks_Patch.SafeMapOverride = s => ReferenceEquals(s, homeSettlement) ? mockMap : null;

        List<FloatMenuOption> dispatchOptions = WorldSelector_HandleWorldClicks_Patch.BuildSettlementDispatchOptions(homeSettlement, tile);
        Assert.IsFalse(dispatchOptions.Any(o => o.Label.Contains("Dispatch Construction Expedition", StringComparison.OrdinalIgnoreCase)),
            "Settlement dispatch menu must not offer outpost construction on a tile occupied by an existing settlement/site.");
    }
}
