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

/// <summary>
/// Harmony patches culling non-player world objects, landmarks, and geographic region names
/// in unexplored sectors under Classic Haze of War, masking detected-but-unidentified sites
/// as "Unidentified site" / "Unknown site", and orienting zoomed-in world object quads upright
/// to match zoomed-out expanding icons.
/// </summary>
[StaticConstructorOnStartup]
public static class WorldObject_Visibility_Patch
{
    private const int MaxVisibilityCacheEntries = 4096;
    public static readonly Color UnknownSiteIconColor = new(1f, 0.78f, 0.15f, 0.95f);
    private static Texture2D? cachedUnknownExpandingTex;
    private static Material? cachedUnknownWorldMat;
    private static readonly Dictionary<int, bool> shouldHideTileCache = new(256);
    private static int shouldHideCacheRevision = -1;
    private static VisibilityMode shouldHideCacheMode = (VisibilityMode)(-1);
    private static SensoryManager? shouldHideCacheSensory;

    public static void ClearVisibilityCaches()
    {
        shouldHideTileCache.Clear();
        shouldHideCacheRevision = -1;
        shouldHideCacheMode = (VisibilityMode)(-1);
        shouldHideCacheSensory = null;
        LandmarksToShow_Patch.ClearCache();
    }

    public static Texture2D GetUnknownSiteExpandingTexture()
    {
        if (cachedUnknownExpandingTex != null)
        {
            return cachedUnknownExpandingTex;
        }

        try
        {
            cachedUnknownExpandingTex =
                ContentFinder<Texture2D>.Get("UI/Overlays/QuestionMark", false) ??
                ContentFinder<Texture2D>.Get("World/WorldObjects/Expanding/Sites/Outpost", false) ??
                ContentFinder<Texture2D>.Get("World/WorldObjects/Sites/GenericSite", false) ??
                BaseContent.BadTex;
        }
        catch
        {
            cachedUnknownExpandingTex = BaseContent.BadTex;
        }

        return cachedUnknownExpandingTex!;
    }

    public static Material GetUnknownSiteWorldMaterial()
    {
        if (cachedUnknownWorldMat != null)
        {
            return cachedUnknownWorldMat;
        }

        try
        {
            Texture2D tex = GetUnknownSiteExpandingTexture();
            cachedUnknownWorldMat = MaterialPool.MatFrom(
                tex,
                ShaderDatabase.WorldOverlayTransparentLit,
                UnknownSiteIconColor,
                WorldMaterials.WorldObjectRenderQueue);
        }
        catch
        {
            cachedUnknownWorldMat = BaseContent.BadMat;
        }

        return cachedUnknownWorldMat!;
    }

    private static IEnumerable<MethodBase> ResolveDeclaredMethods(string methodName, params Type[] types)
    {
        for (int i = 0; i < types.Length; i++)
        {
            MethodInfo? m = AccessTools.DeclaredMethod(types[i], methodName);
            if (m != null && !m.IsAbstract)
            {
                yield return m;
            }
        }
    }

    /// <summary>
    /// Evaluates whether a WorldObject should be completely concealed from rendering, selection, and mouse hover
    /// under the current campaign visibility rules and sensory detection state.
    /// Non-player orbital entities require active technological space-tracking sensors; surface entities require explored hexes.
    /// </summary>
    public static bool ShouldHideWorldObject(WorldObject? wo)
    {
        if (wo == null) return false;

        VisibilityMode mode = GeoscapeRules.VisibilityMode;
        if (mode == VisibilityMode.Omniscient)
        {
            return false;
        }

        if (wo.Faction?.IsPlayer == true)
        {
            return false;
        }

        if (wo is IOrbitalEntity)
        {
            return !VigilanceModuleManager.IsOrbitalEntityDetected(wo);
        }

        if (mode != VisibilityMode.ClassicHazeOfWar)
        {
            return false;
        }

        SensoryManager? sensory = SensoryManager.Instance;
        if (sensory == null)
        {
            return false;
        }

        if (SensoryManager.IsExploredOverride != null)
        {
            return !sensory.IsExplored(wo.Tile);
        }

        PlanetTile tile = wo.Tile;
        if (!tile.Valid)
        {
            return false;
        }

        int revision = sensory.SweepRevision;
        if (shouldHideCacheRevision != revision ||
            shouldHideCacheMode != mode ||
            !ReferenceEquals(shouldHideCacheSensory, sensory))
        {
            shouldHideTileCache.Clear();
            shouldHideCacheRevision = revision;
            shouldHideCacheMode = mode;
            shouldHideCacheSensory = sensory;
        }

        int tileId = tile.tileId;
        if (shouldHideTileCache.TryGetValue(tileId, out bool cachedHidden))
        {
            return cachedHidden;
        }

        bool hidden = !sensory.IsExplored(tile);
        if (shouldHideTileCache.Count < MaxVisibilityCacheEntries)
        {
            shouldHideTileCache[tileId] = hidden;
        }

        return hidden;
    }

    public static bool ShouldDrawUpright(WorldObject? wo)
    {
        if (wo == null) return false;
        if (wo is WorldObject_ListeningPost || wo is WorldObject_Incursion) return true;
        if (wo is IOrbitalEntity) return true;
        if (SensoryManager.ShouldMaskWorldObjectAsUnknown(wo)) return true;
        if (wo.def != null && wo.def.expandingIcon && wo.def.texture == wo.def.expandingIconTexture) return true;
        return false;
    }

    // ---------------------------------------------------------
    // 0. Upright Zoomed-In World Object Icon Orientation
    // ---------------------------------------------------------
    [HarmonyPatch(typeof(WorldObject), nameof(WorldObject.Draw))]
    [StaticConstructorOnStartup]
    public static class WorldObject_Draw_Upright_Patch
    {
        private static MaterialPropertyBlock? sharedPropertyBlock;

        public static bool Prefix(WorldObject __instance)
        {
            if (ShouldHideWorldObject(__instance))
            {
                return false;
            }

            if (!ShouldDrawUpright(__instance))
            {
                return true;
            }

            float averageTileSize = Find.WorldGrid.AverageTileSize;
            float rawTransitionPct = ExpandableWorldObjectsUtility.RawTransitionPct;
            Material? material = __instance.Material;
            if (material == null)
            {
                return false;
            }

            if (__instance.def.expandingIcon && rawTransitionPct > 0f)
            {
                Color color = material.color;
                float num = 1f - rawTransitionPct;
                sharedPropertyBlock ??= new MaterialPropertyBlock();
                sharedPropertyBlock.SetColor(ShaderPropertyIDs.Color, new Color(color.r, color.g, color.b, color.a * num));
                WorldRendererUtility.DrawQuadTangentialToPlanet(
                    __instance.DrawPos,
                    0.7f * averageTileSize,
                    __instance.DrawAltitude,
                    material,
                    WorldObject_ListeningPost.UprightQuadRotationAngle,
                    counterClockwise: false,
                    useSkyboxLayer: false,
                    sharedPropertyBlock);
                return false;
            }

            WorldRendererUtility.DrawQuadTangentialToPlanet(
                __instance.DrawPos,
                0.7f * averageTileSize,
                __instance.DrawAltitude,
                material,
                WorldObject_ListeningPost.UprightQuadRotationAngle);
            return false;
        }
    }

    [HarmonyPatch(typeof(WorldObject), nameof(WorldObject.Print))]
    public static class WorldObject_Print_Upright_Patch
    {
        public static bool Prefix(WorldObject __instance, LayerSubMesh subMesh)
        {
            if (ShouldHideWorldObject(__instance))
            {
                return false;
            }

            if (!ShouldDrawUpright(__instance))
            {
                return true;
            }

            float averageTileSize = Find.WorldGrid.AverageTileSize;
            WorldRendererUtility.PrintQuadTangentialToPlanet(
                __instance.DrawPos,
                0.7f * averageTileSize,
                __instance.DrawAltitude,
                subMesh,
                counterClockwise: false,
                WorldObject_ListeningPost.UprightPrintRotationAngle,
                printUVs: true);
            return false;
        }
    }

    /// <summary>
    /// Checks whether the currently selected camera layer is either the planetary Surface
    /// or an orbital/space layer (e.g. Odyssey's Orbit layer), enabling seamless cross-layer
    /// rendering of orbital entities.
    /// </summary>
    public static bool IsValidOrbitalLayerActive()
    {
        if (Find.World == null || Find.WorldSelector == null) return true;
        PlanetLayer? sel = Find.WorldSelector.SelectedLayer;
        if (sel == null) return true;
        return sel == Find.WorldGrid?.Surface || sel.Def?.isSpace == true || sel.Def?.defName == "Orbit";
    }

    /// <summary>
    /// Identifies ground entities that remain visible as strategic uplink anchors when viewing the Orbit layer.
    /// Player colonies (MapParent) stay visible; tactical ground foot/animal caravans and unmanned outposts
    /// remain restricted to the Surface layer so they do not clutter colony icons from space.
    /// </summary>
    public static bool IsSurfaceAnchorVisibleFromOrbit(WorldObject? wo) =>
        wo?.Faction?.IsPlayer == true && wo is MapParent;

    // ---------------------------------------------------------
    // 1. 2D Expanding Icons (Zoomed Out / Mid Distance)
    // ---------------------------------------------------------
    [HarmonyPatch(typeof(ExpandableWorldObjectsUtility), nameof(ExpandableWorldObjectsUtility.HiddenByRules))]
    public static class HiddenByRules_Patch
    {
        public static void Postfix(WorldObject wo, ref bool __result)
        {
            if (wo is IOrbitalEntity)
            {
                if (ShouldHideWorldObject(wo))
                {
                    __result = true;
                    return;
                }

                // If detected / player-owned, allow rendering across both Surface and Orbit views
                if (IsValidOrbitalLayerActive())
                {
                    __result = false;
                    return;
                }
            }

            // Maintain visibility of player strategic colonies (MapParent) when viewing the orbital planet layer
            if (IsSurfaceAnchorVisibleFromOrbit(wo))
            {
                if (IsValidOrbitalLayerActive())
                {
                    __result = false;
                    return;
                }
            }

            if (__result)
            {
                return;
            }

            if (ShouldHideWorldObject(wo))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(ExpandableWorldObjectsUtility), nameof(ExpandableWorldObjectsUtility.TransitionPct))]
    public static class TransitionPct_Patch
    {
        public static void Postfix(WorldObject wo, ref float __result)
        {
            if (wo is IOrbitalEntity)
            {
                if (ShouldHideWorldObject(wo))
                {
                    __result = 0f;
                    return;
                }

                if (IsValidOrbitalLayerActive())
                {
                    // Orbital entities in high planetary orbit maintain full expanding icon visibility across both surface and space views
                    __result = 1f;
                }
            }
            else if (IsSurfaceAnchorVisibleFromOrbit(wo))
            {
                // When viewing the orbital planet layer, ensure player strategic colonies remain fully expanded
                PlanetLayer? sel = Find.WorldSelector?.SelectedLayer;
                if (sel?.Def?.isSpace == true || sel?.Def?.defName == "Orbit")
                {
                    __result = Mathf.Max(__result, 1f);
                }
            }
        }
    }

    [HarmonyPatch(typeof(WorldObjectSelectionUtility), nameof(WorldObjectSelectionUtility.HiddenBehindTerrainNow), typeof(WorldObject))]
    public static class HiddenBehindTerrainNow_Patch
    {
        public static void Postfix(WorldObject o, ref bool __result)
        {
            if (!__result)
            {
                return;
            }

            if (o is IOrbitalEntity)
            {
                if (!ShouldHideWorldObject(o) && IsValidOrbitalLayerActive())
                {
                    // Orbital entities in high planetary orbit should only be hidden if physically occluded by the planetary globe
                    if (Current.ProgramState == ProgramState.Playing && Current.Game != null && Find.WorldCameraDriver != null)
                    {
                        __result = WorldRendererUtility.HiddenBehindTerrainNow(o.DrawPos);
                    }
                    else
                    {
                        __result = false;
                    }
                    return;
                }
            }

            // Maintain visibility of player strategic colonies (MapParent) when viewing the orbital planet layer
            if (IsSurfaceAnchorVisibleFromOrbit(o))
            {
                if (IsValidOrbitalLayerActive())
                {
                    // Player surface colonies remain visible on the globe in orbit view
                    // unless physically occluded behind planetary terrain on the far side of the planet
                    if (Current.ProgramState == ProgramState.Playing && Current.Game != null && Find.WorldCameraDriver != null)
                    {
                        __result = WorldRendererUtility.HiddenBehindTerrainNow(o.DrawPos);
                    }
                    else
                    {
                        __result = false;
                    }
                }
            }
        }
    }

    // ---------------------------------------------------------
    // 2. 3D World Object Mesh Rendering (Zoomed In)
    // ---------------------------------------------------------
    [HarmonyPatch]
    public static class WorldObjects_ShouldSkip_Patch
    {
        public static bool Prepare() => TargetMethods().Any();

        public static IEnumerable<MethodBase> TargetMethods() => ResolveDeclaredMethods(
            "ShouldSkip",
            typeof(WorldDrawLayer_WorldObjects_Expandable),
            typeof(WorldDrawLayer_WorldObjects_NonExpandable));

        public static void Postfix(WorldObject worldObject, ref bool __result)
        {
            if (worldObject is IOrbitalEntity)
            {
                if (ShouldHideWorldObject(worldObject))
                {
                    __result = true;
                    return;
                }

                if (__result && IsValidOrbitalLayerActive())
                {
                    __result = false;
                    return;
                }
            }

            if (__result)
            {
                return;
            }

            if (ShouldHideWorldObject(worldObject))
            {
                __result = true;
            }
        }
    }

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

            GUI.color = Color.white;
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

    // ---------------------------------------------------------
    // 5. World Object Selection & Hover Overlays
    // ---------------------------------------------------------
    [HarmonyPatch(typeof(WorldObject), "get_SelectableNow")]
    public static class SelectableNow_Patch
    {
        public static void Postfix(WorldObject __instance, ref bool __result)
        {
            if (!__result)
            {
                return;
            }

            if (ShouldHideWorldObject(__instance))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(WorldObjectSelectionUtility), nameof(WorldObjectSelectionUtility.VisibleToCameraNow))]
    public static class VisibleToCameraNow_Patch
    {
        public static void Postfix(WorldObject o, ref bool __result)
        {
            if (o is IOrbitalEntity)
            {
                if (ShouldHideWorldObject(o))
                {
                    __result = false;
                    return;
                }

                if (!__result && IsValidOrbitalLayerActive())
                {
                    __result = true;
                    return;
                }
            }

            if (!__result)
            {
                return;
            }

            if (ShouldHideWorldObject(o))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(GenWorldUI), nameof(GenWorldUI.WorldObjectsUnderMouse))]
    public static class WorldObjectsUnderMouse_Patch
    {
        public static void Postfix(List<WorldObject> __result)
        {
            if (__result == null || __result.Count == 0)
            {
                return;
            }

            for (int i = __result.Count - 1; i >= 0; i--)
            {
                WorldObject wo = __result[i];
                if (ShouldHideWorldObject(wo))
                {
                    __result.RemoveAt(i);
                }
            }
        }
    }

    [HarmonyPatch(typeof(WorldSelector), nameof(WorldSelector.Select), typeof(WorldObject), typeof(bool))]
    public static class WorldSelector_Select_Patch
    {
        public static bool Prefix(WorldObject obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (ShouldHideWorldObject(obj))
            {
                return false;
            }

            if (!obj.SelectableNow)
            {
                return false;
            }

            if (obj is WorldObject_Incursion incursion)
            {
                if (GeoscapeRules.VisibilityMode != VisibilityMode.Omniscient &&
                    incursion.CurrentIntelligenceTier < IntelligenceTier.Contact)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Automatically drops selection on any non-player WorldObject that drifts outside active sensor
    /// detection (e.g. an orbital satellite leaving radar coverage or an incursion fading from contact).
    /// Zero-allocation O(1) bailout when nothing is selected or Omniscient mode is active.
    /// </summary>
    public static int PruneHiddenSelectedObjects(WorldSelector? selector)
    {
        if (selector == null || selector.NumSelectedObjects == 0)
        {
            return 0;
        }

        if (GeoscapeRules.VisibilityMode == VisibilityMode.Omniscient)
        {
            return 0;
        }

        List<WorldObject> selected = selector.SelectedObjects;
        int deselected = 0;
        for (int i = selected.Count - 1; i >= 0; i--)
        {
            WorldObject wo = selected[i];
            if (wo == null)
            {
                continue;
            }

            bool lostContact = ShouldHideWorldObject(wo) ||
                (wo is WorldObject_Incursion incursion && incursion.CurrentIntelligenceTier < IntelligenceTier.Contact);

            if (lostContact)
            {
                selector.Deselect(wo);
                deselected++;
            }
        }

        return deselected;
    }

    [HarmonyPatch(typeof(WorldInterface), nameof(WorldInterface.WorldInterfaceUpdate))]
    public static class WorldInterface_Update_PruneHiddenSelection_Patch
    {
        public static void Prefix(WorldInterface __instance)
        {
            PruneHiddenSelectedObjects(__instance?.selector);
        }
    }

    [HarmonyPatch(typeof(WorldObjectSelectionUtility), nameof(WorldObjectSelectionUtility.MultiSelectableWorldObjectsInScreenRectDistinct))]
    public static class MultiSelectableWorldObjectsInScreenRectDistinct_Patch
    {
        public static void Postfix(ref IEnumerable<WorldObject> __result)
        {
            if (__result == null)
            {
                return;
            }

            __result = FilterSelectable(__result);
        }

        private static IEnumerable<WorldObject> FilterSelectable(IEnumerable<WorldObject> source)
        {
            bool isOmniscient = GeoscapeRules.VisibilityMode == VisibilityMode.Omniscient;

            foreach (WorldObject obj in source)
            {
                if (!obj.SelectableNow)
                {
                    continue;
                }

                if (obj is WorldObject_Incursion inc && !isOmniscient && inc.CurrentIntelligenceTier < IntelligenceTier.Contact)
                {
                    continue;
                }

                if (ShouldHideWorldObject(obj))
                {
                    continue;
                }

                yield return obj;
            }
        }
    }

    // ---------------------------------------------------------
    // 6. Detection-Only Unknown Site Masking (Settlements, Sites, Outposts, Camps, Landmarks)
    // ---------------------------------------------------------
    [HarmonyPatch]
    public static class UnknownSite_Label_Patch
    {
        public static bool Prepare() => TargetMethods().Any();

        public static IEnumerable<MethodBase> TargetMethods() => ResolveDeclaredMethods(
            "get_Label",
            typeof(WorldObject),
            typeof(Settlement),
            typeof(Site),
            typeof(Caravan),
            typeof(Gravship),
            typeof(RoutePlannerWaypoint),
            typeof(SpaceMapParent),
            typeof(WorldObject_ListeningPost));

        public static bool Prefix(WorldObject __instance, ref string __result)
        {
            if (SensoryManager.ShouldMaskWorldObjectAsUnknown(__instance))
            {
                __result = LandmarkSensoryUtility.IsAnomalousStructure(__instance)
                    ? SensoryNarrativeUtility.GetUnknownLandmarkLabel()
                    : SensoryNarrativeUtility.GetUnknownSiteLabel();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    public static class UnknownSite_ExpandingIcon_Patch
    {
        public static bool Prepare() => TargetMethods().Any();

        public static IEnumerable<MethodBase> TargetMethods() => ResolveDeclaredMethods(
            "get_ExpandingIcon",
            typeof(WorldObject),
            typeof(Settlement),
            typeof(Site),
            typeof(EscapeShip));

        public static bool Prefix(WorldObject __instance, ref Texture2D __result)
        {
            if (SensoryManager.ShouldMaskWorldObjectAsUnknown(__instance))
            {
                __result = LandmarkSensoryUtility.IsAnomalousStructure(__instance)
                    ? LandmarkSensoryUtility.GetUnknownLandmarkExpandingTexture()
                    : GetUnknownSiteExpandingTexture();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    public static class UnknownSite_ExpandingIconColor_Patch
    {
        public static bool Prepare() => TargetMethods().Any();

        public static IEnumerable<MethodBase> TargetMethods() => ResolveDeclaredMethods(
            "get_ExpandingIconColor",
            typeof(WorldObject),
            typeof(EscapeShip),
            typeof(WorldObject_ListeningPost));

        public static bool Prefix(WorldObject __instance, ref Color __result)
        {
            if (SensoryManager.ShouldMaskWorldObjectAsUnknown(__instance))
            {
                __result = LandmarkSensoryUtility.IsAnomalousStructure(__instance)
                    ? LandmarkSensoryUtility.TacticalCyan
                    : UnknownSiteIconColor;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(WorldObject), "get_ExpandingMaterial")]
    public static class UnknownSite_ExpandingMaterial_Patch
    {
        public static bool Prefix(WorldObject __instance, ref Material? __result)
        {
            if (SensoryManager.ShouldMaskWorldObjectAsUnknown(__instance))
            {
                __result = null;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    public static class UnknownSite_Material_Patch
    {
        public static bool Prepare() => TargetMethods().Any();

        public static IEnumerable<MethodBase> TargetMethods() => ResolveDeclaredMethods(
            "get_Material",
            typeof(WorldObject),
            typeof(Settlement),
            typeof(Site),
            typeof(Camp),
            typeof(PeaceTalks),
            typeof(EscapeShip),
            typeof(WorldObject_ListeningPost));

        public static bool Prefix(WorldObject __instance, ref Material __result)
        {
            if (SensoryManager.ShouldMaskWorldObjectAsUnknown(__instance))
            {
                __result = LandmarkSensoryUtility.IsAnomalousStructure(__instance)
                    ? LandmarkSensoryUtility.GetUnknownLandmarkWorldMaterial()
                    : GetUnknownSiteWorldMaterial();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    public static class UnknownSite_GetInspectString_Patch
    {
        public static bool Prepare() => TargetMethods().Any();

        public static IEnumerable<MethodBase> TargetMethods() => ResolveDeclaredMethods(
            nameof(WorldObject.GetInspectString),
            typeof(WorldObject),
            typeof(MapParent),
            typeof(Settlement),
            typeof(Site),
            typeof(AbandonedSettlement),
            typeof(WorldObject_EncounterAftermath),
            typeof(WorldObject_ListeningPost));

        public static bool Prefix(WorldObject __instance, ref string __result)
        {
            if (SensoryManager.ShouldMaskWorldObjectAsUnknown(__instance))
            {
                __result = LandmarkSensoryUtility.IsAnomalousStructure(__instance)
                    ? SensoryNarrativeUtility.GetUnknownLandmarkInspectString()
                    : SensoryNarrativeUtility.GetUnknownSiteInspectString();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    public static class UnknownSite_GetDescription_Patch
    {
        public static bool Prepare() => TargetMethods().Any();

        public static IEnumerable<MethodBase> TargetMethods() => ResolveDeclaredMethods(
            nameof(WorldObject.GetDescription),
            typeof(WorldObject),
            typeof(Site));

        public static bool Prefix(WorldObject __instance, ref string __result)
        {
            if (SensoryManager.ShouldMaskWorldObjectAsUnknown(__instance))
            {
                __result = LandmarkSensoryUtility.IsAnomalousStructure(__instance)
                    ? SensoryNarrativeUtility.GetUnknownLandmarkDescription()
                    : SensoryNarrativeUtility.GetUnknownSiteDescription();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    public static class UnknownSite_ShowRelatedQuests_Patch
    {
        public static bool Prepare() => TargetMethods().Any();

        public static IEnumerable<MethodBase> TargetMethods() => ResolveDeclaredMethods(
            "get_ShowRelatedQuests",
            typeof(WorldObject),
            typeof(Settlement));

        public static bool Prefix(WorldObject __instance, ref bool __result)
        {
            if (SensoryManager.ShouldMaskWorldObjectAsUnknown(__instance))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    public static class UnknownSite_GetGizmos_Patch
    {
        public static bool Prepare() => TargetMethods().Any();

        public static IEnumerable<MethodBase> TargetMethods() => ResolveDeclaredMethods(
            nameof(WorldObject.GetGizmos),
            typeof(WorldObject),
            typeof(MapParent),
            typeof(Settlement),
            typeof(Site),
            typeof(WorldObject_ListeningPost));

        public static bool Prefix(WorldObject __instance, ref IEnumerable<Gizmo> __result)
        {
            if (SensoryManager.ShouldMaskWorldObjectAsUnknown(__instance))
            {
                __result = Enumerable.Empty<Gizmo>();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    public static class UnknownSite_GetFloatMenuOptions_Patch
    {
        public static bool Prepare() => TargetMethods().Any();

        public static IEnumerable<MethodBase> TargetMethods() => ResolveDeclaredMethods(
            nameof(WorldObject.GetFloatMenuOptions),
            typeof(WorldObject),
            typeof(MapParent),
            typeof(Settlement),
            typeof(Site),
            typeof(PeaceTalks),
            typeof(WorldObject_EncounterAftermath),
            typeof(WorldObject_ListeningPost));

        public static bool Prefix(WorldObject __instance, Caravan caravan, ref IEnumerable<FloatMenuOption> __result)
        {
            if (SensoryManager.ShouldMaskWorldObjectAsUnknown(__instance))
            {
                __result = BuildInvestigateFloatMenuOptions(__instance, caravan);
                return false;
            }
            return true;
        }

        private static IEnumerable<FloatMenuOption> BuildInvestigateFloatMenuOptions(WorldObject site, Caravan caravan)
        {
            if (caravan == null || !caravan.IsPlayerControlled || !site.Tile.Valid)
            {
                yield break;
            }

            string label = $"Investigate {SensoryNarrativeUtility.GetUnknownSiteLabel()} (Tile {site.Tile.tileId})";
            yield return new FloatMenuOption(label, () =>
            {
                if (caravan.Spawned && caravan.pather != null)
                {
                    caravan.pather.StartPath(site.Tile, null, repathImmediately: true);
                }
            });
        }
    }

    // ---------------------------------------------------------
    // 7. Tile Terrain & World Inspect Pane Shrouding
    // ---------------------------------------------------------
    [HarmonyPatch(typeof(WorldInspectPane), "GetLabel")]
    public static class WorldInspectPane_GetLabel_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(ref string __result)
        {
            if (GeoscapeRules.VisibilityMode != VisibilityMode.FogOfWar)
            {
                return true;
            }

            if (Find.WorldSelector.NumSelectedObjects == 0 && Find.WorldSelector.SelectedTile.Valid)
            {
                PlanetTile tile = Find.WorldSelector.SelectedTile;
                TileIntelLevel intel = TerrainTelemetryUtility.GetTileIntelLevel(tile);
                if (intel == TileIntelLevel.Unexplored)
                {
                    __result = "Unexplored";
                    return false;
                }

                if (intel == TileIntelLevel.DetectedOnly)
                {
                    BiomeDef? biome = Find.WorldGrid?[tile]?.PrimaryBiome;
                    if (biome != null)
                    {
                        __result = biome.LabelCap;
                        return false;
                    }
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(WorldInspectPane), "TileInspectString", MethodType.Getter)]
    public static class WorldInspectPane_TileInspectString_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(ref string __result)
        {
            if (GeoscapeRules.VisibilityMode != VisibilityMode.FogOfWar)
            {
                return true;
            }

            PlanetTile tile = Find.WorldSelector.SelectedTile;
            if (tile.Valid)
            {
                TileIntelLevel intel = TerrainTelemetryUtility.GetTileIntelLevel(tile);
                if (intel == TileIntelLevel.Unexplored)
                {
                    __result = SensoryNarrativeUtility.IsLowTech
                        ? "Uncharted territory. Send scouts or travellers to explore this region."
                        : "This region is completely unexplored. Dispatch a caravan or establish reconnaissance to chart the terrain.";
                    return false;
                }

                if (intel == TileIntelLevel.DetectedOnly)
                {
                    __result = TerrainTelemetryUtility.BuildDetectedTileInspectString(tile);
                    return false;
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(WorldInspectPane), nameof(WorldInspectPane.DoInspectPaneButtons))]
    public static class WorldInspectPane_DoInspectPaneButtons_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Rect rect, ref float lineEndWidth)
        {
            if (GeoscapeRules.VisibilityMode != VisibilityMode.FogOfWar)
            {
                return true;
            }

            if (Find.WorldSelector.SingleSelectedObject != null &&
                SensoryManager.ShouldMaskWorldObjectAsUnknown(Find.WorldSelector.SingleSelectedObject))
            {
                return false;
            }

            if (Find.WorldSelector.NumSelectedObjects == 0 && Find.WorldSelector.SelectedTile.Valid &&
                !StartingSiteFeedbackUtility.IsTileExplored(Find.WorldSelector.SelectedTile))
            {
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(WorldInspectPane), "CurTabs", MethodType.Getter)]
    public static class WorldInspectPane_CurTabs_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ref IEnumerable<InspectTabBase> __result)
        {
            if (GeoscapeRules.VisibilityMode != VisibilityMode.FogOfWar)
            {
                return;
            }

            if (Find.WorldSelector.SingleSelectedObject != null &&
                SensoryManager.ShouldMaskWorldObjectAsUnknown(Find.WorldSelector.SingleSelectedObject))
            {
                __result = Enumerable.Empty<InspectTabBase>();
                return;
            }

            if (Find.WorldSelector.NumSelectedObjects == 0 && Find.WorldSelector.SelectedTile.Valid &&
                !StartingSiteFeedbackUtility.IsTileExplored(Find.WorldSelector.SelectedTile))
            {
                __result = Enumerable.Empty<InspectTabBase>();
            }
        }
    }

    [HarmonyPatch(typeof(WITab_Terrain), "IsVisible", MethodType.Getter)]
    public static class WITab_Terrain_IsVisible_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ref bool __result)
        {
            if (!__result || GeoscapeRules.VisibilityMode != VisibilityMode.FogOfWar)
            {
                return;
            }

            PlanetTile tile = Find.WorldSelector.SelectedTile;
            if (tile.Valid && Find.WorldSelector.NumSelectedObjects == 0 &&
                TerrainTelemetryUtility.GetTileIntelLevel(tile) != TileIntelLevel.Identified)
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(WITab_Terrain), "FillTab")]
    public static class WITab_Terrain_FillTab_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (GeoscapeRules.VisibilityMode != VisibilityMode.FogOfWar)
            {
                return true;
            }

            PlanetTile tile = Find.WorldSelector.SelectedTile;
            if (tile.Valid && Find.WorldSelector.NumSelectedObjects == 0 &&
                TerrainTelemetryUtility.GetTileIntelLevel(tile) != TileIntelLevel.Identified)
            {
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(WITab_Planet), "IsVisible", MethodType.Getter)]
    public static class WITab_Planet_IsVisible_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ref bool __result)
        {
            if (!__result || GeoscapeRules.VisibilityMode != VisibilityMode.FogOfWar)
            {
                return;
            }

            PlanetTile tile = Find.WorldSelector.SelectedTile;
            if (tile.Valid && Find.WorldSelector.NumSelectedObjects == 0 &&
                !StartingSiteFeedbackUtility.IsTileExplored(tile))
            {
                __result = false;
            }
        }
    }

    // ---------------------------------------------------------
    // 8. Cell Inspector Shrouding on World Map (Left Alt)
    // ---------------------------------------------------------
    [HarmonyPatch(typeof(CellInspectorDrawer), "DrawWorldInspector")]
    public static class CellInspectorDrawer_DrawWorldInspector_Patch
    {
        private static bool delegatesInitialised;
        private static Action<string>? drawHeader;
        private static Action<string, string>? drawRow;
        private static Action? drawDivider;

        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (GeoscapeRules.VisibilityMode != VisibilityMode.FogOfWar)
            {
                return true;
            }

            PlanetTile mouseTile = GenWorld.MouseTile(false);
            if (!mouseTile.Valid)
            {
                return true;
            }

            TileIntelLevel intel = TerrainTelemetryUtility.GetTileIntelLevel(mouseTile);
            if (intel == TileIntelLevel.Unexplored)
            {
                return false;
            }

            if (intel == TileIntelLevel.DetectedOnly)
            {
                return !TryDrawDetectedOnlyInspector(mouseTile);
            }

            return true;
        }

        private static bool TryDrawDetectedOnlyInspector(PlanetTile mouseTile)
        {
            if (!TryEnsureDrawDelegates())
            {
                return false;
            }

            Tile? tile = Find.WorldGrid?[mouseTile];
            if (tile == null)
            {
                return false;
            }

            List<WorldObject>? worldObjects = GenWorldUI.WorldObjectsUnderMouse(UI.MousePositionOnUI);
            if (worldObjects != null)
            {
                for (int i = 0; i < worldObjects.Count; i++)
                {
                    WorldObject wo = worldObjects[i];
                    if (wo == null || ShouldHideWorldObject(wo))
                    {
                        continue;
                    }

                    if (wo is WorldObject_Incursion incursion && incursion.CurrentIntelligenceTier < IntelligenceTier.Contact)
                    {
                        continue;
                    }

                    drawHeader!(wo.LabelCap);
                    if (wo is Caravan caravan)
                    {
                        DrawCaravanInspectorRows(caravan);
                    }
                    else if (wo is Settlement settlement &&
                             !SensoryManager.ShouldMaskWorldObjectAsUnknown(settlement) &&
                             settlement.Faction != null)
                    {
                        DrawSettlementFactionRows(settlement);
                    }

                    drawDivider!();
                }
            }

            drawRow!("Biome_Label".Translate(), tile.PrimaryBiome.LabelCap);
            if (!tile.PrimaryBiome.impassable && tile.hilliness != Hilliness.Undefined)
            {
                drawRow!("Hilliness_Label".Translate(), tile.hilliness.GetLabelCap());
            }

            if (tile is SurfaceTile surfaceTile)
            {
                if (!surfaceTile.Roads.NullOrEmpty())
                {
                    RoadDef? road = surfaceTile.Roads.Select(rl => rl.road).MaxBy(r => r.priority);
                    if (road != null)
                    {
                        drawRow!("Road_Label".Translate(), road.LabelCap);
                    }
                }

                if (!surfaceTile.Rivers.NullOrEmpty() && surfaceTile.Rivers[0].river != null)
                {
                    drawRow!("River_Label".Translate(), surfaceTile.Rivers[0].river.LabelCap);
                }
            }

            drawRow!("AvgTemp".Translate(), TerrainTelemetryUtility.FormatEstimatedTemperature(mouseTile));

            if (TerrainTelemetryUtility.HasUnidentifiedManMadeLandmark(mouseTile))
            {
                drawRow!("Signature", "Unidentified landmark");
            }

            return true;
        }

        private static void DrawCaravanInspectorRows(Caravan caravan)
        {
            drawRow!("CaravanColonists_Label".Translate(), caravan.pawns.Count<Pawn>(p => p.IsColonist).ToString());
            if (caravan.pather.Moving)
            {
                if (!caravan.pather.MovingNow)
                {
                    drawRow!("CaravanStatus_Label".Translate(), CaravanBedUtility.AppendUsingBedsLabel("CaravanResting".Translate(), caravan.beds.GetUsedBedCount()));
                }
                else if (caravan.pather.ArrivalAction != null)
                {
                    drawRow!("CaravanStatus_Label".Translate(), caravan.pather.ArrivalAction.ReportString);
                }
                else
                {
                    drawRow!("CaravanStatus_Label".Translate(), "CaravanTraveling".Translate());
                }

                float days = (float)CaravanArrivalTimeEstimator.EstimatedTicksToArrive(caravan, true) / 60000f;
                drawRow!("CaravanTTD_Label".Translate(), days.ToString("0.#"));
            }
            else
            {
                Settlement? visited = CaravanVisitUtility.SettlementVisitedNow(caravan);
                drawRow!(
                    "CaravanStatus_Label".Translate(),
                    visited != null ? "CaravanVisiting".Translate(visited.Label) : "CaravanWaiting".Translate());
            }
        }

        private static void DrawSettlementFactionRows(Settlement settlement)
        {
            drawRow!("Faction_Label".Translate(), settlement.Faction!.Name);
            if (settlement.Faction != Faction.OfPlayer)
            {
                string relation = settlement.Faction.Hidden
                    ? settlement.Faction.PlayerRelationKind.GetLabelCap()
                    : settlement.Faction.PlayerRelationKind.GetLabelCap() + " (" + settlement.Faction.PlayerGoodwill.ToStringWithSign() + ")";
                drawRow!("Relationship_Label".Translate(), relation);
            }
        }

        private static bool TryEnsureDrawDelegates()
        {
            if (delegatesInitialised)
            {
                return drawHeader != null && drawRow != null && drawDivider != null;
            }

            delegatesInitialised = true;
            MethodInfo? headerMi = AccessTools.Method(typeof(CellInspectorDrawer), "DrawHeader", new[] { typeof(string) });
            MethodInfo? rowMi = AccessTools.Method(typeof(CellInspectorDrawer), "DrawRow", new[] { typeof(string), typeof(string) });
            MethodInfo? dividerMi = AccessTools.Method(typeof(CellInspectorDrawer), "DrawDivider");

            if (headerMi != null)
            {
                drawHeader = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), headerMi);
            }

            if (rowMi != null)
            {
                drawRow = (Action<string, string>)Delegate.CreateDelegate(typeof(Action<string, string>), rowMi);
            }

            if (dividerMi != null)
            {
                drawDivider = (Action)Delegate.CreateDelegate(typeof(Action), dividerMi);
            }

            return drawHeader != null && drawRow != null && drawDivider != null;
        }
    }
}
