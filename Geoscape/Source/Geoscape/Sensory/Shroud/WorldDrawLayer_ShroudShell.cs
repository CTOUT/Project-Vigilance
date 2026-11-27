using System;
using System.Collections;
using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory
{
    /// <summary>
    /// Tier S hierarchical shroud layer rendering a 20-chunk subdivision-5 icosphere shell
    /// at altitude +0.062f and render queue 3545 over unexplored terrain regions.
    /// </summary>
    public class WorldDrawLayer_ShroudShell : WorldDrawLayer
    {
        public const int ShroudShellRenderQueue = 3545;
        public const float ShellAltitudeOffset = 0.062f;

        private const float AnimationIntervalSeconds = 0.05f;
        private const int AnimationChunksPerStep = 2;

        public static WorldDrawLayer_ShroudShell? Instance { get; private set; }

        internal static Func<Material?>? SubMeshMaterialOverride { get; set; }
        internal static Func<int, Material?>? RenderQueueMaterialOverride { get; set; }
        internal static Func<float>? TimeSecondsOverride { get; set; }

        private static bool s_timeUnavailable;

        private readonly bool[] _chunkVertsUploaded = new bool[ShroudGeometry.ShellChunkCount];
        private readonly bool[] _chunkColorsDirtyWhileHidden = new bool[ShroudGeometry.ShellChunkCount];
        private bool _isBuilt;
        private float _shellRadius = 100f + ShellAltitudeOffset;
        private float _lastAnimTime = -1f;
        private int _animChunkCursor;

        public WorldDrawLayer_ShroudShell()
        {
            Instance = this;
        }

        public bool IsGeometryBuilt => _isBuilt;

        public int ActiveShellChunkCount
        {
            get
            {
                int active = 0;
                int limit = Math.Min(subMeshes.Count, ShroudGeometry.ShellChunkCount);
                for (int i = 0; i < limit; i++)
                {
                    if (subMeshes[i] != null && subMeshes[i].finalized)
                    {
                        active++;
                    }
                }

                return active;
            }
        }

        public int TotalShellTriangleCount
        {
            get
            {
                int total = 0;
                int limit = Math.Min(subMeshes.Count, ShroudGeometry.ShellChunkCount);
                for (int i = 0; i < limit; i++)
                {
                    LayerSubMesh? sm = subMeshes[i];
                    if (sm != null && sm.finalized)
                    {
                        total += sm.tris.Count / 3;
                    }
                }

                return total;
            }
        }

        public override bool VisibleWhenLayerNotSelected => true;

        public override bool Visible
        {
            get
            {
                if (GeoscapeRules.VisibilityMode != VisibilityMode.ClassicHazeOfWar)
                {
                    return false;
                }

                return planetLayer != null
                    && planetLayer.Visible
                    && Find.World != null
                    && WorldRendererUtility.WorldRendered;
            }
        }

        public override IEnumerable Regenerate()
        {
            Instance = this;

            if (planetLayer == null || GeoscapeRules.VisibilityMode != VisibilityMode.ClassicHazeOfWar)
            {
                HideAndClearAllSubMeshes();
                _isBuilt = false;
                Patches.WorldDrawLayer_Terrain_Cull.RefreshAllSubMeshes();
                yield break;
            }

            ShroudHierarchy hierarchy = WorldDrawLayer_HazeOfWar.EnsureSharedHierarchy(planetLayer);
            ShroudGeometry.IcoSphere sphere = ShroudGeometry.GetSharedSphere();

            _shellRadius = ResolvePlanetSurfaceRadius(planetLayer) + ShellAltitudeOffset;

            Material? material = WorldDrawLayer_HazeOfWar.ResolveVertexColorMaterial(
                ShroudShellRenderQueue,
                SubMeshMaterialOverride ?? WorldDrawLayer_HazeOfWar.SubMeshMaterialOverride,
                RenderQueueMaterialOverride ?? WorldDrawLayer_HazeOfWar.RenderQueueMaterialOverride);

            EnsureChunkSubMeshes(material);

            ShroudEffectLevel effectLevel = GetEffectiveShroudLevel();
            SensoryTheme theme = GetEffectiveTheme();
            bool reduceMotion = GetEffectiveReduceMotion();
            float timeSeconds = GetTimeSeconds();

            for (int c = 0; c < ShroudGeometry.ShellChunkCount; c++)
            {
                _chunkVertsUploaded[c] = false;
                _chunkColorsDirtyWhileHidden[c] = false;
                PopulateChunkGeometry(
                    c,
                    hierarchy,
                    sphere.ShellChunks[c],
                    effectLevel,
                    theme,
                    timeSeconds,
                    reduceMotion);
            }

            _isBuilt = true;
            _lastAnimTime = timeSeconds;
            hierarchy.ConsumeShellChunkDirtyMask();

            Patches.WorldDrawLayer_Terrain_Cull.TrySyncAndApply(planetLayer, hierarchy);
            yield break;
        }

        /// <summary>
        /// Re-triangulates and uploads only the Tier S chunks flagged in <paramref name="dirtyMask"/>.
        /// </summary>
        public void ApplyDirtyShellChunks(ulong dirtyMask)
        {
            if (!_isBuilt || dirtyMask == 0UL)
            {
                return;
            }

            ShroudHierarchy? hierarchy = WorldDrawLayer_HazeOfWar.SharedHierarchy;
            if (hierarchy == null)
            {
                return;
            }

            ShroudGeometry.IcoSphere sphere = ShroudGeometry.GetSharedSphere();
            int chunkLimit = Math.Min(subMeshes.Count, ShroudGeometry.ShellChunkCount);

            for (int c = 0; c < chunkLimit; c++)
            {
                if ((dirtyMask & (1UL << c)) == 0UL)
                {
                    continue;
                }

                RebuildChunkTriangles(c, hierarchy, sphere.ShellChunks[c]);
            }
        }

        /// <summary>
        /// Updates vertex colours across all finalized shell chunks using current atmosphere settings.
        /// </summary>
        public void RefreshAllShellColors(float timeSeconds)
        {
            if (!_isBuilt)
            {
                return;
            }

            ShroudGeometry.IcoSphere sphere = ShroudGeometry.GetSharedSphere();
            ShroudEffectLevel effectLevel = GetEffectiveShroudLevel();
            SensoryTheme theme = GetEffectiveTheme();
            bool reduceMotion = GetEffectiveReduceMotion();
            int chunkLimit = Math.Min(subMeshes.Count, ShroudGeometry.ShellChunkCount);

            for (int c = 0; c < chunkLimit; c++)
            {
                UpdateChunkColors(c, sphere.ShellChunks[c], effectLevel, theme, timeSeconds, reduceMotion, uploadIfFinalized: true);
            }
        }

        /// <summary>
        /// Re-applies vertex colours across both Tier S shell chunks and Tier F frontier tiles
        /// immediately after Mod Settings changes.
        /// </summary>
        public static void NotifyAtmosphereSettingsChanged()
        {
            float now = GetTimeSeconds();
            Instance?.RefreshAllShellColors(now);
            WorldDrawLayer_HazeOfWar.Instance?.RefreshAtmosphereColors();
        }

        public override void Render()
        {
            AdvanceFrameWork();
            base.Render();
        }

        internal void AdvanceFrameWork()
        {
            if (!_isBuilt || GeoscapeRules.VisibilityMode != VisibilityMode.ClassicHazeOfWar)
            {
                return;
            }

            WorldDrawLayer_HazeOfWar.Instance?.PumpOnePrewarmChunk();

            ShroudEffectLevel effectLevel = GetEffectiveShroudLevel();
            bool reduceMotion = GetEffectiveReduceMotion();
            if (effectLevel == ShroudEffectLevel.Off || reduceMotion || subMeshes.Count < ShroudGeometry.ShellChunkCount)
            {
                return;
            }

            float now = GetTimeSeconds();
            if (_lastAnimTime >= 0f && (now - _lastAnimTime) < AnimationIntervalSeconds)
            {
                return;
            }

            _lastAnimTime = now;
            ShroudGeometry.IcoSphere sphere = ShroudGeometry.GetSharedSphere();
            SensoryTheme theme = GetEffectiveTheme();

            for (int step = 0; step < AnimationChunksPerStep; step++)
            {
                int c = _animChunkCursor;
                _animChunkCursor = (c + 1) % ShroudGeometry.ShellChunkCount;

                if (c < subMeshes.Count && subMeshes[c] != null && subMeshes[c].finalized)
                {
                    UpdateChunkColors(c, sphere.ShellChunks[c], effectLevel, theme, now, reduceMotion: false, uploadIfFinalized: true);
                }
            }
        }

        internal static void ClearStaticStateForTesting()
        {
            Instance = null;
            SubMeshMaterialOverride = null;
            RenderQueueMaterialOverride = null;
            TimeSecondsOverride = null;
            s_timeUnavailable = false;
        }

        private void HideAndClearAllSubMeshes()
        {
            for (int i = 0; i < subMeshes.Count; i++)
            {
                LayerSubMesh? sm = subMeshes[i];
                if (sm == null)
                {
                    continue;
                }

                sm.Clear(MeshParts.All);
                sm.finalized = false;
            }

            Array.Clear(_chunkVertsUploaded, 0, _chunkVertsUploaded.Length);
            Array.Clear(_chunkColorsDirtyWhileHidden, 0, _chunkColorsDirtyWhileHidden.Length);
        }

        private void EnsureChunkSubMeshes(Material? material)
        {
            while (subMeshes.Count < ShroudGeometry.ShellChunkCount)
            {
                subMeshes.Add(new LayerSubMesh(new Mesh(), material!));
            }

            for (int c = 0; c < ShroudGeometry.ShellChunkCount; c++)
            {
                LayerSubMesh? existing = subMeshes[c];
                if (existing == null)
                {
                    subMeshes[c] = new LayerSubMesh(new Mesh(), material!);
                }
                else if (material != null)
                {
                    existing.material = material;
                }
            }
        }

        private void PopulateChunkGeometry(
            int chunkIndex,
            ShroudHierarchy hierarchy,
            ShroudGeometry.ShellChunkGeometry chunkGeom,
            ShroudEffectLevel effectLevel,
            SensoryTheme theme,
            float timeSeconds,
            bool reduceMotion)
        {
            LayerSubMesh sm = subMeshes[chunkIndex];
            sm.Clear(MeshParts.All);

            int localCount = chunkGeom.LocalVertexCount;
            Vector3[] dirs = chunkGeom.LocalDirections;
            sm.verts.Capacity = Math.Max(sm.verts.Capacity, localCount);
            sm.colors.Capacity = Math.Max(sm.colors.Capacity, localCount);

            for (int v = 0; v < localCount; v++)
            {
                Vector3 dir = dirs[v];
                sm.verts.Add(dir * _shellRadius);
                sm.colors.Add(ShroudAtmosphere.EvaluateShellVertexColor(dir, effectLevel, theme, timeSeconds, reduceMotion));
            }

            // Leave sm.uvs untouched (0 UVs allocated per WorldVertexColorTransparent)
            RebuildChunkTriangles(chunkIndex, hierarchy, chunkGeom);
        }

        private void RebuildChunkTriangles(
            int chunkIndex,
            ShroudHierarchy hierarchy,
            ShroudGeometry.ShellChunkGeometry chunkGeom)
        {
            LayerSubMesh sm = subMeshes[chunkIndex];

            if (sm.verts.Count != chunkGeom.LocalVertexCount)
            {
                ShroudEffectLevel effectLevel = GetEffectiveShroudLevel();
                SensoryTheme theme = GetEffectiveTheme();
                bool reduceMotion = GetEffectiveReduceMotion();
                float now = GetTimeSeconds();
                _chunkVertsUploaded[chunkIndex] = false;
                PopulateChunkGeometry(chunkIndex, hierarchy, chunkGeom, effectLevel, theme, now, reduceMotion);
                return;
            }

            sm.tris.Clear();

            if (hierarchy.GetShellChunkCoarseCount(chunkIndex) <= 0)
            {
                sm.finalized = false;
                return;
            }

            int[] v0 = chunkGeom.FaceLocalV0;
            int[] v1 = chunkGeom.FaceLocalV1;
            int[] v2 = chunkGeom.FaceLocalV2;
            int startFace = chunkGeom.StartFace;

            for (int localFace = 0; localFace < ShroudGeometry.FacesPerShellChunk; localFace++)
            {
                if (!hierarchy.IsFaceCoarse(startFace + localFace))
                {
                    continue;
                }

                sm.tris.Add(v0[localFace]);
                sm.tris.Add(v1[localFace]);
                sm.tris.Add(v2[localFace]);
            }

            if (sm.tris.Count == 0)
            {
                sm.finalized = false;
                return;
            }

            // RimWorld's LayerSubMesh.FinalizeMesh invokes mesh.Clear() whenever MeshParts.Verts or MeshParts.Tris
            // is present, so triangle re-uploads must always include Verts and Colors to avoid wiping the chunk mesh.
            sm.finalized = false;
            sm.FinalizeMesh(MeshParts.Verts | MeshParts.Colors | MeshParts.Tris);
            _chunkVertsUploaded[chunkIndex] = true;
            _chunkColorsDirtyWhileHidden[chunkIndex] = false;
        }

        private void UpdateChunkColors(
            int chunkIndex,
            ShroudGeometry.ShellChunkGeometry chunkGeom,
            ShroudEffectLevel effectLevel,
            SensoryTheme theme,
            float timeSeconds,
            bool reduceMotion,
            bool uploadIfFinalized)
        {
            LayerSubMesh? sm = subMeshes[chunkIndex];
            if (sm == null || sm.colors.Count != chunkGeom.LocalVertexCount)
            {
                return;
            }

            Vector3[] dirs = chunkGeom.LocalDirections;
            for (int v = 0; v < dirs.Length; v++)
            {
                sm.colors[v] = ShroudAtmosphere.EvaluateShellVertexColor(dirs[v], effectLevel, theme, timeSeconds, reduceMotion);
            }

            if (!uploadIfFinalized)
            {
                return;
            }

            if (sm.finalized)
            {
                sm.finalized = false;
                if (!_chunkVertsUploaded[chunkIndex])
                {
                    sm.FinalizeMesh(MeshParts.Verts | MeshParts.Colors | MeshParts.Tris);
                    _chunkVertsUploaded[chunkIndex] = true;
                }
                else
                {
                    sm.FinalizeMesh(MeshParts.Colors);
                }

                _chunkColorsDirtyWhileHidden[chunkIndex] = false;
            }
            else
            {
                _chunkColorsDirtyWhileHidden[chunkIndex] = true;
            }
        }

        private static float ResolvePlanetSurfaceRadius(PlanetLayer layer)
        {
            int count = layer.TilesCount;
            if (count > 0 && (WorldDrawLayer_HazeOfWar.TileCenterOverride != null || Find.World?.grid != null))
            {
                float maxMag = 0f;
                int step = Math.Max(1, count / 16);
                for (int i = 0; i < count; i += step)
                {
                    Vector3 center = WorldDrawLayer_HazeOfWar.GetTileCenterForLayer(layer, i);
                    float mag = center.magnitude;
                    if (mag > maxMag)
                    {
                        maxMag = mag;
                    }
                }

                if (maxMag > 10f)
                {
                    return Math.Max(100f, maxMag);
                }
            }

            return 100f;
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

        internal static float GetTimeSeconds()
        {
            if (TimeSecondsOverride != null)
            {
                return TimeSecondsOverride();
            }

            if (s_timeUnavailable)
            {
                return 0f;
            }

            try
            {
                return Time.realtimeSinceStartup;
            }
            catch
            {
                s_timeUnavailable = true;
                return 0f;
            }
        }
    }
}
