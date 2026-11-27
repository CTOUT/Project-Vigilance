using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using HarmonyLib;
using LudeonTK;
using ProjectVigilance.Geoscape.Patches;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Debug;

/// <summary>
/// Unified Developer Mode diagnostic tool reporting globe geometry radii, terrain culling,
/// mesh collider picking alignment, and Waylith shroud/radar layer telemetry.
/// </summary>
public static class DebugActionsRendererDiagnostics
{
    private static readonly FieldInfo? SubMeshesField =
        AccessTools.Field(typeof(WorldDrawLayerBase), "subMeshes");

    private static readonly FieldInfo? TriangleIndexToTileIdField =
        AccessTools.Field(typeof(WorldDrawLayer), "triangleIndexToTileID");

    private static readonly FieldInfo? MeshCollidersInOrderField =
        AccessTools.Field(typeof(WorldDrawLayer), "meshCollidersInOrder");

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
            Log.Message("[PV Renderer Diagnostics] Aborted: active world surface PlanetLayer is null.");
            NotifyToast("[PV Diagnostics] Open a planet world first.", MessageTypeDefOf.RejectInput);
            terrain = null;
            subMeshes = null;
            return false;
        }

        terrain = surface.WorldDrawLayers != null ? surface.GetLayer<WorldDrawLayer_Terrain>() : null;
        if (terrain == null)
        {
            Log.Message("[PV Renderer Diagnostics] Aborted: WorldDrawLayer_Terrain not found on surface layer.");
            NotifyToast("[PV Diagnostics] WorldDrawLayer_Terrain not found on surface.", MessageTypeDefOf.RejectInput);
            subMeshes = null;
            return false;
        }

        subMeshes = SubMeshesField?.GetValue(terrain) as List<LayerSubMesh>;
        if (subMeshes == null)
        {
            Log.Message("[PV Renderer Diagnostics] Aborted: WorldDrawLayerBase.subMeshes field unavailable.");
            return false;
        }

        return true;
    }

    private static void SummarizeLayerMeshes(
        List<LayerSubMesh>? subMeshes,
        out int subMeshCount,
        out int verts,
        out int tris,
        out int uvs,
        out int finalizedCount,
        out int drawnVerts,
        out int drawnTris)
    {
        subMeshCount = subMeshes?.Count ?? 0;
        verts = 0;
        tris = 0;
        uvs = 0;
        finalizedCount = 0;
        drawnVerts = 0;
        drawnTris = 0;
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

            int smVerts = sm.verts.Count;
            int smTris = sm.tris.Count / 3;
            verts += smVerts;
            tris += smTris;
            uvs += sm.uvs.Count;
            if (sm.finalized)
            {
                finalizedCount++;
                drawnVerts += smVerts;
                drawnTris += smTris;
            }
        }
    }

    [DebugAction("Project Vigilance", "Dump World Renderer & Shroud Diagnostics", allowedGameStates = AllowedGameStates.PlayingOnWorld)]
    public static void RunWorldRendererAndShroudDiagnostics()
    {
        if (!TryGetSurfaceTerrain(out PlanetLayer? surface, out WorldDrawLayer_Terrain? terrain, out List<LayerSubMesh>? terrainSubMeshes) ||
            surface == null ||
            terrain == null ||
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
        int disabledTerrain = 0;
        int finalizedTerrain = 0;
        for (int s = 0; s < terrainSubMeshes.Count; s++)
        {
            LayerSubMesh sm = terrainSubMeshes[s];
            if (sm == null)
            {
                continue;
            }

            if (sm.finalized) finalizedTerrain++;
            if (sm.disabled) disabledTerrain++;

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
        SummarizeLayerMeshes(shellSubMeshes, out int shellSm, out int shellVerts, out int shellTris, out int shellUvs, out int shellFin, out int shellDrawnVerts, out int shellDrawnTris);

        var hazeSubMeshes = hazeLayer != null ? SubMeshesField?.GetValue(hazeLayer) as List<LayerSubMesh> : null;
        SummarizeLayerMeshes(hazeSubMeshes, out int hazeSm, out int hazeVerts, out int hazeTris, out int hazeUvs, out int hazeFin, out int hazeDrawnVerts, out int hazeDrawnTris);

        var radarSubMeshes = radarLayer != null ? SubMeshesField?.GetValue(radarLayer) as List<LayerSubMesh> : null;
        SummarizeLayerMeshes(radarSubMeshes, out int radarSm, out int radarVerts, out int radarTris, out int radarUvs, out int radarFin, out _, out _);

        var colliders = MeshCollidersInOrderField?.GetValue(terrain) as List<MeshCollider>;
        var triToTile = TriangleIndexToTileIdField?.GetValue(terrain) as List<List<int>>;
        var cullStats = WorldDrawLayer_Terrain_Cull.GetCullStats();
        bool cullSetting = GeoscapeMod.Settings?.optimiseUnexploredTerrain ?? true;

        var sb = new StringBuilder();
        sb.AppendLine($"[PV Renderer Diagnostics] Globe Radii & Stage 2 Layer Telemetry (VisibilityMode={GeoscapeRules.VisibilityMode}, Tiles={tilesCount:N0}):");
        sb.AppendLine($"  TileCenterMag=[{minCenterMag:F4}, {maxCenterMag:F4}], TileVertexMag=[{minTileVertMag:F4}, {maxTileVertMag:F4}], TerrainMeshVertMag=[{minTerrainVertMag:F4}, {maxTerrainVertMag:F4}], RecommendedMinShellRadius>={recommendedMinShellRadius:F4}");
        sb.AppendLine($"  TerrainPicking: subMeshes={terrainSubMeshes.Count} (finalized={finalizedTerrain}, disabled={disabledTerrain}), colliders={colliders?.Count ?? -1}, triMapLists={triToTile?.Count ?? -1}");
        sb.AppendLine($"  ShroudShellLayer: built={(shellLayer?.IsGeometryBuilt ?? false)}, activeChunks={shellLayer?.ActiveShellChunkCount ?? 0}/{ShroudGeometry.ShellChunkCount}, subMeshes={shellSm} (finalized={shellFin}), drawnVerts={shellDrawnVerts:N0} (alloc={shellVerts:N0}), drawnTris={shellDrawnTris:N0} (alloc={shellTris:N0}), uvs={shellUvs:N0}");
        sb.AppendLine($"  HazeOfWarLayer: built={(hazeLayer?.IsGeometryBuilt ?? false)}, builtChunks={hazeLayer?.BuiltFineChunkCount ?? 0}/{ShroudGeometry.FineChunkCount}, visibleChunks={hazeLayer?.VisibleFineChunkCount ?? 0}, subMeshes={hazeSm} (finalized={hazeFin}), drawnVerts={hazeDrawnVerts:N0} (alloc={hazeVerts:N0}), drawnTris={hazeDrawnTris:N0} (alloc={hazeTris:N0}), uvs={hazeUvs:N0}");
        sb.AppendLine($"  TerrainCull: setting={cullSetting}, active={cullStats.active}, culledSubMeshes={cullStats.culledSubMeshes}/{cullStats.totalSubMeshes}, culledTris={cullStats.culledTris:N0}/{cullStats.totalTris:N0}");
        sb.Append($"  RadarCoverageLayer: visible={(radarLayer?.Visible ?? false)}, dimSubMeshes={(radarLayer?.DimSubMeshCount ?? 0)}, totalSubMeshes={radarSm} (finalized={radarFin}), verts={radarVerts:N0}, tris={radarTris:N0}, uvs={radarUvs:N0}");

        Log.Message(sb.ToString());
        NotifyToast("[PV Diagnostics] World renderer & shroud telemetry logged.", MessageTypeDefOf.PositiveEvent);
    }
}
