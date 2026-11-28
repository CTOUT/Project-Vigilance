using System;
using System.Collections.Generic;
using System.Linq;
using ProjectVigilance.Core.Localization;
using ProjectVigilance.Core.UI;
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

public enum SeamlessZoomTargetMode
{
    CursorFirst,
    ScreenCenter
}

public enum SeamlessZoomIndicatorStyle
{
    Off,
    BorderFlash,
    BottomPill,
    Hybrid
}

public enum SeamlessZoomTransitionStyle
{
    Instant,
    Fade,
    Traverse
}

public partial class GeoscapeSettings : ModSettings
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
    public Color sonarCustomColor = new(0.22f, 0.57f, 1.0f);
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
    public bool showGeoscapeClouds = true;
    public float geoscapeCloudOpacity = 0.18f;
    public bool enableSeamlessCameraZoom = true;
    public int seamlessZoomOverscrollNotches = 2;
    public SeamlessZoomTargetMode seamlessZoomTargetMode = SeamlessZoomTargetMode.CursorFirst;
    public SeamlessZoomIndicatorStyle seamlessZoomIndicatorStyle = SeamlessZoomIndicatorStyle.Hybrid;
    public SeamlessZoomTransitionStyle seamlessZoomTransitionStyle = SeamlessZoomTransitionStyle.Traverse;

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
    private static readonly Color DisabledControlColor = VigilanceUiWidgets.DisabledTextColor;
    private static readonly Color DisabledHeaderColor = new(0.62f, 0.76f, 0.84f, 0.72f);

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
        if (theme is SensoryTheme.ProtanopiaDeuteranopia or SensoryTheme.Tritanopia or SensoryTheme.HighContrast)
        {
            usePlayerFactionColor = false;
        }
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
        Scribe_Values.Look(ref sonarCustomColor, "sonarCustomColor", new Color(0.22f, 0.57f, 1.0f));
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
        Scribe_Values.Look(ref showGeoscapeClouds, "showGeoscapeClouds", true);
        Scribe_Values.Look(ref geoscapeCloudOpacity, "geoscapeCloudOpacity", 0.18f);
        geoscapeCloudOpacity = Mathf.Clamp(geoscapeCloudOpacity, 0f, 0.75f);
        Scribe_Values.Look(ref enableSeamlessCameraZoom, "enableSeamlessCameraZoom", true);
        Scribe_Values.Look(ref seamlessZoomOverscrollNotches, "seamlessZoomOverscrollNotches", 2);
        seamlessZoomOverscrollNotches = Mathf.Clamp(seamlessZoomOverscrollNotches, 1, 5);
        Scribe_Values.Look(ref seamlessZoomTargetMode, "seamlessZoomTargetMode", SeamlessZoomTargetMode.CursorFirst);
        Scribe_Values.Look(ref seamlessZoomIndicatorStyle, "seamlessZoomIndicatorStyle", SeamlessZoomIndicatorStyle.Hybrid);
        Scribe_Values.Look(ref seamlessZoomTransitionStyle, "seamlessZoomTransitionStyle", SeamlessZoomTransitionStyle.Traverse);
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
        VigilanceSettingsRegistry.RegisterTab(new BuiltinTabProvider("Diagnostics", "PV_Settings_Tab_Diagnostics", "Diagnostics", 1000, (l, r) => DrawAboutDiagnosticsTab(l)));
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
            string label = VigilanceLoc.Tr(tab.TabLabelKey, tab.FallbackTabLabel);

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
                Widgets.DrawBoxSolid(bannerRect, new Color(0.32f, 0.08f, 0.08f, 0.78f));
                Widgets.DrawHighlight(bannerRect);
                using (new VigilanceUiWidgets.ImguiStateScope())
                {
                    GUI.color = VigilanceUiWidgets.AlertTextColor;
                    Widgets.Label(bannerRect.ContractedBy(4f), "Active Save: EXTERNAL SAVE MODIFICATION DETECTED (Active rules diverge from audit ledger)");
                }
            }
            else if (UnsafeRuleOverride.Effective)
            {
                Widgets.DrawBoxSolid(bannerRect, new Color(0.30f, 0.08f, 0.08f, 0.72f));
                Widgets.DrawHighlight(bannerRect);
                using (new VigilanceUiWidgets.ImguiStateScope())
                {
                    GUI.color = VigilanceUiWidgets.AlertTextColor;
                    Widgets.Label(bannerRect.ContractedBy(4f), "Active Save: DEV OVERRIDE ACTIVE (All rule mutability restrictions bypassed)");
                }
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
        scoutCustomColor = new Color(1.0f, 0.75f, 0.20f);
        sonarCustomColor = new Color(0.22f, 0.57f, 1.0f);
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
        showGeoscapeClouds = true;
        geoscapeCloudOpacity = 0.18f;
        enableSeamlessCameraZoom = true;
        seamlessZoomOverscrollNotches = 2;
        seamlessZoomTargetMode = SeamlessZoomTargetMode.CursorFirst;
        seamlessZoomIndicatorStyle = SeamlessZoomIndicatorStyle.Hybrid;
        seamlessZoomTransitionStyle = SeamlessZoomTransitionStyle.Traverse;
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

    private static string SanitizeSaveText(string? raw, int maxLength) =>
        ProjectVigilance.Core.Migration.Dialog_VigilanceModuleMigration.SanitizeDisplayText(raw, maxLength);
}
