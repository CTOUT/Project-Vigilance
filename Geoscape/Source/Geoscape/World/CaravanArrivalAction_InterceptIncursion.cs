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
        ? $"Intercept {targetIncursion.LabelCap}"
        : "Intercept";

    public override string ReportString => targetIncursion != null
        ? $"intercepting {targetIncursion.Label}"
        : "intercepting";

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

        IncursionInterceptionUtility.ExecuteInterception(caravan, targetIncursion);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref targetIncursion, "targetIncursion");
    }
}
