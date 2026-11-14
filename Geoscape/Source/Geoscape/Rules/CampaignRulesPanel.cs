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
            VisibilityMode.FogOfWar,
            "Fog of War",
            "The planet is uncharted. Unexplored tiles are shrouded in darkness, explored unmonitored tiles show a translucent veil, and active sensors reveal live movement.",
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

    public static void DrawStartingSiteSelectionSection(
        Listing_Standard listing,
        GeoscapeRuleSet rules,
        GeoscapeCampaignRules? campaign,
        RuleEditContext ctx,
        bool showLockPreview = false)
    {
        Text.Font = GameFont.Medium;
        listing.Label("Landing Site Selection");
        Text.Font = GameFont.Small;
        listing.GapLine();

        CampaignRuleWidgets.DrawStartingSiteSelectionRadio(
            listing,
            StartingSiteSelectionMode.AutomatedLandfall,
            "Automated Landfall (Recommended for Fog of War)",
            "World generation automatically selects a viable, habitable landing site and advances directly to Ideology/Colonist setup. Eliminates purposeless clicking on an uncharted dark globe.",
            rules.StartingSiteSelection,
            campaign,
            ctx,
            showLockPreview);

        CampaignRuleWidgets.DrawStartingSiteSelectionRadio(
            listing,
            StartingSiteSelectionMode.ManualTargeting,
            "Manual Globe Targeting",
            "Presents the planetary globe during campaign setup. Recommended when playing with Planetary Surveillance, or for players intentionally aiming for specific polar or equatorial latitudes under Fog of War.",
            rules.StartingSiteSelection,
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
        bool applicable = rules.Visibility.Value != VisibilityMode.Omniscient;

        Text.Font = GameFont.Medium;
        if (!applicable)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.38f);
        }
        listing.Label("Lookout & Scouting Horizons");
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
        listing.GapLine();

        static float ClampIdHorizon(
            CampaignRule<float> scoutRule,
            CampaignRule<float> idRule,
            GeoscapeCampaignRules? activeCampaign)
        {
            float maxId = GeoscapeRuleSet.GetMaxIdentificationRadius(scoutRule.Value);
            float clamped = GeoscapeRuleSet.ClampIdentificationRadius(idRule.Value, scoutRule.Value);
            if (Mathf.Abs(clamped - idRule.Value) > 0.01f)
            {
                if (activeCampaign == null)
                {
                    idRule.Value = clamped;
                }
                else if (!activeCampaign.TryApply(idRule, clamped, out _))
                {
                    activeCampaign.ForceApplyUnsafe(idRule, clamped);
                }
            }
            return maxId;
        }

        CampaignRuleWidgets.DrawFloat(
            listing,
            rules.ColonyScoutRadius,
            1.0f,
            15.0f,
            campaign,
            ctx,
            showLockPreview,
            enabled: applicable);

        float maxColonyId = ClampIdHorizon(rules.ColonyScoutRadius, rules.ColonyIdentificationRadius, campaign);
        CampaignRuleWidgets.DrawFloat(
            listing,
            rules.ColonyIdentificationRadius,
            0.0f,
            Mathf.Max(1.0f, maxColonyId),
            campaign,
            ctx,
            showLockPreview,
            enabled: applicable && maxColonyId > 0.0f);

        CampaignRuleWidgets.DrawFloat(
            listing,
            rules.CaravanScoutRadius,
            1.0f,
            10.0f,
            campaign,
            ctx,
            showLockPreview,
            enabled: applicable);

        float maxCaravanId = ClampIdHorizon(rules.CaravanScoutRadius, rules.CaravanIdentificationRadius, campaign);
        CampaignRuleWidgets.DrawFloat(
            listing,
            rules.CaravanIdentificationRadius,
            0.0f,
            Mathf.Max(1.0f, maxCaravanId),
            campaign,
            ctx,
            showLockPreview,
            enabled: applicable && maxCaravanId > 0.0f);

        CampaignRuleWidgets.DrawBool(
            listing,
            rules.NighttimeSensoryReduction,
            campaign,
            ctx,
            showLockPreview,
            enabled: applicable);

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
        DrawStartingSiteSelectionSection(listing, rules, campaign, ctx, showLockPreview);
        DrawLookoutScoutingSection(listing, rules, campaign, ctx, showLockPreview);
        DrawIncursionsSection(listing, rules, campaign, ctx, showLockPreview);
        DrawNotificationsSection(listing, rules, campaign, ctx, showLockPreview);
    }
}
