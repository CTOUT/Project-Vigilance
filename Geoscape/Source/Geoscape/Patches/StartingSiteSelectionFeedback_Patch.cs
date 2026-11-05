using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Harmony patches intercepting starting site selection feedback and confirmation dialogs
/// for uncharted worlds under Fog of War.
/// </summary>
[StaticConstructorOnStartup]
public static class StartingSiteSelectionFeedback_Patch
{
    /// <summary>
    /// Intercepts settlement validity checks. Under Fog of War, if an unexplored tile cannot support
    /// a colony (e.g. ocean, impassable mountains), replaces explicit terrain rejection reasons
    /// with an ambiguous telemetry error to prevent sonar-probing the shroud.
    /// </summary>
    [HarmonyPatch(typeof(TileFinder), nameof(TileFinder.IsValidTileForNewSettlement), typeof(PlanetTile), typeof(StringBuilder), typeof(bool))]
    public static class TileFinder_IsValidTileForNewSettlement_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(PlanetTile tile, StringBuilder? reason, bool __result)
        {
            if (!__result && reason != null)
            {
                if (StartingSiteFeedbackUtility.ShouldMaskRejectionReason(tile))
                {
                    reason.Clear();
                    reason.Append(StartingSiteFeedbackUtility.AmbiguousLandingRejectionMessage);
                }
            }
        }
    }

    /// <summary>
    /// Intercepts settlement confirmation checks. Under Fog of War, if the selected tile is unexplored,
    /// suppresses warning confirmation dialogs (toxic pollution, extreme temperatures, harsh biomes)
    /// so colony drops proceed completely blind into the unknown.
    /// </summary>
    [HarmonyPatch(typeof(SettlementProximityGoodwillUtility), nameof(SettlementProximityGoodwillUtility.CheckConfirmSettle))]
    public static class SettlementProximityGoodwillUtility_CheckConfirmSettle_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(PlanetTile tile, Action settleAction)
        {
            if (StartingSiteFeedbackUtility.ShouldSuppressHazardConfirmation(tile))
            {
                settleAction?.Invoke();
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Suppresses confirmation descriptions for unexplored tiles under Fog of War, ensuring any
    /// callers expecting descriptions receive an empty sequence instead of leaked environmental intel.
    /// </summary>
    [HarmonyPatch(typeof(SettlementProximityGoodwillUtility), "GetConfirmationDescriptions")]
    public static class SettlementProximityGoodwillUtility_GetConfirmationDescriptions_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(PlanetTile tile, ref IEnumerable<TaggedString> __result)
        {
            if (StartingSiteFeedbackUtility.ShouldSuppressHazardConfirmation(tile))
            {
                __result = Enumerable.Empty<TaggedString>();
            }
        }
    }

    /// <summary>
    /// Suppresses confirmation goodwill effects for unexplored tiles under Fog of War, preventing
    /// proximity diplomatic penalties from leaking the presence of hidden faction bases.
    /// </summary>
    [HarmonyPatch(typeof(SettlementProximityGoodwillUtility), "GetConfirmationEffects")]
    public static class SettlementProximityGoodwillUtility_GetConfirmationEffects_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(PlanetTile tile, ref IEnumerable<string> __result)
        {
            if (StartingSiteFeedbackUtility.ShouldSuppressHazardConfirmation(tile))
            {
                __result = Enumerable.Empty<string>();
            }
        }
    }

    /// <summary>
    /// Intercepts Page_SelectStartingSite.CanDoNext(). Under Fog of War, if the player opted into manual targeting
    /// but advances without manually selecting a tile, automatically designates a viable random landing site
    /// instead of presenting the vanilla rejection error.
    /// </summary>
    [HarmonyPatch(typeof(Page_SelectStartingSite), "CanDoNext")]
    public static class Page_SelectStartingSite_CanDoNext_Patch
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            if (GeoscapeRules.VisibilityMode == VisibilityMode.FogOfWar &&
                Find.WorldInterface != null &&
                !Find.WorldInterface.SelectedTile.Valid)
            {
                PlanetTile candidate = TileFinder.RandomStartingTile();
                if (candidate.Valid)
                {
                    Find.WorldInterface.SelectedTile = candidate;
                    Messages.Message("Random landing site automatically designated.", MessageTypeDefOf.SilentInput, false);
                }
            }
        }
    }
}
