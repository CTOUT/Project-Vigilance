using System.Linq;
using ProjectVigilance.Geoscape.Patches;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Natural line-of-sight sensor representing lookouts at player settlements and traveling caravans.
/// Provides baseline early warning without requiring electricity, research, or buildings.
/// Respects day/night illumination cycles, lookout alertness (everyone sleeping), and Night Owl night vision.
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
    public bool IsStationary => isColony || parentObject is MapParent;

    public string SensorLabel
    {
        get
        {
            if (parentObject is MapParent mapParent && mapParent.HasMap && mapParent.Map != null)
            {
                string targetType = isColony ? "Lookouts" : "Camp Lookouts";
                string nightType = isColony ? "Night Watch" : "Camp Night Watch";
                string sleepingType = isColony ? "Sleeping Lookouts" : "Sleeping Camp Watch";

                if (GeoscapeMod.Settings.enableNighttimeSensoryReduction)
                {
                    if (GenCelestial.CurCelestialSunGlow(mapParent.Map) < 0.35f)
                    {
                        var pawns = mapParent.Map.mapPawns?.FreeColonistsSpawned;
                        bool anyAwake = false;
                        bool anyAwakeNightOwl = false;
                        if (pawns != null)
                        {
                            for (int i = 0; i < pawns.Count; i++)
                            {
                                Pawn p = pawns[i];
                                if (!p.Dead && !p.Downed && !p.Suspended && RestUtility.Awake(p))
                                {
                                    anyAwake = true;
                                    if (Caravan_NightResting_Patch.HasNightOwlTrait(p))
                                    {
                                        anyAwakeNightOwl = true;
                                        break;
                                    }
                                }
                            }
                        }

                        if (!anyAwake)
                        {
                            return SensoryNarrativeUtility.IsLowTech
                                ? $"{parentObject.LabelCap} ({sleepingType})"
                                : $"{parentObject.LabelCap} (Unattended Watch)";
                        }

                        if (anyAwakeNightOwl)
                        {
                            return $"{parentObject.LabelCap} ({nightType} - Night Owl)";
                        }

                        return SensoryNarrativeUtility.IsLowTech
                            ? $"{parentObject.LabelCap} {nightType}"
                            : $"{parentObject.LabelCap} {nightType}";
                    }
                }

                return SensoryNarrativeUtility.IsLowTech
                    ? $"{parentObject.LabelCap} Watchers"
                    : $"{parentObject.LabelCap} {targetType}";
            }

            if (parentObject is Caravan caravan)
            {
                int hour = caravan.Tile.Valid ? GenLocalDate.HourInteger(caravan.Tile) : 12;
                Caravan_NightResting_Patch.GetCaravanAwakeWatchState(
                    caravan,
                    hour,
                    out bool anyAwakeColonist,
                    out bool anyAwakeNightOwl);

                if (caravan.NightResting)
                {
                    if (!anyAwakeColonist)
                    {
                        return $"{parentObject.LabelCap} (Asleep in Camp)";
                    }

                    bool isDarkHours = caravan.Tile.Valid && CaravanNightRestUtility.RestingNowAt(caravan.Tile);
                    if (isDarkHours && anyAwakeNightOwl)
                    {
                        return $"{parentObject.LabelCap} (Night Owl Camp Watch)";
                    }

                    return $"{parentObject.LabelCap} (Camp Watch)";
                }

                if (caravan.Tile.Valid && CaravanNightRestUtility.RestingNowAt(caravan.Tile) && anyAwakeNightOwl)
                {
                    return $"{parentObject.LabelCap} (Night Owl Scouts)";
                }
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
            int vantageBonus = SensoryTerrainUtility.GetVantageBonus(SensorTile);
            float baseRadius = (isColony
                ? GeoscapeMod.Settings.colonyScoutRadius
                : GeoscapeMod.Settings.caravanScoutRadius) + vantageBonus;

            if (!GeoscapeMod.Settings.enableNighttimeSensoryReduction)
            {
                return baseRadius;
            }

            // 1. Colony Settlement & Camp lookouts
            if (parentObject is MapParent mapParent && mapParent.HasMap && mapParent.Map != null)
            {
                Map map = mapParent.Map;
                float sunGlow = GenCelestial.CurCelestialSunGlow(map);
                bool isNight = sunGlow < 0.35f;

                if (!isNight)
                {
                    return baseRadius;
                }

                // Nighttime: check if any conscious colonist is awake, and whether any awake colonist is a Night Owl
                bool anyAwake = false;
                bool anyAwakeNightOwl = false;
                var pawns = map.mapPawns?.FreeColonistsSpawned;
                if (pawns != null)
                {
                    for (int i = 0; i < pawns.Count; i++)
                    {
                        Pawn p = pawns[i];
                        if (!p.Dead && !p.Downed && !p.Suspended && RestUtility.Awake(p))
                        {
                            anyAwake = true;
                            if (Caravan_NightResting_Patch.HasNightOwlTrait(p))
                            {
                                anyAwakeNightOwl = true;
                                break;
                            }
                        }
                    }
                }

                if (!anyAwake)
                {
                    // Unattended watch: Everyone is asleep!
                    // Visual horizon collapses to immediate base perimeter
                    return 1.0f;
                }

                if (anyAwakeNightOwl)
                {
                    // Awake Night Owl on watch negates the nighttime vision penalty
                    return baseRadius;
                }

                // Standard night watchman on duty: 50% visibility in darkness
                return Mathf.Max(1.0f, baseRadius * 0.5f);
            }

            // 2. Travelling Caravan lookouts
            if (!isColony && parentObject is Caravan caravan)
            {
                int hour = caravan.Tile.Valid ? GenLocalDate.HourInteger(caravan.Tile) : 12;
                Caravan_NightResting_Patch.GetCaravanAwakeWatchState(
                    caravan,
                    hour,
                    out bool anyAwakeColonist,
                    out bool anyAwakeNightOwl);

                // If every conscious colonist in the caravan is asleep (e.g., all-Daywalker party at 22:00-06:00,
                // or all-Night-Owl party resting at 11:00-18:00), vision collapses to camp perimeter (1.0f).
                if (caravan.NightResting && !anyAwakeColonist)
                {
                    return 1.0f;
                }

                bool isDarkHours = caravan.Tile.Valid && CaravanNightRestUtility.RestingNowAt(caravan.Tile);
                if (isDarkHours)
                {
                    // At night (22:00-06:00):
                    // - If an awake Night Owl is present (either an all-Night-Owl squad marching at night
                    //   or a Night Owl standing camp watch in a mixed squad), they maintain full baseRadius (100%).
                    // - If only non-Night-Owl colonists are awake at night (e.g., force-marching), apply 50% darkness reduction.
                    if (anyAwakeNightOwl)
                    {
                        return baseRadius;
                    }

                    return Mathf.Max(1.0f, baseRadius * 0.5f);
                }

                // During daylight (06:00-22:00), any awake colonist (including Daywalkers on watch while
                // a majority-Night-Owl caravan camps from 11:00-18:00) maintains full baseRadius (100%).
                return baseRadius;
            }

            return baseRadius;
        }
    }

    public IntelligenceTier MaxIntelligenceTier => IntelligenceTier.Classified;

    public int IdentificationRadiusInTiles => DetectionRadiusInTiles > 0f ? Mathf.Max(1, Mathf.RoundToInt(DetectionRadiusInTiles * 0.45f)) : 0;

    public bool PenetratesTerrainMasking => false;

    public bool IsOceanicSonar => false;

    public bool IsSensorActive
    {
        get
        {
            if (parentObject == null || parentObject.Destroyed || !parentObject.Spawned)
            {
                return false;
            }

            if (parentObject is MapParent mapParent)
            {
                if (!mapParent.HasMap || mapParent.Map == null)
                {
                    return false;
                }

                Map map = mapParent.Map;
                // Lookouts require at least one conscious colonist present at the settlement or camp
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

        int maxSteps = Mathf.Max(1, Mathf.RoundToInt(DetectionRadiusInTiles));
        return Find.WorldGrid.TraversalDistanceBetween(SensorTile, target.CurrentTile, passImpassable: true, maxDist: maxSteps) <= maxSteps;
    }
}
