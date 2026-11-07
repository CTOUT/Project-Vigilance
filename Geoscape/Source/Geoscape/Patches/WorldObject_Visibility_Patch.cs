using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Geoscape.Sensory;
using ProjectVigilance.Geoscape.World;
using ProjectVigilance.Geoscape.Orbital;

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
    public static readonly Color UnknownSiteIconColor = new(1f, 0.78f, 0.15f, 0.95f);
    private static Texture2D? cachedUnknownExpandingTex;
    private static Material? cachedUnknownWorldMat;

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

        if (GeoscapeRules.VisibilityMode != VisibilityMode.ClassicHazeOfWar)
        {
            return false;
        }

        if (wo.Faction?.IsPlayer == true)
        {
            return false;
        }

        if (wo is IOrbitalEntity || wo is WorldObject_Satellite || wo is WorldObject_OrbitalStation)
        {
            return !OrbitalVisibilityUtility.IsOrbitalEntityDetected(wo);
        }

        return SensoryManager.Instance != null && !SensoryManager.Instance.IsExplored(wo.Tile);
    }

    public static bool ShouldDrawUpright(WorldObject? wo)
    {
        if (wo == null) return false;
        if (wo is WorldObject_ListeningPost || wo is WorldObject_Incursion) return true;
        if (wo is IOrbitalEntity || wo is WorldObject_Satellite || wo is WorldObject_OrbitalStation) return true;
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

    // ---------------------------------------------------------
    // 1. 2D Expanding Icons (Zoomed Out / Mid Distance)
    // ---------------------------------------------------------
    [HarmonyPatch(typeof(ExpandableWorldObjectsUtility), nameof(ExpandableWorldObjectsUtility.HiddenByRules))]
    public static class HiddenByRules_Patch
    {
        public static void Postfix(WorldObject wo, ref bool __result)
        {
            if (wo is IOrbitalEntity || wo is WorldObject_Satellite || wo is WorldObject_OrbitalStation)
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
            if (wo is IOrbitalEntity || wo is WorldObject_Satellite || wo is WorldObject_OrbitalStation)
            {
                if (ShouldHideWorldObject(wo))
                {
                    __result = 0f;
                    return;
                }

                if (IsValidOrbitalLayerActive())
                {
                    // If viewing the Orbit/space layer (e.g. Odyssey Asteroid or Orbit layer), ensure expanding icon is fully visible
                    PlanetLayer? sel = Find.WorldSelector?.SelectedLayer;
                    if (sel?.Def?.isSpace == true || sel?.Def?.defName == "Orbit")
                    {
                        __result = Mathf.Max(__result, 1f);
                    }
                    else if (__result <= 0f)
                    {
                        // On planetary surface view, fallback to standard expanding icon transition
                        __result = Mathf.Max(__result, ExpandableWorldObjectsUtility.ExpandMoreTransitionPct);
                    }
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

            if (o is IOrbitalEntity || o is WorldObject_Satellite || o is WorldObject_OrbitalStation)
            {
                if (!ShouldHideWorldObject(o) && IsValidOrbitalLayerActive())
                {
                    // Orbital entities in high planetary orbit should only be hidden if physically occluded by the planetary globe
                    __result = WorldRendererUtility.HiddenBehindTerrainNow(o.DrawPos);
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
        public static IEnumerable<MethodBase> TargetMethods() => ResolveDeclaredMethods(
            "ShouldSkip",
            typeof(WorldDrawLayer_WorldObjects_Expandable),
            typeof(WorldDrawLayer_WorldObjects_NonExpandable));

        public static void Postfix(WorldObject worldObject, ref bool __result)
        {
            if (worldObject is IOrbitalEntity || worldObject is WorldObject_Satellite || worldObject is WorldObject_OrbitalStation)
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
        public static bool Prefix(PlanetTile tile)
        {
            if (GeoscapeRules.VisibilityMode != VisibilityMode.ClassicHazeOfWar)
            {
                return true;
            }

            if (SensoryManager.Instance != null && !SensoryManager.Instance.IsExplored(tile))
            {
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(ExpandableLandmarksUtility), "get_LandmarksToShow")]
    public static class LandmarksToShow_Patch
    {
        public static void Postfix(ref List<PlanetTile> __result)
        {
            if (GeoscapeRules.VisibilityMode != VisibilityMode.ClassicHazeOfWar || __result == null || __result.Count == 0)
            {
                return;
            }

            SensoryManager? sensory = SensoryManager.Instance;
            if (sensory == null)
            {
                return;
            }

            // Return a filtered copy rather than mutating vanilla's cachedLandmarksToShow static list in-place
            __result = __result.Where(t => sensory.IsExplored(t)).ToList();
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
            if (o is IOrbitalEntity || o is WorldObject_Satellite || o is WorldObject_OrbitalStation)
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
    // 6. Detection-Only Unknown Site Masking (Settlements, Sites, Outposts, Camps)
    // ---------------------------------------------------------
    [HarmonyPatch]
    public static class UnknownSite_Label_Patch
    {
        public static IEnumerable<MethodBase> TargetMethods() => ResolveDeclaredMethods(
            "get_Label",
            typeof(WorldObject),
            typeof(Settlement),
            typeof(Site),
            typeof(WorldObject_ListeningPost));

        public static bool Prefix(WorldObject __instance, ref string __result)
        {
            if (SensoryManager.ShouldMaskWorldObjectAsUnknown(__instance))
            {
                __result = SensoryNarrativeUtility.GetUnknownSiteLabel();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    public static class UnknownSite_ExpandingIcon_Patch
    {
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
                __result = GetUnknownSiteExpandingTexture();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    public static class UnknownSite_ExpandingIconColor_Patch
    {
        public static IEnumerable<MethodBase> TargetMethods() => ResolveDeclaredMethods(
            "get_ExpandingIconColor",
            typeof(WorldObject),
            typeof(EscapeShip),
            typeof(WorldObject_ListeningPost));

        public static bool Prefix(WorldObject __instance, ref Color __result)
        {
            if (SensoryManager.ShouldMaskWorldObjectAsUnknown(__instance))
            {
                __result = UnknownSiteIconColor;
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
                __result = GetUnknownSiteWorldMaterial();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    public static class UnknownSite_GetInspectString_Patch
    {
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
                __result = SensoryNarrativeUtility.GetUnknownSiteInspectString();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    public static class UnknownSite_GetDescription_Patch
    {
        public static IEnumerable<MethodBase> TargetMethods() => ResolveDeclaredMethods(
            nameof(WorldObject.GetDescription),
            typeof(WorldObject),
            typeof(Site));

        public static bool Prefix(WorldObject __instance, ref string __result)
        {
            if (SensoryManager.ShouldMaskWorldObjectAsUnknown(__instance))
            {
                __result = SensoryNarrativeUtility.GetUnknownSiteDescription();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    public static class UnknownSite_ShowRelatedQuests_Patch
    {
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

            if (Find.WorldSelector.NumSelectedObjects == 0 && Find.WorldSelector.SelectedTile.Valid &&
                !StartingSiteFeedbackUtility.IsTileExplored(Find.WorldSelector.SelectedTile))
            {
                __result = "Unexplored";
                return false;
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

            if (Find.WorldSelector.SelectedTile.Valid &&
                !StartingSiteFeedbackUtility.IsTileExplored(Find.WorldSelector.SelectedTile))
            {
                __result = SensoryNarrativeUtility.IsLowTech
                    ? "Uncharted territory. Send scouts or travellers to explore this region."
                    : "This region is completely unexplored. Dispatch a caravan or establish reconnaissance to chart the terrain.";
                return false;
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
                !StartingSiteFeedbackUtility.IsTileExplored(tile))
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
                !StartingSiteFeedbackUtility.IsTileExplored(tile))
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
        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (GeoscapeRules.VisibilityMode != VisibilityMode.FogOfWar)
            {
                return true;
            }

            PlanetTile mouseTile = GenWorld.MouseTile(false);
            if (mouseTile.Valid && !StartingSiteFeedbackUtility.IsTileExplored(mouseTile))
            {
                return false;
            }

            return true;
        }
    }
}
