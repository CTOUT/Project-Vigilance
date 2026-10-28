using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.World;

/// <summary>
/// Caravan arrival action that guides a player caravan to an Encounter Aftermath site
/// and triggers the scavenging and survivor tending process upon arrival.
/// </summary>
public class CaravanArrivalAction_ScavengeAftermath : CaravanArrivalAction
{
    private WorldObject_EncounterAftermath? aftermath;

    public WorldObject_EncounterAftermath? Aftermath => aftermath;

    public CaravanArrivalAction_ScavengeAftermath()
    {
    }

    public CaravanArrivalAction_ScavengeAftermath(WorldObject_EncounterAftermath aftermath)
    {
        this.aftermath = aftermath;
    }

    public override string Label => "Scavenge aftermath";

    public override string ReportString => "travelling to encounter aftermath";

    public override FloatMenuAcceptanceReport StillValid(Caravan caravan, PlanetTile destinationTile)
    {
        if (aftermath == null || !aftermath.Spawned)
        {
            return FloatMenuAcceptanceReport.WithFailReason("Encounter aftermath has already expired or weathered away.");
        }

        return FloatMenuAcceptanceReport.WasAccepted;
    }

    public override void Arrived(Caravan caravan)
    {
        if (aftermath != null && aftermath.Spawned && caravan.Tile == aftermath.Tile)
        {
            aftermath.ScavengeByCaravan(caravan);
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref aftermath, "aftermath");
    }
}
