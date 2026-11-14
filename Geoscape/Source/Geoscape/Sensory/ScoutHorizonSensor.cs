using System;
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

    private static float GetSafeSunGlow(Map? map)
    {
        if (map == null || Find.World?.grid == null || !map.Tile.Valid)
        {
            return 1.0f;
        }

        try
        {
            return GenCelestial.CurCelestialSunGlow(map);
        }
        catch (Exception)
        {
            return 1.0f;
        }
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

                if (GeoscapeRules.NighttimeSensoryReduction)
                {
                    if (GetSafeSunGlow(mapParent.Map) < 0.35f)
                    {
                        bool anyAwake = HasActiveOrArrivingColonists(
                            mapParent.Map,
                            requireAwake: true,
                            out bool anyAwakeNightOwl);

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
                            ? $"{parentObject.LabelCap} Night Watchers"
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
                ? GeoscapeRules.ColonyScoutRadius
                : GeoscapeRules.CaravanScoutRadius) + vantageBonus;

            if (!GeoscapeRules.NighttimeSensoryReduction)
            {
                return baseRadius;
            }

            // 1. Colony Settlement & Camp lookouts
            if (parentObject is MapParent mapParent && mapParent.HasMap && mapParent.Map != null)
            {
                Map map = mapParent.Map;
                float sunGlow = GetSafeSunGlow(map);
                bool isNight = sunGlow < 0.35f;

                if (!isNight)
                {
                    return baseRadius;
                }

                // Nighttime: check if any conscious colonist is awake, and whether any awake colonist is a Night Owl
                bool anyAwake = HasActiveOrArrivingColonists(
                    map,
                    requireAwake: true,
                    out bool anyAwakeNightOwl);

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

    public int IdentificationRadiusInTiles
    {
        get
        {
            float det = DetectionRadiusInTiles;
            if (det <= 0f)
            {
                return 0;
            }

            int vantage = SensoryTerrainUtility.GetVantageBonus(SensorTile);
            int idVantage = SensoryTerrainUtility.GetIdentificationVantageBonus(vantage);

            if (!isColony)
            {
                // Moving Caravan: 1 ring baseline; elevated vantage bonus (+1 to +2 rings) if on high ground
                return Mathf.Min(Mathf.RoundToInt(det), 1 + idVantage);
            }

            // Colony Lookout:
            // Baseline 2 rings (default 5-tile horizon), or scaled integer if slider modified
            int baseColonyId = Mathf.Max(1, Mathf.RoundToInt(GeoscapeRules.ColonyScoutRadius * 0.4f));

            // Darkness reduction (night watch or asleep): collapses to 1 ring
            float daylightDet = GeoscapeRules.ColonyScoutRadius + vantage;
            if (det < daylightDet)
            {
                return 1;
            }

            return Mathf.Min(Mathf.RoundToInt(det), baseColonyId + idVantage);
        }
    }

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
                return HasActiveOrArrivingColonists(map, requireAwake: false, out _);
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

    public bool TryGetDetectionDistance(IDetectableTarget target, out int distance) =>
        SensorDetectionUtility.TryGetDetectionDistance(this, target, out distance);

    public bool CanDetect(IDetectableTarget target) => TryGetDetectionDistance(target, out _);

    /// <summary>
    /// Evaluates whether a colony or outpost map has at least one conscious spawned colonist
    /// or arriving conscious player colonist in a landing drop pod / shuttle transporter.
    /// </summary>
    internal static bool HasActiveOrArrivingColonists(Map? map, bool requireAwake, out bool anyAwakeNightOwl)
    {
        anyAwakeNightOwl = false;
        if (map == null)
        {
            return false;
        }

        bool anyActive = false;
        var spawnedColonists = map.mapPawns?.FreeColonistsSpawned;
        if (spawnedColonists != null)
        {
            for (int i = 0; i < spawnedColonists.Count; i++)
            {
                Pawn pawn = spawnedColonists[i];
                if (pawn == null || pawn.Dead || pawn.Downed)
                {
                    continue;
                }

                if (requireAwake && (pawn.Suspended || !RestUtility.Awake(pawn)))
                {
                    continue;
                }

                anyActive = true;
                if (!requireAwake)
                {
                    return true;
                }

                if (Caravan_NightResting_Patch.HasNightOwlTrait(pawn))
                {
                    anyAwakeNightOwl = true;
                    return true;
                }
            }
        }

        bool isPlayerMap = map.IsPlayerHome || map.Parent?.Faction?.IsPlayer == true;
        if (isPlayerMap && map.listerThings != null)
        {
            List<Thing>? transporters = map.listerThings.ThingsInGroup(ThingRequestGroup.ActiveTransporter);
            if (ContainsActiveOrArrivingColonist(transporters, requireAwake, ref anyAwakeNightOwl))
            {
                return true;
            }
        }

        return anyActive;
    }

    /// <summary>
    /// Inspects arriving transporters (excluding outgoing leaving ships) for conscious player colonists.
    /// </summary>
    internal static bool ContainsActiveOrArrivingColonist(
        IReadOnlyList<Thing>? transporters,
        bool requireAwake,
        ref bool anyAwakeNightOwl)
    {
        if (transporters == null || transporters.Count == 0)
        {
            return false;
        }

        bool found = false;
        for (int i = 0; i < transporters.Count; i++)
        {
            Thing thing = transporters[i];
            if (thing == null || thing is FlyShipLeaving || thing is not IActiveTransporter transporter)
            {
                continue;
            }

            if (thing is Skyfaller skyfaller &&
                (skyfaller.innerContainer == null ||
                 skyfaller.innerContainer.Count == 0 ||
                 skyfaller.innerContainer[0] is not IActiveTransporter))
            {
                continue;
            }

            ThingOwner? container = transporter.Contents?.innerContainer;
            if (container == null || container.Count == 0)
            {
                continue;
            }

            for (int j = 0; j < container.Count; j++)
            {
                if (container[j] is not Pawn pawn || pawn.Faction?.IsPlayer != true)
                {
                    continue;
                }

                if (pawn.health != null && (pawn.Dead || pawn.Downed))
                {
                    continue;
                }

                if (pawn.def?.race != null && !pawn.IsColonist)
                {
                    continue;
                }

                found = true;
                if (!requireAwake)
                {
                    return true;
                }

                if (Caravan_NightResting_Patch.HasNightOwlTrait(pawn))
                {
                    anyAwakeNightOwl = true;
                    return true;
                }
            }
        }

        return found;
    }
}
