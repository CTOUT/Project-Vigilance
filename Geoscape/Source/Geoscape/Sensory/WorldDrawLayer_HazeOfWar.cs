using System;
using System.Collections;
using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Tier F hierarchical Fog of War layer rendering lazy corner-only tile fans across 320 surface
/// chunks at altitude +0.065f and render queue 3546 for frontier shroud, explored haze, and
/// live vision clearings, paired with <see cref="WorldDrawLayer_ShroudShell"/> (Tier S).
/// </summary>
public class WorldDrawLayer_HazeOfWar : WorldDrawLayer
{
    public const float HazeAltitudeOffset = 0.065f; // Strictly above Tier S shell (+0.062f), below RadarWash/Outline

    // Explicit transparent render queue above Tier S shroud shell (3545) and below radar shadow wash (3547)
    public const int HazeRenderQueue = 3546;

    internal static readonly int[] OverlayRenderQueues =
    {
        WorldDrawLayer_RadarCoverage.DimmingRenderQueue,
        WorldDrawLayer_ShroudShell.ShroudShellRenderQueue,
        HazeRenderQueue,
        WorldDrawLayer_RadarCoverage.ShadowWashRenderQueue,
        WorldDrawLayer_RadarCoverage.OutlineRenderQueue
    };

    public static Func<Material?>? SubMeshMaterialOverride { get; set; }
    internal static Func<int, Material?>? RenderQueueMaterialOverride { get; set; }
    internal static Func<bool>? IsInMainThreadOverride { get; set; }
    internal static Action<PlanetTile, List<Vector3>>? TileVerticesOverride { get; set; }
    internal static Func<PlanetTile, Vector3>? TileCenterOverride { get; set; }
    internal static Action<PlanetTile, List<PlanetTile>>? TileNeighborsOverride { get; set; }

    private static bool IsCurrentThreadMain => IsInMainThreadOverride?.Invoke() ?? UnityData.IsInMainThread;

    /// <summary>
    /// Regenerates an overlay layer immediately when invoked on the Unity main thread, or marks it dirty
    /// for deferred regeneration when called from a background loading thread.
    /// </summary>
    internal static void RequestLayerRefresh(WorldDrawLayerBase? layer)
    {
        if (layer == null)
        {
            return;
        }

        if (IsCurrentThreadMain)
        {
            layer.RegenerateNow();
        }
        else
        {
            layer.SetDirty();
        }
    }

    public static readonly Color32 ShroudColor = new(6, 8, 12, 255); // 100% opaque deep obsidian shroud
    public static readonly Color32 HazeColor = new(36, 42, 50, 140);  // ~55% translucent slate grey
    public static readonly Color32 ClearColor = new(0, 0, 0, 0);     // 100% transparent (active vision)

    private struct TileVertSlice
    {
        public int ChunkIndex;
        public int StartVertIndex;
        public int VertCount;
        public Vector3 UnitDir;
    }

    private static ShroudHierarchy? s_sharedHierarchy;
    private static PlanetLayer? s_hierarchyLayer;
    private static int s_hierarchyTileCount = -1;
    private static bool s_hierarchySyncedFromManager;

    internal static ShroudHierarchy? SharedHierarchy => s_sharedHierarchy;

    private readonly List<Vector3> _tmpTileVerts = new(8);
    private readonly LayerSubMesh?[] _fineChunkSubMeshes = new LayerSubMesh?[ShroudGeometry.FineChunkCount];
    private readonly bool[] _fineChunkBuilt = new bool[ShroudGeometry.FineChunkCount];
    private readonly bool[] _dirtyFineChunks = new bool[ShroudGeometry.FineChunkCount];
    private readonly bool[] _prewarmQueued = new bool[ShroudGeometry.FineChunkCount];
    private readonly Queue<int> _prewarmQueue = new(16);
    private TileVertSlice[]? _tileSlices;
    private bool _isGeometryBuilt;

    public static WorldDrawLayer_HazeOfWar? Instance { get; private set; }

    public bool IsGeometryBuilt => _isGeometryBuilt && s_sharedHierarchy != null;

    public int BuiltFineChunkCount
    {
        get
        {
            int count = 0;
            for (int c = 0; c < _fineChunkBuilt.Length; c++)
            {
                if (_fineChunkBuilt[c])
                {
                    count++;
                }
            }

            return count;
        }
    }

    public int VisibleFineChunkCount
    {
        get
        {
            int count = 0;
            for (int c = 0; c < _fineChunkSubMeshes.Length; c++)
            {
                LayerSubMesh? sm = _fineChunkSubMeshes[c];
                if (sm != null && sm.finalized)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public WorldDrawLayer_HazeOfWar()
    {
        Instance = this;
    }

    public override bool VisibleWhenLayerNotSelected => true;

    public override bool Visible
    {
        get
        {
            if (GeoscapeRules.VisibilityMode != VisibilityMode.FogOfWar)
            {
                return false;
            }

            return planetLayer != null
                && planetLayer.Visible
                && Find.World != null
                && WorldRendererUtility.WorldRendered;
        }
    }

    /// <summary>
    /// Ensures the shared hierarchical state machine exists for <paramref name="layer"/> and is
    /// synchronised with <see cref="SensoryManager.Instance"/> when available.
    /// </summary>
    internal static ShroudHierarchy EnsureSharedHierarchy(PlanetLayer layer)
    {
        int tileCount = layer.TilesCount;
        bool needBuild = s_sharedHierarchy == null
            || !ReferenceEquals(s_hierarchyLayer, layer)
            || s_hierarchyTileCount != tileCount;

        if (needBuild)
        {
            ShroudGeometry.IcoSphere sphere = ShroudGeometry.GetSharedSphere();
            var tmpNeighbors = new List<PlanetTile>(8);

            s_sharedHierarchy = new ShroudHierarchy(
                in sphere,
                tileCount,
                t => GetTileCenterForLayer(layer, t),
                (t, outVerts) => FillTileVerticesForLayer(layer, t, outVerts),
                (t, outNeighbors) =>
                {
                    outNeighbors.Clear();
                    tmpNeighbors.Clear();
                    PlanetTile pt = layer.PlanetTileForID(t);
                    if (!pt.Valid)
                    {
                        return;
                    }

                    if (TileNeighborsOverride != null)
                    {
                        TileNeighborsOverride(pt, tmpNeighbors);
                    }
                    else
                    {
                        layer.GetTileNeighbors(pt, tmpNeighbors);
                    }

                    for (int i = 0; i < tmpNeighbors.Count; i++)
                    {
                        outNeighbors.Add(tmpNeighbors[i].tileId);
                    }
                });

            s_hierarchyLayer = layer;
            s_hierarchyTileCount = tileCount;
            s_hierarchySyncedFromManager = false;
        }

        if (!s_hierarchySyncedFromManager && SensoryManager.Instance != null)
        {
            SyncHierarchyFromSensoryManager(layer, s_sharedHierarchy!, SensoryManager.Instance);
            s_hierarchySyncedFromManager = true;
        }

        return s_sharedHierarchy!;
    }

    internal static void InvalidateHierarchySync()
    {
        s_hierarchySyncedFromManager = false;
    }

    /// <summary>
    /// Stages a tile state transition in the shared hierarchical state machine.
    /// Call <see cref="FlushShroudUpdates"/> after staging a batch of tile changes.
    /// </summary>
    public bool NotifyTileState(int tileId, byte newState)
    {
        if (!_isGeometryBuilt || s_sharedHierarchy == null)
        {
            return false;
        }

        return s_sharedHierarchy.SetTileState(tileId, newState);
    }

    /// <summary>
    /// Backwards-compatible wrapper translating <see cref="Color32"/> alpha semantics to
    /// hierarchical shroud states (<see cref="ShroudHierarchy.StateClear"/> / Haze / Shroud).
    /// </summary>
    public bool SetTileColor(int tileId, Color32 color)
    {
        byte state = color.a == 0
            ? ShroudHierarchy.StateClear
            : (color.a < 255 ? ShroudHierarchy.StateHaze : ShroudHierarchy.StateShroud);
        return NotifyTileState(tileId, state);
    }

    /// <summary>
    /// Flushes all staged hierarchy changes in zero-hole order:
    /// 1. Reveal terrain submeshes becoming fine,
    /// 2. Lazily build / update Tier F vertex colours and finalized states,
    /// 3. Update dirty Tier S shell chunks,
    /// 4. Cull newly coarse terrain submeshes now covered by the shell.
    /// </summary>
    public void FlushShroudUpdates()
    {
        ShroudHierarchy? hierarchy = s_sharedHierarchy;
        if (!_isGeometryBuilt || hierarchy == null || planetLayer == null)
        {
            return;
        }

        IReadOnlyList<int> newlyFine = hierarchy.NewlyFineTileIds;
        IReadOnlyList<int> newlyCoarse = hierarchy.NewlyCoarseTileIds;
        IReadOnlyList<int> dirtyFine = hierarchy.DirtyFineTileIds;
        ulong dirtyShellMask = hierarchy.ConsumeShellChunkDirtyMask();

        if (newlyFine.Count == 0 && newlyCoarse.Count == 0 && dirtyFine.Count == 0 && dirtyShellMask == 0UL)
        {
            return;
        }

        // 1. Reveal newly fine terrain chunks BEFORE shroud opens over them
        Patches.WorldDrawLayer_Terrain_Cull.NotifyTilesBecameFine(newlyFine);

        // 2. Build newly needed Tier F chunks lazily and update modified vertex colours
        ShroudEffectLevel effectLevel = GetEffectiveShroudLevel();
        SensoryTheme theme = GetEffectiveTheme();
        bool reduceMotion = GetEffectiveReduceMotion();
        float timeSeconds = WorldDrawLayer_ShroudShell.GetTimeSeconds();

        for (int i = 0; i < dirtyFine.Count; i++)
        {
            int tileId = dirtyFine[i];
            if ((uint)tileId >= (uint)hierarchy.TileCount || !hierarchy.IsTileFine(tileId))
            {
                continue;
            }

            int chunkIndex = hierarchy.GetTileFineChunk(tileId);
            if (!_fineChunkBuilt[chunkIndex])
            {
                if (hierarchy.GetFineChunkVisibleTileCount(chunkIndex) <= 0)
                {
                    continue;
                }

                EnsureFineChunkBuilt(chunkIndex, hierarchy, effectLevel, theme, timeSeconds, reduceMotion);
                EnqueueAdjacentUnbuiltChunks(chunkIndex, hierarchy);
                _dirtyFineChunks[chunkIndex] = false;
                continue;
            }

            UpdateBuiltTileColor(tileId, hierarchy, effectLevel, theme, timeSeconds, reduceMotion, forceClear: false);
            _dirtyFineChunks[chunkIndex] = true;
        }

        for (int i = 0; i < newlyCoarse.Count; i++)
        {
            int tileId = newlyCoarse[i];
            if ((uint)tileId >= (uint)hierarchy.TileCount || hierarchy.IsTileFine(tileId))
            {
                continue;
            }

            int chunkIndex = hierarchy.GetTileFineChunk(tileId);
            if (!_fineChunkBuilt[chunkIndex])
            {
                continue;
            }

            UpdateBuiltTileColor(tileId, hierarchy, effectLevel, theme, timeSeconds, reduceMotion, forceClear: true);
            _dirtyFineChunks[chunkIndex] = true;
        }

        for (int c = 0; c < ShroudGeometry.FineChunkCount; c++)
        {
            if (!_dirtyFineChunks[c])
            {
                continue;
            }

            _dirtyFineChunks[c] = false;
            LayerSubMesh? sm = _fineChunkSubMeshes[c];
            if (sm == null)
            {
                continue;
            }

            if (hierarchy.GetFineChunkVisibleTileCount(c) <= 0)
            {
                sm.finalized = false;
            }
            else
            {
                sm.finalized = false;
                sm.FinalizeMesh(MeshParts.Colors);
            }
        }

        // 3. Re-triangulate dirty Tier S coarse shell chunks
        if (dirtyShellMask != 0UL)
        {
            WorldDrawLayer_ShroudShell.Instance?.ApplyDirtyShellChunks(dirtyShellMask);
        }

        // 4. Hide newly coarse terrain chunks AFTER Tier S shell covers them
        Patches.WorldDrawLayer_Terrain_Cull.NotifyTilesBecameCoarse(newlyCoarse);

        hierarchy.ClearDirtyFlags();
    }

    /// <summary>
    /// Backwards-compatible alias for <see cref="FlushShroudUpdates"/>.
    /// </summary>
    public void ApplyDirtyColors()
    {
        FlushShroudUpdates();
    }

    public override IEnumerable Regenerate()
    {
        Instance = this;

        if (planetLayer == null || GeoscapeRules.VisibilityMode != VisibilityMode.FogOfWar)
        {
            HideAndClearChunkSubMeshes();
            _isGeometryBuilt = false;
            s_hierarchySyncedFromManager = false;
            Patches.WorldDrawLayer_Terrain_Cull.RefreshAllSubMeshes();
            yield break;
        }

        if (SensoryManager.Instance != null && s_hierarchySyncedFromManager)
        {
            SyncHierarchyFromSensoryManager(planetLayer, EnsureSharedHierarchy(planetLayer), SensoryManager.Instance);
        }

        ShroudHierarchy hierarchy = EnsureSharedHierarchy(planetLayer);
        ResetChunkState();

        int tilesCount = planetLayer.TilesCount;
        if (_tileSlices == null || _tileSlices.Length != tilesCount)
        {
            _tileSlices = new TileVertSlice[tilesCount];
        }
        else
        {
            Array.Clear(_tileSlices, 0, _tileSlices.Length);
        }

        PrecomputeTileDirections(planetLayer, tilesCount);

        ShroudEffectLevel effectLevel = GetEffectiveShroudLevel();
        SensoryTheme theme = GetEffectiveTheme();
        bool reduceMotion = GetEffectiveReduceMotion();
        float timeSeconds = WorldDrawLayer_ShroudShell.GetTimeSeconds();

        for (int c = 0; c < ShroudGeometry.FineChunkCount; c++)
        {
            if (hierarchy.GetFineChunkVisibleTileCount(c) <= 0)
            {
                continue;
            }

            EnsureFineChunkBuilt(c, hierarchy, effectLevel, theme, timeSeconds, reduceMotion);
        }

        for (int c = 0; c < ShroudGeometry.FineChunkCount; c++)
        {
            if (_fineChunkBuilt[c])
            {
                EnqueueAdjacentUnbuiltChunks(c, hierarchy);
            }
        }

        ulong remainingShellMask = hierarchy.ConsumeShellChunkDirtyMask();
        hierarchy.ClearDirtyFlags();
        _isGeometryBuilt = true;

        if (remainingShellMask != 0UL)
        {
            WorldDrawLayer_ShroudShell.Instance?.ApplyDirtyShellChunks(remainingShellMask);
        }

        Patches.WorldDrawLayer_Terrain_Cull.TrySyncAndApply(planetLayer, hierarchy);
        yield break;
    }

    /// <summary>
    /// Builds at most one queued adjacent Tier F chunk in the background without disturbing active draw calls.
    /// </summary>
    internal void PumpOnePrewarmChunk()
    {
        ShroudHierarchy? hierarchy = s_sharedHierarchy;
        if (!_isGeometryBuilt || hierarchy == null || planetLayer == null || _prewarmQueue.Count == 0)
        {
            return;
        }

        while (_prewarmQueue.Count > 0)
        {
            int c = _prewarmQueue.Dequeue();
            if ((uint)c >= (uint)ShroudGeometry.FineChunkCount || _fineChunkBuilt[c])
            {
                continue;
            }

            ShroudEffectLevel effectLevel = GetEffectiveShroudLevel();
            SensoryTheme theme = GetEffectiveTheme();
            bool reduceMotion = GetEffectiveReduceMotion();
            float timeSeconds = WorldDrawLayer_ShroudShell.GetTimeSeconds();

            EnsureFineChunkBuilt(c, hierarchy, effectLevel, theme, timeSeconds, reduceMotion);
            return;
        }
    }

    /// <summary>
    /// Refreshes vertex colours for all fine frontier shroud tiles across built Tier F chunks.
    /// </summary>
    internal void RefreshAtmosphereColors()
    {
        ShroudHierarchy? hierarchy = s_sharedHierarchy;
        if (!_isGeometryBuilt || hierarchy == null || _tileSlices == null)
        {
            return;
        }

        ShroudEffectLevel effectLevel = GetEffectiveShroudLevel();
        SensoryTheme theme = GetEffectiveTheme();
        bool reduceMotion = GetEffectiveReduceMotion();
        float timeSeconds = WorldDrawLayer_ShroudShell.GetTimeSeconds();

        for (int c = 0; c < ShroudGeometry.FineChunkCount; c++)
        {
            if (!_fineChunkBuilt[c] || hierarchy.GetFineChunkVisibleTileCount(c) <= 0)
            {
                continue;
            }

            LayerSubMesh? sm = _fineChunkSubMeshes[c];
            if (sm == null)
            {
                continue;
            }

            ReadOnlySpan<int> chunkTiles = hierarchy.GetFineChunkTiles(c);
            for (int i = 0; i < chunkTiles.Length; i++)
            {
                int tileId = chunkTiles[i];
                if (!hierarchy.IsTileFine(tileId))
                {
                    continue;
                }

                if (hierarchy.GetTileState(tileId) != ShroudHierarchy.StateShroud)
                {
                    continue;
                }

                UpdateBuiltTileColor(tileId, hierarchy, effectLevel, theme, timeSeconds, reduceMotion, forceClear: false);
            }

            sm.finalized = false;
            sm.FinalizeMesh(MeshParts.Colors);
        }
    }

    internal static void ResetSharedHierarchyForTesting()
    {
        Instance = null;
        s_sharedHierarchy = null;
        s_hierarchyLayer = null;
        s_hierarchyTileCount = -1;
        s_hierarchySyncedFromManager = false;
        SubMeshMaterialOverride = null;
        RenderQueueMaterialOverride = null;
        IsInMainThreadOverride = null;
        TileVerticesOverride = null;
        TileCenterOverride = null;
        TileNeighborsOverride = null;
    }

    internal static Vector3 GetTileCenterForLayer(PlanetLayer layer, int tileId)
    {
        PlanetTile pt = layer.PlanetTileForID(tileId);
        if (!pt.Valid)
        {
            return Vector3.up * 100f;
        }

        if (TileCenterOverride != null)
        {
            return TileCenterOverride(pt);
        }

        return Find.WorldGrid != null ? Find.WorldGrid.GetTileCenter(pt) : Vector3.up * 100f;
    }

    private static void FillTileVerticesForLayer(PlanetLayer layer, int tileId, List<Vector3> outVerts)
    {
        outVerts.Clear();
        PlanetTile pt = layer.PlanetTileForID(tileId);
        if (!pt.Valid)
        {
            return;
        }

        if (TileVerticesOverride != null)
        {
            TileVerticesOverride(pt, outVerts);
        }
        else if (Find.WorldGrid != null)
        {
            Find.WorldGrid.GetTileVertices(pt, outVerts);
        }
    }

    private static void SyncHierarchyFromSensoryManager(
        PlanetLayer layer,
        ShroudHierarchy hierarchy,
        SensoryManager sensory)
    {
        int count = Math.Min(layer.TilesCount, hierarchy.TileCount);
        bool allClear = GeoscapeRules.VisibilityMode != VisibilityMode.FogOfWar;

        for (int t = 0; t < count; t++)
        {
            if (allClear)
            {
                hierarchy.SetTileState(t, ShroudHierarchy.StateClear);
                continue;
            }

            PlanetTile pt = layer.PlanetTileForID(t);
            if (!pt.Valid)
            {
                continue;
            }

            byte state = sensory.HasActiveVision(pt)
                ? ShroudHierarchy.StateClear
                : (sensory.IsTileIdExplored(t) ? ShroudHierarchy.StateHaze : ShroudHierarchy.StateShroud);

            hierarchy.SetTileState(t, state);
        }
    }

    private void PrecomputeTileDirections(PlanetLayer layer, int tilesCount)
    {
        if (_tileSlices == null)
        {
            return;
        }

        for (int t = 0; t < tilesCount; t++)
        {
            Vector3 center = GetTileCenterForLayer(layer, t);
            _tileSlices[t].UnitDir = center.sqrMagnitude > 1e-12f ? center.normalized : Vector3.up;
        }
    }

    private void EnsureFineChunkBuilt(
        int chunkIndex,
        ShroudHierarchy hierarchy,
        ShroudEffectLevel effectLevel,
        SensoryTheme theme,
        float timeSeconds,
        bool reduceMotion)
    {
        if (_fineChunkBuilt[chunkIndex] || planetLayer == null || _tileSlices == null)
        {
            return;
        }

        Material? material = ResolveVertexColorMaterial(
            HazeRenderQueue,
            SubMeshMaterialOverride,
            RenderQueueMaterialOverride);

        LayerSubMesh? sm = _fineChunkSubMeshes[chunkIndex];
        if (sm == null)
        {
            sm = new LayerSubMesh(new Mesh(), material!);
            _fineChunkSubMeshes[chunkIndex] = sm;
            subMeshes.Add(sm);
        }
        else
        {
            sm.Clear(MeshParts.All);
            if (material != null)
            {
                sm.material = material;
            }
        }

        ReadOnlySpan<int> chunkTiles = hierarchy.GetFineChunkTiles(chunkIndex);
        for (int i = 0; i < chunkTiles.Length; i++)
        {
            int tileId = chunkTiles[i];
            if ((uint)tileId >= (uint)_tileSlices.Length)
            {
                continue;
            }

            _tmpTileVerts.Clear();
            FillTileVerticesForLayer(planetLayer, tileId, _tmpTileVerts);
            int vertCount = _tmpTileVerts.Count;
            if (vertCount < 3)
            {
                continue;
            }

            for (int v = 0; v < vertCount; v++)
            {
                Vector3 corner = _tmpTileVerts[v];
                _tmpTileVerts[v] = corner + (corner.normalized * HazeAltitudeOffset);
            }

            Vector3 unitDir = _tileSlices[tileId].UnitDir;
            Color32 tileColor = hierarchy.IsTileFine(tileId)
                ? ShroudAtmosphere.EvaluateFineTileColor(
                    unitDir,
                    hierarchy.GetTileState(tileId),
                    hierarchy.IsFrontierRing(tileId),
                    effectLevel,
                    theme,
                    timeSeconds,
                    reduceMotion)
                : ClearColor;

            int baseIdx = sm.verts.Count;
            for (int v = 0; v < vertCount; v++)
            {
                sm.verts.Add(_tmpTileVerts[v]);
                sm.colors.Add(tileColor);
            }

            // Corner-only fan: 0 UVs needed, outward winding checked once at k == 1
            Vector3 v0 = _tmpTileVerts[0];
            Vector3 v1 = _tmpTileVerts[1];
            Vector3 v2 = _tmpTileVerts[2];
            bool outwardCCW = Vector3.Dot(Vector3.Cross(v1 - v0, v2 - v0), unitDir) > 0f;

            for (int k = 1; k <= vertCount - 2; k++)
            {
                if (outwardCCW)
                {
                    sm.tris.Add(baseIdx);
                    sm.tris.Add(baseIdx + k);
                    sm.tris.Add(baseIdx + k + 1);
                }
                else
                {
                    sm.tris.Add(baseIdx);
                    sm.tris.Add(baseIdx + k + 1);
                    sm.tris.Add(baseIdx + k);
                }
            }

            _tileSlices[tileId].ChunkIndex = chunkIndex;
            _tileSlices[tileId].StartVertIndex = baseIdx;
            _tileSlices[tileId].VertCount = vertCount;
        }

        _fineChunkBuilt[chunkIndex] = true;

        bool hasVisible = hierarchy.GetFineChunkVisibleTileCount(chunkIndex) > 0 && sm.tris.Count > 0;
        sm.finalized = false;
        if (sm.verts.Count > 0 && sm.tris.Count > 0)
        {
            sm.FinalizeMesh(MeshParts.Verts | MeshParts.Colors | MeshParts.Tris);
            if (!hasVisible)
            {
                sm.finalized = false;
            }
        }
        else
        {
            sm.finalized = false;
        }
    }

    private void UpdateBuiltTileColor(
        int tileId,
        ShroudHierarchy hierarchy,
        ShroudEffectLevel effectLevel,
        SensoryTheme theme,
        float timeSeconds,
        bool reduceMotion,
        bool forceClear)
    {
        if (_tileSlices == null)
        {
            return;
        }

        ref readonly TileVertSlice slice = ref _tileSlices[tileId];
        if (slice.VertCount == 0)
        {
            return;
        }

        LayerSubMesh? sm = _fineChunkSubMeshes[slice.ChunkIndex];
        if (sm == null || slice.StartVertIndex + slice.VertCount > sm.colors.Count)
        {
            return;
        }

        Color32 color = forceClear
            ? ClearColor
            : ShroudAtmosphere.EvaluateFineTileColor(
                slice.UnitDir,
                hierarchy.GetTileState(tileId),
                hierarchy.IsFrontierRing(tileId),
                effectLevel,
                theme,
                timeSeconds,
                reduceMotion);

        int end = slice.StartVertIndex + slice.VertCount;
        for (int v = slice.StartVertIndex; v < end; v++)
        {
            sm.colors[v] = color;
        }
    }

    private void EnqueueAdjacentUnbuiltChunks(int chunkIndex, ShroudHierarchy hierarchy)
    {
        ReadOnlySpan<int> adj = hierarchy.GetFineChunkAdjacentChunks(chunkIndex);
        for (int i = 0; i < adj.Length; i++)
        {
            int nChunk = adj[i];
            if (!_fineChunkBuilt[nChunk]
                && !_prewarmQueued[nChunk]
                && hierarchy.GetFineChunkTiles(nChunk).Length > 0)
            {
                _prewarmQueued[nChunk] = true;
                _prewarmQueue.Enqueue(nChunk);
            }
        }
    }

    private void HideAndClearChunkSubMeshes()
    {
        for (int s = 0; s < subMeshes.Count; s++)
        {
            LayerSubMesh? sm = subMeshes[s];
            if (sm != null)
            {
                sm.Clear(MeshParts.All);
                sm.finalized = false;
            }
        }

        ResetChunkState();
    }

    private void ResetChunkState()
    {
        for (int i = 0; i < subMeshes.Count; i++)
        {
            LayerSubMesh? sm = subMeshes[i];
            if (sm != null)
            {
                sm.Clear(MeshParts.All);
                sm.finalized = false;
            }
        }

        Array.Clear(_fineChunkBuilt, 0, _fineChunkBuilt.Length);
        Array.Clear(_dirtyFineChunks, 0, _dirtyFineChunks.Length);
        Array.Clear(_prewarmQueued, 0, _prewarmQueued.Length);
        _prewarmQueue.Clear();
    }

    private static ShroudEffectLevel GetEffectiveShroudLevel()
    {
        return GeoscapeMod.Settings != null ? GeoscapeMod.Settings.shroudEffects : ShroudEffectLevel.Subtle;
    }

    private static SensoryTheme GetEffectiveTheme()
    {
        return GeoscapeMod.Settings != null ? GeoscapeMod.Settings.sensoryTheme : SensoryTheme.TacticalCyan;
    }

    private static bool GetEffectiveReduceMotion()
    {
        return GeoscapeMod.Settings != null && GeoscapeMod.Settings.reduceShroudMotion;
    }

    internal static Material? ResolveVertexColorMaterial(
        int renderQueue,
        Func<Material?>? materialOverride,
        Func<int, Material?>? queueMaterialOverride)
    {
        if (queueMaterialOverride != null)
        {
            return queueMaterialOverride(renderQueue);
        }

        if (materialOverride != null)
        {
            return materialOverride();
        }

        return Verse.MatLoader.LoadMat(GeoscapeOverlayMaterials.VertexColorTransparentPath, renderQueue);
    }

    internal static LayerSubMesh GetOrCreateVertexColorSubMesh(
        List<LayerSubMesh> subMeshes,
        int maxVertsPerMesh,
        int neededVerts,
        int renderQueue,
        Func<Material?>? materialOverride,
        Func<int, Material?>? queueMaterialOverride,
        ref int subMeshIndex,
        Predicate<LayerSubMesh>? skipSubMesh = null)
    {
        Material? targetMaterial = ResolveVertexColorMaterial(renderQueue, materialOverride, queueMaterialOverride);

        if (subMeshIndex < subMeshes.Count)
        {
            LayerSubMesh current = subMeshes[subMeshIndex];
            if ((skipSubMesh == null || !skipSubMesh(current)) &&
                ReferenceEquals(current.material, targetMaterial) &&
                current.verts.Count + neededVerts <= maxVertsPerMesh)
            {
                return current;
            }
        }

        for (int i = 0; i < subMeshes.Count; i++)
        {
            LayerSubMesh existing = subMeshes[i];
            if ((skipSubMesh == null || !skipSubMesh(existing)) &&
                ReferenceEquals(existing.material, targetMaterial) &&
                existing.verts.Count + neededVerts <= maxVertsPerMesh)
            {
                subMeshIndex = i;
                return existing;
            }
        }

        LayerSubMesh newSubMesh = new(new Mesh(), targetMaterial!);
        subMeshes.Add(newSubMesh);
        subMeshIndex = subMeshes.Count - 1;
        return newSubMesh;
    }
}

/// <summary>
/// Pre-1.7 backward-compatible alias preserving naming conventions across XML and external mods.
/// </summary>
public class WorldLayer_HazeOfWar : WorldDrawLayer_HazeOfWar
{
}
