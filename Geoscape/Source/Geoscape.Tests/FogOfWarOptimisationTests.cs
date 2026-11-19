using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape;
using ProjectVigilance.Geoscape.Debug;
using ProjectVigilance.Geoscape.Patches;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld;
using RimWorld.Planet;
using Unity.Collections;
using UnityEngine;
using Verse;

namespace Geoscape.Tests;

[TestClass]
[DoNotParallelize]
public sealed class FogOfWarOptimisationTests : GeoscapeTestBase
{
    private static readonly FieldInfo? WorldDrawLayerPlanetLayerField =
        AccessTools.Field(typeof(WorldDrawLayer), "planetLayer");

    private static readonly FieldInfo? PlanetLayerTilesField =
        AccessTools.Field(typeof(PlanetLayer), "tiles");

    private static readonly FieldInfo? PlanetLayerDrawLayersField =
        AccessTools.Field(typeof(PlanetLayer), "<WorldDrawLayers>k__BackingField");

    private static readonly FieldInfo? PlanetLayerNeighborsOffsetsField =
        AccessTools.Field(typeof(PlanetLayer), "tileIDToNeighbors_offsets");

    private static readonly FieldInfo? PlanetLayerVertsOffsetsField =
        AccessTools.Field(typeof(PlanetLayer), "tileIDToVerts_offsets");

    private static readonly FieldInfo? PlanetLayerVertsField =
        AccessTools.Field(typeof(PlanetLayer), "verts");

    private static PlanetLayer CreateSurfaceWithTiles(int tileCount)
    {
        GeoscapeTestHarness.InitMockWorld(out PlanetLayer surfaceLayer);
        var tiles = new List<Tile>(tileCount);
        var vOffsets = new NativeArray<int>(tileCount, Allocator.Temp);
        var verts = new NativeArray<Vector3>(tileCount * 3, Allocator.Temp);
        for (int i = 0; i < tileCount; i++)
        {
            tiles.Add(new SurfaceTile(new PlanetTile(i, surfaceLayer)));
            vOffsets[i] = i * 3;
            verts[i * 3] = new Vector3(i * 10f, 0f, 100f);
            verts[i * 3 + 1] = new Vector3(i * 10f + 1f, 0f, 100f);
            verts[i * 3 + 2] = new Vector3(i * 10f, 1f, 100f);
        }

        PlanetLayerTilesField!.SetValue(surfaceLayer, tiles);
        PlanetLayerDrawLayersField!.SetValue(surfaceLayer, new List<WorldDrawLayer>());
        PlanetLayerNeighborsOffsetsField!.SetValue(surfaceLayer, new NativeArray<int>(tileCount, Allocator.Temp));
        PlanetLayerVertsOffsetsField!.SetValue(surfaceLayer, vOffsets);
        PlanetLayerVertsField!.SetValue(surfaceLayer, verts);
        return surfaceLayer;
    }

    private static Material CreateMockMaterial() =>
        (Material)RuntimeHelpers.GetUninitializedObject(typeof(Material));

    private static List<Vector3> CreateConvexHexVertices(int tileId)
    {
        var center = new Vector3(tileId * 10f, 0f, 100f);
        Vector3 norm = center.normalized;
        Vector3 up = Math.Abs(norm.y) > 0.95f ? Vector3.right : Vector3.up;
        Vector3 u = Vector3.Cross(norm, up).normalized;
        Vector3 v = Vector3.Cross(norm, u).normalized;

        var verts = new List<Vector3>(6);
        for (int i = 0; i < 6; i++)
        {
            float angle = 2f * Mathf.PI * i / 6f;
            verts.Add(center + (u * Mathf.Cos(angle) + v * Mathf.Sin(angle)) * 2f);
        }

        return verts;
    }

    private static void DrainRegenerate(WorldDrawLayer layer)
    {
        foreach (object _ in layer.Regenerate())
        {
        }
    }

    [TestMethod]
    public void RadarCoverage_ClassicHazeOfWar_EmitsZeroDimmingSubMeshesAndTriangles()
    {
        PlanetLayer surfaceLayer = CreateSurfaceWithTiles(6);
        var sensory = new SensoryManager(Find.World!);
        SensoryManager.Instance = sensory;

        GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;
        GeoscapeMod.Settings.radarOutlineStyle = RadarOutlineStyle.BoundaryAndGrid;
        GeoscapeMod.Settings.showIdentificationHorizon = false;
        GeoscapeMod.Settings.surveillanceDimming = 0.5f;

        Material dimMat = CreateMockMaterial();
        Material shadowMat = CreateMockMaterial();
        Material outlineMat = CreateMockMaterial();
        WorldDrawLayer_RadarCoverage.RenderQueueMaterialOverride = queue =>
            queue == WorldDrawLayer_RadarCoverage.DimmingRenderQueue ? dimMat :
            queue == WorldDrawLayer_RadarCoverage.ShadowWashRenderQueue ? shadowMat :
            outlineMat;
        WorldDrawLayer_RadarCoverage.TileVerticesOverride = (t, list) =>
        {
            list.Clear();
            list.AddRange(CreateConvexHexVertices(t.tileId));
        };
        WorldDrawLayer_RadarCoverage.TileNeighborsOverride = static (_, list) => list.Clear();
        WorldDrawLayer_RadarCoverage.TileCenterOverride = t => new Vector3(t.tileId * 10f, 0f, 100f);

        var sensor = new MockSensorProvider
        {
            SensorTile = new PlanetTile(0, surfaceLayer),
            DetectionRadiusInTiles = 1f,
            IdentificationRadiusInTiles = 0
        };
        SensoryManager.ActiveSensorsOverride = () => new[] { sensor };

        var layer = new WorldDrawLayer_RadarCoverage();
        WorldDrawLayerPlanetLayerField!.SetValue(layer, surfaceLayer);

        DrainRegenerate(layer);

        Assert.AreEqual(0, layer.DimSubMeshCount, "ClassicHazeOfWar must never allocate dimming submeshes.");
        foreach (LayerSubMesh sm in layer.SubMeshes)
        {
            Assert.IsFalse(ReferenceEquals(sm.material, dimMat), "ClassicHazeOfWar must emit no dimming material submeshes.");
        }
    }

    [TestMethod]
    public void RadarCoverage_PlanetarySurveillance_EmitsCornerFansAndRefreshesColorsInPlace()
    {
        PlanetLayer surfaceLayer = CreateSurfaceWithTiles(6);
        var sensory = new SensoryManager(Find.World!);
        SensoryManager.Instance = sensory;

        GeoscapeMod.Settings.visibilityMode = VisibilityMode.PlanetarySurveillance;
        GeoscapeMod.Settings.radarOutlineStyle = RadarOutlineStyle.BoundaryAndGrid;
        GeoscapeMod.Settings.showIdentificationHorizon = false;
        GeoscapeMod.Settings.surveillanceDimming = 0.4f;

        Material dimMat = CreateMockMaterial();
        Material shadowMat = CreateMockMaterial();
        Material outlineMat = CreateMockMaterial();
        WorldDrawLayer_RadarCoverage.RenderQueueMaterialOverride = queue =>
            queue == WorldDrawLayer_RadarCoverage.DimmingRenderQueue ? dimMat :
            queue == WorldDrawLayer_RadarCoverage.ShadowWashRenderQueue ? shadowMat :
            outlineMat;
        WorldDrawLayer_RadarCoverage.TileVerticesOverride = (t, list) =>
        {
            list.Clear();
            list.AddRange(CreateConvexHexVertices(t.tileId));
        };
        WorldDrawLayer_RadarCoverage.TileNeighborsOverride = static (_, list) => list.Clear();
        WorldDrawLayer_RadarCoverage.TileCenterOverride = t => new Vector3(t.tileId * 10f, 0f, 100f);

        var activeTile = new PlanetTile(0, surfaceLayer);
        var sensor = new MockSensorProvider
        {
            SensorTile = activeTile,
            DetectionRadiusInTiles = 1f,
            IdentificationRadiusInTiles = 0
        };
        SensoryManager.ActiveSensorsOverride = () => new[] { sensor };

        var layer = new WorldDrawLayer_RadarCoverage();
        WorldDrawLayerPlanetLayerField!.SetValue(layer, surfaceLayer);

        // 1. Initial build emits 6-vert, 4-tri (12 index), 0-UV corner fans across all 6 tiles.
        DrainRegenerate(layer);

        Assert.AreEqual(1, layer.DimSubMeshCount);
        LayerSubMesh? dimSubMesh = layer.SubMeshes.Find(sm => ReferenceEquals(sm.material, dimMat));
        Assert.IsNotNull(dimSubMesh);
        Assert.AreEqual(6 * 6, dimSubMesh.verts.Count, "Corner-only hex fan must have 6 verts per tile (no center vertex).");
        Assert.AreEqual(6 * 4 * 3, dimSubMesh.tris.Count, "Corner-only hex fan must emit 4 triangles (12 indices) per hex.");
        Assert.AreEqual(0, dimSubMesh.uvs.Count, "Vertex-color dimming fans must omit unused UV allocations.");

        byte expectedAlpha40 = (byte)(Mathf.Clamp01(0.4f) * 255);
        for (int v = 0; v < 6; v++)
        {
            Assert.AreEqual(0, dimSubMesh.colors[v].a, "Active sensor tile 0 must have alpha 0.");
            Assert.AreEqual(expectedAlpha40, dimSubMesh.colors[6 + v].a, "Inactive tile 1 must carry dimming alpha.");
        }

        // 2. Moving active sensor to tile 1 and changing opacity updates colors in-place on the same LayerSubMesh.
        sensor.SensorTile = new PlanetTile(1, surfaceLayer);
        GeoscapeMod.Settings.surveillanceDimming = 0.6f;

        DrainRegenerate(layer);

        Assert.AreEqual(1, layer.DimSubMeshCount);
        LayerSubMesh? reusedDimSubMesh = layer.SubMeshes.Find(sm => ReferenceEquals(sm.material, dimMat));
        Assert.IsNotNull(reusedDimSubMesh);
        Assert.AreSame(dimSubMesh, reusedDimSubMesh, "Subsequent Regenerate must reuse cached dimming LayerSubMesh in-place.");
        Assert.AreEqual(36, reusedDimSubMesh.verts.Count);
        Assert.AreEqual(72, reusedDimSubMesh.tris.Count);
        Assert.AreEqual(0, reusedDimSubMesh.uvs.Count);

        byte expectedAlpha60 = (byte)(Mathf.Clamp01(0.6f) * 255);
        for (int v = 0; v < 6; v++)
        {
            Assert.AreEqual(expectedAlpha60, reusedDimSubMesh.colors[v].a, "Previously active tile 0 must now be dimmed to 60%.");
            Assert.AreEqual(0, reusedDimSubMesh.colors[6 + v].a, "Newly active tile 1 must become transparent.");
        }

        // 3. Switching back to ClassicHazeOfWar removes cached dimming submesh cleanly.
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;
        DrainRegenerate(layer);

        Assert.AreEqual(0, layer.DimSubMeshCount);
        Assert.IsNull(layer.SubMeshes.Find(sm => ReferenceEquals(sm.material, dimMat)));
    }

    [TestMethod]
    public void VisibilityCaches_ZeroSteadyStateAllocationAndInvalidateOnSweepRevision()
    {
        PlanetLayer surfaceLayer = CreateSurfaceWithTiles(16);
        var sensory = new SensoryManager(Find.World!);
        SensoryManager.Instance = sensory;
        SensoryManager.IsExploredOverride = null;
        SensoryManager.IsIdentifiedOverride = null;
        SensoryManager.ActiveSensorsOverride = static () => Array.Empty<ISensorProvider>();
        SensoryManager.TileNeighborsOverride = static (_, list) => list.Clear();

        GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;

        // Initialise bitsets with tile 0 explored so byte arrays exist.
        sensory.MarkTileExplored(new PlanetTile(0, surfaceLayer));

        // 1. Setup landmark list
        var landmarksSource = new List<PlanetTile>
        {
            new(0, surfaceLayer),
            new(3, surfaceLayer)
        };

        // 2. Setup WorldFeature bound to tile 7
        var feature = (WorldFeature)RuntimeHelpers.GetUninitializedObject(typeof(WorldFeature));
        feature.uniqueID = 42;
        feature.layer = surfaceLayer;
        feature.drawCenter = Vector3.zero;
        var tilesList = (List<Tile>)PlanetLayerTilesField!.GetValue(surfaceLayer)!;
        tilesList[7].feature = feature;

        Assert.IsFalse(sensory.IsFeatureExplored(feature));

        // 3. Setup NPC settlement on unexplored & unidentified tile 5
        Faction npcFaction = GeoscapeTestHarness.CreateNpcFaction();
        Settlement settlement = GeoscapeTestHarness.CreateSettlement(new PlanetTile(5, surfaceLayer), npcFaction, id: 7005);

        // 4. Setup NPC settlement on explored but unidentified tile 9
        Settlement maskedSettlement = GeoscapeTestHarness.CreateSettlement(new PlanetTile(9, surfaceLayer), npcFaction, id: 7009);
        sensory.MarkTileExplored(new PlanetTile(9, surfaceLayer));

        // Warm up caches once before allocation measurement
        List<PlanetTile> shown = landmarksSource;
        WorldObject_Visibility_Patch.LandmarksToShow_Patch.Postfix(ref shown);
        Assert.AreEqual(1, shown.Count);
        Assert.AreEqual(0, shown[0].tileId);

        Assert.IsFalse(sensory.IsFeatureExplored(feature));
        Assert.IsTrue(WorldObject_Visibility_Patch.ShouldHideWorldObject(settlement));
        Assert.IsTrue(SensoryManager.ShouldMaskWorldObjectAsUnknown(maskedSettlement));

        // Steady-state measurement: 0 bytes allocated across 100 calls to all 4 cached paths
        bool anyWrong = false;
        long bytesBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++)
        {
            List<PlanetTile> iterShown = landmarksSource;
            WorldObject_Visibility_Patch.LandmarksToShow_Patch.Postfix(ref iterShown);
            if (iterShown.Count != 1) anyWrong = true;
            if (sensory.IsFeatureExplored(feature)) anyWrong = true;
            if (!WorldObject_Visibility_Patch.ShouldHideWorldObject(settlement)) anyWrong = true;
            if (!SensoryManager.ShouldMaskWorldObjectAsUnknown(maskedSettlement)) anyWrong = true;
        }
        long bytesAfter = GC.GetAllocatedBytesForCurrentThread();

        Assert.IsFalse(anyWrong, "Steady-state visibility calls produced incorrect cached values.");
        Assert.AreEqual(0L, bytesAfter - bytesBefore, "Steady-state visibility queries must allocate 0 bytes.");

        // Reveal tiles 3, 5, 7 and identify settlement 7009 -> SweepRevision bumps and all 4 caches invalidate immediately
        sensory.MarkTileExplored(new PlanetTile(3, surfaceLayer));
        sensory.MarkTileExplored(new PlanetTile(7, surfaceLayer));
        sensory.MarkTileExplored(new PlanetTile(5, surfaceLayer));
        sensory.MarkWorldObjectIdentified(maskedSettlement);

        List<PlanetTile> refreshedShown = landmarksSource;
        WorldObject_Visibility_Patch.LandmarksToShow_Patch.Postfix(ref refreshedShown);
        Assert.AreEqual(2, refreshedShown.Count, "LandmarksToShow cache must invalidate when SweepRevision increases.");
        Assert.IsTrue(sensory.IsFeatureExplored(feature), "IsFeatureExplored cache must invalidate when SweepRevision increases.");
        Assert.IsFalse(WorldObject_Visibility_Patch.ShouldHideWorldObject(settlement), "ShouldHideWorldObject cache must invalidate when SweepRevision increases.");
        Assert.IsFalse(SensoryManager.ShouldMaskWorldObjectAsUnknown(maskedSettlement), "ShouldMaskWorldObjectAsUnknown cache must invalidate when SweepRevision increases.");
    }

    [TestMethod]
    public void MarkSurroundingTilesIdentified_IdentifiesTargetsViaTileIndexWithManyWorldObjects()
    {
        PlanetLayer surfaceLayer = CreateSurfaceWithTiles(320);
        var holder = (WorldObjectsHolder)RuntimeHelpers.GetUninitializedObject(typeof(WorldObjectsHolder));
        var worldObjects = new List<WorldObject>(320);
        AccessTools.Field(typeof(WorldObjectsHolder), "worldObjects")!.SetValue(holder, worldObjects);
        Find.World!.worldObjects = holder;

        Faction npcFaction = GeoscapeTestHarness.CreateNpcFaction();
        for (int i = 0; i < 320; i++)
        {
            worldObjects.Add(GeoscapeTestHarness.CreateSettlement(new PlanetTile(i, surfaceLayer), npcFaction, id: 1000 + i));
        }

        var sensory = new SensoryManager(Find.World!);
        SensoryManager.Instance = sensory;
        SensoryManager.TileNeighborsOverride = (tile, list) =>
        {
            list.Clear();
            if (tile.tileId == 5)
            {
                list.Add(new PlanetTile(6, surfaceLayer));
            }
        };

        WorldObject site5 = worldObjects[5];
        WorldObject site12 = worldObjects[12];
        WorldObject site250 = worldObjects[250];
        sensory.MarkWorldObjectUnidentified(site5);
        sensory.MarkWorldObjectUnidentified(site12);
        sensory.MarkWorldObjectUnidentified(site250);

        Assert.IsFalse(sensory.IsWorldObjectIdentified(site5));
        Assert.IsFalse(sensory.IsWorldObjectIdentified(site12));
        Assert.IsFalse(sensory.IsWorldObjectIdentified(site250));

        // 1. Live tile index identifies site5 cleanly without touching site12 or site250.
        sensory.MarkSurroundingTilesIdentified(new PlanetTile(5, surfaceLayer), radiusInTiles: 1);

        Assert.IsTrue(sensory.IsWorldObjectIdentified(site5), "Site 5 must be identified via tile index.");
        Assert.IsFalse(sensory.IsWorldObjectIdentified(site12), "Site 12 outside radius must remain unidentified.");
        Assert.IsFalse(sensory.IsWorldObjectIdentified(site250), "Site 250 outside radius must remain unidentified.");

        // 2. Simulated save-load dirty flag lazily rebuilds tile index and identifies site12 accurately.
        sensory.MarkUnidentifiedTileIndexDirty();
        sensory.MarkSurroundingTilesIdentified(new PlanetTile(12, surfaceLayer), radiusInTiles: 0);

        Assert.IsTrue(sensory.IsWorldObjectIdentified(site12), "Site 12 must be identified after lazy tile-index rebuild.");
        Assert.IsFalse(sensory.IsWorldObjectIdentified(site250), "Site 250 must still remain unidentified.");
    }

    [TestMethod]
    public void UpdateHazeActiveVision_MobileOnly_PreservesStationaryAndSkipsStationaryBfs()
    {
        PlanetLayer surfaceLayer = CreateSurfaceWithTiles(64);
        var sensory = new SensoryManager(Find.World!);
        SensoryManager.Instance = sensory;
        GeoscapeMod.Settings.enableNighttimeSensoryReduction = false;
        SensoryTerrainUtility.VantageBonusOverride = static _ => 0;

        var queriedTileIds = new List<int>(16);
        var graph = new Dictionary<int, int[]>
        {
            [0] = new[] { 1 },
            [1] = new[] { 0, 2 },
            [2] = new[] { 1 },
            [50] = new[] { 51 },
            [51] = new[] { 50, 52 },
            [52] = new[] { 51, 53 },
            [53] = new[] { 52, 54 },
            [54] = new[] { 53 }
        };

        SensoryManager.TileNeighborsOverride = (tile, list) =>
        {
            queriedTileIds.Add(tile.tileId);
            list.Clear();
            if (graph.TryGetValue(tile.tileId, out int[]? neighbors))
            {
                for (int i = 0; i < neighbors.Length; i++)
                {
                    list.Add(new PlanetTile(neighbors[i], surfaceLayer));
                }
            }
        };

        Faction playerFaction = GeoscapeTestHarness.CreatePlayerFaction();
        Settlement colony = GeoscapeTestHarness.CreateSettlement(new PlanetTile(0, surfaceLayer), playerFaction, id: 9001);
        Caravan caravan = GeoscapeTestHarness.CreateCaravan(new PlanetTile(50, surfaceLayer), playerFaction);

        var stationaryColonySensor = new ScoutHorizonSensor(colony, isColony: true);
        var mobileCaravanSensor = new ScoutHorizonSensor(caravan, isColony: false);
        SensoryManager.ActiveSensorsOverride = () => new ISensorProvider[] { stationaryColonySensor, mobileCaravanSensor };

        // 1. Full rebuild populates stationary + mobile caches.
        sensory.UpdateHazeActiveVision(mobileOnly: false);

        Assert.IsTrue(sensory.HasActiveVision(new PlanetTile(0, surfaceLayer)));
        Assert.IsTrue(sensory.HasActiveVision(new PlanetTile(1, surfaceLayer)));
        Assert.IsTrue(sensory.HasActiveVision(new PlanetTile(50, surfaceLayer)));
        Assert.IsTrue(sensory.HasActiveVision(new PlanetTile(51, surfaceLayer)));
        Assert.IsTrue(queriedTileIds.Contains(0), "Initial full update must traverse stationary sensor tile 0.");

        // 2. Move caravan beyond its 2-tile radius and run mobileOnly update: stationary vision stays intact while stationary BFS is skipped.
        queriedTileIds.Clear();
        AccessTools.Field(typeof(WorldObject), "tile")!.SetValue(caravan, new PlanetTile(53, surfaceLayer));

        sensory.UpdateHazeActiveVision(mobileOnly: true);

        Assert.IsFalse(queriedTileIds.Contains(0), "mobileOnly update must skip stationary sensor BFS at tile 0.");
        Assert.IsTrue(queriedTileIds.Contains(53), "mobileOnly update must traverse moved mobile caravan at tile 53.");
        Assert.IsTrue(sensory.HasActiveVision(new PlanetTile(0, surfaceLayer)), "Stationary tile 0 must remain active.");
        Assert.IsTrue(sensory.HasActiveVision(new PlanetTile(1, surfaceLayer)), "Stationary tile 1 must remain active.");
        Assert.IsTrue(sensory.HasActiveVision(new PlanetTile(53, surfaceLayer)), "Moved caravan tile 53 must become active.");
        Assert.IsTrue(sensory.HasActiveVision(new PlanetTile(54, surfaceLayer)), "Moved caravan neighbor tile 54 must become active.");
        Assert.IsFalse(sensory.HasActiveVision(new PlanetTile(50, surfaceLayer)), "Caravan departure tile 50 must leave active vision.");
    }

    [TestMethod]
    public void Phase0Spikes_NullWorldAndMockTerrain_DoNotThrow()
    {
        WorldDrawLayer_HazeOfWar.SubMeshMaterialOverride = CreateMockMaterial;

        // 1. Null Find.World safety: none of the Phase 0 spike methods may throw.
        DebugActionsShroudSpikes.RunAllNonDestructiveSpikes();
        DebugActionsShroudSpikes.RunSpikeS1a_ToggleTerrainFinalized();
        DebugActionsShroudSpikes.RunSpikeS1b_ToggleTerrainDisabled();
        DebugActionsShroudSpikes.ResetSpikeS1_RestoreTerrainSubMeshes();
        DebugActionsShroudSpikes.RunSpikeS2_TerrainMappingAudit();
        DebugActionsShroudSpikes.RunSpikeS3_FlushCompatBenchmark();
        DebugActionsShroudSpikes.RunSpikeS4_GlobeRadiusAndLayerBaseline();

        // 2. Mock terrain layer: S1a -> S1b -> Reset preserves and restores original (finalized, disabled) states.
        PlanetLayer surfaceLayer = CreateSurfaceWithTiles(4);
        var terrainLayer = new WorldDrawLayer_Terrain();
        WorldDrawLayerPlanetLayerField!.SetValue(terrainLayer, surfaceLayer);

        Material mat = CreateMockMaterial();
        var sm0 = new LayerSubMesh(new Mesh(), mat) { finalized = true, disabled = false };
        var sm1 = new LayerSubMesh(new Mesh(), mat) { finalized = false, disabled = true };
        AccessTools.Field(typeof(WorldDrawLayerBase), "subMeshes")!
            .SetValue(terrainLayer, new List<LayerSubMesh> { sm0, sm1 });

        var drawLayers = (List<WorldDrawLayer>)PlanetLayerDrawLayersField!.GetValue(surfaceLayer)!;
        drawLayers.Add(terrainLayer);

        DebugActionsShroudSpikes.RunSpikeS1a_ToggleTerrainFinalized();
        Assert.IsFalse(sm0.finalized);
        Assert.IsFalse(sm0.disabled);
        Assert.IsFalse(sm1.finalized);
        Assert.IsTrue(sm1.disabled);

        DebugActionsShroudSpikes.RunSpikeS1b_ToggleTerrainDisabled();
        Assert.IsTrue(sm0.finalized);
        Assert.IsTrue(sm0.disabled);
        Assert.IsFalse(sm1.finalized);
        Assert.IsTrue(sm1.disabled);

        DebugActionsShroudSpikes.ResetSpikeS1_RestoreTerrainSubMeshes();
        Assert.IsTrue(sm0.finalized);
        Assert.IsFalse(sm0.disabled);
        Assert.IsFalse(sm1.finalized);
        Assert.IsTrue(sm1.disabled);
    }
}
