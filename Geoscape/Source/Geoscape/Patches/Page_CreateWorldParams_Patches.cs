using HarmonyLib;
using ProjectVigilance.Geoscape.Rules;
using RimWorld;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Synchronises wizard page linkage and ensures automated landfall designation
/// when world generation finishes on Page_CreateWorldParams.
/// </summary>
public static class Page_CreateWorldParams_Patches
{
    [HarmonyPatch(typeof(Page_CreateWorldParams), nameof(Page_CreateWorldParams.PreOpen))]
    public static class PreOpen_Patch
    {
        public static void Postfix()
        {
            GeoscapeRuleSet draft = NewCampaignRuleDraft.GetOrCreate(GeoscapeMod.Settings);
            CampaignWizardPageUtility.SyncPageLinkage(draft);
        }
    }

    [HarmonyPatch(typeof(Page_CreateWorldParams), "<CanDoNext>b__20_1")]
    public static class CanDoNext_Finished_Patch
    {
        public static void Prefix()
        {
            GeoscapeRuleSet draft = NewCampaignRuleDraft.GetOrCreate(GeoscapeMod.Settings);
            if (CampaignWizardPageUtility.ShouldSkipStartingSite(draft))
            {
                CampaignWizardPageUtility.EnsureStartingTileAssigned();
            }
        }
    }
}
