using System;
using System.Collections.Generic;
using ProjectVigilance.Core.Localization;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape;

public partial class GeoscapeSettings
{
    // =========================================================================
    // Tab 2: Radar Overlay & Tactical Visuals
    // =========================================================================

    private void DrawRadarTab(Listing_Standard listing)
    {
        // 1. Header: Live HUD Scope Preview, Overlay Mode & Background Dimming
        float headerHeight = 138f;
        Rect headerRect = listing.GetRect(headerHeight);

        float previewSize = 136f;
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

            DrawToggleableSlider(
                modeListing,
                $"Surveillance Background Dimming: {surveillanceDimming:P0}",
                ref surveillanceDimming,
                0.0f,
                1.0f,
                radarOutlineStyle != RadarOutlineStyle.None);
        }
        modeListing.End();

        listing.GapLine(4f);

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

        Rect themeRow = listing.GetRect(26f);
        Rect themeBtn = new Rect(themeRow.x, themeRow.y, colWidth, 24f);
        Rect overrideRect = new Rect(themeRow.x + colWidth + 16f, themeRow.y, colWidth, 24f);

        string themeKey = $"PV_Settings_Theme_{sensoryTheme}";
        string themeLabel = VigilanceLoc.Tr(themeKey, sensoryTheme.ToString());
        if (Widgets.ButtonText(themeBtn, $"Theme: {themeLabel}"))
        {
            List<FloatMenuOption> themeOptions = new();
            foreach (SensoryTheme st in Enum.GetValues(typeof(SensoryTheme)))
            {
                string key = $"PV_Settings_Theme_{st}";
                string lbl = VigilanceLoc.Tr(key, st.ToString());
                themeOptions.Add(new FloatMenuOption(lbl, () =>
                {
                    ApplySensoryTheme(st);
                }));
            }
            Find.WindowStack.Add(new FloatMenu(themeOptions));
        }

        bool customOverride = useCustomPaletteOverrides;
        string overrideLabel = VigilanceLoc.Tr("PV_Settings_UseCustomPaletteOverrides", "Use Custom Palette Overrides");
        string overrideDesc = VigilanceLoc.Tr(
            "PV_Settings_UseCustomPaletteOverrides_Desc",
            "Enable granular colour overrides for Radar, Identification, and Scout perimeters. When disabled, curated theme presets apply.");
        if (!outlinesEnabled) GUI.color = DisabledControlColor;
        Widgets.CheckboxLabeled(overrideRect, overrideLabel, ref customOverride, disabled: !outlinesEnabled);
        GUI.color = Color.white;
        TooltipHandler.TipRegion(overrideRect, overrideDesc);
        if (outlinesEnabled && customOverride != useCustomPaletteOverrides)
        {
            useCustomPaletteOverrides = customOverride;
            NotifyRadarVisualsChanged();
        }

        listing.Gap(2f);

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

        radarColumnsHeight = Mathf.Max(leftListing.CurHeight, rightListing.CurHeight) + 2f;

        listing.GapLine(4f);

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

            Rect intensityRow = shroudCol.GetRect(24f);
            Rect intensityLabelRect = new Rect(intensityRow.x, intensityRow.y + 1f, intensityRow.width * 0.56f, 22f);
            Rect intensityBarRect = new Rect(intensityLabelRect.xMax + 6f, intensityRow.y + 1f, intensityRow.width * 0.44f - 6f, 22f);
            Widgets.Label(intensityLabelRect, "PV_Shroud_IntensityLabel".Translate(Mathf.RoundToInt(shroudAtmosphereIntensity * 100f)));
            float newIntensity = Widgets.HorizontalSlider(intensityBarRect, shroudAtmosphereIntensity, 0f, 1f, middleAlignment: true);
            if (Mathf.Abs(newIntensity - shroudAtmosphereIntensity) > 0.005f)
            {
                shroudAtmosphereIntensity = Mathf.Clamp01(newIntensity);
                shroudEffects = shroudAtmosphereIntensity <= 0.01f
                    ? ShroudEffectLevel.Off
                    : (shroudAtmosphereIntensity >= 0.65f ? ShroudEffectLevel.Atmospheric : ShroudEffectLevel.Subtle);
                WorldDrawLayer_ShroudShell.NotifyAtmosphereSettingsChanged();
            }
            TooltipHandler.TipRegion(intensityRow, "PV_Shroud_IntensityTooltip".Translate());

            bool prevReduceMotion = reduceShroudMotion;
            DrawToggleableCheckbox(
                shroudCol,
                "PV_Shroud_ReduceMotionLabel".Translate(),
                ref reduceShroudMotion,
                "PV_Shroud_ReduceMotionTooltip".Translate(),
                shroudAtmosphereIntensity > 0.01f);
            if (prevReduceMotion != reduceShroudMotion)
            {
                if (reduceShroudMotion)
                {
                    seamlessZoomTransitionStyle = SeamlessZoomTransitionStyle.Instant;
                }
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

            shroudCol.CheckboxLabeled(
                "PV_Shroud_CloudsLabel".Translate(),
                ref showGeoscapeClouds,
                "PV_Shroud_CloudsTooltip".Translate());

            Rect cloudSliderRow = shroudCol.GetRect(24f);
            Rect cloudLabelRect = new Rect(cloudSliderRow.x, cloudSliderRow.y + 1f, cloudSliderRow.width * 0.56f, 22f);
            Rect cloudBarRect = new Rect(cloudLabelRect.xMax + 6f, cloudSliderRow.y + 1f, cloudSliderRow.width * 0.44f - 6f, 22f);
            if (!showGeoscapeClouds)
            {
                GUI.color = DisabledControlColor;
            }
            Widgets.Label(cloudLabelRect, "PV_Shroud_CloudOpacityLabel".Translate(Mathf.RoundToInt(geoscapeCloudOpacity * 100f)));
            float newCloudOpacity = Widgets.HorizontalSlider(cloudBarRect, geoscapeCloudOpacity, 0f, 0.75f, middleAlignment: true);
            if (!showGeoscapeClouds)
            {
                GUI.color = Color.white;
            }
            else if (Mathf.Abs(newCloudOpacity - geoscapeCloudOpacity) > 0.005f)
            {
                geoscapeCloudOpacity = newCloudOpacity;
            }
        }
        shroudCol.End();

        radarBottomRowHeight = Mathf.Max(shadowCol.CurHeight, shroudCol.CurHeight);
    }
}
