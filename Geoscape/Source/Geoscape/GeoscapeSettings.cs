using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape;

public class GeoscapeSettings : ModSettings
{
    public bool synchronizeMultiVectorRaids = true;
    public bool showFactionEmblems = true;
    public bool resetThreatCooldownOnArrival = false;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref synchronizeMultiVectorRaids, "synchronizeMultiVectorRaids", true);
        Scribe_Values.Look(ref showFactionEmblems, "showFactionEmblems", true);
        Scribe_Values.Look(ref resetThreatCooldownOnArrival, "resetThreatCooldownOnArrival", false);
    }

    public void DoWindowContents(Rect inRect)
    {
        Listing_Standard listing = new();
        listing.Begin(inRect);

        listing.CheckboxLabeled(
            "Synchronize multi-vector raid departures",
            ref synchronizeMultiVectorRaids,
            "When hostile factions launch multi-group raids from separate bases, closer settlements mobilize at base so forces converge simultaneously.");

        listing.Gap();

        listing.CheckboxLabeled(
            "Show faction emblems on Geoscape incursions",
            ref showFactionEmblems,
            "Render the attacking faction's emblem icon on planetary incursions instead of generic crosshairs.");

        listing.Gap();

        listing.CheckboxLabeled(
            "Reset Storyteller threat cycle on map arrival (Default: Off)",
            ref resetThreatCooldownOnArrival,
            "When enabled, the Storyteller threat cooldown begins only when the raid arrives on the colony map rather than upon planetary departure. Warning: may leave extended peaceful intervals between events.");

        listing.End();
    }
}
