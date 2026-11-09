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
    Sensory,
    Radar,
    Incursions,
    Geoscape,
    Diagnostics
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

    private static GeoscapeSettingsTab currentTab = GeoscapeSettingsTab.Sensory;
    private static readonly Vector2[] scrollPositions = new Vector2[5];
    private static readonly float[] tabHeights = { 460f, 580f, 320f, 480f, 420f };

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
        Rect tabRowRect = new Rect(inRect.x, inRect.y, inRect.width, 34f);
        string[] tabLabels = { "Sensory", "Radar", "Incursions", "Geoscape", "Diagnostics" };
        int tabCount = tabLabels.Length;
        float spacing = 6f;
        float tabWidth = (tabRowRect.width - (tabCount - 1) * spacing) / tabCount;

        for (int i = 0; i < tabCount; i++)
        {
            Rect tabBtnRect = new Rect(tabRowRect.x + i * (tabWidth + spacing), tabRowRect.y, tabWidth, 30f);
            bool isSelected = currentTab == (GeoscapeSettingsTab)i;

            // Draw button text; when selected, skip default grey background so custom highlight is visible
            if (Widgets.ButtonText(tabBtnRect, tabLabels[i], drawBackground: !isSelected))
            {
                if (currentTab != (GeoscapeSettingsTab)i)
                {
                    currentTab = (GeoscapeSettingsTab)i;
                    SoundDefOf.Click?.PlayOneShotOnCamera();
                }
            }

            if (isSelected)
            {
                // Active tab background and selection glow
                Widgets.DrawBoxSolid(tabBtnRect, new Color(0.12f, 0.22f, 0.32f, 0.85f));
                Widgets.DrawHighlightSelected(tabBtnRect);
                Widgets.DrawBoxSolid(new Rect(tabBtnRect.x, tabBtnRect.yMax - 3f, tabBtnRect.width, 3f), new Color(0f, 0.85f, 1f));

                // Re-render crisp cyan tab label over the active highlight
                TextAnchor prevAnchor = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(0.4f, 0.95f, 1f);
                Widgets.Label(tabBtnRect, tabLabels[i]);
                GUI.color = Color.white;
                Text.Anchor = prevAnchor;
            }
        }

        Widgets.DrawLineHorizontal(inRect.x, inRect.y + 36f, inRect.width);

        // 2. Scrollable Content View
        float contentTop = inRect.y + 40f;
        float contentHeight = inRect.height - 40f;
        Rect contentRect = new Rect(inRect.x, contentTop, inRect.width, contentHeight);

        int tabIndex = (int)currentTab;
        float requiredHeight = Mathf.Max(tabHeights[tabIndex], contentHeight);
        bool needsScrollbar = requiredHeight > contentHeight;
        Rect viewRect = new Rect(0f, 0f, contentRect.width - (needsScrollbar ? 16f : 0f), requiredHeight);

        Widgets.BeginScrollView(contentRect, ref scrollPositions[tabIndex], viewRect);
        Listing_Standard listing = new();
        listing.Begin(viewRect);

        switch (currentTab)
        {
            case GeoscapeSettingsTab.Sensory:
                DrawSensoryTab(listing);
                break;
            case GeoscapeSettingsTab.Radar:
                DrawRadarTab(listing);
                break;
            case GeoscapeSettingsTab.Incursions:
                DrawIncursionsTab(listing);
                break;
            case GeoscapeSettingsTab.Geoscape:
                DrawGeoscapeTab(listing);
                break;
            case GeoscapeSettingsTab.Diagnostics:
                DrawAboutDiagnosticsTab(listing);
                break;
        }

        tabHeights[tabIndex] = Mathf.Max(listing.CurHeight + 20f, contentHeight);
        listing.End();
        Widgets.EndScrollView();
    }

    // =========================================================================
    // Tab 1: Sensory & Visibility Rules
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

    private void DrawSensoryTab(Listing_Standard listing)
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
        }

        CampaignRulesPanel.DrawStartingSiteSelectionSection(listing, rules, campaign, ctx, showLockPreview: !inCampaign);

        if (!inCampaign)
        {
            rules.WriteToDefaults(this);
        }
    }

    // =========================================================================
    // Tab 2: Radar Overlay & Tactical Visuals
    // =========================================================================

    private void DrawRadarTab(Listing_Standard listing)
    {
        Text.Font = GameFont.Medium;
        listing.Label("Tactical Radar Overlay & Range Outlines");
        Text.Font = GameFont.Small;
        listing.Gap(4f);

        // 1. Header: Live HUD Scope Preview & Tactical Overlay Mode
        float headerHeight = 186f;
        Rect headerRect = listing.GetRect(headerHeight);

        float previewSize = 180f;
        Rect previewRect = new Rect(headerRect.x, headerRect.y, previewSize, previewSize);
        Rect modeColRect = new Rect(headerRect.x + previewSize + 16f, headerRect.y, headerRect.width - previewSize - 16f, headerHeight);

        DrawRadarScopePreview(previewRect);

        Listing_Standard modeListing = new();
        modeListing.Begin(modeColRect);
        {
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.9f, 1f);
            modeListing.Label("TACTICAL RADAR OVERLAY MODE");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            if (modeListing.RadioButton("Boundary Ring & Honeycomb Grid", radarOutlineStyle == RadarOutlineStyle.BoundaryAndGrid, tooltip: "Renders glowing perimeter boundary rings and an interior hexagonal holographic grid over active sensor envelopes."))
            {
                if (radarOutlineStyle != RadarOutlineStyle.BoundaryAndGrid)
                {
                    radarOutlineStyle = RadarOutlineStyle.BoundaryAndGrid;
                    NotifyRadarVisualsChanged();
                }
            }

            if (modeListing.RadioButton("Boundary Ring Only (Clean Perimeter)", radarOutlineStyle == RadarOutlineStyle.BoundaryOnly, tooltip: "Renders only the outer glowing perimeter boundary ring around active radar coverage."))
            {
                if (radarOutlineStyle != RadarOutlineStyle.BoundaryOnly)
                {
                    radarOutlineStyle = RadarOutlineStyle.BoundaryOnly;
                    NotifyRadarVisualsChanged();
                }
            }

            if (modeListing.RadioButton("None (Undecorated Globe)", radarOutlineStyle == RadarOutlineStyle.None, tooltip: "Hides radar range outlines entirely."))
            {
                if (radarOutlineStyle != RadarOutlineStyle.None)
                {
                    radarOutlineStyle = RadarOutlineStyle.None;
                    NotifyRadarVisualsChanged();
                }
            }

            modeListing.Gap(6f);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.85f, 0.95f);
            modeListing.Label("Real-time telemetry scope reacts live to all colour, intensity, horizon, and mountain shadow controls below.");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }
        modeListing.End();

        listing.GapLine();

        // 2. Two-Column Layout for Detection vs Identification
        float totalWidth = listing.ColumnWidth;
        float colWidth = (totalWidth - 16f) / 2f;
        float colHeight = (radarOutlineStyle == RadarOutlineStyle.BoundaryAndGrid) ? 190f : 150f;
        if (!usePlayerFactionColor || useCustomIdentificationColor)
        {
            colHeight += 36f;
        }

        Rect twoColRect = listing.GetRect(colHeight);
        Rect leftRect = new Rect(twoColRect.x, twoColRect.y, colWidth, twoColRect.height);
        Rect rightRect = new Rect(twoColRect.x + colWidth + 16f, twoColRect.y, colWidth, twoColRect.height);

        // Left Column: Outer Detection Perimeter
        Listing_Standard leftListing = new();
        leftListing.Begin(leftRect);
        {
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.9f, 1f);
            leftListing.Label("OUTER DETECTION PERIMETER");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            bool prevUseFaction = usePlayerFactionColor;
            leftListing.CheckboxLabeled(
                "Use Player Faction Colour",
                ref usePlayerFactionColor,
                "Automatically matches radar and boundary outlines to your player faction's primary banner colour.");
            if (prevUseFaction != usePlayerFactionColor)
            {
                NotifyRadarVisualsChanged();
            }

            Color activeColor = EffectiveRadarColor;
            Rect colorBarRect = leftListing.GetRect(24f);
            Widgets.DrawBoxSolid(new Rect(colorBarRect.x, colorBarRect.y, 28f, 22f), activeColor);

            Faction? playerFaction = (Current.ProgramState == ProgramState.Playing && Current.Game != null && Find.FactionManager != null)
                ? Faction.OfPlayerSilentFail
                : null;

            if (usePlayerFactionColor && playerFaction != null)
            {
                Widgets.Label(
                    new Rect(colorBarRect.x + 34f, colorBarRect.y + 1f, colorBarRect.width - 34f, 22f),
                    $"Synced: {playerFaction.Name} (#{ColorUtility.ToHtmlStringRGB(activeColor)})");
            }
            else if (usePlayerFactionColor)
            {
                Widgets.Label(
                    new Rect(colorBarRect.x + 34f, colorBarRect.y + 1f, colorBarRect.width - 34f, 22f),
                    $"Synced to Player Faction (#{ColorUtility.ToHtmlStringRGB(activeColor)})");
            }
            else
            {
                DrawCompactColorPicker(leftListing, colorBarRect, ref radarCustomColor, RadarPerimeterPresets, () => usePlayerFactionColor = false);
            }

            leftListing.Gap(4f);
            leftListing.Label($"Perimeter Intensity: {radarOutlineOpacity:P0}");
            float newOpacity = leftListing.Slider(radarOutlineOpacity, 0f, 1f);
            if (Mathf.Abs(newOpacity - radarOutlineOpacity) > 0.005f)
            {
                radarOutlineOpacity = newOpacity;
                NotifyRadarVisualsChanged();
            }

            if (radarOutlineStyle == RadarOutlineStyle.BoundaryAndGrid)
            {
                leftListing.Label($"Honeycomb Grid Intensity: {honeycombGridOpacity:P0}");
                float newGridOpacity = leftListing.Slider(honeycombGridOpacity, 0f, 1f);
                if (Mathf.Abs(newGridOpacity - honeycombGridOpacity) > 0.005f)
                {
                    honeycombGridOpacity = newGridOpacity;
                    NotifyRadarVisualsChanged();
                }
            }
        }
        leftListing.End();

        // Right Column: Inner Identification Horizon
        Listing_Standard rightListing = new();
        rightListing.Begin(rightRect);
        {
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.9f, 1f);
            rightListing.Label("INNER IDENTIFICATION HORIZON");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            bool prevShowId = showIdentificationHorizon;
            rightListing.CheckboxLabeled(
                "Show Identification Horizon",
                ref showIdentificationHorizon,
                "Renders an inner concentric tactical horizon ring marking the transition where contact tracks are classified into faction and force intelligence.");
            if (prevShowId != showIdentificationHorizon)
            {
                NotifyRadarVisualsChanged();
            }

            if (showIdentificationHorizon)
            {
                bool prevUseCustomIdColor = useCustomIdentificationColor;
                rightListing.CheckboxLabeled(
                    "Use Custom Identification Colour",
                    ref useCustomIdentificationColor,
                    "Select an explicit custom colour rather than auto-deriving a complementary hue from the detection perimeter.");
                if (prevUseCustomIdColor != useCustomIdentificationColor)
                {
                    NotifyRadarVisualsChanged();
                }

                Color activeIdColor = EffectiveIdentificationColor;
                Rect idColorBarRect = rightListing.GetRect(24f);
                Widgets.DrawBoxSolid(new Rect(idColorBarRect.x, idColorBarRect.y, 28f, 22f), activeIdColor);

                if (!useCustomIdentificationColor)
                {
                    Widgets.Label(
                        new Rect(idColorBarRect.x + 34f, idColorBarRect.y + 1f, idColorBarRect.width - 34f, 22f),
                        $"Auto Complementary (#{ColorUtility.ToHtmlStringRGB(activeIdColor)})");
                }
                else
                {
                    DrawCompactColorPicker(rightListing, idColorBarRect, ref radarIdentificationColor, IdentificationRingPresets, () => useCustomIdentificationColor = true);
                }

                rightListing.Gap(4f);
                rightListing.Label($"Identification Intensity: {identificationRingOpacity:P0}");
                float newIdOpacity = rightListing.Slider(identificationRingOpacity, 0f, 1f);
                if (Mathf.Abs(newIdOpacity - identificationRingOpacity) > 0.005f)
                {
                    identificationRingOpacity = newIdOpacity;
                    NotifyRadarVisualsChanged();
                }
            }
        }
        rightListing.End();

        listing.GapLine();

        // 3. Mountain Radar Shadows
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

        // 4. Background Dimming
        listing.Gap(4f);
        listing.Label($"Surveillance Background Dimming: {surveillanceDimming:P0}");
        float newDimming = listing.Slider(surveillanceDimming, 0.0f, 1.0f);
        if (Mathf.Abs(newDimming - surveillanceDimming) > 0.005f)
        {
            surveillanceDimming = newDimming;
            NotifyRadarVisualsChanged();
        }
    }

    // =========================================================================
    // Tactical Radar Scope Live Preview
    // =========================================================================

    private void DrawRadarScopePreview(Rect rect)
    {
        // 1. CRT Scope Background & Tactical Bezel
        Widgets.DrawBoxSolid(rect, new Color(0.03f, 0.05f, 0.08f, 0.95f));

        if (surveillanceDimming > 0.01f)
        {
            Widgets.DrawBoxSolid(rect, new Color(0f, 0f, 0f, surveillanceDimming * 0.45f));
        }

        // Tactical Corner Accents
        Color cornerCol = new Color(0f, 0.85f, 1f, 0.75f);
        float cornerLen = 8f;
        Widgets.DrawLine(new Vector2(rect.x, rect.y), new Vector2(rect.x + cornerLen, rect.y), cornerCol, 1.5f);
        Widgets.DrawLine(new Vector2(rect.x, rect.y), new Vector2(rect.x, rect.y + cornerLen), cornerCol, 1.5f);
        Widgets.DrawLine(new Vector2(rect.xMax, rect.y), new Vector2(rect.xMax - cornerLen, rect.y), cornerCol, 1.5f);
        Widgets.DrawLine(new Vector2(rect.xMax, rect.y), new Vector2(rect.xMax, rect.y + cornerLen), cornerCol, 1.5f);
        Widgets.DrawLine(new Vector2(rect.x, rect.yMax), new Vector2(rect.x + cornerLen, rect.yMax), cornerCol, 1.5f);
        Widgets.DrawLine(new Vector2(rect.x, rect.yMax), new Vector2(rect.x, rect.yMax - cornerLen), cornerCol, 1.5f);
        Widgets.DrawLine(new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMax - cornerLen, rect.yMax), cornerCol, 1.5f);
        Widgets.DrawLine(new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMax, rect.y - cornerLen), cornerCol, 1.5f);

        Widgets.DrawBox(rect, 1, BaseContent.GreyTex);

        Vector2 center = rect.center;
        float maxR = (rect.width / 2f) - 10f;
        float outerR = maxR * 0.90f;
        float idR = outerR * 0.58f;

        // 2. Faint CRT Range Rings & Cardinal Crosshairs
        Color crtGridCol = new Color(0.12f, 0.28f, 0.35f, 0.35f);
        DrawCircleOutline(center, maxR * 0.35f, crtGridCol, 1f, 24);
        DrawCircleOutline(center, maxR * 0.70f, crtGridCol, 1f, 32);
        DrawCircleOutline(center, maxR, crtGridCol, 1f, 36);

        Color crossCol = new Color(0.15f, 0.35f, 0.42f, 0.4f);
        Widgets.DrawLine(new Vector2(rect.x + 6f, center.y), new Vector2(center.x - 8f, center.y), crossCol, 1f);
        Widgets.DrawLine(new Vector2(center.x + 8f, center.y), new Vector2(rect.xMax - 6f, center.y), crossCol, 1f);
        Widgets.DrawLine(new Vector2(center.x, rect.y + 6f), new Vector2(center.x, center.y - 8f), crossCol, 1f);
        Widgets.DrawLine(new Vector2(center.x, center.y + 8f), new Vector2(center.x, rect.yMax - 6f), crossCol, 1f);

        // 3. Honeycomb Grid
        if (radarOutlineStyle == RadarOutlineStyle.BoundaryAndGrid)
        {
            Color hexCol = EffectiveRadarColor;
            hexCol.a *= Mathf.Clamp01(honeycombGridOpacity * 0.5f);
            DrawHoneycombPattern(center, outerR, hexCol);
        }

        // 4. Mountain Shadow Wedge
        if (radarOutlineStyle != RadarOutlineStyle.None && radarTerrainShadowStyle != RadarTerrainShadowStyle.None)
        {
            DrawMountainShadowWedge(center, idR * 0.85f, outerR, radarTerrainShadowStyle);
        }

        // 5. Inner Identification Horizon
        if (showIdentificationHorizon && radarOutlineStyle != RadarOutlineStyle.None)
        {
            Color idCol = EffectiveIdentificationColor;
            idCol.a *= Mathf.Clamp01(identificationRingOpacity);
            DrawCircleOutline(center, idR, idCol, 2f, 32);
        }

        // 6. Outer Detection Perimeter
        if (radarOutlineStyle != RadarOutlineStyle.None)
        {
            Color outerCol = EffectiveRadarColor;
            outerCol.a *= Mathf.Clamp01(radarOutlineOpacity);
            DrawCircleOutline(center, outerR, outerCol, 2f, 36);
        }

        // 7. Telemetry Blips
        Widgets.DrawBoxSolid(new Rect(center.x - 3f, center.y - 3f, 6f, 6f), new Color(0f, 0.95f, 1f, 0.95f));

        Vector2 idContactPos = center + new Vector2(-idR * 0.55f, idR * 0.40f);
        Widgets.DrawBoxSolid(new Rect(idContactPos.x - 2.5f, idContactPos.y - 2.5f, 5f, 5f), new Color(0.25f, 1f, 0.5f, 0.9f));

        Vector2 unkContactPos = center + new Vector2(outerR * 0.70f, -outerR * 0.45f);
        Widgets.DrawBoxSolid(new Rect(unkContactPos.x - 2.5f, unkContactPos.y - 2.5f, 5f, 5f), new Color(1f, 0.8f, 0.2f, 0.95f));

        // 8. Overlays
        Text.Font = GameFont.Tiny;
        GUI.color = new Color(0f, 0.85f, 1f, 0.85f);
        Widgets.Label(new Rect(rect.x + 5f, rect.y + 3f, 95f, 16f), "HUD SCOPE");

        GUI.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Widgets.Label(new Rect(unkContactPos.x + 4f, unkContactPos.y - 6f, 12f, 14f), "?");

        GUI.color = new Color(0.4f, 1f, 0.6f, 0.85f);
        Widgets.Label(new Rect(idContactPos.x + 4f, idContactPos.y - 6f, 26f, 14f), "TR-1");

        GUI.color = new Color(0.55f, 0.7f, 0.8f, 0.65f);
        Text.Anchor = TextAnchor.LowerRight;
        string modeTag = radarOutlineStyle switch
        {
            RadarOutlineStyle.BoundaryAndGrid => "GRID",
            RadarOutlineStyle.BoundaryOnly => "PERIM",
            _ => "CLEAN"
        };
        Widgets.Label(new Rect(rect.xMax - 54f, rect.yMax - 18f, 50f, 16f), modeTag);
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
    }

    private static void DrawCircleOutline(Vector2 center, float radius, Color color, float width, int segments)
    {
        if (radius <= 0f || color.a <= 0.005f) return;

        float step = Mathf.PI * 2f / segments;
        Vector2 prev = center + new Vector2(Mathf.Cos(0f), Mathf.Sin(0f)) * radius;

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * step;
            Vector2 curr = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Widgets.DrawLine(prev, curr, color, width);
            prev = curr;
        }
    }

    private static void DrawHoneycombPattern(Vector2 center, float maxRadius, Color color)
    {
        if (color.a <= 0.005f) return;

        float r = 13f;
        float w = Mathf.Sqrt(3f) * r;
        float h = 1.5f * r;

        for (int row = -3; row <= 3; row++)
        {
            for (int col = -3; col <= 3; col++)
            {
                float x = center.x + col * w + (((row & 1) != 0) ? (w * 0.5f) : 0f);
                float y = center.y + row * h;
                Vector2 hexCenter = new Vector2(x, y);

                if (Vector2.Distance(center, hexCenter) > maxRadius - 5f)
                    continue;

                Vector2 p0 = hexCenter + new Vector2(0f, -r);
                Vector2 p1 = hexCenter + new Vector2(w * 0.5f, -0.5f * r);
                Vector2 p2 = hexCenter + new Vector2(w * 0.5f, 0.5f * r);
                Vector2 p3 = hexCenter + new Vector2(0f, r);
                Vector2 p4 = hexCenter + new Vector2(-w * 0.5f, 0.5f * r);
                Vector2 p5 = hexCenter + new Vector2(-w * 0.5f, -0.5f * r);

                Widgets.DrawLine(p0, p1, color, 1f);
                Widgets.DrawLine(p1, p2, color, 1f);
                Widgets.DrawLine(p2, p3, color, 1f);
                Widgets.DrawLine(p3, p4, color, 1f);
                Widgets.DrawLine(p4, p5, color, 1f);
                Widgets.DrawLine(p5, p0, color, 1f);
            }
        }
    }

    private static void DrawMountainShadowWedge(Vector2 center, float innerR, float outerR, RadarTerrainShadowStyle style)
    {
        float startAngle = 0.52f;
        float endAngle = 1.31f;

        if (style == RadarTerrainShadowStyle.Both || style == RadarTerrainShadowStyle.CellHatching)
        {
            Color hatchCol = new Color(1f, 0.68f, 0.15f, 0.42f);
            int hatchLines = 7;
            for (int i = 0; i < hatchLines; i++)
            {
                float t = (i + 0.5f) / hatchLines;
                float r = Mathf.Lerp(innerR + 3f, outerR - 3f, t);
                float a1 = Mathf.Lerp(startAngle + 0.1f, endAngle - 0.2f, t);
                float a2 = a1 + 0.22f;

                Vector2 hStart = center + new Vector2(Mathf.Cos(a1), -Mathf.Sin(a1)) * (r - 4f);
                Vector2 hEnd = center + new Vector2(Mathf.Cos(a2), -Mathf.Sin(a2)) * (r + 4f);
                Widgets.DrawLine(hStart, hEnd, hatchCol, 1.5f);
            }
        }

        if (style == RadarTerrainShadowStyle.Both || style == RadarTerrainShadowStyle.DashedContours)
        {
            Color contourCol = new Color(1f, 0.78f, 0.22f, 0.85f);
            int dashSegments = 6;
            for (int i = 0; i < dashSegments; i++)
            {
                if ((i & 1) == 0)
                {
                    float a1 = Mathf.Lerp(startAngle, endAngle, (float)i / dashSegments);
                    float a2 = Mathf.Lerp(startAngle, endAngle, (float)(i + 1) / dashSegments);
                    Vector2 p1 = center + new Vector2(Mathf.Cos(a1), -Mathf.Sin(a1)) * outerR;
                    Vector2 p2 = center + new Vector2(Mathf.Cos(a2), -Mathf.Sin(a2)) * outerR;
                    Widgets.DrawLine(p1, p2, contourCol, 1.5f);
                }
            }

            Vector2 radInner1 = center + new Vector2(Mathf.Cos(startAngle), -Mathf.Sin(startAngle)) * innerR;
            Vector2 radOuter1 = center + new Vector2(Mathf.Cos(startAngle), -Mathf.Sin(startAngle)) * outerR;
            Widgets.DrawLine(radInner1, Vector2.Lerp(radInner1, radOuter1, 0.45f), contourCol, 1.5f);
            Widgets.DrawLine(Vector2.Lerp(radInner1, radOuter1, 0.65f), radOuter1, contourCol, 1.5f);

            Vector2 radInner2 = center + new Vector2(Mathf.Cos(endAngle), -Mathf.Sin(endAngle)) * innerR;
            Vector2 radOuter2 = center + new Vector2(Mathf.Cos(endAngle), -Mathf.Sin(endAngle)) * outerR;
            Widgets.DrawLine(radInner2, Vector2.Lerp(radInner2, radOuter2, 0.45f), contourCol, 1.5f);
            Widgets.DrawLine(Vector2.Lerp(radInner2, radOuter2, 0.65f), radOuter2, contourCol, 1.5f);
        }

        float midAngle = (startAngle + endAngle) * 0.5f;
        Vector2 mtnPos = center + new Vector2(Mathf.Cos(midAngle), -Mathf.Sin(midAngle)) * (innerR + 6f);
        Text.Font = GameFont.Tiny;
        GUI.color = new Color(1f, 0.75f, 0.25f, 0.9f);
        Widgets.Label(new Rect(mtnPos.x - 6f, mtnPos.y - 7f, 16f, 14f), "▲");
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
    }

    // =========================================================================
    // Tab 3: Incursions & Overland AI
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

        Text.Font = GameFont.Medium;
        listing.Label("Geoscape Tactical Controls");
        Text.Font = GameFont.Small;
        listing.GapLine();

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

        listing.Gap();

        CampaignRulesPanel.DrawNotificationsSection(listing, rules, campaign, ctx, showLockPreview: !inCampaign);

        listing.Gap();

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

    private void DrawCompactColorPicker(
        Listing_Standard listing,
        Rect rowRect,
        ref Color color,
        (string label, Color color)[] presets,
        Action? onColorChanged = null)
    {
        string hexStr = "#" + ColorUtility.ToHtmlStringRGB(color);
        string newHex = Widgets.TextField(new Rect(rowRect.x + 34f, rowRect.y, 68f, 22f), hexStr);
        if (newHex != hexStr && ColorUtility.TryParseHtmlString(newHex, out Color parsed))
        {
            color = parsed;
            onColorChanged?.Invoke();
            NotifyRadarVisualsChanged();
        }

        float chipStart = rowRect.x + 106f;
        float chipSize = 20f;
        float chipSpacing = 4f;

        for (int i = 0; i < presets.Length; i++)
        {
            Rect chipRect = new Rect(chipStart + i * (chipSize + chipSpacing), rowRect.y + 1f, chipSize, chipSize);
            Widgets.DrawBoxSolid(chipRect, presets[i].color);
            Widgets.DrawHighlightIfMouseover(chipRect);
            if (Widgets.ButtonInvisible(chipRect))
            {
                color = presets[i].color;
                onColorChanged?.Invoke();
                NotifyRadarVisualsChanged();
            }
            TooltipHandler.TipRegion(chipRect, presets[i].label);
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
