using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

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
                    var living = record.trackedPawns
                        .Concat(record.escapedPawns ?? Enumerable.Empty<Pawn>())
                        .Where(p => p != null && !p.Dead && !p.Destroyed)
                        .Distinct()
                        .ToList();
                    DespawnAndPassHostilesToWorld(living);
                    record.incursion.SetCommittedPawns(living);
                    record.incursion.IsEngagedInTacticalMap = false;
                    record.incursion.AdvancePathTile();
                }
            }
        }
        else if (sourceIncursion != null && sourceIncursion.Spawned)
        {
            var livingEnemies = ambushComp != null ? ambushComp.GetLivingEnemies() : new List<Pawn>();
            DespawnAndPassHostilesToWorld(livingEnemies);
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

        // Form or join the player caravan on the world tile BEFORE passing pawns to WorldPawns.
        // Calling PassToWorld before Caravan.AddPawn / CaravanMaker.MakeCaravan causes Pawn.Notify_PassedToWorld
        // to see WorldPawnSituation.Free and reassign non-leader player colonists to a random foreign faction,
        // which then causes Caravan.AddPawn(ShouldAutoCapture) to capture them as prisoners of the player caravan.
        PlanetTile tile = map.Tile;
        Caravan? caravan = AddOrFormPlayerCaravan(playerPawns, tile);

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

        List<ThreatInfo> knownThreats = CollectKnownThreats(map);
        int retreatCount = 0;
        IntVec3 lastSpot = IntVec3.Invalid;

        for (int i = 0; i < playerPawns.Count; i++)
        {
            Pawn pawn = playerPawns[i];
            if (TryFindSafestExitSpotFor(pawn, knownThreats, out IntVec3 exitSpot))
            {
                if (pawn.drafter != null)
                {
                    pawn.drafter.Drafted = false;
                }
                if (!pawn.Awake())
                {
                    RestUtility.WakeUp(pawn);
                }

                Job job = JobMaker.MakeJob(JobDefOf.Goto, exitSpot);
                job.exitMapOnArrival = true;
                job.locomotionUrgency = LocomotionUrgency.Sprint;
                job.playerForced = true;

                pawn.jobs.ClearQueuedJobs();
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                JobTag tag = pawn.IsColonist ? JobTag.Misc : JobTag.JoiningCaravan;
                pawn.jobs.TryTakeOrderedJob(job, tag);

                retreatCount++;
                lastSpot = exitSpot;
            }
        }

        if (retreatCount > 0)
        {
            Messages.Message(
                knownThreats.Count > 0
                    ? $"Retreat ordered! {retreatCount} conscious squad member(s) are falling back along the safest available routes away from known threats."
                    : $"Retreat ordered! {retreatCount} conscious squad member(s) are falling back to their closest map edges.",
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

    public readonly struct ThreatInfo
    {
        public readonly IntVec3 Position;
        public readonly float DangerRadius;
        public readonly float Weight;

        public ThreatInfo(IntVec3 position, float dangerRadius, float weight)
        {
            Position = position;
            DangerRadius = dangerRadius;
            Weight = weight;
        }
    }

    public static List<ThreatInfo> CollectKnownThreats(Map? map)
    {
        var threats = new List<ThreatInfo>();
        if (map == null)
        {
            return threats;
        }

        var seenThings = new HashSet<int>();

        // 1. Check all spawned pawns on the map (raiders, mechanoids, insectoids, manhunters, entities)
        var allPawns = map.mapPawns?.AllPawnsSpawned;
        if (allPawns != null)
        {
            for (int i = 0; i < allPawns.Count; i++)
            {
                Pawn p = allPawns[i];
                if (p == null || !p.Spawned || p.Dead || p.Downed || p.Position.Fogged(map))
                {
                    continue;
                }

                if (!p.HostileTo(Faction.OfPlayer))
                {
                    continue;
                }

                if (p.IsPsychologicallyInvisible())
                {
                    continue;
                }

                seenThings.Add(p.thingIDNumber);

                float range = 24f;
                float weight = 1.0f;

                Verb? primaryVerb = p.equipment?.PrimaryEq?.PrimaryVerb;
                if (primaryVerb != null && !primaryVerb.IsMeleeAttack && primaryVerb.verbProps != null && primaryVerb.verbProps.range > 5f)
                {
                    range = Mathf.Clamp(primaryVerb.verbProps.range + 6f, 26f, 48f);
                    weight = 1.4f;
                }
                else if (p.RaceProps.IsMechanoid)
                {
                    range = 32f;
                    weight = 1.5f;
                }
                else if (p.RaceProps.Insect)
                {
                    range = 26f;
                    weight = 1.35f;
                }

                threats.Add(new ThreatInfo(p.Position, range, weight));
            }
        }

        // 2. Check hostile attack targets (turrets, hives, mech cluster defenses)
        var hostileTargets = map.attackTargetsCache?.TargetsHostileToColony;
        if (hostileTargets != null)
        {
            foreach (var target in hostileTargets)
            {
                if (target?.Thing == null || !target.Thing.Spawned || target.Thing.Destroyed)
                {
                    continue;
                }

                Thing t = target.Thing;
                if (seenThings.Contains(t.thingIDNumber) || t.Position.Fogged(map))
                {
                    continue;
                }

                if (GenHostility.IsActiveThreatToPlayer(target))
                {
                    seenThings.Add(t.thingIDNumber);
                    float range = t is Building_Turret ? 36f : 22f;
                    float weight = t is Building_Turret ? 1.5f : 1.1f;
                    threats.Add(new ThreatInfo(t.Position, range, weight));
                }
            }
        }

        return threats;
    }

    public static bool TryFindClosestExitSpotFor(Pawn pawn, out IntVec3 exitSpot)
    {
        if (pawn == null || !pawn.Spawned || pawn.Map == null)
        {
            exitSpot = IntVec3.Invalid;
            return false;
        }

        List<ThreatInfo> threats = CollectKnownThreats(pawn.Map);
        return TryFindSafestExitSpotFor(pawn, threats, out exitSpot);
    }

    public static bool TryFindSafestExitSpotFor(Pawn pawn, List<ThreatInfo>? knownThreats, out IntVec3 exitSpot)
    {
        exitSpot = IntVec3.Invalid;
        if (pawn == null || !pawn.Spawned || pawn.Map == null)
        {
            return false;
        }

        Map map = pawn.Map;
        CellRect wholeMap = CellRect.WholeMap(map);

        // If there are active known threats on the map, evaluate candidate exit cells across all 4 edges
        // and select the route that minimizes exposure to hostile raiders, insectoids, mechanoids, and turrets.
        if (knownThreats != null && knownThreats.Count > 0)
        {
            List<IntVec3> candidates = GatherCandidateExitCells(pawn, map, wholeMap);
            if (candidates.Count > 0)
            {
                // Stage 1: Score all candidates by geometric corridor safety + exit safety + distance
                var scoredCandidates = new List<(IntVec3 Cell, float HeuristicCost)>(candidates.Count);
                for (int i = 0; i < candidates.Count; i++)
                {
                    IntVec3 c = candidates[i];
                    float cost = EvaluateExitCandidateHeuristic(pawn.Position, c, knownThreats);
                    scoredCandidates.Add((c, cost));
                }

                scoredCandidates.Sort((a, b) => a.HeuristicCost.CompareTo(b.HeuristicCost));

                // Stage 2: Verify the actual walked PawnPath for the top candidates so walls/doors/caves
                // don't funnel the retreating pawn through a room or chokepoint containing threats.
                int verifyCount = Mathf.Min(6, scoredCandidates.Count);
                IntVec3 bestCell = scoredCandidates[0].Cell;
                float bestTotalCost = float.MaxValue;

                for (int i = 0; i < verifyCount; i++)
                {
                    IntVec3 candidate = scoredCandidates[i].Cell;
                    float totalCost = EvaluateActualPathCost(pawn, map, candidate, knownThreats, scoredCandidates[i].HeuristicCost);
                    if (totalCost < bestTotalCost)
                    {
                        bestTotalCost = totalCost;
                        bestCell = candidate;
                    }
                }

                exitSpot = bestCell;
                return true;
            }
        }

        // Fallback when no threats are known or no perimeter samples matched: closest reachable exit
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

    private static List<IntVec3> GatherCandidateExitCells(Pawn pawn, Map map, CellRect wholeMap)
    {
        var candidates = new List<IntVec3>(48);

        void TryAddCandidate(IntVec3 cell)
        {
            if (!IsReachableEdgeDestination(pawn, map, cell))
            {
                return;
            }
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].InHorDistOf(cell, 6f))
                {
                    return;
                }
            }
            candidates.Add(cell);
        }

        // 1. Direct projections of pawn's position onto all 4 edges (North, East, South, West)
        for (int dir = 0; dir < 4; dir++)
        {
            Rot4 edge = new Rot4(dir);
            IntVec3 proj = wholeMap.GetCellOnEdge(edge, pawn.Position);
            if (IsReachableEdgeDestination(pawn, map, proj))
            {
                TryAddCandidate(proj);
            }
            else if (CellFinder.TryFindRandomEdgeCellNearWith(proj, 12f, map, c => IsReachableEdgeDestination(pawn, map, c), out IntVec3 nearProj))
            {
                TryAddCandidate(nearProj);
            }
        }

        // 2. Native closest exit spot as a candidate
        if (RCellFinder.TryFindBestExitSpot(pawn, out IntVec3 nativeBest, TraverseMode.ByPawn, canBash: false))
        {
            TryAddCandidate(nativeBest);
        }

        // 3. Systematic perimeter sampling along all 4 map edges
        int stepX = Mathf.Max(10, map.Size.x / 14);
        int stepZ = Mathf.Max(10, map.Size.z / 14);

        for (int x = 4; x < map.Size.x - 4; x += stepX)
        {
            TryAddEdgeOrNearby(pawn, map, new IntVec3(x, 0, 0), candidates, TryAddCandidate);
            TryAddEdgeOrNearby(pawn, map, new IntVec3(x, 0, map.Size.z - 1), candidates, TryAddCandidate);
        }

        for (int z = 4; z < map.Size.z - 4; z += stepZ)
        {
            TryAddEdgeOrNearby(pawn, map, new IntVec3(0, 0, z), candidates, TryAddCandidate);
            TryAddEdgeOrNearby(pawn, map, new IntVec3(map.Size.x - 1, 0, z), candidates, TryAddCandidate);
        }

        return candidates;
    }

    private static void TryAddEdgeOrNearby(Pawn pawn, Map map, IntVec3 edgeCell, List<IntVec3> candidates, Action<IntVec3> addAction)
    {
        if (IsReachableEdgeDestination(pawn, map, edgeCell))
        {
            addAction(edgeCell);
            return;
        }

        // Check a few cells along the edge if the exact sample point is blocked by rock/water
        for (int offset = -4; offset <= 4; offset += 2)
        {
            if (offset == 0) continue;
            IntVec3 alt = (edgeCell.z == 0 || edgeCell.z == map.Size.z - 1)
                ? new IntVec3(Mathf.Clamp(edgeCell.x + offset, 0, map.Size.x - 1), 0, edgeCell.z)
                : new IntVec3(edgeCell.x, 0, Mathf.Clamp(edgeCell.z + offset, 0, map.Size.z - 1));

            if (IsReachableEdgeDestination(pawn, map, alt))
            {
                addAction(alt);
                return;
            }
        }
    }

    private static float EvaluateExitCandidateHeuristic(IntVec3 start, IntVec3 exit, List<ThreatInfo> threats)
    {
        Vector2 startV = new Vector2(start.x, start.z);
        Vector2 exitV = new Vector2(exit.x, exit.z);
        Vector2 seg = exitV - startV;
        float dist = seg.magnitude;
        Vector2 dir = dist > 0.001f ? seg / dist : Vector2.zero;

        // Base cost is travel distance (1.0 per cell)
        float cost = dist;

        for (int i = 0; i < threats.Count; i++)
        {
            ThreatInfo threat = threats[i];
            Vector2 tv = new Vector2(threat.Position.x, threat.Position.z);

            // 1. Penalty if the exit cell itself is near a threat
            float exitToThreat = Vector2.Distance(exitV, tv);
            float safeExitRadius = threat.DangerRadius + 10f;
            if (exitToThreat < safeExitRadius)
            {
                float penetration = safeExitRadius - exitToThreat;
                cost += penetration * penetration * 12f * threat.Weight;
            }

            // 2. Penalty if the straight-line corridor from start to exit passes within the threat's danger zone
            float segLenSq = seg.sqrMagnitude;
            float t = segLenSq > 0.001f ? Mathf.Clamp01(Vector2.Dot(tv - startV, seg) / segLenSq) : 0f;
            Vector2 closestOnSeg = startV + t * seg;
            float corridorDist = Vector2.Distance(tv, closestOnSeg);

            if (corridorDist < threat.DangerRadius)
            {
                float penetration = threat.DangerRadius - corridorDist;
                // Weight forward corridor encounters more heavily than threats already right behind the pawn at t=0
                float forwardFactor = Mathf.Lerp(0.35f, 1.0f, Mathf.Clamp01(t * 4f));
                cost += penetration * penetration * 18f * threat.Weight * forwardFactor;
            }

            // 3. Directional penalty for running toward a nearby threat
            Vector2 toThreat = tv - startV;
            float pawnToThreatDist = toThreat.magnitude;
            if (pawnToThreatDist > 0.5f && pawnToThreatDist < dist + 15f)
            {
                float alignment = Vector2.Dot(dir, toThreat / pawnToThreatDist);
                if (alignment > 0.3f && pawnToThreatDist < threat.DangerRadius + 25f)
                {
                    cost += alignment * 1200f * threat.Weight * Mathf.Clamp01(1f - (pawnToThreatDist / (threat.DangerRadius + 40f)));
                }
            }
        }

        return cost;
    }

    private static float EvaluateActualPathCost(Pawn pawn, Map map, IntVec3 exit, List<ThreatInfo> threats, float fallbackHeuristicCost)
    {
        if (map.pathFinder == null)
        {
            return fallbackHeuristicCost;
        }

        using PawnPath path = map.pathFinder.FindPathNow(pawn.Position, exit, pawn, null, PathEndMode.OnCell);
        if (path == null || !path.Found || path.NodesReversed == null || path.NodesReversed.Count == 0)
        {
            return fallbackHeuristicCost + 5000f;
        }

        var nodes = path.NodesReversed;
        int count = nodes.Count;
        float cost = count; // Actual walked path length in cells

        // Sample nodes along the actual path (from pawn toward exit; NodesReversed has destination at index 0 and start at count-1)
        int step = Mathf.Max(1, count / 30);
        for (int i = 0; i < threats.Count; i++)
        {
            ThreatInfo threat = threats[i];
            float minDistSq = float.MaxValue;
            IntVec3 closestNode = exit;

            for (int n = 0; n < count - 2; n += step)
            {
                IntVec3 node = nodes[n];
                float dSq = (node - threat.Position).LengthHorizontalSquared;
                if (dSq < minDistSq)
                {
                    minDistSq = dSq;
                    closestNode = node;
                }
            }

            float minDist = Mathf.Sqrt(minDistSq);
            if (minDist < threat.DangerRadius)
            {
                float penetration = threat.DangerRadius - minDist;
                float losMultiplier = (minDist < 12f || GenSight.LineOfSight(threat.Position, closestNode, map, skipFirstCell: true))
                    ? 1.5f
                    : 0.5f;
                cost += penetration * penetration * 22f * threat.Weight * losMultiplier;
            }
        }

        return cost;
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
                    var living = record.trackedPawns
                        .Concat(record.escapedPawns ?? Enumerable.Empty<Pawn>())
                        .Where(p => p != null && !p.Dead && !p.Destroyed)
                        .Distinct()
                        .ToList();
                    DespawnAndPassHostilesToWorld(living);
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

            DespawnAndPassHostilesToWorld(livingEnemies);
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

        if (downedColonists.Count > 0)
        {
            PassCapturedColonistsToWorld(downedColonists);
        }

        PlanetTile tile = map.Tile;
        Caravan? caravan = AddOrFormPlayerCaravan(mobileColonists, tile);

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

        // Never destroy the MapParent of a permanent player home settlement when retreating;
        // only destroy temporary encounter / battlefield MapParents.
        if (!map.IsPlayerHome)
        {
            MapParent? parent = map.Parent;
            if (parent != null && !parent.Destroyed)
            {
                parent.Destroy();
            }
        }
    }

    private static void DetachFromLord(Pawn p)
    {
        if (p == null)
        {
            return;
        }

        Lord? lord = p.GetLord();
        if (lord != null)
        {
            Map? lordMap = lord.Map ?? p.Map;
            lord.RemovePawn(p);
            if (lord.ownedPawns.Count == 0 && lordMap?.lordManager != null && lordMap.lordManager.lords.Contains(lord))
            {
                lordMap.lordManager.RemoveLord(lord);
            }
        }
    }

    public static void DespawnAndPassHostilesToWorld(IReadOnlyList<Pawn> hostiles)
    {
        if (hostiles == null)
        {
            return;
        }

        for (int i = 0; i < hostiles.Count; i++)
        {
            Pawn p = hostiles[i];
            if (p == null || p.Dead || p.Destroyed)
            {
                continue;
            }

            DetachFromLord(p);
            if (p.Spawned)
            {
                p.DeSpawn(DestroyMode.Vanish);
            }
            if (!Find.WorldPawns.Contains(p))
            {
                Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
            }
            Find.WorldPawns.ForcefullyKeptPawns.Add(p);
        }
    }

    /// <summary>
    /// Safely adds retreating player pawns to an existing player caravan at <paramref name="tile"/> or forms a new one.
    /// IMPORTANT: Pawns MUST be added to the Caravan BEFORE calling WorldPawns.PassToWorld.
    /// If PassToWorld is called while a non-leader colonist is not yet a CaravanMember, Pawn.Notify_PassedToWorld
    /// treats them as WorldPawnSituation.Free (banished/abandoned) and reassigns their Faction to a random non-colony
    /// humanlike faction, which then causes Caravan.AddPawn(ShouldAutoCapture) to capture them as prisoners.
    /// </summary>
    public static Caravan? AddOrFormPlayerCaravan(IReadOnlyList<Pawn> pawns, PlanetTile tile, Caravan? existingCaravan = null, bool wakeRemainingAnimalsOnMap = false)
    {
        if (pawns == null || pawns.Count == 0)
        {
            return existingCaravan;
        }

        Map? sourceMap = null;
        var validPawns = new List<Pawn>(pawns.Count);
        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn p = pawns[i];
            if (p == null || p.Dead)
            {
                continue;
            }

            sourceMap ??= p.Map;

            if (p.Faction != Faction.OfPlayer)
            {
                p.SetFaction(Faction.OfPlayer);
            }
            if (p.guest != null && (p.guest.HostFaction != null || p.guest.IsPrisoner || p.guest.IsSlave))
            {
                p.guest.SetGuestStatus(null, GuestStatus.Guest);
            }
            DetachFromLord(p);
            if (p.Spawned)
            {
                p.DeSpawn(DestroyMode.Vanish);
            }

            validPawns.Add(p);
        }

        if (validPawns.Count == 0)
        {
            return existingCaravan;
        }

        Caravan? caravan = existingCaravan ?? Find.WorldObjects?.Caravans?.FirstOrDefault(c => c.Tile == tile && c.Faction == Faction.OfPlayer);
        if (caravan != null)
        {
            caravan.autoJoinable = true;
            for (int i = 0; i < validPawns.Count; i++)
            {
                Pawn p = validPawns[i];
                if (!caravan.ContainsPawn(p))
                {
                    caravan.AddPawn(p, addCarriedPawnToWorldPawnsIfAny: true);
                }
                if (!Find.WorldPawns.Contains(p))
                {
                    Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
                }
            }
        }
        else
        {
            caravan = CaravanMaker.MakeCaravan(validPawns, Faction.OfPlayer, tile, addToWorldPawnsIfNotAlready: true);
            if (caravan != null)
            {
                caravan.autoJoinable = true;
            }
        }

        // Post-condition check: ensure Notify_PassedToWorld or any hook never left a player pawn in a foreign faction or prisoner state
        for (int i = 0; i < validPawns.Count; i++)
        {
            Pawn p = validPawns[i];
            if (p.Faction != Faction.OfPlayer)
            {
                p.SetFaction(Faction.OfPlayer);
            }
            if (p.guest != null && (p.guest.HostFaction != null || p.guest.IsPrisoner || p.guest.IsSlave))
            {
                p.guest.SetGuestStatus(null, GuestStatus.Guest);
            }
        }

        if (wakeRemainingAnimalsOnMap && sourceMap != null)
        {
            var spawnedPawns = sourceMap.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < spawnedPawns.Count; i++)
            {
                Pawn p = spawnedPawns[i];
                if (p != null && !p.Downed && !p.Drafted && !p.IsColonist && p.Faction == Faction.OfPlayer)
                {
                    if (p.IsAnimal)
                    {
                        RestUtility.WakeUp(p);
                    }
                    p.jobs?.CheckForJobOverride();
                }
            }
        }

        return caravan;
    }

    /// <summary>
    /// Safely despawns and passes captured/abducted player colonists to WorldPawns while preserving Faction.OfPlayer.
    /// </summary>
    public static void PassCapturedColonistsToWorld(IReadOnlyList<Pawn> capturedColonists)
    {
        if (capturedColonists == null)
        {
            return;
        }

        for (int i = 0; i < capturedColonists.Count; i++)
        {
            Pawn p = capturedColonists[i];
            if (p == null || p.Destroyed)
            {
                continue;
            }

            DetachFromLord(p);
            if (p.Spawned)
            {
                p.DeSpawn(DestroyMode.Vanish);
            }
            if (!Find.WorldPawns.Contains(p))
            {
                Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
            }
            Find.WorldPawns.ForcefullyKeptPawns.Add(p);
            if (p.Faction != Faction.OfPlayer)
            {
                p.SetFaction(Faction.OfPlayer);
            }
        }
    }
}
