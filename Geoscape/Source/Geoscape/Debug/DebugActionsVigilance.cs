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
/// Grouped logically by operational domain: Incursions, Outposts, and Recon/Sensory.
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

    internal static Func<PlanetTile>? TargetTileOverride;

    internal static PlanetTile ResolveTargetTile()
    {
        if (TargetTileOverride != null)
        {
            return TargetTileOverride();
        }

        // 1. If in World view, prioritize the user's interactive cursor click (ToolWorld)
        if (WorldRendererUtility.WorldRendered)
        {
            // A. If mouse is hovering over a valid globe tile without window obstruction, use the clicked tile
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

            // B. If player has an explicitly selected tile on the world selector (fallback)
            if (Find.WorldSelector != null && Find.WorldSelector.SelectedTile.Valid)
            {
                return Find.WorldSelector.SelectedTile;
            }

            // C. If a single world object is selected, use its tile (fallback when clicked off-globe)
            if (Find.WorldSelector?.SingleSelectedObject != null && Find.WorldSelector.SingleSelectedObject.Tile.Valid)
            {
                return Find.WorldSelector.SingleSelectedObject.Tile;
            }
        }

        // 2. If currently on a colony map view, target the map's tile
        if (!WorldRendererUtility.WorldRendered && Find.CurrentMap != null && Find.CurrentMap.Tile.Valid)
        {
            return Find.CurrentMap.Tile;
        }

        // 3. Fallback to active player colony map
        PlanetTile colonyTile = ResolveColonyTile();
        if (colonyTile.Valid)
        {
            return colonyTile;
        }

        // 4. Last resort: GenWorld.MouseTile
        PlanetTile fallbackMouse = GenWorld.MouseTile();
        if (fallbackMouse.Valid) return fallbackMouse;

        return PlanetTile.Invalid;
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
