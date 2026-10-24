using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.World;

public class IncursionEngagementRecord : IExposable
{
    public WorldObject_Incursion? incursion;
    public int trackingId;
    public Faction? faction;
    public string contactLabel = "Hostile Warband";
    public List<Pawn> trackedPawns = new();
    public List<Pawn> escapedPawns = new();
    public List<Pawn> capturedColonists = new();
    public bool isDisengaged;
    public bool isTransitOnly;
    public bool wasEngagedInCombat;

    public IncursionEngagementRecord() { }

    public IncursionEngagementRecord(WorldObject_Incursion inc, IEnumerable<Pawn>? pawns)
    {
        incursion = inc;
        trackingId = inc.TrackingId;
        faction = inc.Faction;
        contactLabel = inc.LabelCap;
        trackedPawns = pawns != null ? pawns.Where(p => p != null).Distinct().ToList() : new List<Pawn>();
        isDisengaged = false;
    }

    public void ExposeData()
    {
        Scribe_References.Look(ref incursion, "incursion");
        Scribe_Values.Look(ref trackingId, "trackingId", 0);
        Scribe_References.Look(ref faction, "faction");
        Scribe_Values.Look(ref contactLabel, "contactLabel", "Hostile Warband");
        Scribe_Collections.Look(ref trackedPawns, "trackedPawns", LookMode.Reference);
        Scribe_Collections.Look(ref escapedPawns, "escapedPawns", LookMode.Reference);
        Scribe_Collections.Look(ref capturedColonists, "capturedColonists", LookMode.Reference);
        Scribe_Values.Look(ref isDisengaged, "isDisengaged", false);
        Scribe_Values.Look(ref isTransitOnly, "isTransitOnly", false);
        Scribe_Values.Look(ref wasEngagedInCombat, "wasEngagedInCombat", false);
    }
}

public class GuestCohortRecord : IExposable
{
    public string groupLabel = "Visiting Party";
    public Faction? faction;
    public List<Pawn> pawns = new();
    public bool hasDeparted;

    public GuestCohortRecord() { }

    public GuestCohortRecord(string label, Faction? f, IEnumerable<Pawn> cohortPawns)
    {
        groupLabel = label;
        faction = f;
        pawns = cohortPawns != null ? cohortPawns.Where(p => p != null).Distinct().ToList() : new List<Pawn>();
        hasDeparted = false;
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref groupLabel, "groupLabel", "Visiting Party");
        Scribe_References.Look(ref faction, "faction");
        Scribe_Collections.Look(ref pawns, "pawns", LookMode.Reference);
        Scribe_Values.Look(ref hasDeparted, "hasDeparted", false);
    }
}

public class MapComponent_OverlandAmbush : MapComponent
{
    private bool isActive;
    private bool ambushSprung;
    private Faction? enemyFaction;
    private string contactName = "Hostile Warband";
    private string factionName = "enemy forces";
    private List<Pawn> trackedEnemies = new();
    private List<Pawn> allEnemies = new();
    private Lord? travelLord;
    private WorldObject_Incursion? sourceIncursion;
    private List<Pawn> escapedHostiles = new();
    private List<Pawn> capturedColonists = new();
    private List<IncursionEngagementRecord> engagements = new();
    private List<GuestCohortRecord> guestCohorts = new();
    private int lastCombatNoiseTick = -99999;
    private IntVec3 lastCombatNoisePos = IntVec3.Invalid;

    public int LastCombatNoiseTick => lastCombatNoiseTick;
    public IntVec3 LastCombatNoisePos => lastCombatNoisePos;

    public MapComponent_OverlandAmbush(Map map) : base(map)
    {
    }

    public bool IsActive => isActive;
    public bool AmbushSprung => ambushSprung;
    public List<IncursionEngagementRecord> Engagements => engagements;
    public List<GuestCohortRecord> GuestCohorts => guestCohorts;
    public WorldObject_Incursion? SourceIncursion => engagements.FirstOrDefault(e => !e.isDisengaged)?.incursion ?? sourceIncursion;
    public string ContactName => contactName;
    public string FactionName => factionName;
    public List<Pawn> GetLivingEnemies() => allEnemies.Where(p => p != null && !p.Dead && !p.Destroyed).ToList();
    public List<Pawn> EscapedHostiles => escapedHostiles;
    public List<Pawn> CapturedColonists => capturedColonists;

    public void RegisterGuestCohort(string label, Faction? f, IEnumerable<Pawn> cohortPawns)
    {
        if (cohortPawns == null) return;
        var list = cohortPawns.Where(p => p != null).Distinct().ToList();
        if (list.Count == 0) return;
        guestCohorts.Add(new GuestCohortRecord(label, f, list));
        Log.Message($"[Project Vigilance] Registered independent guest cohort '{label}' ({list.Count} pawns, Faction: {f?.Name ?? "Neutral"}) to overland map.");
    }

    public bool HasTrackedIncursionFor(Pawn raider)
    {
        if (raider == null) return false;
        return engagements.Any(e => !e.isDisengaged && e.trackedPawns.Contains(raider)) ||
               (sourceIncursion != null && allEnemies.Contains(raider));
    }

    public bool HasTrackedIncursion(WorldObject_Incursion inc)
    {
        if (inc == null) return false;
        return engagements.Any(e => !e.isDisengaged && e.incursion == inc) ||
               (sourceIncursion == inc && !engagements.Any(e => e.incursion == inc && e.isDisengaged));
    }

    public void RegisterIncursion(WorldObject_Incursion inc, IEnumerable<Pawn>? enemies, bool markAmbushSprung = true)
    {
        if (inc == null) return;

        inc.IsEngagedInTacticalMap = true;

        var existingRecord = engagements.FirstOrDefault(e => e.incursion == inc);
        if (existingRecord != null)
        {
            if (enemies != null)
            {
                foreach (var p in enemies)
                {
                    if (p != null && !existingRecord.trackedPawns.Contains(p))
                    {
                        existingRecord.trackedPawns.Add(p);
                    }
                }
            }
            existingRecord.isDisengaged = false;
        }
        else
        {
            var record = new IncursionEngagementRecord(inc, enemies);
            engagements.Add(record);
        }

        if (enemies != null)
        {
            foreach (var p in enemies)
            {
                if (p != null)
                {
                    if (!allEnemies.Contains(p)) allEnemies.Add(p);
                    if (!trackedEnemies.Contains(p)) trackedEnemies.Add(p);
                }
            }
        }

        if (sourceIncursion == null)
        {
            sourceIncursion = inc;
        }
        if (enemyFaction == null && inc.Faction != null)
        {
            enemyFaction = inc.Faction;
        }

        contactName = inc.LabelCap;
        factionName = inc.Faction?.Name ?? factionName;
        if (markAmbushSprung)
        {
            ambushSprung = true;
        }
    }

    public void Notify_PawnExitedMap(Pawn raider, Pawn? captive)
    {
        if (raider != null && !escapedHostiles.Contains(raider))
        {
            escapedHostiles.Add(raider);
        }

        var record = raider != null ? engagements.FirstOrDefault(e => !e.isDisengaged && e.trackedPawns.Contains(raider)) : null;
        if (record != null)
        {
            if (raider != null && !record.escapedPawns.Contains(raider))
            {
                record.escapedPawns.Add(raider);
            }

            if (captive != null && !record.capturedColonists.Contains(captive))
            {
                record.capturedColonists.Add(captive);
                if (record.faction?.kidnapped != null && record.faction.kidnapped.KidnappedPawnsListForReading.Contains(captive))
                {
                    record.faction.kidnapped.RemoveKidnappedPawn(captive);
                }
                if (record.incursion != null)
                {
                    record.incursion.AddKidnappedColonist(captive);
                }
                Log.Message($"[Project Vigilance] Colonist {captive.LabelShort} abducted by escaping {raider?.LabelShort} from incursion {record.contactLabel}. Retained for overland pursuit.");
            }
        }
        else if (sourceIncursion != null)
        {
            if (captive != null && !capturedColonists.Contains(captive))
            {
                if (enemyFaction?.kidnapped != null && enemyFaction.kidnapped.KidnappedPawnsListForReading.Contains(captive))
                {
                    enemyFaction.kidnapped.RemoveKidnappedPawn(captive);
                }
                sourceIncursion.AddKidnappedColonist(captive);
            }
        }

        if (captive != null && !capturedColonists.Contains(captive))
        {
            capturedColonists.Add(captive);
        }
    }

    public void InitializeAmbush(
        Faction faction,
        List<Pawn> enemies,
        Lord? lord,
        string contact,
        string factionLabel,
        WorldObject_Incursion? incursion)
    {
        isActive = true;
        ambushSprung = false;
        enemyFaction = faction;
        travelLord = lord;
        allEnemies = enemies != null ? new List<Pawn>(enemies) : new List<Pawn>();
        trackedEnemies = new List<Pawn>(allEnemies);
        contactName = contact;
        factionName = factionLabel;
        sourceIncursion = incursion;

        if (sourceIncursion != null && sourceIncursion.Spawned)
        {
            RegisterIncursion(sourceIncursion, enemies, markAmbushSprung: false);
            var record = engagements.FirstOrDefault(e => e.incursion == sourceIncursion);
            if (record != null)
            {
                record.isTransitOnly = true;
                record.wasEngagedInCombat = false;
            }
            ambushSprung = false; // Ambush not sprung yet
        }
    }

    public void InitializeActiveCombat(
        Faction faction,
        List<Pawn> enemies,
        string contact,
        string factionLabel,
        WorldObject_Incursion? incursion)
    {
        isActive = false;
        ambushSprung = true;
        enemyFaction = faction;
        travelLord = null;
        allEnemies = enemies != null ? new List<Pawn>(enemies) : new List<Pawn>();
        trackedEnemies = new List<Pawn>(allEnemies);
        contactName = contact;
        factionName = factionLabel;
        sourceIncursion = incursion;

        if (sourceIncursion != null && sourceIncursion.Spawned)
        {
            RegisterIncursion(sourceIncursion, enemies);
        }
    }

    public override void MapComponentTick()
    {
        base.MapComponentTick();

        if (ambushSprung || engagements.Any(e => !e.isDisengaged))
        {
            if (Find.TickManager.TicksGame % 60 == 0)
            {
                // Process each engaged incursion independently
                for (int i = 0; i < engagements.Count; i++)
                {
                    var record = engagements[i];
                    if (record.isDisengaged || record.incursion == null || !record.incursion.Spawned)
                    {
                        continue;
                    }

                    // While the primary ambush remains concealed and un-sprung, let the ambush sweep below handle sourceIncursion
                    if (isActive && !ambushSprung && record.incursion == sourceIncursion)
                    {
                        continue;
                    }

                    // Check if this specific incursion still has conscious mobile combatants on the map
                    bool hasHumanlikesOrMechs = record.trackedPawns.Any(p => p != null && (p.RaceProps.Humanlike || p.RaceProps.IsMechanoid));
                    bool hasMobileCombatants = hasHumanlikesOrMechs
                        ? record.trackedPawns.Any(p => p != null && p.Spawned && !p.Dead && !p.Downed && (p.RaceProps.Humanlike || p.RaceProps.IsMechanoid))
                        : record.trackedPawns.Any(p => p != null && p.Spawned && !p.Dead && !p.Downed);
                    if (hasMobileCombatants)
                    {
                        continue;
                    }

                    // Order any remaining unguided animals from this incursion to flee the battlefield
                    foreach (var p in record.trackedPawns)
                    {
                        if (p != null && p.Spawned && !p.Dead && !p.Downed && p.RaceProps.Animal)
                        {
                            if (p.CurJob == null || p.CurJob.def != JobDefOf.Goto)
                            {
                                if (RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 exitSpot, map, CellFinder.EdgeRoadChance_Hostile))
                                {
                                    Job fleeJob = JobMaker.MakeJob(JobDefOf.Goto, exitSpot);
                                    fleeJob.exitMapOnArrival = true;
                                    p.jobs?.ClearQueuedJobs();
                                    p.jobs?.EndCurrentJob(JobCondition.InterruptForced);
                                    p.jobs?.TryTakeOrderedJob(fleeJob, JobTag.Misc);
                                }
                            }
                        }
                    }

                    // No conscious mobile combatants remain for this incursion: resolve survivors
                    var exitedPawns = record.trackedPawns.Where(p => p != null && !p.Spawned && !p.Dead && !p.Destroyed).ToList();
                    foreach (var p in exitedPawns)
                    {
                        if (!record.escapedPawns.Contains(p))
                        {
                            record.escapedPawns.Add(p);
                        }
                    }

                    record.isDisengaged = true;

                    if (record.isTransitOnly && !record.wasEngagedInCombat)
                    {
                        // Clean peaceful transit across the sector without taking casualties or joining battle
                        record.incursion.SetCommittedPawns(record.escapedPawns);
                        record.incursion.IsEngagedInTacticalMap = false;
                        Log.Message($"[Project Vigilance] Incursion {record.incursion.TrackingId} ({record.contactLabel}) completed sector transit peacefully with {record.escapedPawns.Count} pawns. Continuing on Geoscape towards {record.incursion.DestinationTile}.");
                        Messages.Message($"{record.contactLabel} has passed through the sector and resumed their journey on the Geoscape.", record.incursion, MessageTypeDefOf.NeutralEvent);
                    }
                    else if (record.escapedPawns.Count > 0)
                    {
                        // Raiders successfully exited the battlefield and continue their overland march / retreat
                        record.incursion.SetCommittedPawns(record.escapedPawns);
                        record.incursion.SetRetreatingPawns(record.escapedPawns);
                        record.incursion.IsRetreating = true;
                        record.incursion.IsEngagedInTacticalMap = false;
                        record.incursion.IsIdentified = true;
                        record.incursion.CurrentIntelligenceTier = IntelligenceTier.Classified;

                        if (record.capturedColonists.Count > 0)
                        {
                            record.incursion.SetKidnappedColonists(new List<Pawn>(record.capturedColonists));
                        }

                        // Resolve retreat route back to faction stronghold
                        if (IncursionOriginResolver.TryResolveRetreatRoute(map.Tile, record.incursion.Faction, out var destTile, out var retreatPath, out var speed))
                        {
                            record.incursion.SetRetreatRoute(destTile, retreatPath, speed);
                        }
                        else
                        {
                            record.incursion.ReverseCourse("escaping with survivors/captives after engagement");
                        }

                        Log.Message($"[Project Vigilance] Incursion {record.incursion.TrackingId} ({record.contactLabel}) escaped battlefield with {record.escapedPawns.Count} survivors and {record.capturedColonists.Count} captive(s). Continuing on Geoscape towards {record.incursion.DestinationTile}.");
                        Messages.Message($"Enemy raid {record.contactLabel} has broken off the engagement and is retreating on the Geoscape with {record.escapedPawns.Count} survivor(s)!", record.incursion, MessageTypeDefOf.NeutralEvent);
                    }
                    else
                    {
                        // Incursion completely neutralized
                        record.incursion.Destroy();
                        Log.Message($"[Project Vigilance] Incursion {record.incursion.TrackingId} ({record.contactLabel}) fully neutralized on battlefield. Incursion destroyed.");
                    }
                }

                // Fallback for legacy single-incursion if engagements was empty
                if (engagements.Count == 0 && sourceIncursion != null && sourceIncursion.Spawned)
                {
                    var livingHostiles = allEnemies.Where(p => p != null && !p.Dead && !p.Downed && p.Spawned).ToList();
                    if (livingHostiles.Count == 0)
                    {
                        var escapedEnemies = allEnemies.Where(p => p != null && !p.Spawned && !p.Dead && !p.Destroyed).ToList();
                        foreach (var p in escapedEnemies)
                        {
                            if (!escapedHostiles.Contains(p))
                            {
                                escapedHostiles.Add(p);
                            }
                        }

                        if (escapedHostiles.Count > 0)
                        {
                            sourceIncursion.SetCommittedPawns(escapedHostiles);
                            sourceIncursion.SetRetreatingPawns(escapedHostiles);
                            sourceIncursion.IsRetreating = true;
                            sourceIncursion.IsEngagedInTacticalMap = false;
                            sourceIncursion.IsIdentified = true;
                            sourceIncursion.CurrentIntelligenceTier = IntelligenceTier.Classified;

                            if (capturedColonists.Count > 0)
                            {
                                sourceIncursion.SetKidnappedColonists(new List<Pawn>(capturedColonists));
                            }

                            if (IncursionOriginResolver.TryResolveRetreatRoute(map.Tile, sourceIncursion.Faction, out var destTile, out var retreatPath, out var speed))
                            {
                                sourceIncursion.SetRetreatRoute(destTile, retreatPath, speed);
                            }
                            else
                            {
                                sourceIncursion.ReverseCourse("escaping with survivors/captives after engagement");
                            }

                            Log.Message($"[Project Vigilance] Incursion {sourceIncursion.TrackingId} escaped battlefield with {escapedHostiles.Count} survivors and {capturedColonists.Count} captive(s). Continuing on Geoscape towards {sourceIncursion.DestinationTile}.");
                        }
                        else
                        {
                            sourceIncursion.Destroy();
                            Log.Message("[Project Vigilance] Incursion fully neutralized on battlefield. Incursion destroyed.");
                        }

                        sourceIncursion = null;
                        ambushSprung = false;
                    }
                }

                // Check if all engagements have disengaged
                bool anyActive = engagements.Any(e => !e.isDisengaged);
                if (!anyActive && engagements.Count > 0 && (!isActive || ambushSprung))
                {
                    ambushSprung = false;
                    sourceIncursion = null;

                    // Release and order any independent guest cohorts to depart safely off map edge
                    DepartGuestCohorts();

                    bool isTacticalEncounter = map.Parent is CaravansBattlefield;
                    if (isTacticalEncounter)
                    {
                        bool anyLivingColonistsOnMap = map.mapPawns.AllPawns.Any(p => p.Faction == Faction.OfPlayer && p.IsColonist && p.Spawned && !p.Dead);
                        if (!anyLivingColonistsOnMap)
                        {
                            IncursionTacticalRetreatUtility.CleanUpEncounterMap(map);
                        }
                    }
                }
            }

            if (!isActive || ambushSprung)
            {
                return;
            }
        }

        if (!isActive)
        {
            if (Find.TickManager.TicksGame % 120 == 0)
            {
                // Check if any abandoned enemies woke up after column departed
                for (int i = 0; i < allEnemies.Count; i++)
                {
                    Pawn p = allEnemies[i];
                    if (p != null && p.Spawned && !p.Dead && !p.Downed && p.Awake() && p.HostileTo(Faction.OfPlayer))
                    {
                        if (p.CurJob == null || p.CurJob.def != JobDefOf.Goto)
                        {
                            if (RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 exitSpot, map, CellFinder.EdgeRoadChance_Hostile))
                            {
                                Job fleeJob = JobMaker.MakeJob(JobDefOf.Goto, exitSpot);
                                fleeJob.exitMapOnArrival = true;
                                p.jobs?.ClearQueuedJobs();
                                p.jobs?.EndCurrentJob(JobCondition.InterruptForced);
                                p.jobs?.TryTakeOrderedJob(fleeJob, JobTag.Misc);
                            }
                        }
                    }
                }
            }
            return;
        }

        // Run detection sweeps on 30-tick intervals (half-second)
        if (Find.TickManager.TicksGame % 30 != 0)
        {
            return;
        }

        // Clean up dead/destroyed pawns from allEnemies
        allEnemies.RemoveAll(p => p == null || p.Dead || p.Destroyed);

        // Conscious mobile enemies marching across the map
        var mobileEnemies = allEnemies.Where(p => p.Spawned && !p.Downed && p.Awake()).ToList();

        // Downed enemies currently lying on the map
        var downedEnemies = allEnemies.Where(p => p.Spawned && p.Downed).ToList();

        // Re-track any downed enemies who have recovered and stood back up
        for (int i = 0; i < mobileEnemies.Count; i++)
        {
            Pawn mobile = mobileEnemies[i];
            if (travelLord != null && !travelLord.ownedPawns.Contains(mobile) && map.lordManager.lords.Contains(travelLord))
            {
                travelLord.AddPawn(mobile);
            }
        }

        if (mobileEnemies.Count == 0)
        {
            isActive = false;
            var livingEscaped = allEnemies.Where(p => !p.Spawned && !p.Dead && !p.Destroyed).ToList();

            // Hostile incursion continues overland march with pawns that successfully exited
            if (sourceIncursion != null && sourceIncursion.Spawned)
            {
                var record = engagements.FirstOrDefault(e => e.incursion == sourceIncursion);
                if (record != null)
                {
                    record.isDisengaged = true;
                }

                if (livingEscaped.Count > 0)
                {
                    sourceIncursion.SetCommittedPawns(livingEscaped);
                    sourceIncursion.IsEngagedInTacticalMap = false;
                    sourceIncursion.AdvancePathTile();
                }
                else
                {
                    sourceIncursion.Destroy();
                }
                sourceIncursion = null;
            }

            TaggedString letterTitle;
            TaggedString letterText;

            if (downedEnemies.Count > 0)
            {
                letterTitle = "Sector Clear (Comrades Abandoned): " + contactName;
                letterText = $"The enemy column from {factionName} has traversed your sector and departed without detecting your scouts.\n\n" +
                             $"However, {downedEnemies.Count} incapacitated enemy combatant(s) were abandoned on the field by their comrades:\n" +
                             string.Join("\n", downedEnemies.Select(p => $"• {p.LabelCap}")) + "\n\n" +
                             $"Your squad remains concealed. You may safely approach to capture, tend, or interrogate the fallen enemies before reforming your caravan.\n\n" +
                             $"Select any colonist or use the Reform Caravan button to proceed.";
            }
            else
            {
                letterTitle = "Sector Clear: " + contactName;
                letterText = $"The enemy column from {factionName} has traversed your sector and departed without detecting your scouts.\n\n" +
                             $"Scout observations confirmed {livingEscaped.Count} combatants in the force.\n\n" +
                             $"Your squad remains concealed and can now safely reform the caravan to return to the geoscape.\n\n" +
                             $"Select any colonist or use the Reform Caravan button to proceed.";
            }

            LetterDef def = PV_LetterDefOf.PV_TacticalEncounter_Positive ?? LetterDefOf.PositiveEvent;
            var clearLetter = (ChoiceLetter_TacticalEncounter)LetterMaker.MakeLetter(def);
            clearLetter.label = letterTitle;
            clearLetter.text = letterText;
            clearLetter.battleMap = map;
            clearLetter.targetWorldObject = map.Parent ?? (WorldObject?)sourceIncursion;
            clearLetter.targetTile = map.Tile;
            clearLetter.canReformCaravan = true;
            clearLetter.battleButtonLabel = "Go to Battlescape";
            clearLetter.worldButtonLabel = "Go to cell (Geoscape)";
            clearLetter.lookTargets = downedEnemies.Count > 0
                ? new LookTargets(downedEnemies.Concat(map.mapPawns.FreeColonistsSpawned))
                : new LookTargets(map.mapPawns.FreeColonistsSpawned);

            Find.LetterStack.ReceiveLetter(clearLetter);

            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            return;
        }

        // Check Line of Sight detection against player colonists within 18 cells
        var playerPawns = map.mapPawns?.FreeColonistsSpawned;
        if (playerPawns == null || playerPawns.Count == 0)
        {
            return;
        }

        for (int e = 0; e < mobileEnemies.Count; e++)
        {
            Pawn enemy = mobileEnemies[e];
            for (int c = 0; c < playerPawns.Count; c++)
            {
                Pawn colonist = playerPawns[c];
                if (colonist.Dead || colonist.Downed || !colonist.Spawned)
                {
                    continue;
                }

                float dist = enemy.Position.DistanceTo(colonist.Position);
                if (dist <= 18f && GenSight.LineOfSight(enemy.Position, colonist.Position, map, skipFirstCell: true))
                {
                    SpringAmbush($"{colonist.LabelShort} was spotted by {enemy.LabelShort} at close range ({Mathf.RoundToInt(dist)} cells)!");
                    return;
                }
            }
        }
    }

    public void Notify_TransitLordsGunfire(IntVec3 pos, float radius = 45f)
    {
        if (map?.lordManager?.lords == null) return;
        for (int i = 0; i < map.lordManager.lords.Count; i++)
        {
            Lord lord = map.lordManager.lords[i];
            if (lord.LordJob is LordJob_IncursionTransit transitJob)
            {
                transitJob.Notify_GunfireHeard(pos, radius);
            }
        }
    }

    public void Notify_DamageTaken(Pawn victim, DamageInfo dinfo)
    {
        if (victim != null && dinfo.Instigator != null)
        {
            lastCombatNoiseTick = Find.TickManager.TicksGame;
            lastCombatNoisePos = victim.Position;
            Notify_TransitLordsGunfire(victim.Position, 30f);
        }

        if (!isActive || ambushSprung)
        {
            return;
        }

        // Only player-caused damage breaks concealment
        if (!IsPlayerCausedDamage(dinfo))
        {
            return;
        }

        string instigatorName = dinfo.Instigator?.LabelShort ?? "your forces";
        string victimName = victim?.LabelShort ?? "a target";
        string damageLabel = dinfo.Def?.label ?? "attack";
        SpringAmbush($"{victimName} was struck by {instigatorName} ({damageLabel})!");
    }

    public void Notify_WeaponDischarged(Pawn caster)
    {
        lastCombatNoiseTick = Find.TickManager.TicksGame;
        if (caster != null)
        {
            lastCombatNoisePos = caster.Position;
            Notify_TransitLordsGunfire(caster.Position, 45f);
        }

        if (!isActive || ambushSprung)
        {
            return;
        }

        if (caster != null && (caster.Faction == Faction.OfPlayer || caster.IsColonist))
        {
            SpringAmbush($"{caster.LabelShort} opened fire, breaking concealment!");
        }
    }

    private static bool IsPlayerCausedDamage(DamageInfo dinfo)
    {
        if (dinfo.Instigator == null)
        {
            return false;
        }

        // Player faction direct attribution (colonist, tamed animal, player mechanoid)
        if (dinfo.Instigator.Faction == Faction.OfPlayer)
        {
            return true;
        }

        // Colonist or guest pawn instigator
        if (dinfo.Instigator is Pawn pawn && (pawn.Faction == Faction.OfPlayer || pawn.HostFaction == Faction.OfPlayer || pawn.IsColonist))
        {
            return true;
        }

        // Player-owned defense, trap, or turret
        if (dinfo.Instigator is Building building && building.Faction == Faction.OfPlayer)
        {
            return true;
        }

        return false;
    }

    public void SprungAmbush(string reason) => SpringAmbush(reason);

    public void SpringAmbush(string reason)
    {
        if (!isActive || ambushSprung)
        {
            return;
        }

        ambushSprung = true;
        isActive = false;

        Log.Message($"[Project Vigilance] Ambush sprung on map {map.uniqueID}. Reason: {reason}");

        // Keep world incursion marked as engaged in tactical map
        if (sourceIncursion != null && sourceIncursion.Spawned)
        {
            sourceIncursion.IsEngagedInTacticalMap = true;
            var record = engagements.FirstOrDefault(e => e.incursion == sourceIncursion);
            if (record != null)
            {
                record.isTransitOnly = false;
                record.wasEngagedInCombat = true;
            }
        }

        // Re-enable FireAtWill for drafted player colonists now that concealment is broken
        var spawnedColonists = map.mapPawns?.FreeColonistsSpawned;
        if (spawnedColonists != null)
        {
            for (int i = 0; i < spawnedColonists.Count; i++)
            {
                Pawn col = spawnedColonists[i];
                if (col?.drafter != null && col.drafter.Drafted)
                {
                    col.drafter.FireAtWill = true;
                }
            }
        }

        // Transition remaining active enemies to AssaultColony combat LordJob
        var activeEnemies = trackedEnemies.Where(p => !p.Dead && !p.Downed && p.Spawned).ToList();
        if (activeEnemies.Count > 0 && enemyFaction != null)
        {
            // Detach all active enemies from any existing lords (travelLord, trade lord, or prior combat lord)
            for (int i = 0; i < activeEnemies.Count; i++)
            {
                Pawn enemy = activeEnemies[i];
                Lord existingLord = enemy.GetLord();
                if (existingLord != null)
                {
                    existingLord.RemovePawn(enemy);
                    if (existingLord.ownedPawns.Count == 0 && map.lordManager.lords.Contains(existingLord))
                    {
                        map.lordManager.RemoveLord(existingLord);
                    }
                }
            }

            if (travelLord != null && map.lordManager.lords.Contains(travelLord))
            {
                map.lordManager.RemoveLord(travelLord);
            }

            LordJob combatJob = new LordJob_AssaultColony(
                enemyFaction,
                canKidnap: true,
                canTimeoutOrFlee: true,
                sappers: false,
                useAvoidGridSmart: false,
                canSteal: false);
            LordMaker.MakeNewLord(enemyFaction, combatJob, map, activeEnemies);
        }

        Find.TickManager.Notify_GeneratedPotentiallyHostileMap();

        TaggedString label = "Ambush Sprung: " + contactName;
        TaggedString text = $"Your strike team's ambush against {contactName} from {factionName} has been sprung!\n\n" +
                            $"Trigger: {reason}\n\n" +
                            $"The enemy column has broken travel formation and is scrambling into combat positions to counterattack. Prepare your forces!\n\n" +
                            $"Tip: Select any colonist to use Attempt Retreat or Reform Caravan at any time.";

        LetterDef def = PV_LetterDefOf.PV_TacticalEncounter_Threat ?? LetterDefOf.ThreatBig;
        var ambushLetter = (ChoiceLetter_TacticalEncounter)LetterMaker.MakeLetter(def);
        ambushLetter.label = label;
        ambushLetter.text = text;
        ambushLetter.battleMap = map;
        ambushLetter.targetWorldObject = map.Parent ?? (WorldObject?)sourceIncursion;
        ambushLetter.targetTile = map.Tile;
        ambushLetter.battleButtonLabel = "Go to Battlescape";
        ambushLetter.worldButtonLabel = "Go to cell (Geoscape)";
        ambushLetter.lookTargets = new LookTargets(activeEnemies.Concat(map.mapPawns?.FreeColonistsSpawned ?? Enumerable.Empty<Pawn>()));

        Find.LetterStack.ReceiveLetter(ambushLetter);
        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
    }

    public override void MapComponentOnGUI()
    {
        base.MapComponentOnGUI();

        if (Find.CurrentMap != map) return;

        // Do not render tactical HUD banners on ordinary colony, settlement, or non-encounter maps
        bool isOverlandEncounter = map.Parent is CaravansBattlefield || sourceIncursion != null || isActive || ambushSprung || (enemyFaction != null && allEnemies.Count > 0);
        if (!isOverlandEncounter) return;

        // Do not render banners if there are no player colonists on the map (e.g. formed into a caravan, escaped, evacuated)
        bool hasAnyColonist = map.mapPawns?.AllPawns?.Any(p => p.Faction == Faction.OfPlayer && p.IsColonist && p.Spawned) ?? false;
        if (!hasAnyColonist) return;

        bool hasMobileColonist = map.mapPawns?.FreeColonistsSpawned?.Any(p => !p.Dead && !p.Downed) ?? false;
        bool hasDownedColonist = map.mapPawns?.AllPawns?.Any(p => p.Faction == Faction.OfPlayer && p.IsColonist && p.Spawned && p.Downed && !p.Dead) ?? false;

        float topY = 16f;
        var colonistBar = Find.ColonistBar;
        if (colonistBar != null && colonistBar.Entries != null && colonistBar.Entries.Count > 0)
        {
            float maxBottom = 0f;
            var drawLocs = colonistBar.DrawLocs;
            if (drawLocs != null && drawLocs.Count > 0)
            {
                for (int i = 0; i < drawLocs.Count; i++)
                {
                    float bottom = drawLocs[i].y + colonistBar.Size.y;
                    if (bottom > maxBottom)
                    {
                        maxBottom = bottom;
                    }
                }
            }
            else
            {
                maxBottom = 21f + colonistBar.Size.y;
            }

            if (maxBottom > 0f)
            {
                float labelPadding = 24f * colonistBar.Scale + 8f;
                topY = Mathf.Max(topY, maxBottom + labelPadding);
            }
        }

        if (isActive && !ambushSprung)
        {
            if (!hasMobileColonist) return;

            float width = 390f;
            float height = 40f;
            Rect rect = new Rect((UI.screenWidth - width) / 2f, topY, width, height);

            Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.12f, 0.10f, 0.95f));
            Widgets.DrawHighlightIfMouseover(rect);
            GUI.color = new Color(0.4f, 0.9f, 0.4f);
            Widgets.DrawBox(rect, 2);
            GUI.color = Color.white;

            Rect labelRect = new Rect(rect.x + 12f, rect.y + 8f, 215f, 24f);
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.4f, 0.9f, 0.4f);
            Widgets.Label(labelRect, "Concealment: Holding Fire");
            GUI.color = Color.white;

            Rect btnRect = new Rect(rect.x + 235f, rect.y + 6f, 145f, 28f);
            if (Widgets.ButtonText(btnRect, "Quietly Disengage"))
            {
                IncursionTacticalRetreatUtility.PromptStealthDisengage(map, this);
            }
        }
        else
        {
            bool hasActiveThreats = GenHostility.AnyHostileActiveThreatToPlayer(map, countDormantPawnsAsHostile: false);
            if (!hasActiveThreats && (map.Parent is CaravansBattlefield || ambushSprung || sourceIncursion != null))
            {
                if (hasMobileColonist)
                {
                    float width = 310f;
                    float height = 40f;
                    Rect rect = new Rect((UI.screenWidth - width) / 2f, topY, width, height);

                    Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.12f, 0.15f, 0.95f));
                    Widgets.DrawHighlightIfMouseover(rect);
                    GUI.color = new Color(0.2f, 0.85f, 0.5f);
                    Widgets.DrawBox(rect, 2);
                    GUI.color = Color.white;

                    Rect labelRect = new Rect(rect.x + 12f, rect.y + 8f, 135f, 24f);
                    Text.Font = GameFont.Small;
                    GUI.color = new Color(0.3f, 0.9f, 1f);
                    Widgets.Label(labelRect, "Sector Clear");
                    GUI.color = Color.white;

                    Rect btnRect = new Rect(rect.x + 155f, rect.y + 6f, 145f, 28f);
                    if (Widgets.ButtonText(btnRect, "Reform Caravan"))
                    {
                        IncursionTacticalRetreatUtility.OpenReformCaravanDialog(map);
                    }
                }
                else if (hasDownedColonist)
                {
                    float width = 340f;
                    float height = 40f;
                    Rect rect = new Rect((UI.screenWidth - width) / 2f, topY, width, height);

                    Widgets.DrawBoxSolid(rect, new Color(0.18f, 0.05f, 0.05f, 0.95f));
                    Widgets.DrawHighlightIfMouseover(rect);
                    GUI.color = new Color(0.95f, 0.25f, 0.25f);
                    Widgets.DrawBox(rect, 2);
                    GUI.color = Color.white;

                    Rect labelRect = new Rect(rect.x + 12f, rect.y + 8f, width - 24f, 24f);
                    Text.Font = GameFont.Small;
                    GUI.color = new Color(1f, 0.4f, 0.4f);
                    Widgets.Label(labelRect, "Squad Incapacitated (Relief Required)");
                    GUI.color = Color.white;
                }
            }
        }
    }

    public void DepartGuestCohorts()
    {
        if (guestCohorts == null || guestCohorts.Count == 0) return;

        for (int i = 0; i < guestCohorts.Count; i++)
        {
            var cohort = guestCohorts[i];
            if (cohort.hasDeparted) continue;

            var livingPawns = cohort.pawns.Where(p => p != null && p.Spawned && !p.Dead && !p.Downed).ToList();
            if (livingPawns.Count > 0)
            {
                Faction cohortFaction = cohort.faction ?? livingPawns[0].Faction;
                Lord? existingLord = livingPawns[0].GetLord();
                if (existingLord != null)
                {
                    map.lordManager.RemoveLord(existingLord);
                }

                LordJob exitJob = new LordJob_ExitMapBest(LocomotionUrgency.Walk, canDig: true, canDefendSelf: true);
                LordMaker.MakeNewLord(cohortFaction, exitJob, map, livingPawns);

                Log.Message($"[Project Vigilance] Guest cohort '{cohort.groupLabel}' ordered to depart overland encounter map cleanly.");
                Messages.Message($"{cohort.groupLabel} has gathered their survivors and is departing the area.", new GlobalTargetInfo(livingPawns[0].Position, map), MessageTypeDefOf.NeutralEvent);
            }
            cohort.hasDeparted = true;
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref isActive, "isActive", false);
        Scribe_Values.Look(ref ambushSprung, "ambushSprung", false);
        Scribe_References.Look(ref enemyFaction, "enemyFaction");
        Scribe_Values.Look(ref contactName, "contactName", "Hostile Warband");
        Scribe_Values.Look(ref factionName, "factionName", "enemy forces");
        Scribe_Collections.Look(ref trackedEnemies, "trackedEnemies", LookMode.Reference);
        Scribe_Collections.Look(ref allEnemies, "allEnemies", LookMode.Reference);
        Scribe_References.Look(ref travelLord, "travelLord");
        Scribe_References.Look(ref sourceIncursion, "sourceIncursion");
        Scribe_Collections.Look(ref escapedHostiles, "escapedHostiles", LookMode.Reference);
        Scribe_Collections.Look(ref capturedColonists, "capturedColonists", LookMode.Reference);
        Scribe_Collections.Look(ref engagements, "engagements", LookMode.Deep);
        Scribe_Collections.Look(ref guestCohorts, "guestCohorts", LookMode.Deep);
        Scribe_Values.Look(ref lastCombatNoiseTick, "lastCombatNoiseTick", -99999);
        Scribe_Values.Look(ref lastCombatNoisePos, "lastCombatNoisePos", IntVec3.Invalid);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            if (engagements == null)
            {
                engagements = new List<IncursionEngagementRecord>();
            }
            if (guestCohorts == null)
            {
                guestCohorts = new List<GuestCohortRecord>();
            }

            // Backward compatibility migration for saves with sourceIncursion
            if (engagements.Count == 0 && sourceIncursion != null)
            {
                engagements.Add(new IncursionEngagementRecord(sourceIncursion, allEnemies)
                {
                    escapedPawns = new List<Pawn>(escapedHostiles ?? new List<Pawn>()),
                    capturedColonists = new List<Pawn>(capturedColonists ?? new List<Pawn>()),
                    isDisengaged = !ambushSprung && !isActive
                });
            }
        }
    }
}
