using ProjectVigilance.Geoscape.Sensory;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape;

public class GeoscapeSettings : ModSettings
{
    public bool synchronizeMultiVectorRaids = true;
    public bool showFactionEmblems = true;
    public bool resetThreatCooldownOnArrival = false;
    public bool enableTacticalIngressFlanking = true;

    public VisibilityMode visibilityMode = VisibilityMode.PlanetarySurveillance;
    public RadarOutlineStyle radarOutlineStyle = RadarOutlineStyle.BoundaryAndGrid;
    public RadarColorPalette radarColorPalette = RadarColorPalette.TacticalCyan;
    public float radarOutlineOpacity = 0.85f;
    public float surveillanceDimming = 0.0f;
    public float colonyScoutRadius = 5.0f;
    public float caravanScoutRadius = 3.0f;
    public bool enableEarlyWarningLetters = true;
    public bool enableNighttimeSensoryReduction = true;
    public bool incursionsRestAtNight = true;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref synchronizeMultiVectorRaids, "synchronizeMultiVectorRaids", true);
        Scribe_Values.Look(ref showFactionEmblems, "showFactionEmblems", true);
        Scribe_Values.Look(ref resetThreatCooldownOnArrival, "resetThreatCooldownOnArrival", false);
        Scribe_Values.Look(ref enableTacticalIngressFlanking, "enableTacticalIngressFlanking", true);

        Scribe_Values.Look(ref visibilityMode, "visibilityMode", VisibilityMode.PlanetarySurveillance);
        Scribe_Values.Look(ref radarOutlineStyle, "radarOutlineStyle", RadarOutlineStyle.BoundaryAndGrid);
        Scribe_Values.Look(ref radarColorPalette, "radarColorPalette", RadarColorPalette.TacticalCyan);
        Scribe_Values.Look(ref radarOutlineOpacity, "radarOutlineOpacity", 0.85f);
        Scribe_Values.Look(ref surveillanceDimming, "surveillanceDimming", 0.0f);
        Scribe_Values.Look(ref colonyScoutRadius, "colonyScoutRadius", 5.0f);
        Scribe_Values.Look(ref caravanScoutRadius, "caravanScoutRadius", 3.0f);
        Scribe_Values.Look(ref enableEarlyWarningLetters, "enableEarlyWarningLetters", true);
        Scribe_Values.Look(ref enableNighttimeSensoryReduction, "enableNighttimeSensoryReduction", true);
        Scribe_Values.Look(ref incursionsRestAtNight, "incursionsRestAtNight", true);
    }

    public void DoWindowContents(Rect inRect)
    {
        Listing_Standard listing = new();
        listing.Begin(inRect);

        Text.Font = GameFont.Medium;
        listing.Label("Sensory & Planetary Visibility");
        Text.Font = GameFont.Small;
        listing.GapLine();

        if (listing.RadioButton("Planetary Surveillance (X-COM Style — Default)", visibilityMode == VisibilityMode.PlanetarySurveillance, tooltip: "Continents and static bases are known. Moving hostile incursions are hidden unless within active scout or radar range."))
        {
            if (visibilityMode != VisibilityMode.PlanetarySurveillance)
            {
                visibilityMode = VisibilityMode.PlanetarySurveillance;
                OnVisibilityModeChanged();
            }
        }

        if (listing.RadioButton("Classic Haze of War (3-Tier RTS)", visibilityMode == VisibilityMode.ClassicHazeOfWar, tooltip: "Unexplored tiles are dark, unmonitored explored tiles show a translucent grey haze, and active sensors reveal live movement."))
        {
            if (visibilityMode != VisibilityMode.ClassicHazeOfWar)
            {
                visibilityMode = VisibilityMode.ClassicHazeOfWar;
                OnVisibilityModeChanged();
            }
        }

        if (listing.RadioButton("Omniscient (Spectator / Living Map)", visibilityMode == VisibilityMode.Omniscient, tooltip: "No fog of war. All worldwide movement is visible at all times across the globe without scout or radar requirements."))
        {
            if (visibilityMode != VisibilityMode.Omniscient)
            {
                visibilityMode = VisibilityMode.Omniscient;
                OnVisibilityModeChanged();
            }
        }

        listing.Gap();

        if (visibilityMode != VisibilityMode.Omniscient)
        {
            listing.Label($"Colony Scout Horizon: {colonyScoutRadius:F1} hexes (Natural settlement vision)");
            float newColonyRadius = listing.Slider(colonyScoutRadius, 1f, 15f);
            if (Mathf.Abs(newColonyRadius - colonyScoutRadius) > 0.05f)
            {
                colonyScoutRadius = newColonyRadius;
                SensoryManager.Instance?.Notify_SensorRadiusChanged();
            }

            listing.Label($"Caravan Scout Lookout: {caravanScoutRadius:F1} hexes (Moving caravan vision)");
            float newCaravanRadius = listing.Slider(caravanScoutRadius, 1f, 10f);
            if (Mathf.Abs(newCaravanRadius - caravanScoutRadius) > 0.05f)
            {
                caravanScoutRadius = newCaravanRadius;
                SensoryManager.Instance?.Notify_SensorRadiusChanged();
            }

            listing.CheckboxLabeled(
                "Dispatch Early Warning notifications on contact detection",
                ref enableEarlyWarningLetters,
                "Send a letter when lookouts or sensors first detect an approaching incursion on the planetary map.");

            bool prevNightReduction = enableNighttimeSensoryReduction;
            listing.CheckboxLabeled(
                "Nighttime visual lookout reduction",
                ref enableNighttimeSensoryReduction,
                "Naked-eye lookouts suffer a 50% visual range reduction at night, collapsing to 1 hex if all conscious colonists are asleep.");
            if (prevNightReduction != enableNighttimeSensoryReduction)
            {
                SensoryManager.Instance?.Notify_SensorRadiusChanged();
            }

            listing.Gap();

            listing.Label("Tactical Radar Overlay & Range Outlines (X-COM Style):");

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
                listing.Label($"Radar Color Palette: {GetPaletteLabel(radarColorPalette)}");
                Rect paletteRect = listing.GetRect(26f);
                float btnWidth = (paletteRect.width - 20f) / 5f;
                DrawPaletteButton(new Rect(paletteRect.x, paletteRect.y, btnWidth, 24f), RadarColorPalette.TacticalCyan, "Cyan");
                DrawPaletteButton(new Rect(paletteRect.x + (btnWidth + 5f) * 1, paletteRect.y, btnWidth, 24f), RadarColorPalette.SurveillanceAmber, "Amber");
                DrawPaletteButton(new Rect(paletteRect.x + (btnWidth + 5f) * 2, paletteRect.y, btnWidth, 24f), RadarColorPalette.EmeraldPhosphor, "Emerald");
                DrawPaletteButton(new Rect(paletteRect.x + (btnWidth + 5f) * 3, paletteRect.y, btnWidth, 24f), RadarColorPalette.CrimsonAlert, "Crimson");
                DrawPaletteButton(new Rect(paletteRect.x + (btnWidth + 5f) * 4, paletteRect.y, btnWidth, 24f), RadarColorPalette.GhostWhite, "White");

                listing.Label($"Radar Outline Intensity: {radarOutlineOpacity:P0}");
                float newOpacity = listing.Slider(radarOutlineOpacity, 0.2f, 1f);
                if (Mathf.Abs(newOpacity - radarOutlineOpacity) > 0.02f)
                {
                    radarOutlineOpacity = newOpacity;
                    NotifyRadarVisualsChanged();
                }
            }

            listing.Label($"Surveillance Background Dimming: {surveillanceDimming:P0} (Option 3 Hybrid)");
            float newDimming = listing.Slider(surveillanceDimming, 0.0f, 0.5f);
            if (Mathf.Abs(newDimming - surveillanceDimming) > 0.02f)
            {
                surveillanceDimming = newDimming;
                NotifyRadarVisualsChanged();
            }

            listing.Gap();
        }

        Text.Font = GameFont.Medium;
        listing.Label("Incursion Movement & Storyteller Rules");
        Text.Font = GameFont.Small;
        listing.GapLine();

        listing.CheckboxLabeled(
            "Synchronize multi-vector raid departures",
            ref synchronizeMultiVectorRaids,
            "When hostile factions launch multi-group raids from separate bases, closer settlements mobilize at base so forces converge simultaneously.");

        listing.Gap();

        listing.CheckboxLabeled(
            "Biological incursions rest at night",
            ref incursionsRestAtNight,
            "Hostile warbands and neutral caravans from biological factions halt and camp between 22:00 and 06:00 solar time. Mechanoids and anomaly entities march relentlessly 24/7.");

        listing.Gap();

        listing.CheckboxLabeled(
            "Enable tactical ingress diversification & anti-funnelling",
            ref enableTacticalIngressFlanking,
            "Raiders and tribal warbands occasionally take off-road flanking routes and approach from unexpected vectors to avoid predictable highway checkpoints.");

        listing.Gap();

        listing.CheckboxLabeled(
            "Show faction emblems on Geoscape incursions",
            ref showFactionEmblems,
            "Render the attacking faction's emblem icon on planetary incursions once classified, instead of generic markers.");

        listing.Gap();

        listing.CheckboxLabeled(
            "Reset Storyteller threat cycle on map arrival (Default: Off)",
            ref resetThreatCooldownOnArrival,
            "When enabled, the Storyteller threat cooldown begins only when the raid arrives on the colony map rather than upon planetary departure. Warning: may leave extended peaceful intervals between events.");

        listing.End();
    }

    private void DrawPaletteButton(Rect rect, RadarColorPalette palette, string label)
    {
        bool isSelected = radarColorPalette == palette;
        if (Widgets.ButtonText(rect, isSelected ? $"[{label}]" : label))
        {
            if (radarColorPalette != palette)
            {
                radarColorPalette = palette;
                NotifyRadarVisualsChanged();
            }
        }
    }

    private static string GetPaletteLabel(RadarColorPalette palette) => palette switch
    {
        RadarColorPalette.TacticalCyan => "Tactical Cyan (X-COM Holographic)",
        RadarColorPalette.SurveillanceAmber => "Surveillance Amber (Command Center)",
        RadarColorPalette.EmeraldPhosphor => "Emerald Phosphor (CRT Green)",
        RadarColorPalette.CrimsonAlert => "Crimson Alert (High Threat)",
        RadarColorPalette.GhostWhite => "Ghost White (Monochrome)",
        _ => palette.ToString()
    };

    private void NotifyRadarVisualsChanged()
    {
        WorldDrawLayer_RadarCoverage.Instance?.SetDirty();
        WorldDrawLayer_RadarCoverage.Instance?.RegenerateNow();
    }

    private void OnVisibilityModeChanged()
    {
        WorldDrawLayer_HazeOfWar.Instance?.SetDirty();
        WorldDrawLayer_RadarCoverage.Instance?.SetDirty();
        if (Find.WorldGrid?.Surface != null)
        {
            Find.WorldGrid.Surface.SetAllLayersDirty();
        }
        Find.WorldFeatures?.UpdateFeatures();
    }
}
