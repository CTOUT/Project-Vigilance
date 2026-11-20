using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.Patches
{
    /// <summary>
    /// Incremental tile-to-terrain-submesh index tracking how many fine/explored tiles occupy each terrain chunk
    /// so fully shrouded terrain chunks can be hidden without altering MeshColliders.
    /// </summary>
    internal sealed class TerrainCullIndex
    {
        private readonly int _tileCount;
        private readonly int _subMeshCount;
        private readonly int[] _tileSubMeshOffsets;
        private readonly int[] _tileSubMeshes;
        private readonly bool[] _originallyFinalized;
        private readonly int[] _subMeshTriangleCounts;
        private readonly int[] _subMeshActiveTileCount;
        private readonly bool[] _tileCountedActive;

        private readonly int _totalTriangleCount;
        private int _culledSubMeshCount;
        private int _culledTriangleCount;

        public int SubMeshCount => _subMeshCount;
        public int TotalTriangleCount => _totalTriangleCount;
        public int CulledSubMeshCount => _culledSubMeshCount;
        public int CulledTriangleCount => _culledTriangleCount;

        public TerrainCullIndex(
            int tileCount,
            IList<LayerSubMesh> subMeshes,
            IReadOnlyList<IReadOnlyList<int>> triToTile)
        {
            _tileCount = Math.Max(0, tileCount);
            _subMeshCount = subMeshes?.Count ?? 0;

            _originallyFinalized = new bool[_subMeshCount];
            _subMeshTriangleCounts = new int[_subMeshCount];
            _subMeshActiveTileCount = new int[_subMeshCount];
            _tileCountedActive = new bool[_tileCount];

            int totalTris = 0;
            for (int sm = 0; sm < _subMeshCount; sm++)
            {
                LayerSubMesh? subMesh = subMeshes![sm];
                _originallyFinalized[sm] = subMesh != null && subMesh.finalized;
                int tris = (subMesh?.tris.Count ?? 0) / 3;
                _subMeshTriangleCounts[sm] = tris;
                totalTris += tris;
            }

            _totalTriangleCount = totalTris;

            var tileSubMeshCounts = new int[_tileCount];
            var seenSubMeshStamp = new int[_tileCount];
            Array.Fill(seenSubMeshStamp, -1);

            int mappedLimit = triToTile != null ? Math.Min(_subMeshCount, triToTile.Count) : 0;
            for (int sm = 0; sm < mappedLimit; sm++)
            {
                IReadOnlyList<int>? list = triToTile![sm];
                if (list == null)
                {
                    continue;
                }

                for (int i = 0; i < list.Count; i++)
                {
                    int tileId = list[i];
                    if ((uint)tileId >= (uint)_tileCount || seenSubMeshStamp[tileId] == sm)
                    {
                        continue;
                    }

                    seenSubMeshStamp[tileId] = sm;
                    tileSubMeshCounts[tileId]++;
                }
            }

            var offsets = new int[_tileCount + 1];
            int totalPairs = 0;
            for (int t = 0; t < _tileCount; t++)
            {
                offsets[t] = totalPairs;
                totalPairs += tileSubMeshCounts[t];
            }

            offsets[_tileCount] = totalPairs;
            var indices = new int[totalPairs];
            var writePos = new int[_tileCount];
            Array.Copy(offsets, writePos, _tileCount);

            Array.Fill(seenSubMeshStamp, -1);
            for (int sm = 0; sm < mappedLimit; sm++)
            {
                IReadOnlyList<int>? list = triToTile![sm];
                if (list == null)
                {
                    continue;
                }

                for (int i = 0; i < list.Count; i++)
                {
                    int tileId = list[i];
                    if ((uint)tileId >= (uint)_tileCount || seenSubMeshStamp[tileId] == sm)
                    {
                        continue;
                    }

                    seenSubMeshStamp[tileId] = sm;
                    indices[writePos[tileId]++] = sm;
                }
            }

            _tileSubMeshOffsets = offsets;
            _tileSubMeshes = indices;
            RecomputeCulledTotals();
        }

        public bool WasOriginallyFinalized(int subMeshIndex)
        {
            return (uint)subMeshIndex < (uint)_subMeshCount && _originallyFinalized[subMeshIndex];
        }

        public int GetSubMeshActiveTileCount(int subMeshIndex)
        {
            return (uint)subMeshIndex < (uint)_subMeshCount ? _subMeshActiveTileCount[subMeshIndex] : 0;
        }

        public void SyncFromHierarchy(ShroudHierarchy hierarchy)
        {
            if (hierarchy == null)
            {
                return;
            }

            Array.Clear(_tileCountedActive, 0, _tileCountedActive.Length);
            Array.Clear(_subMeshActiveTileCount, 0, _subMeshActiveTileCount.Length);

            int limit = Math.Min(_tileCount, hierarchy.TileCount);
            for (int t = 0; t < limit; t++)
            {
                if (!hierarchy.IsTileFine(t))
                {
                    continue;
                }

                _tileCountedActive[t] = true;
                int start = _tileSubMeshOffsets[t];
                int end = _tileSubMeshOffsets[t + 1];
                for (int i = start; i < end; i++)
                {
                    _subMeshActiveTileCount[_tileSubMeshes[i]]++;
                }
            }

            RecomputeCulledTotals();
        }

        public void NotifyTilesBecameFine(
            IReadOnlyList<int> tileIds,
            IList<LayerSubMesh>? subMeshes,
            bool cullEnabled)
        {
            if (tileIds == null || tileIds.Count == 0)
            {
                return;
            }

            for (int i = 0; i < tileIds.Count; i++)
            {
                int t = tileIds[i];
                if ((uint)t >= (uint)_tileCount || _tileCountedActive[t])
                {
                    continue;
                }

                _tileCountedActive[t] = true;
                int start = _tileSubMeshOffsets[t];
                int end = _tileSubMeshOffsets[t + 1];
                for (int s = start; s < end; s++)
                {
                    int sm = _tileSubMeshes[s];
                    if (++_subMeshActiveTileCount[sm] != 1)
                    {
                        continue;
                    }

                    if (_originallyFinalized[sm])
                    {
                        _culledSubMeshCount--;
                        _culledTriangleCount -= _subMeshTriangleCounts[sm];
                        if (cullEnabled && subMeshes != null && sm < subMeshes.Count && subMeshes[sm] != null)
                        {
                            subMeshes[sm].finalized = true;
                        }
                    }
                }
            }
        }

        public void NotifyTilesBecameCoarse(
            IReadOnlyList<int> tileIds,
            IList<LayerSubMesh>? subMeshes,
            bool cullEnabled)
        {
            if (tileIds == null || tileIds.Count == 0)
            {
                return;
            }

            for (int i = 0; i < tileIds.Count; i++)
            {
                int t = tileIds[i];
                if ((uint)t >= (uint)_tileCount || !_tileCountedActive[t])
                {
                    continue;
                }

                _tileCountedActive[t] = false;
                int start = _tileSubMeshOffsets[t];
                int end = _tileSubMeshOffsets[t + 1];
                for (int s = start; s < end; s++)
                {
                    int sm = _tileSubMeshes[s];
                    if (--_subMeshActiveTileCount[sm] != 0)
                    {
                        continue;
                    }

                    if (_originallyFinalized[sm])
                    {
                        _culledSubMeshCount++;
                        _culledTriangleCount += _subMeshTriangleCounts[sm];
                        if (cullEnabled && subMeshes != null && sm < subMeshes.Count && subMeshes[sm] != null)
                        {
                            subMeshes[sm].finalized = false;
                        }
                    }
                }
            }
        }

        public void ApplyToSubMeshes(IList<LayerSubMesh> subMeshes, bool cullEnabled)
        {
            if (subMeshes == null)
            {
                return;
            }

            int limit = Math.Min(_subMeshCount, subMeshes.Count);
            for (int sm = 0; sm < limit; sm++)
            {
                LayerSubMesh? subMesh = subMeshes[sm];
                if (subMesh == null)
                {
                    continue;
                }

                subMesh.finalized = cullEnabled
                    ? (_originallyFinalized[sm] && _subMeshActiveTileCount[sm] > 0)
                    : _originallyFinalized[sm];
            }
        }

        private void RecomputeCulledTotals()
        {
            int culledSm = 0;
            int culledTri = 0;
            for (int sm = 0; sm < _subMeshCount; sm++)
            {
                if (_originallyFinalized[sm] && _subMeshActiveTileCount[sm] == 0)
                {
                    culledSm++;
                    culledTri += _subMeshTriangleCounts[sm];
                }
            }

            _culledSubMeshCount = culledSm;
            _culledTriangleCount = culledTri;
        }
    }

    /// <summary>
    /// Tier T unexplored-terrain culler hiding fully shrouded surface terrain submeshes when
    /// WorldDrawLayer_ShroudShell covers them, while leaving MeshCollider raycasting intact.
    /// </summary>
    [HarmonyPatch(typeof(WorldDrawLayerBase), "FinalizeMesh", new[] { typeof(MeshParts) })]
    public static class WorldDrawLayer_Terrain_Cull
    {
        private static readonly FieldInfo? PlanetLayerField =
            AccessTools.Field(typeof(WorldDrawLayer), "planetLayer");

        private static readonly FieldInfo? SubMeshesField =
            AccessTools.Field(typeof(WorldDrawLayerBase), "subMeshes");

        private static readonly FieldInfo? TriangleIndexToTileIdField =
            AccessTools.Field(typeof(WorldDrawLayer), "triangleIndexToTileID");

        private static WorldDrawLayer_Terrain? s_terrainLayer;
        private static List<LayerSubMesh>? s_terrainSubMeshes;
        private static TerrainCullIndex? s_index;

        public static bool IsCullActiveNow()
        {
            return GeoscapeMod.Settings != null
                && GeoscapeMod.Settings.optimiseUnexploredTerrain
                && GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar
                && WorldDrawLayer_ShroudShell.Instance != null
                && WorldDrawLayer_ShroudShell.Instance.IsGeometryBuilt;
        }

        public static void Postfix(WorldDrawLayerBase __instance)
        {
            if (__instance is not WorldDrawLayer_Terrain terrain)
            {
                return;
            }

            PlanetLayer? layer = PlanetLayerField?.GetValue(terrain) as PlanetLayer;
            if (layer == null)
            {
                return;
            }

            if (layer != Find.World?.grid?.Surface)
            {
                return;
            }

            RebuildIndexFromTerrain(layer, terrain, WorldDrawLayer_HazeOfWar.SharedHierarchy);
        }

        internal static void TrySyncAndApply(PlanetLayer? surface, ShroudHierarchy? hierarchy)
        {
            if (surface == null)
            {
                return;
            }

            WorldDrawLayer_Terrain? terrain = surface.WorldDrawLayers != null
                ? (surface.GetLayer<WorldDrawLayer_Terrain>() ?? s_terrainLayer)
                : s_terrainLayer;
            if (terrain == null)
            {
                return;
            }

            if (s_index == null || s_terrainLayer != terrain)
            {
                RebuildIndexFromTerrain(surface, terrain, hierarchy);
                return;
            }

            if (hierarchy != null)
            {
                s_index.SyncFromHierarchy(hierarchy);
            }

            if (s_terrainSubMeshes != null)
            {
                s_index.ApplyToSubMeshes(s_terrainSubMeshes, IsCullActiveNow());
            }
        }

        internal static void NotifyTilesBecameFine(IReadOnlyList<int> tileIds)
        {
            if (s_index == null || tileIds == null || tileIds.Count == 0)
            {
                return;
            }

            s_index.NotifyTilesBecameFine(tileIds, s_terrainSubMeshes, IsCullActiveNow());
        }

        internal static void NotifyTilesBecameCoarse(IReadOnlyList<int> tileIds)
        {
            if (s_index == null || tileIds == null || tileIds.Count == 0)
            {
                return;
            }

            s_index.NotifyTilesBecameCoarse(tileIds, s_terrainSubMeshes, IsCullActiveNow());
        }

        public static void RefreshAllSubMeshes()
        {
            if (s_index != null && s_terrainSubMeshes != null)
            {
                if (WorldDrawLayer_HazeOfWar.SharedHierarchy != null)
                {
                    s_index.SyncFromHierarchy(WorldDrawLayer_HazeOfWar.SharedHierarchy);
                }

                s_index.ApplyToSubMeshes(s_terrainSubMeshes, IsCullActiveNow());
                return;
            }

            PlanetLayer? surface = Find.World?.grid?.Surface;
            if (surface != null)
            {
                TrySyncAndApply(surface, WorldDrawLayer_HazeOfWar.SharedHierarchy);
            }
        }

        public static (bool active, int culledSubMeshes, int totalSubMeshes, int culledTris, int totalTris) GetCullStats()
        {
            bool active = IsCullActiveNow();
            if (s_index == null)
            {
                return (active, 0, 0, 0, 0);
            }

            return (
                active,
                active ? s_index.CulledSubMeshCount : 0,
                s_index.SubMeshCount,
                active ? s_index.CulledTriangleCount : 0,
                s_index.TotalTriangleCount);
        }

        internal static void ClearStaticStateForTesting()
        {
            s_terrainLayer = null;
            s_terrainSubMeshes = null;
            s_index = null;
        }

        private static void RebuildIndexFromTerrain(
            PlanetLayer surface,
            WorldDrawLayer_Terrain terrain,
            ShroudHierarchy? hierarchy)
        {
            var subMeshes = SubMeshesField?.GetValue(terrain) as List<LayerSubMesh>;
            var triToTile = TriangleIndexToTileIdField?.GetValue(terrain) as List<List<int>>;
            if (subMeshes == null || triToTile == null)
            {
                return;
            }

            s_terrainLayer = terrain;
            s_terrainSubMeshes = subMeshes;
            s_index = new TerrainCullIndex(surface.TilesCount, subMeshes, triToTile);

            if (hierarchy != null)
            {
                s_index.SyncFromHierarchy(hierarchy);
            }

            s_index.ApplyToSubMeshes(subMeshes, IsCullActiveNow());
        }
    }
}
