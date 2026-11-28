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

}
