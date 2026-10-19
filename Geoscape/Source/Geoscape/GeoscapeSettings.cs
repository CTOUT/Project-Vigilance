using ProjectVigilance.Geoscape.Sensory;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape;

public class GeoscapeSettings : ModSettings
{
    public bool synchronizeMultiVectorRaids = true;
    public bool showFactionEmblems = true;
    public bool resetThreatCooldownOnArrival = false;

    public VisibilityMode visibilityMode = VisibilityMode.PlanetarySurveillance;
    public float colonyScoutRadius = 5.0f;
    public float caravanScoutRadius = 3.0f;
    public bool enableEarlyWarningLetters = true;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref synchronizeMultiVectorRaids, "synchronizeMultiVectorRaids", true);
        Scribe_Values.Look(ref showFactionEmblems, "showFactionEmblems", true);
        Scribe_Values.Look(ref resetThreatCooldownOnArrival, "resetThreatCooldownOnArrival", false);

        Scribe_Values.Look(ref visibilityMode, "visibilityMode", VisibilityMode.PlanetarySurveillance);
        Scribe_Values.Look(ref colonyScoutRadius, "colonyScoutRadius", 5.0f);
        Scribe_Values.Look(ref caravanScoutRadius, "caravanScoutRadius", 3.0f);
        Scribe_Values.Look(ref enableEarlyWarningLetters, "enableEarlyWarningLetters", true);
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
            visibilityMode = VisibilityMode.PlanetarySurveillance;
        }

        if (listing.RadioButton("Classic Haze of War (3-Tier RTS)", visibilityMode == VisibilityMode.ClassicHazeOfWar, tooltip: "Unexplored tiles are dark, unmonitored explored tiles show a translucent grey haze, and active sensors reveal live movement."))
        {
            visibilityMode = VisibilityMode.ClassicHazeOfWar;
        }

        if (listing.RadioButton("Omniscient (Spectator / Living Map)", visibilityMode == VisibilityMode.Omniscient, tooltip: "No fog of war. All worldwide movement is visible at all times across the globe without scout or radar requirements."))
        {
            visibilityMode = VisibilityMode.Omniscient;
        }

        listing.Gap();

        if (visibilityMode != VisibilityMode.Omniscient)
        {
            listing.Label($"Colony Scout Horizon: {colonyScoutRadius:F1} hexes (Natural settlement vision)");
            colonyScoutRadius = listing.Slider(colonyScoutRadius, 1f, 15f);

            listing.Label($"Caravan Scout Lookout: {caravanScoutRadius:F1} hexes (Moving caravan vision)");
            caravanScoutRadius = listing.Slider(caravanScoutRadius, 1f, 10f);

            listing.CheckboxLabeled(
                "Dispatch Early Warning notifications on contact detection",
                ref enableEarlyWarningLetters,
                "Send a letter when lookouts or sensors first detect an approaching incursion on the planetary map.");

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
}
