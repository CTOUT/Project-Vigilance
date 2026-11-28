using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Core.Modules;
using ProjectVigilance.Geoscape.Orbital;
using ProjectVigilance.Geoscape.Sensory;
using ProjectVigilance.Geoscape.World;

namespace ProjectVigilance.Geoscape.Patches;

public static partial class WorldObject_Visibility_Patch
{
    // ---------------------------------------------------------
    // 3. Planetary Landmarks (Hexagons & Icons)
    // ---------------------------------------------------------
    [HarmonyPatch(typeof(WorldDrawLayer_Landmarks), "DrawStandard", typeof(Landmark), typeof(PlanetTile))]
    public static class Landmarks_DrawStandard_Patch
    {
        private static Func<WorldDrawLayer_Landmarks, Material, LayerSubMesh>? getSubMesh;
        private static bool getSubMeshResolved;

        public static bool Prefix(WorldDrawLayer_Landmarks? __instance, Landmark landmark, PlanetTile tile)
        {
            if (GeoscapeRules.VisibilityMode != VisibilityMode.ClassicHazeOfWar)
            {
                return true;
            }

            SensoryManager? sensory = SensoryManager.Instance;
            if (sensory != null)
            {
                if (!sensory.IsExplored(tile))
                {
                    return false;
                }

                // Explored but unidentified man-made landmarks draw an upright tactical cyan '!' marker instead of the landmark sprite.
                if (landmark?.def != null &&
                    LandmarkSensoryUtility.IsManMadeLandmarkDef(landmark.def.defName) &&
                    !sensory.IsTileIdentified(tile))
                {
                    DrawUnknownLandmarkWorldQuad(__instance, landmark, tile);
                    return false;
                }
            }

            return true;
        }

        private static void DrawUnknownLandmarkWorldQuad(WorldDrawLayer_Landmarks? layer, Landmark landmark, PlanetTile tile)
        {
            if (layer == null || layer.planetLayer == null)
            {
                return;
            }

            Func<WorldDrawLayer_Landmarks, Material, LayerSubMesh>? subMeshGetter = ResolveGetSubMesh();
            if (subMeshGetter == null)
            {
                return;
            }

            Material material = LandmarkSensoryUtility.GetUnknownLandmarkWorldMaterial();
            LayerSubMesh subMesh = subMeshGetter(layer, material);
            Vector3 tileCenter = layer.planetLayer.GetTileCenter(tile);
            float scale = landmark.def != null ? Mathf.Max(0.8f, landmark.def.drawScale) : 1f;
            float size = layer.planetLayer.GetTileSize(tile) * scale;
            WorldRendererUtility.PrintQuadTangentialToPlanet(
                tileCenter,
                size,
                0.03f,
                subMesh,
                counterClockwise: false,
                rotation: WorldObject_ListeningPost.UprightPrintRotationAngle,
                printUVs: true);
        }

        private static Func<WorldDrawLayer_Landmarks, Material, LayerSubMesh>? ResolveGetSubMesh()
        {
            if (getSubMeshResolved)
            {
                return getSubMesh;
            }

            getSubMeshResolved = true;
            MethodInfo? mi = AccessTools.Method(typeof(WorldDrawLayer_Landmarks), "GetSubMesh", new[] { typeof(Material) });
            if (mi != null)
            {
                getSubMesh = (Func<WorldDrawLayer_Landmarks, Material, LayerSubMesh>)Delegate.CreateDelegate(
                    typeof(Func<WorldDrawLayer_Landmarks, Material, LayerSubMesh>),
                    mi);
            }

            return getSubMesh;
        }
    }

    [HarmonyPatch(typeof(ExpandableLandmarksUtility), "get_LandmarksToShow")]
    public static class LandmarksToShow_Patch
    {
        private static readonly List<PlanetTile> cachedFilteredLandmarks = new(64);
        private static readonly List<PlanetTile> scratchFilteredLandmarks = new(64);
        private static List<PlanetTile>? cachedSourceList;
        private static int cachedSourceCount = -1;
        private static int cachedSweepRevision = -1;
        private static VisibilityMode cachedVisibilityMode = (VisibilityMode)(-1);
        private static SensoryManager? cachedSensoryManager;

        public static void ClearCache()
        {
            cachedFilteredLandmarks.Clear();
            scratchFilteredLandmarks.Clear();
            cachedSourceList = null;
            cachedSourceCount = -1;
            cachedSweepRevision = -1;
            cachedVisibilityMode = (VisibilityMode)(-1);
            cachedSensoryManager = null;
        }

        public static void Postfix(ref List<PlanetTile> __result)
        {
            VisibilityMode mode = GeoscapeRules.VisibilityMode;
            if (mode != VisibilityMode.ClassicHazeOfWar || __result == null || __result.Count == 0)
            {
                return;
            }

            SensoryManager? sensory = SensoryManager.Instance;
            if (sensory == null)
            {
                return;
            }

            int revision = sensory.SweepRevision;
            if (SensoryManager.IsExploredOverride == null &&
                cachedSweepRevision == revision &&
                cachedVisibilityMode == mode &&
                ReferenceEquals(cachedSensoryManager, sensory) &&
                (ReferenceEquals(__result, cachedFilteredLandmarks) ||
                 (ReferenceEquals(cachedSourceList, __result) && cachedSourceCount == __result.Count)))
            {
                __result = cachedFilteredLandmarks;
                return;
            }

            if (ReferenceEquals(__result, cachedFilteredLandmarks))
            {
                scratchFilteredLandmarks.Clear();
                for (int i = 0; i < __result.Count; i++)
                {
                    PlanetTile tile = __result[i];
                    if (sensory.IsExplored(tile))
                    {
                        scratchFilteredLandmarks.Add(tile);
                    }
                }

                cachedFilteredLandmarks.Clear();
                cachedFilteredLandmarks.AddRange(scratchFilteredLandmarks);
            }
            else
            {
                cachedFilteredLandmarks.Clear();
                for (int i = 0; i < __result.Count; i++)
                {
                    PlanetTile tile = __result[i];
                    if (sensory.IsExplored(tile))
                    {
                        cachedFilteredLandmarks.Add(tile);
                    }
                }

                cachedSourceList = __result;
                cachedSourceCount = __result.Count;
            }

            if (SensoryManager.IsExploredOverride == null)
            {
                cachedSweepRevision = revision;
                cachedVisibilityMode = mode;
                cachedSensoryManager = sensory;
            }
            else
            {
                cachedSweepRevision = -1;
            }

            // Keep explored man-made landmarks in LandmarksToShow so zoomed-out GUI can render cyan '!' markers until identified.
            __result = cachedFilteredLandmarks;
        }
    }

    [HarmonyPatch(typeof(ExpandableLandmarksUtility), nameof(ExpandableLandmarksUtility.ExpandableLandmarksOnGUI))]
    public static class ExpandableLandmarksUtility_ExpandableLandmarksOnGUI_Patch
    {
        private static bool accessorsInitialised;
        private static Func<List<PlanetTile>>? getLandmarksToShow;
        private static Func<float>? getTransitionPct;
        private static Func<Texture2D>? getLandmarkTex;
        private static Func<PlanetTile, bool>? isHighlighted;
        private static Func<PlanetTile, bool>? hiddenBehindTerrainNow;
        private static Func<Color, Material>? getMaterial;
        private static Func<PlanetTile, Rect>? expandedIconScreenRect;

        public static bool Prefix()
        {
            if (GeoscapeRules.VisibilityMode != VisibilityMode.ClassicHazeOfWar)
            {
                return true;
            }

            SensoryManager? sensory = SensoryManager.Instance;
            if (sensory == null || !TryEnsureAccessors())
            {
                return true;
            }

            List<PlanetTile> tiles = getLandmarksToShow!();
            float transitionPct = getTransitionPct!();

            using (new ProjectVigilance.Core.UI.VigilanceUiWidgets.ImguiStateScope())
            {
                for (int i = 0; i < tiles.Count; i++)
                {
                    PlanetTile tile = tiles[i];
                    try
                    {
                        bool highlighted = isHighlighted!(tile);
                        if (transitionPct == 0f && !highlighted)
                        {
                            continue;
                        }

                        if (hiddenBehindTerrainNow!(tile))
                        {
                            continue;
                        }

                        Landmark? landmark = Find.WorldGrid?[tile]?.Landmark;
                        bool isMaskedManMade = landmark?.def != null &&
                            LandmarkSensoryUtility.IsManMadeLandmarkDef(landmark.def.defName) &&
                            !sensory.IsTileIdentified(tile);

                        Color drawColor = isMaskedManMade ? LandmarkSensoryUtility.TacticalCyan : Color.white;
                        drawColor.a = highlighted ? 1f : transitionPct;
                        Texture2D tex = isMaskedManMade
                            ? LandmarkSensoryUtility.GetUnknownLandmarkExpandingTexture()
                            : getLandmarkTex!();

                        GUI.color = drawColor;
                        Rect rect = expandedIconScreenRect!(tile);
                        Material? mat = highlighted ? getMaterial!(drawColor) : null;
                        Widgets.DrawTextureRotated(rect, tex, 0f, mat);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Error while drawing landmark at {tile.ToStringSafe()}: {ex}");
                    }
                }
            }

            return false;
        }

        private static bool TryEnsureAccessors()
        {
            if (accessorsInitialised)
            {
                return getLandmarksToShow != null &&
                    getTransitionPct != null &&
                    getLandmarkTex != null &&
                    isHighlighted != null &&
                    hiddenBehindTerrainNow != null &&
                    getMaterial != null &&
                    expandedIconScreenRect != null;
            }

            accessorsInitialised = true;
            getLandmarksToShow = TryBindDelegate<Func<List<PlanetTile>>>("get_LandmarksToShow");
            getTransitionPct = TryBindDelegate<Func<float>>("get_TransitionPct");
            getLandmarkTex = TryBindDelegate<Func<Texture2D>>("get_LandmarkTex");
            isHighlighted = TryBindDelegate<Func<PlanetTile, bool>>("IsHighlighted", new[] { typeof(PlanetTile) });
            hiddenBehindTerrainNow = TryBindDelegate<Func<PlanetTile, bool>>("HiddenBehindTerrainNow", new[] { typeof(PlanetTile) });
            getMaterial = TryBindDelegate<Func<Color, Material>>("GetMaterial", new[] { typeof(Color) });
            expandedIconScreenRect = TryBindDelegate<Func<PlanetTile, Rect>>("ExpandedIconScreenRect", new[] { typeof(PlanetTile) });

            return getLandmarksToShow != null &&
                getTransitionPct != null &&
                getLandmarkTex != null &&
                isHighlighted != null &&
                hiddenBehindTerrainNow != null &&
                getMaterial != null &&
                expandedIconScreenRect != null;
        }

        private static T? TryBindDelegate<T>(string methodName, Type[]? parameters = null) where T : Delegate
        {
            MethodInfo? mi = parameters == null
                ? AccessTools.Method(typeof(ExpandableLandmarksUtility), methodName)
                : AccessTools.Method(typeof(ExpandableLandmarksUtility), methodName, parameters);
            return mi != null ? (T)Delegate.CreateDelegate(typeof(T), mi) : null;
        }
    }

    // ---------------------------------------------------------
    // 4. Geographic Region & Feature Names ("Oceans", "Woodlands")
    // ---------------------------------------------------------
    [HarmonyPatch(typeof(WorldFeatures), "UpdateAlpha")]
    public static class WorldFeatures_UpdateAlpha_Patch
    {
        public static bool Prefix(WorldFeatureTextMesh text, WorldFeature feature)
        {
            if (GeoscapeRules.VisibilityMode != VisibilityMode.ClassicHazeOfWar)
            {
                return true;
            }

            if (SensoryManager.Instance == null || !SensoryManager.Instance.IsFeatureExplored(feature))
            {
                feature.alpha = 0f;
                Color c = text.Color;
                if (c.a > 0f)
                {
                    c.a = 0f;
                    text.Color = c;
                }
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(WorldFeatures), "GoodCameraAltitudeFor")]
    public static class WorldFeatures_GoodCameraAltitudeFor_Patch
    {
        public static void Postfix(WorldFeature feature, ref bool __result)
        {
            if (!__result || GeoscapeRules.VisibilityMode != VisibilityMode.ClassicHazeOfWar)
            {
                return;
            }

            if (SensoryManager.Instance == null || !SensoryManager.Instance.IsFeatureExplored(feature))
            {
                __result = false;
            }
        }
    }

}
