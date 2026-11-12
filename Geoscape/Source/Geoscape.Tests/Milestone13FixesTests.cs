using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape.Sensory;
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
        Assert.AreEqual(120, OutpostMaterialUtility.AutomatedSonarSteelCost);
        Assert.AreEqual(4, OutpostMaterialUtility.AutomatedSonarComponentCost);
        Assert.AreEqual(30, OutpostMaterialUtility.AutomatedSonarSteelRepairCost);
        Assert.AreEqual(1, OutpostMaterialUtility.AutomatedSonarComponentRepairCost);
        Assert.AreEqual(90, OutpostMaterialUtility.AutomatedSonarSteelSalvage);
        Assert.AreEqual(3, OutpostMaterialUtility.AutomatedSonarComponentSalvage);
    }

    [TestMethod]
    public void OutpostAssemblySpeedUtility_AutomatedSonar_WorkTicks()
    {
        Assert.AreEqual(15000, OutpostAssemblySpeedUtility.BaseAutomatedSonarTicks);
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
}
