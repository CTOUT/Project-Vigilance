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
    public float surveillanceDimming = 0.0f;
    public float colonyScoutRadius = 5.0f;
    public float caravanScoutRadius = 3.0f;
    public bool enableEarlyWarningLetters = true;
    public bool enableNighttimeSensoryReduction = true;
    public bool incursionsRestAtNight = true;
    public bool showIdentificationHorizon = true;

    // =========================================================================
    // Transient UI State (Tabs, Scrolling & Dynamic Sizing)
    // =========================================================================

    private static GeoscapeSettingsTab currentTab = GeoscapeSettingsTab.SensoryRadar;
    private static readonly Vector2[] scrollPositions = new Vector2[4];
    private static readonly float[] tabHeights = { 860f, 520f, 660f, 480f };

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
        Scribe_Values.Look(ref showIdentificationHorizon, "showIdentificationHorizon", true);
        Scribe_Values.Look(ref surveillanceDimming, "surveillanceDimming", 0.0f);
        Scribe_Values.Look(ref colonyScoutRadius, "colonyScoutRadius", 5.0f);
        Scribe_Values.Look(ref caravanScoutRadius, "caravanScoutRadius", 3.0f);
        Scribe_Values.Look(ref enableEarlyWarningLetters, "enableEarlyWarningLetters", true);
        Scribe_Values.Look(ref enableNighttimeSensoryReduction, "enableNighttimeSensoryReduction", true);
        Scribe_Values.Look(ref incursionsRestAtNight, "incursionsRestAtNight", true);
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
        Rect viewRect = new Rect(0f, 0f, contentRect.width - 24f, tabHeights[tabIndex]);

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

        tabHeights[tabIndex] = Mathf.Max(listing.CurHeight + 40f, contentRect.height);
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
            if (UnsafeRuleOverride.Effective)
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
            listing.Label("Tactical Radar Overlay & Range Outlines (X-COM Style)");
            Text.Font = GameFont.Small;
            listing.GapLine();

            if (listing.RadioButton("Boundary Ring & Honeycomb Grid (Preview Style)", radarOutlineStyle == RadarOutlineStyle.BoundaryAndGrid, tooltip: "Renders glowing perimeter boundary rings and an interior hexagonal holographic grid over active sensor envelopes."))
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

            if (radarOutlineStyle != RadarOutlineStyle.None)
            {
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
                Rect colorBarRect = listing.GetRect(28f);
                Widgets.DrawBoxSolid(new Rect(colorBarRect.x, colorBarRect.y, 36f, 26f), activeColor);

                Faction? playerFaction = (Current.ProgramState == ProgramState.Playing && Current.Game != null && Find.FactionManager != null)
                    ? Faction.OfPlayerSilentFail
                    : null;

                if (usePlayerFactionColor && playerFaction != null)
                {
                    Widgets.Label(
                        new Rect(colorBarRect.x + 44f, colorBarRect.y + 3f, colorBarRect.width - 44f, 24f),
                        $"Synced to {playerFaction.Name} (#{ColorUtility.ToHtmlStringRGB(activeColor)})");
                }
                else if (usePlayerFactionColor)
                {
                    Widgets.Label(
                        new Rect(colorBarRect.x + 44f, colorBarRect.y + 3f, colorBarRect.width - 44f, 24f),
                        $"Synced to Player Faction color during gameplay (#{ColorUtility.ToHtmlStringRGB(activeColor)})");
                }
                else
                {
                    string hexStr = "#" + ColorUtility.ToHtmlStringRGB(radarCustomColor);
                    string newHex = Widgets.TextField(new Rect(colorBarRect.x + 44f, colorBarRect.y, 90f, 26f), hexStr);
                    if (newHex != hexStr && ColorUtility.TryParseHtmlString(newHex, out Color parsed))
                    {
                        radarCustomColor = parsed;
                        NotifyRadarVisualsChanged();
                    }

                    float presetStart = colorBarRect.x + 144f;
                    float presetWidth = (colorBarRect.width - 144f) / 5f;
                    DrawPresetButton(new Rect(presetStart + presetWidth * 0, colorBarRect.y, presetWidth - 4f, 26f), new Color(0f, 0.90f, 1f), "Cyan");
                    DrawPresetButton(new Rect(presetStart + presetWidth * 1, colorBarRect.y, presetWidth - 4f, 26f), new Color(1f, 0.76f, 0.16f), "Amber");
                    DrawPresetButton(new Rect(presetStart + presetWidth * 2, colorBarRect.y, presetWidth - 4f, 26f), new Color(0.08f, 0.94f, 0.43f), "Emerald");
                    DrawPresetButton(new Rect(presetStart + presetWidth * 3, colorBarRect.y, presetWidth - 4f, 26f), new Color(1f, 0.18f, 0.29f), "Crimson");
                    DrawPresetButton(new Rect(presetStart + presetWidth * 4, colorBarRect.y, presetWidth - 4f, 26f), new Color(0.90f, 0.94f, 0.98f), "White");

                    listing.Label($"Red: {Mathf.RoundToInt(radarCustomColor.r * 255f)}");
                    float r = listing.Slider(radarCustomColor.r, 0f, 1f);
                    listing.Label($"Green: {Mathf.RoundToInt(radarCustomColor.g * 255f)}");
                    float g = listing.Slider(radarCustomColor.g, 0f, 1f);
                    listing.Label($"Blue: {Mathf.RoundToInt(radarCustomColor.b * 255f)}");
                    float b = listing.Slider(radarCustomColor.b, 0f, 1f);

                    if (Mathf.Abs(r - radarCustomColor.r) > 0.005f ||
                        Mathf.Abs(g - radarCustomColor.g) > 0.005f ||
                        Mathf.Abs(b - radarCustomColor.b) > 0.005f)
                    {
                        radarCustomColor = new Color(r, g, b, 1f);
                        NotifyRadarVisualsChanged();
                    }
                }

                listing.Label($"Radar Outline Intensity: {radarOutlineOpacity:P0}");
                float newOpacity = listing.Slider(radarOutlineOpacity, 0f, 1f);
                if (Mathf.Abs(newOpacity - radarOutlineOpacity) > 0.005f)
                {
                    radarOutlineOpacity = newOpacity;
                    NotifyRadarVisualsChanged();
                }

                bool prevShowId = showIdentificationHorizon;
                listing.CheckboxLabeled(
                    "Show Identification Horizon",
                    ref showIdentificationHorizon,
                    "Renders an inner concentric tactical horizon ring marking the transition where contact tracks are classified into faction and force intelligence. Expanded outward across discrete integer traversal rings by multi-dish radar interferometry.");
                if (prevShowId != showIdentificationHorizon)
                {
                    NotifyRadarVisualsChanged();
                }
            }

            listing.Label($"Surveillance Background Dimming: {surveillanceDimming:P0}");
            float newDimming = listing.Slider(surveillanceDimming, 0.0f, 1.0f);
            if (Mathf.Abs(newDimming - surveillanceDimming) > 0.005f)
            {
                surveillanceDimming = newDimming;
                NotifyRadarVisualsChanged();
            }
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
                                $"• Review Window: {(campaign.ReviewWindowOpen ? "Open (Day 1 Grace Period)" : "Locked")}\n" +
                                $"• Ledger Records: {campaign.Ledger.Count} entries";
            listing.Label(statusText);

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
        surveillanceDimming = 0.0f;
        colonyScoutRadius = 5.0f;
        caravanScoutRadius = 3.0f;
        enableEarlyWarningLetters = true;
        enableNighttimeSensoryReduction = true;
        incursionsRestAtNight = true;
        showIdentificationHorizon = true;
        NotifyRadarVisualsChanged();
        OnVisibilityModeChanged();
    }

    private void DrawPresetButton(Rect rect, Color color, string label)
    {
        if (Widgets.ButtonText(rect, label))
        {
            radarCustomColor = color;
            usePlayerFactionColor = false;
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
