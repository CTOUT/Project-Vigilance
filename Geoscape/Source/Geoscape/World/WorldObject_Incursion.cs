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
public class WorldObject_Incursion : WorldObject, IDetectableTarget, ITrader, IThingHolder
{
    private IncidentDef? incidentDef;
    private IncidentParms? incidentParms;
    private PlanetTile originTile;
    private PlanetTile destinationTile;
    private List<PlanetTile> pathTiles = new();
    private int currentPathIndex;
    private float tileProgress;
    private float speedTilesPerDay = 24f;
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
    private Dictionary<int, int> caravanCooldowns = new();
    private bool isRetreating = false;
    private bool isTradeMissionAborted = false;
    private List<Pawn>? retreatingPawns;
    private List<Pawn>? kidnappedColonists;
    private List<Pawn>? committedPawns;
    private Pawn? traderPawn;
    private TraderKindDef? explicitTraderKind;
    private ThingOwner<Thing>? tradeStock;

    public IncidentDef? IncidentDef => incidentDef;
    public IncidentParms? IncidentParms => incidentParms;
    public PlanetTile OriginTile => originTile;
    public PlanetTile DestinationTile { get => destinationTile; set => destinationTile = value; }
    public IReadOnlyList<PlanetTile> PathTiles => pathTiles;
    public int CurrentPathIndex => currentPathIndex;
    public float TileProgress => tileProgress;
    public float SpeedTilesPerDay => speedTilesPerDay;
    public int DepartureDelayTicks => departureDelayTicks;
    public bool IsMobilizing => departureDelayTicks > 0;
    public int TrackingId { get => trackingId; set => trackingId = value; }
    public Pawn? TraderPawn { get => traderPawn; set => traderPawn = value; }
    public TraderKindDef? ExplicitTraderKind { get => explicitTraderKind; set => explicitTraderKind = value; }
    public bool IsTradeMissionAborted => isTradeMissionAborted;
    public bool IsTurningBack => isTurningBack;
    public bool HasBeenWarnedAway => hasBeenWarnedAway;
    public void MarkWarnedAway() => hasBeenWarnedAway = true;
    public bool HasMetCaravan(int caravanId) => metCaravanIds.Contains(caravanId);
    public void MarkCaravanMet(int caravanId) => metCaravanIds.Add(caravanId);
    public void ResetCaravanMet(int caravanId) => metCaravanIds.Remove(caravanId);
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
    public bool IsEngagedInTacticalMap { get => isEngagedInTacticalMap; set => isEngagedInTacticalMap = value; }

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
                if (traderPawn == null || !committedPawns.Contains(traderPawn))
                {
                    traderPawn = committedPawns.FirstOrDefault(p => p.trader != null) ??
                                 committedPawns.FirstOrDefault(p => p.kindDef != null && p.kindDef.trader) ??
                                 committedPawns.FirstOrDefault(p => p.RaceProps != null && p.RaceProps.Humanlike);
                }
                if (traderPawn != null)
                {
                    traderPawn.mindState.wantsToTradeWithColony = true;
                    if (traderPawn.trader == null)
                    {
                        traderPawn.trader = new Pawn_TraderTracker(traderPawn);
                    }
                    if (traderPawn.trader.traderKind == null && TraderKind != null)
                    {
                        traderPawn.trader.traderKind = TraderKind;
                    }
                }
            }
        }
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

    private static void EnsureInWorldPawnsIfUnspawned(Pawn? p, bool isCaptiveColonist = false)
    {
        if (p == null || p.Destroyed || p.Dead || p.Spawned)
        {
            return;
        }

        if (Find.WorldPawns != null)
        {
            if (!Find.WorldPawns.Contains(p))
            {
                Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
            }
            Find.WorldPawns.ForcefullyKeptPawns.Add(p);
        }

        if (isCaptiveColonist && p.Faction != Faction.OfPlayer)
        {
            p.SetFaction(Faction.OfPlayer);
        }
    }

    /// <summary>
    /// Removes dangling DirectPawnRelations pointing to pawns that were discarded by RimWorld's
    /// WorldPawns garbage collection (or never added to WorldPawns/Maps). Prevents harmless but noisy
    /// "Could not resolve reference... curParent=RimWorld.DirectPawnRelation" and "has relation with null pawn"
    /// load-time warnings.
    /// </summary>
    private static void SanitizeDanglingRelations(Pawn? p)
    {
        if (p?.relations == null) return;

        var relations = p.relations.DirectRelations;
        for (int r = relations.Count - 1; r >= 0; r--)
        {
            Pawn other = relations[r].otherPawn;
            if (other == null || other.Discarded || (other.Destroyed && Find.WorldPawns != null && !Find.WorldPawns.Contains(other)))
            {
                relations.RemoveAt(r);
            }
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
            if (!GeoscapeMod.Settings.incursionsRestAtNight)
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
        get => GeoscapeMod.Settings.visibilityMode == VisibilityMode.Omniscient ||
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
    public bool IsHostileToPlayer => Faction?.HostileTo(Faction.OfPlayer) ?? true;
    public IntelligenceTier CurrentIntelligenceTier
    {
        get => intelligenceTier;
        set
        {
            intelligenceTier = value;
            if (value < IntelligenceTier.Contact && GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient)
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
        lastSampledDrawPos = TrueDrawPos;
    }

    private static Texture2D? cachedCaravanExpandingIcon;
    private static Texture2D CaravanExpandingIcon =>
        cachedCaravanExpandingIcon ??= ContentFinder<Texture2D>.Get("World/WorldObjects/Expanding/Caravan");

    private static Texture2D? cachedCaravanWorldTexture;
    private static Texture2D CaravanWorldTexture =>
        cachedCaravanWorldTexture ??= ContentFinder<Texture2D>.Get("World/WorldObjects/Caravan");

    public override bool SelectableNow =>
        GeoscapeMod.Settings.visibilityMode == VisibilityMode.Omniscient || intelligenceTier >= IntelligenceTier.Contact;

    public override bool NeverMultiSelect =>
        GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient && intelligenceTier < IntelligenceTier.Contact;

    public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan)
    {
        foreach (var opt in base.GetFloatMenuOptions(caravan))
        {
            yield return opt;
        }

        if (caravan == null || !caravan.Spawned || caravan.Faction != Faction.OfPlayer)
        {
            yield break;
        }

        if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient && intelligenceTier < IntelligenceTier.Contact)
        {
            yield break;
        }

        bool isClassified = GeoscapeMod.Settings.visibilityMode == VisibilityMode.Omniscient ||
                            (isIdentified && intelligenceTier >= IntelligenceTier.Classified);

        if (!isClassified)
        {
            // Unidentified contact: do not leak whether they are hostile or trade!
            string label = $"Investigate {LabelCap}";
            yield return new FloatMenuOption(label, () =>
            {
                PlanetTile targetTile = GetInterceptTargetTile(caravan);
                caravan.pather.StartPath(targetTile, new CaravanArrivalAction_InterceptIncursion(this), true);
            });
            yield break;
        }

        bool isHostile = Faction?.HostileTo(Faction.OfPlayer) ?? true;
        if (isHostile)
        {
            string interceptLabel = $"Intercept {LabelCap}";
            yield return new FloatMenuOption(interceptLabel, () =>
            {
                PlanetTile targetTile = GetInterceptTargetTile(caravan);
                caravan.pather.StartPath(targetTile, new CaravanArrivalAction_InterceptIncursion(this), true);
            });
        }
        else
        {
            if (GroupKind == IncursionGroupKind.TradeCaravan)
            {
                string tradeLabel = $"Hail & Trade with {LabelCap}";
                yield return new FloatMenuOption(tradeLabel, () =>
                {
                    PlanetTile targetTile = GetInterceptTargetTile(caravan);
                    caravan.pather.StartPath(targetTile, new CaravanArrivalAction_InterceptIncursion(this), true);
                });
            }

            if (hasBeenWarnedAway || isTurningBack)
            {
                yield return new FloatMenuOption($"Warn away {LabelCap} (Already retreating)", null);
            }
            else if (!HasImminentThreatNearby())
            {
                yield return new FloatMenuOption($"Warn away {LabelCap} (No imminent threat nearby)", null);
            }
            else
            {
                string warnLabel = $"Warn away / Turn back {LabelCap}";
                yield return new FloatMenuOption(warnLabel, () =>
                {
                    IncursionInterceptionUtility.WarnAway(this, caravan);
                });
            }
        }
    }

    public bool HasImminentThreatNearby()
    {
        PlanetTile dest = destinationTile.Valid ? destinationTile : Tile;

        // 1. Check if the destination map has active hostile threats
        Map? destMap = Current.Game?.FindMap(dest);
        if (destMap != null && GenHostility.AnyHostileActiveThreatToPlayer(destMap, countDormantPawnsAsHostile: false, canBeFogged: true))
        {
            return true;
        }

        // 2. Check if any hostile incursion is within 4 tiles of our current tile or our destination tile
        List<WorldObject>? allWorldObjects = Find.WorldObjects?.AllWorldObjects;
        if (allWorldObjects != null)
        {
            for (int i = 0; i < allWorldObjects.Count; i++)
            {
                if (allWorldObjects[i] is WorldObject_Incursion other && other != this && other.Spawned)
                {
                    bool otherHostile = other.Faction?.HostileTo(Faction) ?? other.Faction?.HostileTo(Faction.OfPlayer) ?? true;
                    if (otherHostile)
                    {
                        float distCurrent = Find.WorldGrid.ApproxDistanceInTiles(Tile, other.Tile);
                        float distDest = Find.WorldGrid.ApproxDistanceInTiles(dest, other.Tile);
                        if (distCurrent <= 4f || distDest <= 4f)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    public override string LabelShort => Label;

    public override string Label
    {
        get
        {
            bool isClassified = GeoscapeMod.Settings.visibilityMode == VisibilityMode.Omniscient ||
                                (isIdentified && intelligenceTier >= IntelligenceTier.Classified);

            if (!isClassified)
            {
                return SensoryNarrativeUtility.GetContactLabel(trackingId: trackingId);
            }

            if (incidentDef != null || Faction != null)
            {
                string idTag = trackingId > 0
                    ? (SensoryNarrativeUtility.IsLowTech ? $" [#{trackingId}]" : $" [Contact-{trackingId}]")
                    : string.Empty;

                if (isRetreating)
                {
                    string retreatTag = (kidnappedColonists != null && kidnappedColonists.Count > 0)
                        ? " [Kidnappers]"
                        : " [Retreating]";
                    return $"{GetIdentifiedGroupLabel()}{idTag}{retreatTag}";
                }

                return $"{GetIdentifiedGroupLabel()}{idTag}";
            }

            return base.Label;
        }
    }

    /// <summary>
    /// True if this incursion was spawned as a merchant trade caravan (even if its faction subsequently turned hostile).
    /// </summary>
    public bool IsUnderlyingTradeCaravan
    {
        get
        {
            if (incidentParms?.traderKind != null ||
                (incidentParms?.pawnGroupKind != null && incidentParms.pawnGroupKind == PawnGroupKindDefOf.Trader))
            {
                return true;
            }

            string defName = incidentDef?.defName ?? string.Empty;
            string defLabel = incidentDef?.label ?? string.Empty;

            return defName.Equals("TraderCaravanArrival", StringComparison.OrdinalIgnoreCase) ||
                   defName.IndexOf("Trader", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   defLabel.IndexOf("trader", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   defLabel.IndexOf("caravan", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   defLabel.IndexOf("merchant", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    /// <summary>
    /// True if this incursion was spawned as a visitor, traveller, pilgrim, or beggar group.
    /// </summary>
    public bool IsUnderlyingVisitorOrTraveler
    {
        get
        {
            if (IsUnderlyingTradeCaravan)
            {
                return false;
            }

            string defName = incidentDef?.defName ?? string.Empty;
            string defLabel = incidentDef?.label ?? string.Empty;

            return defName.Equals("VisitorGroup", StringComparison.OrdinalIgnoreCase) ||
                   defName.IndexOf("Visitor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   defLabel.IndexOf("visitor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   defLabel.IndexOf("guest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   defName.Equals("TravelerGroup", StringComparison.OrdinalIgnoreCase) ||
                   defName.IndexOf("Traveler", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   defLabel.IndexOf("traveler", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   defLabel.IndexOf("traveller", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   defLabel.IndexOf("passer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   defName.IndexOf("Pilgrim", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   defName.IndexOf("Beggar", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    /// <summary>
    /// Resolves the specific tactical kind of this incursion based on incident parameters and defs.
    /// Distinguishes between trade caravans, social visitors, passing travellers, tribute collectors, and hostile warbands.
    /// </summary>
    public IncursionGroupKind GroupKind
    {
        get
        {
            if (IsHostileToPlayer)
            {
                return IncursionGroupKind.HostileWarband;
            }

            if (incidentParms?.traderKind != null ||
                (incidentParms?.pawnGroupKind != null && incidentParms.pawnGroupKind == PawnGroupKindDefOf.Trader))
            {
                return IncursionGroupKind.TradeCaravan;
            }

            string defName = incidentDef?.defName ?? string.Empty;
            string defLabel = incidentDef?.label ?? string.Empty;

            if (defName.Equals("TraderCaravanArrival", StringComparison.OrdinalIgnoreCase) ||
                defName.IndexOf("Trader", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defLabel.IndexOf("trader", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defLabel.IndexOf("caravan", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defLabel.IndexOf("merchant", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return IncursionGroupKind.TradeCaravan;
            }

            if (defName.Equals("VisitorGroup", StringComparison.OrdinalIgnoreCase) ||
                defName.IndexOf("Visitor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defLabel.IndexOf("visitor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defLabel.IndexOf("guest", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return IncursionGroupKind.Visitors;
            }

            if (defName.Equals("TravelerGroup", StringComparison.OrdinalIgnoreCase) ||
                defName.IndexOf("Traveler", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defLabel.IndexOf("traveler", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defLabel.IndexOf("traveller", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defLabel.IndexOf("passer", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return IncursionGroupKind.Travelers;
            }

            if (defName.IndexOf("Tribute", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defLabel.IndexOf("tribute", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return IncursionGroupKind.TributeCollector;
            }

            if (defName.IndexOf("Pilgrim", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defLabel.IndexOf("pilgrim", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return IncursionGroupKind.Pilgrims;
            }

            if (defName.IndexOf("Beggar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defLabel.IndexOf("beggar", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return IncursionGroupKind.Beggars;
            }

            if (defName.IndexOf("RaidFriendly", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defName.IndexOf("MilitaryAid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defLabel.IndexOf("reinforcement", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defLabel.IndexOf("allies", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return IncursionGroupKind.AlliedReinforcements;
            }

            return IncursionGroupKind.GenericNeutral;
        }
    }

    /// <summary>
    /// Returns a human-readable lowercase label distinguishing the identified group.
    /// </summary>
    public string GetIdentifiedGroupLabel()
    {
        switch (GroupKind)
        {
            case IncursionGroupKind.HostileWarband:
                return "hostile warband";
            case IncursionGroupKind.TradeCaravan:
                if (isTradeMissionAborted)
                {
                    return "returning caravan (aborted mission)";
                }
                return explicitTraderKind != null
                    ? $"trade caravan ({explicitTraderKind.label})"
                    : incidentParms?.traderKind != null
                        ? $"trade caravan ({incidentParms.traderKind.label})"
                        : "trade caravan";
            case IncursionGroupKind.Visitors:
                return "visitors";
            case IncursionGroupKind.Travelers:
                return "travellers";
            case IncursionGroupKind.TributeCollector:
                return "royal tribute collector";
            case IncursionGroupKind.Pilgrims:
                return "pilgrims";
            case IncursionGroupKind.Beggars:
                return "beggars";
            case IncursionGroupKind.AlliedReinforcements:
                return "allied reinforcements";
            default:
                return incidentDef?.label ?? "peaceful party";
        }
    }

    public override Texture2D ExpandingIcon
    {
        get
        {
            if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient && intelligenceTier < IntelligenceTier.Contact)
            {
                return BaseContent.ClearTex;
            }
            return CaravanExpandingIcon;
        }
    }

    /// <summary>
    /// Calculates a calm, rhythmic tactical amber pulse (breathing beacon) for unidentified contacts.
    /// Uses real-world time with a smooth sinusoidal curve (~2.5s cycle) easing between 0.18 and 1.0 alpha.
    /// </summary>
    public static Color TacticalAmberPulseColor(float alphaMultiplier = 1f)
    {
        // Smooth breathing cycle (~2.5s) easing between 0.18 (deep translucency) and 1.0 (vivid beacon)
        float sinVal = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 2.5f);
        float pulse = 0.18f + 0.82f * sinVal;
        return new Color(1f, 0.78f, 0.15f, pulse * alphaMultiplier);
    }

    public override Color ExpandingIconColor
    {
        get
        {
            if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient && intelligenceTier < IntelligenceTier.Classified)
            {
                return TacticalAmberPulseColor(0.95f);
            }

            bool isHostile = Faction?.HostileTo(Faction.OfPlayer) ?? true;
            if (isHostile)
            {
                return new Color(1f, 0.25f, 0.25f, 0.95f); // Vivid hostile red
            }

            return new Color(0.25f, 0.85f, 1f, 0.95f); // Tactical friendly cyan
        }
    }

    public override Material Material
    {
        get
        {
            if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient && intelligenceTier < IntelligenceTier.Classified)
            {
                return MaterialPool.MatFrom(CaravanWorldTexture, ShaderDatabase.WorldOverlayTransparentLit, TacticalAmberPulseColor(0.95f), WorldMaterials.DynamicObjectRenderQueue);
            }

            bool isHostile = Faction?.HostileTo(Faction.OfPlayer) ?? true;
            Color incursionColor = isHostile
                ? new Color(1f, 0.25f, 0.25f, 0.95f)
                : new Color(0.25f, 0.85f, 1f, 0.95f);

            return MaterialPool.MatFrom(CaravanWorldTexture, ShaderDatabase.WorldOverlayTransparentLit, incursionColor, WorldMaterials.DynamicObjectRenderQueue);
        }
    }

    public override float DrawAltitude => 0.035f;

    public override void Draw()
    {
        if (!WorldRendererUtility.WorldRendered || Tile.LayerDef.isSpace)
        {
            return;
        }

        // Hide entirely if undetected by the sensory net
        if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient && intelligenceTier < IntelligenceTier.Contact)
        {
            return;
        }

        float averageTileSize = Tile.Layer.AverageTileSize;
        float rawTransitionPct = ExpandableWorldObjectsUtility.RawTransitionPct;
        float altOffset = Rand.RangeSeeded(0f, 0.01f, ID) + def.drawAltitudeOffset;

        bool isHostile = Faction?.HostileTo(Faction.OfPlayer) ?? true;
        Color statusColor;

        if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient && intelligenceTier < IntelligenceTier.Classified)
        {
            // Calm tactical amber breathing blip for unidentified contacts
            statusColor = TacticalAmberPulseColor(1f);
        }
        else
        {
            statusColor = isHostile
                ? new Color(1f, 0.25f, 0.25f, 0.95f)
                : new Color(0.25f, 0.85f, 1f, 0.95f);
        }

        // Alpha fade matching vanilla expandable objects transition
        float alpha = 1f;
        if (def.expandingIcon && rawTransitionPct > 0f && !ExpandableWorldObjectsUtility.HiddenByRules(this))
        {
            alpha = Mathf.Clamp01(1f - rawTransitionPct);
        }

        // 1. Base Layer: Caravan / marching force silhouette
        Material baseMat = MaterialPool.MatFrom(
            CaravanWorldTexture,
            ShaderDatabase.WorldOverlayTransparentLit,
            new Color(statusColor.r, statusColor.g, statusColor.b, statusColor.a * alpha),
            WorldMaterials.DynamicObjectRenderQueue);

        WorldRendererUtility.DrawQuadTangentialToPlanet(
            DrawPos,
            0.85f * averageTileSize,
            DrawAltitude + altOffset,
            baseMat,
            -90f,
            false,
            false,
            null);

        // 2. Overlay Layer: Faction Emblem (ONLY when Classified or Analysed)
        bool canShowEmblem = GeoscapeMod.Settings.visibilityMode == VisibilityMode.Omniscient || intelligenceTier >= IntelligenceTier.Classified;
        if (canShowEmblem && isIdentified && Faction?.def?.FactionIcon != null && GeoscapeMod.Settings.showFactionEmblems)
        {
            Color factionTint = Faction.Color;
            Material emblemMat = MaterialPool.MatFrom(
                Faction.def.FactionIcon,
                ShaderDatabase.WorldOverlayTransparentLit,
                new Color(factionTint.r, factionTint.g, factionTint.b, alpha),
                WorldMaterials.DynamicObjectRenderQueue);

            WorldRendererUtility.DrawQuadTangentialToPlanet(
                DrawPos,
                0.52f * averageTileSize,
                DrawAltitude + altOffset + 0.005f,
                emblemMat,
                -90f,
                false,
                false,
                null);
        }
    }

    public void SetupIncursion(
        IncidentDef def,
        IncidentParms parms,
        PlanetTile origin,
        PlanetTile destination,
        List<PlanetTile> path,
        float speed,
        int delayTicks = 0)
    {
        incidentDef = def;
        incidentParms = parms;
        originTile = origin;
        destinationTile = destination;
        pathTiles = path ?? new List<PlanetTile>();
        currentPathIndex = 0;
        tileProgress = 0f;
        speedTilesPerDay = speed > 0f ? speed : 24f;
        departureDelayTicks = Math.Max(0, delayTicks);

        if (parms.faction != null)
        {
            SetFaction(parms.faction);
        }

        if (parms.traderKind != null)
        {
            explicitTraderKind = parms.traderKind;
        }
        else if (parms.faction?.def?.caravanTraderKinds != null && parms.faction.def.caravanTraderKinds.Count > 0)
        {
            explicitTraderKind = parms.faction.def.caravanTraderKinds.FirstOrDefault();
        }

        if (pathTiles.Count > 0)
        {
            Tile = pathTiles[0];
        }
        else
        {
            Tile = origin;
        }

        // Allocate unique tracking ID immediately upon incursion creation
        if (trackingId <= 0)
        {
            trackingId = ContactRegistry.Instance?.AllocateTrackingId() ??
                         SensoryManager.Instance?.AllocateContactTrackingId() ?? 1;
        }

        // Eagerly pre-materialize the full force makeup (pawns, animals, mechs, weapons, inventory)
        EnsureRosterGenerated();

        isIdentified = (GeoscapeMod.Settings.visibilityMode == VisibilityMode.Omniscient);
        intelligenceTier = (GeoscapeMod.Settings.visibilityMode == VisibilityMode.Omniscient)
            ? IntelligenceTier.Analysed
            : IntelligenceTier.Undetected;
        hasDispatchedContactAlert = false;
        hasDispatchedClassifiedAlert = false;
        lastSampledDrawPos = TrueDrawPos;

        // Register target with SensoryManager and ContactRegistry, then trigger immediate detection sweep
        SensoryManager.Instance?.RegisterTarget(this);
        SensoryManager.Instance?.PerformSensorySweep();
        ContactRegistry.Instance?.RegisterContact(this);
    }

    /// <summary>
    /// Eagerly generates and commits the incursion's force makeup (human combatants, pack animals,
    /// mechanoids, weapons, armor, carried provisions, and merchant trade cargo) into WorldPawns
    /// so the force composition exists deterministically from the moment of departure.
    /// </summary>
    public void EnsureRosterGenerated(Map? targetMap = null)
    {
        if (committedPawns == null || committedPawns.Count == 0)
        {
            var generated = IncursionInterceptionUtility.GetOrGenerateIncursionPawns(this, targetMap);
            if (generated != null && generated.Count > 0)
            {
                SetCommittedPawns(generated);
            }
        }

        if (IsUnderlyingTradeCaravan)
        {
            if (traderPawn == null && committedPawns != null)
            {
                traderPawn = committedPawns.FirstOrDefault(p => p.trader != null) ??
                             committedPawns.FirstOrDefault(p => p.kindDef != null && p.kindDef.trader) ??
                             committedPawns.FirstOrDefault(p => p.RaceProps != null && p.RaceProps.Humanlike);
            }
            if (traderPawn != null)
            {
                traderPawn.mindState.wantsToTradeWithColony = true;
                if (traderPawn.trader == null)
                {
                    traderPawn.trader = new Pawn_TraderTracker(traderPawn);
                }
                if (traderPawn.trader.traderKind == null && TraderKind != null)
                {
                    traderPawn.trader.traderKind = TraderKind;
                }
            }
            GetOrCreateTradeStock();
        }
    }

    public override void SpawnSetup()
    {
        base.SpawnSetup();
        if (lastSampledDrawPos == Vector3.zero)
        {
            lastSampledDrawPos = TrueDrawPos;
        }
        if (trackingId <= 0)
        {
            trackingId = ContactRegistry.Instance?.AllocateTrackingId() ??
                         SensoryManager.Instance?.AllocateContactTrackingId() ?? 1;
        }
        SensoryManager.Instance?.RegisterTarget(this);
        ContactRegistry.Instance?.RegisterContact(this);
    }

    public override void PostRemove()
    {
        SensoryManager.Instance?.DeregisterTarget(this);
        ContactRegistry.Instance?.Notify_ContactRemoved(this);
        base.PostRemove();
    }

    public void OnDetected(ISensorProvider sensor, IntelligenceTier newTier)
    {
        intelligenceTier = newTier;

        if (newTier >= IntelligenceTier.Contact && trackingId <= 0)
        {
            trackingId = ContactRegistry.Instance?.AllocateTrackingId() ??
                         SensoryManager.Instance?.AllocateContactTrackingId() ?? 1;
            ContactRegistry.Instance?.RegisterContact(this);
        }

        if (newTier >= IntelligenceTier.Classified)
        {
            isIdentified = true;
        }
        else
        {
            isIdentified = (GeoscapeMod.Settings.visibilityMode == VisibilityMode.Omniscient);
        }

        UpdateSampledPosition();

        if (GeoscapeMod.Settings.enableEarlyWarningLetters)
        {
            // If immediately detected at Classified+ (e.g. adjacent spawn or high-spec sensor),
            // bypass the vague Contact blip alert and directly issue the detailed identification alert.
            if (newTier >= IntelligenceTier.Classified)
            {
                if (!hasDispatchedClassifiedAlert)
                {
                    hasDispatchedContactAlert = true;
                    hasDispatchedClassifiedAlert = true;
                    DispatchClassifiedLetter(sensor);
                }
            }
            else if (newTier >= IntelligenceTier.Contact && !hasDispatchedContactAlert)
            {
                hasDispatchedContactAlert = true;
                DispatchContactLetter(sensor);
            }
        }
    }

    private void SendIncursionLetter(TaggedString title, TaggedString text, LetterDef def, Map? destinationMap, string targetName)
    {
        LetterDef choiceDef;
        if (def == LetterDefOf.ThreatBig || def == LetterDefOf.ThreatSmall)
        {
            choiceDef = PV_LetterDefOf.PV_TacticalEncounter_Threat ?? LetterDefOf.ThreatBig;
        }
        else if (def == LetterDefOf.PositiveEvent)
        {
            choiceDef = PV_LetterDefOf.PV_TacticalEncounter_Positive ?? LetterDefOf.PositiveEvent;
        }
        else
        {
            choiceDef = PV_LetterDefOf.PV_TacticalEncounter_Neutral ?? LetterDefOf.NeutralEvent;
        }

        var letter = (ChoiceLetter_TacticalEncounter)LetterMaker.MakeLetter(choiceDef);
        letter.label = title;
        letter.text = text;
        letter.targetWorldObject = this;
        letter.targetTile = Tile;
        letter.worldButtonLabel = "Go to Contact (Geoscape)";
        letter.lookTargets = new GlobalTargetInfo(this);

        if (destinationMap != null && destinationMap.Index >= 0)
        {
            letter.battleMap = destinationMap;
            letter.battleButtonLabel = $"Go inside {targetName} (Defenses)";
        }

        Find.LetterStack.ReceiveLetter(letter);
    }

    private void DispatchContactLetter(ISensorProvider sensor)
    {
        Map? targetMap = Current.Game.FindMap(destinationTile);
        string targetName = targetMap?.Parent?.LabelCap ?? "Colony territory";

        string title = SensoryNarrativeUtility.GetContactLetterTitle(sensor, trackingId);
        string text = SensoryNarrativeUtility.GetContactLetterBody(sensor, targetName, speedTilesPerDay, trackingId);

        SendIncursionLetter(title, text, LetterDefOf.ThreatSmall, targetMap, targetName);
    }

    private void DispatchClassifiedLetter(ISensorProvider sensor)
    {
        Map? targetMap = Current.Game.FindMap(destinationTile);
        string targetName = targetMap?.Parent?.LabelCap ?? "Colony territory";

        float actualRemainingTiles = Math.Max(0f, (pathTiles.Count - currentPathIndex) - tileProgress);
        float remainingHours = (actualRemainingTiles / Math.Max(0.1f, speedTilesPerDay)) * 24f;

        string factionStr = Faction?.Name ?? "Unknown Faction";
        string attrLine = SensoryNarrativeUtility.GetAttributionLine(sensor);
        string idPrefix = trackingId > 0
            ? (SensoryNarrativeUtility.IsLowTech ? $"Strangers #{trackingId} Classified: " : $"Contact-{trackingId} Classified: ")
            : string.Empty;
        string contactName = trackingId > 0
            ? (SensoryNarrativeUtility.IsLowTech ? $"strangers #{trackingId}" : $"Contact-{trackingId}")
            : "the approaching contact";

        switch (GroupKind)
        {
            case IncursionGroupKind.HostileWarband:
                {
                    string title = $"{idPrefix}Hostile Incursion ({factionStr})";
                    string closingAdvice = SensoryNarrativeUtility.GetHostileClosingAdvice(sensor);
                    string text = $"{sensor.SensorLabel} have confirmed that {contactName} is a hostile warband from {factionStr} marching towards {targetName}.\n\n" +
                                  $"Target: {targetName}\n" +
                                  $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                                  $"{attrLine}\n\n" +
                                  $"{closingAdvice}";

                    SendIncursionLetter(title, text, LetterDefOf.ThreatBig, targetMap, targetName);
                    break;
                }

            case IncursionGroupKind.TradeCaravan:
                {
                    string traderDetail = incidentParms?.traderKind != null ? $" ({incidentParms.traderKind.label})" : string.Empty;
                    string title = $"{idPrefix}Approaching Trade Caravan ({factionStr})";
                    string text = $"{sensor.SensorLabel} have confirmed that {contactName} is a friendly trade caravan{traderDetail} from {factionStr} heading towards {targetName}.\n\n" +
                                  $"Target: {targetName}\n" +
                                  $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                                  $"{attrLine}\n\n" +
                                  "They are bringing goods and silver to trade. Prepare your negotiators, silver stores, and trade stockpiles for their arrival.";

                    SendIncursionLetter(title, text, LetterDefOf.PositiveEvent, targetMap, targetName);
                    break;
                }

            case IncursionGroupKind.Visitors:
                {
                    string title = $"{idPrefix}Approaching Visitors ({factionStr})";
                    string text = $"{sensor.SensorLabel} have confirmed that {contactName} is a party of visitors from {factionStr} travelling towards {targetName}.\n\n" +
                                  $"Target: {targetName}\n" +
                                  $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                                  $"{attrLine}\n\n" +
                                  "They are visiting to socialise and relax. They are not a merchant caravan, though individual visitors may carry personal items to barter. Ensure your colony has a hospitable social space and spare guest accommodations.";

                    SendIncursionLetter(title, text, LetterDefOf.NeutralEvent, targetMap, targetName);
                    break;
                }

            case IncursionGroupKind.Travelers:
                {
                    string title = $"{idPrefix}Travellers Passing Near ({factionStr})";
                    string text = $"{sensor.SensorLabel} have confirmed that {contactName} is a group of travellers from {factionStr} moving through the region near {targetName}.\n\n" +
                                  $"Target: {targetName}\n" +
                                  $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                                  $"{attrLine}\n\n" +
                                  "They are simply passing through your territory and do not intend to visit, stay, or trade.";

                    SendIncursionLetter(title, text, LetterDefOf.NeutralEvent, targetMap, targetName);
                    break;
                }

            case IncursionGroupKind.TributeCollector:
                {
                    string title = $"{idPrefix}Royal Tribute Collector ({factionStr})";
                    string text = $"{sensor.SensorLabel} have confirmed that {contactName} is a royal tribute collector from {factionStr} heading towards {targetName}.\n\n" +
                                  $"Target: {targetName}\n" +
                                  $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                                  $"{attrLine}\n\n" +
                                  "They seek offerings of gold or enslaved prisoners in exchange for royal favour and honour.";

                    SendIncursionLetter(title, text, LetterDefOf.PositiveEvent, targetMap, targetName);
                    break;
                }

            case IncursionGroupKind.Pilgrims:
                {
                    string title = $"{idPrefix}Approaching Pilgrims ({factionStr})";
                    string text = $"{sensor.SensorLabel} have confirmed that {contactName} is a group of pilgrims from {factionStr} travelling towards {targetName}.\n\n" +
                                  $"Target: {targetName}\n" +
                                  $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                                  $"{attrLine}\n\n" +
                                  "They are travelling to venerate sacred relics or holy sites at your settlement.";

                    SendIncursionLetter(title, text, LetterDefOf.NeutralEvent, targetMap, targetName);
                    break;
                }

            case IncursionGroupKind.Beggars:
                {
                    string title = $"{idPrefix}Approaching Beggars ({factionStr})";
                    string beggarsDetail = SensoryNarrativeUtility.GetBeggarsIntentDescription();
                    string text = $"{sensor.SensorLabel} have confirmed that {contactName} is an impoverished group travelling towards {targetName}.\n\n" +
                                  $"Target: {targetName}\n" +
                                  $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                                  $"{attrLine}\n\n" +
                                  $"{beggarsDetail}";

                    SendIncursionLetter(title, text, LetterDefOf.NeutralEvent, targetMap, targetName);
                    break;
                }

            case IncursionGroupKind.AlliedReinforcements:
                {
                    string title = $"{idPrefix}Allied Reinforcements ({factionStr})";
                    string text = $"{sensor.SensorLabel} have confirmed that {contactName} is an allied military contingent from {factionStr} marching towards {targetName} to reinforce your defences.\n\n" +
                                  $"Target: {targetName}\n" +
                                  $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                                  $"{attrLine}\n\n" +
                                  "They are armed and ready to assist against planetary threats.";

                    SendIncursionLetter(title, text, LetterDefOf.PositiveEvent, targetMap, targetName);
                    break;
                }

            default:
                {
                    string groupType = GetIdentifiedGroupLabel();
                    string title = $"{idPrefix}Approaching {groupType.CapitalizeFirst()} ({factionStr})";
                    string text = $"{sensor.SensorLabel} have confirmed that {contactName} ({groupType}) from {factionStr} is heading towards {targetName}.\n\n" +
                                  $"Target: {targetName}\n" +
                                  $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                                  $"{attrLine}\n\n" +
                                  "They are approaching peacefully without hostile intent.";

                    SendIncursionLetter(title, text, LetterDefOf.NeutralEvent, targetMap, targetName);
                    break;
                }
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();

        // Cleanse target reference if the target Map was destroyed/abandoned to avoid orphaned Scribe cross-reference errors
        if (incidentParms != null && incidentParms.target is Map map && !Find.Maps.Contains(map))
        {
            incidentParms.target = null!;
        }

        Scribe_Defs.Look(ref incidentDef, "incidentDef");
        Scribe_Deep.Look(ref incidentParms, "incidentParms");
        Scribe_Values.Look(ref speedTilesPerDay, "speedTilesPerDay", 24f);
        Scribe_Values.Look(ref currentPathIndex, "currentPathIndex", 0);
        Scribe_Values.Look(ref tileProgress, "tileProgress", 0f);
        Scribe_Values.Look(ref departureDelayTicks, "departureDelayTicks", 0);
        Scribe_Values.Look(ref isIdentified, "isIdentified", false);
        Scribe_Values.Look(ref intelligenceTier, "intelligenceTier", IntelligenceTier.Undetected);
        Scribe_Values.Look(ref hasDispatchedContactAlert, "hasDispatchedContactAlert", false);
        Scribe_Values.Look(ref hasDispatchedClassifiedAlert, "hasDispatchedClassifiedAlert", false);
        Scribe_Values.Look(ref lastSampledDrawPos, "lastSampledDrawPos", Vector3.zero);
        Scribe_Values.Look(ref trackingId, "trackingId", 0);
        Scribe_Values.Look(ref isTurningBack, "isTurningBack", false);
        Scribe_Values.Look(ref hasBeenWarnedAway, "hasBeenWarnedAway", false);
        Scribe_Collections.Look(ref metCaravanIds, "metCaravanIds", LookMode.Value);
        if (metCaravanIds == null)
        {
            metCaravanIds = new HashSet<int>();
        }
        Scribe_Collections.Look(ref caravanCooldowns, "caravanCooldowns", LookMode.Value, LookMode.Value);
        if (caravanCooldowns == null)
        {
            caravanCooldowns = new Dictionary<int, int>();
        }
        Scribe_Values.Look(ref isRetreating, "isRetreating", false);
        Scribe_Values.Look(ref isTradeMissionAborted, "isTradeMissionAborted", false);
        Scribe_Values.Look(ref isEngagedInTacticalMap, "isEngagedInTacticalMap", false);

        if (Scribe.mode == LoadSaveMode.Saving)
        {
            if (retreatingPawns != null)
            {
                for (int i = 0; i < retreatingPawns.Count; i++)
                {
                    Pawn p = retreatingPawns[i];
                    if (p != null && !p.Destroyed && !p.Spawned && !Find.WorldPawns.Contains(p))
                    {
                        Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
                    }
                    SanitizeDanglingRelations(p);
                }
            }
            if (kidnappedColonists != null)
            {
                for (int i = 0; i < kidnappedColonists.Count; i++)
                {
                    Pawn p = kidnappedColonists[i];
                    if (p != null && !p.Destroyed && !p.Spawned && !Find.WorldPawns.Contains(p))
                    {
                        Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
                        if (p.Faction != Faction.OfPlayer)
                        {
                            p.SetFaction(Faction.OfPlayer);
                        }
                    }
                    SanitizeDanglingRelations(p);
                }
            }
            if (committedPawns != null)
            {
                for (int i = 0; i < committedPawns.Count; i++)
                {
                    Pawn p = committedPawns[i];
                    if (p != null && !p.Destroyed && !p.Spawned && !Find.WorldPawns.Contains(p))
                    {
                        Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
                    }
                    SanitizeDanglingRelations(p);
                }
            }
        }

        Scribe_Collections.Look(ref retreatingPawns, "retreatingPawns", LookMode.Reference);
        Scribe_Collections.Look(ref kidnappedColonists, "kidnappedColonists", LookMode.Reference);
        Scribe_Collections.Look(ref committedPawns, "committedPawns", LookMode.Reference);
        Scribe_References.Look(ref traderPawn, "traderPawn");
        Scribe_Defs.Look(ref explicitTraderKind, "explicitTraderKind");

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient && intelligenceTier < IntelligenceTier.Classified)
            {
                isIdentified = false;
            }
            committedPawns?.RemoveAll(p => p == null || p.Dead || p.Destroyed);
            retreatingPawns?.RemoveAll(p => p == null || p.Dead || p.Destroyed);
            kidnappedColonists?.RemoveAll(p => p == null || p.Dead || p.Destroyed);

            if (traderPawn != null && (traderPawn.Dead || traderPawn.Destroyed))
            {
                traderPawn = null;
            }

            if (IsUnderlyingTradeCaravan)
            {
                if (traderPawn == null && committedPawns != null)
                {
                    traderPawn = committedPawns.FirstOrDefault(p => p.trader != null) ??
                                 committedPawns.FirstOrDefault(p => p.kindDef != null && p.kindDef.trader) ??
                                 committedPawns.FirstOrDefault(p => p.RaceProps != null && p.RaceProps.Humanlike);
                }

                if (traderPawn != null)
                {
                    traderPawn.mindState.wantsToTradeWithColony = true;
                    if (traderPawn.trader == null)
                    {
                        traderPawn.trader = new Pawn_TraderTracker(traderPawn);
                    }
                    if (traderPawn.trader.traderKind == null && TraderKind != null)
                    {
                        traderPawn.trader.traderKind = TraderKind;
                    }
                }
            }
        }
        Scribe_Deep.Look(ref tradeStock, "tradeStock", this);

        bool legacyAlert = false;
        Scribe_Values.Look(ref legacyAlert, "hasDispatchedAlert", false);
        if (legacyAlert)
        {
            hasDispatchedContactAlert = true;
            hasDispatchedClassifiedAlert = true;
        }

        int originId = originTile.tileId;
        Scribe_Values.Look(ref originId, "originTileId", 0);
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            originTile = (PlanetTile)originId;
        }

        int destinationId = destinationTile.tileId;
        Scribe_Values.Look(ref destinationId, "destinationTileId", 0);
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            destinationTile = (PlanetTile)destinationId;
        }

        List<int>? serializedPath = null;
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            serializedPath = pathTiles.Select(t => t.tileId).ToList();
        }

        Scribe_Collections.Look(ref serializedPath, "pathTileIds", LookMode.Value);

        if (Scribe.mode == LoadSaveMode.LoadingVars && serializedPath != null)
        {
            pathTiles = serializedPath.Select(id => (PlanetTile)id).ToList();
        }

        if (Scribe.mode == LoadSaveMode.PostLoadInit && incidentParms != null && incidentParms.target == null && destinationTile.Valid)
        {
            incidentParms.target = Current.Game.FindMap(destinationTile);
        }
    }

    protected override void Tick()
    {
        base.Tick();

        if (intelligenceTier < IntelligenceTier.Contact && GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient)
        {
            if (Find.WorldSelector != null && Find.WorldSelector.IsSelected(this))
            {
                Find.WorldSelector.Deselect(this);
            }
        }

        if (isEngagedInTacticalMap)
        {
            // Watchdog: verify that an active tactical map actually exists for this incursion
            bool hasActiveTacticalMap = false;
            if (Find.Maps != null)
            {
                for (int m = 0; m < Find.Maps.Count; m++)
                {
                    var comp = Find.Maps[m].GetComponent<MapComponent_OverlandAmbush>();
                    if (comp != null && comp.HasTrackedIncursion(this))
                    {
                        hasActiveTacticalMap = true;
                        break;
                    }
                }
            }

            if (!hasActiveTacticalMap)
            {
                Log.Message($"[Project Vigilance] Incursion {TrackingId} ({LabelCap}) detected orphaned tactical map engagement flag. Self-healing and clearing engagement state.");
                isEngagedInTacticalMap = false;
            }
            else
            {
                return;
            }
        }

        if (pathTiles == null || pathTiles.Count == 0)
        {
            IncursionArrivalManager.ExecuteArrival(this);
            return;
        }

        // If mobilizing at origin base, decrement delay and hold position
        if (departureDelayTicks > 0)
        {
            departureDelayTicks--;
            return;
        }

        // Overland Physiological Simulation: retreating raiders and committed combatants
        if (Find.TickManager.TicksGame % 250 == 0)
        {
            if (isRetreating && retreatingPawns != null)
            {
                TickRetreatingPawnsPhysiology(250);
            }
            if (committedPawns != null && !isEngagedInTacticalMap)
            {
                TickCommittedPawnsPhysiology(250);
            }
            if (IsUnderlyingTradeCaravan && !isTradeMissionAborted)
            {
                if (traderPawn != null && (traderPawn.Dead || traderPawn.Destroyed))
                {
                    AbortTradeMission("caravan master perished en route");
                }
                else if (committedPawns != null && committedPawns.Count > 0 && !committedPawns.Any(p => p != null && !p.Dead && p.RaceProps != null && p.RaceProps.Humanlike))
                {
                    AbortTradeMission("all merchant personnel lost");
                }
            }
        }

        // Nighttime Rest: Biological mortal forces halt and camp at night
        if (IsRestingAtNight)
        {
            return;
        }

        float tilesPerTick = speedTilesPerDay / 60000f;
        tileProgress += tilesPerTick;

        while (tileProgress >= 1f)
        {
            tileProgress -= 1f;
            currentPathIndex++;

            if (currentPathIndex < pathTiles.Count)
            {
                Tile = pathTiles[currentPathIndex];
            }
            else
            {
                IncursionArrivalManager.ExecuteArrival(this);
                return;
            }
        }

        CheckInterceptionsAndPursuits();
    }

    public void ReverseCourse(string? reason = null)
    {
        if (!isRetreating)
        {
            isTurningBack = true;
        }
        if (pathTiles != null && pathTiles.Count > 0)
        {
            List<PlanetTile> newPath = new();
            int startIdx = Mathf.Clamp(currentPathIndex, 0, pathTiles.Count - 1);
            for (int i = startIdx; i >= 0; i--)
            {
                newPath.Add(pathTiles[i]);
            }
            if (newPath.Count <= 1 && Tile != originTile && originTile.Valid)
            {
                if (IncursionOriginResolver.TryBuildPath(Tile, originTile, out var directReturnPath))
                {
                    newPath = directReturnPath;
                }
            }
            if (newPath.Count <= 1 && (!originTile.Valid || Tile == originTile))
            {
                if (IncursionOriginResolver.TryResolveRetreatRoute(Tile, Faction, out var destTile, out var retreatPath, out var speed))
                {
                    destinationTile = destTile;
                    newPath = retreatPath;
                    if (speed > 0f) speedTilesPerDay = speed;
                }
            }

            pathTiles = newPath;
            currentPathIndex = 0;
            tileProgress = 0f;
            if (newPath.Count > 0)
            {
                Tile = newPath[0];
            }
            if (!destinationTile.Valid || destinationTile == Tile)
            {
                destinationTile = originTile.Valid ? originTile : (newPath.Count > 0 ? newPath[newPath.Count - 1] : Tile);
            }
        }
        else
        {
            if (IncursionOriginResolver.TryResolveRetreatRoute(Tile, Faction, out var destTile, out var retreatPath, out var speed))
            {
                destinationTile = destTile;
                pathTiles = retreatPath;
                currentPathIndex = 0;
                tileProgress = 0f;
                if (speed > 0f) speedTilesPerDay = speed;
                if (pathTiles.Count > 0) Tile = pathTiles[0];
            }
        }
        Log.Message($"[Project Vigilance] Incursion {TrackingId} ({LabelCap}) reversed course. Reason: {reason ?? "none"}. Returning to tile {destinationTile}.");
    }

    private void CheckInterceptionsAndPursuits()
    {
        if (!Spawned)
        {
            return;
        }

        bool isHostile = Faction?.HostileTo(Faction.OfPlayer) ?? true;

        // 1. Check for collision with an active player Camp or Settlement or Battlefield on this tile
        if (true)
        {
            Map? tileMap = Current.Game?.FindMap(Tile);
            MapParent? mapParent = Find.WorldObjects?.MapParentAt(Tile);

            if (tileMap != null && !tileMap.IsPocketMap && tileMap.mapPawns != null && tileMap.mapPawns.AnyFreeColonistSpawned)
            {
                var ambushComp = tileMap.GetComponent<MapComponent_OverlandAmbush>();
                bool alreadyEngaged = this.IsEngagedInTacticalMap || (ambushComp != null && ambushComp.HasTrackedIncursion(this));

                if (!alreadyEngaged)
                {
                    if (tileMap.IsPlayerHome)
                    {
                        if (isHostile && !isRetreating)
                        {
                            IncursionInterceptionUtility.ExecuteCampOrSettlementAssault(tileMap, this);
                            return;
                        }
                        else if (!isHostile)
                        {
                            int mapId = tileMap.Parent?.ID ?? tileMap.uniqueID;
                            if (!metCaravanIds.Contains(mapId))
                            {
                                metCaravanIds.Add(mapId);
                                IncursionInterceptionUtility.ExecuteFriendlyCampOrSettlementVisit(tileMap, this);
                            }
                        }
                    }
                    else
                    {
                        // Temporary camp or CaravansBattlefield: Transit across the battlescape!
                        int mapId = tileMap.Parent?.ID ?? tileMap.uniqueID;
                        if (!metCaravanIds.Contains(mapId))
                        {
                            metCaravanIds.Add(mapId);
                            IncursionInterceptionUtility.ExecuteBattlescapeTransit(tileMap, this);
                            return;
                        }
                    }
                }
            }
            else if (mapParent is Settlement settlement && settlement.Faction == Faction.OfPlayer)
            {
                if (isHostile && !isRetreating)
                {
                    destinationTile = Tile;
                    IncursionArrivalManager.ExecuteArrival(this);
                    return;
                }
            }
        }

        // 2. Check for player caravans on the world map
        var worldCaravans = Find.WorldObjects?.Caravans;
        if (worldCaravans == null || worldCaravans.Count == 0)
        {
            return;
        }

        // Clean up any caravans no longer on this tile that are not on active cooldown
        metCaravanIds.RemoveWhere(id =>
        {
            var cv = worldCaravans.FirstOrDefault(c => c.ID == id);
            return cv == null || cv.Tile != Tile;
        });

        // 2a. Dynamic pursuit tracking: caravans with intercept actions targeted at this incursion
        for (int i = 0; i < worldCaravans.Count; i++)
        {
            Caravan c = worldCaravans[i];
            if (c == null || !c.Spawned || c.Faction != Faction.OfPlayer)
            {
                continue;
            }

            if (c.pather?.ArrivalAction is CaravanArrivalAction_InterceptIncursion interceptAction && interceptAction.TargetIncursion == this)
            {
                // Reached same tile -> execute interception immediately
                if (c.Tile == Tile)
                {
                    IncursionInterceptionUtility.ExecuteInterception(c, this, isAmbush: false);
                    return;
                }

                // Head-on shared-edge crossing:
                // If the caravan is on our immediate next tile and either waiting for us at the border
                // or marching directly into our current tile while we march into theirs, engage where the icons meet!
                PlanetTile nextTile = NextPathTile;
                if (nextTile.Valid && nextTile != Tile && c.Tile == nextTile)
                {
                    bool caravanMovingIntoUs = c.pather is { Moving: true } && c.pather.nextTile == Tile;
                    float caravanProgress = caravanMovingIntoUs
                        ? 1f - Mathf.Clamp01(c.pather.nextTileCostLeft / Mathf.Max(1f, c.pather.nextTileCostTotal))
                        : 0f;

                    if ((caravanMovingIntoUs && (tileProgress + caravanProgress) >= 0.95f) ||
                        (!caravanMovingIntoUs && tileProgress >= 0.85f))
                    {
                        AdvancePathTile();
                        IncursionInterceptionUtility.ExecuteInterception(c, this, isAmbush: false);
                        return;
                    }
                }

                // Dynamic pursuit tracking: plot X-COM lead intercept tile along incursion heading
                var sensoryManager = Find.World?.GetComponent<SensoryManager>();
                bool hasVision = sensoryManager != null && sensoryManager.HasActiveVision(Tile);
                if (GeoscapeMod.Settings.visibilityMode == VisibilityMode.Omniscient || intelligenceTier >= IntelligenceTier.Contact || hasVision)
                {
                    PlanetTile targetTile = GetInterceptTargetTile(c);
                    if (c.pather.Destination != targetTile)
                    {
                        c.pather.StartPath(targetTile, interceptAction, resetPauseStatus: false);
                    }
                }
                else if (c.pather.Destination != Tile && c.pather.Destination != NextPathTile)
                {
                    IncursionInterceptionUtility.CheckContactLost(c, this);
                }
            }
        }

        // 2b. Direct tile collision: caravans occupying this hex
        if (isRetreating && Tile == originTile)
        {
            return;
        }

        var caravansOnTile = worldCaravans
            .Where(c => c != null && c.Spawned && c.Faction == Faction.OfPlayer && c.Tile == Tile &&
                        !(c.pather?.ArrivalAction is CaravanArrivalAction_InterceptIncursion intercept && intercept.TargetIncursion == this))
            .ToList();

        if (caravansOnTile.Count == 0)
        {
            return;
        }

        // Filter out caravans that have already met or are on encounter cooldown
        var eligibleCaravans = caravansOnTile
            .Where(c => !metCaravanIds.Contains(c.ID) && !IsCaravanOnCooldown(c.ID))
            .ToList();

        if (eligibleCaravans.Count == 0)
        {
            return;
        }

        if (!isHostile)
        {
            IncursionInterceptionUtility.ExecuteFriendlyInterception(eligibleCaravans, this);
        }
        else
        {
            IncursionInterceptionUtility.ExecuteHostileInterception(eligibleCaravans, this, isAmbush: true);
        }
    }

    public PlanetTile NextPathTile =>
        (pathTiles != null && currentPathIndex + 1 < pathTiles.Count)
            ? pathTiles[currentPathIndex + 1]
            : Tile;

    /// <summary>
    /// Computes the X-COM style predictive intercept hex for a pursuing player caravan.
    /// Instead of aiming at the hex the incursion is already walking out of, leads moving targets
    /// to their next transit hex when appropriate.
    /// </summary>
    public PlanetTile GetInterceptTargetTile(Caravan? caravan)
    {
        if (caravan == null || !caravan.Spawned || !Tile.Valid || pathTiles == null || pathTiles.Count == 0)
        {
            return Tile;
        }

        if (caravan.Tile == Tile)
        {
            return Tile;
        }

        // If the incursion is stationary (mobilizing, resting for the night, in combat, or at final tile), target its current hex
        if (IsMobilizing || IsRestingAtNight || isEngagedInTacticalMap || currentPathIndex >= pathTiles.Count - 1)
        {
            return Tile;
        }

        PlanetTile nextTile = pathTiles[currentPathIndex + 1];

        // If the caravan is already standing on nextTile (directly in front of the incursion),
        // step toward Tile if the incursion is still in the first half of Tile, or hold on nextTile if it's crossing the border
        if (caravan.Tile == nextTile)
        {
            return tileProgress >= 0.55f ? nextTile : Tile;
        }

        // Do not lead into a player home settlement tile on the very last step if the caravan is trying to intercept in the field outside the base
        bool nextIsFinalPlayerSettlement = (currentPathIndex + 1 == pathTiles.Count - 1) &&
                                           (Current.Game?.FindMap(nextTile)?.IsPlayerHome == true);
        if (nextIsFinalPlayerSettlement && tileProgress < 0.75f)
        {
            return Tile;
        }

        float distToCur = Find.WorldGrid.ApproxDistanceInTiles(caravan.Tile, Tile);
        float distToNext = Find.WorldGrid.ApproxDistanceInTiles(caravan.Tile, nextTile);

        // Lead the target to nextTile when:
        // 1. The incursion is past the center of its current hex (heading toward the exit edge into nextTile), OR
        // 2. nextTile is closer or equal distance to the intercepting caravan's approach vector, OR
        // 3. The caravan is more than 1 hex away and chasing a moving target
        if (tileProgress >= 0.40f || distToNext <= distToCur + 0.25f || distToCur > 1.35f)
        {
            return nextTile;
        }

        return Tile;
    }

    /// <summary>
    /// Edge-to-Edge Within-Hex Interpolation:
    /// Rather than interpolating from Center[i] to Center[i+1] (which visually places the icon inside Hex[i+1]
    /// while Tile is still Hex[i] for tileProgress 0.50..0.99), interpolates across the current hex's physical span:
    ///   - Entry Border (shared midpoint with Hex[i-1]) at tileProgress = 0.0
    ///   - Hex Center (Center[i]) at tileProgress = 0.5
    ///   - Exit Border (shared midpoint with Hex[i+1]) at tileProgress = 1.0
    /// Because ExitBorder[i] == EntryBorder[i+1], motion is 100% C0-continuous while guaranteeing the icon is
    /// ALWAYS visually inside its actual Tile and crosses the hex border at the exact tick Tile updates.
    /// </summary>
    public Vector3 TrueDrawPos
    {
        get
        {
            if (pathTiles == null || pathTiles.Count == 0 || !Spawned)
            {
                return base.DrawPos;
            }

            if (departureDelayTicks > 0)
            {
                return Find.WorldGrid.GetTileCenter(originTile.Valid ? originTile : Tile);
            }

            if (currentPathIndex >= pathTiles.Count - 1)
            {
                int lastIdx = pathTiles.Count - 1;
                Vector3 finalCenter = Find.WorldGrid.GetTileCenter(pathTiles[lastIdx]);
                if (lastIdx > 0)
                {
                    Vector3 prevCenter = Find.WorldGrid.GetTileCenter(pathTiles[lastIdx - 1]);
                    Vector3 entryEdge = Vector3.Slerp(prevCenter, finalCenter, 0.5f);
                    return Vector3.Slerp(entryEdge, finalCenter, Mathf.Clamp01(tileProgress * 2f));
                }
                return finalCenter;
            }

            PlanetTile curTile = pathTiles[currentPathIndex];
            PlanetTile nextTile = pathTiles[currentPathIndex + 1];

            Vector3 curCenter = Find.WorldGrid.GetTileCenter(curTile);
            Vector3 nextCenter = Find.WorldGrid.GetTileCenter(nextTile);
            Vector3 exitEdge = Vector3.Slerp(curCenter, nextCenter, 0.5f);

            float t = Mathf.Clamp01(tileProgress);

            // Origin tile (index 0): starts at origin center and moves to the first hex border at t = 1.0
            if (currentPathIndex == 0)
            {
                return Vector3.Slerp(curCenter, exitEdge, t);
            }

            // Transit tiles (index > 0):
            // t in [0.0, 0.5] -> Entry Edge (border with previous tile) to Current Tile Center
            // t in [0.5, 1.0] -> Current Tile Center to Exit Edge (border with next tile)
            Vector3 prevTileCenter = Find.WorldGrid.GetTileCenter(pathTiles[currentPathIndex - 1]);
            Vector3 entryEdgePos = Vector3.Slerp(prevTileCenter, curCenter, 0.5f);

            if (t <= 0.5f)
            {
                return Vector3.Slerp(entryEdgePos, curCenter, t * 2f);
            }

            return Vector3.Slerp(curCenter, exitEdge, (t - 0.5f) * 2f);
        }
    }

    public override Vector3 DrawPos
    {
        get
        {
            // In Contact Tier, position updates discretely on 250-tick sweeps rather than 60 FPS continuous Slerp
            if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient && intelligenceTier == IntelligenceTier.Contact)
            {
                if (lastSampledDrawPos != Vector3.zero)
                {
                    return lastSampledDrawPos;
                }
            }

            return TrueDrawPos;
        }
    }

    private static Material? cachedHostileLineMat;
    private static Material? cachedHostilePastLineMat;
    private static Material? cachedFriendlyLineMat;
    private static Material? cachedFriendlyPastLineMat;

    private static Material HostileLineMat =>
        cachedHostileLineMat ??= MaterialPool.MatFrom(
            GenDraw.LineTexPath,
            ShaderDatabase.WorldOverlayTransparent,
            new Color(1f, 0.25f, 0.25f, 0.95f),
            WorldMaterials.WorldLineRenderQueue);

    private static Material HostilePastLineMat =>
        cachedHostilePastLineMat ??= MaterialPool.MatFrom(
            GenDraw.LineTexPath,
            ShaderDatabase.WorldOverlayTransparent,
            new Color(0.85f, 0.2f, 0.2f, 0.35f),
            WorldMaterials.WorldLineRenderQueue);

    private static Material FriendlyLineMat =>
        cachedFriendlyLineMat ??= MaterialPool.MatFrom(
            GenDraw.LineTexPath,
            ShaderDatabase.WorldOverlayTransparent,
            new Color(0.25f, 0.85f, 1f, 0.95f),
            WorldMaterials.WorldLineRenderQueue);

    private static Material FriendlyPastLineMat =>
        cachedFriendlyPastLineMat ??= MaterialPool.MatFrom(
            GenDraw.LineTexPath,
            ShaderDatabase.WorldOverlayTransparent,
            new Color(0.25f, 0.85f, 1f, 0.35f),
            WorldMaterials.WorldLineRenderQueue);

    private static Vector3 GetElevatedTileCenter(PlanetTile tile)
    {
        Vector3 pos = Find.WorldGrid.GetTileCenter(tile);
        return pos + pos.normalized * 0.08f;
    }

    public override void DrawExtraSelectionOverlays()
    {
        if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient && intelligenceTier < IntelligenceTier.Contact)
        {
            return;
        }

        base.DrawExtraSelectionOverlays();
        DrawPathOverlay();
    }

    private void DrawPathOverlay()
    {
        if (!WorldRendererUtility.WorldRendered || pathTiles == null || pathTiles.Count < 2)
        {
            return;
        }

        List<PlanetTile> tiles = pathTiles;

        // In Contact tier, do not reveal the entire projected planetary path to the player
        if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient && intelligenceTier < IntelligenceTier.Classified)
        {
            Vector3 curDrawPos = DrawPos + DrawPos.normalized * 0.08f;
            int anchorIdx = (currentPathIndex > 0 && tileProgress < 0.5f) ? currentPathIndex - 1 : currentPathIndex;
            if (anchorIdx < tiles.Count)
            {
                Vector3 pAnchor = GetElevatedTileCenter(tiles[anchorIdx]);
                if ((curDrawPos - pAnchor).sqrMagnitude > 0.001f)
                {
                    GenDraw.DrawWorldLineBetween(pAnchor, curDrawPos, FriendlyPastLineMat, 1.0f);
                }
            }
            return;
        }

        bool isHostile = Faction?.HostileTo(Faction.OfPlayer) ?? true;
        Material activeMat = isHostile ? HostileLineMat : FriendlyLineMat;
        Material pastMat = isHostile ? HostilePastLineMat : FriendlyPastLineMat;

        // If mobilizing at origin base, draw the full planned path as active
        if (departureDelayTicks > 0)
        {
            for (int i = 0; i < tiles.Count - 1; i++)
            {
                Vector3 pA = GetElevatedTileCenter(tiles[i]);
                Vector3 pB = GetElevatedTileCenter(tiles[i + 1]);
                GenDraw.DrawWorldLineBetween(pA, pB, activeMat, 1.2f);
            }
            return;
        }

        Vector3 curDrawPos2 = DrawPos + DrawPos.normalized * 0.08f;
        bool beforeHexCenter = currentPathIndex > 0 && tileProgress < 0.5f;

        if (beforeHexCenter)
        {
            // Icon is between EntryEdge[i] and Center[i] (segment tiles[i-1] -> tiles[i])
            for (int i = 0; i < currentPathIndex - 1; i++)
            {
                Vector3 pA = GetElevatedTileCenter(tiles[i]);
                Vector3 pB = GetElevatedTileCenter(tiles[i + 1]);
                GenDraw.DrawWorldLineBetween(pA, pB, pastMat, 1.0f);
            }

            Vector3 pPrev = GetElevatedTileCenter(tiles[currentPathIndex - 1]);
            if ((curDrawPos2 - pPrev).sqrMagnitude > 0.001f)
            {
                GenDraw.DrawWorldLineBetween(pPrev, curDrawPos2, pastMat, 1.0f);
            }

            Vector3 pCur = GetElevatedTileCenter(tiles[currentPathIndex]);
            if ((pCur - curDrawPos2).sqrMagnitude > 0.001f)
            {
                GenDraw.DrawWorldLineBetween(curDrawPos2, pCur, activeMat, 1.2f);
            }

            for (int i = currentPathIndex; i < tiles.Count - 1; i++)
            {
                Vector3 pA = GetElevatedTileCenter(tiles[i]);
                Vector3 pB = GetElevatedTileCenter(tiles[i + 1]);
                GenDraw.DrawWorldLineBetween(pA, pB, activeMat, 1.2f);
            }
        }
        else
        {
            // Icon is between Center[i] and ExitEdge[i] (segment tiles[i] -> tiles[i+1])
            for (int i = 0; i < currentPathIndex; i++)
            {
                Vector3 pA = GetElevatedTileCenter(tiles[i]);
                Vector3 pB = GetElevatedTileCenter(tiles[i + 1]);
                GenDraw.DrawWorldLineBetween(pA, pB, pastMat, 1.0f);
            }

            if (currentPathIndex < tiles.Count)
            {
                Vector3 pCur = GetElevatedTileCenter(tiles[currentPathIndex]);
                if ((curDrawPos2 - pCur).sqrMagnitude > 0.001f)
                {
                    GenDraw.DrawWorldLineBetween(pCur, curDrawPos2, pastMat, 1.0f);
                }
            }

            if (currentPathIndex + 1 < tiles.Count)
            {
                Vector3 pNext = GetElevatedTileCenter(tiles[currentPathIndex + 1]);
                if ((pNext - curDrawPos2).sqrMagnitude > 0.001f)
                {
                    GenDraw.DrawWorldLineBetween(curDrawPos2, pNext, activeMat, 1.2f);
                }

                for (int i = currentPathIndex + 1; i < tiles.Count - 1; i++)
                {
                    Vector3 pA = GetElevatedTileCenter(tiles[i]);
                    Vector3 pB = GetElevatedTileCenter(tiles[i + 1]);
                    GenDraw.DrawWorldLineBetween(pA, pB, activeMat, 1.2f);
                }
            }
        }
    }

    public override string GetInspectString()
    {
        if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient && intelligenceTier < IntelligenceTier.Contact)
        {
            return string.Empty;
        }

        bool isClassified = GeoscapeMod.Settings.visibilityMode == VisibilityMode.Omniscient ||
                            (isIdentified && intelligenceTier >= IntelligenceTier.Classified);

        string idTag = trackingId > 0
            ? (SensoryNarrativeUtility.IsLowTech ? $" [#{trackingId}]" : $" [Contact-{trackingId}]")
            : string.Empty;
        string factionName = isClassified
            ? (Faction?.Name ?? "Unknown")
            : (SensoryNarrativeUtility.IsLowTech ? "Unknown Faction" : "Unidentified Faction");
        string label = isClassified
            ? $"{GetIdentifiedGroupLabel()}{idTag}"
            : SensoryNarrativeUtility.GetContactLabel(trackingId: trackingId);

        if (isEngagedInTacticalMap)
        {
            int combatantCount = committedPawns?.Count(p => p != null && !p.Dead && !p.Destroyed) ?? 0;
            string countStr = combatantCount > 0 ? $" ({combatantCount} combatants)" : string.Empty;
            if (!isClassified)
            {
                return $"{label.CapitalizeFirst()}\nStatus: Engaged in active combat{countStr}\nLocation: Hex {Tile}";
            }
            return $"{label.CapitalizeFirst()} ({factionName})\nStatus: Engaged in active combat{countStr}\nLocation: Hex {Tile}";
        }

        if (isRetreating)
        {
            int survivorCount = retreatingPawns?.Count(p => p != null && !p.Dead && !p.Destroyed) ?? committedPawns?.Count(p => p != null && !p.Dead && !p.Destroyed) ?? 0;
            int captiveCount = kidnappedColonists?.Count ?? 0;
            string captiveStr = captiveCount > 0 ? $"\nCaptives: {captiveCount} abducted colonist(s)" : string.Empty;
            if (!isClassified)
            {
                return $"{label.CapitalizeFirst()}\nStatus: Retreating ({survivorCount} survivors){captiveStr}\nSpeed: ~{speedTilesPerDay:F0} hexes/day";
            }
            return $"{label.CapitalizeFirst()} ({factionName})\nStatus: Retreating ({survivorCount} survivors){captiveStr}\nDestination: Tile {destinationTile}";
        }

        bool isMechanoid = Faction != null && (Faction.def == FactionDefOf.Mechanoid || Faction.def.defName.Contains("Mechanoid"));
        float effectiveSpeed = (GeoscapeMod.Settings.incursionsRestAtNight && !isMechanoid)
            ? speedTilesPerDay * (16f / 24f)
            : speedTilesPerDay;
        float actualRemainingTiles = Math.Max(0f, (pathTiles != null ? (pathTiles.Count - currentPathIndex) : 0) - tileProgress);
        float remainingDays = actualRemainingTiles / Math.Max(0.1f, effectiveSpeed);
        float remainingHours = remainingDays * 24f;

        if (isTurningBack)
        {
            if (!isClassified)
            {
                return $"{label.CapitalizeFirst()}\nStatus: Returning home\nSpeed: ~{speedTilesPerDay:F0} hexes/day";
            }
            return $"{label.CapitalizeFirst()} ({factionName})\nStatus: Returning home\nDestination: Tile {destinationTile}\nETA: ~{remainingHours:F1} hours ({Mathf.CeilToInt(actualRemainingTiles)} hexes remaining)";
        }

        if (departureDelayTicks > 0)
        {
            float delayHours = departureDelayTicks / 2500f;
            int totalTiles = Math.Max(0, pathTiles?.Count ?? 0);
            float transitHours = (totalTiles / Math.Max(0.1f, effectiveSpeed)) * 24f;
            if (!isClassified)
            {
                return $"{label.CapitalizeFirst()}\nMobilizing (Departing in ~{delayHours:F1} hours)\nEst. Transit: ~{transitHours:F1} hours ({totalTiles} hexes)";
            }
            return $"{label.CapitalizeFirst()} ({factionName})\nMobilizing at base (Departing in ~{delayHours:F1} hours)\nEst. Transit: ~{transitHours:F1} hours ({totalTiles} hexes)";
        }

        if (IsRestingAtNight)
        {
            float restHoursLeft = CaravanNightRestUtility.LeftRestTicksAt(Tile) / 2500f;
            remainingHours += restHoursLeft;

            if (!isClassified)
            {
                if (SensoryNarrativeUtility.IsLowTech)
                {
                    string designation = trackingId > 0 ? $"Approaching Strangers #{trackingId}" : "Approaching Strangers";
                    return $"{designation}\nStatus: Camped for the night\nPace: Stationary (Resuming march at dawn, ~{restHoursLeft:F1}h)\nObservations: Campfires spotted on horizon";
                }
                string highTechDesignation = trackingId > 0 ? $"Contact-{trackingId}" : "Unidentified Contact";
                return $"{highTechDesignation}\nStatus: Stationary (Camped at night)\nPace: Zero movement telemetry (~{restHoursLeft:F1}h until dawn)";
            }

            return $"{label.CapitalizeFirst()} ({factionName})\nCamped for the night (Resuming march at dawn, ~{restHoursLeft:F1}h)\nETA: ~{remainingHours:F1} hours ({Mathf.CeilToInt(actualRemainingTiles)} hexes remaining)";
        }

        if (!isClassified)
        {
            if (SensoryNarrativeUtility.IsLowTech)
            {
                string designation = trackingId > 0 ? $"Approaching Strangers #{trackingId}" : "Approaching Strangers";
                return $"{designation}\nStatus: Spotted in the distance\nPace: ~{speedTilesPerDay:F0} hexes/day\nObservations: Identity and intent unknown from this distance";
            }
            string highTechDesignation = trackingId > 0 ? $"Contact-{trackingId}" : "Unidentified Contact";
            return $"{highTechDesignation}\nStatus: Tracking in progress\nSpeed: ~{speedTilesPerDay:F0} hexes/day\nTelemetry: Insufficient sensor data to classify faction";
        }

        Map? targetMap = Current.Game.FindMap(destinationTile);
        if (targetMap == null || !Find.Maps.Contains(targetMap))
        {
            return $"{label.CapitalizeFirst()} ({factionName})\nTarget settlement is abandoned\nETA: ~{remainingHours:F1} hours ({Mathf.CeilToInt(actualRemainingTiles)} hexes remaining)";
        }

        return $"{label.CapitalizeFirst()} ({factionName})\nETA: ~{remainingHours:F1} hours ({Mathf.CeilToInt(actualRemainingTiles)} hexes remaining)";
    }

    public override string GetDescription()
    {
        bool isClassified = GeoscapeMod.Settings.visibilityMode == VisibilityMode.Omniscient ||
                            (isIdentified && intelligenceTier >= IntelligenceTier.Classified);

        if (!isClassified)
        {
            return SensoryNarrativeUtility.GetContactDescription();
        }

        switch (GroupKind)
        {
            case IncursionGroupKind.HostileWarband:
                return $"A hostile warband from {Faction?.Name ?? "an unknown enemy faction"} traversing the planetary surface towards colony territory.";
            case IncursionGroupKind.TradeCaravan:
                if (isTradeMissionAborted)
                {
                    return $"A surviving merchant group from {Faction?.Name ?? "a friendly faction"} returning to base after losing their head merchant.";
                }
                string traderDetail = TraderKind != null ? $" ({TraderKind.label})" : string.Empty;
                return $"A merchant trade caravan{traderDetail} from {Faction?.Name ?? "a friendly faction"} travelling with trade goods and pack animals.";
            case IncursionGroupKind.Visitors:
                return $"A party of visitors from {Faction?.Name ?? "a friendly faction"} travelling to socialise and visit your settlement.";
            case IncursionGroupKind.Travelers:
                return $"A group of travellers from {Faction?.Name ?? "a neutral faction"} passing peacefully through the region.";
            case IncursionGroupKind.TributeCollector:
                return $"An imperial royal tribute collector from {Faction?.Name ?? "the Empire"} seeking tithes of gold or prisoners.";
            case IncursionGroupKind.Pilgrims:
                return $"A group of pilgrims from {Faction?.Name ?? "a friendly faction"} travelling on a spiritual journey.";
            case IncursionGroupKind.Beggars:
                return "A group of impoverished travellers in desperate need of charity and aid.";
            case IncursionGroupKind.AlliedReinforcements:
                return $"An allied military force from {Faction?.Name ?? "an allied faction"} marching to reinforce your colony.";
            default:
                return $"A travelling party from {Faction?.Name ?? "an unknown faction"} traversing the planetary surface.";
        }
    }

    // =========================================================================
    // ITrader & IThingHolder Implementations (Roadside Persistent Trading)
    // =========================================================================

    public TraderKindDef? TraderKind => IsUnderlyingTradeCaravan
        ? (explicitTraderKind ?? incidentParms?.traderKind ?? Faction?.def?.caravanTraderKinds?.FirstOrDefault())
        : null;

    public IEnumerable<Thing> Goods => IsUnderlyingTradeCaravan ? GetOrCreateTradeStock() : Enumerable.Empty<Thing>();

    public int RandomPriceFactorSeed => ID;

    public string TraderName => LabelCap;

    public bool CanTradeNow => IsUnderlyingTradeCaravan && !isTradeMissionAborted && Faction != null && !Faction.HostileTo(Faction.OfPlayer);

    public float TradePriceImprovementOffsetForPlayer => 0f;

    public TradeCurrency TradeCurrency => TraderKind?.tradeCurrency ?? TradeCurrency.Silver;

    public IEnumerable<Thing> ColonyThingsWillingToBuy(Pawn playerNegotiator)
    {
        Caravan? caravan = playerNegotiator?.GetCaravan();
        if (caravan != null)
        {
            foreach (Thing t in CaravanInventoryUtility.AllInventoryItems(caravan))
            {
                yield return t;
            }
        }
    }

    public void GiveSoldThingToTrader(Thing toGive, int countToGive, Pawn playerNegotiator)
    {
        Thing split = toGive.SplitOff(countToGive);
        GetOrCreateTradeStock().TryAdd(split, canMergeWithExistingStacks: true);
    }

    public void GiveSoldThingToPlayer(Thing toGive, int countToGive, Pawn playerNegotiator)
    {
        Caravan? caravan = playerNegotiator?.GetCaravan();
        Thing split = toGive.SplitOff(countToGive);
        if (caravan != null)
        {
            CaravanInventoryUtility.GiveThing(caravan, split);
        }
        else if (playerNegotiator?.inventory != null)
        {
            playerNegotiator.inventory.innerContainer.TryAdd(split);
        }
    }

    public ThingOwner<Thing> GetOrCreateTradeStock()
    {
        if (tradeStock == null)
        {
            tradeStock = new ThingOwner<Thing>(this);
            if (IsUnderlyingTradeCaravan)
            {
                // First check if committed or retreating pawns already carry wares from generation
                bool foundAnyItemsInCarriers = false;
                var sourcePawns = committedPawns ?? retreatingPawns;
                if (sourcePawns != null && sourcePawns.Count > 0)
                {
                    for (int i = 0; i < sourcePawns.Count; i++)
                    {
                        Pawn p = sourcePawns[i];
                        if (p?.inventory?.innerContainer != null && p.inventory.innerContainer.Count > 0)
                        {
                            foundAnyItemsInCarriers = true;
                            var items = p.inventory.innerContainer.ToList();
                            for (int it = 0; it < items.Count; it++)
                            {
                                Thing t = items[it];
                                p.inventory.innerContainer.Remove(t);
                                tradeStock.TryAdd(t, canMergeWithExistingStacks: true);
                            }
                        }
                    }
                }

                if (!foundAnyItemsInCarriers && TraderKind != null)
                {
                    ThingSetMakerParams parms = default;
                    parms.traderDef = TraderKind;
                    parms.makingFaction = Faction;
                    parms.tile = Tile;
                    List<Thing> generated = ThingSetMakerDefOf.TraderStock.root.Generate(parms);
                    for (int i = 0; i < generated.Count; i++)
                    {
                        tradeStock.TryAdd(generated[i], canMergeWithExistingStacks: false);
                    }
                }
            }
        }
        return tradeStock;
    }

    /// <summary>
    /// Transfers all items held in tradeStock onto carrier pack animals (or humanoids if pack animals are absent)
    /// so the caravan pawns physically transport their trade cargo on tactical battle and transit maps.
    /// </summary>
    public void SyncCargoFromTradeStockToPawns(List<Pawn> pawns)
    {
        if (!IsUnderlyingTradeCaravan || tradeStock == null || tradeStock.Count == 0 || pawns == null || pawns.Count == 0) return;

        var carriers = pawns.Where(p => p != null && !p.Dead && p.inventory != null && p.RaceProps != null && p.RaceProps.packAnimal).ToList();
        if (carriers.Count == 0)
        {
            carriers = pawns.Where(p => p != null && !p.Dead && p.inventory != null).ToList();
        }

        if (carriers.Count > 0)
        {
            int carrierIdx = 0;
            var stockList = tradeStock.ToList();
            for (int s = 0; s < stockList.Count; s++)
            {
                Thing item = stockList[s];
                tradeStock.Remove(item);
                Pawn carrier = carriers[carrierIdx % carriers.Count];
                carrier.inventory.innerContainer.TryAdd(item, canMergeWithExistingStacks: true);
                carrierIdx++;
            }
        }
    }

    /// <summary>
    /// Gathers all items from carrier pawns' inventories back into tradeStock when transitioning from tactical maps
    /// to the Geoscape, ensuring dropped cargo from casualties stays on the battlefield while surviving cargo persists.
    /// </summary>
    public void SyncCargoFromPawnsToTradeStock(List<Pawn> pawns)
    {
        if (!IsUnderlyingTradeCaravan || pawns == null || pawns.Count == 0) return;
        var stock = GetOrCreateTradeStock();
        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn p = pawns[i];
            if (p?.inventory?.innerContainer != null && p.inventory.innerContainer.Count > 0)
            {
                var items = p.inventory.innerContainer.ToList();
                for (int it = 0; it < items.Count; it++)
                {
                    Thing t = items[it];
                    p.inventory.innerContainer.Remove(t);
                    stock.TryAdd(t, canMergeWithExistingStacks: true);
                }
            }
        }
    }

    public void GetChildHolders(List<IThingHolder> outChildren)
    {
        ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
    }

    public ThingOwner GetDirectlyHeldThings() => GetOrCreateTradeStock();

    // =========================================================================
    // Overland Physiological Simulation (Retreating Raiders & Captives)
    // =========================================================================

    private void TickCommittedPawnsPhysiology(int interval)
    {
        if (committedPawns != null && committedPawns.Count > 0)
        {
            SimulatePawnsPhysiology(committedPawns, interval, "Incursion combatant");
        }
    }

    private void TickRetreatingPawnsPhysiology(int interval)
    {
        if (retreatingPawns != null && retreatingPawns.Count > 0)
        {
            SimulatePawnsPhysiology(retreatingPawns, interval, "Retreating raider");
        }

        if (retreatingPawns == null || retreatingPawns.Count == 0)
        {
            if (kidnappedColonists != null && kidnappedColonists.Count > 0)
            {
                string captivesList = string.Join(", ", kidnappedColonists.Select(c => c.LabelShort));
                LetterDef posDef = PV_LetterDefOf.PV_TacticalEncounter_Positive ?? LetterDefOf.PositiveEvent;
                var letter = (ChoiceLetter_TacticalEncounter)LetterMaker.MakeLetter(posDef);
                letter.label = "Captors Succumbed: " + LabelCap;
                letter.text = $"All captors holding {captivesList} have died of their wounds on the overland trail!\n\n" +
                              $"{captivesList} remains stranded at tile {Tile}. A rescue caravan can now be dispatched to recover them safely.";
                letter.targetWorldObject = this;
                letter.targetTile = Tile;
                letter.worldButtonLabel = "Go to cell (Geoscape)";
                letter.lookTargets = new GlobalTargetInfo(Tile);
                Find.LetterStack.ReceiveLetter(letter);
            }
            return;
        }

        Pawn? doctor = retreatingPawns
            .Where(p => !p.Dead && !p.Downed && p.health?.capacities?.CanBeAwake == true)
            .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0)
            .FirstOrDefault();

        Medicine? carriedMed = null;
        foreach (var p in retreatingPawns)
        {
            if (p.inventory?.innerContainer != null)
            {
                var med = p.inventory.innerContainer.FirstOrDefault(t => t is Medicine) as Medicine;
                if (med != null)
                {
                    carriedMed = med;
                    break;
                }
            }
        }

        if (kidnappedColonists != null && kidnappedColonists.Count > 0)
        {
            for (int i = kidnappedColonists.Count - 1; i >= 0; i--)
            {
                Pawn captive = kidnappedColonists[i];
                if (captive == null)
                {
                    kidnappedColonists.RemoveAt(i);
                    continue;
                }

                if (!captive.Dead)
                {
                    try
                    {
                        captive.health?.HealthTickInterval(interval);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[Project Vigilance] Error ticking health for captive {captive.LabelShort}: {ex.Message}");
                    }

                    if (captive.Dead)
                    {
                        Messages.Message($"Captured colonist {captive.LabelShort} has died of their wounds while in enemy hands.", new GlobalTargetInfo(Tile), MessageTypeDefOf.NegativeEvent);
                    }
                    else
                    {
                        if (doctor != null && captive.health != null && captive.health.HasHediffsNeedingTend())
                        {
                            TendUtility.DoTend(doctor, captive, carriedMed);
                        }

                        if (captive.needs?.food != null)
                        {
                            captive.needs.food.CurLevel -= 0.0005f * (interval / 250f);

                            // Captors feed prisoners from carried provisions
                            if (captive.needs.food.CurLevelPercentage < 0.3f)
                            {
                                foreach (var captor in retreatingPawns)
                                {
                                    Thing? food = captor.inventory?.innerContainer?.FirstOrDefault(t => t.def.IsNutritionGivingIngestible);
                                    if (food != null)
                                    {
                                        float nut = food.def.ingestible?.CachedNutrition ?? 0.5f;
                                        captive.needs.food.CurLevel += nut;
                                        food.SplitOff(1).Destroy();
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        if (retreatingPawns.Count == 0 && kidnappedColonists != null && kidnappedColonists.Count > 0)
        {
            string captivesList = string.Join(", ", kidnappedColonists.Select(c => c.LabelShort));
            LetterDef posDef = PV_LetterDefOf.PV_TacticalEncounter_Positive ?? LetterDefOf.PositiveEvent;
            var letter = (ChoiceLetter_TacticalEncounter)LetterMaker.MakeLetter(posDef);
            letter.label = "Captors Succumbed: " + LabelCap;
            letter.text = $"All captors holding {captivesList} have died of their wounds on the overland trail!\n\n" +
                          $"{captivesList} remains stranded at tile {Tile}. A rescue caravan can now be dispatched to recover them safely.";
            letter.targetWorldObject = this;
            letter.targetTile = Tile;
            letter.worldButtonLabel = "Go to cell (Geoscape)";
            letter.lookTargets = new GlobalTargetInfo(Tile);
            Find.LetterStack.ReceiveLetter(letter);
        }
    }

    private void SimulatePawnsPhysiology(List<Pawn> pawns, int interval, string pawnLabel)
    {
        if (pawns == null || pawns.Count == 0) return;

        bool restingNow = IsRestingAtNight;

        Pawn? doctor = pawns
            .Where(p => !p.Dead && !p.Downed && p.health?.capacities?.CanBeAwake == true)
            .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0)
            .FirstOrDefault();

        Medicine? carriedMed = null;
        foreach (var p in pawns)
        {
            if (p.inventory?.innerContainer != null)
            {
                var med = p.inventory.innerContainer.FirstOrDefault(t => t is Medicine) as Medicine;
                if (med != null)
                {
                    carriedMed = med;
                    break;
                }
            }
        }

        for (int i = pawns.Count - 1; i >= 0; i--)
        {
            Pawn p = pawns[i];
            if (p == null)
            {
                pawns.RemoveAt(i);
                continue;
            }

            if (!p.Dead)
            {
                try
                {
                    p.health?.HealthTickInterval(interval);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[Project Vigilance] Error ticking health for {pawnLabel} {p.LabelShort}: {ex.Message}");
                }

                if (p.Dead)
                {
                    Log.Message($"[Project Vigilance] {pawnLabel} {p.LabelShort} succumbed to wounds on the march.");
                    pawns.RemoveAt(i);
                    continue;
                }

                if (doctor != null && p.health != null && p.health.HasHediffsNeedingTend())
                {
                    TendUtility.DoTend(doctor, p, carriedMed);
                }

                if (p.needs != null)
                {
                    if (restingNow && p.needs.rest != null)
                    {
                        p.needs.rest.CurLevel += 0.002f * (interval / 250f);
                    }
                    if (p.needs.food != null)
                    {
                        p.needs.food.CurLevel -= 0.0005f * (interval / 250f);

                        // Caravan Eating Simulation: Consume carried provisions when hungry
                        if (p.needs.food.CurLevelPercentage < 0.35f && p.inventory?.innerContainer != null)
                        {
                            Thing? edible = p.inventory.innerContainer.FirstOrDefault(t => t.def.IsNutritionGivingIngestible);
                            if (edible != null)
                            {
                                float nut = edible.def.ingestible?.CachedNutrition ?? 0.5f;
                                p.needs.food.CurLevel += nut;
                                edible.SplitOff(1).Destroy();
                            }
                        }
                    }
                }
            }
        }
    }

    public override void Destroy()
    {
        string defaultOutcome = (Faction != null && Faction.HostileTo(Faction.OfPlayer))
            ? "Neutralized"
            : "Disbanded";
        ContactRegistry.Instance?.Notify_ContactDestroyed(this, defaultOutcome);

        if (committedPawns != null)
        {
            foreach (var p in committedPawns)
            {
                if (p != null && Find.WorldPawns != null)
                {
                    Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
                }
            }
        }
        if (retreatingPawns != null)
        {
            foreach (var p in retreatingPawns)
            {
                if (p != null && Find.WorldPawns != null)
                {
                    Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
                }
            }
        }
        tradeStock?.ClearAndDestroyContents();
        base.Destroy();
    }
}

