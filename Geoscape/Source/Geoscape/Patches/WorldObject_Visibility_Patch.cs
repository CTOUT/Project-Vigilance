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
public static partial class WorldObject_Visibility_Patch
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

        if (mode != VisibilityMode.FogOfWar)
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

}
