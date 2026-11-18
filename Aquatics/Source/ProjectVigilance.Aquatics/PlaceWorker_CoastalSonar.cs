using RimWorld;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Restricts Settlement Sonar Facility construction to settlements on coastal Geoscape tiles
/// (tiles that have at least one adjacent WaterCovered Geoscape tile).
/// </summary>
public class PlaceWorker_CoastalSonar : PlaceWorker
{
    public const string CoastalAccessRequiredMessage = "Must be constructed at a coastal settlement (requires at least one adjacent water tile on the Geoscape).";

    public override AcceptanceReport AllowsPlacing(
        BuildableDef checkingDef,
        IntVec3 loc,
        Rot4 rot,
        Map map,
        Thing? thingToIgnore = null,
        Thing? thing = null)
    {
        if (map == null || !map.Tile.Valid || !SonarTraversalUtility.HasCoastalAccess(map.Tile))
        {
            return new AcceptanceReport(CoastalAccessRequiredMessage);
        }

        return AcceptanceReport.WasAccepted;
    }
}

