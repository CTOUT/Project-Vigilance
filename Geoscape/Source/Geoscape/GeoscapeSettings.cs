using System;
using System.Collections.Generic;
using System.Linq;
using ProjectVigilance.Geoscape.Rules;
using ProjectVigilance.Geoscape.Sensory;
using ProjectVigilance.Geoscape.Settings;
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
    public SensoryTheme sensoryTheme = SensoryTheme.TacticalCyan;
    public bool useCustomPaletteOverrides = false;
    public Color radarCustomColor = new(0f, 0.90f, 1.0f);
    public float radarOutlineOpacity = 0.50f;
    public float identificationRingOpacity = 0.65f;
    public float honeycombGridOpacity = 0.40f;
    public bool useCustomIdentificationColor = false;
    public Color radarIdentificationColor = new(1.0f, 0.82f, 0.15f);
    public Color scoutCustomColor = new(1.0f, 0.75f, 0.20f);
    public Color sonarCustomColor = new(0.12f, 0.45f, 0.85f);
    public float surveillanceDimming = 0.25f;
    public float colonyScoutRadius = 3.0f;
    public float colonyIdentificationRadius = 1.0f;
    public float caravanScoutRadius = 2.0f;
    public float caravanIdentificationRadius = 1.0f;
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

    public bool enableThermalIntegrityAudit = true;
    public bool optimisticCaravanDetours = true;
    public bool enableInterFactionCollisions = true;
    public ShroudEffectLevel shroudEffects = ShroudEffectLevel.Subtle;
    public bool reduceShroudMotion = false;
    public bool optimiseUnexploredTerrain = true;

    // =========================================================================
    // Transient UI State (Tabs, Scrolling & Dynamic Sizing)
    // =========================================================================

    private sealed class BuiltinTabProvider : ISettingsTabProvider
    {
        public string TabKey { get; }
        public string TabLabelKey { get; }
        public string FallbackTabLabel { get; }
        public int SortOrder { get; }
        private readonly Action<Listing_Standard, Rect> drawAction;

        public BuiltinTabProvider(string tabKey, string tabLabelKey, string fallbackLabel, int sortOrder, Action<Listing_Standard, Rect> drawAction)
        {
            TabKey = tabKey;
            TabLabelKey = tabLabelKey;
            FallbackTabLabel = fallbackLabel;
            SortOrder = sortOrder;
            this.drawAction = drawAction;
        }

        public void DoTabContents(Listing_Standard listing, Rect inRect) => drawAction(listing, inRect);
    }

    private static string currentTabKey = "Sensory";
    private static readonly Dictionary<string, Vector2> dynamicScrollPositions = new();
    private static readonly Dictionary<string, float> dynamicTabHeights = new();
    private static bool builtinTabsRegistered;

    public static GeoscapeSettingsTab CurrentTab
    {
        get => currentTabKey switch
        {
            "Sensory" => GeoscapeSettingsTab.Sensory,
            "Radar" => GeoscapeSettingsTab.Radar,
            "Incursions" => GeoscapeSettingsTab.Incursions,
            "Geoscape" => GeoscapeSettingsTab.Geoscape,
            "Diagnostics" => GeoscapeSettingsTab.Diagnostics,
            _ => GeoscapeSettingsTab.Sensory
        };
        set => currentTabKey = value.ToString();
    }

    private static float sensoryColumnsHeight = 250f;
    private static float radarColumnsHeight = 180f;
    private static float shadowGridHeight = 56f;
    private static float radarBottomRowHeight = 148f;
    private static float geoscapeTopHeight = 195f;
    private static float diagnosticsBottomHeight = 185f;

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

    public Color EffectiveScoutColor => scoutCustomColor;
    public Color EffectiveSonarColor => sonarCustomColor;

    public void ApplySensoryTheme(SensoryTheme theme)
    {
        sensoryTheme = theme;
        var colors = SensoryThemeUtility.GetThemeColors(theme);
        radarCustomColor = colors.radar;
        radarIdentificationColor = colors.identification;
        scoutCustomColor = colors.scout;
        sonarCustomColor = colors.sonar;
        useCustomIdentificationColor = true;
        NotifyRadarVisualsChanged();
        WorldDrawLayer_ShroudShell.NotifyAtmosphereSettingsChanged();
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
        Scribe_Values.Look(ref sensoryTheme, "sensoryTheme", SensoryTheme.TacticalCyan);
        Scribe_Values.Look(ref useCustomPaletteOverrides, "useCustomPaletteOverrides", false);
        Scribe_Values.Look(ref radarCustomColor, "radarCustomColor", new Color(0f, 0.90f, 1.0f));
        Scribe_Values.Look(ref radarOutlineOpacity, "radarOutlineOpacity", 0.50f);
        Scribe_Values.Look(ref identificationRingOpacity, "identificationRingOpacity", 0.65f);
        Scribe_Values.Look(ref honeycombGridOpacity, "honeycombGridOpacity", 0.40f);
        Scribe_Values.Look(ref useCustomIdentificationColor, "useCustomIdentificationColor", false);
        Scribe_Values.Look(ref radarIdentificationColor, "radarIdentificationColor", new Color(1.0f, 0.82f, 0.15f));
        Scribe_Values.Look(ref scoutCustomColor, "scoutCustomColor", new Color(1.0f, 0.75f, 0.20f));
        Scribe_Values.Look(ref sonarCustomColor, "sonarCustomColor", new Color(0.12f, 0.45f, 0.85f));
        Scribe_Values.Look(ref showIdentificationHorizon, "showIdentificationHorizon", true);
        Scribe_Values.Look(ref radarTerrainShadowStyle, "radarTerrainShadowStyle", RadarTerrainShadowStyle.Both);
        Scribe_Values.Look(ref surveillanceDimming, "surveillanceDimming", 0.25f);
        Scribe_Values.Look(ref colonyScoutRadius, "colonyScoutRadius", 3.0f);
        Scribe_Values.Look(ref colonyIdentificationRadius, "colonyIdentificationRadius", 1.0f);
        Scribe_Values.Look(ref caravanScoutRadius, "caravanScoutRadius", 2.0f);
        Scribe_Values.Look(ref caravanIdentificationRadius, "caravanIdentificationRadius", 1.0f);
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            ClampLookoutRadii();
        }
        Scribe_Values.Look(ref enableEarlyWarningLetters, "enableEarlyWarningLetters", true);
        Scribe_Values.Look(ref enableNighttimeSensoryReduction, "enableNighttimeSensoryReduction", true);
        Scribe_Values.Look(ref incursionsRestAtNight, "incursionsRestAtNight", true);
        Scribe_Values.Look(ref startingSiteSelection, "startingSiteSelection", StartingSiteSelectionMode.AutomatedLandfall);
        Scribe_Values.Look(ref enableOverlappingEntitySelectionMenu, "enableOverlappingEntitySelectionMenu", true);
        Scribe_Values.Look(ref enableIntelligentRightClick, "enableIntelligentRightClick", true);
        Scribe_Values.Look(ref maxContextualCaravansShown, "maxContextualCaravansShown", 3);
        Scribe_Values.Look(ref maxContextualColoniesShown, "maxContextualColoniesShown", 2);

        Scribe_Values.Look(ref enableThermalIntegrityAudit, "enableThermalIntegrityAudit", true);
        Scribe_Values.Look(ref optimisticCaravanDetours, "optimisticCaravanDetours", true);
        Scribe_Values.Look(ref enableInterFactionCollisions, "enableInterFactionCollisions", true);
        Scribe_Values.Look(ref shroudEffects, "shroudEffects", ShroudEffectLevel.Subtle);
        Scribe_Values.Look(ref reduceShroudMotion, "reduceShroudMotion", false);
        Scribe_Values.Look(ref optimiseUnexploredTerrain, "optimiseUnexploredTerrain", true);
    }

    // =========================================================================
    // Tabbed Modular Settings Window
    // =========================================================================

    public void EnsureBuiltinTabsRegistered()
    {
        if (builtinTabsRegistered) return;
        builtinTabsRegistered = true;

        VigilanceSettingsRegistry.RegisterTab(new BuiltinTabProvider("Sensory", "PV_Settings_Tab_Sensory", "Sensory", 10, (l, r) => DrawSensoryTab(l)));
        VigilanceSettingsRegistry.RegisterTab(new BuiltinTabProvider("Radar", "PV_Settings_Tab_Radar", "Radar", 20, (l, r) => DrawRadarTab(l)));
        VigilanceSettingsRegistry.RegisterTab(new BuiltinTabProvider("Incursions", "PV_Settings_Tab_Incursions", "Incursions", 30, (l, r) => DrawIncursionsTab(l)));
        VigilanceSettingsRegistry.RegisterTab(new BuiltinTabProvider("Geoscape", "PV_Settings_Tab_Geoscape", "Geoscape", 40, (l, r) => DrawGeoscapeTab(l)));
        VigilanceSettingsRegistry.RegisterTab(new BuiltinTabProvider("Diagnostics", "PV_Settings_Tab_Diagnostics", "Diagnostics", 50, (l, r) => DrawAboutDiagnosticsTab(l)));
    }

    public void DoWindowContents(Rect inRect)
    {
        EnsureBuiltinTabsRegistered();

        if (VigilanceSettingsRegistry.PendingTabSelection != null)
        {
            currentTabKey = VigilanceSettingsRegistry.PendingTabSelection;
            VigilanceSettingsRegistry.ClearPendingSelection();
        }

        var tabs = VigilanceSettingsRegistry.RegisteredTabs;
        if (tabs.Count == 0) return;

        if (!tabs.Any(t => t.TabKey == currentTabKey))
        {
            currentTabKey = tabs[0].TabKey;
        }

        // 1. Draw Tab Bar Header
        Rect tabRowRect = new Rect(inRect.x, inRect.y, inRect.width, 34f);
        int tabCount = tabs.Count;
        float spacing = 6f;
        float tabWidth = (tabRowRect.width - (tabCount - 1) * spacing) / tabCount;

        for (int i = 0; i < tabCount; i++)
        {
            var tab = tabs[i];
            Rect tabBtnRect = new Rect(tabRowRect.x + i * (tabWidth + spacing), tabRowRect.y, tabWidth, 30f);
            bool isSelected = currentTabKey == tab.TabKey;
            string label = tab.TabLabelKey.CanTranslate() ? tab.TabLabelKey.Translate().ToString() : tab.FallbackTabLabel;

            if (isSelected)
            {
                DrawLatchedTab(tabBtnRect, label);
            }
            else if (Widgets.ButtonText(tabBtnRect, label))
            {
                currentTabKey = tab.TabKey;
                SoundDefOf.Click?.PlayOneShotOnCamera();
            }
        }

        Widgets.DrawLineHorizontal(inRect.x, inRect.y + 36f, inRect.width);

        // 2. Scrollable Content View
        float contentTop = inRect.y + 40f;
        float contentHeight = inRect.height - 40f;
        Rect contentRect = new Rect(inRect.x, contentTop, inRect.width, contentHeight);

        if (!dynamicScrollPositions.ContainsKey(currentTabKey))
        {
            dynamicScrollPositions[currentTabKey] = Vector2.zero;
        }
        if (!dynamicTabHeights.ContainsKey(currentTabKey))
        {
            dynamicTabHeights[currentTabKey] = contentHeight;
        }

        float requiredHeight = Mathf.Max(dynamicTabHeights[currentTabKey], contentHeight);
        bool needsScrollbar = requiredHeight > contentHeight + 1f;
        Rect viewRect = new Rect(0f, 0f, contentRect.width - (needsScrollbar ? 16f : 0f), requiredHeight);

        Vector2 scroll = dynamicScrollPositions[currentTabKey];
        Widgets.BeginScrollView(contentRect, ref scroll, viewRect);
        dynamicScrollPositions[currentTabKey] = scroll;

        Listing_Standard listing = new() { maxOneColumn = true };
        listing.Begin(viewRect);

        var activeTab = tabs.FirstOrDefault(t => t.TabKey == currentTabKey) ?? tabs[0];
        activeTab.DoTabContents(listing, viewRect);

        dynamicTabHeights[currentTabKey] = Mathf.Max(listing.CurHeight + 8f, contentHeight);
        listing.End();
        Widgets.EndScrollView();
    }

    // =========================================================================
    // Tab 1: Sensory & Visibility Rules
    // =========================================================================

    private GeoscapeRuleSet? transientDefaultRuleSet;

    private GeoscapeRuleSet GetOrCreateTransientRuleSet()
    {
        ClampLookoutRadii();
        transientDefaultRuleSet ??= new GeoscapeRuleSet();
        transientDefaultRuleSet.CopyFromDefaults(this);
        return transientDefaultRuleSet;
    }

    private void ClampLookoutRadii()
    {
        colonyScoutRadius = Mathf.Max(1.0f, Mathf.Round(colonyScoutRadius));
        caravanScoutRadius = Mathf.Max(1.0f, Mathf.Round(caravanScoutRadius));
        colonyIdentificationRadius = GeoscapeRuleSet.ClampIdentificationRadius(colonyIdentificationRadius, colonyScoutRadius);
        caravanIdentificationRadius = GeoscapeRuleSet.ClampIdentificationRadius(caravanIdentificationRadius, caravanScoutRadius);
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
        // 1. Header: Live HUD Scope Preview, Overlay Mode & Background Dimming
        float headerHeight = 158f;
        Rect headerRect = listing.GetRect(headerHeight);

        float previewSize = 156f;
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

            modeListing.Gap(4f);
            DrawToggleableSlider(
                modeListing,
                $"Surveillance Background Dimming: {surveillanceDimming:P0}",
                ref surveillanceDimming,
                0.0f,
                1.0f,
                radarOutlineStyle != RadarOutlineStyle.None);
        }
        modeListing.End();

        listing.GapLine(6f);

        bool outlinesEnabled = radarOutlineStyle != RadarOutlineStyle.None;
        bool gridEnabled = radarOutlineStyle == RadarOutlineStyle.BoundaryAndGrid;
        bool idControlsEnabled = outlinesEnabled && showIdentificationHorizon;
        bool shadowsEnabled = outlinesEnabled && showIdentificationHorizon;
        bool customColorsEnabled = outlinesEnabled && useCustomPaletteOverrides;
        bool customIdColorsEnabled = idControlsEnabled && useCustomPaletteOverrides;

        // 2. Curated Sensory Theme & Custom Overrides Toggle (single compact row)
        Text.Font = GameFont.Tiny;
        GUI.color = outlinesEnabled ? SectionHeaderColor : DisabledHeaderColor;
        listing.Label("SENSORY PALETTE THEME & ACCESSIBILITY (WCAG AA)");
        GUI.color = Color.white;
        Text.Font = GameFont.Small;

        float totalWidth = listing.ColumnWidth;
        float colWidth = (totalWidth - 16f) / 2f;

        Rect themeRow = listing.GetRect(28f);
        Rect themeBtn = new Rect(themeRow.x, themeRow.y, colWidth, 26f);
        Rect overrideRect = new Rect(themeRow.x + colWidth + 16f, themeRow.y, colWidth, 26f);

        string themeKey = $"PV_Settings_Theme_{sensoryTheme}";
        string themeLabel = themeKey.CanTranslate() ? themeKey.Translate().ToString() : sensoryTheme.ToString();
        if (Widgets.ButtonText(themeBtn, $"Theme: {themeLabel}"))
        {
            List<FloatMenuOption> themeOptions = new();
            foreach (SensoryTheme st in Enum.GetValues(typeof(SensoryTheme)))
            {
                string key = $"PV_Settings_Theme_{st}";
                string lbl = key.CanTranslate() ? key.Translate().ToString() : st.ToString();
                themeOptions.Add(new FloatMenuOption(lbl, () =>
                {
                    ApplySensoryTheme(st);
                }));
            }
            Find.WindowStack.Add(new FloatMenu(themeOptions));
        }

        bool customOverride = useCustomPaletteOverrides;
        string overrideLabel = "PV_Settings_UseCustomPaletteOverrides".CanTranslate() ? "PV_Settings_UseCustomPaletteOverrides".Translate().ToString() : "Use Custom Palette Overrides";
        string overrideDesc = "PV_Settings_UseCustomPaletteOverrides_Desc".CanTranslate() ? "PV_Settings_UseCustomPaletteOverrides_Desc".Translate().ToString() : "Enable granular colour overrides for Radar, Identification, and Scout perimeters. When disabled, curated theme presets apply.";
        if (!outlinesEnabled) GUI.color = DisabledControlColor;
        Widgets.CheckboxLabeled(overrideRect, overrideLabel, ref customOverride, disabled: !outlinesEnabled);
        GUI.color = Color.white;
        TooltipHandler.TipRegion(overrideRect, overrideDesc);
        if (outlinesEnabled && customOverride != useCustomPaletteOverrides)
        {
            useCustomPaletteOverrides = customOverride;
            NotifyRadarVisualsChanged();
        }

        listing.Gap(4f);

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
                customColorsEnabled);

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
                customIdColorsEnabled);

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

        listing.GapLine(6f);

        // 3 & 4 Bottom Two-Column Row: Terrain Masking (left) & Shroud Atmosphere (right)
        Rect bottomRowRect = listing.GetRect(radarBottomRowHeight);
        Rect shadowColRect = new Rect(bottomRowRect.x, bottomRowRect.y, colWidth, bottomRowRect.height);
        Rect shroudColRect = new Rect(bottomRowRect.x + colWidth + 16f, bottomRowRect.y, colWidth, bottomRowRect.height);

        Listing_Standard shadowCol = new() { maxOneColumn = true };
        shadowCol.Begin(shadowColRect);
        {
            Text.Font = GameFont.Tiny;
            GUI.color = shadowsEnabled ? SectionHeaderColor : DisabledHeaderColor;
            shadowCol.Label("TERRAIN MASKING (MOUNTAIN RADAR SHADOWS)");
            GUI.color = shadowsEnabled ? SubNoteColor : DisabledControlColor;
            shadowCol.Label("Mountains hide unpenetrated tracks unless a SIGINT-linked array sees them.");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            float subColW = (colWidth - 8f) / 2f;
            Rect shadowGridRect = shadowCol.GetRect(shadowGridHeight);
            Rect shadowLeftRect = new Rect(shadowGridRect.x, shadowGridRect.y, subColW, shadowGridRect.height);
            Rect shadowRightRect = new Rect(shadowGridRect.x + subColW + 8f, shadowGridRect.y, subColW, shadowGridRect.height);

            Listing_Standard shadowLeft = new() { maxOneColumn = true };
            shadowLeft.Begin(shadowLeftRect);
            {
                if (DrawToggleableRadio(shadowLeft, "Both", radarTerrainShadowStyle == RadarTerrainShadowStyle.Both, "Marks shadowed mountain hexes inside identification range with both a dashed amber outer contour and diagonal cell hatching.", shadowsEnabled))
                {
                    if (radarTerrainShadowStyle != RadarTerrainShadowStyle.Both)
                    {
                        radarTerrainShadowStyle = RadarTerrainShadowStyle.Both;
                        NotifyRadarVisualsChanged();
                    }
                }

                if (DrawToggleableRadio(shadowLeft, "Hatching", radarTerrainShadowStyle == RadarTerrainShadowStyle.CellHatching, "Draws diagonal scanline hatching across shadowed mountain hexes inside identification range.", shadowsEnabled))
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
                if (DrawToggleableRadio(shadowRight, "Contours", radarTerrainShadowStyle == RadarTerrainShadowStyle.DashedContours, "Draws a dashed amber outline around shadowed mountain clusters inside identification range.", shadowsEnabled))
                {
                    if (radarTerrainShadowStyle != RadarTerrainShadowStyle.DashedContours)
                    {
                        radarTerrainShadowStyle = RadarTerrainShadowStyle.DashedContours;
                        NotifyRadarVisualsChanged();
                    }
                }

                if (DrawToggleableRadio(shadowRight, "None", radarTerrainShadowStyle == RadarTerrainShadowStyle.None, "Leaves shadowed mountain hexes undecorated on the globe (terrain masking mechanics still apply).", shadowsEnabled))
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
        shadowCol.End();

        Listing_Standard shroudCol = new() { maxOneColumn = true };
        shroudCol.Begin(shroudColRect);
        {
            Text.Font = GameFont.Tiny;
            GUI.color = SectionHeaderColor;
            shroudCol.Label("PV_Shroud_SectionHeader".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            Rect styleRow = shroudCol.GetRect(26f);
            Rect styleLabelRect = new Rect(styleRow.x, styleRow.y + 2f, styleRow.width * 0.44f, 22f);
            Rect styleBtnRect = new Rect(styleLabelRect.xMax + 6f, styleRow.y, styleRow.width * 0.56f - 6f, 24f);
            Widgets.Label(styleLabelRect, "PV_Shroud_EffectsLabel".Translate());
            string currentEffectLabel = $"PV_Shroud_Effects_{shroudEffects}".Translate();
            if (Widgets.ButtonText(styleBtnRect, currentEffectLabel))
            {
                var options = new List<FloatMenuOption>();
                foreach (ShroudEffectLevel lvl in Enum.GetValues(typeof(ShroudEffectLevel)))
                {
                    ShroudEffectLevel captured = lvl;
                    string optLabel = $"PV_Shroud_Effects_{captured}".Translate();
                    options.Add(new FloatMenuOption(optLabel, () =>
                    {
                        if (shroudEffects != captured)
                        {
                            shroudEffects = captured;
                            WorldDrawLayer_ShroudShell.NotifyAtmosphereSettingsChanged();
                        }
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            TooltipHandler.TipRegion(styleRow, "PV_Shroud_EffectsTooltip".Translate());

            shroudCol.Gap(2f);
            bool prevReduceMotion = reduceShroudMotion;
            DrawToggleableCheckbox(
                shroudCol,
                "PV_Shroud_ReduceMotionLabel".Translate(),
                ref reduceShroudMotion,
                "PV_Shroud_ReduceMotionTooltip".Translate(),
                shroudEffects != ShroudEffectLevel.Off);
            if (prevReduceMotion != reduceShroudMotion)
            {
                WorldDrawLayer_ShroudShell.NotifyAtmosphereSettingsChanged();
            }

            bool prevOptimise = optimiseUnexploredTerrain;
            shroudCol.CheckboxLabeled(
                "PV_Shroud_OptimiseTerrainLabel".Translate(),
                ref optimiseUnexploredTerrain,
                "PV_Shroud_OptimiseTerrainTooltip".Translate());
            if (prevOptimise != optimiseUnexploredTerrain)
            {
                Patches.WorldDrawLayer_Terrain_Cull.RefreshAllSubMeshes();
            }
        }
        shroudCol.End();

        radarBottomRowHeight = Mathf.Max(shadowCol.CurHeight, shroudCol.CurHeight) + 2f;
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

        listing.CheckboxLabeled(
            "PV_Settings_InterFactionCollisions".CanTranslate() ? "PV_Settings_InterFactionCollisions".Translate().ToString() : "Inter-Faction Overland Collisions",
            ref enableInterFactionCollisions,
            "PV_Settings_InterFactionCollisions_Desc".CanTranslate() ? "PV_Settings_InterFactionCollisions_Desc".Translate().ToString() : "Simulates background encounters and skirmishes when rival NPC incursions cross paths on the globe.");

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
                "PV_Settings_OptimisticDetours".CanTranslate() ? "PV_Settings_OptimisticDetours".Translate().ToString() : "Optimistic Caravan Detours",
                ref optimisticCaravanDetours,
                "PV_Settings_OptimisticDetours_Desc".CanTranslate() ? "PV_Settings_OptimisticDetours_Desc".Translate().ToString() : "Caravans plot paths assuming unmapped fog-of-war sectors are passable, repathing dynamically when impassable terrain is discovered.");

            if (!ProjectVigilance.Core.Modules.VigilanceModuleManager.IsThermalActive)
            {
                leftCol.CheckboxLabeled(
                    "PV_Settings_ThermalIntegrityAudit".CanTranslate() ? "PV_Settings_ThermalIntegrityAudit".Translate().ToString() : "Thermal Integrity Def Audit",
                    ref enableThermalIntegrityAudit,
                    "PV_Settings_ThermalIntegrityAudit_Desc".CanTranslate() ? "PV_Settings_ThermalIntegrityAudit_Desc".Translate().ToString() : "Automatically enforces non-combustible properties on metals, stone, and structural components at startup.");
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

    // =========================================================================
    // Tab 4: About Suite & Diagnostics
    // =========================================================================

    private void DrawAboutDiagnosticsTab(Listing_Standard listing)
    {
        Text.Font = GameFont.Medium;
        listing.Label("PV_Diag_ArchTitle".CanTranslate() ? "PV_Diag_ArchTitle".Translate().ToString() : "Project Vigilance — Modular Architecture");
        Text.Font = GameFont.Small;
        listing.GapLine();

        Rect descRect = listing.GetRect(46f);
        Widgets.DrawBoxSolid(descRect, new Color(0.12f, 0.15f, 0.20f, 0.6f));
        Widgets.DrawBox(descRect);
        string desc = "PV_Diag_ArchDesc".CanTranslate()
            ? "PV_Diag_ArchDesc".Translate().ToString()
            : "Decoupled RimWorld 1.7 framework suite. Modules operate independently and interconnect automatically when active.";
        Widgets.Label(descRect.ContractedBy(8f), desc);

        listing.Gap(6f);

        Text.Font = GameFont.Medium;
        listing.Label("PV_Diag_ModulesHeader".CanTranslate() ? "PV_Diag_ModulesHeader".Translate().ToString() : "Installed & Available Modules");
        Text.Font = GameFont.Small;
        listing.GapLine();

        var modules = ProjectVigilance.Core.Modules.VigilanceModuleManager.Modules;
        bool isEmbeddedFallback = ProjectVigilance.Core.Modules.VigilanceModuleManager.IsCoreEmbeddedFallback;
        float rowHeight = 24f;
        float tableHeight = (modules.Count * rowHeight) + 16f + (isEmbeddedFallback ? 24f : 0f);

        Rect modTableRect = listing.GetRect(tableHeight);
        Widgets.DrawBoxSolid(modTableRect, new Color(0.08f, 0.11f, 0.14f, 0.8f));
        Widgets.DrawBox(modTableRect);
        Rect modContentRect = modTableRect.ContractedBy(8f);

        string activeLabel = "PV_Diag_Status_Active".CanTranslate() ? "PV_Diag_Status_Active".Translate().ToString() : "Active";
        string embeddedLabel = "PV_Diag_Status_Embedded".CanTranslate() ? "PV_Diag_Status_Embedded".Translate().ToString() : "Embedded Fallback";
        string notInstalledLabel = "PV_Diag_Status_NotInstalled".CanTranslate() ? "PV_Diag_Status_NotInstalled".Translate().ToString() : "Not Installed";

        float curY = modContentRect.y;
        for (int i = 0; i < modules.Count; i++)
        {
            var mod = modules[i];
            bool isActive = ProjectVigilance.Core.Modules.VigilanceModuleManager.IsModuleActive(mod.Id);
            bool isCoreEmbedded = mod.Id == ProjectVigilance.Core.Modules.VigilanceModuleId.Core && !isActive && isEmbeddedFallback;

            Rect rowRect = new Rect(modContentRect.x, curY, modContentRect.width, rowHeight);
            if ((i & 1) == 1)
            {
                Widgets.DrawBoxSolid(rowRect, new Color(1f, 1f, 1f, 0.03f));
            }

            float leftWidth = rowRect.width * 0.68f;
            float rightWidth = rowRect.width - leftWidth;

            string versionInfo = string.Empty;
            if (isActive)
            {
                versionInfo = mod.Id == ProjectVigilance.Core.Modules.VigilanceModuleId.Geoscape
                    ? $" ({GeoscapeMod.FormatBuildDetails()})"
                    : $" (v{BuildInfo.Version})";
            }

            Widgets.Label(new Rect(rowRect.x + 4f, rowRect.y + 2f, leftWidth - 4f, rowHeight - 2f), $"• {mod.DisplayName}{versionInfo}");

            TextAnchor prevAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleRight;
            if (isActive)
            {
                GUI.color = new Color(0.35f, 0.95f, 0.45f);
                Widgets.Label(new Rect(rowRect.x + leftWidth, rowRect.y, rightWidth - 6f, rowHeight), activeLabel);
            }
            else if (isCoreEmbedded)
            {
                GUI.color = new Color(1f, 0.72f, 0.25f);
                Widgets.Label(new Rect(rowRect.x + leftWidth, rowRect.y, rightWidth - 6f, rowHeight), embeddedLabel);
            }
            else
            {
                GUI.color = new Color(0.58f, 0.65f, 0.72f);
                Widgets.Label(new Rect(rowRect.x + leftWidth, rowRect.y, rightWidth - 6f, rowHeight), notInstalledLabel);
            }
            GUI.color = Color.white;
            Text.Anchor = prevAnchor;

            string tip = "PV_Diag_ModTooltip".CanTranslate()
                ? "PV_Diag_ModTooltip".Translate(mod.Summary, mod.PackageId).ToString()
                : $"{mod.Summary}\nPackage ID: {mod.PackageId}";
            TooltipHandler.TipRegion(rowRect, tip);

            curY += rowHeight;
        }

        if (isEmbeddedFallback)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(1f, 0.80f, 0.35f);
            string note = "PV_Diag_EmbeddedCoreNote".CanTranslate()
                ? "PV_Diag_EmbeddedCoreNote".Translate().ToString()
                : "Running Geoscape with embedded Core fallback. Subscribe to standalone Core Framework to enable optional modules (Orbital, Aquatics, Thermal).";
            Widgets.Label(new Rect(modContentRect.x + 4f, curY + 2f, modContentRect.width - 8f, 22f), note);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        listing.Gap(6f);

        float leftColW = (listing.ColumnWidth - 16f) * 0.56f;
        float rightColW = (listing.ColumnWidth - 16f) - leftColW;
        Rect bottomRect = listing.GetRect(diagnosticsBottomHeight);
        Rect govRect = new Rect(bottomRect.x, bottomRect.y, leftColW, bottomRect.height);
        Rect cfgRect = new Rect(bottomRect.x + leftColW + 16f, bottomRect.y, rightColW, bottomRect.height);

        GeoscapeCampaignRules? campaign = GeoscapeCampaignRules.Active;

        Listing_Standard govCol = new() { maxOneColumn = true };
        govCol.Begin(govRect);
        {
            Text.Font = GameFont.Medium;
            govCol.Label("PV_Diag_GovernanceHeader".CanTranslate() ? "PV_Diag_GovernanceHeader".Translate().ToString() : "Active Campaign Governance");
            Text.Font = GameFont.Small;
            govCol.GapLine();

            if (campaign != null)
            {
                string statusText = $"• Active Profile: {campaign.Rules.DetectCurrentProfile().GetDisplayName()}\n" +
                                    $"• Schema Version: v{campaign.SchemaVersion}\n" +
                                    $"• Campaign Integrity: {campaign.IntegrityStatusLabel}\n" +
                                    $"• Review Window: {(campaign.ReviewWindowOpen ? "Open (Day 1 Grace Period)" : "Locked")}\n" +
                                    $"• Ledger Records: {campaign.Ledger.Count} entries";
                govCol.Label(statusText);

                if (campaign.HasExternalModifications)
                {
                    RuleChangeRecord? lastMod = campaign.Ledger.LastOrDefault(r => r.kind == RuleChangeKind.ExternalModification);
                    if (lastMod != null)
                    {
                        govCol.Gap(2f);
                        Rect warnRect = govCol.GetRect(38f);
                        Widgets.DrawBoxSolid(warnRect, new Color(0.35f, 0.08f, 0.08f, 0.6f));
                        Widgets.DrawBox(warnRect);
                        GUI.color = new Color(1f, 0.45f, 0.45f);
                        Widgets.Label(warnRect.ContractedBy(4f), $"[Save Integrity Warning] {lastMod.note}");
                        GUI.color = Color.white;
                    }
                }

                if (campaign.ReviewWindowOpen && govCol.ButtonText("Confirm & Lock In Campaign Rules Now"))
                {
                    campaign.ConfirmAndLock();
                    SoundDefOf.Click?.PlayOneShotOnCamera();
                    Messages.Message("Campaign rules locked. Mutability restrictions now active.", MessageTypeDefOf.PositiveEvent, historical: false);
                }

                if (Prefs.DevMode)
                {
                    govCol.Gap(4f);
                    bool req = UnsafeRuleOverride.Requested;
                    bool prev = req;
                    govCol.CheckboxLabeled(
                        "Allow Unsafe Rule Transitions (Session Only)",
                        ref req,
                        "Developer Mode override. Bypasses all mutability constraints, allowing downgrades and editing of immutable rules. All changes are marked [Unsafe] in the audit ledger.");
                    if (req != prev)
                    {
                        if (!req)
                        {
                            UnsafeRuleOverride.SetRequested(false);
                        }
                        else
                        {
                            string warnText = "WARNING: Enabling unsafe rule transitions allows bypassing all campaign mutability constraints (including irreversible downgrades and immutable rule overrides).\n\n" +
                                              "Modifying locked rules mid-campaign can desynchronise active incursion intelligence, corrupt fog-of-war exploration state, or compromise campaign integrity.\n\n" +
                                              "This override lasts for this game session only, but any rule changes made while active are permanently signed into the campaign audit ledger.\n\n" +
                                              "Do you wish to enable Developer Mode unsafe rule transitions?";
                            Dialog_MessageBox confirmBox = Dialog_MessageBox.CreateConfirmation(
                                warnText,
                                () => UnsafeRuleOverride.SetRequested(true),
                                destructive: true,
                                title: "Enable Unsafe Rule Transitions");
                            Find.WindowStack?.Add(confirmBox);
                        }
                    }
                }
            }
            else
            {
                GUI.color = SubNoteColor;
                string noSave = "PV_Diag_GovernanceNoSave".CanTranslate()
                    ? "PV_Diag_GovernanceNoSave".Translate().ToString()
                    : "No active campaign loaded. Defaults apply to newly started colonies.";
                govCol.Label(noSave);
                GUI.color = Color.white;
            }
        }
        govCol.End();

        Listing_Standard cfgCol = new() { maxOneColumn = true };
        cfgCol.Begin(cfgRect);
        {
            Text.Font = GameFont.Medium;
            cfgCol.Label("PV_Diag_ConfigHeader".CanTranslate() ? "PV_Diag_ConfigHeader".Translate().ToString() : "Configuration Management");
            Text.Font = GameFont.Small;
            cfgCol.GapLine();

            GUI.color = SubNoteColor;
            string cfgDesc = "PV_Diag_ConfigDesc".CanTranslate()
                ? "PV_Diag_ConfigDesc".Translate().ToString()
                : "Restore all Geoscape defaults or reset new-campaign rules without altering an active save.";
            cfgCol.Label(cfgDesc);
            GUI.color = Color.white;
            cfgCol.Gap(6f);

            string resetBtnLabel = campaign != null
                ? ("PV_Diag_ResetCampaignDefaults".CanTranslate() ? "PV_Diag_ResetCampaignDefaults".Translate().ToString() : "Reset New Campaign Defaults")
                : ("PV_Diag_ResetDefaults".CanTranslate() ? "PV_Diag_ResetDefaults".Translate().ToString() : "Reset All Geoscape Settings");

            if (cfgCol.ButtonText(resetBtnLabel))
            {
                ResetToDefaults();
                SoundDefOf.ClickReject?.PlayOneShotOnCamera();
                string msg = campaign != null
                    ? "Project Vigilance new campaign defaults reset. Active save rules remain unaffected."
                    : "Project Vigilance settings reset to default values.";
                Messages.Message(msg, MessageTypeDefOf.NeutralEvent, historical: false);
            }
        }
        cfgCol.End();

        diagnosticsBottomHeight = Mathf.Max(govCol.CurHeight, cfgCol.CurHeight) + 2f;
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
        colonyScoutRadius = 3.0f;
        colonyIdentificationRadius = 1.0f;
        caravanScoutRadius = 2.0f;
        caravanIdentificationRadius = 1.0f;
        enableEarlyWarningLetters = true;
        enableNighttimeSensoryReduction = true;
        incursionsRestAtNight = true;
        showIdentificationHorizon = true;
        radarTerrainShadowStyle = RadarTerrainShadowStyle.Both;
        enableOverlappingEntitySelectionMenu = true;
        enableIntelligentRightClick = true;
        maxContextualCaravansShown = 3;
        maxContextualColoniesShown = 2;
        shroudEffects = ShroudEffectLevel.Subtle;
        reduceShroudMotion = false;
        optimiseUnexploredTerrain = true;
        WorldDrawLayer_ShroudShell.NotifyAtmosphereSettingsChanged();
        Patches.WorldDrawLayer_Terrain_Cull.RefreshAllSubMeshes();
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
