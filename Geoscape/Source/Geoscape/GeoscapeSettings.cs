using System;
using System.Collections.Generic;
using ProjectVigilance.Geoscape.Rules;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ProjectVigilance.Geoscape;

public enum GeoscapeSettingsTab
{
    SensoryRadar,
    Incursions,
    EarlyWarningIntel,
    AboutSuite
}

public class GeoscapeSettings : ModSettings
{
    // =========================================================================
    // Persistent Configuration Fields
    // =========================================================================

    public bool synchronizeMultiVectorRaids = true;
    public bool showFactionEmblems = true;
    public bool resetThreatCooldownOnArrival = false;
    public bool enableTacticalIngressFlanking = true;

    public VisibilityMode visibilityMode = VisibilityMode.ClassicHazeOfWar;
    public RadarOutlineStyle radarOutlineStyle = RadarOutlineStyle.BoundaryAndGrid;
    public bool usePlayerFactionColor = true;
    public Color radarCustomColor = new(0f, 0.90f, 1.0f);
    public float radarOutlineOpacity = 0.50f;
    public float identificationRingOpacity = 0.65f;
    public float honeycombGridOpacity = 0.40f;
    public bool useCustomIdentificationColor = false;
    public Color radarIdentificationColor = new(1.0f, 0.82f, 0.15f);
    public float surveillanceDimming = 0.25f;
    public float colonyScoutRadius = 5.0f;
    public float caravanScoutRadius = 3.0f;
    public bool enableEarlyWarningLetters = true;
    public bool enableNighttimeSensoryReduction = true;
    public bool incursionsRestAtNight = true;
    public bool showIdentificationHorizon = true;
    public RadarTerrainShadowStyle radarTerrainShadowStyle = RadarTerrainShadowStyle.Both;
    public StartingSiteSelectionMode startingSiteSelection = StartingSiteSelectionMode.AutomatedLandfall;
    public bool enableOverlappingEntitySelectionMenu = true;
    public bool enableIntelligentRightClick = true;
    public int maxContextualCaravansShown = 3;
    public int maxContextualColoniesShown = 2;

    // =========================================================================
    // Transient UI State (Tabs, Scrolling & Dynamic Sizing)
    // =========================================================================

    private static GeoscapeSettingsTab currentTab = GeoscapeSettingsTab.SensoryRadar;
    private static readonly Vector2[] scrollPositions = new Vector2[4];
    private static readonly float[] tabHeights = { 960f, 520f, 660f, 480f };

    public Color EffectiveRadarColor
    {
        get
        {
            if (usePlayerFactionColor && Current.ProgramState == ProgramState.Playing && Current.Game != null && Find.FactionManager != null)
            {
                Faction? playerFaction = Faction.OfPlayerSilentFail;
                if (playerFaction != null && playerFaction.Color != default)
                {
                    return playerFaction.Color;
                }
            }
            return radarCustomColor;
        }
    }

    public Color EffectiveIdentificationColor
    {
        get
        {
            if (useCustomIdentificationColor)
            {
                return radarIdentificationColor;
            }
            return WorldDrawLayer_RadarCoverage.GetAutomaticIdentificationColor(EffectiveRadarColor);
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref synchronizeMultiVectorRaids, "synchronizeMultiVectorRaids", true);
        Scribe_Values.Look(ref showFactionEmblems, "showFactionEmblems", true);
        Scribe_Values.Look(ref resetThreatCooldownOnArrival, "resetThreatCooldownOnArrival", false);
        Scribe_Values.Look(ref enableTacticalIngressFlanking, "enableTacticalIngressFlanking", true);

        Scribe_Values.Look(ref visibilityMode, "visibilityMode", VisibilityMode.ClassicHazeOfWar);
        Scribe_Values.Look(ref radarOutlineStyle, "radarOutlineStyle", RadarOutlineStyle.BoundaryAndGrid);
        Scribe_Values.Look(ref usePlayerFactionColor, "usePlayerFactionColor", true);
        Scribe_Values.Look(ref radarCustomColor, "radarCustomColor", new Color(0f, 0.90f, 1.0f));
        Scribe_Values.Look(ref radarOutlineOpacity, "radarOutlineOpacity", 0.50f);
        Scribe_Values.Look(ref identificationRingOpacity, "identificationRingOpacity", 0.65f);
        Scribe_Values.Look(ref honeycombGridOpacity, "honeycombGridOpacity", 0.40f);
        Scribe_Values.Look(ref useCustomIdentificationColor, "useCustomIdentificationColor", false);
        Scribe_Values.Look(ref radarIdentificationColor, "radarIdentificationColor", new Color(1.0f, 0.82f, 0.15f));
        Scribe_Values.Look(ref showIdentificationHorizon, "showIdentificationHorizon", true);
        Scribe_Values.Look(ref radarTerrainShadowStyle, "radarTerrainShadowStyle", RadarTerrainShadowStyle.Both);
        Scribe_Values.Look(ref surveillanceDimming, "surveillanceDimming", 0.25f);
        Scribe_Values.Look(ref colonyScoutRadius, "colonyScoutRadius", 5.0f);
        Scribe_Values.Look(ref caravanScoutRadius, "caravanScoutRadius", 3.0f);
        Scribe_Values.Look(ref enableEarlyWarningLetters, "enableEarlyWarningLetters", true);
        Scribe_Values.Look(ref enableNighttimeSensoryReduction, "enableNighttimeSensoryReduction", true);
        Scribe_Values.Look(ref incursionsRestAtNight, "incursionsRestAtNight", true);
        Scribe_Values.Look(ref startingSiteSelection, "startingSiteSelection", StartingSiteSelectionMode.AutomatedLandfall);
        Scribe_Values.Look(ref enableOverlappingEntitySelectionMenu, "enableOverlappingEntitySelectionMenu", true);
        Scribe_Values.Look(ref enableIntelligentRightClick, "enableIntelligentRightClick", true);
        Scribe_Values.Look(ref maxContextualCaravansShown, "maxContextualCaravansShown", 3);
        Scribe_Values.Look(ref maxContextualColoniesShown, "maxContextualColoniesShown", 2);
    }

    // =========================================================================
    // Tabbed Modular Settings Window
    // =========================================================================

    public void DoWindowContents(Rect inRect)
    {
        // 1. Draw Tab Bar Header
        Rect tabRowRect = new Rect(inRect.x, inRect.y, inRect.width, 36f);
        string[] tabLabels = { "Sensory & Radar", "Incursions & AI", "Early Warning & Intel", "About & Diagnostics" };
        int tabCount = tabLabels.Length;
        float spacing = 6f;
        float tabWidth = (tabRowRect.width - (tabCount - 1) * spacing) / tabCount;

        for (int i = 0; i < tabCount; i++)
        {
            Rect tabBtnRect = new Rect(tabRowRect.x + i * (tabWidth + spacing), tabRowRect.y, tabWidth, 32f);
            bool isSelected = currentTab == (GeoscapeSettingsTab)i;

            if (isSelected)
            {
                Widgets.DrawHighlightSelected(tabBtnRect);
                Widgets.DrawBoxSolid(new Rect(tabBtnRect.x, tabBtnRect.yMax - 3f, tabBtnRect.width, 3f), new Color(0f, 0.85f, 1f));
            }

            if (Widgets.ButtonText(tabBtnRect, tabLabels[i]))
            {
                if (currentTab != (GeoscapeSettingsTab)i)
                {
                    currentTab = (GeoscapeSettingsTab)i;
                    SoundDefOf.Click?.PlayOneShotOnCamera();
                }
            }
        }

        Widgets.DrawLineHorizontal(inRect.x, inRect.y + 38f, inRect.width);

        // 2. Scrollable Content View
        Rect contentRect = new Rect(inRect.x, inRect.y + 44f, inRect.width, inRect.height - 44f);
        int tabIndex = (int)currentTab;
        Rect viewRect = new Rect(0f, 0f, contentRect.width - 24f, Mathf.Max(tabHeights[tabIndex], contentRect.height));

        Widgets.BeginScrollView(contentRect, ref scrollPositions[tabIndex], viewRect);
        Listing_Standard listing = new();
        listing.Begin(viewRect);

        switch (currentTab)
        {
            case GeoscapeSettingsTab.SensoryRadar:
                DrawSensoryRadarTab(listing);
                break;
            case GeoscapeSettingsTab.Incursions:
                DrawIncursionsTab(listing);
                break;
            case GeoscapeSettingsTab.EarlyWarningIntel:
                DrawEarlyWarningIntelTab(listing);
                break;
            case GeoscapeSettingsTab.AboutSuite:
                DrawAboutDiagnosticsTab(listing);
                break;
        }

        tabHeights[tabIndex] = Mathf.Max(listing.CurHeight + 20f, contentRect.height);
        listing.End();
        Widgets.EndScrollView();
    }

    // =========================================================================
    // Tab 1: Sensory & Radar Grid
    // =========================================================================

    private GeoscapeRuleSet? transientDefaultRuleSet;

    private GeoscapeRuleSet GetOrCreateTransientRuleSet()
    {
        if (transientDefaultRuleSet == null)
        {
            transientDefaultRuleSet = new GeoscapeRuleSet();
            transientDefaultRuleSet.CopyFromDefaults(this);
        }
        else
        {
            transientDefaultRuleSet.CopyFromDefaults(this);
        }
        return transientDefaultRuleSet;
    }

    private void DrawContextBanner(Listing_Standard listing, GeoscapeCampaignRules? campaign)
    {
        Rect bannerRect = listing.GetRect(28f);
        if (campaign != null)
        {
            if (campaign.HasExternalModifications)
            {
                Widgets.DrawBoxSolid(bannerRect, new Color(0.5f, 0.1f, 0.1f, 0.65f));
                Widgets.DrawHighlight(bannerRect);
                GUI.color = new Color(1f, 0.35f, 0.35f);
                Widgets.Label(bannerRect.ContractedBy(4f), "Active Save: EXTERNAL SAVE MODIFICATION DETECTED (Active rules diverge from audit ledger)");
                GUI.color = Color.white;
            }
            else if (UnsafeRuleOverride.Effective)
            {
                Widgets.DrawBoxSolid(bannerRect, new Color(0.4f, 0.1f, 0.1f, 0.5f));
                Widgets.DrawHighlight(bannerRect);
                GUI.color = new Color(1f, 0.4f, 0.4f);
                Widgets.Label(bannerRect.ContractedBy(4f), "Active Save: DEV OVERRIDE ACTIVE (All rule mutability restrictions bypassed)");
                GUI.color = Color.white;
            }
            else if (campaign.ReviewWindowOpen)
            {
                Widgets.DrawBoxSolid(bannerRect, new Color(0.1f, 0.35f, 0.2f, 0.5f));
                Widgets.DrawHighlight(bannerRect);
                GUI.color = new Color(0.4f, 1f, 0.5f);
                Widgets.Label(bannerRect.ContractedBy(4f), "Active Save: 1-Day Review Window Open (Free rule adjustments permitted)");
                GUI.color = Color.white;
            }
            else
            {
                Widgets.DrawBoxSolid(bannerRect, new Color(0.1f, 0.2f, 0.3f, 0.5f));
                Widgets.DrawHighlight(bannerRect);
                GUI.color = new Color(0.6f, 0.85f, 1f);
                Widgets.Label(bannerRect.ContractedBy(4f), "Active Save: Campaign Rules Bound to Save (Immutable rules locked; Escalation allowed)");
                GUI.color = Color.white;
            }
        }
        else
        {
            Widgets.DrawBoxSolid(bannerRect, new Color(0.15f, 0.15f, 0.15f, 0.5f));
            Widgets.DrawHighlight(bannerRect);
            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            Widgets.Label(bannerRect.ContractedBy(4f), "New Campaign Defaults (These settings initialise newly created games)");
            GUI.color = Color.white;
        }
        listing.Gap(4f);
    }

    private void DrawSensoryRadarTab(Listing_Standard listing)
    {
        GeoscapeCampaignRules? campaign = GeoscapeCampaignRules.Active;
        bool inCampaign = campaign != null;
        GeoscapeRuleSet rules = campaign != null ? campaign.Rules : GetOrCreateTransientRuleSet();
        RuleEditContext ctx = campaign != null ? campaign.CreateEditContext() : new RuleEditContext(false, false, false);

        DrawContextBanner(listing, campaign);

        CampaignRulesPanel.DrawSensoryVisibilitySection(listing, rules, campaign, ctx, showLockPreview: !inCampaign);

        if (rules.Visibility.Value != VisibilityMode.Omniscient)
        {
            CampaignRulesPanel.DrawLookoutScoutingSection(listing, rules, campaign, ctx, showLockPreview: !inCampaign);

            Text.Font = GameFont.Medium;
            listing.Label("Tactical Radar Overlay & Range Outlines");
            Text.Font = GameFont.Small;
            listing.GapLine();

            // 1. Overlay Mode
            if (listing.RadioButton("Boundary Ring & Honeycomb Grid", radarOutlineStyle == RadarOutlineStyle.BoundaryAndGrid, tooltip: "Renders glowing perimeter boundary rings and an interior hexagonal holographic grid over active sensor envelopes."))
            {
                if (radarOutlineStyle != RadarOutlineStyle.BoundaryAndGrid)
                {
                    radarOutlineStyle = RadarOutlineStyle.BoundaryAndGrid;
                    NotifyRadarVisualsChanged();
                }
            }

            if (listing.RadioButton("Boundary Ring Only (Clean Perimeter)", radarOutlineStyle == RadarOutlineStyle.BoundaryOnly, tooltip: "Renders only the outer glowing perimeter boundary ring around active radar coverage."))
            {
                if (radarOutlineStyle != RadarOutlineStyle.BoundaryOnly)
                {
                    radarOutlineStyle = RadarOutlineStyle.BoundaryOnly;
                    NotifyRadarVisualsChanged();
                }
            }

            if (listing.RadioButton("None (Undecorated Globe)", radarOutlineStyle == RadarOutlineStyle.None, tooltip: "Hides radar range outlines entirely."))
            {
                if (radarOutlineStyle != RadarOutlineStyle.None)
                {
                    radarOutlineStyle = RadarOutlineStyle.None;
                    NotifyRadarVisualsChanged();
                }
            }

            // 2. Outer Detection Perimeter
            listing.Gap(6f);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.9f, 1f);
            listing.Label("OUTER DETECTION PERIMETER");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            bool prevUseFaction = usePlayerFactionColor;
            listing.CheckboxLabeled(
                "Use Player Faction Color",
                ref usePlayerFactionColor,
                "Automatically matches radar and boundary outlines to your player faction's primary banner color.");
            if (prevUseFaction != usePlayerFactionColor)
            {
                NotifyRadarVisualsChanged();
            }

            Color activeColor = EffectiveRadarColor;
            Rect colorBarRect = listing.GetRect(26f);
            Widgets.DrawBoxSolid(new Rect(colorBarRect.x, colorBarRect.y, 32f, 24f), activeColor);

            Faction? playerFaction = (Current.ProgramState == ProgramState.Playing && Current.Game != null && Find.FactionManager != null)
                ? Faction.OfPlayerSilentFail
                : null;

            if (usePlayerFactionColor && playerFaction != null)
            {
                Widgets.Label(
                    new Rect(colorBarRect.x + 38f, colorBarRect.y + 2f, colorBarRect.width - 38f, 24f),
                    $"Synced: {playerFaction.Name} (#{ColorUtility.ToHtmlStringRGB(activeColor)})");
            }
            else if (usePlayerFactionColor)
            {
                Widgets.Label(
                    new Rect(colorBarRect.x + 38f, colorBarRect.y + 2f, colorBarRect.width - 38f, 24f),
                    $"Synced to Player Faction (#{ColorUtility.ToHtmlStringRGB(activeColor)})");
            }
            else
            {
                DrawCustomColorPicker(listing, colorBarRect, ref radarCustomColor, RadarPerimeterPresets);
            }

            Rect detSliderRect = listing.GetRect(26f);
            Widgets.Label(new Rect(detSliderRect.x, detSliderRect.y, 250f, 24f), $"Detection Perimeter Intensity: {radarOutlineOpacity:P0}");
            float newOpacity = Widgets.HorizontalSlider(
                new Rect(detSliderRect.x + 255f, detSliderRect.y + 2f, detSliderRect.width - 255f, 20f),
                radarOutlineOpacity, 0f, 1f);
            if (Mathf.Abs(newOpacity - radarOutlineOpacity) > 0.005f)
            {
                radarOutlineOpacity = newOpacity;
                NotifyRadarVisualsChanged();
            }

            if (radarOutlineStyle == RadarOutlineStyle.BoundaryAndGrid)
            {
                Rect gridSliderRect = listing.GetRect(26f);
                Widgets.Label(new Rect(gridSliderRect.x, gridSliderRect.y, 250f, 24f), $"Honeycomb Grid Intensity: {honeycombGridOpacity:P0}");
                float newGridOpacity = Widgets.HorizontalSlider(
                    new Rect(gridSliderRect.x + 255f, gridSliderRect.y + 2f, gridSliderRect.width - 255f, 20f),
                    honeycombGridOpacity, 0f, 1f);
                if (Mathf.Abs(newGridOpacity - honeycombGridOpacity) > 0.005f)
                {
                    honeycombGridOpacity = newGridOpacity;
                    NotifyRadarVisualsChanged();
                }
            }

            // 3. Inner Identification Horizon
            listing.Gap(6f);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.9f, 1f);
            listing.Label("INNER IDENTIFICATION HORIZON");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            bool prevShowId = showIdentificationHorizon;
            listing.CheckboxLabeled(
                "Show Identification Horizon",
                ref showIdentificationHorizon,
                "Renders an inner concentric tactical horizon ring marking the transition where contact tracks are classified into faction and force intelligence.");
            if (prevShowId != showIdentificationHorizon)
            {
                NotifyRadarVisualsChanged();
            }

            if (showIdentificationHorizon)
            {
                Rect idSliderRect = listing.GetRect(26f);
                Widgets.Label(new Rect(idSliderRect.x, idSliderRect.y, 250f, 24f), $"Identification Horizon Intensity: {identificationRingOpacity:P0}");
                float newIdOpacity = Widgets.HorizontalSlider(
                    new Rect(idSliderRect.x + 255f, idSliderRect.y + 2f, idSliderRect.width - 255f, 20f),
                    identificationRingOpacity, 0f, 1f);
                if (Mathf.Abs(newIdOpacity - identificationRingOpacity) > 0.005f)
                {
                    identificationRingOpacity = newIdOpacity;
                    NotifyRadarVisualsChanged();
                }

                bool prevUseCustomIdColor = useCustomIdentificationColor;
                listing.CheckboxLabeled(
                    "Use Custom Identification Color",
                    ref useCustomIdentificationColor,
                    "Select an explicit custom color rather than auto-deriving a complementary hue from the detection perimeter.");
                if (prevUseCustomIdColor != useCustomIdentificationColor)
                {
                    NotifyRadarVisualsChanged();
                }

                Color activeIdColor = EffectiveIdentificationColor;
                Rect idColorBarRect = listing.GetRect(26f);
                Widgets.DrawBoxSolid(new Rect(idColorBarRect.x, idColorBarRect.y, 32f, 24f), activeIdColor);

                if (!useCustomIdentificationColor)
                {
                    Widgets.Label(
                        new Rect(idColorBarRect.x + 38f, idColorBarRect.y + 2f, idColorBarRect.width - 38f, 24f),
                        $"Auto Complementary: Synced to Perimeter (#{ColorUtility.ToHtmlStringRGB(activeIdColor)})");
                }
                else
                {
                    DrawCustomColorPicker(listing, idColorBarRect, ref radarIdentificationColor, IdentificationRingPresets, "Identification");
                }
            }

            // 4. Mountain Radar Shadows
            listing.Gap(6f);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.9f, 1f);
            listing.Label("MOUNTAIN RADAR SHADOWS (MODEL B+)");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            if (listing.RadioButton("Both (Dashed Contours & Cell Hatching)", radarTerrainShadowStyle == RadarTerrainShadowStyle.Both, tooltip: "Renders dashed amber ribbons around internal mountain shadows and diagonal scanline hatching across masked mountain hex surfaces within identification range."))
            {
                if (radarTerrainShadowStyle != RadarTerrainShadowStyle.Both)
                {
                    radarTerrainShadowStyle = RadarTerrainShadowStyle.Both;
                    NotifyRadarVisualsChanged();
                }
            }

            if (listing.RadioButton("Dashed Contours Only", radarTerrainShadowStyle == RadarTerrainShadowStyle.DashedContours, tooltip: "Renders dashed amber ribbons enclosing internal mountain clusters and radar shadows inside identification range."))
            {
                if (radarTerrainShadowStyle != RadarTerrainShadowStyle.DashedContours)
                {
                    radarTerrainShadowStyle = RadarTerrainShadowStyle.DashedContours;
                    NotifyRadarVisualsChanged();
                }
            }

            if (listing.RadioButton("Cell Hatching Only", radarTerrainShadowStyle == RadarTerrainShadowStyle.CellHatching, tooltip: "Renders subtle radar static and diagonal scanline hatching across masked mountain hex surfaces inside identification range."))
            {
                if (radarTerrainShadowStyle != RadarTerrainShadowStyle.CellHatching)
                {
                    radarTerrainShadowStyle = RadarTerrainShadowStyle.CellHatching;
                    NotifyRadarVisualsChanged();
                }
            }

            if (listing.RadioButton("None (Undecorated)", radarTerrainShadowStyle == RadarTerrainShadowStyle.None, tooltip: "Hides visual shadow indicators on masked mountain tiles inside identification range."))
            {
                if (radarTerrainShadowStyle != RadarTerrainShadowStyle.None)
                {
                    radarTerrainShadowStyle = RadarTerrainShadowStyle.None;
                    NotifyRadarVisualsChanged();
                }
            }

            // 5. Background Dimming
            listing.Gap(6f);
            Rect dimSliderRect = listing.GetRect(26f);
            Widgets.Label(new Rect(dimSliderRect.x, dimSliderRect.y, 250f, 24f), $"Surveillance Background Dimming: {surveillanceDimming:P0}");
            float newDimming = Widgets.HorizontalSlider(
                new Rect(dimSliderRect.x + 255f, dimSliderRect.y + 2f, dimSliderRect.width - 255f, 20f),
                surveillanceDimming, 0.0f, 1.0f);
            if (Mathf.Abs(newDimming - surveillanceDimming) > 0.005f)
            {
                surveillanceDimming = newDimming;
                NotifyRadarVisualsChanged();
            }
        }

        listing.Gap();
        listing.CheckboxLabeled(
            "Tactical Contact Disambiguation Menu",
            ref enableOverlappingEntitySelectionMenu,
            "When entities overlap on the planetary Geoscape (such as a caravan stationed at a colony), open a tactical selection menu rather than blindly cycling.");

        listing.CheckboxLabeled(
            "Intelligent Geoscape Right-Click Dispatch",
            ref enableIntelligentRightClick,
            "Allow right-clicking anywhere on the Geoscape without selecting a source first, intelligently suggesting actions from the nearest colonies and caravans.");

        if (enableIntelligentRightClick)
        {
            listing.Gap(4f);
            listing.Label($"Max Contextual Caravans Displayed: {maxContextualCaravansShown}", tooltip: "Limits the number of nearest player caravans directly displayed on the right-click menu. Additional caravans can be accessed via a drilldown sub-menu.");
            maxContextualCaravansShown = Mathf.RoundToInt(listing.Slider(maxContextualCaravansShown, 1f, 8f));

            listing.Label($"Max Contextual Colonies Displayed: {maxContextualColoniesShown}", tooltip: "Limits the number of nearest player colonies directly displayed on the right-click menu. Additional colonies can be accessed via a drilldown sub-menu.");
            maxContextualColoniesShown = Mathf.RoundToInt(listing.Slider(maxContextualColoniesShown, 1f, 5f));
        }

        if (!inCampaign)
        {
            rules.WriteToDefaults(this);
        }
    }

    // =========================================================================
    // Tab 2: Incursions & Overland AI
    // =========================================================================

    private void DrawIncursionsTab(Listing_Standard listing)
    {
        GeoscapeCampaignRules? campaign = GeoscapeCampaignRules.Active;
        bool inCampaign = campaign != null;
        GeoscapeRuleSet rules = campaign != null ? campaign.Rules : GetOrCreateTransientRuleSet();
        RuleEditContext ctx = campaign != null ? campaign.CreateEditContext() : new RuleEditContext(false, false, false);

        DrawContextBanner(listing, campaign);

        CampaignRulesPanel.DrawIncursionsSection(listing, rules, campaign, ctx, showLockPreview: !inCampaign);

        listing.CheckboxLabeled(
            "Show faction emblems on Geoscape incursions",
            ref showFactionEmblems,
            "Render the attacking faction's emblem icon on planetary incursions once classified, instead of generic markers.");

        if (!inCampaign)
        {
            rules.WriteToDefaults(this);
        }
    }

    // =========================================================================
    // Tab 3: Early Warning & Intelligence
    // =========================================================================

    private void DrawEarlyWarningIntelTab(Listing_Standard listing)
    {
        GeoscapeCampaignRules? campaign = GeoscapeCampaignRules.Active;
        bool inCampaign = campaign != null;
        GeoscapeRuleSet rules = campaign != null ? campaign.Rules : GetOrCreateTransientRuleSet();
        RuleEditContext ctx = campaign != null ? campaign.CreateEditContext() : new RuleEditContext(false, false, false);

        DrawContextBanner(listing, campaign);

        CampaignRulesPanel.DrawNotificationsSection(listing, rules, campaign, ctx, showLockPreview: !inCampaign);

        Text.Font = GameFont.Medium;
        listing.Label("Epistemic Discovery: \"You Don't Know What You Don't Know\"");
        Text.Font = GameFont.Small;
        listing.GapLine();

        Rect infoRect = listing.GetRect(190f);
        Widgets.DrawBoxSolid(infoRect, new Color(0.12f, 0.15f, 0.20f, 0.6f));
        Widgets.DrawBox(infoRect);
        Rect textRect = infoRect.ContractedBy(8f);

        string infoText = "Project Vigilance models information warfare. Threats evolve across a locked 5-tier intelligence architecture as sensor telemetry deepens:\n\n" +
                          "• Undetected: Shrouded in planetary fog of war. Zero tactical awareness.\n" +
                          "• Suspected: Distant cues from smoke columns, traveller rumours, or acoustic anomalies.\n" +
                          "• Contact: Radar track or lookout visual. Planetary position, velocity vector, and heading known.\n" +
                          "• Classified: Faction identity and force size revealed within discrete identification rings (expanded via multi-dish interferometry).\n" +
                          "• Analysed: Full tactical breakdown (pawn headcounts, weaponry, target colony, ETA) via linked SIGINT decryption or field interception.";

        Widgets.Label(textRect, infoText);

        listing.Gap();

        Text.Font = GameFont.Medium;
        listing.Label("Sensory Narrative & Tech-Level Adaptability");
        Text.Font = GameFont.Small;
        listing.GapLine();

        Rect techRect = listing.GetRect(90f);
        Widgets.DrawBoxSolid(techRect, new Color(0.12f, 0.15f, 0.20f, 0.6f));
        Widgets.DrawBox(techRect);
        Rect techTextRect = techRect.ContractedBy(8f);

        string techInfo = "Sensory feedback adapts dynamically to your colony's tech level:\n" +
                          "• Neolithic / Medieval: Lookouts use narrative, observational language (e.g., 'Approaching Strangers', 'Band of hostile fighters').\n" +
                          "• Industrial / Spacer: Advanced sensors provide military telemetry (e.g., 'Contact-03', 'Bearing South-Southeast, ~24 hexes/day').";

        Widgets.Label(techTextRect, techInfo);

        if (!inCampaign)
        {
            rules.WriteToDefaults(this);
        }
    }

    // =========================================================================
    // Tab 4: About Suite & Diagnostics
    // =========================================================================

    private void DrawAboutDiagnosticsTab(Listing_Standard listing)
    {
        Text.Font = GameFont.Medium;
        listing.Label("Project Vigilance — Modular Architecture");
        Text.Font = GameFont.Small;
        listing.GapLine();

        Rect descRect = listing.GetRect(85f);
        Widgets.DrawBoxSolid(descRect, new Color(0.12f, 0.15f, 0.20f, 0.6f));
        Widgets.DrawBox(descRect);
        Rect descTextRect = descRect.ContractedBy(8f);
        string desc = "Project Vigilance is engineered as a decoupled suite of standalone utility modules designed for maximum compatibility with RimWorld 1.7.\n\n" +
                      "Each module functions as an independent framework while seamlessly interconnecting when multiple modules are active.";
        Widgets.Label(descTextRect, desc);

        listing.Gap();

        Text.Font = GameFont.Medium;
        listing.Label("Installed & Available Modules");
        Text.Font = GameFont.Small;
        listing.GapLine();

        Rect modTableRect = listing.GetRect(130f);
        Widgets.DrawBoxSolid(modTableRect, new Color(0.08f, 0.11f, 0.14f, 0.8f));
        Widgets.DrawBox(modTableRect);
        Rect modContentRect = modTableRect.ContractedBy(8f);

        string modulesStatus = "• Module 1: Geoscape & Radar Grid (Active — v0.3.0)\n" +
                               "   Planetary fog of war, early-warning radar domes, sensor grids, incursion tracking.\n" +
                               "• Module 2: Tactical Ground Interception & Forward Ambush (Active — v0.3.0)\n" +
                               "   Dynamic overland pursuit, unaware column ambushes, field scouting recon, hostage rescue.\n" +
                               "• Module 3: Containment & Biology (Planned)\n" +
                               "   Stasis holding pods, surgical autopsies, and alien/entity reverse-engineering.";
        Widgets.Label(modContentRect, modulesStatus);

        listing.Gap();

        GeoscapeCampaignRules? campaign = GeoscapeCampaignRules.Active;
        if (campaign != null)
        {
            Text.Font = GameFont.Medium;
            listing.Label("Active Campaign Rules & Governance");
            Text.Font = GameFont.Small;
            listing.GapLine();

            string statusText = $"• Active Profile: {campaign.Rules.DetectCurrentProfile().GetDisplayName()}\n" +
                                $"• Schema Version: v{campaign.SchemaVersion}\n" +
                                $"• Campaign Integrity: {campaign.IntegrityStatusLabel}\n" +
                                $"• Review Window: {(campaign.ReviewWindowOpen ? "Open (Day 1 Grace Period)" : "Locked")}\n" +
                                $"• Ledger Records: {campaign.Ledger.Count} entries";
            listing.Label(statusText);

            if (campaign.HasExternalModifications)
            {
                RuleChangeRecord? lastMod = campaign.Ledger.LastOrDefault(r => r.kind == RuleChangeKind.ExternalModification);
                if (lastMod != null)
                {
                    listing.Gap(4f);
                    Rect warnRect = listing.GetRect(44f);
                    Widgets.DrawBoxSolid(warnRect, new Color(0.35f, 0.08f, 0.08f, 0.6f));
                    Widgets.DrawBox(warnRect);
                    GUI.color = new Color(1f, 0.45f, 0.45f);
                    Widgets.Label(warnRect.ContractedBy(6f), $"[Save Integrity Warning] {lastMod.note}");
                    GUI.color = Color.white;
                }
            }

            if (campaign.ReviewWindowOpen)
            {
                if (listing.ButtonText("Confirm & Lock In Campaign Rules Now"))
                {
                    campaign.ConfirmAndLock();
                    SoundDefOf.Click?.PlayOneShotOnCamera();
                    Messages.Message("Campaign rules locked. Mutability restrictions now active.", MessageTypeDefOf.PositiveEvent, historical: false);
                }
            }

            if (Prefs.DevMode)
            {
                listing.Gap(6f);
                bool req = UnsafeRuleOverride.Requested;
                bool prev = req;
                listing.CheckboxLabeled(
                    "Allow Unsafe Rule Transitions (Session Only)",
                    ref req,
                    "Developer Mode override. Bypasses all mutability constraints, allowing downgrades and editing of immutable rules. All changes are marked [Unsafe] in the audit ledger.");
                if (req != prev)
                {
                    UnsafeRuleOverride.SetRequested(req);
                }
            }

            listing.Gap();
        }

        Text.Font = GameFont.Medium;
        listing.Label("Configuration Management");
        Text.Font = GameFont.Small;
        listing.GapLine();

        string resetBtnLabel = campaign != null
            ? "Reset New Campaign Defaults to Factory Settings"
            : "Reset All Geoscape Settings to Defaults";

        if (listing.ButtonText(resetBtnLabel))
        {
            ResetToDefaults();
            SoundDefOf.ClickReject?.PlayOneShotOnCamera();
            string msg = campaign != null
                ? "Project Vigilance new campaign defaults reset. Active save rules remain unaffected."
                : "Project Vigilance settings reset to default values.";
            Messages.Message(msg, MessageTypeDefOf.NeutralEvent, historical: false);
        }
    }

    public void ResetToDefaults()
    {
        synchronizeMultiVectorRaids = true;
        showFactionEmblems = true;
        resetThreatCooldownOnArrival = false;
        enableTacticalIngressFlanking = true;
        visibilityMode = VisibilityMode.ClassicHazeOfWar;
        radarOutlineStyle = RadarOutlineStyle.BoundaryAndGrid;
        usePlayerFactionColor = true;
        radarCustomColor = new Color(0f, 0.90f, 1.0f);
        radarOutlineOpacity = 0.50f;
        identificationRingOpacity = 0.65f;
        honeycombGridOpacity = 0.40f;
        useCustomIdentificationColor = false;
        radarIdentificationColor = new Color(1.0f, 0.82f, 0.15f);
        surveillanceDimming = 0.25f;
        colonyScoutRadius = 5.0f;
        caravanScoutRadius = 3.0f;
        enableEarlyWarningLetters = true;
        enableNighttimeSensoryReduction = true;
        incursionsRestAtNight = true;
        showIdentificationHorizon = true;
        radarTerrainShadowStyle = RadarTerrainShadowStyle.Both;
        enableOverlappingEntitySelectionMenu = true;
        enableIntelligentRightClick = true;
        maxContextualCaravansShown = 3;
        maxContextualColoniesShown = 2;
        NotifyRadarVisualsChanged();
        OnVisibilityModeChanged();
    }

    private static readonly (string label, Color color)[] RadarPerimeterPresets = new[]
    {
        ("Cyan", new Color(0f, 0.90f, 1f)),
        ("Amber", new Color(1f, 0.76f, 0.16f)),
        ("Emerald", new Color(0.08f, 0.94f, 0.43f)),
        ("Crimson", new Color(1f, 0.18f, 0.29f)),
        ("White", new Color(0.90f, 0.94f, 0.98f))
    };

    private static readonly (string label, Color color)[] IdentificationRingPresets = new[]
    {
        ("Gold", new Color(1f, 0.82f, 0.15f)),
        ("Cyan", new Color(0f, 0.90f, 1f)),
        ("Emerald", new Color(0.08f, 0.94f, 0.43f)),
        ("Crimson", new Color(1f, 0.18f, 0.29f)),
        ("White", new Color(0.90f, 0.94f, 0.98f))
    };

    private void DrawCustomColorPicker(
        Listing_Standard listing,
        Rect colorBarRect,
        ref Color color,
        (string label, Color color)[] presets,
        string labelPrefix = "")
    {
        string hexStr = "#" + ColorUtility.ToHtmlStringRGB(color);
        string newHex = Widgets.TextField(new Rect(colorBarRect.x + 44f, colorBarRect.y, 90f, 26f), hexStr);
        if (newHex != hexStr && ColorUtility.TryParseHtmlString(newHex, out Color parsed))
        {
            color = parsed;
            NotifyRadarVisualsChanged();
        }

        float presetStart = colorBarRect.x + 144f;
        float presetWidth = (colorBarRect.width - 144f) / presets.Length;
        for (int i = 0; i < presets.Length; i++)
        {
            var p = presets[i];
            Rect btnRect = new Rect(presetStart + presetWidth * i, colorBarRect.y, presetWidth - 4f, 26f);
            if (Widgets.ButtonText(btnRect, p.label))
            {
                color = p.color;
                usePlayerFactionColor = false;
                NotifyRadarVisualsChanged();
            }
        }

        string prefix = string.IsNullOrEmpty(labelPrefix) ? "" : labelPrefix + " ";
        Rect rRow = listing.GetRect(22f);
        Widgets.Label(new Rect(rRow.x, rRow.y, 90f, 22f), $"{prefix}Red: {Mathf.RoundToInt(color.r * 255f)}");
        float r = Widgets.HorizontalSlider(new Rect(rRow.x + 95f, rRow.y + 2f, rRow.width - 95f, 18f), color.r, 0f, 1f);

        Rect gRow = listing.GetRect(22f);
        Widgets.Label(new Rect(gRow.x, gRow.y, 90f, 22f), $"{prefix}Green: {Mathf.RoundToInt(color.g * 255f)}");
        float g = Widgets.HorizontalSlider(new Rect(gRow.x + 95f, gRow.y + 2f, gRow.width - 95f, 18f), color.g, 0f, 1f);

        Rect bRow = listing.GetRect(22f);
        Widgets.Label(new Rect(bRow.x, bRow.y, 90f, 22f), $"{prefix}Blue: {Mathf.RoundToInt(color.b * 255f)}");
        float b = Widgets.HorizontalSlider(new Rect(bRow.x + 95f, bRow.y + 2f, bRow.width - 95f, 18f), color.b, 0f, 1f);

        if (Mathf.Abs(r - color.r) > 0.005f ||
            Mathf.Abs(g - color.g) > 0.005f ||
            Mathf.Abs(b - color.b) > 0.005f)
        {
            color = new Color(r, g, b, 1f);
            NotifyRadarVisualsChanged();
        }
    }

    private void DrawPresetButton(Rect rect, string label, Action onSelect)
    {
        if (Widgets.ButtonText(rect, label))
        {
            onSelect();
            NotifyRadarVisualsChanged();
        }
    }

    private void NotifyRadarVisualsChanged()
    {
        if (Current.ProgramState == ProgramState.Playing && Find.World != null)
        {
            WorldDrawLayer_RadarCoverage.NotifyVisualsChanged();
        }
    }

    private void OnVisibilityModeChanged()
    {
        RuleTransitionExecutor.Execute(RuleTransitionEffects.RebuildWorldLayers);
    }
}
