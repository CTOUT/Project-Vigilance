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
    private static readonly float[] tabHeights = { 460f, 520f, 320f, 480f, 420f };
    private static float radarColumnsHeight = 170f;
    private static float shadowGridHeight = 56f;

    private static readonly Color LatchedTabFill = new(0.12f, 0.22f, 0.32f, 0.85f);
    private static readonly Color LatchedTabAccent = new(0f, 0.85f, 1f);
    private static readonly Color LatchedTabText = new(0.4f, 0.95f, 1f);
    private static readonly Color SectionHeaderColor = new(0.7f, 0.9f, 1f);
    private static readonly Color SubNoteColor = new(0.7f, 0.85f, 0.95f);
    private static readonly Color DisabledControlColor = new(1f, 1f, 1f, 0.38f);
    private static readonly Color DisabledHeaderColor = new(0.45f, 0.58f, 0.65f, 0.50f);

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
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            colonyScoutRadius = Mathf.Round(colonyScoutRadius);
            caravanScoutRadius = Mathf.Round(caravanScoutRadius);
        }
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

            if (isSelected)
            {
                DrawLatchedTab(tabBtnRect, tabLabels[i]);
            }
            else if (Widgets.ButtonText(tabBtnRect, tabLabels[i]))
            {
                currentTab = (GeoscapeSettingsTab)i;
                SoundDefOf.Click?.PlayOneShotOnCamera();
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
        Listing_Standard listing = new() { maxOneColumn = true };
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
        colonyScoutRadius = Mathf.Round(colonyScoutRadius);
        caravanScoutRadius = Mathf.Round(caravanScoutRadius);
        transientDefaultRuleSet ??= new GeoscapeRuleSet();
        transientDefaultRuleSet.CopyFromDefaults(this);
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
        CampaignRulesPanel.DrawLookoutScoutingSection(listing, rules, campaign, ctx, showLockPreview: !inCampaign);
        CampaignRulesPanel.DrawStartingSiteSelectionSection(listing, rules, campaign, ctx, showLockPreview: !inCampaign);

        if (!inCampaign)
        {
            rules.WriteToDefaults(this);
        }
    }

    // =========================================================================
    // Tab 2: Radar Overlay & Tactical Visuals
    // =========================================================================

    private static void DrawLatchedTab(Rect rect, string label)
    {
        Widgets.DrawBoxSolid(rect, LatchedTabFill);
        Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 3f, rect.width, 3f), LatchedTabAccent);
        TextAnchor prevAnchor = Text.Anchor;
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = LatchedTabText;
        Widgets.Label(rect, label);
        GUI.color = Color.white;
        Text.Anchor = prevAnchor;
    }

    private void DrawRadarTab(Listing_Standard listing)
    {
        Text.Font = GameFont.Medium;
        listing.Label("Tactical Radar Overlay & Range Outlines");
        Text.Font = GameFont.Small;
        listing.Gap(4f);

        // 1. Header: Live HUD Scope Preview, Overlay Mode & Background Dimming
        float headerHeight = 182f;
        Rect headerRect = listing.GetRect(headerHeight);

        float previewSize = 180f;
        Rect previewRect = new Rect(headerRect.x, headerRect.y, previewSize, previewSize);
        Rect modeColRect = new Rect(headerRect.x + previewSize + 16f, headerRect.y, headerRect.width - previewSize - 16f, headerHeight);

        RadarScopePreview.Draw(previewRect, this);

        Listing_Standard modeListing = new() { maxOneColumn = true };
        modeListing.Begin(modeColRect);
        {
            Text.Font = GameFont.Tiny;
            GUI.color = SectionHeaderColor;
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
            DrawToggleableSlider(
                modeListing,
                $"Surveillance Background Dimming: {surveillanceDimming:P0}",
                ref surveillanceDimming,
                0.0f,
                1.0f,
                radarOutlineStyle != RadarOutlineStyle.None);

            Text.Font = GameFont.Tiny;
            GUI.color = SubNoteColor;
            modeListing.Label("Hover the HUD scope for a legend. Changes below update the scope live.");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }
        modeListing.End();

        listing.GapLine();

        // 2. Two-Column Layout for Detection vs Identification (all controls remain in fixed positions; non-applicable items grey out)
        bool outlinesEnabled = radarOutlineStyle != RadarOutlineStyle.None;
        bool gridEnabled = radarOutlineStyle == RadarOutlineStyle.BoundaryAndGrid;
        bool idControlsEnabled = outlinesEnabled && showIdentificationHorizon;
        bool shadowsEnabled = outlinesEnabled && showIdentificationHorizon;

        float totalWidth = listing.ColumnWidth;
        float colWidth = (totalWidth - 16f) / 2f;

        Rect twoColRect = listing.GetRect(radarColumnsHeight);
        Rect leftRect = new Rect(twoColRect.x, twoColRect.y, colWidth, twoColRect.height);
        Rect rightRect = new Rect(twoColRect.x + colWidth + 16f, twoColRect.y, colWidth, twoColRect.height);

        // Left Column: Outer Detection Perimeter
        float bottomSliderY;
        Listing_Standard leftListing = new() { maxOneColumn = true };
        leftListing.Begin(leftRect);
        {
            Text.Font = GameFont.Tiny;
            GUI.color = outlinesEnabled ? SectionHeaderColor : DisabledHeaderColor;
            leftListing.Label("OUTER DETECTION PERIMETER");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            DrawToggleableCheckbox(
                leftListing,
                "Use Player Faction Colour",
                ref usePlayerFactionColor,
                "Automatically matches radar and boundary outlines to your player faction's primary banner colour.",
                outlinesEnabled);

            Color activeColor = EffectiveRadarColor;
            Faction? playerFaction = (Current.ProgramState == ProgramState.Playing && Current.Game != null && Find.FactionManager != null)
                ? Faction.OfPlayerSilentFail
                : null;
            string factionSyncLabel = (usePlayerFactionColor && playerFaction != null)
                ? $"Synced: {playerFaction.Name} (#{ColorUtility.ToHtmlStringRGB(activeColor)})"
                : $"Synced to Player Faction (#{ColorUtility.ToHtmlStringRGB(activeColor)})";

            DrawColorRow(
                leftListing,
                activeColor,
                usePlayerFactionColor,
                factionSyncLabel,
                ref radarCustomColor,
                RadarPerimeterPresets,
                () => usePlayerFactionColor = false,
                outlinesEnabled);

            leftListing.Gap(4f);
            DrawToggleableSlider(
                leftListing,
                $"Perimeter Intensity: {radarOutlineOpacity:P0}",
                ref radarOutlineOpacity,
                0f,
                1f,
                outlinesEnabled);

            bottomSliderY = leftListing.CurHeight;
            DrawToggleableSlider(
                leftListing,
                $"Honeycomb Grid Intensity: {honeycombGridOpacity:P0}",
                ref honeycombGridOpacity,
                0f,
                1f,
                gridEnabled);
        }
        leftListing.End();

        // Right Column: Inner Identification Horizon
        Listing_Standard rightListing = new() { maxOneColumn = true };
        rightListing.Begin(rightRect);
        {
            Text.Font = GameFont.Tiny;
            GUI.color = outlinesEnabled ? SectionHeaderColor : DisabledHeaderColor;
            rightListing.Label("INNER IDENTIFICATION HORIZON");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            DrawToggleableCheckbox(
                rightListing,
                "Show Identification Horizon",
                ref showIdentificationHorizon,
                "Renders an inner concentric tactical horizon ring marking the transition where contact tracks are classified into faction and force intelligence.",
                outlinesEnabled);

            DrawToggleableCheckbox(
                rightListing,
                "Use Custom Identification Colour",
                ref useCustomIdentificationColor,
                "Select an explicit custom colour rather than auto-deriving a complementary hue from the detection perimeter.",
                idControlsEnabled);

            Color activeIdColor = EffectiveIdentificationColor;
            string autoIdLabel = $"Auto Complementary (#{ColorUtility.ToHtmlStringRGB(activeIdColor)})";

            DrawColorRow(
                rightListing,
                activeIdColor,
                !useCustomIdentificationColor,
                autoIdLabel,
                ref radarIdentificationColor,
                IdentificationRingPresets,
                () => useCustomIdentificationColor = true,
                idControlsEnabled);

            if (rightListing.CurHeight < bottomSliderY)
            {
                rightListing.Gap(bottomSliderY - rightListing.CurHeight);
            }
            DrawToggleableSlider(
                rightListing,
                $"Identification Intensity: {identificationRingOpacity:P0}",
                ref identificationRingOpacity,
                0f,
                1f,
                idControlsEnabled);
        }
        rightListing.End();

        radarColumnsHeight = Mathf.Max(leftListing.CurHeight, rightListing.CurHeight) + 4f;

        listing.GapLine();

        // 3. Terrain Masking (Mountain Radar Shadows) in a 2x2 Grid
        Text.Font = GameFont.Tiny;
        GUI.color = shadowsEnabled ? SectionHeaderColor : DisabledHeaderColor;
        listing.Label("TERRAIN MASKING (MOUNTAIN RADAR SHADOWS)");
        GUI.color = shadowsEnabled ? SubNoteColor : DisabledControlColor;
        listing.Label("Mountains inside the identification horizon hide contacts in their valleys; tracks there stay unidentified (?) unless an array linked to a SIGINT console penetrates the terrain. Choose how shadowed mountain hexes are marked on the globe:");
        GUI.color = Color.white;
        Text.Font = GameFont.Small;

        Rect shadowGridRect = listing.GetRect(shadowGridHeight);
        Rect shadowLeftRect = new Rect(shadowGridRect.x, shadowGridRect.y, colWidth, shadowGridRect.height);
        Rect shadowRightRect = new Rect(shadowGridRect.x + colWidth + 16f, shadowGridRect.y, colWidth, shadowGridRect.height);

        Listing_Standard shadowLeft = new() { maxOneColumn = true };
        shadowLeft.Begin(shadowLeftRect);
        {
            if (DrawToggleableRadio(shadowLeft, "Both (Dashed Contours & Cell Hatching)", radarTerrainShadowStyle == RadarTerrainShadowStyle.Both, "Marks shadowed mountain hexes inside identification range with both a dashed amber outer contour and diagonal cell hatching.", shadowsEnabled))
            {
                if (radarTerrainShadowStyle != RadarTerrainShadowStyle.Both)
                {
                    radarTerrainShadowStyle = RadarTerrainShadowStyle.Both;
                    NotifyRadarVisualsChanged();
                }
            }

            if (DrawToggleableRadio(shadowLeft, "Cell Hatching Only", radarTerrainShadowStyle == RadarTerrainShadowStyle.CellHatching, "Draws diagonal scanline hatching across shadowed mountain hexes inside identification range.", shadowsEnabled))
            {
                if (radarTerrainShadowStyle != RadarTerrainShadowStyle.CellHatching)
                {
                    radarTerrainShadowStyle = RadarTerrainShadowStyle.CellHatching;
                    NotifyRadarVisualsChanged();
                }
            }
        }
        shadowLeft.End();

        Listing_Standard shadowRight = new() { maxOneColumn = true };
        shadowRight.Begin(shadowRightRect);
        {
            if (DrawToggleableRadio(shadowRight, "Dashed Contours Only", radarTerrainShadowStyle == RadarTerrainShadowStyle.DashedContours, "Draws a dashed amber outline around shadowed mountain clusters inside identification range.", shadowsEnabled))
            {
                if (radarTerrainShadowStyle != RadarTerrainShadowStyle.DashedContours)
                {
                    radarTerrainShadowStyle = RadarTerrainShadowStyle.DashedContours;
                    NotifyRadarVisualsChanged();
                }
            }

            if (DrawToggleableRadio(shadowRight, "None (Undecorated)", radarTerrainShadowStyle == RadarTerrainShadowStyle.None, "Leaves shadowed mountain hexes undecorated on the globe (terrain masking mechanics still apply).", shadowsEnabled))
            {
                if (radarTerrainShadowStyle != RadarTerrainShadowStyle.None)
                {
                    radarTerrainShadowStyle = RadarTerrainShadowStyle.None;
                    NotifyRadarVisualsChanged();
                }
            }
        }
        shadowRight.End();

        shadowGridHeight = Mathf.Max(shadowLeft.CurHeight, shadowRight.CurHeight) + 2f;
    }

    private void DrawToggleableCheckbox(Listing_Standard listing, string label, ref bool value, string tooltip, bool enabled)
    {
        bool localVal = value;
        if (!enabled)
        {
            GUI.color = DisabledControlColor;
        }

        listing.CheckboxLabeled(label, ref localVal, tooltip);

        if (!enabled)
        {
            GUI.color = Color.white;
            return;
        }

        if (localVal != value)
        {
            value = localVal;
            NotifyRadarVisualsChanged();
        }
    }

    private void DrawToggleableSlider(Listing_Standard listing, string label, ref float value, float min, float max, bool enabled)
    {
        if (!enabled)
        {
            GUI.color = DisabledControlColor;
        }

        listing.Label(label);
        float newVal = listing.Slider(value, min, max);

        if (!enabled)
        {
            GUI.color = Color.white;
            return;
        }

        if (Mathf.Abs(newVal - value) > 0.005f)
        {
            value = newVal;
            NotifyRadarVisualsChanged();
        }
    }

    private static bool DrawToggleableRadio(Listing_Standard listing, string label, bool isSelected, string tooltip, bool enabled)
    {
        if (!enabled)
        {
            GUI.color = DisabledControlColor;
        }

        bool clicked = listing.RadioButton(label, isSelected, tabIn: 0f, tooltip: tooltip, tooltipDelay: null, disabled: !enabled);

        if (!enabled)
        {
            GUI.color = Color.white;
            return false;
        }

        return clicked;
    }

    private static void DrawToggleableIntSlider(Listing_Standard listing, string label, ref int value, int min, int max, string tooltip, bool enabled)
    {
        if (!enabled)
        {
            GUI.color = DisabledControlColor;
        }

        listing.Label(label, tooltip: tooltip);
        int newVal = Mathf.RoundToInt(listing.Slider(value, min, max));

        if (!enabled)
        {
            GUI.color = Color.white;
            return;
        }

        if (newVal != value)
        {
            value = newVal;
        }
    }

    private void DrawColorRow(
        Listing_Standard listing,
        Color activeColor,
        bool isSyncedOrAuto,
        string syncedLabel,
        ref Color customColor,
        (string label, Color color)[] presets,
        Action onCustomSelected,
        bool enabled)
    {
        Rect rowRect = listing.GetRect(24f);
        Color swatchColor = activeColor;
        if (!enabled)
        {
            swatchColor.a *= 0.35f;
        }
        Widgets.DrawBoxSolid(new Rect(rowRect.x, rowRect.y, 28f, 22f), swatchColor);

        if (isSyncedOrAuto)
        {
            if (!enabled)
            {
                GUI.color = DisabledControlColor;
            }
            Widgets.Label(new Rect(rowRect.x + 34f, rowRect.y + 1f, rowRect.width - 34f, 22f), syncedLabel);
            if (!enabled)
            {
                GUI.color = Color.white;
            }
        }
        else
        {
            DrawCompactColorPicker(listing, rowRect, ref customColor, presets, onCustomSelected, enabled);
        }
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

        listing.Gap(4f);
        DrawToggleableIntSlider(
            listing,
            $"Max Contextual Caravans Displayed: {maxContextualCaravansShown}",
            ref maxContextualCaravansShown,
            1,
            8,
            "Limits the number of nearest player caravans directly displayed on the right-click menu. Additional caravans can be accessed via a drilldown sub-menu.",
            enableIntelligentRightClick);

        DrawToggleableIntSlider(
            listing,
            $"Max Contextual Colonies Displayed: {maxContextualColoniesShown}",
            ref maxContextualColoniesShown,
            1,
            5,
            "Limits the number of nearest player colonies directly displayed on the right-click menu. Additional colonies can be accessed via a drilldown sub-menu.",
            enableIntelligentRightClick);

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
        Action? onColorChanged = null,
        bool enabled = true)
    {
        string hexStr = "#" + ColorUtility.ToHtmlStringRGB(color);
        Rect hexRect = new Rect(rowRect.x + 34f, rowRect.y, 68f, 22f);

        if (enabled)
        {
            string newHex = Widgets.TextField(hexRect, hexStr);
            if (newHex != hexStr && ColorUtility.TryParseHtmlString(newHex, out Color parsed))
            {
                color = parsed;
                onColorChanged?.Invoke();
                NotifyRadarVisualsChanged();
            }
        }
        else
        {
            GUI.color = DisabledControlColor;
            Widgets.Label(new Rect(hexRect.x, hexRect.y + 1f, hexRect.width, hexRect.height), hexStr);
            GUI.color = Color.white;
        }

        float chipStart = rowRect.x + 106f;
        float chipSize = 20f;
        float chipSpacing = 4f;

        for (int i = 0; i < presets.Length; i++)
        {
            Rect chipRect = new Rect(chipStart + i * (chipSize + chipSpacing), rowRect.y + 1f, chipSize, chipSize);
            Color chipCol = presets[i].color;
            if (!enabled)
            {
                chipCol.a *= 0.35f;
            }
            Widgets.DrawBoxSolid(chipRect, chipCol);

            if (enabled)
            {
                Widgets.DrawHighlightIfMouseover(chipRect);
                if (Widgets.ButtonInvisible(chipRect))
                {
                    color = presets[i].color;
                    onColorChanged?.Invoke();
                    NotifyRadarVisualsChanged();
                }
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
