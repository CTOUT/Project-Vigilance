using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Core.Modules;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.Orbital;

/// <summary>
/// Evaluates player detection and visibility of orbital entities (satellites, orbital stations).
/// Enforces the foundational principle: "Information Must Be Earned, Not Guessed".
/// Non-player orbital entities remain completely concealed and unselectable unless the player
/// has active technological sensory infrastructure capable of space tracking covering the entity.
/// </summary>
public static class OrbitalVisibilityUtility
{
    private const int DetectionCacheIntervalTicks = 30;
    private const int MaxDetectionCacheEntries = 4096;

    private readonly struct GroundOrbitalSensorEntry
    {
        public readonly PlanetTile MapTile;
        public readonly Vector3 MapUnitDir;
        public readonly bool HasPoweredComms;
        public readonly float MaxRadarRangeTiles;

        public GroundOrbitalSensorEntry(PlanetTile mapTile, Vector3 mapUnitDir, bool hasPoweredComms, float maxRadarRangeTiles)
        {
            MapTile = mapTile;
            MapUnitDir = mapUnitDir;
            HasPoweredComms = hasPoweredComms;
            MaxRadarRangeTiles = maxRadarRangeTiles;
        }
    }

    private readonly struct CachedOrbitalDetectionEntry
    {
        public readonly int TileId;
        public readonly bool Detected;

        public CachedOrbitalDetectionEntry(int tileId, bool detected)
        {
            TileId = tileId;
            Detected = detected;
        }
    }

    private static readonly System.Reflection.FieldInfo? WorldObjectFactionField =
        HarmonyLib.AccessTools.Field(typeof(WorldObject), "factionInt");

    private static readonly List<GroundOrbitalSensorEntry> cachedGroundSensors = new(4);
    private static readonly List<WorldObject_Satellite> cachedPlayerSensorSatellites = new(8);
    private static readonly Dictionary<WorldObject, CachedOrbitalDetectionEntry> detectionCache = new(ReferenceEqualityComparer.Instance);
    private static int cachedTickBucket = -1;
    private static VisibilityMode cachedVisibilityMode = (VisibilityMode)(-1);
    private static int cachedSweepRevision = -1;
    private static RimWorld.Planet.World? cachedWorld;
    private static OrbitalManager? cachedOrbitalManager;
    private static int cachedMapCount = -1;
    private static bool snapshotBuiltForBucket;

    public static void ClearDetectionCaches()
    {
        cachedGroundSensors.Clear();
        cachedPlayerSensorSatellites.Clear();
        detectionCache.Clear();
        cachedTickBucket = -1;
        cachedVisibilityMode = (VisibilityMode)(-1);
        cachedSweepRevision = -1;
        cachedWorld = null;
        cachedOrbitalManager = null;
        cachedMapCount = -1;
        snapshotBuiltForBucket = false;
    }

    public static Material? ResolveOrbitalMaterial(
        WorldObject wo,
        ref Material? cachedMat,
        ref Faction? cachedFaction)
    {
        if (cachedMat == null || cachedFaction != wo.Faction)
        {
            cachedFaction = wo.Faction;
            Color color = wo.Faction != null ? wo.Faction.Color : Color.white;
            if (wo.def?.texture != null)
            {
                cachedMat = MaterialPool.MatFrom(
                    wo.def.texture,
                    ShaderDatabase.WorldOverlayTransparentLit,
                    color,
                    WorldMaterials.WorldObjectRenderQueue);
            }
        }
        return cachedMat;
    }

    public static void SetFactionSafe(
        WorldObject wo,
        Faction newFaction,
        Action<Faction> baseSetFaction,
        ref Material? cachedMat,
        ref Faction? cachedFaction)
    {
        try
        {
            if (Find.World != null && Find.WorldObjects != null && Find.FactionManager != null)
            {
                baseSetFaction(newFaction);
            }
            else
            {
                WorldObjectFactionField?.SetValue(wo, newFaction);
            }
        }
        catch
        {
            WorldObjectFactionField?.SetValue(wo, newFaction);
        }
        cachedMat = null;
        cachedFaction = null;
    }

    /// <summary>
    /// Checks whether an orbital entity (satellite, orbital station, or space vessel) is currently
    /// detected and visible to the player under current campaign rules and technological infrastructure.
    /// </summary>
    public static bool IsOrbitalEntityDetected(WorldObject wo)
    {
        if (wo == null) return false;

        // 1. Omniscient / Full Visibility modes bypass fog
        VisibilityMode mode = VigilanceModuleManager.GetEffectiveVisibilityMode();
        if (mode != VisibilityMode.ClassicHazeOfWar)
        {
            return true;
        }

        // 2. Player-owned orbital entities are always known to the player
        if (wo.Faction != null && wo.Faction.IsPlayer)
        {
            return true;
        }

        RimWorld.Planet.World? currentWorld = Find.World;
        if (currentWorld == null)
        {
            return false;
        }

        PlanetTile targetTile = wo.Tile;
        if (!targetTile.Valid)
        {
            return false;
        }

        // Bypass caching when test distance overrides are active.
        if (VigilanceModuleManager.DistanceCalculationOverrideHook != null)
        {
            return EvaluateDirectCoverage(wo, targetTile);
        }

        int currentTick = Current.Game?.tickManager?.TicksGame ?? 0;
        int tickBucket = currentTick / DetectionCacheIntervalTicks;
        int sweepRevision = VigilanceModuleManager.GetSensorySweepRevision();
        OrbitalManager? orbitalManager = OrbitalManager.Instance;
        int mapCount = Find.Maps?.Count ?? 0;

        if (cachedTickBucket != tickBucket ||
            cachedVisibilityMode != mode ||
            cachedSweepRevision != sweepRevision ||
            !ReferenceEquals(cachedWorld, currentWorld) ||
            !ReferenceEquals(cachedOrbitalManager, orbitalManager) ||
            cachedMapCount != mapCount)
        {
            detectionCache.Clear();
            cachedGroundSensors.Clear();
            cachedPlayerSensorSatellites.Clear();
            cachedTickBucket = tickBucket;
            cachedVisibilityMode = mode;
            cachedSweepRevision = sweepRevision;
            cachedWorld = currentWorld;
            cachedOrbitalManager = orbitalManager;
            cachedMapCount = mapCount;
            snapshotBuiltForBucket = false;
        }

        int tileId = targetTile.tileId;
        if (detectionCache.TryGetValue(wo, out CachedOrbitalDetectionEntry entry) && entry.TileId == tileId)
        {
            return entry.Detected;
        }

        EnsureSensorSnapshot(orbitalManager);
        bool detected = EvaluateWithSnapshot(wo, targetTile, currentTick);
        if (detectionCache.Count < MaxDetectionCacheEntries)
        {
            detectionCache[wo] = new CachedOrbitalDetectionEntry(tileId, detected);
        }

        return detected;
    }

    private static void EnsureSensorSnapshot(OrbitalManager? orbitalManager)
    {
        if (snapshotBuiltForBucket)
        {
            return;
        }

        snapshotBuiltForBucket = true;
        cachedGroundSensors.Clear();
        cachedPlayerSensorSatellites.Clear();

        if (Find.Maps != null)
        {
            WorldGrid? grid = Find.WorldGrid;
            var maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                Map map = maps[i];
                if (map == null || !map.IsPlayerHome || !map.Tile.Valid || map.listerBuildings == null)
                {
                    continue;
                }

                bool hasComms = HasPoweredCommsConsole(map);
                float maxRadarRange = VigilanceModuleManager.GetMaxGroundRadarRange(map);
                if (!hasComms && maxRadarRange <= 0f)
                {
                    continue;
                }

                Vector3 mapDir = hasComms && grid != null
                    ? grid.GetTileCenter(map.Tile).normalized
                    : Vector3.zero;
                cachedGroundSensors.Add(new GroundOrbitalSensorEntry(map.Tile, mapDir, hasComms, maxRadarRange));
            }
        }

        var satellites = orbitalManager?.Satellites;
        if (satellites != null)
        {
            for (int s = 0; s < satellites.Count; s++)
            {
                var playerSat = satellites[s];
                if (playerSat != null && playerSat.Faction?.IsPlayer == true && playerSat.IsSensorActive)
                {
                    cachedPlayerSensorSatellites.Add(playerSat);
                }
            }
        }
    }

    private static bool EvaluateWithSnapshot(WorldObject wo, PlanetTile targetTile, int currentTick)
    {
        WorldGrid? grid = Find.WorldGrid;
        for (int i = 0; i < cachedGroundSensors.Count; i++)
        {
            GroundOrbitalSensorEntry entry = cachedGroundSensors[i];
            if (entry.HasPoweredComms)
            {
                if (wo is WorldObject_Satellite sat && sat.CoversTile(entry.MapTile))
                {
                    return true;
                }

                if (wo is WorldObject_OrbitalStation station && station.Orbit != null && grid != null)
                {
                    Vector3 stationDir = OrbitalFlightModel.CalculateSubSatelliteDirection(station.Orbit, currentTick);
                    if (OrbitalFlightModel.AngularDistanceDeg(entry.MapUnitDir, stationDir) <= station.ServiceAreaAngularRadiusDeg)
                    {
                        return true;
                    }
                }
            }

            if (entry.MaxRadarRangeTiles > 0f && grid != null &&
                grid.ApproxDistanceInTiles(entry.MapTile, targetTile) <= entry.MaxRadarRangeTiles)
            {
                return true;
            }
        }

        for (int s = 0; s < cachedPlayerSensorSatellites.Count; s++)
        {
            if (cachedPlayerSensorSatellites[s].CoversTile(targetTile))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if any player sensory infrastructure currently has detection coverage over the orbital object.
    /// </summary>
    public static bool HasActiveSensorCoveringOrbitalEntity(WorldObject wo)
    {
        if (Find.World == null || wo == null) return false;

        PlanetTile targetTile = wo.Tile;
        if (!targetTile.Valid) return false;

        if (VigilanceModuleManager.DistanceCalculationOverrideHook != null)
        {
            return EvaluateDirectCoverage(wo, targetTile);
        }

        EnsureSensorSnapshot(OrbitalManager.Instance);
        int currentTick = Current.Game?.tickManager?.TicksGame ?? 0;
        return EvaluateWithSnapshot(wo, targetTile, currentTick);
    }

    private static bool EvaluateDirectCoverage(WorldObject wo, PlanetTile targetTile)
    {
        // A. Check Colony Ground Infrastructure (Powered Comms Consoles & Technological Radars)
        if (Find.Maps != null)
        {
            for (int i = 0; i < Find.Maps.Count; i++)
            {
                Map map = Find.Maps[i];
                if (map == null || !map.IsPlayerHome || !map.Tile.Valid || map.listerBuildings == null) continue;

                // 1. Powered Comms Console:
                // Acquires orbital telemetry when the orbital entity passes within its comms footprint
                if (HasPoweredCommsConsole(map))
                {
                    if (wo is WorldObject_Satellite sat && sat.CoversTile(map.Tile))
                    {
                        return true;
                    }
                    if (wo is WorldObject_OrbitalStation station && station.Orbit != null && Find.WorldGrid != null)
                    {
                        Vector3 stationDir = OrbitalFlightModel.CalculateSubSatelliteDirection(
                            station.Orbit, Current.Game?.tickManager?.TicksGame ?? 0);
                        Vector3 mapDir = Find.WorldGrid.GetTileCenter(map.Tile).normalized;
                        if (OrbitalFlightModel.AngularDistanceDeg(mapDir, stationDir) <= station.ServiceAreaAngularRadiusDeg)
                        {
                            return true;
                        }
                    }
                }

                // 2. Operational Technological Radar (CompRadar via Geoscape soft hook):
                // Early-Warning Radar Domes, Phased Radar Arrays, SIGINT Consoles
                if (VigilanceModuleManager.HasGroundRadarCoveringTile(map, targetTile))
                {
                    return true;
                }
            }
        }

        // B. Check Operational Player-Owned Satellites in Orbit
        var satellites = OrbitalManager.Instance?.Satellites;
        if (satellites != null)
        {
            for (int s = 0; s < satellites.Count; s++)
            {
                var playerSat = satellites[s];
                if (playerSat != null && playerSat.Faction?.IsPlayer == true && playerSat.IsSensorActive)
                {
                    if (playerSat.CoversTile(targetTile))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static Building_CommsConsole? FindPoweredCommsConsoleInList(List<Building>? buildings)
    {
        if (buildings == null)
        {
            return null;
        }

        for (int i = 0; i < buildings.Count; i++)
        {
            Building b = buildings[i];
            if (b is Building_CommsConsole console && console.Spawned && !console.Destroyed)
            {
                CompPowerTrader power = console.GetComp<CompPowerTrader>();
                if (power == null || power.PowerOn)
                {
                    return console;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the first operational, powered Comms Console on the given map, or null if none exist.
    /// </summary>
    public static Building_CommsConsole? FindPoweredCommsConsole(Map map)
    {
        if (map?.listerBuildings == null) return null;

        if (ThingDefOf.CommsConsole != null)
        {
            Building_CommsConsole? vanillaConsole = FindPoweredCommsConsoleInList(
                map.listerBuildings.AllBuildingsColonistOfDef(ThingDefOf.CommsConsole));
            if (vanillaConsole != null)
            {
                return vanillaConsole;
            }
        }

        return FindPoweredCommsConsoleInList(map.listerBuildings.allBuildingsColonist);
    }

    /// <summary>
    /// Checks whether the given map possesses an active, powered Comms Console.
    /// </summary>
    public static bool HasPoweredCommsConsole(Map map)
    {
        return FindPoweredCommsConsole(map) != null;
    }
}
