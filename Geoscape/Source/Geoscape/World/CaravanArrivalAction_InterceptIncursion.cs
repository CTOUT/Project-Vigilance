using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.World;

public class CaravanArrivalAction_InterceptIncursion : CaravanArrivalAction
{
    private WorldObject_Incursion? targetIncursion;

    public WorldObject_Incursion? TargetIncursion => targetIncursion;

    public CaravanArrivalAction_InterceptIncursion()
    {
    }

    public CaravanArrivalAction_InterceptIncursion(WorldObject_Incursion incursion)
    {
        targetIncursion = incursion;
    }

    public override string Label => targetIncursion != null
        ? (targetIncursion.IsIdentified
            ? (targetIncursion.IsHostileToPlayer ? $"Intercept {targetIncursion.LabelCap}" : $"Approach {targetIncursion.LabelCap}")
            : $"Investigate {targetIncursion.LabelCap}")
        : "Investigate";

    public override string ReportString => targetIncursion != null
        ? (targetIncursion.IsIdentified
            ? (targetIncursion.IsHostileToPlayer ? $"intercepting {targetIncursion.Label}" : $"approaching {targetIncursion.Label}")
            : $"investigating {targetIncursion.Label}")
        : "investigating";

    public override FloatMenuAcceptanceReport StillValid(Caravan caravan, PlanetTile destinationTile)
    {
        if (targetIncursion == null || !targetIncursion.Spawned)
        {
            return FloatMenuAcceptanceReport.WithFailReason("Target contact lost or disbanded.");
        }

        return FloatMenuAcceptanceReport.WasAccepted;
    }

    public override void Arrived(Caravan caravan)
    {
        if (targetIncursion == null || !targetIncursion.Spawned)
        {
            return;
        }

        if (caravan.Tile != targetIncursion.Tile)
        {
            Messages.Message($"{caravan.LabelCap} arrived at the last known position of {targetIncursion.LabelCap}, but the contact has moved on.", caravan, MessageTypeDefOf.NeutralEvent);
            return;
        }

        IncursionInterceptionUtility.ExecuteInterception(caravan, targetIncursion, isAmbush: false);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref targetIncursion, "targetIncursion");
    }
}
