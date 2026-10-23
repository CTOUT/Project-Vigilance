using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ProjectVigilance.Geoscape.World;

public static class IncursionTacticalRetreatUtility
{
    public static bool CanReformCaravanNow(Map map)
    {
        if (map == null || !Find.Maps.Contains(map)) return false;
        return !GenHostility.AnyHostileActiveThreatToPlayer(map, countDormantPawnsAsHostile: false);
    }

    public static void OpenReformCaravanDialog(Map map)
    {
        if (map == null || !Find.Maps.Contains(map)) return;
        if (Find.WindowStack.IsOpen<Dialog_FormCaravan>()) return;
        Find.WindowStack.Add(new Dialog_FormCaravan(map, reform: true));
    }

    public static void CloseTacticalRetreatDialogs()
    {
        if (Find.WindowStack == null) return;

        var toClose = Find.WindowStack.Windows
            .OfType<Dialog_MessageBox>()
            .Where(w => w.title == "Tactical Retreat" || w.title == "Quietly Disengage Scouts")
            .ToList();

        foreach (var window in toClose)
        {
            window.Close(doCloseSound: false);
        }
    }

    public static void PromptStealthDisengage(Map map, MapComponent_OverlandAmbush ambushComp)
    {
        if (map == null || !Find.Maps.Contains(map) || ambushComp == null) return;

        CloseTacticalRetreatDialogs();

        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
            "Quietly withdraw your scouting squad without alerting the enemy column?\n\n" +
            "Your caravan will reform and slip away on the geoscape while the enemy force continues past.",
            () =>
            {
                CloseTacticalRetreatDialogs();
                ExecuteStealthDisengage(map, ambushComp);
            },
            destructive: false,
            title: "Quietly Disengage Scouts"
        ));
    }

    public static void ExecuteStealthDisengage(Map map, MapComponent_OverlandAmbush ambushComp)
    {
        CloseTacticalRetreatDialogs();

        if (map == null || !Find.Maps.Contains(map))
        {
            Log.Warning("[Project Vigilance] Cannot execute stealth disengage: tactical map is null or already removed.");
            return;
        }

        var sourceIncursion = ambushComp?.SourceIncursion;

        // Commit surviving tracked enemies to the world incursions
        if (ambushComp?.Engagements != null && ambushComp.Engagements.Count > 0)
        {
            foreach (var record in ambushComp.Engagements)
            {
                if (record.incursion != null && record.incursion.Spawned)
                {
                    var living = record.trackedPawns.Where(p => p != null && !p.Dead && !p.Destroyed).ToList();
                    record.incursion.SetCommittedPawns(living);
                    record.incursion.IsEngagedInTacticalMap = false;
                    record.incursion.AdvancePathTile();
                }
            }
        }
        else if (sourceIncursion != null && sourceIncursion.Spawned)
        {
            var livingEnemies = ambushComp != null ? ambushComp.GetLivingEnemies() : new List<Pawn>();
            sourceIncursion.SetCommittedPawns(livingEnemies);
            sourceIncursion.IsEngagedInTacticalMap = false;
            sourceIncursion.AdvancePathTile();
        }

        // Gather all player colonists and tamed animals
        var playerPawns = map.mapPawns.AllPawns
            .Where(p => p.Faction == Faction.OfPlayer && !p.Dead && p.Spawned)
            .ToList();

        if (playerPawns.Count == 0)
        {
            Log.Warning("[Project Vigilance] Stealth disengage found 0 surviving player pawns.");
            return;
        }

        // Form the reformed caravan on the world tile
        PlanetTile tile = map.Tile;
        Caravan caravan = CaravanMaker.MakeCaravan(playerPawns, Faction.OfPlayer, tile, addToWorldPawnsIfNotAlready: true);

        // Clean up encounter map
        CleanUpEncounterMap(map);

        if (caravan != null)
        {
            if (ambushComp?.Engagements != null && ambushComp.Engagements.Count > 0)
            {
                foreach (var record in ambushComp.Engagements)
                {
                    if (record.incursion != null && record.incursion.Spawned)
                    {
                        record.incursion.MarkCaravanMet(caravan.ID);
                        record.incursion.SetCaravanCooldown(caravan.ID, Find.TickManager.TicksGame + 15000);
                    }
                }
            }
            else if (sourceIncursion != null && sourceIncursion.Spawned)
            {
                sourceIncursion.MarkCaravanMet(caravan.ID);
                sourceIncursion.SetCaravanCooldown(caravan.ID, Find.TickManager.TicksGame + 15000);
            }
        }

        if (caravan != null)
        {
            CameraJumper.TryJumpAndSelect(caravan);
            Messages.Message(
                "Your scouting squad quietly withdrew from the sector undetected. The enemy force continues their overland march.",
                caravan,
                MessageTypeDefOf.PositiveEvent
            );
        }
    }

    public static void PromptCombatRetreat(Map map, MapComponent_OverlandAmbush? ambushComp)
    {
        if (map == null || !Find.Maps.Contains(map)) return;

        CloseTacticalRetreatDialogs();

        TaggedString text = "Order your strike team to disengage and retreat from this encounter?\n\n" +
                            "• Order Retreat to Edge: Conscious colonists will immediately break off combat and fall back to their closest map edge to escape.\n\n" +
                            "• Emergency Evacuation: Force immediate retreat for all mobile colonists. Downed colonists may be captured.";

        var dialog = new Dialog_MessageBox(
            text,
            "Order Retreat to Edge",
            () =>
            {
                CloseTacticalRetreatDialogs();
                OrderFallBackToEdge(map, ambushComp);
            },
            "Emergency Evacuation",
            () =>
            {
                CloseTacticalRetreatDialogs();
                ExecuteEmergencyEvacuation(map, ambushComp);
            },
            "Tactical Retreat"
        );
        dialog.buttonCText = "Cancel";
        dialog.buttonCAction = () => CloseTacticalRetreatDialogs();
        dialog.buttonCClose = true;

        Find.WindowStack.Add(dialog);
    }

    public static void OrderFallBackToEdge(Map map, MapComponent_OverlandAmbush? ambushComp)
    {
        CloseTacticalRetreatDialogs();

        if (map == null || !Find.Maps.Contains(map))
        {
            Log.Warning("[Project Vigilance] Cannot order retreat to edge: tactical map is null or already removed.");
            return;
        }

        var playerPawns = map.mapPawns.AllPawns
            .Where(p => p.Faction == Faction.OfPlayer && !p.Dead && !p.Downed && p.Spawned)
            .ToList();

        int retreatCount = 0;
        IntVec3 lastSpot = IntVec3.Invalid;

        for (int i = 0; i < playerPawns.Count; i++)
        {
            Pawn pawn = playerPawns[i];
            if (TryFindClosestExitSpotFor(pawn, out IntVec3 exitSpot))
            {
                if (pawn.drafter != null)
                {
                    pawn.drafter.Drafted = false;
                }

                Job job = JobMaker.MakeJob(JobDefOf.Goto, exitSpot);
                job.exitMapOnArrival = true;
                job.locomotionUrgency = LocomotionUrgency.Sprint;
                job.playerForced = true;

                pawn.jobs.ClearQueuedJobs();
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);

                retreatCount++;
                lastSpot = exitSpot;
            }
        }

        if (retreatCount > 0)
        {
            Messages.Message(
                $"Retreat ordered! {retreatCount} conscious squad member(s) are falling back to their closest map edges.",
                new TargetInfo(lastSpot, map),
                MessageTypeDefOf.ThreatSmall
            );
        }
        else
        {
            Messages.Message(
                "Retreat ordered, but no viable map edges could be reached by your colonists.",
                MessageTypeDefOf.RejectInput
            );
        }
    }

    public static bool TryFindClosestExitSpotFor(Pawn pawn, out IntVec3 exitSpot)
    {
        exitSpot = IntVec3.Invalid;
        if (pawn == null || !pawn.Spawned || pawn.Map == null)
        {
            return false;
        }

        Map map = pawn.Map;
        CellRect wholeMap = CellRect.WholeMap(map);

        // 1. Try engine native best exit spot (standard logic used by JobGiver_ExitMapBest)
        if (RCellFinder.TryFindBestExitSpot(pawn, out exitSpot, TraverseMode.ByPawn, canBash: false))
        {
            return true;
        }

        // 2. Direct projection to the mathematically closest map edge using native CellRect
        Rot4 closestEdge = wholeMap.GetClosestEdge(pawn.Position);
        IntVec3 projected = wholeMap.GetCellOnEdge(closestEdge, pawn.Position);
        if (IsReachableEdgeDestination(pawn, map, projected))
        {
            exitSpot = projected;
            return true;
        }

        // 3. Search near the projected spot along the edge boundary
        if (CellFinder.TryFindRandomEdgeCellNearWith(projected, 20f, map, c => IsReachableEdgeDestination(pawn, map, c), out exitSpot))
        {
            return true;
        }

        // 4. Try any reachable cell along the closest edge direction
        if (CellFinder.TryFindRandomEdgeCellWith(c => IsReachableEdgeDestination(pawn, map, c), map, closestEdge, 0f, out exitSpot))
        {
            return true;
        }

        // 5. Try closest reachable edge cell via region BFS traversal
        if (RCellFinder.TryFindClosestEdgeCellTo(pawn.Position, map, out exitSpot) &&
            IsReachableEdgeDestination(pawn, map, exitSpot))
        {
            return true;
        }

        // 6. Try any reachable edge cell anywhere on the map
        if (CellFinder.TryFindRandomEdgeCellWith(c => IsReachableEdgeDestination(pawn, map, c), map, 0f, out exitSpot))
        {
            return true;
        }

        // 7. Last-resort fallback to any walkable edge cell
        return CellFinder.TryFindRandomEdgeCellWith(c => c.InBounds(map) && c.Walkable(map) && !c.Fogged(map), map, 0f, out exitSpot);
    }

    private static bool IsReachableEdgeDestination(Pawn pawn, Map map, IntVec3 c)
    {
        return c.InBounds(map) &&
               c.Walkable(map) &&
               !c.Fogged(map) &&
               pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly);
    }

    public static void ExecuteEmergencyEvacuation(Map map, MapComponent_OverlandAmbush? ambushComp)
    {
        CloseTacticalRetreatDialogs();

        if (map == null || !Find.Maps.Contains(map))
        {
            Log.Warning("[Project Vigilance] Cannot execute emergency evacuation: tactical map is null or already removed.");
            return;
        }

        var allPlayerPawns = map.mapPawns.AllPawns
            .Where(p => p.Faction == Faction.OfPlayer && !p.Dead && p.Spawned)
            .ToList();

        var mobileColonists = allPlayerPawns.Where(p => !p.Downed).ToList();
        var downedColonists = allPlayerPawns.Where(p => p.Downed).ToList();

        var sourceIncursion = ambushComp?.SourceIncursion;
        if (ambushComp?.Engagements != null && ambushComp.Engagements.Count > 0)
        {
            bool captiveAssigned = false;
            foreach (var record in ambushComp.Engagements)
            {
                if (record.incursion != null && record.incursion.Spawned)
                {
                    var living = record.trackedPawns.Where(p => p != null && !p.Dead && !p.Destroyed).ToList();
                    record.incursion.SetCommittedPawns(living);
                    if (!captiveAssigned && downedColonists.Count > 0)
                    {
                        record.incursion.SetKidnappedColonists(downedColonists);
                        captiveAssigned = true;
                    }
                    record.incursion.IsEngagedInTacticalMap = false;
                }
            }
        }
        else if (sourceIncursion != null && sourceIncursion.Spawned)
        {
            var livingEnemies = ambushComp != null
                ? ambushComp.GetLivingEnemies()
                : map.mapPawns.AllPawns.Where(p => p.HostileTo(Faction.OfPlayer) && !p.Dead).ToList();

            sourceIncursion.SetCommittedPawns(livingEnemies);

            if (downedColonists.Count > 0)
            {
                sourceIncursion.SetKidnappedColonists(downedColonists);
            }

            sourceIncursion.IsEngagedInTacticalMap = false;
        }

        if (mobileColonists.Count == 0)
        {
            Messages.Message("No mobile colonists were able to evacuate the sector.", MessageTypeDefOf.NegativeEvent);
            return;
        }

        PlanetTile tile = map.Tile;
        Caravan caravan = CaravanMaker.MakeCaravan(mobileColonists, Faction.OfPlayer, tile, addToWorldPawnsIfNotAlready: true);

        CleanUpEncounterMap(map);

        if (ambushComp?.Engagements != null && ambushComp.Engagements.Count > 0)
        {
            foreach (var record in ambushComp.Engagements)
            {
                if (record.incursion != null && record.incursion.Spawned)
                {
                    record.incursion.IsEngagedInTacticalMap = false;
                    record.incursion.AdvancePathTile();
                    if (caravan != null)
                    {
                        record.incursion.MarkCaravanMet(caravan.ID);
                        record.incursion.SetCaravanCooldown(caravan.ID, Find.TickManager.TicksGame + 7500);
                    }
                }
            }
        }
        else if (sourceIncursion != null && sourceIncursion.Spawned)
        {
            sourceIncursion.IsEngagedInTacticalMap = false;
            sourceIncursion.AdvancePathTile();
            if (caravan != null)
            {
                sourceIncursion.MarkCaravanMet(caravan.ID);
                sourceIncursion.SetCaravanCooldown(caravan.ID, Find.TickManager.TicksGame + 7500);
            }
        }

        if (caravan != null)
        {
            CameraJumper.TryJumpAndSelect(caravan);
            Find.LetterStack.ReceiveLetter(
                "Tactical Withdrawal",
                $"Your forces successfully broke contact and evacuated to the geoscape.\n\n" +
                (downedColonists.Count > 0 ? $"{downedColonists.Count} downed colonist(s) could not be retrieved and were left behind.\n\n" : "") +
                "The caravan has been formed on the overland map.",
                LetterDefOf.NeutralEvent,
                caravan
            );
        }
    }

    public static void CleanUpEncounterMap(Map map)
    {
        if (map == null) return;

        var ambushComp = map.GetComponent<MapComponent_OverlandAmbush>();
        if (ambushComp != null)
        {
            if (ambushComp.Engagements != null)
            {
                foreach (var record in ambushComp.Engagements)
                {
                    if (record.incursion != null && record.incursion.Spawned)
                    {
                        record.incursion.IsEngagedInTacticalMap = false;
                    }
                }
            }
            if (ambushComp.SourceIncursion != null && ambushComp.SourceIncursion.Spawned)
            {
                ambushComp.SourceIncursion.IsEngagedInTacticalMap = false;
            }
        }

        MapParent? parent = map.Parent;
        if (parent != null && !parent.Destroyed)
        {
            parent.Destroy();
        }
    }
}
