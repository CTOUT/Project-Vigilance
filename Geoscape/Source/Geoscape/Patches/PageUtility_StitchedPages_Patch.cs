using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ProjectVigilance.Geoscape.Rules;
using RimWorld;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Inserts Page_GeoscapeRules into RimWorld's campaign creation wizard sequence between
/// Storyteller selection and World Generation parameters (Option A).
/// </summary>
[HarmonyPatch(typeof(PageUtility), nameof(PageUtility.StitchedPages))]
public static class PageUtility_StitchedPages_Patch
{
    public static void Prefix(ref IEnumerable<Page> pages)
    {
        if (pages == null) return;

        List<Page> pageList = pages.ToList();

        // Idempotency guard: prevent duplicate insertion
        if (pageList.Any(p => p is Page_GeoscapeRules))
        {
            return;
        }

        // Insert immediately before Page_CreateWorldParams
        int worldParamsIdx = pageList.FindIndex(p => p is Page_CreateWorldParams);
        if (worldParamsIdx >= 0)
        {
            NewCampaignRuleDraft.BeginFromDefaults(GeoscapeMod.Settings);
            Page_GeoscapeRules geoscapePage = new();
            pageList.Insert(worldParamsIdx, geoscapePage);
            pages = pageList;
        }
    }
}
