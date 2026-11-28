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
    [StaticConstructorOnStartup]
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
        private static Material? s_gpuShroudMaterial;
        private static Material? s_gpuHazeMaterial;
        private static bool s_gpuShroudLoadAttempted;
        private static int s_propBaseColor = -1;
        private static int s_propNebulaColor = -1;
        private static int s_propRimColor = -1;
        private static int s_propShroudParams = -1;

        private readonly bool[] _chunkVertsUploaded = new bool[ShroudGeometry.ShellChunkCount];
        private readonly bool[] _chunkColorsDirtyWhileHidden = new bool[ShroudGeometry.ShellChunkCount];
        private bool _isBuilt;
        private bool _usingGpuShader;
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

        public override IEnumerable Regenerate()
        {
            Instance = this;

            if (planetLayer == null || GeoscapeRules.VisibilityMode != VisibilityMode.FogOfWar)
            {
                HideAndClearAllSubMeshes();
                _isBuilt = false;
                Patches.WorldDrawLayer_Terrain_Cull.RefreshAllSubMeshes();
                yield break;
            }

            ShroudHierarchy hierarchy = WorldDrawLayer_HazeOfWar.EnsureSharedHierarchy(planetLayer);
            ShroudGeometry.IcoSphere sphere = ShroudGeometry.GetSharedSphere();

            _shellRadius = ResolvePlanetSurfaceRadius(planetLayer) + ShellAltitudeOffset;

            Material? material = ResolveShroudShellMaterial(out _usingGpuShader);
            if (_usingGpuShader)
            {
                ApplyGpuShaderUniforms();
            }

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
            if (Instance != null && Instance._usingGpuShader)
            {
                ApplyGpuShaderUniforms();
            }
            else
            {
                float now = GetTimeSeconds();
                Instance?.RefreshAllShellColors(now);
            }

            WorldDrawLayer_HazeOfWar.Instance?.RefreshAtmosphereColors();
        }

        public override void Render()
        {
            AdvanceFrameWork();
            base.Render();
        }

        internal void AdvanceFrameWork()
        {
            if (!_isBuilt || GeoscapeRules.VisibilityMode != VisibilityMode.FogOfWar)
            {
                return;
            }

            WorldDrawLayer_HazeOfWar.Instance?.PumpOnePrewarmChunk();

            // When native Waylith 1.7 GPU shader is active, nebula drift runs entirely on the GPU via _GameSeconds (0 ms CPU).
            if (_usingGpuShader)
            {
                return;
            }

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

        private static Material? ResolveShroudShellMaterial(out bool usingGpuShader)
        {
            usingGpuShader = false;

            if (SubMeshMaterialOverride != null ||
                WorldDrawLayer_HazeOfWar.SubMeshMaterialOverride != null ||
                RenderQueueMaterialOverride != null ||
                WorldDrawLayer_HazeOfWar.RenderQueueMaterialOverride != null)
            {
                return WorldDrawLayer_HazeOfWar.ResolveVertexColorMaterial(
                    ShroudShellRenderQueue,
                    SubMeshMaterialOverride ?? WorldDrawLayer_HazeOfWar.SubMeshMaterialOverride,
                    RenderQueueMaterialOverride ?? WorldDrawLayer_HazeOfWar.RenderQueueMaterialOverride);
            }

            if (!s_gpuShroudLoadAttempted)
            {
                s_gpuShroudLoadAttempted = true;
                try
                {
                    s_gpuShroudMaterial = MatLoader.LoadMat("World/PV_ShroudShell", ShroudShellRenderQueue);
                }
                catch
                {
                    s_gpuShroudMaterial = null;
                }
            }

            if (s_gpuShroudMaterial != null)
            {
                usingGpuShader = true;
                return s_gpuShroudMaterial;
            }

            return WorldDrawLayer_HazeOfWar.ResolveVertexColorMaterial(
                ShroudShellRenderQueue,
                null,
                null);
        }

        internal static Material? ResolveFineHazeMaterial(
            Func<Material?>? materialOverride,
            Func<int, Material?>? queueMaterialOverride)
        {
            if (materialOverride != null || queueMaterialOverride != null)
            {
                return WorldDrawLayer_HazeOfWar.ResolveVertexColorMaterial(
                    WorldDrawLayer_HazeOfWar.HazeRenderQueue,
                    materialOverride,
                    queueMaterialOverride);
            }

            // Ensure base GPU shroud material is loaded
            ResolveShroudShellMaterial(out bool usingGpu);
            if (usingGpu && s_gpuShroudMaterial != null)
            {
                if (s_gpuHazeMaterial == null)
                {
                    try
                    {
                        s_gpuHazeMaterial = new Material(s_gpuShroudMaterial)
                        {
                            renderQueue = WorldDrawLayer_HazeOfWar.HazeRenderQueue
                        };
                    }
                    catch
                    {
                        s_gpuHazeMaterial = null;
                    }
                }

                if (s_gpuHazeMaterial != null)
                {
                    ApplyGpuShaderUniforms();
                    return s_gpuHazeMaterial;
                }
            }

            return WorldDrawLayer_HazeOfWar.ResolveVertexColorMaterial(
                WorldDrawLayer_HazeOfWar.HazeRenderQueue,
                null,
                null);
        }

        internal static void ApplyGpuShaderUniforms()
        {
            Material? shellMat = s_gpuShroudMaterial;
            Material? hazeMat = s_gpuHazeMaterial;
            if (shellMat == null && hazeMat == null)
            {
                return;
            }

            try
            {
                if (s_propBaseColor < 0)
                {
                    s_propBaseColor = Shader.PropertyToID("_BaseColor");
                    s_propNebulaColor = Shader.PropertyToID("_NebulaColor");
                    s_propRimColor = Shader.PropertyToID("_RimColor");
                    s_propShroudParams = Shader.PropertyToID("_ShroudParams");
                }

                SensoryTheme theme = GetEffectiveTheme();
                float intensity = GetEffectiveShroudIntensity();
                float motionScale = GetEffectiveReduceMotion() ? 0f : 1f;
                Vector4 shroudParams = new Vector4(intensity, motionScale, 1f, 0f);

                ShroudAtmosphere.GetGpuThemePalette(theme, out Vector4 baseObsidian, out Vector4 nebulaTint, out Vector4 rimColor);
                if (shellMat != null)
                {
                    shellMat.SetVector(s_propBaseColor, baseObsidian);
                    shellMat.SetVector(s_propNebulaColor, nebulaTint);
                    shellMat.SetVector(s_propRimColor, rimColor);
                    shellMat.SetVector(s_propShroudParams, shroudParams);
                }

                if (hazeMat != null)
                {
                    hazeMat.SetVector(s_propBaseColor, baseObsidian);
                    hazeMat.SetVector(s_propNebulaColor, nebulaTint);
                    hazeMat.SetVector(s_propRimColor, rimColor);
                    hazeMat.SetVector(s_propShroudParams, shroudParams);
                }
            }
            catch
            {
                // Headless unit test environment
            }
        }

        private static float GetEffectiveShroudIntensity()
        {
            return GeoscapeMod.Settings != null ? Mathf.Clamp01(GeoscapeMod.Settings.shroudAtmosphereIntensity) : 0.45f;
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
