using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.World;

/// <summary>
/// Broad tactical category of planetary incursion, differentiating trade merchants, visitors,
/// passers-by, imperial tribute collectors, pilgrims, and hostile combatants.
/// </summary>
public enum IncursionGroupKind
{
    HostileWarband,
    TradeCaravan,
    Visitors,
    Travelers,
    TributeCollector,
    Pilgrims,
    Beggars,
    AlliedReinforcements,
    GenericNeutral
}

[StaticConstructorOnStartup]
public partial class WorldObject_Incursion : WorldObject, IDetectableTarget, ITrader, IThingHolder
{
    private IncidentDef? incidentDef;
    private IncidentParms? incidentParms;
    private PlanetTile originTile;
    private PlanetTile destinationTile;
    private List<PlanetTile> pathTiles = new();
    private int currentPathIndex;
    private float tileProgress;
    private float speedTilesPerDay = 20f;
    private int departureDelayTicks;
    private bool isIdentified = false;
    private IntelligenceTier intelligenceTier = IntelligenceTier.Undetected;
    private bool hasDispatchedContactAlert = false;
    private bool hasDispatchedClassifiedAlert = false;
    private Vector3 lastSampledDrawPos = Vector3.zero;
    private int trackingId;
    private bool isTurningBack = false;
    private bool hasBeenWarnedAway = false;
    private HashSet<int> metCaravanIds = new();
    private HashSet<int> metMapIds = new();
    private Dictionary<int, int> caravanCooldowns = new();
    private int skirmishCooldownExpiry = 0;
    private bool isRetreating = false;
    private bool captorsSuccumbedLetterSent = false;
    private bool isTradeMissionAborted = false;
    private List<Pawn>? retreatingPawns;
    private List<Pawn>? kidnappedColonists;
    private List<Pawn>? committedPawns;
    private Pawn? traderPawn;
    private TraderKindDef? explicitTraderKind;
    private ThingOwner<Thing>? tradeStock;
    private Caravan? pursuitTargetCaravan;
    private int pursuitTargetCaravanId = -1;
    private int tacticalWatchdogCooldown;

    public IncidentDef? IncidentDef => incidentDef;
    public IncidentParms? IncidentParms => incidentParms;
    public PlanetTile OriginTile => originTile;
    public PlanetTile DestinationTile { get => destinationTile; set => destinationTile = value; }
    public IReadOnlyList<PlanetTile> PathTiles => pathTiles;
    public int CurrentPathIndex => currentPathIndex;
    public void SetCurrentPathIndex(int index) => currentPathIndex = Mathf.Clamp(index, 0, Math.Max(0, (pathTiles?.Count ?? 1) - 1));
    public void SetTileRaw(PlanetTile t) => WorldObjectAccessUtility.SetTileRaw(this, t);
    public void SetSpawnedRaw(bool s) => WorldObjectAccessUtility.SetSpawnedRaw(this, s);
    public float TileProgress => tileProgress;
    public float SpeedTilesPerDay => speedTilesPerDay;

    /// <summary>
    /// Returns the terrain and road movement difficulty multiplier for the incursion's current step.
    /// </summary>
    public float GetCurrentMovementDifficulty()
    {
        PlanetTile curTile = Tile;
        PlanetTile neighborTile = curTile;

        if (pathTiles != null && currentPathIndex >= 0 && currentPathIndex < pathTiles.Count)
        {
            if (currentPathIndex > 0 && (tileProgress < 0.5f || currentPathIndex + 1 >= pathTiles.Count))
            {
                neighborTile = pathTiles[currentPathIndex - 1];
            }
            else if (currentPathIndex + 1 < pathTiles.Count)
            {
                neighborTile = pathTiles[currentPathIndex + 1];
            }
        }

        return IncursionOriginResolver.GetStepMovementDifficulty(neighborTile, curTile);
    }

    /// <summary>
    /// Effective overland speed in tiles per day after applying current terrain and road difficulty.
    /// </summary>
    public float EffectiveSpeedTilesPerDay => speedTilesPerDay / GetCurrentMovementDifficulty();

    /// <summary>
    /// Updates baseline open-terrain speed from the incursion's active roster (mounts, creatures, wounds).
    /// </summary>
    public void RecalculateSpeedFromPawns()
    {
        var activePawns = (isRetreating && retreatingPawns is { Count: > 0 })
            ? retreatingPawns
            : committedPawns;
        if (activePawns is { Count: > 0 })
        {
            speedTilesPerDay = IncursionOriginResolver.CalculateGroupBaseSpeedTilesPerDay(activePawns, Faction);
        }
    }
    public int DepartureDelayTicks => departureDelayTicks;
    public bool IsMobilizing => departureDelayTicks > 0;
    public int TrackingId { get => trackingId; set => trackingId = value; }
    public bool HasDispatchedContactAlert { get => hasDispatchedContactAlert; set => hasDispatchedContactAlert = value; }
    public bool HasDispatchedClassifiedAlert { get => hasDispatchedClassifiedAlert; set => hasDispatchedClassifiedAlert = value; }
    public Pawn? TraderPawn { get => traderPawn; set => traderPawn = value; }
    public TraderKindDef? ExplicitTraderKind { get => explicitTraderKind; set => explicitTraderKind = value; }
    public bool IsTradeMissionAborted => isTradeMissionAborted;
    public bool IsTurningBack => isTurningBack;
    public bool HasBeenWarnedAway => hasBeenWarnedAway;
    public void MarkWarnedAway() => hasBeenWarnedAway = true;
    public bool HasMetCaravan(int caravanId) => metCaravanIds.Contains(caravanId);
    public void MarkCaravanMet(int caravanId) => metCaravanIds.Add(caravanId);
    public void ResetCaravanMet(int caravanId) => metCaravanIds.Remove(caravanId);
    public bool HasMetMap(int mapId) => metMapIds.Contains(mapId);
    public void MarkMapMet(int mapId) => metMapIds.Add(mapId);
    public void ResetMapMet(int mapId) => metMapIds.Remove(mapId);
    public bool IsCaravanOnCooldown(int caravanId)
    {
        return caravanCooldowns != null &&
               caravanCooldowns.TryGetValue(caravanId, out int expiry) &&
               Find.TickManager.TicksGame < expiry;
    }
    public void SetCaravanCooldown(int caravanId, int expiryTick)
    {
        caravanCooldowns ??= new Dictionary<int, int>();
        caravanCooldowns[caravanId] = expiryTick;
    }
    public void ResetCaravanCooldown(int caravanId)
    {
        caravanCooldowns?.Remove(caravanId);
        metCaravanIds?.Remove(caravanId);
    }
    public bool IsEngagedInTacticalMap { get => isEngagedInTacticalMap; set => isEngagedInTacticalMap = value; }
    public Caravan? PursuitTargetCaravan => pursuitTargetCaravan;
    public bool IsPursuingCaravan => pursuitTargetCaravanId > 0;
    public int PursuitTargetCaravanId => pursuitTargetCaravanId;

    public bool IsSkirmishOnCooldown => Current.Game?.tickManager is { } tm && tm.TicksGame < skirmishCooldownExpiry;
    public int SkirmishCooldownExpiry { get => skirmishCooldownExpiry; set => skirmishCooldownExpiry = value; }
    public void SetSkirmishCooldown(int durationTicks = 15000)
    {
        int currentTicks = Current.Game?.tickManager?.TicksGame ?? 0;
        skirmishCooldownExpiry = currentTicks + durationTicks;
    }

    public void SetCombatPoints(float points)
    {
        if (incidentParms != null)
        {
            incidentParms.points = points;
        }
    }

    public List<Pawn> ApplySkirmishCasualties(float casualtyFraction, float remainingPoints)
    {
        SetCombatPoints(remainingPoints);
        List<Pawn> casualties = new();

        if (committedPawns != null && committedPawns.Count > 0)
        {
            int pawnsToRemove = (int)Math.Round(committedPawns.Count * casualtyFraction);
            pawnsToRemove = Math.Clamp(pawnsToRemove, 0, committedPawns.Count);
            if (pawnsToRemove > 0)
            {
                for (int i = 0; i < pawnsToRemove; i++)
                {
                    int lastIdx = committedPawns.Count - 1;
                    Pawn victim = committedPawns[lastIdx];
                    committedPawns.RemoveAt(lastIdx);
                    if (victim != null)
                    {
                        casualties.Add(victim);
                        if (Find.WorldPawns != null)
                        {
                            Find.WorldPawns.ForcefullyKeptPawns.Remove(victim);
                        }
                    }
                }
            }

            if (IsUnderlyingTradeCaravan && !isTradeMissionAborted)
            {
                if (traderPawn == null || !committedPawns.Contains(traderPawn) || committedPawns.Count == 0)
                {
                    AbortTradeMission("merchant leadership lost in skirmish");
                }
            }
        }

        return casualties;
    }

    public void SetPursuitTarget(Caravan targetCaravan, List<PlanetTile> initialPath)
    {
        if (targetCaravan == null) return;
        pursuitTargetCaravan = targetCaravan;
        pursuitTargetCaravanId = targetCaravan.ID;
        destinationTile = targetCaravan.Tile;
        pathTiles = initialPath;
        currentPathIndex = 0;
        tileProgress = 0f;
        isTurningBack = false;
        if (initialPath != null && initialPath.Count > 0)
        {
            Tile = initialPath[0];
        }
        Log.Message($"[Project Vigilance] Incursion {TrackingId} ({LabelCap}) initiated hot pursuit of caravan {targetCaravan.LabelCap} towards tile {destinationTile}.");
    }

    public void ClearPursuitTarget()
    {
        pursuitTargetCaravan = null;
        pursuitTargetCaravanId = -1;
    }

    public void SetRetargetRoute(PlanetTile newDest, List<PlanetTile> newPath)
    {
        ClearPursuitTarget();
        destinationTile = newDest;
        pathTiles = newPath;
        currentPathIndex = 0;
        tileProgress = 0f;
        isTurningBack = false;
        if (newPath.Count > 0)
        {
            Tile = newPath[0];
        }
        if (incidentParms != null)
        {
            incidentParms.target = Current.Game?.FindMap(newDest);
        }
        Log.Message($"[Project Vigilance] Incursion {TrackingId} ({LabelCap}) diverted to secondary target tile {newDest}.");
    }

    public void AdvancePathTile()
    {
        if (pathTiles != null && pathTiles.Count > 0)
        {
            if (currentPathIndex < pathTiles.Count - 1)
            {
                currentPathIndex++;
                Tile = pathTiles[currentPathIndex];
                tileProgress = 0f;
            }
            else
            {
                if (IncursionOriginResolver.TryResolveRetreatRoute(Tile, Faction, out var destTile, out var retreatPath, out var speed))
                {
                    SetRetreatRoute(destTile, retreatPath, speed);
                }
                else
                {
                    ReverseCourse("departing destination sector after engagement");
                }
            }
        }
    }

    private bool isEngagedInTacticalMap = false;
    public bool IsRetreating
    {
        get => isRetreating;
        set => isRetreating = value;
    }
    public List<Pawn>? RetreatingPawns => retreatingPawns;
    public List<Pawn>? KidnappedColonists => kidnappedColonists;
    public List<Pawn>? CommittedPawns => committedPawns;
    public ThingOwner<Thing>? TradeStock => tradeStock;

    public void SetRetreatingPawns(List<Pawn> pawns)
    {
        retreatingPawns = pawns != null
            ? pawns.Where(p => p != null && !p.Dead && !p.Destroyed).Distinct().ToList()
            : null;

        if (retreatingPawns != null)
        {
            for (int i = 0; i < retreatingPawns.Count; i++)
            {
                EnsureInWorldPawnsIfUnspawned(retreatingPawns[i]);
            }

            if (IsUnderlyingTradeCaravan)
            {
                SyncCargoFromPawnsToTradeStock(retreatingPawns);
            }
        }

        RecalculateSpeedFromPawns();
    }

    public void SetKidnappedColonists(List<Pawn> pawns)
    {
        kidnappedColonists = pawns != null
            ? pawns.Where(p => p != null && !p.Dead && !p.Destroyed).Distinct().ToList()
            : null;

        if (kidnappedColonists != null)
        {
            for (int i = 0; i < kidnappedColonists.Count; i++)
            {
                EnsureInWorldPawnsIfUnspawned(kidnappedColonists[i], isCaptiveColonist: true);
            }
        }
    }

    public void SetCommittedPawns(List<Pawn>? pawns)
    {
        committedPawns = pawns != null
            ? pawns.Where(p => p != null && !p.Dead && !p.Destroyed).Distinct().ToList()
            : null;

        if (committedPawns != null)
        {
            for (int i = 0; i < committedPawns.Count; i++)
            {
                EnsureInWorldPawnsIfUnspawned(committedPawns[i]);
            }

            if (IsUnderlyingTradeCaravan)
            {
                SyncCargoFromPawnsToTradeStock(committedPawns);
                EnsureTraderPawnConfigured();
            }
        }

        RecalculateSpeedFromPawns();
    }

    public void AddKidnappedColonist(Pawn captive)
    {
        if (captive == null || captive.Dead || captive.Destroyed) return;
        kidnappedColonists ??= new List<Pawn>();
        if (!kidnappedColonists.Contains(captive))
        {
            kidnappedColonists.Add(captive);
        }
        EnsureInWorldPawnsIfUnspawned(captive, isCaptiveColonist: true);
    }

    private static void EnsureInWorldPawnsIfUnspawned(Pawn? p, bool isCaptiveColonist = false) =>
        WorldPawnIntegrityUtility.EnsurePawnInWorldPawnsIfUnspawned(p, isCaptiveColonist);

    /// <summary>
    /// Removes dangling DirectPawnRelations pointing to pawns that were discarded by RimWorld's
    /// WorldPawns garbage collection (or never added to WorldPawns/Maps). Prevents harmless but noisy
    /// "Could not resolve reference... curParent=RimWorld.DirectPawnRelation" and "has relation with null pawn"
    /// load-time warnings.
    /// </summary>
    /// <summary>
    /// Scrubs direct social relations referencing discarded or unresolvable pawns before saving.
    /// </summary>
    private static void SanitizeDanglingRelations(Pawn? p) =>
        WorldPawnIntegrityUtility.SanitizeDanglingRelations(p);

    private static void PreparePawnListForSave(List<Pawn>? pawns, bool isCaptiveColonist = false)
    {
        if (pawns == null)
        {
            return;
        }

        WorldPawnIntegrityUtility.EnsureListInWorldPawns(pawns, isCaptiveColonist);
        WorldPawnIntegrityUtility.PruneInvalidPawnReferences(pawns, removeDeadOrDestroyed: true);
        for (int i = 0; i < pawns.Count; i++)
        {
            WorldPawnIntegrityUtility.SanitizeDanglingRelations(pawns[i]);
        }
    }

    public void SetRetreatRoute(PlanetTile destination, List<PlanetTile> retreatPath, float speed = 0f)
    {
        isTurningBack = false;
        isRetreating = true;
        destinationTile = destination;
        pathTiles = retreatPath != null ? new List<PlanetTile>(retreatPath) : new List<PlanetTile>();
        currentPathIndex = 0;
        tileProgress = 0f;
        if (speed > 0f)
        {
            speedTilesPerDay = speed;
        }
        if (pathTiles.Count > 0)
        {
            Tile = pathTiles[0];
        }
    }

    public void TransitionToOverlandRetreat(List<Pawn> survivors, List<Pawn>? capturedColonists, Map? sourceMap)
    {
        SetCommittedPawns(survivors);
        SetRetreatingPawns(survivors);
        isRetreating = true;
        isEngagedInTacticalMap = false;
        isIdentified = true;
        intelligenceTier = IntelligenceTier.Classified;

        if (sourceMap != null)
        {
            MarkMapMet(sourceMap.Parent?.ID ?? sourceMap.uniqueID);

            int mapCaravanCooldown = Find.TickManager.TicksGame + 15000;
            var localCaravans = Find.WorldObjects?.Caravans?.Where(c => c != null && c.Spawned && c.Faction == Faction.OfPlayer && c.Tile == sourceMap.Tile);
            if (localCaravans != null)
            {
                foreach (var c in localCaravans)
                {
                    MarkCaravanMet(c.ID);
                    SetCaravanCooldown(c.ID, mapCaravanCooldown);
                }
            }

            if (capturedColonists != null && capturedColonists.Count > 0)
            {
                SetKidnappedColonists(new List<Pawn>(capturedColonists));
            }

            if (IncursionOriginResolver.TryResolveRetreatRoute(sourceMap.Tile, Faction, out var destTile, out var retreatPath, out var speed))
            {
                SetRetreatRoute(destTile, retreatPath, speed);
            }
            else
            {
                ReverseCourse("escaping with survivors/captives after engagement");
            }
        }

        RecalculateSpeedFromPawns();
    }

    /// <summary>
    /// Aborts an active trade caravan expedition (e.g. following the death or loss of the caravan master/trader),
    /// cancelling the rest of the trade route and reversing course or retreating to the nearest friendly base.
    /// </summary>
    public void AbortTradeMission(string? reason = null)
    {
        if (isTradeMissionAborted) return;
        isTradeMissionAborted = true;
        isRetreating = true;
        isTurningBack = false;

        if (traderPawn != null)
        {
            traderPawn.mindState.wantsToTradeWithColony = false;
            traderPawn = null;
        }

        if (IncursionOriginResolver.TryResolveRetreatRoute(Tile, Faction, out var destTile, out var retreatPath, out var speed))
        {
            SetRetreatRoute(destTile, retreatPath, speed);
        }
        else
        {
            ReverseCourse(reason ?? "trade mission aborted");
        }

        string factionName = Faction?.Name ?? "Caravan";
        Messages.Message($"Trade Mission Aborted: Following the loss of their merchant, the surviving travellers from {factionName} have cancelled their journey and are returning to base.", this, MessageTypeDefOf.NeutralEvent);
        Log.Message($"[Project Vigilance] Incursion {TrackingId} ({LabelCap}) aborted trade mission. Reason: {reason ?? "merchant lost"}. Returning to {destinationTile}.");
    }

    public bool IsRestingAtNight
    {
        get
        {
            if (!GeoscapeRules.IncursionsRestAtNight)
            {
                return false;
            }

            // Incursions at their final destination tile push forward to begin arrival/attack
            if (currentPathIndex >= pathTiles.Count - 1)
            {
                return false;
            }

            // Mechanoids, ancient automatons, and supernatural horrors never sleep
            if (Faction != null && (Faction.def == FactionDefOf.Mechanoid || Faction.def.defName.Contains("Mechanoid")))
            {
                return false;
            }

            if (incidentDef != null)
            {
                string defName = incidentDef.defName;
                if (defName.Contains("Mechanoid") || defName.Contains("Entity") || defName.Contains("Chimera") || defName.Contains("Devourer") || defName.Contains("Gorehulk"))
                {
                    return false;
                }
            }

            // Biological mortal factions camp during local solar night
            return Tile.Valid && CaravanNightRestUtility.RestingNowAt(Tile);
        }
    }

    public bool IsIdentified
    {
        get => GeoscapeRules.VisibilityMode == VisibilityMode.Omniscient ||
               (isIdentified && intelligenceTier >= IntelligenceTier.Classified);
        set
        {
            isIdentified = value;
            if (value && intelligenceTier < IntelligenceTier.Classified)
            {
                intelligenceTier = IntelligenceTier.Classified;
            }
        }
    }

    // IDetectableTarget Implementation
    public string TargetLabel => LabelCap;
    public PlanetTile CurrentTile => Tile;
    public Vector3 CurrentDrawPos => TrueDrawPos;
    public Faction? TargetFaction => Faction;
    public bool IsHostileToPlayer
    {
        get
        {
            if (Faction == null) return true;
            Faction? playerFaction = (Find.World != null && Find.FactionManager != null) ? Faction.OfPlayerSilentFail : null;
            return playerFaction != null ? Faction.HostileTo(playerFaction) : (Faction.def?.permanentEnemy ?? true);
        }
    }
    public IntelligenceTier CurrentIntelligenceTier
    {
        get => intelligenceTier;
        set
        {
            intelligenceTier = value;
            if (value < IntelligenceTier.Contact && GeoscapeRules.VisibilityMode != VisibilityMode.Omniscient)
            {
                if (Find.WorldSelector != null && Find.WorldSelector.IsSelected(this))
                {
                    Find.WorldSelector.Deselect(this);
                }
            }
        }
    }

    public void UpdateSampledPosition()
    {
        if (Find.World?.grid != null)
        {
            lastSampledDrawPos = TrueDrawPos;
        }
    }

}
