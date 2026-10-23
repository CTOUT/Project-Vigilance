using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace ProjectVigilance.Geoscape.World;

public enum TransitThreatReaction
{
    HostileAssault,
    HostileEvade,
    NeutralWideBerth
}

public class LordToil_IncursionTransit : LordToil_Travel
{
    public LordToil_IncursionTransit(IntVec3 dest) : base(dest)
    {
    }

    public override void UpdateAllDuties()
    {
        LordToilData_Travel data = Data;
        for (int i = 0; i < lord.ownedPawns.Count; i++)
        {
            Pawn p = lord.ownedPawns[i];
            if (p != null)
            {
                PawnDuty duty = new PawnDuty(DutyDefOf.TravelOrLeave, data.dest, -1f)
                {
                    locomotion = LocomotionUrgency.Walk,
                    maxDanger = maxDanger,
                    canDig = false
                };
                p.mindState.duty = duty;
            }
        }
    }
}

public class Trigger_IncursionThreatDetection : Trigger
{
    private readonly LordJob_IncursionTransit job;
    private readonly TransitThreatReaction reaction;

    public Trigger_IncursionThreatDetection(LordJob_IncursionTransit job, TransitThreatReaction reaction)
    {
        this.job = job;
        this.reaction = reaction;
    }

    public override bool ActivateOn(Lord lord, TriggerSignal signal)
    {
        if (job == null || lord == null || lord.Map == null)
        {
            return false;
        }

        bool threatTriggered = false;

        // 1. Memo received from acoustic sensor or sight detection
        if (signal.type == TriggerSignalType.Memo && signal.memo == "IncursionThreatSpotted")
        {
            threatTriggered = true;
        }
        // 2. Direct damage/harm sustained by pawns in this lord
        else if (Trigger_PawnHarmed.SignalIsHarm(signal))
        {
            threatTriggered = true;
        }
        // 3. Periodic tactical check (every 30 ticks = 0.5s)
        else if (signal.type == TriggerSignalType.Tick && Find.TickManager.TicksGame % 30 == 0)
        {
            threatTriggered = CheckSensoryDetection(lord);
        }

        if (!threatTriggered)
        {
            return false;
        }

        // Threat confirmed; evaluate reaction mode
        if (reaction == TransitThreatReaction.NeutralWideBerth)
        {
            return true;
        }

        float ratio = LordJob_IncursionTransit.CalculateCombatRatio(lord, lord.Map);
        if (reaction == TransitThreatReaction.HostileAssault)
        {
            return ratio >= 0.75f;
        }
        else if (reaction == TransitThreatReaction.HostileEvade)
        {
            return ratio < 0.75f;
        }

        return false;
    }

    private static bool CheckSensoryDetection(Lord lord)
    {
        Map map = lord.Map;
        if (map == null) return false;

        var ambush = map.GetComponent<MapComponent_OverlandAmbush>();

        // Ongoing firefight active on map
        if (ambush != null && ambush.AmbushSprung)
        {
            return true;
        }

        // Recent gunfire / explosion within hearing distance
        if (ambush != null && ambush.LastCombatNoiseTick > 0 && (Find.TickManager.TicksGame - ambush.LastCombatNoiseTick) < 300)
        {
            IntVec3 noisePos = ambush.LastCombatNoisePos;
            if (noisePos.IsValid)
            {
                for (int i = 0; i < lord.ownedPawns.Count; i++)
                {
                    Pawn p = lord.ownedPawns[i];
                    if (p != null && p.Spawned && !p.Dead && p.Position.InHorDistOf(noisePos, 50f))
                    {
                        return true;
                    }
                }
            }
        }

        // Visual Line of Sight and Proximity to Conscious Player Colonists
        var colonists = map.mapPawns?.FreeColonistsSpawned;
        if (colonists != null)
        {
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn incursionPawn = lord.ownedPawns[i];
                if (incursionPawn == null || !incursionPawn.Spawned || incursionPawn.Dead || incursionPawn.Downed)
                {
                    continue;
                }

                for (int j = 0; j < colonists.Count; j++)
                {
                    Pawn col = colonists[j];
                    if (col == null || !col.Spawned || col.Dead || col.Downed)
                    {
                        continue;
                    }

                    float distSq = incursionPawn.Position.DistanceToSquared(col.Position);

                    // Close proximity (~8 cells) detects regardless of LOS (footsteps/gear rustling)
                    if (distSq <= 64f)
                    {
                        return true;
                    }

                    // Medium proximity (~26 cells) with unobstructed Line of Sight
                    if (distSq <= 676f)
                    {
                        if (GenSight.LineOfSight(incursionPawn.Position, col.Position, map))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }
}

public class LordJob_IncursionTransit : LordJob
{
    private WorldObject_Incursion? incursion;
    private Faction? faction;
    private IntVec3 exitCell;
    private bool isHostile;
    private bool threatDetected;
    private int spawnTick;

    public WorldObject_Incursion? Incursion => incursion;
    public Faction? Faction => faction;
    public IntVec3 ExitCell => exitCell;
    public bool IsHostile => isHostile;
    public bool ThreatDetected => threatDetected;

    public LordJob_IncursionTransit()
    {
    }

    public LordJob_IncursionTransit(WorldObject_Incursion inc, Faction fac, IntVec3 exit, bool hostile)
    {
        incursion = inc;
        faction = fac;
        exitCell = exit;
        isHostile = hostile;
        spawnTick = Find.TickManager?.TicksGame ?? 0;
    }

    public void Notify_GunfireHeard(IntVec3 pos, float radius)
    {
        if (threatDetected || lord == null) return;

        for (int i = 0; i < lord.ownedPawns.Count; i++)
        {
            Pawn p = lord.ownedPawns[i];
            if (p != null && p.Spawned && !p.Dead && p.Position.InHorDistOf(pos, radius))
            {
                threatDetected = true;
                lord.ReceiveMemo("IncursionThreatSpotted");
                break;
            }
        }
    }

    public static float CalculateCombatRatio(Lord lord, Map map)
    {
        float incursionPower = 0f;
        for (int i = 0; i < lord.ownedPawns.Count; i++)
        {
            Pawn p = lord.ownedPawns[i];
            if (p != null && p.Spawned && !p.Dead && !p.Downed)
            {
                float basePower = p.kindDef?.combatPower ?? 100f;
                float health = p.health?.summaryHealth?.SummaryHealthPercent ?? 1f;
                incursionPower += basePower * Mathf.Clamp01(health);
            }
        }

        float colonistPower = 0f;
        var colonists = map.mapPawns?.FreeColonistsSpawned;
        if (colonists != null)
        {
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn c = colonists[i];
                if (c != null && c.Spawned && !c.Dead && !c.Downed)
                {
                    float basePower = c.kindDef?.combatPower ?? 100f;
                    float health = c.health?.summaryHealth?.SummaryHealthPercent ?? 1f;
                    colonistPower += basePower * Mathf.Clamp01(health);
                }
            }
        }

        if (colonistPower <= 1f)
        {
            return 2.0f; // No effective player defenders; confident
        }

        return incursionPower / colonistPower;
    }

    private void MarkIncursionCombatEngagement()
    {
        if (Map == null || incursion == null) return;
        var ambush = Map.GetComponent<MapComponent_OverlandAmbush>();
        if (ambush != null)
        {
            var record = ambush.Engagements.FirstOrDefault(e => e.incursion == incursion);
            if (record != null)
            {
                record.wasEngagedInCombat = true;
            }
            if (isHostile)
            {
                ambush.SprungAmbush("Hostile Patrol Engagement");
            }
        }
    }

    public override StateGraph CreateGraph()
    {
        StateGraph stateGraph = new StateGraph();

        // 1. Initial Transit Toil (quiet march towards exitCell)
        LordToil_IncursionTransit transitToil = new LordToil_IncursionTransit(exitCell);
        stateGraph.StartingToil = transitToil;

        // Exit toil when normal peaceful arrival at exit cell occurs
        LordToil_ExitMap peacefulExitToil = new LordToil_ExitMap(LocomotionUrgency.None, false, false);
        stateGraph.AddToil(peacefulExitToil);

        Transition arrivedTransition = new Transition(transitToil, peacefulExitToil, false, true);
        arrivedTransition.AddTrigger(new Trigger_Memo("TravelArrived"));
        stateGraph.AddTransition(arrivedTransition);

        if (isHostile)
        {
            // 2. Assault Toil (confident attack)
            LordToil_AssaultColony assaultToil = new LordToil_AssaultColony(false, false);
            stateGraph.AddToil(assaultToil);

            // 3. Evasive Exit Toil (intimidated withdrawal)
            LordToil_ExitMap evadingExitToil = new LordToil_ExitMap(LocomotionUrgency.Jog, canDig: false, interruptCurrentJob: true)
            {
                useAvoidGrid = true
            };
            stateGraph.AddToil(evadingExitToil);

            // Transition: Transit -> Assault (Confident)
            Transition assaultTransition = new Transition(transitToil, assaultToil, false, true);
            assaultTransition.AddTrigger(new Trigger_IncursionThreatDetection(this, TransitThreatReaction.HostileAssault));
            assaultTransition.AddPreAction(new TransitionAction_Custom(() =>
            {
                threatDetected = true;
                MarkIncursionCombatEngagement();
                Messages.Message($"Hostile Patrol Alerted: {faction?.Name ?? "Enemy forces"} spotted your squad and launched an immediate assault!", new TargetInfo(lord.ownedPawns.FirstOrDefault()?.Position ?? IntVec3.Invalid, Map), MessageTypeDefOf.ThreatBig);
                LetterDefOf.ThreatBig.arriveSound?.PlayOneShotOnCamera();
            }));
            stateGraph.AddTransition(assaultTransition);

            // Transition: Transit -> Evade (Intimidated)
            Transition evadeTransition = new Transition(transitToil, evadingExitToil, false, true);
            evadeTransition.AddTrigger(new Trigger_IncursionThreatDetection(this, TransitThreatReaction.HostileEvade));
            evadeTransition.AddPreAction(new TransitionAction_Custom(() =>
            {
                threatDetected = true;
                Messages.Message($"Hostile Patrol Evading: {faction?.Name ?? "Enemy forces"} detected your presence, but assessing their weakened numbers, they are hastening towards the perimeter!", new TargetInfo(lord.ownedPawns.FirstOrDefault()?.Position ?? IntVec3.Invalid, Map), MessageTypeDefOf.NeutralEvent);
            }));
            stateGraph.AddTransition(evadeTransition);
        }
        else
        {
            // Neutrals / Traders: Wide-berth evasion upon danger
            LordToil_ExitMap wideBerthExitToil = new LordToil_ExitMap(LocomotionUrgency.Jog, canDig: false, interruptCurrentJob: true)
            {
                useAvoidGrid = true
            };
            stateGraph.AddToil(wideBerthExitToil);

            Transition dangerTransition = new Transition(transitToil, wideBerthExitToil, false, true);
            dangerTransition.AddTrigger(new Trigger_IncursionThreatDetection(this, TransitThreatReaction.NeutralWideBerth));
            dangerTransition.AddPreAction(new TransitionAction_Custom(() =>
            {
                threatDetected = true;
                Messages.Message($"Gunfire ahead! {faction?.Name ?? "Travellers"} have detected danger, giving the combat zone a wide berth as they hurry toward the exit!", new TargetInfo(lord.ownedPawns.FirstOrDefault()?.Position ?? IntVec3.Invalid, Map), MessageTypeDefOf.NeutralEvent);
            }));
            stateGraph.AddTransition(dangerTransition);

            // If directly attacked / harmed, defend themselves
            LordToil_ExitMapTraderFighting defendFightingToil = new LordToil_ExitMapTraderFighting();
            stateGraph.AddToil(defendFightingToil);

            Transition harmTransition = new Transition(transitToil, defendFightingToil, false, true);
            harmTransition.AddSource(wideBerthExitToil);
            harmTransition.AddTrigger(new Trigger_PawnHarmed(1f, false, null, null, null));
            harmTransition.AddPreAction(new TransitionAction_Custom(() =>
            {
                threatDetected = true;
                MarkIncursionCombatEngagement();
            }));
            stateGraph.AddTransition(harmTransition);
        }

        return stateGraph;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref incursion, "incursion");
        Scribe_References.Look(ref faction, "faction");
        Scribe_Values.Look(ref exitCell, "exitCell");
        Scribe_Values.Look(ref isHostile, "isHostile");
        Scribe_Values.Look(ref threatDetected, "threatDetected");
        Scribe_Values.Look(ref spawnTick, "spawnTick");
    }
}
