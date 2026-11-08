using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// World map arrival action that directs a player caravan to travel to a designated tile
/// (such as a settlement hex, outpost, or landmark) and hold position on the world map
/// without entering, merging, or triggering automated settlement absorption.
/// </summary>
public class CaravanArrivalAction_HoldPosition : CaravanArrivalAction
{
    private PlanetTile destinationTile;
    private string? locationLabel;

    public CaravanArrivalAction_HoldPosition()
    {
    }

    public CaravanArrivalAction_HoldPosition(PlanetTile tile, string? label = null)
    {
        destinationTile = tile;
        locationLabel = label;
    }

    public PlanetTile TargetTile => destinationTile;
    public string? LocationLabel => locationLabel;

    public override string Label => !string.IsNullOrEmpty(locationLabel)
        ? $"Hold position outside {locationLabel}"
        : $"Hold position at Tile {destinationTile.tileId}";

    public override string ReportString => !string.IsNullOrEmpty(locationLabel)
        ? $"travelling to hold position outside {locationLabel}"
        : $"travelling to Tile {destinationTile.tileId}";

    public override FloatMenuAcceptanceReport StillValid(Caravan caravan, PlanetTile destinationTile)
    {
        if (caravan == null || !caravan.Spawned)
        {
            return false;
        }

        return true;
    }

    public override void Arrived(Caravan caravan)
    {
        if (caravan == null || !caravan.Spawned)
        {
            return;
        }

        // Halt the caravan on the world hex without entering settlement map or triggering absorption
        caravan.pather?.StopDead();

        string msg = !string.IsNullOrEmpty(locationLabel)
            ? $"Caravan {caravan.LabelCap} arrived and is holding position outside {locationLabel}."
            : $"Caravan {caravan.LabelCap} arrived at Tile {caravan.Tile.tileId} and is holding position.";
        Messages.Message(msg, caravan, MessageTypeDefOf.TaskCompletion, historical: false);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref destinationTile, "destinationTile");
        Scribe_Values.Look(ref locationLabel, "locationLabel");
    }
}
