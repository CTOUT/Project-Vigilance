using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Gates RimWorld's <see cref="Dialog_WorldSearch"/> entries, search matching, labels, and icons
/// against <see cref="SensoryManager"/> exploration, identification, radar contact, and orbital detection state,
/// and refreshes open search results when sensory sweeps reveal or identify sites.
/// </summary>
public static class WorldSearch_Visibility_Patch
{
    private sealed class SearchRevisionState
    {
        public bool Initialised;
        public int LastSweepRevision;
        public VisibilityMode LastVisibilityMode;
    }

    private static readonly ConditionalWeakTable<Dialog_WorldSearch, SearchRevisionState> SearchStates = new();

    private static readonly Func<Dialog_WorldSearch, string, bool>? TextMatchDelegate =
        AccessTools.Method(typeof(Dialog_WorldSearch), "TextMatch", new[] { typeof(string) }) is MethodInfo mi
            ? (Func<Dialog_WorldSearch, string, bool>)Delegate.CreateDelegate(
                typeof(Func<Dialog_WorldSearch, string, bool>),
                mi)
            : null;

    private static readonly Action<Dialog_WorldSearch>? InitializeSearchSetDelegate =
        AccessTools.Method(typeof(Dialog_WorldSearch), "InitializeSearchSet") is MethodInfo mi
            ? (Action<Dialog_WorldSearch>)Delegate.CreateDelegate(
                typeof(Action<Dialog_WorldSearch>),
                mi)
            : null;

    [HarmonyPatch(typeof(WorldSearchElement), nameof(WorldSearchElement.DisplayLabel), MethodType.Getter)]
    public static class WorldSearchElement_DisplayLabel_Patch
    {
        public static bool Prefix(WorldSearchElement __instance, ref string? __result)
        {
            if (GeoscapeRules.VisibilityMode != VisibilityMode.ClassicHazeOfWar)
            {
                return true;
            }

            WorldSearchElementState state = WorldSearchVisibilityUtility.Classify(__instance);
            if (state == WorldSearchElementState.Excluded)
            {
                __result = null;
                return false;
            }

            if (state == WorldSearchElementState.VisibleMasked)
            {
                __result = WorldSearchVisibilityUtility.ResolveMaskedDisplayLabel(__instance);
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Dialog_WorldSearch), "InitializeSearchSet")]
    public static class Dialog_WorldSearch_InitializeSearchSet_Patch
    {
        public static void Postfix(List<WorldSearchElement> ___searchSet)
        {
            if (GeoscapeRules.VisibilityMode != VisibilityMode.ClassicHazeOfWar || ___searchSet == null)
            {
                return;
            }

            ___searchSet.RemoveAll(el => WorldSearchVisibilityUtility.Classify(el) == WorldSearchElementState.Excluded);
        }
    }

    [HarmonyPatch(typeof(Dialog_WorldSearch), "ShouldSkipElement")]
    public static class Dialog_WorldSearch_ShouldSkipElement_Patch
    {
        public static void Postfix(WorldSearchElement element, ref bool __result)
        {
            if (__result || GeoscapeRules.VisibilityMode != VisibilityMode.ClassicHazeOfWar)
            {
                return;
            }

            if (WorldSearchVisibilityUtility.Classify(element) == WorldSearchElementState.Excluded)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Dialog_WorldSearch), "ElementMatch")]
    public static class Dialog_WorldSearch_ElementMatch_Patch
    {
        public static bool Prefix(Dialog_WorldSearch __instance, WorldSearchElement element, ref bool __result)
        {
            if (GeoscapeRules.VisibilityMode != VisibilityMode.ClassicHazeOfWar || TextMatchDelegate == null)
            {
                return true;
            }

            WorldSearchElementState state = WorldSearchVisibilityUtility.Classify(element);
            if (state == WorldSearchElementState.Excluded)
            {
                __result = false;
                return false;
            }

            if (state == WorldSearchElementState.VisibleMasked)
            {
                string maskedLabel = WorldSearchVisibilityUtility.ResolveMaskedDisplayLabel(element);
                __result = !string.IsNullOrEmpty(maskedLabel) && TextMatchDelegate(__instance, maskedLabel);
                return false;
            }

            if (element.worldObject != null && TextMatchDelegate(__instance, element.worldObject.Label))
            {
                __result = true;
                return false;
            }

            if (element.landmark != null)
            {
                if ((!string.IsNullOrEmpty(element.landmark.name) && TextMatchDelegate(__instance, element.landmark.name)) ||
                    (element.landmark.def != null && !string.IsNullOrEmpty(element.landmark.def.label) && TextMatchDelegate(__instance, element.landmark.def.label)))
                {
                    __result = true;
                    return false;
                }
            }

            if (!element.mutators.NullOrEmpty() && WorldSearchVisibilityUtility.CanSearchMutators(element.tile))
            {
                foreach (TileMutatorDef mutator in element.mutators)
                {
                    if (mutator == null)
                    {
                        continue;
                    }

                    if (TextMatchDelegate(__instance, mutator.label) || TextMatchDelegate(__instance, mutator.Label(element.tile)))
                    {
                        __result = true;
                        return false;
                    }
                }
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Dialog_WorldSearch), "DoIcon")]
    public static class Dialog_WorldSearch_DoIcon_Patch
    {
        public static bool Prefix(WorldSearchElement element, Rect iconRect)
        {
            if (GeoscapeRules.VisibilityMode != VisibilityMode.ClassicHazeOfWar)
            {
                return true;
            }

            WorldSearchElementState state = WorldSearchVisibilityUtility.Classify(element);
            if (state == WorldSearchElementState.Excluded)
            {
                return false;
            }

            if (state == WorldSearchElementState.VisibleMasked)
            {
                bool isLandmark = WorldSearchVisibilityUtility.UsesLandmarkMask(element);
                Texture2D icon = isLandmark
                    ? LandmarkSensoryUtility.GetUnknownLandmarkExpandingTexture()
                    : WorldObject_Visibility_Patch.GetUnknownSiteExpandingTexture();
                Color color = isLandmark
                    ? LandmarkSensoryUtility.TacticalCyan
                    : WorldObject_Visibility_Patch.UnknownSiteIconColor;

                GUI.color = color;
                GUI.DrawTexture(iconRect, icon);
                GUI.color = Color.white;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Dialog_WorldSearch), "DoLabel")]
    public static class Dialog_WorldSearch_DoLabel_Patch
    {
        public static bool Prefix(WorldSearchElement element, Rect labelRect)
        {
            if (GeoscapeRules.VisibilityMode != VisibilityMode.ClassicHazeOfWar)
            {
                return true;
            }

            if (WorldSearchVisibilityUtility.Classify(element) == WorldSearchElementState.VisibleMasked)
            {
                Widgets.Label(labelRect, WorldSearchVisibilityUtility.ResolveMaskedDisplayLabel(element));
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Dialog_Search<WorldSearchElement>), nameof(Dialog_Search<WorldSearchElement>.WindowUpdate))]
    public static class Dialog_WorldSearch_WindowUpdate_Patch
    {
        public static void Postfix(Dialog_Search<WorldSearchElement> __instance)
        {
            if (__instance is not Dialog_WorldSearch worldSearch || InitializeSearchSetDelegate == null)
            {
                return;
            }

            int currentRevision = SensoryManager.Instance?.SweepRevision ?? 0;
            VisibilityMode currentMode = GeoscapeRules.VisibilityMode;
            SearchRevisionState state = SearchStates.GetOrCreateValue(worldSearch);

            if (!state.Initialised)
            {
                state.Initialised = true;
                state.LastSweepRevision = currentRevision;
                state.LastVisibilityMode = currentMode;
                return;
            }

            if (state.LastSweepRevision != currentRevision || state.LastVisibilityMode != currentMode)
            {
                state.LastSweepRevision = currentRevision;
                state.LastVisibilityMode = currentMode;
                InitializeSearchSetDelegate(worldSearch);
                worldSearch.Notify_CommonSearchChanged();
            }
        }
    }
}
