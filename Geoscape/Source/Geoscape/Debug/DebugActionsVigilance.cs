using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Geoscape.Orbital;
using ProjectVigilance.Geoscape.Sensory;
using ProjectVigilance.Geoscape.World;

namespace ProjectVigilance.Geoscape.Debug;

/// <summary>
/// Centralized Developer Mode debug actions for Project Vigilance.
/// Grouped logically by operational domain: Incursions, Outposts, Orbital, and Recon/Sensory.
/// </summary>
public static class DebugActionsVigilance
{
    private static bool TryGetDebugIncidentParms(IncidentCategoryDef category, float points, out IncidentParms parms)
    {
        Map map = Find.CurrentMap;
        if (map == null)
        {
            Messages.Message("No active player map found.", MessageTypeDefOf.RejectInput, false);
            parms = null!;
            return false;
        }

        parms = StorytellerUtility.DefaultParmsNow(category, map);
        if (points > 0f)
        {
            parms.points = points;
        }
        return true;
    }

    private static PlanetTile ResolveTargetTile()
    {
        // 1. If currently in World view and mouse is hovering over a valid globe tile without window obstruction
        // (This is the primary path when invoked as ToolWorld or directly clicking the 3D globe)
        if (WorldRendererUtility.WorldRendered)
        {
            bool isMouseObscured = Find.WindowStack != null && Find.WindowStack.MouseObscuredNow;
            if (!isMouseObscured)
            {
                PlanetTile mouseTile = GenWorld.MouseTile();
                if (mouseTile.Valid) return mouseTile;
            }

            // 2. If mouse is obscured by a dev menu/palette window, prioritize player's explicitly selected tile
            if (Find.WorldSelector != null && Find.WorldSelector.SelectedTile.Valid)
            {
                return Find.WorldSelector.SelectedTile;
            }

            // 3. Or selected world object's tile
            if (Find.WorldSelector?.SingleSelectedObject != null && Find.WorldSelector.SingleSelectedObject.Tile.Valid)
            {
                return Find.WorldSelector.SingleSelectedObject.Tile;
            }
        }

        // 4. If a tile is selected on WorldSelector (even if camera is currently on map view)
        if (Find.WorldSelector != null && Find.WorldSelector.SelectedTile.Valid)
        {
            return Find.WorldSelector.SelectedTile;
        }

        // 5. Fallback to active player map
        Map map = Find.CurrentMap;
        if (map != null && map.Tile.Valid)
        {
            return map.Tile;
        }

        // 6. Last resort: GenWorld.MouseTile
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
        float orbitalDriftDegPerDay)
    {
        float lon = Find.WorldGrid?.LongLatOf(targetTile).x ?? 0f;
        float lat = Find.WorldGrid?.LongLatOf(targetTile).y ?? 0f;
        int currentTick = Current.Game?.tickManager?.TicksGame ?? 0;

        // Ensure inclination is strictly greater than latitude so ground track passes directly overhead
        float inclination = Mathf.Clamp(Mathf.Abs(lat) + extraInclinationDeg, 10f, 89.9f);

        // Solve for argument of latitude u at epoch such that latitudeDeg == lat at epochTick
        float incRad = inclination * Mathf.Deg2Rad;
        float latRad = lat * Mathf.Deg2Rad;
        float sinU = Mathf.Sin(latRad) / Mathf.Sin(incRad);
        sinU = Mathf.Clamp(sinU, -1f, 1f);
        float uRad = Mathf.Asin(sinU);
        float uDeg = uRad * Mathf.Rad2Deg;
        if (uDeg < 0f) uDeg += 360f;
        uRad = uDeg * Mathf.Deg2Rad;

        // Angular displacement from ascending node along equator
        float yNode = Mathf.Sin(uRad) * Mathf.Cos(incRad);
        float xNode = Mathf.Cos(uRad);
        float alphaNodeDeg = Mathf.Atan2(yNode, xNode) * Mathf.Rad2Deg;

        // Solve for Ascending Node RAAN such that longitudeDeg == lon at epochTick
        float raanDeg = ((lon - alphaNodeDeg + 180f) % 360f + 360f) % 360f - 180f;

        return new OrbitalParameters(
            altitudeKm: altitudeKm,
            periodTicks: periodTicks,
            inclinationDeg: inclination,
            ascendingNodeDeg: raanDeg,
            precessionRateDegPerDay: precessionRateDegPerDay,
            meanAnomalyEpochDeg: uDeg,
            orbitalDriftDegPerDay: orbitalDriftDegPerDay,
            epochTick: currentTick);
    }

    #region Incursion & Skirmish Actions

    [DebugAction("Project Vigilance", "Incursion: Spawn Walk-In Raid (500 pts)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnWalkInRaid500()
    {
        if (!TryGetDebugIncidentParms(IncidentCategoryDefOf.ThreatBig, 500f, out var parms)) return;
        parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
        IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
    }

    [DebugAction("Project Vigilance", "Incursion: Spawn Walk-In Raid (1500 pts)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnWalkInRaid1500()
    {
        if (!TryGetDebugIncidentParms(IncidentCategoryDefOf.ThreatBig, 1500f, out var parms)) return;
        parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
        IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
    }

    [DebugAction("Project Vigilance", "Incursion: Spawn Multi-Vector Raid (1500 pts)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnMultiVectorRaid1500()
    {
        if (!TryGetDebugIncidentParms(IncidentCategoryDefOf.ThreatBig, 1500f, out var parms)) return;
        parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkInGroups;
        IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
    }

    [DebugAction("Project Vigilance", "Incursion: Spawn Multi-Vector Raid (3000 pts)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnMultiVectorRaid3000()
    {
        if (!TryGetDebugIncidentParms(IncidentCategoryDefOf.ThreatBig, 3000f, out var parms)) return;
        parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkInGroups;
        IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
    }

    [DebugAction("Project Vigilance", "Incursion: Spawn Trade Caravan", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnTradeCaravan()
    {
        if (!TryGetDebugIncidentParms(IncidentDefOf.TraderCaravanArrival.category, 0f, out var parms)) return;
        IncidentDefOf.TraderCaravanArrival.Worker.TryExecute(parms);
    }

    [DebugAction("Project Vigilance", "Incursion: Spawn Visitor Group", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnVisitorGroup()
    {
        if (!TryGetDebugIncidentParms(IncidentDefOf.VisitorGroup.category, 0f, out var parms)) return;
        IncidentDefOf.VisitorGroup.Worker.TryExecute(parms);
    }

    [DebugAction("Project Vigilance", "Incursion: Spawn Skirmish Collision Pair", actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
    private static void SpawnSkirmishCollisionPair()
    {
        PlanetTile targetTile = ResolveTargetTile();
        if (!targetTile.Valid)
        {
            Messages.Message("Select a valid globe tile or enter game world.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        Faction? hostileFaction = Find.FactionManager?.RandomEnemyFaction(allowNonHumanlike: false)
            ?? Find.FactionManager?.AllFactionsListForReading.FirstOrDefault(f => f.HostileTo(Faction.OfPlayer));
        if (hostileFaction == null)
        {
            Messages.Message("No hostile faction found.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        Faction? rivalFaction = Find.FactionManager?.AllFactionsListForReading.FirstOrDefault(f =>
            !f.IsPlayer &&
            f != hostileFaction &&
            f.HostileTo(hostileFaction));
        if (rivalFaction == null)
        {
            rivalFaction = Faction.OfPlayer;
        }

        var incursionDef = DefDatabase<WorldObjectDef>.GetNamedSilentFail("Vigilance_Incursion");
        if (incursionDef == null)
        {
            Messages.Message("Could not find WorldObjectDef 'Vigilance_Incursion'.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        var incA = (WorldObject_Incursion)WorldObjectMaker.MakeWorldObject(incursionDef);
        IncidentParms parmsA = new() { faction = hostileFaction, points = 600f };
        incA.SetupIncursion(IncidentDefOf.RaidEnemy, parmsA, targetTile, targetTile, new List<PlanetTile> { targetTile }, 15f);
        incA.Tile = targetTile;
        Find.WorldObjects.Add(incA);

        var incB = (WorldObject_Incursion)WorldObjectMaker.MakeWorldObject(incursionDef);
        IncidentParms parmsB = new() { faction = rivalFaction, points = 500f };
        incB.SetupIncursion(IncidentDefOf.TraderCaravanArrival, parmsB, targetTile, targetTile, new List<PlanetTile> { targetTile }, 15f);
        incB.Tile = targetTile;
        Find.WorldObjects.Add(incB);

        InterFactionEncounterManager.Instance?.CheckInterFactionCollisions();

        Messages.Message($"Spawned skirmish collision pair at tile {targetTile}: {hostileFaction.Name} vs {rivalFaction.Name}.", MessageTypeDefOf.PositiveEvent, false);
    }

    #endregion

    #region Outpost Actions

    [DebugAction("Project Vigilance", "Outpost: Deploy Listening Post", actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
    private static void DeployListeningPost()
    {
        PlanetTile targetTile = ResolveTargetTile();
        if (!targetTile.Valid)
        {
            Messages.Message("Select a valid globe tile or enter game world.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        var def = DefDatabase<WorldObjectDef>.GetNamedSilentFail("Vigilance_ListeningPost_Automated");
        if (def == null)
        {
            Messages.Message("Could not find WorldObjectDef 'Vigilance_ListeningPost_Automated'.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        var post = (WorldObject_ListeningPost)WorldObjectMaker.MakeWorldObject(def);
        post.Tile = targetTile;
        post.SetFaction(Faction.OfPlayer);
        post.Kind = ListeningPostKind.AutomatedRelay;
        Find.WorldObjects.Add(post);
        SensoryManager.Instance?.PerformSensorySweep();
        WorldDrawLayer_RadarCoverage.Instance?.SetDirty();

        Messages.Message($"Deployed operational Listening Post at tile {targetTile}.", MessageTypeDefOf.PositiveEvent, false);
    }

    [DebugAction("Project Vigilance", "Outpost: Deploy Sonar Station", actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
    private static void DeploySonarStation()
    {
        PlanetTile targetTile = ResolveTargetTile();
        if (!targetTile.Valid)
        {
            Messages.Message("Select a valid globe tile or enter game world.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        if (!SonarTraversalUtility.HasCoastalAccess(targetTile))
        {
            Messages.Message($"Note: Tile {targetTile} lacks coastal access (no adjacent water tiles). Sonar array will have zero acoustic propagation until water is adjacent.", MessageTypeDefOf.CautionInput, false);
        }

        var def = DefDatabase<WorldObjectDef>.GetNamedSilentFail("Vigilance_ListeningPost_Sonar")
            ?? DefDatabase<WorldObjectDef>.GetNamed("Vigilance_ListeningPost_Automated");

        var post = (WorldObject_ListeningPost)WorldObjectMaker.MakeWorldObject(def);
        post.Tile = targetTile;
        post.SetFaction(Faction.OfPlayer);
        post.Kind = ListeningPostKind.AutomatedSonar;
        Find.WorldObjects.Add(post);
        SensoryManager.Instance?.PerformSensorySweep();
        WorldDrawLayer_RadarCoverage.Instance?.SetDirty();

        Messages.Message($"Deployed operational Sonar Station at tile {targetTile}.", MessageTypeDefOf.PositiveEvent, false);
    }

    [DebugAction("Project Vigilance", "Outpost: Deploy Signal Beacon", actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
    private static void DeploySignalBeacon()
    {
        PlanetTile targetTile = ResolveTargetTile();
        if (!targetTile.Valid)
        {
            Messages.Message("Select a valid globe tile or enter game world.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        var def = DefDatabase<WorldObjectDef>.GetNamedSilentFail("Vigilance_ListeningPost_Beacon")
            ?? DefDatabase<WorldObjectDef>.GetNamed("Vigilance_ListeningPost_Automated");

        var post = (WorldObject_ListeningPost)WorldObjectMaker.MakeWorldObject(def);
        post.Tile = targetTile;
        post.SetFaction(Faction.OfPlayer);
        post.Kind = ListeningPostKind.SignalBeacon;
        post.stuffDef = ThingDefOf.WoodLog;
        Find.WorldObjects.Add(post);
        SensoryManager.Instance?.PerformSensorySweep();
        WorldDrawLayer_RadarCoverage.Instance?.SetDirty();

        Messages.Message($"Deployed operational Signal Beacon at tile {targetTile}.", MessageTypeDefOf.PositiveEvent, false);
    }

    #endregion

    #region Orbital Logistics Actions

    [DebugAction("Project Vigilance", "Orbital: Spawn Trade Satellite Overhead", actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
    private static void SpawnTradeSatelliteOverhead()
    {
        PlanetTile targetTile = ResolveTargetTile();
        if (!targetTile.Valid)
        {
            Messages.Message("Select a valid globe tile or enter game world.", MessageTypeDefOf.RejectInput, false);
            return;
        }

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

        Messages.Message($"Spawned Trade Satellite '{sat.OrbitalCallsign}' ({fac.Name}) directly overhead tile {targetTile}.", MessageTypeDefOf.PositiveEvent, false);
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
        SensoryManager.Instance?.PerformSensorySweep();

        Messages.Message($"Spawned Recon Satellite '{sat.OrbitalCallsign}' directly overhead tile {targetTile}.", MessageTypeDefOf.PositiveEvent, false);
    }

    #endregion

    #region Recon & Sensory Actions

    [DebugAction("Project Vigilance", "Recon: Jump to Concealed Site", allowedGameStates = AllowedGameStates.Playing)]
    private static void JumpToConcealedSite()
    {
        var haze = HazeReconnaissanceManager.Instance;
        if (haze == null || !haze.HasConcealedSitesRecorded)
        {
            Messages.Message("No concealed sites currently recorded in Haze.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        var firstRecord = haze.ConcealedSites.Values.FirstOrDefault();
        if (firstRecord != null && firstRecord.tile.Valid)
        {
            CameraJumper.TryJump(firstRecord.tile);
            Messages.Message($"Focused camera on concealed site '{firstRecord.siteLabel}' at tile {firstRecord.tile}.", MessageTypeDefOf.PositiveEvent, false);
        }
    }

    [DebugAction("Project Vigilance", "Recon: Move Selected Caravan to Concealed Site", allowedGameStates = AllowedGameStates.PlayingOnWorld)]
    private static void MoveSelectedCaravanToConcealedSite()
    {
        var caravan = Find.WorldSelector?.SelectedObjects?.OfType<Caravan>().FirstOrDefault();
        if (caravan == null)
        {
            Messages.Message("Please select a caravan on the World Map first.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        var haze = HazeReconnaissanceManager.Instance;
        if (haze == null || !haze.HasConcealedSitesRecorded)
        {
            Messages.Message("No concealed sites currently recorded in Haze.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        var firstRecord = haze.ConcealedSites.Values.FirstOrDefault();
        if (firstRecord != null && firstRecord.tile.Valid)
        {
            caravan.Tile = firstRecord.tile;
            CameraJumper.TryJumpAndSelect(caravan);
            SensoryManager.Instance?.PerformSensorySweep();
            Messages.Message($"Moved caravan '{caravan.LabelCap}' to tile {firstRecord.tile} to trigger reconnaissance reveal.", MessageTypeDefOf.PositiveEvent, false);
        }
    }

    [DebugAction("Project Vigilance", "Recon: Trigger Immediate Sensory Sweep", allowedGameStates = AllowedGameStates.Playing)]
    private static void TriggerSensorySweep()
    {
        SensoryManager.Instance?.PerformSensorySweep();
        WorldDrawLayer_RadarCoverage.Instance?.SetDirty();
        Messages.Message("Executed immediate sensory and radar sweep.", MessageTypeDefOf.PositiveEvent, false);
    }

    [DebugAction("Project Vigilance", "Recon: Unhide All Concealed Sites", allowedGameStates = AllowedGameStates.Playing)]
    private static void UnhideAllConcealedSites()
    {
        var haze = HazeReconnaissanceManager.Instance;
        if (haze == null || !haze.HasConcealedSitesRecorded)
        {
            Messages.Message("No concealed sites currently in Haze.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        int count = haze.ConcealedSites.Count;
        haze.ReleaseAllConcealedSites();
        Messages.Message($"Uncovered {count} concealed sites and unhid all associated quests.", MessageTypeDefOf.PositiveEvent, false);
    }

    #endregion
}
