using System;
using System.Linq;
using LudeonTK;
using ProjectVigilance.Core.Modules;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Orbital;

/// <summary>
/// Developer Mode debug actions for spawning, inspecting, and clearing Project Vigilance orbital constellations.
/// </summary>
public static class DebugActionsOrbital
{
    private static PlanetTile ResolveColonyTile()
    {
        if (Find.CurrentMap != null && Find.CurrentMap.IsPlayerHome && Find.CurrentMap.Tile.Valid)
        {
            return Find.CurrentMap.Tile;
        }

        Map home = Find.AnyPlayerHomeMap;
        if (home != null && home.Tile.Valid)
        {
            return home.Tile;
        }

        Settlement? playerSettlement = Find.WorldObjects?.Settlements?.FirstOrDefault(s => s.Faction?.IsPlayer == true);
        if (playerSettlement != null && playerSettlement.Tile.Valid)
        {
            return playerSettlement.Tile;
        }

        return PlanetTile.Invalid;
    }

    private static PlanetTile ResolveTargetTile()
    {
        if (WorldRendererUtility.WorldRendered)
        {
            bool isMouseObscured = Find.WindowStack != null && Find.WindowStack.MouseObscuredNow;
            if (!isMouseObscured)
            {
                PlanetLayer? surfaceLayer = Find.WorldGrid?.Surface;
                PlanetTile mouseTile = GenWorld.MouseTile();
                if (mouseTile.Valid && (surfaceLayer == null || mouseTile.Layer == surfaceLayer))
                {
                    return mouseTile;
                }

                // When Odyssey's Orbit layer is active (deep orbit view), GenWorld.MouseTile raycasts against the
                // Orbit layer instead of Surface and returns Invalid (or an Orbit-layer tile) for clicks over the
                // globe or orbital shell. Project the WorldCamera ray to the Surface tile directly beneath the cursor.
                Camera? worldCam = null;
                try
                {
                    worldCam = Find.WorldCamera;
                }
                catch
                {
                    worldCam = null;
                }

                if (worldCam != null)
                {
                    Ray ray = worldCam.ScreenPointToRay(UI.MousePositionOnUIInverted * Prefs.UIScale);
                    if (OrbitalFlightModel.TryProjectRayToPlanetDirection(ray.origin, ray.direction, out Vector3 dir))
                    {
                        PlanetTile surfaceTile = OrbitalFlightModel.FindClosestSurfaceTile(dir, surfaceLayer);
                        if (surfaceTile.Valid)
                        {
                            return surfaceTile;
                        }
                    }
                }

                if (mouseTile.Valid)
                {
                    if (Find.WorldGrid != null)
                    {
                        Vector3 orbitTileDir = Find.WorldGrid.GetTileCenter(mouseTile);
                        PlanetTile surfaceTile = OrbitalFlightModel.FindClosestSurfaceTile(orbitTileDir, surfaceLayer);
                        if (surfaceTile.Valid)
                        {
                            return surfaceTile;
                        }
                    }
                    return mouseTile;
                }
            }

            if (Find.WorldSelector != null && Find.WorldSelector.SelectedTile.Valid)
            {
                return Find.WorldSelector.SelectedTile;
            }

            if (Find.WorldSelector?.SingleSelectedObject != null && Find.WorldSelector.SingleSelectedObject.Tile.Valid)
            {
                return Find.WorldSelector.SingleSelectedObject.Tile;
            }
        }

        if (!WorldRendererUtility.WorldRendered && Find.CurrentMap != null && Find.CurrentMap.Tile.Valid)
        {
            return Find.CurrentMap.Tile;
        }

        PlanetTile colonyTile = ResolveColonyTile();
        if (colonyTile.Valid)
        {
            return colonyTile;
        }

        PlanetTile fallbackMouse = GenWorld.MouseTile();
        if (fallbackMouse.Valid) return fallbackMouse;

        return PlanetTile.Invalid;
    }

    private static OrbitalParameters CreateOverheadOrbitParameters(
        PlanetTile targetTile,
        float altitudeKm,
        int periodTicks,
        float extraInclinationDeg,
        float precessionRateDegPerDay,
        float orbitalDriftDegPerDay) =>
        OrbitalFlightModel.CreateOverheadOrbitParameters(
            targetTile,
            altitudeKm,
            periodTicks,
            extraInclinationDeg,
            precessionRateDegPerDay,
            orbitalDriftDegPerDay);

    [DebugAction("Project Vigilance", "Orbital: Spawn Trade Satellite Over Colony", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
    private static void SpawnTradeSatelliteOverColony()
    {
        PlanetTile colonyTile = ResolveColonyTile();
        if (!colonyTile.Valid)
        {
            Messages.Message("No active player colony found to spawn satellite over.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        SpawnTradeSatelliteInternal(colonyTile);
    }

    [DebugAction("Project Vigilance", "Orbital: Spawn Trade Satellite Overhead", actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
    private static void SpawnTradeSatelliteOverhead()
    {
        PlanetTile targetTile = ResolveTargetTile();
        if (!targetTile.Valid)
        {
            Messages.Message("Select a valid globe tile or enter game world.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        SpawnTradeSatelliteInternal(targetTile);
    }

    private static void SpawnTradeSatelliteInternal(PlanetTile targetTile)
    {
        OrbitalParameters orbitParams = CreateOverheadOrbitParameters(
            targetTile,
            altitudeKm: 600f,
            periodTicks: 62500,
            extraInclinationDeg: 5f,
            precessionRateDegPerDay: -3.5f,
            orbitalDriftDegPerDay: 0.8f);

        Faction fac = Find.FactionManager?.AllFactionsListForReading.FirstOrDefault(f => !f.IsPlayer && !f.HostileTo(Faction.OfPlayer))
            ?? Faction.OfPlayer;

        var def = OrbitalDefOf.Vigilance_Satellite_Trade ?? DefDatabase<WorldObjectDef>.GetNamed("Vigilance_Satellite_Trade");
        var sat = (Satellite_Trade)WorldObjectMaker.MakeWorldObject(def);
        sat.SetFaction(fac);
        int uniqueId = Find.TickManager != null ? (Find.TickManager.TicksGame % 10000) : Rand.Range(100, 999);
        sat.InitializeSatellite(SatelliteRole.Trade, orbitParams, stationId: -1, callsign: $"Merchant-{uniqueId}", footprintRadius: 16f);
        Find.WorldObjects.Add(sat);
        sat.CheckColonyOverpass();

        if (WorldRendererUtility.WorldRendered)
        {
            Find.WorldSelector?.ClearSelection();
            Find.WorldSelector?.Select(sat);
            Find.WorldCameraDriver?.JumpTo(sat.DrawPos);
        }

        Messages.Message($"Spawned Trade Satellite '{sat.OrbitalCallsign}' ({fac.Name}) overhead tile {targetTile}. Comms available at Comms Console.", MessageTypeDefOf.PositiveEvent, false);
    }

    [DebugAction("Project Vigilance", "Orbital: Spawn Recon Satellite Over Colony", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
    private static void SpawnReconSatelliteOverColony()
    {
        PlanetTile colonyTile = ResolveColonyTile();
        if (!colonyTile.Valid)
        {
            Messages.Message("No active player colony found to spawn satellite over.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        SpawnReconSatelliteInternal(colonyTile);
    }

    [DebugAction("Project Vigilance", "Orbital: Spawn Recon Satellite Overhead", actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
    private static void SpawnReconSatelliteOverhead()
    {
        PlanetTile targetTile = ResolveTargetTile();
        if (!targetTile.Valid)
        {
            Messages.Message("Select a valid globe tile or enter game world.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        SpawnReconSatelliteInternal(targetTile);
    }

    private static void SpawnReconSatelliteInternal(PlanetTile targetTile)
    {
        OrbitalParameters orbitParams = CreateOverheadOrbitParameters(
            targetTile,
            altitudeKm: 450f,
            periodTicks: 45000,
            extraInclinationDeg: 10f,
            precessionRateDegPerDay: -4.5f,
            orbitalDriftDegPerDay: 1.2f);

        var def = OrbitalDefOf.Vigilance_Satellite_Recon ?? DefDatabase<WorldObjectDef>.GetNamed("Vigilance_Satellite_Recon");
        var sat = (WorldObject_Satellite)WorldObjectMaker.MakeWorldObject(def);
        sat.SetFaction(Faction.OfPlayer);
        int uniqueId = Find.TickManager != null ? (Find.TickManager.TicksGame % 10000) : Rand.Range(100, 999);
        sat.InitializeSatellite(SatelliteRole.Reconnaissance, orbitParams, stationId: -1, callsign: $"Recon-{uniqueId}", footprintRadius: 12f);
        Find.WorldObjects.Add(sat);
        VigilanceModuleManager.PerformSensorySweep();

        if (WorldRendererUtility.WorldRendered)
        {
            Find.WorldSelector?.ClearSelection();
            Find.WorldSelector?.Select(sat);
            Find.WorldCameraDriver?.JumpTo(sat.DrawPos);
        }

        Messages.Message($"Spawned Recon Satellite '{sat.OrbitalCallsign}' overhead tile {targetTile}.", MessageTypeDefOf.PositiveEvent, false);
    }

    [DebugAction("Project Vigilance", "Orbital: Spawn Nav Satellite Over Colony", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
    private static void SpawnNavSatelliteOverColony()
    {
        PlanetTile colonyTile = ResolveColonyTile();
        if (!colonyTile.Valid)
        {
            Messages.Message("No active player colony found to spawn satellite over.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        SpawnNavSatelliteInternal(colonyTile);
    }

    [DebugAction("Project Vigilance", "Orbital: Spawn Nav Satellite Overhead", actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
    private static void SpawnNavSatelliteOverhead()
    {
        PlanetTile targetTile = ResolveTargetTile();
        if (!targetTile.Valid)
        {
            Messages.Message("Select a valid globe tile or enter game world.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        SpawnNavSatelliteInternal(targetTile);
    }

    private static void SpawnNavSatelliteInternal(PlanetTile targetTile)
    {
        OrbitalParameters orbitParams = CreateOverheadOrbitParameters(
            targetTile,
            altitudeKm: 12000f,
            periodTicks: 120000,
            extraInclinationDeg: 12f,
            precessionRateDegPerDay: -1.2f,
            orbitalDriftDegPerDay: 0.4f);

        var def = OrbitalDefOf.Vigilance_Satellite_Nav ?? DefDatabase<WorldObjectDef>.GetNamed("Vigilance_Satellite_Nav");
        var sat = (WorldObject_Satellite)WorldObjectMaker.MakeWorldObject(def);
        sat.SetFaction(Faction.OfPlayer);
        int uniqueId = Find.TickManager != null ? (Find.TickManager.TicksGame % 10000) : Rand.Range(100, 999);
        sat.InitializeSatellite(SatelliteRole.Navigation, orbitParams, stationId: -1, callsign: $"Nav-{uniqueId}", footprintRadius: 24f);
        Find.WorldObjects.Add(sat);

        if (WorldRendererUtility.WorldRendered)
        {
            Find.WorldSelector?.ClearSelection();
            Find.WorldSelector?.Select(sat);
            Find.WorldCameraDriver?.JumpTo(sat.DrawPos);
        }

        Messages.Message($"Spawned Navigation Satellite '{sat.OrbitalCallsign}' overhead tile {targetTile} (Footprint: 24 tiles).", MessageTypeDefOf.PositiveEvent, false);
    }

    [DebugAction("Project Vigilance", "Orbital: Despawn Overhead Satellites", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
    private static void DespawnOverheadSatellites()
    {
        PlanetTile colonyTile = ResolveColonyTile();
        if (!colonyTile.Valid)
        {
            Messages.Message("No active player colony found.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        var activeSats = OrbitalManager.Instance?.GetActiveTradeSatellitesOverTile(colonyTile);
        if (activeSats == null || activeSats.Count == 0)
        {
            Messages.Message("No trade satellites currently overhead colony.", MessageTypeDefOf.NeutralEvent, false);
            return;
        }

        int count = 0;
        var toRemove = activeSats.ToList();
        for (int i = 0; i < toRemove.Count; i++)
        {
            WorldObject_Satellite sat = toRemove[i];
            Find.WorldObjects?.Remove(sat);
            OrbitalManager.Instance?.DeregisterSatellite(sat);
            count++;
        }

        Messages.Message($"Despawned {count} overhead trade satellites.", MessageTypeDefOf.PositiveEvent, false);
    }

    [DebugAction("Project Vigilance", "Orbital: Spawn Orbital Logistics Hub Overhead", actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
    private static void SpawnOrbitalLogisticsHubOverhead()
    {
        PlanetTile targetTile = ResolveTargetTile();
        if (!targetTile.Valid)
        {
            Messages.Message("Select a valid globe tile or enter game world.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        OrbitalParameters orbitParams = CreateOverheadOrbitParameters(
            targetTile,
            altitudeKm: 8500f,
            periodTicks: 78000,
            extraInclinationDeg: 15f,
            precessionRateDegPerDay: -1.5f,
            orbitalDriftDegPerDay: 1.8f);

        Faction fac = Find.FactionManager?.AllFactionsListForReading.FirstOrDefault(f => !f.IsPlayer && !f.HostileTo(Faction.OfPlayer))
            ?? Faction.OfPlayer;

        var def = OrbitalDefOf.Vigilance_OrbitalStation ?? DefDatabase<WorldObjectDef>.GetNamed("Vigilance_OrbitalStation");
        var station = (WorldObject_OrbitalStation)WorldObjectMaker.MakeWorldObject(def);
        station.SetFaction(fac);
        int uniqueId = Find.TickManager != null ? (Find.TickManager.TicksGame % 10000) : Rand.Range(100, 999);
        station.InitializeStation($"Hub-{uniqueId}", orbitParams);
        station.Tile = targetTile;
        Find.WorldObjects.Add(station);

        if (WorldRendererUtility.WorldRendered)
        {
            Find.WorldSelector?.ClearSelection();
            Find.WorldSelector?.Select(station);
            Find.WorldCameraDriver?.JumpTo(station.DrawPos);
        }

        Messages.Message($"Spawned Orbital Logistics Hub '{station.OrbitalCallsign}' ({fac.Name}) overhead tile {targetTile}.", MessageTypeDefOf.PositiveEvent, false);
    }

    [DebugAction("Project Vigilance", "Orbital: Clear All Satellites", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
    private static void ClearAllSatellites()
    {
        if (Find.WorldObjects == null) return;
        var allSats = Find.WorldObjects.AllWorldObjects.OfType<WorldObject_Satellite>().ToList();
        for (int i = 0; i < allSats.Count; i++)
        {
            Find.WorldObjects.Remove(allSats[i]);
            OrbitalManager.Instance?.DeregisterSatellite(allSats[i]);
        }
        Messages.Message($"Cleared {allSats.Count} satellites from orbit.", MessageTypeDefOf.PositiveEvent, false);
    }

    [DebugAction("Project Vigilance", "Orbital: Clear All Orbital Hubs", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
    private static void ClearAllOrbitalHubs()
    {
        if (Find.WorldObjects == null) return;
        var allHubs = Find.WorldObjects.AllWorldObjects.OfType<WorldObject_OrbitalStation>().ToList();
        for (int i = 0; i < allHubs.Count; i++)
        {
            Find.WorldObjects.Remove(allHubs[i]);
            OrbitalManager.Instance?.DeregisterStation(allHubs[i]);
        }
        Messages.Message($"Cleared {allHubs.Count} orbital hubs from orbit.", MessageTypeDefOf.PositiveEvent, false);
    }
}
