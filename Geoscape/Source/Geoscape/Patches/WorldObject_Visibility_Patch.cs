using System.Collections.Generic;
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
/// in unexplored sectors under Classic Haze of War.
/// Prevents 2D/3D settlement rendering, landmarks, regional labels, hover tooltips, and selection.
/// </summary>
public static class WorldObject_Visibility_Patch
{
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

            if (wo.Faction == Faction.OfPlayer)
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

            if (worldObject.Faction == Faction.OfPlayer)
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

            if (worldObject.Faction == Faction.OfPlayer)
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

            if (__instance.Faction == Faction.OfPlayer)
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

            if (o.Faction == Faction.OfPlayer)
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
                if (wo.Faction != Faction.OfPlayer && !sensory.IsExplored(wo.Tile))
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
                if (obj.Faction != Faction.OfPlayer && SensoryManager.Instance != null && !SensoryManager.Instance.IsExplored(obj.Tile))
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

                if (isClassicHaze && obj.Faction != Faction.OfPlayer && sensory != null && !sensory.IsExplored(obj.Tile))
                {
                    continue;
                }

                yield return obj;
            }
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
