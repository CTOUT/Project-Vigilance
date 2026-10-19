using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Natural line-of-sight sensor representing lookouts at player settlements and traveling caravans.
/// Provides baseline early warning without requiring electricity, research, or buildings.
/// Respects day/night illumination cycles and lookout alertness (everyone sleeping).
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

    public string SensorLabel
    {
        get
        {
            if (isColony)
            {
                if (GeoscapeMod.Settings.enableNighttimeSensoryReduction && parentObject is MapParent mapParent && mapParent.HasMap && mapParent.Map != null)
                {
                    if (GenCelestial.CurCelestialSunGlow(mapParent.Map) < 0.35f)
                    {
                        var pawns = mapParent.Map.mapPawns?.FreeColonistsSpawned;
                        bool anyAwake = pawns != null && pawns.Any(p => !p.Dead && !p.Downed && !p.Suspended && RestUtility.Awake(p));
                        if (!anyAwake)
                        {
                            return SensoryNarrativeUtility.IsLowTech
                                ? $"{parentObject.LabelCap} (Sleeping Lookouts)"
                                : $"{parentObject.LabelCap} (Unattended Watch)";
                        }

                        return SensoryNarrativeUtility.IsLowTech
                            ? $"{parentObject.LabelCap} Night Watchers"
                            : $"{parentObject.LabelCap} Night Watch";
                    }
                }

                return SensoryNarrativeUtility.IsLowTech
                    ? $"{parentObject.LabelCap} Watchers"
                    : $"{parentObject.LabelCap} Lookouts";
            }

            if (parentObject is Caravan caravan && caravan.NightResting)
            {
                return $"{parentObject.LabelCap} (Asleep in Camp)";
            }

            return $"{parentObject.LabelCap} Scouts";
        }
    }

    public bool IsTechnological => false;

    public PlanetTile SensorTile => parentObject.Tile;

    public float DetectionRadiusInTiles
    {
        get
        {
            float baseRadius = isColony
                ? GeoscapeMod.Settings.colonyScoutRadius
                : GeoscapeMod.Settings.caravanScoutRadius;

            if (!GeoscapeMod.Settings.enableNighttimeSensoryReduction)
            {
                return baseRadius;
            }

            // 1. Colony Settlement lookouts
            if (isColony && parentObject is MapParent mapParent && mapParent.HasMap && mapParent.Map != null)
            {
                Map map = mapParent.Map;
                float sunGlow = GenCelestial.CurCelestialSunGlow(map);
                bool isNight = sunGlow < 0.35f;

                if (!isNight)
                {
                    return baseRadius;
                }

                // Nighttime: check if any conscious colonist is awake
                bool anyAwake = false;
                var pawns = map.mapPawns?.FreeColonistsSpawned;
                if (pawns != null)
                {
                    for (int i = 0; i < pawns.Count; i++)
                    {
                        Pawn p = pawns[i];
                        if (!p.Dead && !p.Downed && !p.Suspended && RestUtility.Awake(p))
                        {
                            anyAwake = true;
                            break;
                        }
                    }
                }

                if (!anyAwake)
                {
                    // Unattended watch: Everyone is asleep!
                    // Visual horizon collapses to immediate base perimeter
                    return 1.0f;
                }

                // Night watchman on duty: 50% visibility in darkness
                return Mathf.Max(1.0f, baseRadius * 0.5f);
            }

            // 2. Travelling Caravan lookouts
            if (!isColony && parentObject is Caravan caravan)
            {
                if (caravan.NightResting)
                {
                    // Entire caravan is asleep in camp
                    return 1.0f;
                }

                if (CaravanNightRestUtility.RestingNowAt(caravan.Tile))
                {
                    return Mathf.Max(1.0f, baseRadius * 0.5f);
                }
            }

            return baseRadius;
        }
    }

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
