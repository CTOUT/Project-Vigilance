using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
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
    private static readonly System.Reflection.FieldInfo? WorldObjectFactionField =
        HarmonyLib.AccessTools.Field(typeof(WorldObject), "faction");

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
        if (Find.World != null)
        {
            baseSetFaction(newFaction);
        }
        else
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
        if (GeoscapeRules.VisibilityMode != VisibilityMode.ClassicHazeOfWar)
        {
            return true;
        }

        // 2. Player-owned orbital entities are always known to the player
        if (wo.Faction != null && wo.Faction.IsPlayer)
        {
            return true;
        }

        // 3. To detect a non-player orbital entity, the player MUST have operational technological infrastructure:
        //    A) Operational technological radar (Early-Warning Radome, Phased Radar Array, SIGINT) within range
        //    B) Powered Comms Console on a colony within footprint / comms range
        //    C) Operational player-owned satellite with orbital space sensors
        return HasActiveSensorCoveringOrbitalEntity(wo);
    }

    /// <summary>
    /// Checks if any player sensory infrastructure currently has detection coverage over the orbital object.
    /// </summary>
    public static bool HasActiveSensorCoveringOrbitalEntity(WorldObject wo)
    {
        if (Find.World == null) return false;

        PlanetTile targetTile = wo.Tile;
        if (!targetTile.Valid) return false;

        // A. Check Colony Ground Infrastructure (Powered Comms Consoles & Technological Radars)
        if (Find.Maps != null)
        {
            for (int i = 0; i < Find.Maps.Count; i++)
            {
                Map map = Find.Maps[i];
                if (!map.IsPlayerHome || !map.Tile.Valid || map.listerBuildings == null) continue;

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

                // 2. Operational Technological Radar (CompRadar):
                // Early-Warning Radar Domes, Phased Radar Arrays, SIGINT Consoles
                var buildings = map.listerBuildings.allBuildingsColonist;
                if (buildings != null)
                {
                    for (int b = 0; b < buildings.Count; b++)
                    {
                        Building building = buildings[b];
                        if (building == null || !building.Spawned || building.Destroyed) continue;

                        CompRadar? radar = building.GetComp<CompRadar>();
                        if (radar != null && radar.IsSensorActive && radar.IsTechnological && !radar.IsOceanicSonar)
                        {
                            float maxRange = radar.DetectionRadiusInTiles;
                            if (Find.WorldGrid != null)
                            {
                                int dist = Find.WorldGrid.TraversalDistanceBetween(
                                    map.Tile, targetTile, passImpassable: true, maxDist: (int)maxRange);
                                if (dist <= maxRange)
                                {
                                    return true;
                                }
                            }
                        }
                    }
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

    /// <summary>
    /// Checks whether the given map possesses an active, powered Comms Console.
    /// </summary>
    public static bool HasPoweredCommsConsole(Map map)
    {
        if (map?.listerBuildings?.allBuildingsColonist == null) return false;
        var buildings = map.listerBuildings.allBuildingsColonist;
        for (int i = 0; i < buildings.Count; i++)
        {
            Building b = buildings[i];
            if (b is Building_CommsConsole console && console.Spawned && !console.Destroyed)
            {
                CompPowerTrader power = console.GetComp<CompPowerTrader>();
                if (power == null || power.PowerOn)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
