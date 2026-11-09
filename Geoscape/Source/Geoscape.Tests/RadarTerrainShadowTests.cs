using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Geoscape.Tests;

[TestClass]
[DoNotParallelize]
public sealed class RadarTerrainShadowTests
{
    private static readonly PlanetLayer MockLayer =
        (PlanetLayer)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(PlanetLayer));

    private static PlanetTile MakeTile(int id) => new PlanetTile(id, MockLayer);

    private sealed class TestSensor : ISensorProvider
    {
        public PlanetTile SensorTile { get; set; } = MakeTile(0);
        public float DetectionRadiusInTiles { get; set; } = 4f;
        public int IdentificationRadiusInTiles { get; set; } = 3;
        public IntelligenceTier MaxIntelligenceTier { get; set; } = IntelligenceTier.Classified;
        public bool PenetratesTerrainMasking { get; set; } = false;
        public int SigintPenetrationRadiusInTiles { get; set; } = 0;
        public bool IsSensorActive => true;
        public bool IsStationary => true;
        public bool IsTechnological => true;
        public bool IsOceanicSonar => false;
        public string SensorLabel => "Test Radar";
        public WorldObject? Source => null;

        public bool TryGetDetectionDistance(IDetectableTarget target, out int distance)
        {
            distance = Math.Abs(SensorTile.tileId - target.CurrentTile.tileId);
            return distance <= (int)DetectionRadiusInTiles;
        }

        public bool CanDetect(IDetectableTarget target) => TryGetDetectionDistance(target, out _);
    }

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

        Assert.IsNotNull(Find.World, "Find.World must not be null.");
        Assert.IsNotNull(Find.WorldGrid, "Find.WorldGrid must not be null.");
    }

    [TestCleanup]
    public void Cleanup()
    {
        SensoryTerrainUtility.TargetMaskedOverride = null;
        SensoryTerrainUtility.VantageBonusOverride = null;
        SensoryManager.TileNeighborsOverride = null;
        SensoryManager.ActiveSensorsOverride = null;
        SensoryManager.Instance = null;
        WorldDrawLayer_RadarCoverage.SubMeshMaterialOverride = null;
        WorldDrawLayer_RadarCoverage.TileVerticesOverride = null;
        WorldDrawLayer_RadarCoverage.TileCenterOverride = null;
        WorldDrawLayer_RadarCoverage.TileNeighborsOverride = null;
        GeoscapeMod.Settings.ResetToDefaults();
        typeof(Current).GetField("creatingWorldInt", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?.SetValue(null, null);
    }

    [TestMethod]
    public void RadarTerrainShadowStyle_EnumValues_MatchExpectedOrderingAndValues()
    {
        var none = (RadarTerrainShadowStyle)0;
        var dashed = (RadarTerrainShadowStyle)1;
        var hatching = (RadarTerrainShadowStyle)2;
        var both = (RadarTerrainShadowStyle)3;

        Assert.AreEqual(RadarTerrainShadowStyle.None, none);
        Assert.AreEqual(RadarTerrainShadowStyle.DashedContours, dashed);
        Assert.AreEqual(RadarTerrainShadowStyle.CellHatching, hatching);
        Assert.AreEqual(RadarTerrainShadowStyle.Both, both);
    }

    [TestMethod]
    public void GeoscapeSettings_RadarTerrainShadowStyle_DefaultAndReset()
    {
        var settings = new GeoscapeSettings();
        Assert.AreEqual(RadarTerrainShadowStyle.Both, settings.radarTerrainShadowStyle,
            "Default radar terrain shadow style must be Both.");

        settings.radarTerrainShadowStyle = RadarTerrainShadowStyle.DashedContours;
        Assert.AreEqual(RadarTerrainShadowStyle.DashedContours, settings.radarTerrainShadowStyle);

        settings.ResetToDefaults();
        Assert.AreEqual(RadarTerrainShadowStyle.Both, settings.radarTerrainShadowStyle,
            "ResetToDefaults must restore radar terrain shadow style to Both.");
    }

    [TestMethod]
    public void PopulateActiveVisionTiles_ContinuousIdentificationEnvelope_IncludesMaskedTiles()
    {
        // Arrange graph: 0 <-> 1 <-> 2 <-> 3 <-> 4
        var graph = new Dictionary<int, List<PlanetTile>>
        {
            [0] = new() { MakeTile(1) },
            [1] = new() { MakeTile(0), MakeTile(2) },
            [2] = new() { MakeTile(1), MakeTile(3) },
            [3] = new() { MakeTile(2), MakeTile(4) },
            [4] = new() { MakeTile(3) }
        };

        SensoryManager.TileNeighborsOverride = (tile, list) =>
        {
            if (graph.TryGetValue(tile.tileId, out var neighbors))
            {
                list.AddRange(neighbors);
            }
        };

        var sensor = new TestSensor
        {
            SensorTile = MakeTile(0),
            DetectionRadiusInTiles = 4f,
            IdentificationRadiusInTiles = 3,
            PenetratesTerrainMasking = false
        };

        SensoryManager.ActiveSensorsOverride = () => new[] { sensor };

        // Tiles 2 and 3 are rugged Mountainous tiles (masked)
        SensoryTerrainUtility.TargetMaskedOverride = t => t.tileId == 2 || t.tileId == 3;

        var sensory = new SensoryManager(null!);
        var activeTiles = new HashSet<PlanetTile>();
        var idTiles = new HashSet<PlanetTile>();
        var shadowTiles = new HashSet<PlanetTile>();

        // Act
        sensory.PopulateActiveVisionTiles(activeTiles, idTiles, null, shadowTiles);

        // Assert: All tiles within idRadius = 3 must be in idTiles, even if masked
        Assert.IsTrue(idTiles.Contains(MakeTile(0)), "Tile 0 must be identified.");
        Assert.IsTrue(idTiles.Contains(MakeTile(1)), "Tile 1 must be identified.");
        Assert.IsTrue(idTiles.Contains(MakeTile(2)), "Tile 2 (masked) must be in identified envelope to keep perimeter continuous.");
        Assert.IsTrue(idTiles.Contains(MakeTile(3)), "Tile 3 (masked) must be in identified envelope to keep perimeter continuous.");
        Assert.IsFalse(idTiles.Contains(MakeTile(4)), "Tile 4 (distance 4 > 3) must NOT be identified.");

        // Mountain-masked shadow tiles must isolate internal masked hexes inside R_id (distance > 1)
        Assert.IsFalse(shadowTiles.Contains(MakeTile(0)), "Center tile is not masked shadow.");
        Assert.IsFalse(shadowTiles.Contains(MakeTile(1)), "Tile 1 (unmasked) is not shadow.");
        Assert.IsTrue(shadowTiles.Contains(MakeTile(2)), "Tile 2 (masked, step 2 > 1) must be in shadow tiles.");
        Assert.IsTrue(shadowTiles.Contains(MakeTile(3)), "Tile 3 (masked, step 3 > 1) must be in shadow tiles.");
        Assert.IsFalse(shadowTiles.Contains(MakeTile(4)), "Tile 4 (outside R_id) must NOT be in shadow tiles.");
    }

    [TestMethod]
    public void PopulateActiveVisionTiles_SensorPenetratesMasking_EliminatesShadowTiles()
    {
        // Arrange graph: 0 <-> 1 <-> 2 <-> 3
        var graph = new Dictionary<int, List<PlanetTile>>
        {
            [0] = new() { MakeTile(1) },
            [1] = new() { MakeTile(0), MakeTile(2) },
            [2] = new() { MakeTile(1), MakeTile(3) },
            [3] = new() { MakeTile(2) }
        };

        SensoryManager.TileNeighborsOverride = (tile, list) =>
        {
            if (graph.TryGetValue(tile.tileId, out var neighbors))
            {
                list.AddRange(neighbors);
            }
        };

        // Sensor with SIGINT / penetration capability
        var sensor = new TestSensor
        {
            SensorTile = MakeTile(0),
            DetectionRadiusInTiles = 3f,
            IdentificationRadiusInTiles = 3,
            PenetratesTerrainMasking = true
        };

        SensoryManager.ActiveSensorsOverride = () => new[] { sensor };
        SensoryTerrainUtility.TargetMaskedOverride = t => t.tileId == 2 || t.tileId == 3;

        var sensory = new SensoryManager(null!);
        var activeTiles = new HashSet<PlanetTile>();
        var idTiles = new HashSet<PlanetTile>();
        var shadowTiles = new HashSet<PlanetTile>();

        // Act
        sensory.PopulateActiveVisionTiles(activeTiles, idTiles, null, shadowTiles);

        // Assert: SIGINT resolves all identification, eliminating all terrain shadows
        Assert.AreEqual(4, idTiles.Count, "All 4 tiles must be identified.");
        Assert.AreEqual(0, shadowTiles.Count, "Penetrating sensor must produce zero shadow tiles.");
    }

    [TestMethod]
    public void PopulateActiveVisionTiles_MultiSensorOverlap_PenetrationClearsShadow()
    {
        // Sensor A covers 0..3 without penetration
        // Sensor B is stationed at tile 10 with penetration, reaching tile 2 at step 2 but not reaching tile 3
        var graph = new Dictionary<int, List<PlanetTile>>
        {
            [0] = new() { MakeTile(1) },
            [10] = new() { MakeTile(1) },
            [1] = new() { MakeTile(0), MakeTile(10), MakeTile(2) },
            [2] = new() { MakeTile(1), MakeTile(3) },
            [3] = new() { MakeTile(2) }
        };

        SensoryManager.TileNeighborsOverride = (tile, list) =>
        {
            if (graph.TryGetValue(tile.tileId, out var neighbors))
            {
                list.AddRange(neighbors);
            }
        };

        var sensorA = new TestSensor
        {
            SensorTile = MakeTile(0),
            DetectionRadiusInTiles = 3f,
            IdentificationRadiusInTiles = 3,
            PenetratesTerrainMasking = false
        };

        var sensorB = new TestSensor
        {
            SensorTile = MakeTile(10),
            DetectionRadiusInTiles = 2f,
            IdentificationRadiusInTiles = 2,
            PenetratesTerrainMasking = true
        };

        SensoryManager.ActiveSensorsOverride = () => new[] { sensorA, sensorB };
        SensoryTerrainUtility.TargetMaskedOverride = t => t.tileId == 2 || t.tileId == 3;

        var sensory = new SensoryManager(null!);
        var activeTiles = new HashSet<PlanetTile>();
        var idTiles = new HashSet<PlanetTile>();
        var shadowTiles = new HashSet<PlanetTile>();

        // Act
        sensory.PopulateActiveVisionTiles(activeTiles, idTiles, null, shadowTiles);

        // Tile 2 is illuminated by sensor B (penetration at step 2), so it is cleared.
        // Tile 3 is covered only by sensor A (no penetration), so it remains in shadow.
        Assert.IsFalse(shadowTiles.Contains(MakeTile(2)), "Tile 2 penetrated by Sensor B must not be in shadow.");
        Assert.IsTrue(shadowTiles.Contains(MakeTile(3)), "Tile 3 without penetration must remain in shadow.");
        Assert.AreEqual(1, shadowTiles.Count);
    }

    [TestMethod]
    public void PopulateActiveVisionTiles_DifferentLayerInstances_IdentifiesAndPenetratesByTileId()
    {
        // Sensor A uses Layer1; Sensor B uses Layer2 (or null layer)
        var mockLayer2 = (PlanetLayer)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(PlanetLayer));
        PlanetTile TileLayer2(int id) => new PlanetTile(id, mockLayer2);

        var graph = new Dictionary<int, List<PlanetTile>>
        {
            [0] = new() { MakeTile(1) },
            [1] = new() { MakeTile(0), MakeTile(2) },
            [2] = new() { MakeTile(1) }
        };

        SensoryManager.TileNeighborsOverride = (tile, list) =>
        {
            if (graph.TryGetValue(tile.tileId, out var neighbors))
            {
                list.AddRange(neighbors);
            }
        };

        var sensorA = new TestSensor
        {
            SensorTile = MakeTile(0),
            DetectionRadiusInTiles = 3f,
            IdentificationRadiusInTiles = 3,
            PenetratesTerrainMasking = false
        };

        var sensorB = new TestSensor
        {
            SensorTile = TileLayer2(0),
            DetectionRadiusInTiles = 3f,
            IdentificationRadiusInTiles = 3,
            PenetratesTerrainMasking = true
        };

        SensoryManager.ActiveSensorsOverride = () => new[] { sensorA, sensorB };
        SensoryTerrainUtility.TargetMaskedOverride = t => t.tileId == 2;

        var sensory = new SensoryManager(null!);
        var activeTiles = new HashSet<PlanetTile>();
        var idTiles = new HashSet<PlanetTile>();
        var shadowTiles = new HashSet<PlanetTile>();

        // Act
        sensory.PopulateActiveVisionTiles(activeTiles, idTiles, null, shadowTiles);

        // Assert: Even if PlanetTile instances have different PlanetLayer references,
        // tileId-based resolution ensures penetrating Sensor B clears shadow for tile 2.
        Assert.AreEqual(0, shadowTiles.Count, "Penetrating sensor must clear shadow across any layer reference.");
    }

    [TestMethod]
    public void PopulateActiveVisionTiles_CloseSightlineDistanceOne_NotShadowed()
    {
        // Tile 1 is Mountainous and immediately adjacent to the radar station (step 1)
        var graph = new Dictionary<int, List<PlanetTile>>
        {
            [0] = new() { MakeTile(1) },
            [1] = new() { MakeTile(0) }
        };

        SensoryManager.TileNeighborsOverride = (tile, list) =>
        {
            if (graph.TryGetValue(tile.tileId, out var neighbors))
            {
                list.AddRange(neighbors);
            }
        };

        var sensor = new TestSensor
        {
            SensorTile = MakeTile(0),
            DetectionRadiusInTiles = 2f,
            IdentificationRadiusInTiles = 2,
            PenetratesTerrainMasking = false
        };

        SensoryManager.ActiveSensorsOverride = () => new[] { sensor };
        SensoryTerrainUtility.TargetMaskedOverride = t => t.tileId == 1;

        var sensory = new SensoryManager(null!);
        var activeTiles = new HashSet<PlanetTile>();
        var idTiles = new HashSet<PlanetTile>();
        var shadowTiles = new HashSet<PlanetTile>();

        sensory.PopulateActiveVisionTiles(activeTiles, idTiles, null, shadowTiles);

        Assert.IsTrue(idTiles.Contains(MakeTile(1)), "Tile 1 is in ID horizon.");
        Assert.IsFalse(shadowTiles.Contains(MakeTile(1)), "Immediate adjacent hex (step 1) close sightline cannot be concealed by mountain masking.");
    }

    [TestMethod]
    public void PopulateActiveVisionTiles_NullOptionalOutputs_DoesNotThrow()
    {
        var graph = new Dictionary<int, List<PlanetTile>>
        {
            [0] = new() { MakeTile(1) },
            [1] = new() { MakeTile(0) }
        };

        SensoryManager.TileNeighborsOverride = (tile, list) =>
        {
            if (graph.TryGetValue(tile.tileId, out var neighbors))
            {
                list.AddRange(neighbors);
            }
        };

        var sensor = new TestSensor { SensorTile = MakeTile(0) };
        SensoryManager.ActiveSensorsOverride = () => new[] { sensor };

        var sensory = new SensoryManager(null!);
        var activeTiles = new HashSet<PlanetTile>();

        sensory.PopulateActiveVisionTiles(activeTiles, null, null, null);

        Assert.AreEqual(2, activeTiles.Count);
    }

    private static List<Vector3> MakeConvexPolygon(Vector3 center, float radius, int sides)
    {
        Vector3 norm = center.normalized;
        Vector3 up = Math.Abs(norm.y) > 0.95f ? Vector3.right : Vector3.up;
        Vector3 u = Vector3.Cross(norm, up).normalized;
        Vector3 v = Vector3.Cross(norm, u).normalized;

        var verts = new List<Vector3>(sides);
        for (int i = 0; i < sides; i++)
        {
            float angle = 2f * Mathf.PI * i / sides;
            Vector3 pt = center + (u * Mathf.Cos(angle) + v * Mathf.Sin(angle)) * radius;
            verts.Add(pt);
        }
        return verts;
    }

    [TestMethod]
    public void RenderShadowCellHatching_HexagonalTile_GeneratesWashAndFiveScanlineRibbons()
    {
        WorldDrawLayer_RadarCoverage.SubMeshMaterialOverride = () => null;
        var layer = new WorldDrawLayer_RadarCoverage();
        int subMeshIndex = 0;
        var center = new Vector3(100f, 0f, 0f);
        var hexVerts = MakeConvexPolygon(center, 5f, 6);

        WorldDrawLayer_RadarCoverage.TileVerticesOverride = (tile, list) => list.AddRange(hexVerts);
        WorldDrawLayer_RadarCoverage.TileCenterOverride = tile => center;

        var hatchColor = new Color32(255, 185, 36, 180);
        var washColor = new Color32(28, 20, 8, 80);

        layer.RenderShadowCellHatching(null!, MakeTile(2), hatchColor, washColor, ref subMeshIndex);

        Assert.AreEqual(1, layer.SubMeshes.Count);
        var subMesh = layer.SubMeshes[0];

        // 7 wash vertices (1 center + 6 corners) + 5 scanlines * 4 quad vertices = 27 vertices
        Assert.AreEqual(27, subMesh.verts.Count);
        // 6 wash triangles * 3 = 18 indices + 5 scanlines * 2 triangles * 3 = 30 indices = 48 indices
        Assert.AreEqual(48, subMesh.tris.Count);

        for (int i = 0; i < subMesh.verts.Count; i++)
        {
            Vector3 v = subMesh.verts[i];
            Assert.IsFalse(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z), "Vertices must not contain NaN.");
            Assert.IsFalse(float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z), "Vertices must not contain Infinity.");
        }

        // Verify wash color on center vertex
        Assert.AreEqual(washColor, subMesh.colors[0]);
        // Verify hatch color on first scanline vertex
        Assert.AreEqual(hatchColor, subMesh.colors[7]);
    }

    [TestMethod]
    public void RenderShadowCellHatching_PentagonalTile_GeneratesWashAndScanlines()
    {
        WorldDrawLayer_RadarCoverage.SubMeshMaterialOverride = () => null;
        var layer = new WorldDrawLayer_RadarCoverage();
        int subMeshIndex = 0;
        var center = new Vector3(0f, 0f, 100f);
        var pentagonVerts = MakeConvexPolygon(center, 5f, 5);

        WorldDrawLayer_RadarCoverage.TileVerticesOverride = (tile, list) => list.AddRange(pentagonVerts);
        WorldDrawLayer_RadarCoverage.TileCenterOverride = tile => center;

        var hatchColor = new Color32(255, 185, 36, 180);
        var washColor = new Color32(28, 20, 8, 80);

        layer.RenderShadowCellHatching(null!, MakeTile(5), hatchColor, washColor, ref subMeshIndex);

        var subMesh = layer.SubMeshes[0];
        // 6 wash vertices (1 center + 5 corners) + 5 scanlines * 4 quad vertices = 26 vertices
        Assert.AreEqual(26, subMesh.verts.Count);
        // 5 wash triangles * 3 = 15 indices + 5 scanlines * 6 = 30 indices = 45 indices
        Assert.AreEqual(45, subMesh.tris.Count);

        for (int i = 0; i < subMesh.tris.Count; i++)
        {
            Assert.IsTrue(subMesh.tris[i] < subMesh.verts.Count, "Triangle index must point to a valid vertex.");
        }
    }

    [TestMethod]
    public void RenderShadowCellHatching_Poles_CalculatesStableOrthonormalFramesWithoutNaN()
    {
        WorldDrawLayer_RadarCoverage.SubMeshMaterialOverride = () => null;
        var layer = new WorldDrawLayer_RadarCoverage();
        int subMeshIndex = 0;

        // North Pole (normCenter.y == 1.0f)
        var northPole = new Vector3(0f, 100f, 0f);
        var northVerts = MakeConvexPolygon(northPole, 5f, 6);
        WorldDrawLayer_RadarCoverage.TileVerticesOverride = (tile, list) => list.AddRange(northVerts);
        WorldDrawLayer_RadarCoverage.TileCenterOverride = tile => northPole;

        layer.RenderShadowCellHatching(null!, MakeTile(99), new Color32(255, 185, 36, 255), new Color32(28, 20, 8, 100), ref subMeshIndex);
        var northSubMesh = layer.SubMeshes[0];
        Assert.AreEqual(27, northSubMesh.verts.Count);
        for (int i = 0; i < northSubMesh.verts.Count; i++)
        {
            Assert.IsFalse(float.IsNaN(northSubMesh.verts[i].x) || float.IsNaN(northSubMesh.verts[i].y) || float.IsNaN(northSubMesh.verts[i].z));
        }

        // South Pole (normCenter.y == -1.0f)
        int southSubMeshIdx = 0;
        var southLayer = new WorldDrawLayer_RadarCoverage();
        var southPole = new Vector3(0f, -100f, 0f);
        var southVerts = MakeConvexPolygon(southPole, 5f, 6);
        WorldDrawLayer_RadarCoverage.TileVerticesOverride = (tile, list) => list.AddRange(southVerts);
        WorldDrawLayer_RadarCoverage.TileCenterOverride = tile => southPole;

        southLayer.RenderShadowCellHatching(null!, MakeTile(100), new Color32(255, 185, 36, 255), new Color32(28, 20, 8, 100), ref southSubMeshIdx);
        var southSubMesh = southLayer.SubMeshes[0];
        Assert.AreEqual(27, southSubMesh.verts.Count);
        for (int i = 0; i < southSubMesh.verts.Count; i++)
        {
            Assert.IsFalse(float.IsNaN(southSubMesh.verts[i].x) || float.IsNaN(southSubMesh.verts[i].y) || float.IsNaN(southSubMesh.verts[i].z));
        }
    }

    [TestMethod]
    public void RenderShadowCellHatching_DegeneratePolygon_DoesNotAllocate()
    {
        WorldDrawLayer_RadarCoverage.SubMeshMaterialOverride = () => null;
        var layer = new WorldDrawLayer_RadarCoverage();
        int subMeshIndex = 0;

        WorldDrawLayer_RadarCoverage.TileVerticesOverride = (tile, list) =>
        {
            list.Add(new Vector3(100f, 0f, 0f));
            list.Add(new Vector3(101f, 0f, 0f));
        };
        WorldDrawLayer_RadarCoverage.TileCenterOverride = tile => new Vector3(100f, 0f, 0f);

        layer.RenderShadowCellHatching(null!, MakeTile(1), new Color32(255, 185, 36, 255), new Color32(28, 20, 8, 100), ref subMeshIndex);

        Assert.AreEqual(0, layer.SubMeshes.Count);
    }

    [TestMethod]
    public void AddRibbonQuad_ConsistentWinding_FacesOutward()
    {
        WorldDrawLayer_RadarCoverage.SubMeshMaterialOverride = () => null;
        var layer = new WorldDrawLayer_RadarCoverage();
        int subMeshIndex = 0;

        var p0 = new Vector3(0f, 0f, 100f);
        var p1 = new Vector3(1f, 0f, 100f);
        var p2 = new Vector3(1f, 1f, 100f);
        var p3 = new Vector3(0f, 1f, 100f);
        var normal = new Vector3(0f, 0f, 1f);

        layer.AddRibbonQuad(ref subMeshIndex, p0, p1, p2, p3, new Color32(255, 255, 255, 255), normal);

        var subMesh = layer.SubMeshes[0];
        Assert.AreEqual(4, subMesh.verts.Count);
        Assert.AreEqual(6, subMesh.tris.Count);

        // Verify outward winding dot product
        Vector3 v0 = subMesh.verts[subMesh.tris[0]];
        Vector3 v1 = subMesh.verts[subMesh.tris[1]];
        Vector3 v2 = subMesh.verts[subMesh.tris[2]];
        Vector3 triNorm = Vector3.Cross(v1 - v0, v2 - v0);
        Assert.IsTrue(Vector3.Dot(triNorm, normal) > 0f, "Ribbon triangles must face along outward normal.");
    }

    [TestMethod]
    public void WorldDrawLayer_RadarCoverage_Regenerate_AllShadowStyles_BehavioralMatrix()
    {
        WorldDrawLayer_RadarCoverage.SubMeshMaterialOverride = () => null;

        // Arrange 3-tile chain: Tile 0 (sensor center) <-> Tile 1 (plain) <-> Tile 2 (masked mountain shadow) <-> Tile 3 (unidentified)
        var graph = new Dictionary<int, List<PlanetTile>>
        {
            [0] = new() { MakeTile(1) },
            [1] = new() { MakeTile(0), MakeTile(2) },
            [2] = new() { MakeTile(1), MakeTile(3) },
            [3] = new() { MakeTile(2) }
        };

        SensoryManager.TileNeighborsOverride = (tile, list) =>
        {
            if (graph.TryGetValue(tile.tileId, out var neighbors)) list.AddRange(neighbors);
        };
        WorldDrawLayer_RadarCoverage.TileNeighborsOverride = (tile, list) =>
        {
            if (graph.TryGetValue(tile.tileId, out var neighbors)) list.AddRange(neighbors);
        };

        var centers = new Dictionary<int, Vector3>
        {
            [0] = new Vector3(100f, 0f, 0f),
            [1] = new Vector3(100f, 5f, 0f),
            [2] = new Vector3(100f, 10f, 0f),
            [3] = new Vector3(100f, 15f, 0f)
        };

        WorldDrawLayer_RadarCoverage.TileCenterOverride = tile => centers.TryGetValue(tile.tileId, out var c) ? c : new Vector3(100f, 0f, 0f);
        WorldDrawLayer_RadarCoverage.TileVerticesOverride = (tile, list) =>
        {
            Vector3 c = centers.TryGetValue(tile.tileId, out var ctr) ? ctr : new Vector3(100f, 0f, 0f);
            list.AddRange(MakeConvexPolygon(c, 2.5f, 6));
        };

        var sensor = new TestSensor
        {
            SensorTile = MakeTile(0),
            DetectionRadiusInTiles = 3f,
            IdentificationRadiusInTiles = 2,
            PenetratesTerrainMasking = false
        };
        SensoryManager.ActiveSensorsOverride = () => new[] { sensor };
        SensoryTerrainUtility.TargetMaskedOverride = t => t.tileId == 2;

        var sensory = new SensoryManager(null!);
        SensoryManager.Instance = sensory;

        // Act 1: Style = Both
        GeoscapeMod.Settings.radarOutlineStyle = RadarOutlineStyle.BoundaryAndGrid;
        GeoscapeMod.Settings.showIdentificationHorizon = true;
        GeoscapeMod.Settings.radarTerrainShadowStyle = RadarTerrainShadowStyle.Both;
        GeoscapeMod.Settings.surveillanceDimming = 0f;

        var layerBoth = new WorldDrawLayer_RadarCoverage();
        foreach (var _ in layerBoth.Regenerate()) { }

        Assert.IsTrue(layerBoth.SubMeshes.Count > 0, "Regenerate must populate submeshes.");
        bool hasWashBoth = false;
        bool hasHatchBoth = false;
        foreach (var sm in layerBoth.SubMeshes)
        {
            foreach (var col in sm.colors)
            {
                if (col.r == 28 && col.g == 20 && col.b == 8) hasWashBoth = true;
                if (col.r == 255 && col.g == 185 && col.b == 36 && col.a == (byte)(Mathf.Clamp01(GeoscapeMod.Settings.identificationRingOpacity * 0.45f) * 255)) hasHatchBoth = true;
            }
        }
        Assert.IsTrue(hasWashBoth, "Approach Both must contain static wash.");
        Assert.IsTrue(hasHatchBoth, "Approach Both must contain diagonal scanline hatching.");

        // Act 2: Style = DashedContours
        GeoscapeMod.Settings.radarTerrainShadowStyle = RadarTerrainShadowStyle.DashedContours;
        var layerDashed = new WorldDrawLayer_RadarCoverage();
        foreach (var _ in layerDashed.Regenerate()) { }

        bool hasWashDashed = false;
        bool hasHatchDashed = false;
        foreach (var sm in layerDashed.SubMeshes)
        {
            foreach (var col in sm.colors)
            {
                if (col.r == 28 && col.g == 20 && col.b == 8) hasWashDashed = true;
                if (col.r == 255 && col.g == 185 && col.b == 36 && col.a == (byte)(Mathf.Clamp01(GeoscapeMod.Settings.identificationRingOpacity * 0.45f) * 255)) hasHatchDashed = true;
            }
        }
        Assert.IsFalse(hasWashDashed, "DashedContours must NOT contain static wash.");
        Assert.IsFalse(hasHatchDashed, "DashedContours must NOT contain scanlines.");

        // Act 3: Style = CellHatching
        GeoscapeMod.Settings.radarTerrainShadowStyle = RadarTerrainShadowStyle.CellHatching;
        var layerHatch = new WorldDrawLayer_RadarCoverage();
        foreach (var _ in layerHatch.Regenerate()) { }

        bool hasWashHatch = false;
        bool hasHatchHatch = false;
        foreach (var sm in layerHatch.SubMeshes)
        {
            foreach (var col in sm.colors)
            {
                if (col.r == 28 && col.g == 20 && col.b == 8) hasWashHatch = true;
                if (col.r == 255 && col.g == 185 && col.b == 36 && col.a == (byte)(Mathf.Clamp01(GeoscapeMod.Settings.identificationRingOpacity * 0.45f) * 255)) hasHatchHatch = true;
            }
        }
        Assert.IsTrue(hasWashHatch, "CellHatching must contain static wash.");
        Assert.IsTrue(hasHatchHatch, "CellHatching must contain scanlines.");

        // Act 4: Style = None
        GeoscapeMod.Settings.radarTerrainShadowStyle = RadarTerrainShadowStyle.None;
        var layerNone = new WorldDrawLayer_RadarCoverage();
        foreach (var _ in layerNone.Regenerate()) { }

        bool hasWashNone = false;
        bool hasHatchNone = false;
        foreach (var sm in layerNone.SubMeshes)
        {
            foreach (var col in sm.colors)
            {
                if (col.r == 28 && col.g == 20 && col.b == 8) hasWashNone = true;
                if (col.r == 255 && col.g == 185 && col.b == 36 && col.a == (byte)(Mathf.Clamp01(GeoscapeMod.Settings.identificationRingOpacity * 0.45f) * 255)) hasHatchNone = true;
            }
        }
        Assert.IsFalse(hasWashNone, "Style None must NOT contain static wash.");
        Assert.IsFalse(hasHatchNone, "Style None must NOT contain scanlines.");

        // Act 5: showIdentificationHorizon = false suppresses all shadow decorations
        GeoscapeMod.Settings.showIdentificationHorizon = false;
        GeoscapeMod.Settings.radarTerrainShadowStyle = RadarTerrainShadowStyle.Both;
        var layerNoId = new WorldDrawLayer_RadarCoverage();
        foreach (var _ in layerNoId.Regenerate()) { }

        bool hasWashNoId = false;
        bool hasHatchNoId = false;
        foreach (var sm in layerNoId.SubMeshes)
        {
            foreach (var col in sm.colors)
            {
                if (col.r == 28 && col.g == 20 && col.b == 8) hasWashNoId = true;
                if (col.r == 255 && col.g == 185 && col.b == 36 && col.a == (byte)(Mathf.Clamp01(GeoscapeMod.Settings.identificationRingOpacity * 0.45f) * 255)) hasHatchNoId = true;
            }
        }
        Assert.IsFalse(hasWashNoId, "showIdentificationHorizon = false must suppress static wash.");
        Assert.IsFalse(hasHatchNoId, "showIdentificationHorizon = false must suppress scanlines.");
    }

    [TestMethod]
    public void WorldDrawLayer_RadarCoverage_MountainOnBoundary_OuterEdgeSolid_InnerEdgeDashed()
    {
        WorldDrawLayer_RadarCoverage.SubMeshMaterialOverride = () => null;

        // Tile 0 (center) <-> Tile 1 (unshadowed identified) <-> Tile 2 (masked shadow on R_id) <-> Tile 3 (unidentified)
        var graph = new Dictionary<int, List<PlanetTile>>
        {
            [0] = new() { MakeTile(1) },
            [1] = new() { MakeTile(0), MakeTile(2) },
            [2] = new() { MakeTile(1), MakeTile(3) },
            [3] = new() { MakeTile(2) }
        };

        SensoryManager.TileNeighborsOverride = (tile, list) =>
        {
            if (graph.TryGetValue(tile.tileId, out var neighbors)) list.AddRange(neighbors);
        };
        WorldDrawLayer_RadarCoverage.TileNeighborsOverride = (tile, list) =>
        {
            if (graph.TryGetValue(tile.tileId, out var neighbors)) list.AddRange(neighbors);
        };

        var centers = new Dictionary<int, Vector3>
        {
            [0] = new Vector3(100f, 0f, 0f),
            [1] = new Vector3(100f, 5f, 0f),
            [2] = new Vector3(100f, 10f, 0f),
            [3] = new Vector3(100f, 15f, 0f)
        };

        WorldDrawLayer_RadarCoverage.TileCenterOverride = tile => centers.TryGetValue(tile.tileId, out var c) ? c : new Vector3(100f, 0f, 0f);
        WorldDrawLayer_RadarCoverage.TileVerticesOverride = (tile, list) =>
        {
            Vector3 c = centers.TryGetValue(tile.tileId, out var ctr) ? ctr : new Vector3(100f, 0f, 0f);
            list.AddRange(MakeConvexPolygon(c, 2.5f, 6));
        };

        var sensor = new TestSensor
        {
            SensorTile = MakeTile(0),
            DetectionRadiusInTiles = 3f,
            IdentificationRadiusInTiles = 2,
            PenetratesTerrainMasking = false
        };
        SensoryManager.ActiveSensorsOverride = () => new[] { sensor };
        SensoryTerrainUtility.TargetMaskedOverride = t => t.tileId == 2;

        var sensory = new SensoryManager(null!);
        SensoryManager.Instance = sensory;

        GeoscapeMod.Settings.radarOutlineStyle = RadarOutlineStyle.BoundaryOnly; // Only boundary & ID ribbons, no interior grid
        GeoscapeMod.Settings.showIdentificationHorizon = true;
        GeoscapeMod.Settings.radarTerrainShadowStyle = RadarTerrainShadowStyle.DashedContours;
        GeoscapeMod.Settings.surveillanceDimming = 0f;

        var layer = new WorldDrawLayer_RadarCoverage();
        foreach (var _ in layer.Regenerate()) { }

        Assert.IsTrue(layer.SubMeshes.Count > 0);
        int totalVerts = 0;
        foreach (var sm in layer.SubMeshes)
        {
            totalVerts += sm.verts.Count;
        }

        // We have outer perimeter quads, 1 solid ID horizon quad (4 verts), and 3 dashed quads (12 verts)
        Assert.IsTrue(totalVerts >= 16, "Must generate outer perimeter, solid ID horizon, and dashed contour ribbons.");
    }

    [TestMethod]
    public void PopulateActiveVisionTiles_PartialSigintPenetration_ClearsShadowInsideBandwidthAndRetainsBeyond()
    {
        var graph = new Dictionary<int, List<PlanetTile>>
        {
            [0] = new() { MakeTile(1) },
            [1] = new() { MakeTile(0), MakeTile(2) },
            [2] = new() { MakeTile(1), MakeTile(3) },
            [3] = new() { MakeTile(2) }
        };
        SensoryManager.TileNeighborsOverride = (tile, list) =>
        {
            if (graph.TryGetValue(tile.tileId, out var neighbors)) list.AddRange(neighbors);
        };

        var sensor = new TestSensor
        {
            SensorTile = MakeTile(0),
            DetectionRadiusInTiles = 4f,
            IdentificationRadiusInTiles = 3,
            PenetratesTerrainMasking = true,
            SigintPenetrationRadiusInTiles = 2 // Penetrates up to step 2, but step 3 remains shadowed
        };
        SensoryManager.ActiveSensorsOverride = () => new[] { sensor };

        // Tiles 2 and 3 are rugged mountain valleys
        SensoryTerrainUtility.TargetMaskedOverride = t => t.tileId == 2 || t.tileId == 3;

        var sensory = new SensoryManager(null!);
        SensoryManager.Instance = sensory;

        var outTiles = new HashSet<PlanetTile>();
        var outIdentifiedTiles = new HashSet<PlanetTile>();
        var outShadowTiles = new HashSet<PlanetTile>();

        sensory.PopulateActiveVisionTiles(
            outTiles,
            outIdentifiedTiles,
            outTileDistancesFromPerimeter: null,
            outMaskedShadowTiles: outShadowTiles);

        // Tile 2 is at step 2 (within 2-ring SIGINT radius) -> must NOT be shadowed
        Assert.IsFalse(outShadowTiles.Any(t => t.tileId == 2),
            "Tile 2 at step 2 must be penetrated by the 2-ring SIGINT correlator.");

        // Tile 3 is at step 3 (beyond 2-ring SIGINT radius) -> must BE shadowed
        Assert.IsTrue(outShadowTiles.Any(t => t.tileId == 3),
            "Tile 3 at step 3 must retain mountain shadow masking beyond the 2-ring SIGINT correlator.");
    }
}
