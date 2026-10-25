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

        if (caravan.Tile == targetIncursion.Tile)
        {
            IncursionInterceptionUtility.ExecuteInterception(caravan, targetIncursion, isAmbush: false);
            return;
        }

        // If the caravan arrived at the predicted lead hex (NextPathTile) just ahead of the moving incursion:
        if (caravan.Tile == targetIncursion.NextPathTile && targetIncursion.NextPathTile != targetIncursion.Tile)
        {
            if (targetIncursion.TileProgress >= 0.50f)
            {
                targetIncursion.AdvancePathTile();
                IncursionInterceptionUtility.ExecuteInterception(caravan, targetIncursion, isAmbush: false);
                return;
            }

            caravan.pather?.StartPath(targetIncursion.Tile, this, resetPauseStatus: false);
            return;
        }

        // If the incursion is still visible to sensors, continue pursuing its updated lead vector
        var sensoryManager = Find.World?.GetComponent<Sensory.SensoryManager>();
        bool hasVision = sensoryManager != null && sensoryManager.HasActiveVision(targetIncursion.Tile);
        if (GeoscapeMod.Settings.visibilityMode == Sensory.VisibilityMode.Omniscient ||
            targetIncursion.CurrentIntelligenceTier >= Sensory.IntelligenceTier.Contact ||
            hasVision)
        {
            PlanetTile nextTarget = targetIncursion.GetInterceptTargetTile(caravan);
            if (nextTarget.Valid && nextTarget != caravan.Tile)
            {
                caravan.pather?.StartPath(nextTarget, this, resetPauseStatus: false);
                return;
            }
        }

        Messages.Message($"{caravan.LabelCap} arrived at the last known position of {targetIncursion.LabelCap}, but the contact has moved on.", caravan, MessageTypeDefOf.NeutralEvent);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref targetIncursion, "targetIncursion");
    }
}
