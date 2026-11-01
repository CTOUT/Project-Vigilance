using ProjectVigilance.Geoscape.Sensory;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Rules;

/// <summary>
/// Reusable layout panel rendering campaign rule sections across Mod Settings and World-Gen dialogs.
/// </summary>
public static class CampaignRulesPanel
{
    public static void DrawSensoryVisibilitySection(
        Listing_Standard listing,
        GeoscapeRuleSet rules,
        GeoscapeCampaignRules? campaign,
        RuleEditContext ctx,
        bool showLockPreview = false)
    {
        Text.Font = GameFont.Medium;
        listing.Label("Sensory & Planetary Visibility");
        Text.Font = GameFont.Small;
        listing.GapLine();

        CampaignRuleWidgets.DrawVisibilityRadio(
            listing,
            VisibilityMode.ClassicHazeOfWar,
            "Classic Haze of War (3-Tier RTS)",
            "The planet is uncharted. Unexplored tiles are dark, unmonitored explored tiles show a translucent grey haze, and active sensors reveal live movement.",
            rules.Visibility,
            campaign,
            ctx,
            showLockPreview);

        CampaignRuleWidgets.DrawVisibilityRadio(
            listing,
            VisibilityMode.PlanetarySurveillance,
            "Planetary Surveillance (Classic X-COM)",
            "Orbital survey mapping is available. Continents and static bases are known. Moving hostile incursions are hidden unless within active scout or radar range.",
            rules.Visibility,
            campaign,
            ctx,
            showLockPreview);

        CampaignRuleWidgets.DrawVisibilityRadio(
            listing,
            VisibilityMode.Omniscient,
            "Omniscient (Spectator / Living Map)",
            "No fog of war. All worldwide movement is visible at all times across the globe without scout or radar requirements.",
            rules.Visibility,
            campaign,
            ctx,
            showLockPreview);

        listing.Gap();
    }

    public static void DrawLookoutScoutingSection(
        Listing_Standard listing,
        GeoscapeRuleSet rules,
        GeoscapeCampaignRules? campaign,
        RuleEditContext ctx,
        bool showLockPreview = false)
    {
        if (rules.Visibility.Value == VisibilityMode.Omniscient)
        {
            return;
        }

        Text.Font = GameFont.Medium;
        listing.Label("Lookout & Scouting Horizons");
        Text.Font = GameFont.Small;
        listing.GapLine();

        CampaignRuleWidgets.DrawFloat(
            listing,
            rules.ColonyScoutRadius,
            1.0f,
            15.0f,
            campaign,
            ctx,
            showLockPreview);

        CampaignRuleWidgets.DrawFloat(
            listing,
            rules.CaravanScoutRadius,
            1.0f,
            10.0f,
            campaign,
            ctx,
            showLockPreview);

        CampaignRuleWidgets.DrawBool(
            listing,
            rules.NighttimeSensoryReduction,
            campaign,
            ctx,
            showLockPreview);

        listing.Gap();
    }

    public static void DrawIncursionsSection(
        Listing_Standard listing,
        GeoscapeRuleSet rules,
        GeoscapeCampaignRules? campaign,
        RuleEditContext ctx,
        bool showLockPreview = false)
    {
        Text.Font = GameFont.Medium;
        listing.Label("Incursion Movement & Storyteller Rules");
        Text.Font = GameFont.Small;
        listing.GapLine();

        CampaignRuleWidgets.DrawBool(
            listing,
            rules.SynchronizeMultiVectorRaids,
            campaign,
            ctx,
            showLockPreview);

        listing.Gap();

        CampaignRuleWidgets.DrawBool(
            listing,
            rules.IncursionsRestAtNight,
            campaign,
            ctx,
            showLockPreview);

        listing.Gap();

        CampaignRuleWidgets.DrawBool(
            listing,
            rules.TacticalIngressFlanking,
            campaign,
            ctx,
            showLockPreview);

        listing.Gap();

        CampaignRuleWidgets.DrawBool(
            listing,
            rules.ResetThreatCooldownOnArrival,
            campaign,
            ctx,
            showLockPreview);

        listing.Gap();
    }

    public static void DrawNotificationsSection(
        Listing_Standard listing,
        GeoscapeRuleSet rules,
        GeoscapeCampaignRules? campaign,
        RuleEditContext ctx,
        bool showLockPreview = false)
    {
        Text.Font = GameFont.Medium;
        listing.Label("Early Warning & Notifications");
        Text.Font = GameFont.Small;
        listing.GapLine();

        CampaignRuleWidgets.DrawBool(
            listing,
            rules.EarlyWarningLetters,
            campaign,
            ctx,
            showLockPreview);

        listing.Gap();
    }

    public static void DrawAllSections(
        Listing_Standard listing,
        GeoscapeRuleSet rules,
        GeoscapeCampaignRules? campaign,
        RuleEditContext ctx,
        bool showLockPreview = false)
    {
        DrawSensoryVisibilitySection(listing, rules, campaign, ctx, showLockPreview);
        DrawLookoutScoutingSection(listing, rules, campaign, ctx, showLockPreview);
        DrawIncursionsSection(listing, rules, campaign, ctx, showLockPreview);
        DrawNotificationsSection(listing, rules, campaign, ctx, showLockPreview);
    }
}
