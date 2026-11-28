using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape;
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
public sealed class ShroudHierarchyTests : GeoscapeTestBase
{
    private static readonly FieldInfo? WorldDrawLayerPlanetLayerField =
        AccessTools.Field(typeof(WorldDrawLayer), "planetLayer");

    private static readonly FieldInfo? WorldDrawLayerSubMeshesField =
        AccessTools.Field(typeof(WorldDrawLayerBase), "subMeshes");

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

    private sealed class GeodesicDualGlobe
    {
        public readonly Vector3[] Centers;
        public readonly List<Vector3>[] Vertices;
        public readonly int[][] Neighbors;
        public int TileCount => Centers.Length;

        public GeodesicDualGlobe(Vector3[] centers, List<Vector3>[] vertices, int[][] neighbors)
        {
            Centers = centers;
            Vertices = vertices;
            Neighbors = neighbors;
        }
    }

    private static GeodesicDualGlobe BuildGeodesicDualGlobe(int subdivisions = 2, float radius = 100f)
    {
        var verts = new List<Vector3>();
        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;

        void AddNorm(float x, float y, float z) => verts.Add(new Vector3(x, y, z).normalized);

        AddNorm(-1f, t, 0f);
        AddNorm(1f, t, 0f);
        AddNorm(-1f, -t, 0f);
        AddNorm(1f, -t, 0f);
        AddNorm(0f, -1f, t);
        AddNorm(0f, 1f, t);
        AddNorm(0f, -1f, -t);
        AddNorm(0f, 1f, -t);
        AddNorm(t, 0f, -1f);
        AddNorm(t, 0f, 1f);
        AddNorm(-t, 0f, -1f);
        AddNorm(-t, 0f, 1f);

        int[] faces =
        {
            0, 11, 5,   0, 5, 1,    0, 1, 7,    0, 7, 10,   0, 10, 11,
            1, 5, 9,    5, 11, 4,   11, 10, 2,  10, 7, 6,   7, 1, 8,
            3, 9, 4,    3, 4, 2,    3, 2, 6,    3, 6, 8,    3, 8, 9,
            4, 9, 5,    2, 4, 11,   6, 2, 10,   8, 6, 7,    9, 8, 1
        };

        var cache = new Dictionary<long, int>();
        int GetMid(int a, int b)
        {
            int min = Math.Min(a, b);
            int max = Math.Max(a, b);
            long key = ((long)min << 32) | (uint)max;
            if (cache.TryGetValue(key, out int idx))
            {
                return idx;
            }

            idx = verts.Count;
            verts.Add((verts[a] + verts[b]).normalized);
            cache[key] = idx;
            return idx;
        }

        for (int s = 0; s < subdivisions; s++)
        {
            cache.Clear();
            var next = new int[faces.Length * 4];
            for (int f = 0; f < faces.Length; f += 3)
            {
                int i0 = faces[f];
                int i1 = faces[f + 1];
                int i2 = faces[f + 2];
                int m01 = GetMid(i0, i1);
                int m12 = GetMid(i1, i2);
                int m20 = GetMid(i2, i0);

                int d = f * 4;
                next[d + 0] = i0;  next[d + 1] = m01; next[d + 2] = m20;
                next[d + 3] = i1;  next[d + 4] = m12; next[d + 5] = m01;
                next[d + 6] = i2;  next[d + 7] = m20; next[d + 8] = m12;
                next[d + 9] = m01; next[d + 10] = m12; next[d + 11] = m20;
            }

            faces = next;
        }

        int tileCount = verts.Count;
        int faceCount = faces.Length / 3;
        var faceCentroids = new Vector3[faceCount];
        var incidentFaces = new List<int>[tileCount];
        var neighborSets = new HashSet<int>[tileCount];
        for (int i = 0; i < tileCount; i++)
        {
            incidentFaces[i] = new List<int>(6);
            neighborSets[i] = new HashSet<int>();
        }

        for (int f = 0; f < faceCount; f++)
        {
            int a = faces[f * 3];
            int b = faces[f * 3 + 1];
            int c = faces[f * 3 + 2];

            faceCentroids[f] = ((verts[a] + verts[b] + verts[c]).normalized) * radius;

            incidentFaces[a].Add(f);
            incidentFaces[b].Add(f);
            incidentFaces[c].Add(f);

            neighborSets[a].Add(b);
            neighborSets[a].Add(c);
            neighborSets[b].Add(a);
            neighborSets[b].Add(c);
            neighborSets[c].Add(a);
            neighborSets[c].Add(b);
        }

        var centers = new Vector3[tileCount];
        var polyVerts = new List<Vector3>[tileCount];
        var neighbors = new int[tileCount][];

        for (int i = 0; i < tileCount; i++)
        {
            Vector3 dir = verts[i];
            centers[i] = dir * radius;

            Vector3 refAxis = Math.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up;
            Vector3 basisU = Vector3.Cross(dir, refAxis).normalized;
            Vector3 basisV = Vector3.Cross(dir, basisU).normalized;

            List<int> inc = incidentFaces[i];
            inc.Sort((f1, f2) =>
            {
                Vector3 d1 = faceCentroids[f1] - centers[i];
                Vector3 d2 = faceCentroids[f2] - centers[i];
                float a1 = Mathf.Atan2(Vector3.Dot(d1, basisV), Vector3.Dot(d1, basisU));
                float a2 = Mathf.Atan2(Vector3.Dot(d2, basisV), Vector3.Dot(d2, basisU));
                return a1.CompareTo(a2);
            });

            var poly = new List<Vector3>(inc.Count);
            for (int k = 0; k < inc.Count; k++)
            {
                poly.Add(faceCentroids[inc[k]]);
            }

            polyVerts[i] = poly;
            neighbors[i] = new List<int>(neighborSets[i]).ToArray();
        }

        return new GeodesicDualGlobe(centers, polyVerts, neighbors);
    }

    private static Material CreateMockMaterial() =>
        (Material)RuntimeHelpers.GetUninitializedObject(typeof(Material));

    private static PlanetLayer CreateSurfaceForGlobe(GeodesicDualGlobe globe)
    {
        GeoscapeTestHarness.InitMockWorld(out PlanetLayer surfaceLayer);
        int count = globe.TileCount;
        var tiles = new List<Tile>(count);
        var vOffsets = new NativeArray<int>(count, Allocator.Temp);
        var verts = new NativeArray<Vector3>(count * 3, Allocator.Temp);

        for (int i = 0; i < count; i++)
        {
            tiles.Add(new SurfaceTile(new PlanetTile(i, surfaceLayer)));
            vOffsets[i] = i * 3;
            List<Vector3> pv = globe.Vertices[i];
            verts[i * 3] = pv[0];
            verts[i * 3 + 1] = pv[1];
            verts[i * 3 + 2] = pv[2];
        }

        PlanetLayerTilesField!.SetValue(surfaceLayer, tiles);
        PlanetLayerDrawLayersField!.SetValue(surfaceLayer, new List<WorldDrawLayer>());
        PlanetLayerNeighborsOffsetsField!.SetValue(surfaceLayer, new NativeArray<int>(count, Allocator.Temp));
        PlanetLayerVertsOffsetsField!.SetValue(surfaceLayer, vOffsets);
        PlanetLayerVertsField!.SetValue(surfaceLayer, verts);

        WorldDrawLayer_HazeOfWar.TileCenterOverride = pt => globe.Centers[pt.tileId];
        WorldDrawLayer_HazeOfWar.TileVerticesOverride = (pt, outVerts) =>
        {
            outVerts.Clear();
            outVerts.AddRange(globe.Vertices[pt.tileId]);
        };
        WorldDrawLayer_HazeOfWar.TileNeighborsOverride = (pt, outNeighbors) =>
        {
            outNeighbors.Clear();
            int[] ns = globe.Neighbors[pt.tileId];
            for (int i = 0; i < ns.Length; i++)
            {
                outNeighbors.Add(new PlanetTile(ns[i], surfaceLayer));
            }
        };

        return surfaceLayer;
    }

    private static void DrainRegenerate(WorldDrawLayer layer)
    {
        foreach (object _ in layer.Regenerate())
        {
        }
    }

    [TestMethod]
    public void IcoSphere_CountsWindingChunkingAndFindOwningFace_AreExact()
    {
        ShroudGeometry.IcoSphere sphere = ShroudGeometry.GetSharedSphere();

        Assert.AreEqual(ShroudGeometry.TotalVertexCount, sphere.VertexCount);
        Assert.AreEqual(10242, sphere.VertexCount);
        Assert.AreEqual(ShroudGeometry.TotalFaceCount, sphere.FaceCount);
        Assert.AreEqual(20480, sphere.FaceCount);
        Assert.AreEqual(ShroudGeometry.ShellChunkCount, sphere.ShellChunks.Length);
        Assert.AreEqual(20, sphere.ShellChunks.Length);

        for (int c = 0; c < ShroudGeometry.ShellChunkCount; c++)
        {
            ShroudGeometry.ShellChunkGeometry chunk = sphere.ShellChunks[c];
            Assert.AreEqual(c, chunk.ChunkIndex);
            Assert.AreEqual(c * 1024, chunk.StartFace);
            Assert.AreEqual((c + 1) * 1024, chunk.EndFaceExclusive);
            Assert.AreEqual(561, chunk.LocalVertexCount);

            for (int f = 0; f < 1024; f++)
            {
                int globalFace = chunk.StartFace + f;
                Assert.AreEqual(c, ShroudGeometry.GetShellChunk(globalFace));
                int fc = ShroudGeometry.GetFineChunk(globalFace);
                Assert.IsTrue(fc >= c * 16 && fc < (c + 1) * 16);
                Assert.IsTrue((uint)chunk.FaceLocalV0[f] < (uint)chunk.LocalVertexCount);
                Assert.IsTrue((uint)chunk.FaceLocalV1[f] < (uint)chunk.LocalVertexCount);
                Assert.IsTrue((uint)chunk.FaceLocalV2[f] < (uint)chunk.LocalVertexCount);
            }
        }

        for (int f = 0; f < sphere.FaceCount; f++)
        {
            Vector3 a = sphere.Vertices[sphere.FaceV0[f]];
            Vector3 b = sphere.Vertices[sphere.FaceV1[f]];
            Vector3 c = sphere.Vertices[sphere.FaceV2[f]];
            Vector3 outward = a + b + c;
            float dot = Vector3.Dot(Vector3.Cross(b - a, c - a), outward);
            Assert.IsTrue(dot > 0f, $"Face {f} has inward/degenerate winding dot={dot}.");
        }

        // Verify FindOwningFace across 2,000 random directions plus vertices, edge midpoints, and centroids
        var rng = new System.Random(1337);
        for (int i = 0; i < 2000; i++)
        {
            Vector3 dir;
            if (i < 200)
            {
                dir = sphere.Vertices[i * 47 % sphere.VertexCount];
            }
            else if (i < 400)
            {
                int f = i * 89 % sphere.FaceCount;
                dir = (sphere.Vertices[sphere.FaceV0[f]] + sphere.Vertices[sphere.FaceV1[f]]).normalized;
            }
            else if (i < 600)
            {
                int f = i * 73 % sphere.FaceCount;
                dir = sphere.FaceCentroids[f];
            }
            else
            {
                dir = new Vector3(
                    (float)(rng.NextDouble() * 2.0 - 1.0),
                    (float)(rng.NextDouble() * 2.0 - 1.0),
                    (float)(rng.NextDouble() * 2.0 - 1.0)).normalized;
            }

            int ownedFace = ShroudGeometry.FindOwningFace(in sphere, dir);
            Assert.IsTrue((uint)ownedFace < (uint)sphere.FaceCount);

            Vector3 a = sphere.Vertices[sphere.FaceV0[ownedFace]];
            Vector3 b = sphere.Vertices[sphere.FaceV1[ownedFace]];
            Vector3 c = sphere.Vertices[sphere.FaceV2[ownedFace]];
            float e0 = Vector3.Dot(Vector3.Cross(a, b), dir);
            float e1 = Vector3.Dot(Vector3.Cross(b, c), dir);
            float e2 = Vector3.Dot(Vector3.Cross(c, a), dir);
            float minMargin = Math.Min(e0, Math.Min(e1, e2));

            Assert.IsTrue(minMargin >= -1e-4f, $"Direction #{i} mapped to face {ownedFace} with negative margin {minMargin}.");
        }

        // Verify zero steady-state allocations
        _ = ShroudGeometry.FindOwningFace(in sphere, Vector3.right);
        long bytesBefore = GC.GetAllocatedBytesForCurrentThread();
        int accum = 0;
        for (int i = 0; i < 1000; i++)
        {
            Vector3 probe = sphere.FaceCentroids[(i * 19) % sphere.FaceCount];
            accum += ShroudGeometry.FindOwningFace(in sphere, probe);
        }

        long bytesAfter = GC.GetAllocatedBytesForCurrentThread();
        Assert.IsTrue(accum >= 0);
        Assert.AreEqual(0L, bytesAfter - bytesBefore, "FindOwningFace must allocate 0 bytes in steady state.");
    }

    [TestMethod]
    public void ShroudHierarchy_OwnAndRingSets_AreDisjointAndInverseConsistent()
    {
        ShroudGeometry.IcoSphere sphere = ShroudGeometry.GetSharedSphere();
        GeodesicDualGlobe globe = BuildGeodesicDualGlobe(subdivisions: 2);

        var hierarchy = new ShroudHierarchy(
            in sphere,
            globe.TileCount,
            t => globe.Centers[t],
            (t, outVerts) =>
            {
                outVerts.Clear();
                outVerts.AddRange(globe.Vertices[t]);
            },
            (t, outNeighbors) =>
            {
                outNeighbors.Clear();
                outNeighbors.AddRange(globe.Neighbors[t]);
            });

        var ownLookup = new HashSet<int>[globe.TileCount];
        for (int t = 0; t < globe.TileCount; t++)
        {
            ownLookup[t] = new HashSet<int>();
            foreach (int f in hierarchy.GetTileFaces(t))
            {
                ownLookup[t].Add(f);
                bool foundBack = false;
                foreach (int owner in hierarchy.GetOwnTiles(f))
                {
                    if (owner == t)
                    {
                        foundBack = true;
                        break;
                    }
                }

                Assert.IsTrue(foundBack, $"Tile {t} has face {f} without inverse own(F) entry.");
            }
        }

        for (int f = 0; f < sphere.FaceCount; f++)
        {
            ReadOnlySpan<int> own = hierarchy.GetOwnTiles(f);
            ReadOnlySpan<int> ring = hierarchy.GetRingTiles(f);

            var ownSet = new HashSet<int>();
            for (int i = 0; i < own.Length; i++)
            {
                int t = own[i];
                Assert.IsTrue(ownSet.Add(t), $"Duplicate tile {t} in own({f}).");
                Assert.IsTrue(ownLookup[t].Contains(f), $"own({f}) contains {t} missing from tileFaces.");
            }

            var ringSet = new HashSet<int>();
            for (int i = 0; i < ring.Length; i++)
            {
                int r = ring[i];
                Assert.IsFalse(ownSet.Contains(r), $"own({f}) and ring({f}) must be disjoint; shared tile {r}.");
                Assert.IsTrue(ringSet.Add(r), $"Duplicate tile {r} in ring({f}).");

                bool adjToOwn = false;
                foreach (int n in globe.Neighbors[r])
                {
                    if (ownSet.Contains(n))
                    {
                        adjToOwn = true;
                        break;
                    }
                }

                Assert.IsTrue(adjToOwn, $"Tile {r} in ring({f}) has no neighbor in own({f}).");
            }
        }
    }

    [TestMethod]
    public void ShroudHierarchy_10kRandomTransitions_MatchBruteForceClassificationAndFrontier()
    {
        ShroudGeometry.IcoSphere sphere = ShroudGeometry.GetSharedSphere();
        GeodesicDualGlobe globe = BuildGeodesicDualGlobe(subdivisions: 2);

        var hierarchy = new ShroudHierarchy(
            in sphere,
            globe.TileCount,
            t => globe.Centers[t],
            (t, outVerts) =>
            {
                outVerts.Clear();
                outVerts.AddRange(globe.Vertices[t]);
            },
            (t, outNeighbors) =>
            {
                outNeighbors.Clear();
                outNeighbors.AddRange(globe.Neighbors[t]);
            });

        byte[] expectedState = new byte[globe.TileCount];
        var rng = new System.Random(20261120);

        for (int op = 0; op < 10000; op++)
        {
            int tileId = rng.Next(globe.TileCount);
            byte newState = (byte)rng.Next(3);
            expectedState[tileId] = newState;
            hierarchy.SetTileState(tileId, newState);

            if ((op % 250) == 0 || op == 9999)
            {
                AssertMatchesBruteForce(hierarchy, expectedState, in sphere, globe);
                hierarchy.ClearDirtyFlags();
            }
        }
    }

    [TestMethod]
    public void ShroudHierarchy_NoHoleAndShroudOnShroudOverlapInvariant()
    {
        ShroudGeometry.IcoSphere sphere = ShroudGeometry.GetSharedSphere();
        GeodesicDualGlobe globe = BuildGeodesicDualGlobe(subdivisions: 2);

        var hierarchy = new ShroudHierarchy(
            in sphere,
            globe.TileCount,
            t => globe.Centers[t],
            (t, outVerts) =>
            {
                outVerts.Clear();
                outVerts.AddRange(globe.Vertices[t]);
            },
            (t, outNeighbors) =>
            {
                outNeighbors.Clear();
                outNeighbors.AddRange(globe.Neighbors[t]);
            });

        // Pattern A: Single explored tile + neighbor clear ring + separate island across chunks
        hierarchy.SetTileState(0, ShroudHierarchy.StateClear);
        foreach (int n in globe.Neighbors[0])
        {
            hierarchy.SetTileState(n, ShroudHierarchy.StateHaze);
        }

        hierarchy.SetTileState(85, ShroudHierarchy.StateClear);
        hierarchy.SetTileState(86, ShroudHierarchy.StateHaze);

        VerifyNoHoleAndOverlapInvariants(hierarchy, globe);

        // Pattern B: Roll back tile 85 to Shroud while 86 remains Haze
        hierarchy.SetTileState(85, ShroudHierarchy.StateShroud);
        VerifyNoHoleAndOverlapInvariants(hierarchy, globe);

        // Pattern C: Revert all back to full Shroud
        for (int t = 0; t < globe.TileCount; t++)
        {
            hierarchy.SetTileState(t, ShroudHierarchy.StateShroud);
        }

        VerifyNoHoleAndOverlapInvariants(hierarchy, globe);
        for (int f = 0; f < sphere.FaceCount; f++)
        {
            Assert.IsTrue(hierarchy.IsFaceCoarse(f), $"Full reset must return face {f} to coarse.");
        }
    }

    [TestMethod]
    public void ShroudHierarchy_SteadyStateTransitions_AllocateZeroBytes()
    {
        ShroudGeometry.IcoSphere sphere = ShroudGeometry.GetSharedSphere();
        GeodesicDualGlobe globe = BuildGeodesicDualGlobe(subdivisions: 2);

        var hierarchy = new ShroudHierarchy(
            in sphere,
            globe.TileCount,
            t => globe.Centers[t],
            (t, outVerts) =>
            {
                outVerts.Clear();
                outVerts.AddRange(globe.Vertices[t]);
            },
            (t, outNeighbors) =>
            {
                outNeighbors.Clear();
                outNeighbors.AddRange(globe.Neighbors[t]);
            });

        // Warm up
        for (int t = 0; t < 5; t++)
        {
            hierarchy.SetTileState(t, ShroudHierarchy.StateClear);
            hierarchy.SetTileState(t, ShroudHierarchy.StateHaze);
            hierarchy.SetTileState(t, ShroudHierarchy.StateShroud);
        }

        hierarchy.ClearDirtyFlags();

        // Measure two consecutive 500-iteration steady-state batches; any one-time tiered-JIT or TLAB
        // adjustment can only affect at most one batch, whereas per-transition allocations hit both.
        long minAllocated = long.MaxValue;
        for (int pass = 0; pass < 2; pass++)
        {
            long bytesBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int iter = 0; iter < 500; iter++)
            {
                int tileId = iter % globe.TileCount;
                hierarchy.SetTileState(tileId, ShroudHierarchy.StateClear);
                hierarchy.SetTileState(tileId, ShroudHierarchy.StateHaze);
                hierarchy.SetTileState(tileId, ShroudHierarchy.StateShroud);
                hierarchy.ClearDirtyFlags();
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - bytesBefore;
            if (allocated < minAllocated)
            {
                minAllocated = allocated;
            }
        }

        Assert.AreEqual(0L, minAllocated, "Steady-state ShroudHierarchy transitions must allocate 0 bytes.");
    }

    [TestMethod]
    public void ShroudAtmosphere_ModesThemesMotionAndWcagContrast()
    {
        Vector3 dir = new Vector3(0.41f, 0.72f, -0.55f).normalized;
        Color32 white = new(255, 255, 255, 255);

        Assert.AreEqual(
            WorldDrawLayer_HazeOfWar.ClearColor,
            ShroudAtmosphere.EvaluateFineTileColor(dir, ShroudHierarchy.StateClear, false, ShroudEffectLevel.Atmospheric, SensoryTheme.TacticalCyan, 42f, false));
        Assert.AreEqual(
            WorldDrawLayer_HazeOfWar.HazeColor,
            ShroudAtmosphere.EvaluateFineTileColor(dir, ShroudHierarchy.StateHaze, false, ShroudEffectLevel.Atmospheric, SensoryTheme.TacticalCyan, 42f, false));
        Assert.AreEqual(
            WorldDrawLayer_HazeOfWar.ShroudColor,
            ShroudAtmosphere.EvaluateShellVertexColor(dir, ShroudEffectLevel.Off, SensoryTheme.TacticalCyan, 42f, false));
        Assert.AreEqual(
            WorldDrawLayer_HazeOfWar.ShroudColor,
            ShroudAtmosphere.EvaluateFineTileColor(dir, ShroudHierarchy.StateShroud, true, ShroudEffectLevel.Off, SensoryTheme.AmberCRT, 42f, false));

        // Reduce motion freezes colours across time
        Color32 frozen0 = ShroudAtmosphere.EvaluateShellVertexColor(dir, ShroudEffectLevel.Atmospheric, SensoryTheme.TacticalCyan, 0f, reduceMotion: true);
        Color32 frozen1 = ShroudAtmosphere.EvaluateShellVertexColor(dir, ShroudEffectLevel.Atmospheric, SensoryTheme.TacticalCyan, 97.5f, reduceMotion: true);
        Assert.AreEqual(frozen0, frozen1, "reduceMotion=true must produce identical vertex colour regardless of timeSeconds.");

        // Non-reduced motion varies across time on at least one direction
        bool observedDrift = false;
        for (int i = 0; i < 8; i++)
        {
            Vector3 probe = new Vector3(i * 0.37f + 0.1f, i * -0.51f + 0.3f, i * 0.73f - 0.2f).normalized;
            Color32 t0 = ShroudAtmosphere.EvaluateShellVertexColor(probe, ShroudEffectLevel.Atmospheric, SensoryTheme.TacticalCyan, 0f, reduceMotion: false);
            Color32 t1 = ShroudAtmosphere.EvaluateShellVertexColor(probe, ShroudEffectLevel.Atmospheric, SensoryTheme.TacticalCyan, 60f, reduceMotion: false);
            if (!t0.Equals(t1))
            {
                observedDrift = true;
                break;
            }
        }

        Assert.IsTrue(observedDrift, "Atmospheric mode without reduceMotion should drift gently over time.");

        // Frontier glow elevates luminance relative to interior shroud
        Color32 interior = ShroudAtmosphere.EvaluateFineTileColor(dir, ShroudHierarchy.StateShroud, isFrontierRing: false, ShroudEffectLevel.Atmospheric, SensoryTheme.TacticalCyan, 10f, reduceMotion: true);
        Color32 frontier = ShroudAtmosphere.EvaluateFineTileColor(dir, ShroudHierarchy.StateShroud, isFrontierRing: true, ShroudEffectLevel.Atmospheric, SensoryTheme.TacticalCyan, 10f, reduceMotion: true);
        Assert.IsTrue(
            ShroudAtmosphere.ComputeRelativeLuminance(frontier) > ShroudAtmosphere.ComputeRelativeLuminance(interior),
            "Frontier ring must glow brighter than deep interior shroud.");

        // WCAG AA > 8:1 against foreground white text across every theme and effect level
        foreach (SensoryTheme theme in Enum.GetValues(typeof(SensoryTheme)))
        {
            foreach (ShroudEffectLevel level in Enum.GetValues(typeof(ShroudEffectLevel)))
            {
                Color32 shellC = ShroudAtmosphere.EvaluateShellVertexColor(dir, level, theme, 15f, reduceMotion: false);
                Color32 frontierC = ShroudAtmosphere.EvaluateFineTileColor(dir, ShroudHierarchy.StateShroud, isFrontierRing: true, level, theme, 15f, reduceMotion: false);

                float shellContrast = ShroudAtmosphere.ComputeContrastRatio(white, shellC);
                float frontierContrast = ShroudAtmosphere.ComputeContrastRatio(white, frontierC);

                Assert.IsTrue(shellContrast >= 8.0f, $"Theme {theme} level {level} shell contrast {shellContrast:F2} < 8.0");
                Assert.IsTrue(frontierContrast >= 8.0f, $"Theme {theme} level {level} frontier contrast {frontierContrast:F2} < 8.0");
            }
        }
    }

    [TestMethod]
    public void EndToEnd_ShroudShellAndLazyHexFrontier_BuildChunksCornerFansZeroUvsAndModeSwitch()
    {
        GeodesicDualGlobe globe = BuildGeodesicDualGlobe(subdivisions: 2);
        PlanetLayer surfaceLayer = CreateSurfaceForGlobe(globe);

        var sensory = new SensoryManager(Find.World!);
        SensoryManager.Instance = sensory;
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.FogOfWar;
        WorldDrawLayer_ShroudShell.TimeSecondsOverride = () => 12f;
        WorldDrawLayer_HazeOfWar.SubMeshMaterialOverride = CreateMockMaterial;
        WorldDrawLayer_ShroudShell.SubMeshMaterialOverride = CreateMockMaterial;

        sensory.MarkTileExplored(new PlanetTile(0, surfaceLayer));
        foreach (int n in globe.Neighbors[0])
        {
            sensory.MarkTileExplored(new PlanetTile(n, surfaceLayer));
        }

        var shellLayer = new WorldDrawLayer_ShroudShell();
        var hazeLayer = new WorldDrawLayer_HazeOfWar();
        WorldDrawLayerPlanetLayerField!.SetValue(shellLayer, surfaceLayer);
        WorldDrawLayerPlanetLayerField!.SetValue(hazeLayer, surfaceLayer);

        var layers = (List<WorldDrawLayer>)PlanetLayerDrawLayersField!.GetValue(surfaceLayer)!;
        layers.Add(shellLayer);
        layers.Add(hazeLayer);

        DrainRegenerate(shellLayer);
        DrainRegenerate(hazeLayer);

        Assert.IsTrue(shellLayer.IsGeometryBuilt);
        Assert.IsTrue(hazeLayer.IsGeometryBuilt);

        var shellSubMeshes = (List<LayerSubMesh>)WorldDrawLayerSubMeshesField!.GetValue(shellLayer)!;
        Assert.AreEqual(20, shellSubMeshes.Count);
        for (int i = 0; i < shellSubMeshes.Count; i++)
        {
            Assert.AreEqual(0, shellSubMeshes[i].uvs.Count, "Tier S shell submeshes must allocate 0 UVs.");
        }

        Assert.IsTrue(shellLayer.TotalShellTriangleCount < ShroudGeometry.TotalFaceCount,
            "Explored cluster must carve faces from Tier S coarse shell.");

        int initialBuiltChunks = hazeLayer.BuiltFineChunkCount;
        int initialVisibleChunks = hazeLayer.VisibleFineChunkCount;
        Assert.IsTrue(initialBuiltChunks > 0 && initialBuiltChunks < ShroudGeometry.FineChunkCount,
            $"Expected lazy Tier F build (built={initialBuiltChunks} out of {ShroudGeometry.FineChunkCount}).");
        Assert.IsTrue(initialVisibleChunks > 0);

        var hazeSubMeshes = (List<LayerSubMesh>)WorldDrawLayerSubMeshesField!.GetValue(hazeLayer)!;
        Assert.AreEqual(initialBuiltChunks, hazeSubMeshes.Count);
        foreach (LayerSubMesh sm in hazeSubMeshes)
        {
            Assert.AreEqual(0, sm.uvs.Count, "Tier F corner-fan submeshes must allocate 0 UVs.");
            Assert.IsTrue(sm.verts.Count > 0 && sm.tris.Count > 0);
        }

        // Background prewarm builds 1 adjacent non-empty chunk without making an all-shroud chunk finalized
        shellLayer.AdvanceFrameWork();
        Assert.AreEqual(initialBuiltChunks + 1, hazeLayer.BuiltFineChunkCount);
        Assert.AreEqual(initialVisibleChunks, hazeLayer.VisibleFineChunkCount);
        Assert.IsTrue(hazeSubMeshes.TrueForAll(static sm => sm.verts.Count > 0),
            "Lazy prewarm must skip empty uninhabited chunks.");

        // Revealing a tile in an untouched distant chunk triggers synchronous lazy chunk creation
        int distantTile = -1;
        for (int t = 0; t < globe.TileCount; t++)
        {
            int chunk = WorldDrawLayer_HazeOfWar.SharedHierarchy!.GetTileFineChunk(t);
            if (WorldDrawLayer_HazeOfWar.SharedHierarchy.GetFineChunkVisibleTileCount(chunk) == 0 &&
                !WorldDrawLayer_HazeOfWar.SharedHierarchy.IsTileFine(t) &&
                chunk != WorldDrawLayer_HazeOfWar.SharedHierarchy.GetTileFineChunk(0))
            {
                distantTile = t;
                break;
            }
        }

        Assert.IsTrue(distantTile >= 0);
        int builtBeforeDistant = hazeLayer.BuiltFineChunkCount;
        hazeLayer.NotifyTileState(distantTile, ShroudHierarchy.StateClear);
        hazeLayer.FlushShroudUpdates();
        Assert.IsTrue(hazeLayer.BuiltFineChunkCount >= builtBeforeDistant);
        Assert.IsTrue(hazeLayer.VisibleFineChunkCount > initialVisibleChunks);

        // Mode switch to Omniscient hides all submeshes cleanly
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.Omniscient;
        WorldDrawLayer_HazeOfWar.InvalidateHierarchySync();
        DrainRegenerate(shellLayer);
        DrainRegenerate(hazeLayer);
        Assert.IsFalse(shellLayer.IsGeometryBuilt);
        Assert.IsFalse(hazeLayer.IsGeometryBuilt);
        Assert.AreEqual(0, shellLayer.ActiveShellChunkCount);
        Assert.AreEqual(0, hazeLayer.VisibleFineChunkCount);

        // Round-trip back to ClassicHazeOfWar restores ShroudShell and Tier F frontier
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.FogOfWar;
        WorldDrawLayer_HazeOfWar.InvalidateHierarchySync();
        DrainRegenerate(shellLayer);
        DrainRegenerate(hazeLayer);
        Assert.IsTrue(shellLayer.IsGeometryBuilt);
        Assert.IsTrue(hazeLayer.IsGeometryBuilt);
        Assert.IsTrue(shellLayer.ActiveShellChunkCount > 0);
        Assert.IsTrue(hazeLayer.VisibleFineChunkCount > 0);
    }

    [TestMethod]
    public void TerrainCullIndex_IncrementalTransitionsAndToggleOff_PreserveOriginalFinalizedState()
    {
        GeodesicDualGlobe globe = BuildGeodesicDualGlobe(subdivisions: 2);
        ShroudGeometry.IcoSphere sphere = ShroudGeometry.GetSharedSphere();
        var hierarchy = new ShroudHierarchy(
            in sphere,
            globe.TileCount,
            t => globe.Centers[t],
            (t, outVerts) =>
            {
                outVerts.Clear();
                outVerts.AddRange(globe.Vertices[t]);
            },
            (t, outNeighbors) =>
            {
                outNeighbors.Clear();
                outNeighbors.AddRange(globe.Neighbors[t]);
            });

        var farTiles = new List<int>(4);
        Vector3 tile0Dir = globe.Centers[0].normalized;
        for (int t = 1; t < globe.TileCount && farTiles.Count < 4; t++)
        {
            if (Vector3.Dot(globe.Centers[t].normalized, tile0Dir) < -0.5f)
            {
                farTiles.Add(t);
            }
        }
        Assert.AreEqual(4, farTiles.Count, "Expected at least 4 antipodal tiles on dual globe.");

        Material mat = CreateMockMaterial();
        var sm0 = new LayerSubMesh(new Mesh(), mat) { finalized = true };
        var sm1 = new LayerSubMesh(new Mesh(), mat) { finalized = true };
        var sm2 = new LayerSubMesh(new Mesh(), mat) { finalized = false };
        sm0.tris.AddRange(new[] { 0, 1, 2, 0, 2, 3 }); // 2 tris -> tile 0, neighbor
        sm1.tris.AddRange(new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4 }); // 3 tris -> far antipodal tiles
        sm2.tris.AddRange(new[] { 0, 1, 2 }); // 1 tri -> far tile (originally unfinalized)

        var subMeshes = new List<LayerSubMesh> { sm0, sm1, sm2 };
        var triToTile = new List<List<int>>
        {
            new() { 0, globe.Neighbors[0][0] },
            new() { farTiles[0], farTiles[1], farTiles[2] },
            new() { farTiles[3] }
        };

        var index = new TerrainCullIndex(globe.TileCount, subMeshes, triToTile);
        index.SyncFromHierarchy(hierarchy);
        index.ApplyToSubMeshes(subMeshes, cullEnabled: true);

        Assert.AreEqual(2, index.CulledSubMeshCount);
        Assert.AreEqual(5, index.CulledTriangleCount);
        Assert.IsFalse(sm0.finalized);
        Assert.IsFalse(sm1.finalized);
        Assert.IsFalse(sm2.finalized);

        // Reveal tile 0 -> sm0 becomes active while sm1 stays culled
        hierarchy.SetTileState(0, ShroudHierarchy.StateClear);
        index.NotifyTilesBecameFine(hierarchy.NewlyFineTileIds, subMeshes, cullEnabled: true);
        Assert.IsTrue(sm0.finalized);
        Assert.IsFalse(sm1.finalized);
        Assert.IsFalse(sm2.finalized);
        Assert.AreEqual(1, index.CulledSubMeshCount);
        Assert.AreEqual(3, index.CulledTriangleCount);

        // Hide tile 0 back to Shroud -> sm0 re-culls
        hierarchy.ClearDirtyFlags();
        hierarchy.SetTileState(0, ShroudHierarchy.StateShroud);
        index.NotifyTilesBecameCoarse(hierarchy.NewlyCoarseTileIds, subMeshes, cullEnabled: true);
        Assert.IsFalse(sm0.finalized);
        Assert.AreEqual(2, index.CulledSubMeshCount);
        Assert.AreEqual(5, index.CulledTriangleCount);

        // Toggling cullEnabled=false restores original finalized state on sm0/sm1 and preserves sm2=false
        index.ApplyToSubMeshes(subMeshes, cullEnabled: false);
        Assert.IsTrue(sm0.finalized);
        Assert.IsTrue(sm1.finalized);
        Assert.IsFalse(sm2.finalized);
    }

    private static void AssertMatchesBruteForce(
        ShroudHierarchy hierarchy,
        byte[] expectedState,
        in ShroudGeometry.IcoSphere sphere,
        GeodesicDualGlobe globe)
    {
        int faceCount = sphere.FaceCount;
        bool[] expectedFaceCoarse = new bool[faceCount];
        int[] expectedShellCoarse = new int[ShroudGeometry.ShellChunkCount];

        for (int f = 0; f < faceCount; f++)
        {
            bool coarse = true;
            foreach (int owner in hierarchy.GetOwnTiles(f))
            {
                if (expectedState[owner] != ShroudHierarchy.StateShroud)
                {
                    coarse = false;
                    break;
                }
            }

            expectedFaceCoarse[f] = coarse;
            Assert.AreEqual(coarse, hierarchy.IsFaceCoarse(f), $"Face {f} coarse mismatch.");
            if (coarse)
            {
                expectedShellCoarse[ShroudGeometry.GetShellChunk(f)]++;
            }
        }

        for (int sc = 0; sc < ShroudGeometry.ShellChunkCount; sc++)
        {
            Assert.AreEqual(expectedShellCoarse[sc], hierarchy.GetShellChunkCoarseCount(sc), $"Shell chunk {sc} coarse count mismatch.");
        }

        bool[] expectedTileFine = new bool[globe.TileCount];
        for (int f = 0; f < faceCount; f++)
        {
            if (expectedFaceCoarse[f])
            {
                continue;
            }

            foreach (int t in hierarchy.GetOwnTiles(f))
            {
                expectedTileFine[t] = true;
            }

            foreach (int t in hierarchy.GetRingTiles(f))
            {
                expectedTileFine[t] = true;
            }
        }

        int[] expectedChunkFine = new int[ShroudGeometry.FineChunkCount];
        int[] expectedChunkVisible = new int[ShroudGeometry.FineChunkCount];

        for (int t = 0; t < globe.TileCount; t++)
        {
            Assert.AreEqual(expectedTileFine[t], hierarchy.IsTileFine(t), $"Tile {t} fine mismatch.");

            bool hasExploredNeighbor = false;
            foreach (int n in globe.Neighbors[t])
            {
                if (expectedState[n] != ShroudHierarchy.StateShroud)
                {
                    hasExploredNeighbor = true;
                    break;
                }
            }

            bool expectedFrontier = expectedState[t] == ShroudHierarchy.StateShroud && hasExploredNeighbor;
            Assert.AreEqual(expectedFrontier, hierarchy.IsFrontierRing(t), $"Tile {t} frontier mismatch.");

            if (expectedTileFine[t])
            {
                int fc = hierarchy.GetTileFineChunk(t);
                expectedChunkFine[fc]++;
                if (expectedState[t] != ShroudHierarchy.StateClear)
                {
                    expectedChunkVisible[fc]++;
                }
            }
        }

        for (int fc = 0; fc < ShroudGeometry.FineChunkCount; fc++)
        {
            Assert.AreEqual(expectedChunkFine[fc], hierarchy.GetFineChunkFineTileCount(fc), $"Fine chunk {fc} fine count mismatch.");
            Assert.AreEqual(expectedChunkVisible[fc], hierarchy.GetFineChunkVisibleTileCount(fc), $"Fine chunk {fc} visible count mismatch.");
        }
    }

    private static void VerifyNoHoleAndOverlapInvariants(ShroudHierarchy hierarchy, GeodesicDualGlobe globe)
    {
        for (int t = 0; t < globe.TileCount; t++)
        {
            byte state = hierarchy.GetTileState(t);
            if (state != ShroudHierarchy.StateShroud)
            {
                Assert.IsTrue(hierarchy.IsTileFine(t), $"Non-shroud tile {t} must be fine.");
                foreach (int f in hierarchy.GetTileFaces(t))
                {
                    Assert.IsFalse(hierarchy.IsFaceCoarse(f), $"Owned face {f} of non-shroud tile {t} must not stay coarse.");
                }
            }

            foreach (int n in globe.Neighbors[t])
            {
                if (state == ShroudHierarchy.StateShroud && hierarchy.GetTileState(n) != ShroudHierarchy.StateShroud)
                {
                    Assert.IsTrue(hierarchy.IsFrontierRing(t), $"Shroud tile {t} bordering explored {n} must be frontier.");
                    Assert.IsTrue(hierarchy.IsTileFine(t), $"Frontier shroud tile {t} must be fine (shroud-on-shroud apron).");
                }
            }
        }

        for (int f = 0; f < hierarchy.FaceCount; f++)
        {
            if (hierarchy.IsFaceCoarse(f))
            {
                continue;
            }

            foreach (int owner in hierarchy.GetOwnTiles(f))
            {
                Assert.IsTrue(hierarchy.IsTileFine(owner), $"Owner {owner} of non-coarse face {f} must be fine.");
            }

            foreach (int ring in hierarchy.GetRingTiles(f))
            {
                Assert.IsTrue(hierarchy.IsTileFine(ring), $"Ring collar tile {ring} of non-coarse face {f} must be fine.");
            }
        }
    }

    [TestMethod]
    public void OrbitZoomVisibility_ShroudAndHazeStayVisibleWhenLayerNotSelected_AndCloudsClampBelowShroud()
    {
        var shellLayer = new WorldDrawLayer_ShroudShell();
        var hazeLayer = new WorldDrawLayer_HazeOfWar();

        Assert.IsTrue(shellLayer.VisibleWhenLayerNotSelected);
        Assert.IsTrue(hazeLayer.VisibleWhenLayerNotSelected);

        const int expectedMaxAllowed = WorldDrawLayer_RadarCoverage.DimmingRenderQueue - 1;
        Assert.AreEqual(expectedMaxAllowed, WorldDrawLayer_Clouds_Patch.ComputeTargetRenderQueue(3550, shroudActive: true));
        Assert.AreEqual(3510, WorldDrawLayer_Clouds_Patch.ComputeTargetRenderQueue(3510, shroudActive: true));
        Assert.AreEqual(3550, WorldDrawLayer_Clouds_Patch.ComputeTargetRenderQueue(3550, shroudActive: false));

        Assert.AreEqual(0f, WorldDrawLayer_Clouds_GetTargetOpacity_Patch.ComputeTargetOpacity(0f, showCloudsOnSurface: false, 0.18f), 0.0001f);
        Assert.AreEqual(1f, WorldDrawLayer_Clouds_GetTargetOpacity_Patch.ComputeTargetOpacity(1f, showCloudsOnSurface: false, 0.18f), 0.0001f);
        Assert.AreEqual(0.18f, WorldDrawLayer_Clouds_GetTargetOpacity_Patch.ComputeTargetOpacity(0f, showCloudsOnSurface: true, 0.18f), 0.0001f);
        Assert.AreEqual(1f, WorldDrawLayer_Clouds_GetTargetOpacity_Patch.ComputeTargetOpacity(1f, showCloudsOnSurface: true, 0.18f), 0.0001f);
        Assert.AreEqual(0.75f, WorldDrawLayer_Clouds_GetTargetOpacity_Patch.ComputeTargetOpacity(0f, showCloudsOnSurface: true, 0.95f), 0.0001f);
    }
}
