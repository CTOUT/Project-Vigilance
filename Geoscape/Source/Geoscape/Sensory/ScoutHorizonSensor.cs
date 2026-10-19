using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Natural line-of-sight sensor representing lookouts at player settlements and traveling caravans.
/// Provides baseline early warning without requiring electricity, research, or buildings.
/// </summary>
public class ScoutHorizonSensor : ISensorProvider
{
    private readonly WorldObject parentObject;
    private readonly bool isColony;

    public ScoutHorizonSensor(WorldObject parent, bool isColony)
    {
        this.parentObject = parent;
        this.isColony = isColony;
    }

    public string SensorLabel => isColony
        ? $"{parentObject.LabelCap} Lookouts"
        : $"{parentObject.LabelCap} Scouts";

    public PlanetTile SensorTile => parentObject.Tile;

    public float DetectionRadiusInTiles => isColony
        ? GeoscapeMod.Settings.colonyScoutRadius
        : GeoscapeMod.Settings.caravanScoutRadius;

    public IntelligenceTier MaxIntelligenceTier => IntelligenceTier.Classified;

    public bool IsSensorActive
    {
        get
        {
            if (parentObject == null || parentObject.Destroyed || !parentObject.Spawned)
            {
                return false;
            }

            if (isColony && parentObject is MapParent mapParent)
            {
                return mapParent.HasMap;
            }

            return true;
        }
    }

    public bool CanDetect(IDetectableTarget target)
    {
        if (!IsSensorActive || !SensorTile.Valid || !target.CurrentTile.Valid)
        {
            return false;
        }

        if (SensorTile.Layer != target.CurrentTile.Layer)
        {
            return false;
        }

        float distance = Find.WorldGrid.ApproxDistanceInTiles(SensorTile, target.CurrentTile);
        return distance <= DetectionRadiusInTiles;
    }
}
