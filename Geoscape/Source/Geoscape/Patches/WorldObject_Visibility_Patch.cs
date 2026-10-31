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

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Harmony patches culling non-player world objects, landmarks, and geographic region names
/// in unexplored sectors under Classic Haze of War, masking detected-but-unidentified sites
/// as "Unidentified site" / "Unknown site", and orienting zoomed-in world object quads upright
/// to match zoomed-out expanding icons.
/// </summary>
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

    public static bool ShouldDrawUpright(WorldObject? wo)
    {
        if (wo == null) return false;
        if (wo is WorldObject_ListeningPost || wo is WorldObject_Incursion) return true;
        if (SensoryManager.ShouldMaskWorldObjectAsUnknown(wo)) return true;
        if (wo.def != null && wo.def.expandingIcon && wo.def.texture == wo.def.expandingIconTexture) return true;
        return false;
    }

    // ---------------------------------------------------------
    // 0. Upright Zoomed-In World Object Icon Orientation
    // ---------------------------------------------------------
    [HarmonyPatch(typeof(WorldObject), nameof(WorldObject.Draw))]
    public static class WorldObject_Draw_Upright_Patch
    {
        private static MaterialPropertyBlock? sharedPropertyBlock;

        public static bool Prefix(WorldObject __instance)
        {
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

    // ---------------------------------------------------------
    // 1. 2D Expanding Icons (Zoomed Out / Mid Distance)
    // ---------------------------------------------------------
    [HarmonyPatch(typeof(ExpandableWorldObjectsUtility), nameof(ExpandableWorldObjectsUtility.HiddenByRules))]
    public static class HiddenByRules_Patch
    {
        public static void Postfix(WorldObject wo, ref bool __result)
        {
            if (__result || GeoscapeMod.Settings.visibilityMode != VisibilityMode.ClassicHazeOfWar)
            {
                return;
            }

            if (wo.Faction?.IsPlayer == true)
            {
                return;
            }

            if (SensoryManager.Instance != null && !SensoryManager.Instance.IsExplored(wo.Tile))
            {
                __result = true;
            }
        }
    }

    // ---------------------------------------------------------
    // 2. 3D World Object Mesh Rendering (Zoomed In)
    // ---------------------------------------------------------
    [HarmonyPatch(typeof(WorldDrawLayer_WorldObjects_Expandable), "ShouldSkip")]
    public static class Expandable_ShouldSkip_Patch
    {
        public static void Postfix(WorldObject worldObject, ref bool __result)
        {
            if (__result || GeoscapeMod.Settings.visibilityMode != VisibilityMode.ClassicHazeOfWar)
            {
                return;
            }

            if (worldObject.Faction?.IsPlayer == true)
            {
                return;
            }

            if (SensoryManager.Instance != null && !SensoryManager.Instance.IsExplored(worldObject.Tile))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(WorldDrawLayer_WorldObjects_NonExpandable), "ShouldSkip")]
    public static class NonExpandable_ShouldSkip_Patch
    {
        public static void Postfix(WorldObject worldObject, ref bool __result)
        {
            if (__result || GeoscapeMod.Settings.visibilityMode != VisibilityMode.ClassicHazeOfWar)
            {
                return;
            }

            if (worldObject.Faction?.IsPlayer == true)
            {
                return;
            }

            if (SensoryManager.Instance != null && !SensoryManager.Instance.IsExplored(worldObject.Tile))
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
            if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.ClassicHazeOfWar)
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
            if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.ClassicHazeOfWar || __result == null || __result.Count == 0)
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
            if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.ClassicHazeOfWar)
            {
                return true;
            }

            if (SensoryManager.Instance != null && !SensoryManager.Instance.IsFeatureExplored(feature))
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
            if (!__result || GeoscapeMod.Settings.visibilityMode != VisibilityMode.ClassicHazeOfWar)
            {
                return;
            }

            if (SensoryManager.Instance != null && !SensoryManager.Instance.IsFeatureExplored(feature))
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
            if (!__result || GeoscapeMod.Settings.visibilityMode != VisibilityMode.ClassicHazeOfWar)
            {
                return;
            }

            if (__instance.Faction?.IsPlayer == true)
            {
                return;
            }

            if (SensoryManager.Instance != null && !SensoryManager.Instance.IsExplored(__instance.Tile))
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
            if (!__result || GeoscapeMod.Settings.visibilityMode != VisibilityMode.ClassicHazeOfWar)
            {
                return;
            }

            if (o.Faction?.IsPlayer == true)
            {
                return;
            }

            if (SensoryManager.Instance != null && !SensoryManager.Instance.IsExplored(o.Tile))
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
            if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.ClassicHazeOfWar || __result == null || __result.Count == 0)
            {
                return;
            }

            SensoryManager? sensory = SensoryManager.Instance;
            if (sensory == null)
            {
                return;
            }

            for (int i = __result.Count - 1; i >= 0; i--)
            {
                WorldObject wo = __result[i];
                if (wo.Faction?.IsPlayer != true && !sensory.IsExplored(wo.Tile))
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

            if (!obj.SelectableNow)
            {
                return false;
            }

            if (obj is WorldObject_Incursion incursion)
            {
                if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient &&
                    incursion.CurrentIntelligenceTier < IntelligenceTier.Contact)
                {
                    return false;
                }
            }

            if (GeoscapeMod.Settings.visibilityMode == VisibilityMode.ClassicHazeOfWar)
            {
                if (obj.Faction?.IsPlayer != true && SensoryManager.Instance != null && !SensoryManager.Instance.IsExplored(obj.Tile))
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
            SensoryManager? sensory = SensoryManager.Instance;
            bool isClassicHaze = GeoscapeMod.Settings.visibilityMode == VisibilityMode.ClassicHazeOfWar;
            bool isOmniscient = GeoscapeMod.Settings.visibilityMode == VisibilityMode.Omniscient;

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

                if (isClassicHaze && obj.Faction?.IsPlayer != true && sensory != null && !sensory.IsExplored(obj.Tile))
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
            if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.ClassicHazeOfWar)
            {
                return true;
            }

            if (Find.WorldSelector.NumSelectedObjects == 0 && Find.WorldSelector.SelectedTile.Valid &&
                SensoryManager.Instance != null && !SensoryManager.Instance.IsTileIdExplored(Find.WorldSelector.SelectedTile.tileId))
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
            if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.ClassicHazeOfWar)
            {
                return true;
            }

            if (Find.WorldSelector.SelectedTile.Valid && SensoryManager.Instance != null &&
                !SensoryManager.Instance.IsTileIdExplored(Find.WorldSelector.SelectedTile.tileId))
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
            if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.ClassicHazeOfWar)
            {
                return true;
            }

            if (Find.WorldSelector.SingleSelectedObject != null &&
                SensoryManager.ShouldMaskWorldObjectAsUnknown(Find.WorldSelector.SingleSelectedObject))
            {
                return false;
            }

            if (Find.WorldSelector.NumSelectedObjects == 0 && Find.WorldSelector.SelectedTile.Valid &&
                SensoryManager.Instance != null && !SensoryManager.Instance.IsTileIdExplored(Find.WorldSelector.SelectedTile.tileId))
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
            if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.ClassicHazeOfWar)
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
                SensoryManager.Instance != null && !SensoryManager.Instance.IsTileIdExplored(Find.WorldSelector.SelectedTile.tileId))
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
            if (!__result || GeoscapeMod.Settings.visibilityMode != VisibilityMode.ClassicHazeOfWar)
            {
                return;
            }

            PlanetTile tile = Find.WorldSelector.SelectedTile;
            if (tile.Valid && Find.WorldSelector.NumSelectedObjects == 0 && SensoryManager.Instance != null &&
                !SensoryManager.Instance.IsTileIdExplored(tile.tileId))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(WITab_Planet), "IsVisible", MethodType.Getter)]
    public static class WITab_Planet_IsVisible_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ref bool __result)
        {
            if (!__result || GeoscapeMod.Settings.visibilityMode != VisibilityMode.ClassicHazeOfWar)
            {
                return;
            }

            PlanetTile tile = Find.WorldSelector.SelectedTile;
            if (tile.Valid && Find.WorldSelector.NumSelectedObjects == 0 && SensoryManager.Instance != null &&
                !SensoryManager.Instance.IsTileIdExplored(tile.tileId))
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
            if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.ClassicHazeOfWar)
            {
                return true;
            }

            PlanetTile mouseTile = GenWorld.MouseTile(false);
            if (mouseTile.Valid && SensoryManager.Instance != null &&
                !SensoryManager.Instance.IsTileIdExplored(mouseTile.tileId))
            {
                return false;
            }

            return true;
        }
    }
}
