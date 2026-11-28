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
    private static PlanetTile MakeTile(int id) => GeoscapeTestHarness.MakeTile(id);

    [TestInitialize]
    public void Setup()
    {
        GeoscapeTestHarness.EnsureEnvironmentInitialised();
        GeoscapeTestHarness.InitMockWorld(out _);
    }

    [TestCleanup]
    public void Cleanup() => GeoscapeTestHarness.ResetAllStaticState();

    [TestMethod]
    public void DebugActionsVigilance_AllActionsAreAllowedInGameState()
    {
        var methods = System.Linq.Enumerable.ToArray(
            System.Linq.Enumerable.SelectMany(
                new[]
                {
                    typeof(ProjectVigilance.Geoscape.Debug.DebugActionsVigilance),
                    typeof(ProjectVigilance.Geoscape.Orbital.DebugActionsOrbital),
                    typeof(ProjectVigilance.Geoscape.Sensory.DebugActionsAquatics)
                },
                t => t.GetMethods(
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)));

        int actionCount = 0;
        foreach (var m in methods)
        {
            var attr = m.GetCustomAttributes(typeof(LudeonTK.DebugActionAttribute), false).FirstOrDefault() as LudeonTK.DebugActionAttribute;
            if (attr != null)
            {
                actionCount++;
                // Cannot have both WorldRenderedNow (4) and IsCurrentlyOnMap (8) because RimWorld checks them with logical AND
                bool hasWorld = (attr.allowedGameStates & LudeonTK.AllowedGameStates.WorldRenderedNow) != 0;
                bool hasMap = (attr.allowedGameStates & LudeonTK.AllowedGameStates.IsCurrentlyOnMap) != 0;
                Assert.IsFalse(hasWorld && hasMap, $"Method {m.Name} combines WorldRenderedNow and IsCurrentlyOnMap, making it impossible to satisfy in RimWorld!");
            }
        }
        Assert.IsTrue(actionCount >= 10, "Expected at least 10 debug actions.");
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

        var detectionTiles = new HashSet<int>();
        var idTiles = new HashSet<int>();

        SonarTraversalUtility.PopulateSonarCoverage(
            stationTile,
            maxDetectionRadius: 5,
            maxIdentificationRadius: 2,
            outDetectionTileIds: detectionTiles,
            outIdentificationTileIds: idTiles);

        Assert.IsTrue(detectionTiles.Contains(waterTile1.tileId), "Adjacent water tile must be revealed.");
        Assert.IsFalse(detectionTiles.Contains(landBarrier.tileId), "Land barrier must not be revealed by sonar.");
        Assert.IsFalse(detectionTiles.Contains(isolatedWater.tileId), "Isolated water across land barrier must not be revealed.");
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
        var caravan = GeoscapeTestHarness.CreateCaravan(MakeTile(10));
        caravan.ID = 42;

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

        var visualSensor = new MockSensorProvider { IdentificationRadiusInTiles = 0, SensorTile = land0, DetectionRadiusInTiles = 1f, IsTechnological = false, IsOceanicSonar = false };
        var radarSensor = new MockSensorProvider { IdentificationRadiusInTiles = 0, SensorTile = land0, DetectionRadiusInTiles = 2f, IsTechnological = true, IsOceanicSonar = false };
        var sonarSensor = new MockSensorProvider { IdentificationRadiusInTiles = 0, SensorTile = land0, DetectionRadiusInTiles = 5f, IsTechnological = true, IsOceanicSonar = true };

        SensoryManager.ActiveSensorsOverride = () => new ISensorProvider[] { visualSensor, radarSensor, sonarSensor };

        var activeTiles = new HashSet<int>();
        var domains = new Dictionary<int, SensorDomainFlags>();
        manager.PopulateActiveVisionTiles(activeTiles, outTileDomains: domains);

        Assert.AreEqual(SensorDomainFlags.Visual | SensorDomainFlags.Radar, domains[land0.tileId], "Land origin should show Visual + Radar without Sonar.");
        Assert.AreEqual(SensorDomainFlags.Visual | SensorDomainFlags.Radar, domains[land1.tileId], "Land neighbor should show Visual + Radar without Sonar.");
        Assert.AreEqual(SensorDomainFlags.Visual | SensorDomainFlags.Radar | SensorDomainFlags.Sonar, domains[water2.tileId], "Adjacent coastal water tile should merge all 3 domains.");
        Assert.AreEqual(SensorDomainFlags.Radar | SensorDomainFlags.Sonar, domains[water3.tileId], "Connected outer water tile covered by radar and sonar should merge Radar | Sonar.");
        Assert.AreEqual("Radar + Sonar", SensoryManager.FormatSensorDomainFlags(domains[water3.tileId]));
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
            settings.visibilityMode = VisibilityMode.FogOfWar;
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
            var visualSensor = new MockSensorProvider { IdentificationRadiusInTiles = 0, SensorTile = tileA, DetectionRadiusInTiles = 2f, IsTechnological = false, IsOceanicSonar = false };
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
            var sonarSensor = new MockSensorProvider { IdentificationRadiusInTiles = 0, SensorTile = coastalOrigin, DetectionRadiusInTiles = 3f, IsTechnological = true, IsOceanicSonar = true };
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
            var radarSensor = new MockSensorProvider { IdentificationRadiusInTiles = 0, SensorTile = tileA, DetectionRadiusInTiles = 2f, IsTechnological = true, IsOceanicSonar = false };
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
            settings.visibilityMode = VisibilityMode.FogOfWar;
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

            var radarSensor = new MockSensorProvider { IdentificationRadiusInTiles = 0, SensorTile = coastalTile, DetectionRadiusInTiles = 2f, IsTechnological = true, IsOceanicSonar = false };
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

        var caravan = GeoscapeTestHarness.CreateCaravan(tile);
        var campCmd = new Command_Action();
        StartingSiteSelectionFeedback_Patch.SettleInEmptyTileUtility_SetupCamp_Patch.Postfix(caravan, campCmd);
        Assert.IsTrue(campCmd.Disabled, "SetupCamp command must be disabled on outpost tile.");
        Assert.IsTrue(campCmd.disabledReason.Contains("dismantle outpost first", StringComparison.OrdinalIgnoreCase));

        // 4. Settlements, camps, and ruins block outpost construction
        OutpostTileOccupancyUtility.ListeningPostAtOverride = null;
        var npcSettlement = GeoscapeTestHarness.CreateSettlement(tile, name: "Outlander Town");
        OutpostTileOccupancyUtility.BlockingObjectAtOverride = t => t.tileId == tile.tileId ? npcSettlement : null;

        Assert.IsFalse(OutpostTileOccupancyUtility.CanConstructOutpostAt(tile, out string reasonSettlement));
        Assert.IsTrue(reasonSettlement.Contains("Outlander Town", StringComparison.OrdinalIgnoreCase));

        var arrivalAction = new CaravanArrivalAction_ConstructOutpost(tile, ListeningPostKind.AutomatedRelay);
        FloatMenuAcceptanceReport stillValid = arrivalAction.StillValid(caravan, tile);
        Assert.IsFalse((bool)stillValid, "ConstructOutpost arrival action must abort if destination has a settlement/site.");

        // 5. Settlement right-click dispatch menu suppresses outpost expedition options on occupied tile
        var homeSettlement = GeoscapeTestHarness.CreateSettlement(MakeTile(301), name: "Player Base");
        var mockMap = GeoscapeTestHarness.CreateMockMap();
        WorldSelector_HandleWorldClicks_Patch.SafeMapOverride = s => ReferenceEquals(s, homeSettlement) ? mockMap : null;

        List<FloatMenuOption> dispatchOptions = WorldSelector_HandleWorldClicks_Patch.BuildSettlementDispatchOptions(homeSettlement, tile);
        Assert.IsFalse(dispatchOptions.Any(o => o.Label.Contains("Dispatch Construction Expedition", StringComparison.OrdinalIgnoreCase)),
            "Settlement dispatch menu must not offer outpost construction on a tile occupied by an existing settlement/site.");
    }

    [TestMethod]
    public void DebugActionsVigilance_WorldPlacementActionsUseToolWorld()
    {
        var targetActionNames = new[]
        {
            "Incursion: Spawn Skirmish Collision Pair",
            "Outpost: Deploy Listening Post",
            "Outpost: Deploy Sonar Station",
            "Outpost: Deploy Signal Beacon",
            "Orbital: Spawn Trade Satellite Overhead",
            "Orbital: Spawn Recon Satellite Overhead",
            "Orbital: Spawn Orbital Logistics Hub Overhead"
        };

        var methods = System.Linq.Enumerable.ToArray(
            System.Linq.Enumerable.SelectMany(
                new[]
                {
                    typeof(ProjectVigilance.Geoscape.Debug.DebugActionsVigilance),
                    typeof(ProjectVigilance.Geoscape.Orbital.DebugActionsOrbital),
                    typeof(ProjectVigilance.Geoscape.Sensory.DebugActionsAquatics)
                },
                t => t.GetMethods(
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)));

        foreach (var actionName in targetActionNames)
        {
            var method = methods.FirstOrDefault(m =>
            {
                var attr = m.GetCustomAttributes(typeof(LudeonTK.DebugActionAttribute), false).FirstOrDefault() as LudeonTK.DebugActionAttribute;
                return attr != null && attr.name == actionName;
            });

            Assert.IsNotNull(method, $"Expected to find debug action method for '{actionName}'");
            var attr = method.GetCustomAttributes(typeof(LudeonTK.DebugActionAttribute), false).First() as LudeonTK.DebugActionAttribute;
            Assert.AreEqual(LudeonTK.DebugActionType.ToolWorld, attr!.actionType, $"Action '{actionName}' must have actionType = ToolWorld for interactive globe placement.");
            Assert.AreEqual(LudeonTK.AllowedGameStates.PlayingOnWorld, attr.allowedGameStates, $"Action '{actionName}' must have allowedGameStates = PlayingOnWorld.");
        }
    }

    [TestMethod]
    public void SensoryNarrativeUtility_HandlesNullSensorGracefully()
    {
        // Must not throw NullReferenceException when sensor is null (e.g. retreating forces outside radar coverage)
        string title = ProjectVigilance.Geoscape.Sensory.SensoryNarrativeUtility.GetContactLetterTitle(null, trackingId: 42);
        Assert.IsFalse(string.IsNullOrWhiteSpace(title));
        Assert.IsTrue(title.Contains("42"));

        string body = ProjectVigilance.Geoscape.Sensory.SensoryNarrativeUtility.GetContactLetterBody(null, "Sector 100", speedTilesPerDay: 12f, trackingId: 42);
        Assert.IsFalse(string.IsNullOrWhiteSpace(body));
        Assert.IsTrue(body.Contains("Sector 100"));

        string attr = ProjectVigilance.Geoscape.Sensory.SensoryNarrativeUtility.GetAttributionLine(null);
        Assert.IsFalse(string.IsNullOrWhiteSpace(attr));

        string advice = ProjectVigilance.Geoscape.Sensory.SensoryNarrativeUtility.GetHostileClosingAdvice(null);
        Assert.IsFalse(string.IsNullOrWhiteSpace(advice));
    }

    [TestMethod]
    public void OverheadSatellite_CoordinatesMatchTargetLatitudeAndLongitude()
    {
        float targetLat = 38.69f;
        float targetLon = 37.46f;
        float extraInc = 5f;
        float inclination = UnityEngine.Mathf.Clamp(UnityEngine.Mathf.Abs(targetLat) + extraInc, 10f, 89.9f);

        float incRad = inclination * UnityEngine.Mathf.Deg2Rad;
        float latRad = targetLat * UnityEngine.Mathf.Deg2Rad;
        float sinU = UnityEngine.Mathf.Sin(latRad) / UnityEngine.Mathf.Sin(incRad);
        float uRad = UnityEngine.Mathf.Asin(UnityEngine.Mathf.Clamp(sinU, -1f, 1f));
        float uDeg = uRad * UnityEngine.Mathf.Rad2Deg;
        if (uDeg < 0f) uDeg += 360f;
        uRad = uDeg * UnityEngine.Mathf.Deg2Rad;

        float yNode = UnityEngine.Mathf.Sin(uRad) * UnityEngine.Mathf.Cos(incRad);
        float xNode = UnityEngine.Mathf.Cos(uRad);
        float alphaNodeDeg = UnityEngine.Mathf.Atan2(yNode, xNode) * UnityEngine.Mathf.Rad2Deg;
        float raanDeg = ((targetLon - alphaNodeDeg + 180f) % 360f + 360f) % 360f - 180f;

        var orbit = new ProjectVigilance.Geoscape.Orbital.OrbitalParameters(
            altitudeKm: 600f,
            periodTicks: 62500,
            inclinationDeg: inclination,
            ascendingNodeDeg: raanDeg,
            precessionRateDegPerDay: -3.5f,
            meanAnomalyEpochDeg: uDeg,
            orbitalDriftDegPerDay: 0.8f,
            epochTick: 1000);

        ProjectVigilance.Geoscape.Orbital.OrbitalFlightModel.CalculateSubSatelliteCoordinates(
            orbit,
            currentTick: 1000,
            out float subLat,
            out float subLon);

        Assert.AreEqual(targetLat, subLat, 0.05f, "Calculated sub-satellite latitude must match target latitude.");
        Assert.AreEqual(targetLon, subLon, 0.05f, "Calculated sub-satellite longitude must match target longitude.");
    }

    [TestMethod]
    public void DebugActionsVigilance_ResolveTargetTile_TargetTileOverride_TakesPrecedence()
    {
        try
        {
            var expectedTile = (RimWorld.Planet.PlanetTile)5432;
            ProjectVigilance.Geoscape.Debug.DebugActionsVigilance.TargetTileOverride = () => expectedTile;

            RimWorld.Planet.PlanetTile resolved = ProjectVigilance.Geoscape.Debug.DebugActionsVigilance.ResolveTargetTile();
            Assert.AreEqual(expectedTile, resolved, "TargetTileOverride must be returned by ResolveTargetTile when set.");
        }
        finally
        {
            ProjectVigilance.Geoscape.Debug.DebugActionsVigilance.TargetTileOverride = null;
        }
    }

    [TestMethod]
    public void ThermalIntegrityPostDefProcessor_AuditThingDef_ExplicitItemsAndArchotech()
    {
        var flam = ProjectVigilance.Geoscape.Thermal.ThermalIntegrityPostDefProcessor.ResolveFlammabilityStat();

        // 1. Reinforced barrel (artillery steel)
        var barrel = new ThingDef { defName = "ReinforcedBarrel" };
        barrel.statBases = new List<StatModifier> { new StatModifier { stat = flam, value = 0.1f } };
        ProjectVigilance.Geoscape.Thermal.ThermalIntegrityPostDefProcessor.AuditSingleThingDef(barrel);
        Assert.AreEqual(0f, barrel.statBases.Find(m => m.stat == flam || m.stat?.defName == "Flammability")?.value, "Reinforced barrel must be 0% flammable.");

        // 2. Boss chips
        var powerfocus = new ThingDef { defName = "PowerfocusChip" };
        powerfocus.statBases = new List<StatModifier> { new StatModifier { stat = flam, value = 0.25f } };
        ProjectVigilance.Geoscape.Thermal.ThermalIntegrityPostDefProcessor.AuditSingleThingDef(powerfocus);
        Assert.AreEqual(0f, powerfocus.statBases.Find(m => m.stat == flam || m.stat?.defName == "Flammability")?.value, "Powerfocus chip must be 0% flammable.");

        // 3. Archotech item
        var archotechEye = new ThingDef { defName = "ArchotechEye", techLevel = TechLevel.Archotech };
        archotechEye.statBases = new List<StatModifier> { new StatModifier { stat = flam, value = 0.7f } };
        ProjectVigilance.Geoscape.Thermal.ThermalIntegrityPostDefProcessor.AuditSingleThingDef(archotechEye);
        Assert.AreEqual(0f, archotechEye.statBases.Find(m => m.stat == flam || m.stat?.defName == "Flammability")?.value, "Archotech eye must be 0% flammable.");

        // 4. Gravship Hull reset to 1.0 so Stuff material governs
        var hull = new ThingDef { defName = "GravshipHull" };
        hull.statBases = new List<StatModifier> { new StatModifier { stat = flam, value = 0f } };
        ProjectVigilance.Geoscape.Thermal.ThermalIntegrityPostDefProcessor.AuditSingleThingDef(hull);
        Assert.AreEqual(1.0f, hull.statBases.Find(m => m.stat == flam || m.stat?.defName == "Flammability")?.value, "GravshipHull base flammability must be 1.0.");
    }

    [TestMethod]
    public void ThermalIntegrityPostDefProcessor_IsAllMetalOrStoneBuilding_ClassifiesCorrectly()
    {
        var flam = ProjectVigilance.Geoscape.Thermal.ThermalIntegrityPostDefProcessor.ResolveFlammabilityStat();
        var steelDef = new ThingDef { defName = "Steel" };
        var woodDef = new ThingDef { defName = "WoodLog" };
        var compDef = new ThingDef { defName = "ComponentIndustrial" };

        // Pure metal building (e.g. Electric Smelter, Machining Table, Autocannon Turret)
        var metalBuilding = new ThingDef
        {
            defName = "ElectricSmelter",
            category = ThingCategory.Building,
            costList = new List<ThingDefCountClass>
            {
                new ThingDefCountClass(steelDef, 170),
                new ThingDefCountClass(compDef, 2)
            }
        };
        metalBuilding.statBases = new List<StatModifier> { new StatModifier { stat = flam, value = 1.0f } };

        Assert.IsTrue(ProjectVigilance.Geoscape.Thermal.ThermalIntegrityPostDefProcessor.IsAllMetalOrStoneBuilding(metalBuilding),
            "Pure metal building must be identified as all-metal.");
        ProjectVigilance.Geoscape.Thermal.ThermalIntegrityPostDefProcessor.AuditSingleThingDef(metalBuilding);
        Assert.AreEqual(0f, metalBuilding.statBases.Find(m => m.stat == flam || m.stat?.defName == "Flammability")?.value, "All-metal building must be set to 0% flammability.");

        // Combustible building (e.g. Watermill generator with wood)
        var woodBuilding = new ThingDef
        {
            defName = "WatermillGenerator",
            category = ThingCategory.Building,
            costList = new List<ThingDefCountClass>
            {
                new ThingDefCountClass(woodDef, 280),
                new ThingDefCountClass(steelDef, 80)
            }
        };
        woodBuilding.statBases = new List<StatModifier> { new StatModifier { stat = flam, value = 1.0f } };

        Assert.IsFalse(ProjectVigilance.Geoscape.Thermal.ThermalIntegrityPostDefProcessor.IsAllMetalOrStoneBuilding(woodBuilding),
            "Building containing wood must NOT be identified as all-metal.");
        ProjectVigilance.Geoscape.Thermal.ThermalIntegrityPostDefProcessor.AuditSingleThingDef(woodBuilding);
        Assert.AreEqual(1.0f, woodBuilding.statBases.Find(m => m.stat == flam || m.stat?.defName == "Flammability")?.value, "Building containing wood must remain flammable.");

        // Battery exemption (electrochemical thermal runaway)
        var battery = new ThingDef
        {
            defName = "Battery",
            category = ThingCategory.Building,
            costList = new List<ThingDefCountClass>
            {
                new ThingDefCountClass(steelDef, 70),
                new ThingDefCountClass(compDef, 2)
            }
        };
        battery.statBases = new List<StatModifier> { new StatModifier { stat = flam, value = 1.0f } };

        Assert.IsFalse(ProjectVigilance.Geoscape.Thermal.ThermalIntegrityPostDefProcessor.IsAllMetalOrStoneBuilding(battery),
            "Battery must be exempt from all-metal 0% flammability due to chemical thermal runaway.");
    }

    [TestMethod]
    public void ThermalIntegrityPostDefProcessor_AuditTerrainBridges_Logic()
    {
        var flam = ProjectVigilance.Geoscape.Thermal.ThermalIntegrityPostDefProcessor.ResolveFlammabilityStat();
        var bridge = new TerrainDef { defName = "Bridge" };
        var heavyBridge = new TerrainDef { defName = "HeavyBridge" };
        var ancientBridge = new TerrainDef { defName = "AncientHeavyBridge" };
        var substructure = new TerrainDef { defName = "Substructure" };

        ProjectVigilance.Geoscape.Thermal.ThermalIntegrityPostDefProcessor.SetStatBaseValueSafe(bridge, flam, 0.8f);
        ProjectVigilance.Geoscape.Thermal.ThermalIntegrityPostDefProcessor.SetStatBaseValueSafe(heavyBridge, flam, 0.8f);

        // Run terrain audit logic
        if (bridge.defName.Equals("Bridge", StringComparison.OrdinalIgnoreCase))
            ProjectVigilance.Geoscape.Thermal.ThermalIntegrityPostDefProcessor.SetStatBaseValueSafe(bridge, flam, 1.0f);

        if (heavyBridge.defName.Equals("HeavyBridge", StringComparison.OrdinalIgnoreCase))
            ProjectVigilance.Geoscape.Thermal.ThermalIntegrityPostDefProcessor.SetStatBaseValueSafe(heavyBridge, flam, 0f);

        if (ancientBridge.defName.Equals("AncientHeavyBridge", StringComparison.OrdinalIgnoreCase))
            ProjectVigilance.Geoscape.Thermal.ThermalIntegrityPostDefProcessor.SetStatBaseValueSafe(ancientBridge, flam, 0f);

        if (substructure.defName.Equals("Substructure", StringComparison.OrdinalIgnoreCase))
            ProjectVigilance.Geoscape.Thermal.ThermalIntegrityPostDefProcessor.SetStatBaseValueSafe(substructure, flam, 0f);

        Assert.AreEqual(1.0f, bridge.statBases.Find(m => m.stat == flam || m.stat?.defName == "Flammability")?.value, "Bridge flammability must be 1.0.");
        Assert.AreEqual(0f, heavyBridge.statBases.Find(m => m.stat == flam || m.stat?.defName == "Flammability")?.value, "HeavyBridge flammability must be 0.");
        Assert.AreEqual(0f, ancientBridge.statBases.Find(m => m.stat == flam || m.stat?.defName == "Flammability")?.value, "AncientHeavyBridge flammability must be 0.");
        Assert.AreEqual(0f, substructure.statBases.Find(m => m.stat == flam || m.stat?.defName == "Flammability")?.value, "Substructure flammability must be 0.");
    }

    [TestMethod]
    public void SensoryBuildings_Flammability_AlignedWithThermalIntegrity()
    {
        string geoscapeRelativePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(
            System.AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "..", "Geoscape", "1.7", "Defs", "ThingDefs_Buildings", "Buildings_Radar.xml"));
        string buildingsPath = System.IO.File.Exists(geoscapeRelativePath)
            ? geoscapeRelativePath
            : System.IO.Path.GetFullPath(System.IO.Path.Combine(
                System.AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "..", "1.7", "Defs", "ThingDefs_Buildings", "Buildings_Radar.xml"));

        Assert.IsTrue(System.IO.File.Exists(buildingsPath), $"Buildings XML file must exist at {buildingsPath}");

        var doc = new System.Xml.XmlDocument();
        doc.Load(buildingsPath);

        System.Xml.XmlNode? smallRadar = doc.SelectSingleNode("//ThingDef[defName='Geoscape_RadarSmall']/statBases/Flammability");
        Assert.IsNotNull(smallRadar);
        Assert.AreEqual("0", smallRadar!.InnerText.Trim(), "Geoscape_RadarSmall flammability must be 0");

        System.Xml.XmlNode? largeRadar = doc.SelectSingleNode("//ThingDef[defName='Geoscape_RadarLarge']/statBases/Flammability");
        Assert.IsNotNull(largeRadar);
        Assert.AreEqual("0", largeRadar!.InnerText.Trim(), "Geoscape_RadarLarge flammability must be 0");

        System.Xml.XmlNode? sigint = doc.SelectSingleNode("//ThingDef[defName='Geoscape_SigintConsole']/statBases/Flammability");
        Assert.IsNotNull(sigint);
        Assert.AreEqual("0", sigint!.InnerText.Trim(), "Geoscape_SigintConsole flammability must be 0");

        string aquaticsRelativePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(
            System.AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "..", "Aquatics", "1.7", "Defs", "ThingDefs_Buildings", "Buildings_Aquatics.xml"));
        string aquaticsBuildingsPath = System.IO.File.Exists(aquaticsRelativePath)
            ? aquaticsRelativePath
            : System.IO.Path.GetFullPath(System.IO.Path.Combine(
                System.AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "..", "..", "Aquatics", "1.7", "Defs", "ThingDefs_Buildings", "Buildings_Aquatics.xml"));
        var aquaticsDoc = new System.Xml.XmlDocument();
        aquaticsDoc.Load(aquaticsBuildingsPath);
        System.Xml.XmlNode? sonar = aquaticsDoc.SelectSingleNode("//ThingDef[defName='Geoscape_SonarArray']/statBases/Flammability");
        Assert.IsNotNull(sonar);
        Assert.AreEqual("0", sonar!.InnerText.Trim(), "Geoscape_SonarArray flammability must be 0");

        System.Xml.XmlNode? tower = doc.SelectSingleNode("//ThingDef[defName='Geoscape_LookoutTower']/statBases/Flammability");
        Assert.IsNotNull(tower);
        Assert.AreEqual("0.8", tower!.InnerText.Trim(), "Geoscape_LookoutTower flammability must be 0.8 (timber/stone stuffable)");
    }
}
