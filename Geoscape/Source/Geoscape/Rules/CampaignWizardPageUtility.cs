using System.Collections.Generic;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.Rules;

/// <summary>
/// Manages campaign creation wizard page stitching, dynamic skipping of the starting site selection screen
/// under Fog of War / Automated Landfall, and automated viable land plot designation.
/// </summary>
public static class CampaignWizardPageUtility
{
    private static Page_CreateWorldParams? registeredWorldParamsPage;
    private static Page_SelectStartingSite? registeredSitePage;
    private static Page? registeredPageAfterSite;

    public static Page_CreateWorldParams? WorldParamsPage => registeredWorldParamsPage;
    public static Page_SelectStartingSite? SitePage => registeredSitePage;
    public static Page? PageAfterSite => registeredPageAfterSite;

    public static void RegisterPages(Page_CreateWorldParams? worldParams, Page_SelectStartingSite? sitePage, Page? pageAfterSite)
    {
        registeredWorldParamsPage = worldParams;
        registeredSitePage = sitePage;
        registeredPageAfterSite = pageAfterSite;
    }

    public static void Clear()
    {
        registeredWorldParamsPage = null;
        registeredSitePage = null;
        registeredPageAfterSite = null;
    }

    public static bool ShouldSkipStartingSite(GeoscapeRuleSet? rules)
    {
        if (rules == null) return false;
        return rules.StartingSiteSelection.Value == StartingSiteSelectionMode.AutomatedLandfall;
    }

    public static void SyncPageLinkage(GeoscapeRuleSet? rules)
    {
        if (registeredWorldParamsPage == null || registeredSitePage == null || registeredPageAfterSite == null)
        {
            return;
        }

        bool skip = ShouldSkipStartingSite(rules);

        if (skip)
        {
            registeredWorldParamsPage.next = registeredPageAfterSite;
            registeredPageAfterSite.prev = registeredWorldParamsPage;

            registeredSitePage.prev = registeredWorldParamsPage;
            registeredSitePage.next = registeredPageAfterSite;
        }
        else
        {
            registeredWorldParamsPage.next = registeredSitePage;
            registeredSitePage.prev = registeredWorldParamsPage;
            registeredSitePage.next = registeredPageAfterSite;
            registeredPageAfterSite.prev = registeredSitePage;
        }
    }

    /// <summary>
    /// Guarantees that a valid starting tile is designated on the generated world when
    /// site selection was skipped or unassigned under Fog of War.
    /// </summary>
    public static void EnsureStartingTileAssigned()
    {
        if (Current.ProgramState != ProgramState.Entry) return;
        if (Find.GameInitData == null || Find.World == null) return;

        if (!Find.GameInitData.startingTile.Valid)
        {
            PlanetTile candidate = TileFinder.RandomStartingTile();
            if (candidate.Valid)
            {
                Find.GameInitData.startingTile = candidate;
                if (Find.WorldInterface != null)
                {
                    Find.WorldInterface.SelectedTile = candidate;
                }
                Log.Message($"[Project Vigilance] Automated landfall designated starting tile {candidate}.");
            }
        }
    }
}
