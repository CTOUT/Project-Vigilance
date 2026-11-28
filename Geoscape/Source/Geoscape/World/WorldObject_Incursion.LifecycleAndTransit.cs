using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.World;

public partial class WorldObject_Incursion
{
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
        speedTilesPerDay = speed > 0f ? speed : 20f;
        departureDelayTicks = Math.Max(0, delayTicks);

        if (parms.faction != null)
        {
            SetFaction(parms.faction);
        }
        else if (Faction == null)
        {
            bool isRaid = def == IncidentDefOf.RaidEnemy || (def?.defName.Contains("Raid") == true);
            Faction? fallback = isRaid
                ? Find.FactionManager?.AllFactions?.FirstOrDefault(f => f.HostileTo(Faction.OfPlayer) && !f.def.hidden)
                : Find.FactionManager?.AllFactions?.FirstOrDefault(f => !f.HostileTo(Faction.OfPlayer) && !f.IsPlayer && !f.def.hidden);
            if (fallback != null)
            {
                parms.faction = fallback;
                SetFaction(fallback);
            }
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

        isIdentified = (GeoscapeRules.VisibilityMode == VisibilityMode.Omniscient);
        intelligenceTier = (GeoscapeRules.VisibilityMode == VisibilityMode.Omniscient)
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
            EnsureTraderPawnConfigured();
            GetOrCreateTradeStock();
        }
    }

    private void EnsureTraderPawnConfigured()
    {
        if (!IsUnderlyingTradeCaravan) return;

        if ((traderPawn == null || (committedPawns != null && !committedPawns.Contains(traderPawn))) && committedPawns != null)
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
        IncursionManager.Notify_IncursionSpawned(this);
    }

    public override void PostRemove()
    {
        SensoryManager.Instance?.DeregisterTarget(this);
        ContactRegistry.Instance?.Notify_ContactRemoved(this);
        IncursionManager.Notify_IncursionDeparted(this);
        base.PostRemove();
    }

    public void OnDetected(ISensorProvider sensor, IntelligenceTier newTier)
    {
        EarlyWarningDispatcher.ProcessDetection(this, sensor, newTier);
    }

    public void DispatchContactLetter(ISensorProvider sensor)
    {
        EarlyWarningDispatcher.DispatchContactLetter(this, sensor);
    }

    public void DispatchClassifiedLetter(ISensorProvider sensor)
    {
        EarlyWarningDispatcher.DispatchClassifiedLetter(this, sensor);
    }

    public void SendIncursionLetter(TaggedString title, TaggedString text, LetterDef def, Map? destinationMap, string targetName)
    {
        EarlyWarningDispatcher.SendIncursionLetter(this, title, text, def, destinationMap, targetName);
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
        Scribe_Values.Look(ref speedTilesPerDay, "speedTilesPerDay", 20f);
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
        Scribe_Collections.Look(ref metMapIds, "metMapIds", LookMode.Value);
        if (metMapIds == null)
        {
            metMapIds = new HashSet<int>();
        }
        Scribe_Collections.Look(ref caravanCooldowns, "caravanCooldowns", LookMode.Value, LookMode.Value);
        if (caravanCooldowns == null)
        {
            caravanCooldowns = new Dictionary<int, int>();
        }
        Scribe_Values.Look(ref isRetreating, "isRetreating", false);
        Scribe_Values.Look(ref isTradeMissionAborted, "isTradeMissionAborted", false);
        Scribe_Values.Look(ref captorsSuccumbedLetterSent, "captorsSuccumbedLetterSent", false);
        Scribe_Values.Look(ref isEngagedInTacticalMap, "isEngagedInTacticalMap", false);
        Scribe_Values.Look(ref skirmishCooldownExpiry, "skirmishCooldownExpiry", 0);

        if (Scribe.mode == LoadSaveMode.Saving)
        {
            PreparePawnListForSave(retreatingPawns);
            PreparePawnListForSave(kidnappedColonists, isCaptiveColonist: true);
            PreparePawnListForSave(committedPawns);

            if (traderPawn != null &&
                (WorldPawnIntegrityUtility.IsDeadSafe(traderPawn) ||
                 traderPawn.Destroyed ||
                 WorldPawnIntegrityUtility.IsDanglingPawnReference(traderPawn)))
            {
                traderPawn = null;
            }
            else
            {
                WorldPawnIntegrityUtility.SanitizeDanglingRelations(traderPawn);
            }
        }

        Scribe_Collections.Look(ref retreatingPawns, "retreatingPawns", LookMode.Reference);
        Scribe_Collections.Look(ref kidnappedColonists, "kidnappedColonists", LookMode.Reference);
        Scribe_Collections.Look(ref committedPawns, "committedPawns", LookMode.Reference);
        Scribe_References.Look(ref traderPawn, "traderPawn");
        Scribe_Defs.Look(ref explicitTraderKind, "explicitTraderKind");

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            if (GeoscapeRules.VisibilityMode != VisibilityMode.Omniscient && intelligenceTier < IntelligenceTier.Classified)
            {
                isIdentified = false;
            }
            committedPawns?.RemoveAll(p => p == null || p.Dead || p.Destroyed || p.Discarded);
            retreatingPawns?.RemoveAll(p => p == null || p.Dead || p.Destroyed || p.Discarded);
            kidnappedColonists?.RemoveAll(p => p == null || p.Dead || p.Destroyed || p.Discarded);

            if (traderPawn != null && (traderPawn.Dead || traderPawn.Destroyed || traderPawn.Discarded))
            {
                traderPawn = null;
            }

            if (IsUnderlyingTradeCaravan)
            {
                EnsureTraderPawnConfigured();
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

        Scribe_References.Look(ref pursuitTargetCaravan, "pursuitTargetCaravan");
        Scribe_Values.Look(ref pursuitTargetCaravanId, "pursuitTargetCaravanId", -1);

        if (Scribe.mode == LoadSaveMode.PostLoadInit && incidentParms != null && incidentParms.target == null && destinationTile.Valid)
        {
            incidentParms.target = Current.Game.FindMap(destinationTile);
        }
    }

    protected override void Tick()
    {
        base.Tick();

        if (intelligenceTier < IntelligenceTier.Contact && GeoscapeRules.VisibilityMode != VisibilityMode.Omniscient)
        {
            if (Find.WorldSelector != null && Find.WorldSelector.IsSelected(this))
            {
                Find.WorldSelector.Deselect(this);
            }
        }

        if (isEngagedInTacticalMap)
        {
            if (tacticalWatchdogCooldown > 0)
            {
                tacticalWatchdogCooldown--;
                return;
            }
            tacticalWatchdogCooldown = 250;

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
                tacticalWatchdogCooldown = 0;
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
            else if (committedPawns != null && !isEngagedInTacticalMap)
            {
                TickCommittedPawnsPhysiology(250);
            }
            if (IsUnderlyingTradeCaravan && !isTradeMissionAborted)
            {
                if (traderPawn != null && (traderPawn.Dead || traderPawn.Destroyed))
                {
                    AbortTradeMission("caravan master perished en route");
                }
                else if (committedPawns != null && committedPawns.Count > 0)
                {
                    bool hasLivingHumanlike = false;
                    for (int pIdx = 0; pIdx < committedPawns.Count; pIdx++)
                    {
                        Pawn? cp = committedPawns[pIdx];
                        if (cp != null && !cp.Dead && cp.RaceProps != null && cp.RaceProps.Humanlike)
                        {
                            hasLivingHumanlike = true;
                            break;
                        }
                    }
                    if (!hasLivingHumanlike)
                    {
                        AbortTradeMission("all merchant personnel lost");
                    }
                }
            }

            // Dynamic Overland Pursuit Tracking: re-plot intercept path if target caravan moves
            if (pursuitTargetCaravanId > 0)
            {
                if (pursuitTargetCaravan == null || !pursuitTargetCaravan.Spawned)
                {
                    pursuitTargetCaravan = null;
                    var allCaravans = Find.WorldObjects?.Caravans;
                    if (allCaravans != null)
                    {
                        for (int cIdx = 0; cIdx < allCaravans.Count; cIdx++)
                        {
                            Caravan cv = allCaravans[cIdx];
                            if (cv != null && cv.ID == pursuitTargetCaravanId)
                            {
                                pursuitTargetCaravan = cv;
                                break;
                            }
                        }
                    }
                }

                if (pursuitTargetCaravan != null && pursuitTargetCaravan.Spawned)
                {
                    if (pursuitTargetCaravan.Tile != destinationTile)
                    {
                        List<PlanetTile>? updatedPursuitPath = null;
                        if (IncursionOriginResolver.TryBuildPath(Tile, pursuitTargetCaravan.Tile, out var calculatedPath))
                        {
                            updatedPursuitPath = calculatedPath;
                        }
                        else if (Find.WorldGrid.IsNeighborOrSame(Tile, pursuitTargetCaravan.Tile))
                        {
                            updatedPursuitPath = new List<PlanetTile> { Tile, pursuitTargetCaravan.Tile };
                        }

                        if (updatedPursuitPath != null && updatedPursuitPath.Count > 0)
                        {
                            pathTiles = updatedPursuitPath;
                            currentPathIndex = 0;
                            tileProgress = 0f;
                            destinationTile = pursuitTargetCaravan.Tile;
                        }
                    }
                }
                else
                {
                    Log.Message($"[Project Vigilance] Incursion {TrackingId} ({LabelCap}) lost pursuit target caravan #{pursuitTargetCaravanId}. Reversing course.");
                    ClearPursuitTarget();
                    ReverseCourse("lost pursuit target caravan");
                }
            }
        }

        // Nighttime Rest: Biological mortal forces halt and camp at night
        if (IsRestingAtNight)
        {
            return;
        }

        float difficulty = GetCurrentMovementDifficulty();
        float tilesPerTick = (speedTilesPerDay / difficulty) / 60000f;
        tileProgress += tilesPerTick;

        bool didStepTile = false;
        while (tileProgress >= 1f)
        {
            tileProgress -= 1f;
            currentPathIndex++;

            if (currentPathIndex < pathTiles.Count)
            {
                Tile = pathTiles[currentPathIndex];
                didStepTile = true;
                CheckOutpostSabotageOnTile(Tile);
            }
            else
            {
                IncursionArrivalManager.ExecuteArrival(this);
                return;
            }
        }

        CheckInterceptionsAndPursuits(checkStaticMapCollisions: didStepTile || (Find.TickManager.TicksGame % 60 == 0));
    }

    private void CheckOutpostSabotageOnTile(PlanetTile tile)
    {
        if (Faction == null || !Faction.HostileTo(Faction.OfPlayer))
        {
            return;
        }

        var allObjects = Find.WorldObjects?.AllWorldObjects;
        if (allObjects == null)
        {
            return;
        }

        for (int i = 0; i < allObjects.Count; i++)
        {
            if (allObjects[i] is WorldObject_ListeningPost post && post.Tile == tile && post.Faction == Faction.OfPlayer && !post.IsSabotaged)
            {
                post.Sabotage(Faction);
            }
        }
    }

    public void ReverseCourse(string? reason = null)
    {
        ClearPursuitTarget();
        if (!isRetreating)
        {
            isTurningBack = true;
        }

        // 1. Prefer direct path back to origin base if valid and reachable
        if (originTile.Valid && Tile != originTile && IncursionOriginResolver.TryBuildPath(Tile, originTile, out var directReturnPath))
        {
            destinationTile = originTile;
            pathTiles = directReturnPath;
            currentPathIndex = 0;
            tileProgress = 0f;
            if (directReturnPath.Count > 0)
            {
                Tile = directReturnPath[0];
            }
        }
        else if (pathTiles != null && pathTiles.Count > 0)
        {
            List<PlanetTile> newPath = new();
            int startIdx = Mathf.Clamp(currentPathIndex, 0, pathTiles.Count - 1);
            for (int i = startIdx; i >= 0; i--)
            {
                newPath.Add(pathTiles[i]);
            }
            if (newPath.Count <= 1 && Tile != originTile && originTile.Valid)
            {
                if (IncursionOriginResolver.TryBuildPath(Tile, originTile, out var directPath))
                {
                    newPath = directPath;
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

    private void CheckInterceptionsAndPursuits(bool checkStaticMapCollisions)
    {
        if (!Spawned)
        {
            return;
        }

        bool isHostile = Faction?.HostileTo(Faction.OfPlayer) ?? true;

        // 1. Check for player caravans on the world map FIRST
        // If player pawns are in a caravan on this hex (e.g. evacuated outside the colony or marching),
        // engaging the live mobile force always takes priority over inspecting vacant buildings!
        if (!isRetreating || Tile != originTile)
        {
            var worldCaravans = Find.WorldObjects?.Caravans;
            if (worldCaravans != null && worldCaravans.Count > 0)
            {
                // Clean up any caravans no longer on this tile that are not on active cooldown (throttled to 250-tick interval)
                if (metCaravanIds.Count > 0 && Find.TickManager.TicksGame % 250 == 0)
                {
                    metCaravanIds.RemoveWhere(id =>
                    {
                        for (int cIdx = 0; cIdx < worldCaravans.Count; cIdx++)
                        {
                            var cv = worldCaravans[cIdx];
                            if (cv != null && cv.ID == id)
                            {
                                return cv.Tile != Tile && !IsCaravanOnCooldown(id);
                            }
                        }
                        return !IsCaravanOnCooldown(id);
                    });
                }

                // 1a. Dynamic pursuit tracking: caravans with intercept actions targeted at this incursion
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
                            if (!IsCaravanOnCooldown(c.ID))
                            {
                                c.pather?.StopDead();
                                IncursionInterceptionUtility.ExecuteInterception(c, this, isAmbush: false);
                            }
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
                                if (!IsCaravanOnCooldown(c.ID))
                                {
                                    c.pather?.StopDead();
                                    AdvancePathTile();
                                    IncursionInterceptionUtility.ExecuteInterception(c, this, isAmbush: false);
                                }
                                return;
                            }
                        }

                        // Dynamic pursuit tracking: plot X-COM lead intercept tile along incursion heading
                        var sensoryManager = SensoryManager.Instance;
                        bool hasVision = sensoryManager != null && sensoryManager.HasActiveVision(Tile);
                        if (GeoscapeRules.VisibilityMode == VisibilityMode.Omniscient || intelligenceTier >= IntelligenceTier.Contact || hasVision)
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

                // 1b. Direct tile collision: caravans occupying this hex
                List<Caravan>? eligibleCaravans = null;
                for (int i = 0; i < worldCaravans.Count; i++)
                {
                    Caravan c = worldCaravans[i];
                    if (c == null || !c.Spawned || c.Faction != Faction.OfPlayer || c.Tile != Tile)
                    {
                        continue;
                    }

                    if (c.pather?.ArrivalAction is CaravanArrivalAction_InterceptIncursion intercept && intercept.TargetIncursion == this)
                    {
                        continue;
                    }

                    if (!metCaravanIds.Contains(c.ID) && !IsCaravanOnCooldown(c.ID))
                    {
                        eligibleCaravans ??= new List<Caravan>();
                        eligibleCaravans.Add(c);
                    }
                }

                if (eligibleCaravans != null && eligibleCaravans.Count > 0)
                {
                    if (!isHostile)
                    {
                        IncursionInterceptionUtility.ExecuteFriendlyInterception(eligibleCaravans, this);
                    }
                    else if (!isRetreating && !isTurningBack)
                    {
                        IncursionInterceptionUtility.ExecuteHostileInterception(eligibleCaravans, this, isAmbush: true);
                    }
                    return;
                }
            }
        }

        if (!checkStaticMapCollisions)
        {
            return;
        }

        // 2. Check for collision with an active player Camp or Settlement or Battlefield on this tile
        Map? tileMap = Current.Game?.FindMap(Tile);
        MapParent? mapParent = Find.WorldObjects?.MapParentAt(Tile);

        if (tileMap != null && !tileMap.IsPocketMap && tileMap.mapPawns != null && tileMap.mapPawns.AnyFreeColonistSpawned)
        {
            var ambushComp = tileMap.GetComponent<MapComponent_OverlandAmbush>();
            int mapId = tileMap.Parent?.ID ?? tileMap.uniqueID;

            bool hasEngagementRecord = false;
            if (ambushComp != null && !ambushComp.HasTrackedIncursion(this) && ambushComp.Engagements != null)
            {
                for (int eIdx = 0; eIdx < ambushComp.Engagements.Count; eIdx++)
                {
                    if (ambushComp.Engagements[eIdx].incursion == this)
                    {
                        hasEngagementRecord = true;
                        break;
                    }
                }
            }

            bool alreadyHandledByMap = this.IsEngagedInTacticalMap ||
                                       metMapIds.Contains(mapId) ||
                                       (ambushComp != null && (ambushComp.HasTrackedIncursion(this) || hasEngagementRecord));

            if (!alreadyHandledByMap && !isRetreating && !isTurningBack && !IsPursuingCaravan)
            {
                metMapIds.Add(mapId);

                if (tileMap.IsPlayerHome)
                {
                    if (isHostile)
                    {
                        IncursionInterceptionUtility.ExecuteCampOrSettlementAssault(tileMap, this);
                        return;
                    }
                    else
                    {
                        IncursionInterceptionUtility.ExecuteFriendlyCampOrSettlementVisit(tileMap, this);
                    }
                }
                else
                {
                    // Temporary camp or CaravansBattlefield: Transit across the battlescape!
                    IncursionInterceptionUtility.ExecuteBattlescapeTransit(tileMap, this);
                    return;
                }
            }
        }
        else if (mapParent is Settlement settlement && settlement.Faction == Faction.OfPlayer)
        {
            if (isHostile && !isRetreating && !isTurningBack && !IsPursuingCaravan)
            {
                destinationTile = Tile;
                IncursionArrivalManager.ExecuteArrival(this);
                return;
            }
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

}
