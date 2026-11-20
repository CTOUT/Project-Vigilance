using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using HarmonyLib;
using LudeonTK;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Debug;

/// <summary>
/// Developer Mode Phase 0 runtime diagnostics for Waylith / RimWorld 1.7 validation prior to
/// implementing the Stage 2 hierarchical shroud shell, hex frontier, and terrain cull.
/// </summary>
public static class DebugActionsShroudSpikes
{
    private static readonly FieldInfo? SubMeshesField =
        AccessTools.Field(typeof(WorldDrawLayerBase), "subMeshes");

    private static readonly FieldInfo? TriangleIndexToTileIdField =
        AccessTools.Field(typeof(WorldDrawLayer), "triangleIndexToTileID");

    private static readonly FieldInfo? MeshCollidersInOrderField =
        AccessTools.Field(typeof(WorldDrawLayer), "meshCollidersInOrder");

    private static readonly Dictionary<LayerSubMesh, (bool finalized, bool disabled)> savedTerrainStates = new();

    internal static void ClearSpikeStateForTesting() => savedTerrainStates.Clear();

    private static void NotifyToast(string text, MessageTypeDef? type = null)
    {
        if (Current.ProgramState != ProgramState.Playing || Find.World == null)
        {
            return;
        }

        try
        {
            Messages.Message(text, type ?? MessageTypeDefOf.NeutralEvent, false);
        }
        catch (Exception)
        {
            // Headless unit test stubs do not initialize UI message sound/window state.
        }
    }

    private static bool TryGetSurfaceTerrain(
        out PlanetLayer? surface,
        out WorldDrawLayer_Terrain? terrain,
        out List<LayerSubMesh>? subMeshes)
    {
        surface = Find.World?.grid?.Surface;
        if (surface == null)
        {
            Log.Message("[PV Phase 0] Aborted: active world surface PlanetLayer is null.");
            NotifyToast("[PV Phase 0] Open a planet world first.", MessageTypeDefOf.RejectInput);
            terrain = null;
            subMeshes = null;
            return false;
        }

        terrain = surface.GetLayer<WorldDrawLayer_Terrain>();
        if (terrain == null)
        {
            Log.Message("[PV Phase 0] Aborted: WorldDrawLayer_Terrain not found on surface layer.");
            NotifyToast("[PV Phase 0] WorldDrawLayer_Terrain not found on surface.", MessageTypeDefOf.RejectInput);
            subMeshes = null;
            return false;
        }

        subMeshes = SubMeshesField?.GetValue(terrain) as List<LayerSubMesh>;
        if (subMeshes == null)
        {
            Log.Message("[PV Phase 0] Aborted: WorldDrawLayerBase.subMeshes field unavailable.");
            return false;
        }

        return true;
    }

    private static void SaveTerrainStatesIfUnrecorded(List<LayerSubMesh> subMeshes)
    {
        for (int i = 0; i < subMeshes.Count; i++)
        {
            LayerSubMesh sm = subMeshes[i];
            if (sm != null && !savedTerrainStates.ContainsKey(sm))
            {
                savedTerrainStates[sm] = (sm.finalized, sm.disabled);
            }
        }
    }

    private static void SummarizeLayerMeshes(
        List<LayerSubMesh>? subMeshes,
        out int subMeshCount,
        out int verts,
        out int tris,
        out int uvs,
        out int finalizedCount)
    {
        subMeshCount = subMeshes?.Count ?? 0;
        verts = 0;
        tris = 0;
        uvs = 0;
        finalizedCount = 0;
        if (subMeshes == null)
        {
            return;
        }

        for (int i = 0; i < subMeshes.Count; i++)
        {
            LayerSubMesh sm = subMeshes[i];
            if (sm == null)
            {
                continue;
            }

            verts += sm.verts.Count;
            tris += sm.tris.Count / 3;
            uvs += sm.uvs.Count;
            if (sm.finalized)
            {
                finalizedCount++;
            }
        }
    }

    [DebugAction("Project Vigilance", "Spike: Run All Non-Destructive (S2 + S3 + S4)", allowedGameStates = AllowedGameStates.PlayingOnWorld)]
    public static void RunAllNonDestructiveSpikes()
    {
        Log.Message("[PV Phase 0] ===== Beginning Non-Destructive Spikes (S2, S3, S4) =====");
        RunSpikeS2_TerrainMappingAudit();
        RunSpikeS3_FlushCompatBenchmark();
        RunSpikeS4_GlobeRadiusAndLayerBaseline();
        Log.Message("[PV Phase 0] ===== Completed Non-Destructive Spikes =====");
        NotifyToast("[PV Phase 0] S2 + S3 + S4 complete. Inspect Player.log / Dev Console.", MessageTypeDefOf.PositiveEvent);
    }

    [DebugAction("Project Vigilance", "Spike: S1a Hide Terrain (finalized=false)", allowedGameStates = AllowedGameStates.PlayingOnWorld)]
    public static void RunSpikeS1a_ToggleTerrainFinalized()
    {
        if (!TryGetSurfaceTerrain(out _, out _, out List<LayerSubMesh>? subMeshes) || subMeshes == null)
        {
            return;
        }

        SaveTerrainStatesIfUnrecorded(subMeshes);
        for (int i = 0; i < subMeshes.Count; i++)
        {
            if (subMeshes[i] != null)
            {
                subMeshes[i].finalized = false;
            }
        }

        Log.Message($"[PV Spike S1a] Set finalized=false on {subMeshes.Count} terrain submeshes (disabled unchanged). Confirm globe terrain disappears while hover/right-click tile picking still resolves; then run 'Spike: S1 Reset Terrain SubMeshes'.");
        NotifyToast($"[PV Spike S1a] Hid {subMeshes.Count} terrain submeshes via finalized=false.", MessageTypeDefOf.NeutralEvent);
    }

    [DebugAction("Project Vigilance", "Spike: S1b Test Terrain (disabled=true)", allowedGameStates = AllowedGameStates.PlayingOnWorld)]
    public static void RunSpikeS1b_ToggleTerrainDisabled()
    {
        if (!TryGetSurfaceTerrain(out _, out _, out List<LayerSubMesh>? subMeshes) || subMeshes == null)
        {
            return;
        }

        SaveTerrainStatesIfUnrecorded(subMeshes);
        for (int i = 0; i < subMeshes.Count; i++)
        {
            LayerSubMesh sm = subMeshes[i];
            if (sm == null)
            {
                continue;
            }

            bool baselineFinalized = savedTerrainStates.TryGetValue(sm, out (bool finalized, bool disabled) orig)
                ? orig.finalized
                : sm.finalized;
            sm.finalized = baselineFinalized;
            sm.disabled = true;
        }

        Log.Message($"[PV Spike S1b] Set disabled=true (finalized=baseline) on {subMeshes.Count} terrain submeshes. Observe whether terrain remains visible (confirming WorldDrawLayerBase.Render ignores disabled and requires finalized=false).");
        NotifyToast($"[PV Spike S1b] Set disabled=true on {subMeshes.Count} terrain submeshes.", MessageTypeDefOf.NeutralEvent);
    }

    [DebugAction("Project Vigilance", "Spike: S1 Reset Terrain SubMeshes", allowedGameStates = AllowedGameStates.PlayingOnWorld)]
    public static void ResetSpikeS1_RestoreTerrainSubMeshes()
    {
        if (!TryGetSurfaceTerrain(out _, out _, out List<LayerSubMesh>? subMeshes) || subMeshes == null)
        {
            return;
        }

        int restored = 0;
        for (int i = 0; i < subMeshes.Count; i++)
        {
            LayerSubMesh sm = subMeshes[i];
            if (sm == null)
            {
                continue;
            }

            if (savedTerrainStates.TryGetValue(sm, out (bool finalized, bool disabled) orig))
            {
                sm.finalized = orig.finalized;
                sm.disabled = orig.disabled;
            }
            else
            {
                sm.finalized = sm.tris.Count > 0;
                sm.disabled = false;
            }
            restored++;
        }

        savedTerrainStates.Clear();
        Log.Message($"[PV Spike S1] Restored original finalized/disabled flags on {restored} terrain submeshes.");
        NotifyToast($"[PV Spike S1] Restored {restored} terrain submeshes.", MessageTypeDefOf.PositiveEvent);
    }

    [DebugAction("Project Vigilance", "Spike: S2 Terrain SubMesh-to-Tile Audit", allowedGameStates = AllowedGameStates.PlayingOnWorld)]
    public static void RunSpikeS2_TerrainMappingAudit()
    {
        if (!TryGetSurfaceTerrain(out PlanetLayer? surface, out WorldDrawLayer_Terrain? terrain, out List<LayerSubMesh>? subMeshes) ||
            surface == null ||
            terrain == null ||
            subMeshes == null)
        {
            return;
        }

        var colliders = MeshCollidersInOrderField?.GetValue(terrain) as List<MeshCollider>;
        var triToTile = TriangleIndexToTileIdField?.GetValue(terrain) as List<List<int>>;

        int tilesCount = surface.TilesCount;
        int smCount = subMeshes.Count;
        int colCount = colliders?.Count ?? -1;
        int triMapCount = triToTile?.Count ?? -1;

        if (triToTile == null)
        {
            Log.Warning($"[PV Spike S2] triangleIndexToTileID is null! subMeshes={smCount}, colliders={colCount}");
            return;
        }

        bool[] tileCovered = new bool[Math.Max(1, tilesCount)];
        int coveredUniqueTiles = 0;
        int alignedSubMeshes = 0;
        int mismatchedSubMeshes = 0;
        int totalTerrainTris = 0;

        SensoryManager? sensory = SensoryManager.Instance;
        bool[]? exploredMask = null;
        bool[]? frontierMask = null;
        if (sensory != null && tilesCount > 0)
        {
            exploredMask = new bool[tilesCount];
            frontierMask = new bool[tilesCount];
            var tmpNeighbors = new List<PlanetTile>(8);
            for (int t = 0; t < tilesCount; t++)
            {
                if (!sensory.IsTileIdExplored(t))
                {
                    continue;
                }

                exploredMask[t] = true;
                frontierMask[t] = true;
                tmpNeighbors.Clear();
                surface.GetTileNeighbors(new PlanetTile(t, surface), tmpNeighbors);
                for (int n = 0; n < tmpNeighbors.Count; n++)
                {
                    int nId = tmpNeighbors[n].tileId;
                    if (nId >= 0 && nId < tilesCount)
                    {
                        frontierMask[nId] = true;
                    }
                }
            }
        }

        int unexploredSubMeshes = 0;
        int unexploredTris = 0;
        int cullableSubMeshes = 0;
        int cullableTris = 0;

        var distinctTiles = new HashSet<int>();
        var distinctDirs = new List<Vector3>(256);
        var perSubMeshStats = new List<(int idx, int tris, int tiles, float spreadDeg, bool hasExplored, bool hasFrontier)>(smCount);
        WorldGrid? grid = Find.World?.grid;

        int limit = Math.Min(smCount, triToTile.Count);
        for (int i = 0; i < limit; i++)
        {
            LayerSubMesh sm = subMeshes[i];
            List<int> triTiles = triToTile[i];
            int smTris = (sm?.tris.Count ?? 0) / 3;
            int mappedTris = triTiles?.Count ?? 0;
            totalTerrainTris += smTris;

            if (smTris == mappedTris)
            {
                alignedSubMeshes++;
            }
            else
            {
                mismatchedSubMeshes++;
            }

            distinctTiles.Clear();
            distinctDirs.Clear();
            bool hasExplored = false;
            bool hasFrontier = false;
            Vector3 dirSum = Vector3.zero;

            if (triTiles != null)
            {
                for (int k = 0; k < triTiles.Count; k++)
                {
                    int tid = triTiles[k];
                    if (tid < 0 || tid >= tilesCount)
                    {
                        continue;
                    }

                    if (distinctTiles.Add(tid))
                    {
                        if (!tileCovered[tid])
                        {
                            tileCovered[tid] = true;
                            coveredUniqueTiles++;
                        }

                        if (exploredMask != null && exploredMask[tid])
                        {
                            hasExplored = true;
                        }
                        if (frontierMask != null && frontierMask[tid])
                        {
                            hasFrontier = true;
                        }

                        if (grid != null)
                        {
                            Vector3 dir = grid.GetTileCenter(new PlanetTile(tid, surface)).normalized;
                            distinctDirs.Add(dir);
                            dirSum += dir;
                        }
                    }
                }
            }

            if (!hasExplored)
            {
                unexploredSubMeshes++;
                unexploredTris += smTris;
            }
            if (!hasFrontier)
            {
                cullableSubMeshes++;
                cullableTris += smTris;
            }

            float spreadDeg = 0f;
            if (distinctDirs.Count > 1 && dirSum.sqrMagnitude > 1e-6f)
            {
                Vector3 meanDir = dirSum.normalized;
                float minDot = 1f;
                for (int d = 0; d < distinctDirs.Count; d++)
                {
                    float dot = Vector3.Dot(meanDir, distinctDirs[d]);
                    if (dot < minDot)
                    {
                        minDot = dot;
                    }
                }
                spreadDeg = Mathf.Acos(Mathf.Clamp(minDot, -1f, 1f)) * Mathf.Rad2Deg * 2f;
            }

            perSubMeshStats.Add((i, smTris, distinctTiles.Count, spreadDeg, hasExplored, hasFrontier));
        }

        perSubMeshStats.Sort(static (a, b) => b.tris.CompareTo(a.tris));

        var sb = new StringBuilder();
        sb.AppendLine($"[PV Spike S2] WorldTiles={tilesCount}, SubMeshes={smCount}, Colliders={colCount}, TriMapLists={triMapCount}, Aligned={alignedSubMeshes}/{limit}, Mismatched={mismatchedSubMeshes}, CoveredTiles={coveredUniqueTiles}/{tilesCount}, TotalTerrainTris={totalTerrainTris:N0}.");
        if (exploredMask != null)
        {
            double cullPct = totalTerrainTris > 0 ? (cullableTris * 100.0) / totalTerrainTris : 0.0;
            sb.AppendLine($"[PV Spike S2] Tier T Cull Yield (current exploration): UnexploredSubMeshes={unexploredSubMeshes}/{limit} ({unexploredTris:N0} tris), FullyShroudedSubMeshes(no explored/1-ring)={cullableSubMeshes}/{limit} ({cullableTris:N0} tris, {cullPct:F1}%).");
        }

        int topCount = Math.Min(5, perSubMeshStats.Count);
        for (int t = 0; t < topCount; t++)
        {
            var entry = perSubMeshStats[t];
            sb.AppendLine($"  Top#{t + 1} SubMesh[{entry.idx}]: tris={entry.tris:N0}, distinctTiles={entry.tiles:N0}, angularSpread={entry.spreadDeg:F1}deg, hasExplored={entry.hasExplored}, hasFrontier={entry.hasFrontier}");
        }

        Log.Message(sb.ToString().TrimEnd());
    }

    private static void BenchmarkColorUpload(
        Material? material,
        int vertCount,
        int triCount,
        int iterations,
        out double minMs,
        out double medianMs,
        out double p95Ms,
        out double maxMs,
        out double meanMs,
        out double meanAllocBytes)
    {
        var sm = new LayerSubMesh(new Mesh(), material!);
        for (int v = 0; v < vertCount; v++)
        {
            float ang = (2f * Mathf.PI * v) / Math.Max(1, vertCount);
            sm.verts.Add(new Vector3(Mathf.Cos(ang) * 100f, Mathf.Sin(ang) * 100f, 0f));
            sm.colors.Add(new Color32(10, 12, 18, 255));
        }

        for (int t = 0; t < triCount; t++)
        {
            int a = t % vertCount;
            int b = (t + 1) % vertCount;
            int c = (t + 2) % vertCount;
            sm.tris.Add(a);
            sm.tris.Add(b);
            sm.tris.Add(c);
        }

        try
        {
            sm.FinalizeMesh(MeshParts.All);

            var samplesMs = new double[iterations];
            long totalAlloc = 0;
            double totalMs = 0.0;
            double ticksToMs = 1000.0 / Stopwatch.Frequency;

            for (int iter = 0; iter < iterations; iter++)
            {
                byte shade = (byte)(10 + (iter & 15));
                Color32 c = new(shade, (byte)(shade + 2), (byte)(shade + 8), 255);

                long allocBefore = GC.GetAllocatedBytesForCurrentThread();
                long t0 = Stopwatch.GetTimestamp();

                for (int v = 0; v < vertCount; v++)
                {
                    sm.colors[v] = c;
                }
                sm.finalized = false;
                sm.FinalizeMesh(MeshParts.Colors);

                long elapsedTicks = Stopwatch.GetTimestamp() - t0;
                long allocDelta = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

                double ms = elapsedTicks * ticksToMs;
                samplesMs[iter] = ms;
                totalMs += ms;
                totalAlloc += Math.Max(0L, allocDelta);
            }

            Array.Sort(samplesMs);
            minMs = samplesMs[0];
            medianMs = samplesMs[iterations / 2];
            int p95Idx = Math.Min(iterations - 1, (int)Math.Ceiling(iterations * 0.95) - 1);
            p95Ms = samplesMs[p95Idx];
            maxMs = samplesMs[iterations - 1];
            meanMs = totalMs / iterations;
            meanAllocBytes = (double)totalAlloc / iterations;
        }
        finally
        {
            sm.Clear(MeshParts.All);
            try
            {
                if (sm.mesh != null)
                {
                    UnityEngine.Object.Destroy(sm.mesh);
                }
            }
            catch (Exception)
            {
                // Headless stub Mesh cleanup is best-effort.
            }
        }
    }

    [DebugAction("Project Vigilance", "Spike: S3 Waylith FlushCompat Color Benchmark", allowedGameStates = AllowedGameStates.PlayingOnWorld)]
    public static void RunSpikeS3_FlushCompatBenchmark()
    {
        Material? mat = WorldDrawLayer_HazeOfWar.ResolveVertexColorMaterial(
            WorldDrawLayer_HazeOfWar.HazeRenderQueue,
            WorldDrawLayer_HazeOfWar.SubMeshMaterialOverride,
            null);

        BenchmarkColorUpload(
            mat,
            vertCount: 512,
            triCount: 1024,
            iterations: 60,
            out double min512,
            out double med512,
            out double p95_512,
            out double max512,
            out double mean512,
            out double alloc512);

        BenchmarkColorUpload(
            mat,
            vertCount: 2048,
            triCount: 4096,
            iterations: 30,
            out double min2048,
            out double med2048,
            out double p95_2048,
            out double max2048,
            out double mean2048,
            out double alloc2048);

        Log.Message(
            $"[PV Spike S3] FlushCompat Color Upload Benchmark (material={(mat != null ? mat.name : "null")}):{Environment.NewLine}" +
            $"  Chunk  512v / 1024t (60x): min={min512:F3}ms, median={med512:F3}ms, p95={p95_512:F3}ms, max={max512:F3}ms, mean={mean512:F3}ms, alloc={alloc512:F0} B/call{Environment.NewLine}" +
            $"  Chunk 2048v / 4096t (30x): min={min2048:F3}ms, median={med2048:F3}ms, p95={p95_2048:F3}ms, max={max2048:F3}ms, mean={mean2048:F3}ms, alloc={alloc2048:F0} B/call");
    }

    [DebugAction("Project Vigilance", "Spike: S4 Globe Radius & Stage 1 Layer Baseline", allowedGameStates = AllowedGameStates.PlayingOnWorld)]
    public static void RunSpikeS4_GlobeRadiusAndLayerBaseline()
    {
        if (!TryGetSurfaceTerrain(out PlanetLayer? surface, out _, out List<LayerSubMesh>? terrainSubMeshes) ||
            surface == null ||
            terrainSubMeshes == null)
        {
            return;
        }

        WorldGrid? grid = Find.World?.grid;
        if (grid == null)
        {
            return;
        }

        int tilesCount = surface.TilesCount;
        float minCenterMag = float.MaxValue;
        float maxCenterMag = float.MinValue;
        float minTileVertMag = float.MaxValue;
        float maxTileVertMag = float.MinValue;
        var tmpVerts = new List<Vector3>(8);

        for (int i = 0; i < tilesCount; i++)
        {
            PlanetTile tile = surface.PlanetTileForID(i);
            if (!tile.Valid)
            {
                continue;
            }

            float cMag = grid.GetTileCenter(tile).magnitude;
            if (cMag < minCenterMag) minCenterMag = cMag;
            if (cMag > maxCenterMag) maxCenterMag = cMag;

            tmpVerts.Clear();
            grid.GetTileVertices(tile, tmpVerts);
            for (int v = 0; v < tmpVerts.Count; v++)
            {
                float vMag = tmpVerts[v].magnitude;
                if (vMag < minTileVertMag) minTileVertMag = vMag;
                if (vMag > maxTileVertMag) maxTileVertMag = vMag;
            }
        }

        float minTerrainVertMag = float.MaxValue;
        float maxTerrainVertMag = float.MinValue;
        for (int s = 0; s < terrainSubMeshes.Count; s++)
        {
            LayerSubMesh sm = terrainSubMeshes[s];
            if (sm == null)
            {
                continue;
            }

            for (int v = 0; v < sm.verts.Count; v++)
            {
                float m = sm.verts[v].magnitude;
                if (m < minTerrainVertMag) minTerrainVertMag = m;
                if (m > maxTerrainVertMag) maxTerrainVertMag = m;
            }
        }

        float maxObserved = Math.Max(maxTileVertMag == float.MinValue ? 0f : maxTileVertMag, maxTerrainVertMag == float.MinValue ? 0f : maxTerrainVertMag);
        float recommendedMinShellRadius = maxObserved + 0.025f;

        var shellLayer = (surface.WorldDrawLayers != null ? surface.GetLayer<WorldDrawLayer_ShroudShell>() : null) ?? WorldDrawLayer_ShroudShell.Instance;
        var hazeLayer = (surface.WorldDrawLayers != null ? surface.GetLayer<WorldDrawLayer_HazeOfWar>() : null) ?? WorldDrawLayer_HazeOfWar.Instance;
        var radarLayer = (surface.WorldDrawLayers != null ? surface.GetLayer<WorldDrawLayer_RadarCoverage>() : null) ?? WorldDrawLayer_RadarCoverage.Instance;

        var shellSubMeshes = shellLayer != null ? SubMeshesField?.GetValue(shellLayer) as List<LayerSubMesh> : null;
        SummarizeLayerMeshes(shellSubMeshes, out int shellSm, out int shellVerts, out int shellTris, out int shellUvs, out int shellFin);

        var hazeSubMeshes = hazeLayer != null ? SubMeshesField?.GetValue(hazeLayer) as List<LayerSubMesh> : null;
        SummarizeLayerMeshes(hazeSubMeshes, out int hazeSm, out int hazeVerts, out int hazeTris, out int hazeUvs, out int hazeFin);

        var radarSubMeshes = radarLayer != null ? SubMeshesField?.GetValue(radarLayer) as List<LayerSubMesh> : null;
        SummarizeLayerMeshes(radarSubMeshes, out int radarSm, out int radarVerts, out int radarTris, out int radarUvs, out int radarFin);

        var cullStats = ProjectVigilance.Geoscape.Patches.WorldDrawLayer_Terrain_Cull.GetCullStats();

        Log.Message(
            $"[PV Spike S4] Globe Radii & Stage 2 Layer Telemetry (VisibilityMode={GeoscapeRules.VisibilityMode}, Tiles={tilesCount:N0}):{Environment.NewLine}" +
            $"  TileCenterMag=[{minCenterMag:F4}, {maxCenterMag:F4}], TileVertexMag=[{minTileVertMag:F4}, {maxTileVertMag:F4}], TerrainMeshVertMag=[{minTerrainVertMag:F4}, {maxTerrainVertMag:F4}], RecommendedMinShellRadius>={recommendedMinShellRadius:F4}{Environment.NewLine}" +
            $"  ShroudShellLayer: built={(shellLayer?.IsGeometryBuilt ?? false)}, activeChunks={shellLayer?.ActiveShellChunkCount ?? 0}/{ShroudGeometry.ShellChunkCount}, subMeshes={shellSm} (finalized={shellFin}), verts={shellVerts:N0}, tris={shellTris:N0}, uvs={shellUvs:N0}{Environment.NewLine}" +
            $"  HazeOfWarLayer: built={(hazeLayer?.IsGeometryBuilt ?? false)}, builtChunks={hazeLayer?.BuiltFineChunkCount ?? 0}/{ShroudGeometry.FineChunkCount}, visibleChunks={hazeLayer?.VisibleFineChunkCount ?? 0}, subMeshes={hazeSm} (finalized={hazeFin}), verts={hazeVerts:N0}, tris={hazeTris:N0}, uvs={hazeUvs:N0}{Environment.NewLine}" +
            $"  TerrainCull: active={cullStats.active}, culledSubMeshes={cullStats.culledSubMeshes}/{cullStats.totalSubMeshes}, culledTris={cullStats.culledTris:N0}/{cullStats.totalTris:N0}{Environment.NewLine}" +
            $"  RadarCoverageLayer: visible={(radarLayer?.Visible ?? false)}, dimSubMeshes={(radarLayer?.DimSubMeshCount ?? 0)}, totalSubMeshes={radarSm} (finalized={radarFin}), verts={radarVerts:N0}, tris={radarTris:N0}, uvs={radarUvs:N0}");
    }
}
