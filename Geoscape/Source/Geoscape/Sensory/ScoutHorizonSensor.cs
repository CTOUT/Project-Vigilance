using System.Linq;
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

    public WorldObject Source => parentObject;
    public bool IsColony => isColony;
    public bool IsStationary => isColony;

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
                if (!mapParent.HasMap || mapParent.Map == null)
                {
                    return false;
                }

                Map map = mapParent.Map;
                // Lookouts require at least one conscious colonist present at the settlement
                var pawns = map.mapPawns?.FreeColonistsSpawned;
                if (pawns == null || pawns.Count == 0)
                {
                    return false;
                }

                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn p = pawns[i];
                    if (!p.Dead && !p.Downed)
                    {
                        return true;
                    }
                }

                return false;
            }

            if (!isColony && parentObject is Caravan caravan)
            {
                // Caravan lookouts require at least one conscious colonist/member
                var caravanPawns = caravan.PawnsListForReading;
                if (caravanPawns == null || caravanPawns.Count == 0)
                {
                    return false;
                }

                for (int i = 0; i < caravanPawns.Count; i++)
                {
                    Pawn p = caravanPawns[i];
                    if (p.IsColonist && !p.Dead && !p.Downed)
                    {
                        return true;
                    }
                }

                return false;
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
