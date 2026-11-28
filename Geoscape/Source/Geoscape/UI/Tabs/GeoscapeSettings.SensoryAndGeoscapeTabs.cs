using ProjectVigilance.Core.Localization;
using ProjectVigilance.Geoscape.Rules;
using ProjectVigilance.Geoscape.Settings;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ProjectVigilance.Geoscape;

public partial class GeoscapeSettings
{
    // =========================================================================
    // Tab 1: Sensory & Campaign Rules
    // =========================================================================

    private void DrawSensoryTab(Listing_Standard listing)
    {
        GeoscapeCampaignRules? campaign = GeoscapeCampaignRules.Active;
        bool inCampaign = campaign != null;
        GeoscapeRuleSet rules = campaign != null ? campaign.Rules : GetOrCreateTransientRuleSet();
        RuleEditContext ctx = campaign != null ? campaign.CreateEditContext() : new RuleEditContext(false, false, false);

        DrawContextBanner(listing, campaign);

        CampaignRulesPanel.DrawSensoryVisibilitySection(listing, rules, campaign, ctx, showLockPreview: !inCampaign);

        float colWidth = (listing.ColumnWidth - 16f) / 2f;
        Rect colsRect = listing.GetRect(sensoryColumnsHeight);
        Rect leftRect = new(colsRect.x, colsRect.y, colWidth, colsRect.height);
        Rect rightRect = new(colsRect.x + colWidth + 16f, colsRect.y, colWidth, colsRect.height);

        Listing_Standard leftCol = new() { maxOneColumn = true };
        leftCol.Begin(leftRect);
        CampaignRulesPanel.DrawLookoutScoutingSection(leftCol, rules, campaign, ctx, showLockPreview: !inCampaign);
        leftCol.End();

        Listing_Standard rightCol = new() { maxOneColumn = true };
        rightCol.Begin(rightRect);
        CampaignRulesPanel.DrawStartingSiteSelectionSection(rightCol, rules, campaign, ctx, showLockPreview: !inCampaign);
        rightCol.End();

        sensoryColumnsHeight = Mathf.Max(leftCol.CurHeight, rightCol.CurHeight) + 2f;

        if (!inCampaign)
        {
            rules.WriteToDefaults(this);
        }
    }

    // =========================================================================
    // Tab 3: Incursions & Encounters
    // =========================================================================

    private void DrawIncursionsTab(Listing_Standard listing)
    {
        GeoscapeCampaignRules? campaign = GeoscapeCampaignRules.Active;
        bool inCampaign = campaign != null;
        GeoscapeRuleSet rules = campaign != null ? campaign.Rules : GetOrCreateTransientRuleSet();
        RuleEditContext ctx = campaign != null ? campaign.CreateEditContext() : new RuleEditContext(false, false, false);

        DrawContextBanner(listing, campaign);

        CampaignRulesPanel.DrawIncursionsSection(listing, rules, campaign, ctx, showLockPreview: !inCampaign);

        listing.Gap();
        listing.CheckboxLabeled(
            "Show faction emblems on Geoscape incursions",
            ref showFactionEmblems,
            "Render the attacking faction's emblem icon on planetary incursions once classified, instead of generic markers.");

        listing.CheckboxLabeled(
            VigilanceLoc.Tr("PV_Settings_InterFactionCollisions", "Inter-Faction Overland Collisions"),
            ref enableInterFactionCollisions,
            VigilanceLoc.Tr("PV_Settings_InterFactionCollisions_Desc", "Simulates background encounters and skirmishes when rival NPC incursions cross paths on the globe."));

        if (!inCampaign)
        {
            rules.WriteToDefaults(this);
        }
    }

    // =========================================================================
    // Tab 4: Geoscape Tactical Controls & Early Warning
    // =========================================================================

    private void DrawGeoscapeTab(Listing_Standard listing)
    {
        GeoscapeCampaignRules? campaign = GeoscapeCampaignRules.Active;
        bool inCampaign = campaign != null;
        GeoscapeRuleSet rules = campaign != null ? campaign.Rules : GetOrCreateTransientRuleSet();
        RuleEditContext ctx = campaign != null ? campaign.CreateEditContext() : new RuleEditContext(false, false, false);

        float colWidth = (listing.ColumnWidth - 16f) / 2f;
        Rect topRowRect = listing.GetRect(geoscapeTopHeight);
        Rect leftRect = new Rect(topRowRect.x, topRowRect.y, colWidth, topRowRect.height);
        Rect rightRect = new Rect(topRowRect.x + colWidth + 16f, topRowRect.y, colWidth, topRowRect.height);

        Listing_Standard leftCol = new() { maxOneColumn = true };
        leftCol.Begin(leftRect);
        {
            Text.Font = GameFont.Medium;
            leftCol.Label("Geoscape Tactical Controls");
            Text.Font = GameFont.Small;
            leftCol.GapLine();

            leftCol.CheckboxLabeled(
                "Tactical Contact Disambiguation Menu",
                ref enableOverlappingEntitySelectionMenu,
                "When entities overlap on the planetary Geoscape (such as a caravan stationed at a colony), open a tactical selection menu rather than blindly cycling.");

            leftCol.CheckboxLabeled(
                "Intelligent Geoscape Right-Click Dispatch",
                ref enableIntelligentRightClick,
                "Allow right-clicking anywhere on the Geoscape without selecting a source first, intelligently suggesting actions from the nearest colonies and caravans.");

            leftCol.CheckboxLabeled(
                VigilanceLoc.Tr("PV_Settings_OptimisticDetours", "Optimistic Caravan Detours"),
                ref optimisticCaravanDetours,
                VigilanceLoc.Tr("PV_Settings_OptimisticDetours_Desc", "Caravans plot paths assuming unmapped fog-of-war sectors are passable, repathing dynamically when impassable terrain is discovered."));

            if (!ProjectVigilance.Core.Modules.VigilanceModuleManager.IsThermalActive)
            {
                leftCol.CheckboxLabeled(
                    VigilanceLoc.Tr("PV_Settings_ThermalIntegrityAudit", "Thermal Integrity Def Audit"),
                    ref enableThermalIntegrityAudit,
                    VigilanceLoc.Tr("PV_Settings_ThermalIntegrityAudit_Desc", "Automatically enforces non-combustible properties on metals, stone, and structural components at startup."));
            }

            leftCol.CheckboxLabeled(
                VigilanceLoc.Tr("PV_Settings_SeamlessZoom", "Seamless Geoscape / Battlescape Zoom"),
                ref enableSeamlessCameraZoom,
                VigilanceLoc.Tr("PV_Settings_SeamlessZoom_Desc", "Allows zooming out past maximum map zoom to open the planetary Geoscape centred on that tile, and zooming in past minimum globe altitude to enter an active battlescape or colony map."));

            DrawToggleableIntSlider(
                leftCol,
                VigilanceLoc.Tr("PV_Settings_SeamlessZoomNotches", $"Overscroll Notches Required: {seamlessZoomOverscrollNotches}", seamlessZoomOverscrollNotches),
                ref seamlessZoomOverscrollNotches,
                1,
                5,
                VigilanceLoc.Tr("PV_Settings_SeamlessZoomNotches_Desc", "Number of extra scroll-wheel notches required after hitting the zoom boundary before transitioning between Battlescape and Geoscape."),
                enableSeamlessCameraZoom);

            if (enableSeamlessCameraZoom)
            {
                string targetLabel = VigilanceLoc.Tr("PV_Settings_SeamlessZoomTarget", "Zoom-In Target Priority");
                string targetValue = seamlessZoomTargetMode == SeamlessZoomTargetMode.CursorFirst
                    ? VigilanceLoc.Tr("PV_Settings_SeamlessZoomTarget_CursorFirst", "Cursor First (Centre Fallback)")
                    : VigilanceLoc.Tr("PV_Settings_SeamlessZoomTarget_ScreenCenter", "Screen Centre Only");
                string targetDesc = VigilanceLoc.Tr(
                    "PV_Settings_SeamlessZoomTarget_Desc",
                    "Determines how an active map is chosen when zooming in from the Geoscape when multiple battlescapes or colonies are nearby.");
                if (leftCol.ButtonTextLabeled(targetLabel, targetValue, tooltip: targetDesc))
                {
                    seamlessZoomTargetMode = seamlessZoomTargetMode == SeamlessZoomTargetMode.CursorFirst
                        ? SeamlessZoomTargetMode.ScreenCenter
                        : SeamlessZoomTargetMode.CursorFirst;
                    SoundDefOf.Click?.PlayOneShotOnCamera();
                }

                string indicatorLabel = VigilanceLoc.Tr("PV_Settings_SeamlessZoomIndicator", "Boundary Detent Indicator");
                string indicatorValue = seamlessZoomIndicatorStyle switch
                {
                    SeamlessZoomIndicatorStyle.Off => VigilanceLoc.Tr("PV_Settings_SeamlessZoomIndicator_Off", "Off"),
                    SeamlessZoomIndicatorStyle.BorderFlash => VigilanceLoc.Tr("PV_Settings_SeamlessZoomIndicator_BorderFlash", "Border Flash"),
                    SeamlessZoomIndicatorStyle.BottomPill => VigilanceLoc.Tr("PV_Settings_SeamlessZoomIndicator_BottomPill", "Bottom Prompt"),
                    _ => VigilanceLoc.Tr("PV_Settings_SeamlessZoomIndicator_Hybrid", "Border Flash + Prompt")
                };
                string indicatorDesc = VigilanceLoc.Tr(
                    "PV_Settings_SeamlessZoomIndicator_Desc",
                    "Visual cue shown when scrolling against the zoom boundary to indicate that scrolling again within the window will switch views.");
                if (leftCol.ButtonTextLabeled(indicatorLabel, indicatorValue, tooltip: indicatorDesc))
                {
                    seamlessZoomIndicatorStyle = (SeamlessZoomIndicatorStyle)(((int)seamlessZoomIndicatorStyle + 1) % 4);
                    SoundDefOf.Click?.PlayOneShotOnCamera();
                }

                string transitionLabel = VigilanceLoc.Tr("PV_Settings_SeamlessZoomTransition", "View Transition Style");
                string transitionValue = seamlessZoomTransitionStyle switch
                {
                    SeamlessZoomTransitionStyle.Instant => VigilanceLoc.Tr("PV_Settings_SeamlessZoomTransition_Instant", "Instant"),
                    SeamlessZoomTransitionStyle.Fade => VigilanceLoc.Tr("PV_Settings_SeamlessZoomTransition_Fade", "Fade"),
                    _ => VigilanceLoc.Tr("PV_Settings_SeamlessZoomTransition_Traverse", "Traverse")
                };
                string transitionDesc = VigilanceLoc.Tr(
                    "PV_Settings_SeamlessZoomTransition_Desc",
                    "Transition effect between Battlescape and Geoscape: Instant (no effect), Fade (brief dark dip), or Traverse (cloud-deck drift). Enabling Reduce Shroud Motion automatically selects Instant.");
                if (leftCol.ButtonTextLabeled(transitionLabel, transitionValue, tooltip: transitionDesc))
                {
                    seamlessZoomTransitionStyle = (SeamlessZoomTransitionStyle)(((int)seamlessZoomTransitionStyle + 1) % 3);
                    SoundDefOf.Click?.PlayOneShotOnCamera();
                }
            }
        }
        leftCol.End();

        Listing_Standard rightCol = new() { maxOneColumn = true };
        rightCol.Begin(rightRect);
        {
            Text.Font = GameFont.Medium;
            rightCol.Label("Context Limits & Early Warning");
            Text.Font = GameFont.Small;
            rightCol.GapLine();

            DrawToggleableIntSlider(
                rightCol,
                $"Max Contextual Caravans: {maxContextualCaravansShown}",
                ref maxContextualCaravansShown,
                1,
                8,
                "Limits the number of nearest player caravans directly displayed on the right-click menu. Additional caravans can be accessed via a drilldown sub-menu.",
                enableIntelligentRightClick);

            DrawToggleableIntSlider(
                rightCol,
                $"Max Contextual Colonies: {maxContextualColoniesShown}",
                ref maxContextualColoniesShown,
                1,
                5,
                "Limits the number of nearest player colonies directly displayed on the right-click menu. Additional colonies can be accessed via a drilldown sub-menu.",
                enableIntelligentRightClick);

            rightCol.Gap(2f);
            CampaignRulesPanel.DrawNotificationsSection(rightCol, rules, campaign, ctx, showLockPreview: !inCampaign);
        }
        rightCol.End();

        geoscapeTopHeight = Mathf.Max(leftCol.CurHeight, rightCol.CurHeight) + 4f;

        listing.GapLine(6f);

        // Side-by-side Epistemic Discovery & Sensory Narrative Cards
        float infoLeftW = (listing.ColumnWidth - 16f) * 0.56f;
        float infoRightW = (listing.ColumnWidth - 16f) - infoLeftW;

        Rect cardsHeader = listing.GetRect(24f);
        Text.Font = GameFont.Small;
        Widgets.Label(new Rect(cardsHeader.x, cardsHeader.y, infoLeftW, 24f), "Epistemic Discovery: \"You Don't Know What You Don't Know\"");
        Widgets.Label(new Rect(cardsHeader.x + infoLeftW + 16f, cardsHeader.y, infoRightW, 24f), "Sensory Narrative & Tech-Level Adaptability");

        Rect cardsRect = listing.GetRect(168f);
        Rect infoRect = new Rect(cardsRect.x, cardsRect.y, infoLeftW, cardsRect.height);
        Rect techRect = new Rect(cardsRect.x + infoLeftW + 16f, cardsRect.y, infoRightW, cardsRect.height);

        Widgets.DrawBoxSolid(infoRect, new Color(0.12f, 0.15f, 0.20f, 0.6f));
        Widgets.DrawBox(infoRect);
        Widgets.DrawBoxSolid(techRect, new Color(0.12f, 0.15f, 0.20f, 0.6f));
        Widgets.DrawBox(techRect);

        Text.Font = GameFont.Tiny;
        string infoText = "Threats evolve across a 5-tier intelligence architecture:\n" +
                          "• Undetected: Shrouded in planetary fog of war.\n" +
                          "• Suspected: Distant cues from smoke, rumours, or acoustic anomalies.\n" +
                          "• Contact: Radar track or lookout visual (position & heading known).\n" +
                          "• Classified: Faction & force size revealed inside identification rings.\n" +
                          "• Analysed: Full breakdown (headcount, weapons, target, ETA) via SIGINT.";
        Widgets.Label(infoRect.ContractedBy(8f), infoText);

        string techInfo = "Sensory feedback adapts to colony tech level:\n\n" +
                          "• Neolithic / Medieval:\n   Observational terms ('Approaching Strangers', 'Hostile band').\n\n" +
                          "• Industrial / Spacer:\n   Military telemetry ('Contact-03', 'Bearing SSE, ~24 hexes/day').";
        Widgets.Label(techRect.ContractedBy(8f), techInfo);
        Text.Font = GameFont.Small;

        if (!inCampaign)
        {
            rules.WriteToDefaults(this);
        }
    }
}
