using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// World map arrival action that orders a player caravan to travel to a designated planetary tile
/// containing a sensory outpost or signal beacon, commencing dismantling and recovery upon arrival.
/// </summary>
public class CaravanArrivalAction_DismantleOutpost : CaravanArrivalAction
{
    private PlanetTile destinationTile;
    private WorldObject_ListeningPost? targetOutpost;

    public CaravanArrivalAction_DismantleOutpost()
    {
    }

    public CaravanArrivalAction_DismantleOutpost(WorldObject_ListeningPost outpost, PlanetTile tile)
    {
        targetOutpost = outpost;
        destinationTile = tile.Valid ? tile : (outpost?.Tile ?? PlanetTile.Invalid);
    }

    public PlanetTile TargetTile => destinationTile;
    public WorldObject_ListeningPost? TargetOutpost => targetOutpost;

    public override string Label => targetOutpost != null
        ? $"Dismantle {targetOutpost.LabelCap}"
        : "Dismantle Outpost";

    public override string ReportString => targetOutpost != null
        ? $"travelling to dismantle {targetOutpost.LabelCap}"
        : "travelling to dismantle outpost";

    public override FloatMenuAcceptanceReport StillValid(Caravan caravan, PlanetTile destinationTile)
    {
        if (caravan == null || !caravan.Spawned)
        {
            return false;
        }

        WorldObject_ListeningPost? post = targetOutpost ??
            (Find.WorldObjects?.AllWorldObjects != null
                ? Find.WorldObjects.AllWorldObjects.OfType<WorldObject_ListeningPost>().FirstOrDefault(p => p.Tile == destinationTile)
                : null);

        if (post == null || !post.Spawned || post.Destroyed)
        {
            return FloatMenuAcceptanceReport.WithFailMessage("The target outpost no longer exists.");
        }

        return true;
    }

    public override void Arrived(Caravan caravan)
    {
        if (caravan == null || !caravan.Spawned)
        {
            return;
        }

        WorldObject_ListeningPost? post = targetOutpost ??
            (Find.WorldObjects?.AllWorldObjects != null
                ? Find.WorldObjects.AllWorldObjects.OfType<WorldObject_ListeningPost>().FirstOrDefault(p => p.Tile == destinationTile)
                : null);

        if (post == null || !post.Spawned || post.Destroyed)
        {
            Messages.Message($"Caravan {caravan.LabelCap} arrived at Tile {caravan.Tile}, but the outpost was no longer present.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        post.StartDismantling(1500);
        Messages.Message($"Caravan {caravan.LabelCap} arrived at {post.LabelCap} (Tile {caravan.Tile}). Dismantling commenced (requires remaining on-site).", MessageTypeDefOf.PositiveEvent, false);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref destinationTile, "PV_destinationTile");
        Scribe_References.Look(ref targetOutpost, "PV_targetOutpost");
    }
}
