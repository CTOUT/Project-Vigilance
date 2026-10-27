using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.World;

public static class IncursionArrivalManager
{
    private static bool isExecutingArrival;
    private static WorldObject_Incursion? currentExecutingIncursion;

    public static bool IsExecutingArrival
    {
        get => isExecutingArrival;
        set => isExecutingArrival = value;
    }

    public static WorldObject_Incursion? CurrentExecutingIncursion => currentExecutingIncursion;

    public static bool ExecuteArrival(WorldObject_Incursion incursion)
    {
        if (incursion.IsPursuingCaravan)
        {
            Caravan? targetCaravan = incursion.PursuitTargetCaravan ??
                                     Find.WorldObjects?.Caravans?.FirstOrDefault(c => c.ID == incursion.PursuitTargetCaravanId);

            if (targetCaravan != null && targetCaravan.Spawned)
            {
                if (targetCaravan.Tile == incursion.Tile)
                {
                    incursion.ClearPursuitTarget();
                    Log.Message($"[Project Vigilance] Incursion {incursion.TrackingId} ({incursion.Label}) caught up with pursued caravan {targetCaravan.LabelCap} at tile {incursion.Tile}. Executing hostile interception.");
                    IncursionInterceptionUtility.ExecuteInterception(targetCaravan, incursion, isAmbush: true);
                    return true;
                }

                // Re-plot pursuit path to current caravan position
                List<PlanetTile>? newPursuitPath = null;
                if (IncursionOriginResolver.TryBuildPath(incursion.Tile, targetCaravan.Tile, out var calculatedPath))
                {
                    newPursuitPath = calculatedPath;
                }
                else if (Find.WorldGrid.IsNeighborOrSame(incursion.Tile, targetCaravan.Tile))
                {
                    newPursuitPath = new List<PlanetTile> { incursion.Tile, targetCaravan.Tile };
                }

                if (newPursuitPath != null && newPursuitPath.Count > 0)
                {
                    incursion.SetPursuitTarget(targetCaravan, newPursuitPath);
                    Log.Message($"[Project Vigilance] Incursion {incursion.TrackingId} reached waypoint and continues pursuing {targetCaravan.LabelCap} to tile {targetCaravan.Tile}.");
                    return false;
                }
            }

            // Target caravan was lost, disbanded, or unpathable
            Log.Message($"[Project Vigilance] Incursion {incursion.TrackingId} ({incursion.Label}) lost pursuit target caravan. Reversing course.");
            incursion.ClearPursuitTarget();
            incursion.ReverseCourse("lost pursuit target caravan");
            return false;
        }

        if (incursion.IsRetreating)
        {
            bool hasActivePath = incursion.PathTiles != null && incursion.PathTiles.Count > 1;
            bool reachedEndOfPath = incursion.PathTiles != null && incursion.PathTiles.Count > 0 && incursion.CurrentPathIndex >= incursion.PathTiles.Count - 1;
            bool atDestinationTile = incursion.DestinationTile.Valid && incursion.Tile == incursion.DestinationTile;
            bool reachedDest = hasActivePath ? reachedEndOfPath : atDestinationTile;

            if (!reachedDest)
            {
                return false;
            }

            string fName = incursion.Faction?.Name ?? "enemy forces";
            if (incursion.IsTradeMissionAborted)
            {
                Log.Message($"[Project Vigilance] Aborted trade caravan {incursion.TrackingId} ({incursion.Label}) from {fName} returned to their stronghold safely.");
                Messages.Message($"The surviving travellers from {fName} have returned safely to their territory following their aborted trade mission.", new GlobalTargetInfo(incursion.Tile), MessageTypeDefOf.NeutralEvent, historical: true);
                ContactRegistry.Instance?.DeregisterContact(incursion, "AbortedMissionReturnedHome");
                incursion.Destroy();
                return true;
            }

            if (incursion.KidnappedColonists != null && incursion.KidnappedColonists.Count > 0)
            {
                Pawn? kidnapper = incursion.RetreatingPawns?.FirstOrDefault();
                foreach (var captive in incursion.KidnappedColonists)
                {
                    if (captive != null && !captive.Dead)
                    {
                        if (incursion.Faction?.kidnapped != null && !incursion.Faction.kidnapped.KidnappedPawnsListForReading.Contains(captive))
                        {
                            incursion.Faction.kidnapped.Kidnap(captive, kidnapper);
                        }
                        IncursionRetreatManager.RecordSettlementIncarceration(captive, incursion.DestinationTile, incursion.Faction);
                    }
                }

                Settlement? homeSettlement = Find.WorldObjects?.Settlements?.FirstOrDefault(s => s.Tile == incursion.DestinationTile && s.Faction == incursion.Faction);
                string strongholdName = homeSettlement?.LabelCap ?? (incursion.DestinationTile.Valid ? $"stronghold at tile {incursion.DestinationTile}" : "their stronghold");

                string captivesList = string.Join(", ", incursion.KidnappedColonists.Where(c => !c.Dead).Select(c => c.LabelShort));
                Find.LetterStack.ReceiveLetter(
                    "Colonists Incarcerated: " + incursion.LabelCap,
                    $"Forces from {fName} have safely reached {strongholdName} with captured colonist(s): {captivesList}.\n\n" +
                    $"The prisoners are now incarcerated inside {strongholdName}.\n\n" +
                    $"You can liberate them by mounting a direct base assault on {strongholdName}, or await future ransom communications.",
                    LetterDefOf.NegativeEvent,
                    new GlobalTargetInfo(incursion.Tile)
                );
            }
            else
            {
                Log.Message($"[Project Vigilance] Retreating incursion {incursion.TrackingId} ({incursion.Label}) from {fName} returned to their stronghold safely.");
            }

            ContactRegistry.Instance?.Notify_ContactReturnedToBase(incursion);
            incursion.Destroy();
            return true;
        }

        if (incursion.IsTurningBack)
        {
            bool hasActivePath = incursion.PathTiles != null && incursion.PathTiles.Count > 1;
            bool reachedEndOfPath = incursion.PathTiles != null && incursion.PathTiles.Count > 0 && incursion.CurrentPathIndex >= incursion.PathTiles.Count - 1;
            bool atDestinationTile = incursion.DestinationTile.Valid && incursion.Tile == incursion.DestinationTile;
            bool reachedDest = hasActivePath ? reachedEndOfPath : atDestinationTile;

            if (!reachedDest)
            {
                return false;
            }

            string groupDesc = incursion.GetIdentifiedGroupLabel();
            bool isHostile = incursion.Faction?.HostileTo(Faction.OfPlayer) ?? true;
            string fName = incursion.Faction?.Name ?? (isHostile ? "enemy forces" : "peaceful travellers");
            string returnReason = incursion.HasBeenWarnedAway ? "ReturnedHomeAfterWarning" : "ReturnedHomeSafely";
            Log.Message($"[Project Vigilance] Incursion {incursion.TrackingId} ({incursion.Label}) returned home safely.");
            Messages.Message($"The {groupDesc} from {fName} returned to their home territory safely.", new GlobalTargetInfo(incursion.Tile), MessageTypeDefOf.NeutralEvent, historical: true);
            ContactRegistry.Instance?.DeregisterContact(incursion, returnReason);
            incursion.Destroy();
            return true;
        }

        if (incursion.IncidentDef == null || incursion.IncidentParms == null)
        {
            Log.Error("[Project Vigilance] Cannot execute incursion arrival: IncidentDef or IncidentParms is null.");
            ContactRegistry.Instance?.DeregisterContact(incursion, "ArrivalError");
            incursion.Destroy();
            return false;
        }

        // Validate that destination map is still active (player has not abandoned or destroyed the settlement)
        Map? targetMap = Current.Game.FindMap(incursion.DestinationTile) ?? Current.Game.FindMap(incursion.Tile);
        if (targetMap == null || !Find.Maps.Contains(targetMap))
        {
            var sensoryManager = Find.World?.GetComponent<SensoryManager>();
            bool hasVisual = sensoryManager != null && sensoryManager.HasActiveVision(incursion.DestinationTile);
            bool isIdentified = GeoscapeMod.Settings.visibilityMode == VisibilityMode.Omniscient ||
                                incursion.CurrentIntelligenceTier >= IntelligenceTier.Classified ||
                                hasVisual;

            string fName = incursion.Faction?.Name ?? "enemy forces";
            bool isHostile = incursion.Faction?.HostileTo(Faction.OfPlayer) ?? true;
            Log.Message($"[Project Vigilance] Incursion arrived at destination tile {incursion.Tile}, but the target settlement was abandoned. Aborting incident execution.");

            if (!isHostile)
            {
                string groupDesc = incursion.GetIdentifiedGroupLabel();
                string msg = $"The {groupDesc} from {fName} arrived at the abandoned settlement, found no one there, and turned back to return home.";
                Messages.Message(msg, MessageTypeDefOf.NeutralEvent, false);
                incursion.ReverseCourse("found settlement abandoned");
                return false;
            }
            else
            {
                return HandleHostileArrivalAtEmptyOrAbandonedSettlement(incursion, targetMap, incursion.DestinationTile.Valid ? incursion.DestinationTile : incursion.Tile);
            }
        }

        // Validate whether any conscious colonists are present on the active destination map
        bool hasConsciousColonists = false;
        var mapPawns = targetMap.mapPawns?.FreeColonistsSpawned;
        if (mapPawns != null)
        {
            for (int i = 0; i < mapPawns.Count; i++)
            {
                Pawn p = mapPawns[i];
                if (!p.Dead && !p.Downed)
                {
                    hasConsciousColonists = true;
                    break;
                }
            }
        }

        bool isHostileToPlayer = incursion.Faction?.HostileTo(Faction.OfPlayer) ?? true;

        // If no conscious colonists are present on the destination map (evacuated settlement),
        // neither friendly visitors nor hostile raids with requireColonistsPresent can engage.
        if (!hasConsciousColonists)
        {
            string settlementName = targetMap.Parent?.LabelCap ??
                                    Find.WorldObjects?.SettlementAt(targetMap.Tile)?.LabelCap ??
                                    (targetMap.IsPlayerHome ? "your colony" : $"settlement at tile {targetMap.Tile.tileId}");
            string groupDesc = incursion.GetIdentifiedGroupLabel();
            string fName = incursion.Faction?.Name ?? (isHostileToPlayer ? "enemy forces" : "peaceful travellers");

            if (!isHostileToPlayer)
            {
                string emptyMsg = $"The {groupDesc} from {fName} arrived at {settlementName}, found no colonists present, and turned back to return home.";
                Log.Message($"[Project Vigilance] {emptyMsg}");
                Messages.Message(emptyMsg, new GlobalTargetInfo(targetMap.Tile), MessageTypeDefOf.NeutralEvent, historical: true);
                incursion.ReverseCourse("found no colonists present at settlement");
                return false;
            }
            else
            {
                return HandleHostileArrivalAtEmptyOrAbandonedSettlement(incursion, targetMap, targetMap.Tile);
            }
        }

        // If the incursion is now hostile to the player and either already has generated/committed pawns
        // or was originally a non-raid incident (e.g. a Trade Caravan or Traveler Group whose faction turned hostile en route),
        // execute a direct settlement assault instead of calling the neutral IncidentWorker (which would fail and claim they dispersed).
        bool hasPreGeneratedPawns = (incursion.CommittedPawns != null && incursion.CommittedPawns.Count > 0) ||
                                    (incursion.RetreatingPawns != null && incursion.RetreatingPawns.Count > 0);
        bool isConvertedNeutralIncident = isHostileToPlayer && incursion.IncidentDef.category != IncidentCategoryDefOf.ThreatBig;

        if (isHostileToPlayer && (hasPreGeneratedPawns || isConvertedNeutralIncident))
        {
            string arrivalTargetName = targetMap.Parent?.LabelCap ?? Find.WorldObjects?.SettlementAt(targetMap.Tile)?.LabelCap ?? $"tile {targetMap.Tile.tileId}";
            Log.Message($"[Project Vigilance] Hostile incursion {incursion.TrackingId} ({incursion.Label}) arrived at {arrivalTargetName}. Executing direct settlement assault.");
            IncursionInterceptionUtility.ExecuteCampOrSettlementAssault(targetMap, incursion);
            return true;
        }

        // Re-bind target to current active map
        incursion.IncidentParms.target = targetMap;

        isExecutingArrival = true;
        currentExecutingIncursion = incursion;
        bool success = false;

        try
        {
            Log.Message($"[Project Vigilance] Incursion arrived at destination tile {incursion.Tile}. Executing incident: {incursion.IncidentDef.defName}.");
            success = incursion.IncidentDef.Worker.TryExecute(incursion.IncidentParms);

            if (success)
            {
                string arrivalOutcome = (incursion.GroupKind == IncursionGroupKind.HostileWarband || isHostileToPlayer)
                    ? (targetMap.IsPlayerHome ? "AssaultedColony" : "AssaultedOutpost")
                    : (incursion.GroupKind == IncursionGroupKind.TradeCaravan ? "TradedAtColony" : "VisitedColony");
                ContactRegistry.Instance?.DeregisterContact(incursion, arrivalOutcome);

                // Friendly trade caravans or visitors share intelligence about unmapped sites they passed
                if (!isHostileToPlayer && (incursion.GroupKind == IncursionGroupKind.TradeCaravan ||
                                           incursion.GroupKind == IncursionGroupKind.Visitors ||
                                           incursion.GroupKind == IncursionGroupKind.Travelers))
                {
                    BreadcrumbRumourUtility.TryShareTravellerRumour(incursion.Faction, incursion.DestinationTile, incursion);
                }

                if (GeoscapeMod.Settings.resetThreatCooldownOnArrival &&
                    incursion.IncidentDef.category == IncidentCategoryDefOf.ThreatBig &&
                    targetMap.storyState != null)
                {
                    var lastThreatField = Traverse.Create(targetMap.storyState).Field("lastThreatBigTick");
                    if (lastThreatField.FieldExists())
                    {
                        lastThreatField.SetValue(Find.TickManager.TicksGame);
                        Log.Message($"[Project Vigilance] Reset Storyteller LastThreatBigTick to current game tick ({Find.TickManager.TicksGame}) upon incursion arrival.");
                    }
                }
            }
            else if (!success)
            {
                string settlementName = targetMap.Parent?.LabelCap ??
                                        Find.WorldObjects?.SettlementAt(targetMap.Tile)?.LabelCap ??
                                        (targetMap.IsPlayerHome ? "your colony" : $"settlement at tile {targetMap.Tile.tileId}");
                string groupDesc = incursion.GetIdentifiedGroupLabel();
                string fName = incursion.Faction?.Name ?? (isHostileToPlayer ? "enemy forces" : "approaching force");
                string failMsg = incursion.GroupKind == IncursionGroupKind.HostileWarband
                    ? $"The {groupDesc} from {fName} arrived at {settlementName}, but could not find a path to attack and turned back to return home."
                    : $"The {groupDesc} from {fName} arrived at {settlementName}, but departed without incident.";
                Log.Warning($"[Project Vigilance] Incursion arrival incident {incursion.IncidentDef.defName} did not execute successfully. Notifying player.");
                Messages.Message(failMsg, new GlobalTargetInfo(incursion.DestinationTile), MessageTypeDefOf.NeutralEvent, historical: true);

                incursion.ReverseCourse("arrival incident failed to execute");
                return false;
            }

            if (success && incursion.GroupKind == IncursionGroupKind.HostileWarband)
            {
                // Spawn any carried kidnapped colonists on the map near the attackers only if attackers spawned
                if (incursion.KidnappedColonists != null && incursion.KidnappedColonists.Count > 0)
                {
                    if (incursion.CommittedPawns is { Count: > 0 } && incursion.CommittedPawns[0].Spawned)
                    {
                        IntVec3 centerCell = incursion.CommittedPawns[0].Position;
                        foreach (var captive in incursion.KidnappedColonists)
                        {
                            if (captive != null && !captive.Spawned)
                            {
                                CellFinder.TryFindRandomCellNear(centerCell, targetMap, 5, c => c.Walkable(targetMap) && !c.Fogged(targetMap), out IntVec3 cCell);
                                GenSpawn.Spawn(captive, cCell.IsValid ? cCell : centerCell, targetMap, Rot4.Random);
                            }
                        }
                    }
                    else
                    {
                        Log.Warning($"[Project Vigilance] Incursion {incursion.TrackingId} arrived at settlement with {incursion.KidnappedColonists.Count} captive(s), but raiders were not spawned. Captives remain safely in overland captivity.");
                    }
                }

                // Ensure engaged state is set
                if (!incursion.IsEngagedInTacticalMap && incursion.CommittedPawns is { Count: > 0 })
                {
                    incursion.IsEngagedInTacticalMap = true;
                    var ambushComp = targetMap.GetComponent<MapComponent_OverlandAmbush>();
                    ambushComp?.RegisterIncursion(incursion, incursion.CommittedPawns);
                }
            }
        }
        catch (System.Exception ex)
        {
            Log.Error($"[Project Vigilance] Exception while executing incursion arrival incident: {ex}");
        }
        finally
        {
            isExecutingArrival = false;
            currentExecutingIncursion = null;
            if (incursion.Spawned && !incursion.IsEngagedInTacticalMap && !incursion.IsTurningBack && !incursion.IsRetreating && !incursion.IsPursuingCaravan)
            {
                incursion.Destroy();
            }
        }

        return success;
    }

    private static bool HandleHostileArrivalAtEmptyOrAbandonedSettlement(WorldObject_Incursion incursion, Map? targetMap, PlanetTile arrivalTile)
    {
        string fName = incursion.Faction?.Name ?? "enemy forces";
        string groupDesc = incursion.GetIdentifiedGroupLabel();
        string settlementLabel = (targetMap?.Parent?.LabelCap) ?? (arrivalTile.Valid ? $"the site at tile {arrivalTile}" : "the abandoned site");

        PlanetTile currentIncursionTile = incursion.Tile.Valid ? incursion.Tile : arrivalTile;

        // -------------------------------------------------------------------------
        // Priority 1: Hot Pursuit of Evacuating / Nearby Player Caravans
        // -------------------------------------------------------------------------
        if (TryFindEvacuatingCaravanTracks(incursion, arrivalTile, targetMap, out Caravan? targetCaravan, out float calculatedChance))
        {
            if (targetCaravan != null)
            {
                float dist = Find.WorldGrid.ApproxDistanceInTiles(currentIncursionTile, targetCaravan.Tile);

                // If on same tile or right outside (< 0.25 hexes), intercept immediately
                if (dist < 0.25f || targetCaravan.Tile == currentIncursionTile || targetCaravan.Tile == arrivalTile)
                {
                    Log.Message($"[Project Vigilance] Incursion {incursion.TrackingId} ({incursion.Label}) discovered caravan {targetCaravan.LabelCap} on the same tile ({currentIncursionTile}). Intercepting immediately.");
                    IncursionInterceptionUtility.ExecuteInterception(targetCaravan, incursion, isAmbush: true);
                    return true;
                }

                List<PlanetTile>? pursuitPath = null;
                if (IncursionOriginResolver.TryBuildPath(currentIncursionTile, targetCaravan.Tile, out var calculatedPath))
                {
                    pursuitPath = calculatedPath;
                }
                else if (Find.WorldGrid.IsNeighborOrSame(currentIncursionTile, targetCaravan.Tile))
                {
                    pursuitPath = new List<PlanetTile> { currentIncursionTile, targetCaravan.Tile };
                }

                if (pursuitPath != null && pursuitPath.Count > 0)
                {
                    incursion.SetPursuitTarget(targetCaravan, pursuitPath);

                    string title = "Overland Pursuit: " + incursion.LabelCap;
                    string text = $"The {groupDesc} from {fName} arrived at {settlementLabel} and found no defenders present.\n\n" +
                                  $"However, their scouts discovered fresh overland tracks ({calculatedChance:P0} tracking certainty) and have set off in hot pursuit of your caravan ({targetCaravan.LabelCap})!\n\n" +
                                  $"The pursuers are {dist:F1} hexes behind. Prepare your caravan for interception or attempt to outpace them.";

                    Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.ThreatBig, incursion);
                    Log.Message($"[Project Vigilance] Incursion {incursion.TrackingId} ({incursion.Label}) discovered tracks ({calculatedChance:P0}) and initiated hot pursuit of {targetCaravan.LabelCap} ({dist:F1} hexes away).");
                    return false;
                }
            }
        }

        // -------------------------------------------------------------------------
        // Priority 2: Opportunistic Retargeting (Adjacent Colony / Camp / Outpost)
        // -------------------------------------------------------------------------
        Settlement? secondaryTarget = Find.WorldObjects?.Settlements?
            .Where(s => s.Faction == Faction.OfPlayer && s.Tile != arrivalTile && s.Tile.Valid && s.Tile.Layer == arrivalTile.Layer)
            .Where(s => Find.WorldGrid.ApproxDistanceInTiles(arrivalTile, s.Tile) <= 10.0f)
            .OrderBy(s => Find.WorldGrid.ApproxDistanceInTiles(arrivalTile, s.Tile))
            .FirstOrDefault(s => IncursionOriginResolver.TryBuildPath(arrivalTile, s.Tile, out _));

        if (secondaryTarget != null && IncursionOriginResolver.TryBuildPath(arrivalTile, secondaryTarget.Tile, out var retargetPath))
        {
            float dist = Find.WorldGrid.ApproxDistanceInTiles(arrivalTile, secondaryTarget.Tile);
            incursion.SetRetargetRoute(secondaryTarget.Tile, retargetPath);

            string title = "Hostile Force Diverting: " + incursion.LabelCap;
            string text = $"The {groupDesc} from {fName} arrived at {settlementLabel} and found it undefended.\n\n" +
                          $"Spotting your nearby settlement at {secondaryTarget.LabelCap} ({dist:F0} hexes away), the warband has altered course to attack it instead!\n\n" +
                          $"Prepare your defences at {secondaryTarget.LabelCap}.";

            Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.ThreatBig, incursion);
            Log.Message($"[Project Vigilance] Incursion {incursion.TrackingId} ({incursion.Label}) diverted to secondary target {secondaryTarget.LabelCap} ({dist:F0} hexes away).");
            return false;
        }

        // -------------------------------------------------------------------------
        // Priority 3: Fallback Ransack & Return Home March
        // -------------------------------------------------------------------------
        string ransackMsg = $"The {groupDesc} from {fName} arrived at {settlementLabel}, found no defenders present, ransacked the vacant structures, and turned back to return home.";
        Log.Message($"[Project Vigilance] {ransackMsg}");
        Messages.Message(ransackMsg, new GlobalTargetInfo(arrivalTile), MessageTypeDefOf.NeutralEvent, historical: true);

        incursion.ReverseCourse("ransacked empty settlement");
        return false;
    }

    private static bool TryFindEvacuatingCaravanTracks(
        WorldObject_Incursion incursion,
        PlanetTile arrivalTile,
        Map? targetMap,
        out Caravan? targetCaravan,
        out float calculatedChance)
    {
        targetCaravan = null;
        calculatedChance = 0f;

        if (incursion == null || !arrivalTile.Valid)
        {
            return false;
        }

        var caravans = Find.WorldObjects?.Caravans;
        if (caravans == null || caravans.Count == 0)
        {
            return false;
        }

        // Candidates: spawned player caravans in the same layer within max tracking radius (8 hexes)
        var candidates = caravans
            .Where(c => c != null && c.Spawned && c.Faction == Faction.OfPlayer && c.Tile.Valid && c.Tile.Layer == arrivalTile.Layer)
            .Select(c => new { Caravan = c, Distance = Find.WorldGrid.ApproxDistanceInTiles(arrivalTile, c.Tile) })
            .Where(x => x.Distance <= 8.0f)
            .OrderBy(x => x.Distance)
            .ToList();

        if (candidates.Count == 0)
        {
            return false;
        }

        var bestCandidate = candidates[0];
        Caravan candidateCaravan = bestCandidate.Caravan;
        float dist = bestCandidate.Distance;

        // If the caravan is on the same tile or immediately adjacent gate (< 0.25 hexes), tracking is 100% certain
        if (candidateCaravan.Tile == arrivalTile || dist < 0.25f)
        {
            calculatedChance = 1.0f;
            targetCaravan = candidateCaravan;
            Log.Message($"[Project Vigilance] Incursion {incursion.TrackingId} arrived at settlement and directly spotted caravan {candidateCaravan.LabelCap} on the same tile ({dist:F1} hexes).");
            return true;
        }

        // Base Tracking Chance by Faction Tech Level
        float baseChance = 0.50f;
        TechLevel tech = incursion.Faction?.def?.techLevel ?? TechLevel.Industrial;
        switch (tech)
        {
            case TechLevel.Animal:
            case TechLevel.Neolithic:
                baseChance = 0.85f; // Tribal hunters excel at wilderness tracking
                break;
            case TechLevel.Medieval:
                baseChance = 0.65f;
                break;
            case TechLevel.Industrial:
                baseChance = 0.50f;
                break;
            case TechLevel.Spacer:
            case TechLevel.Ultra:
                baseChance = 0.45f;
                break;
            case TechLevel.Archotech:
                baseChance = 0.35f;
                break;
        }

        // Mechanoids rely on sensors/radar rather than footprint tracking
        bool isMechanoid = incursion.Faction != null && (incursion.Faction.def == FactionDefOf.Mechanoid || incursion.Faction.def.defName.Contains("Mechanoid"));
        if (isMechanoid)
        {
            baseChance = 0.30f;
        }

        float chance = baseChance;

        // Modifier: Sapper / Breach raids are focused on structures and less observant
        string incidentDefName = incursion.IncidentDef?.defName ?? string.Empty;
        if (incidentDefName.IndexOf("Sapper", StringComparison.OrdinalIgnoreCase) >= 0 ||
            incidentDefName.IndexOf("Breach", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            chance -= 0.15f;
        }

        // Modifier: War beasts / Hunting hounds scent tracking bonus (+15%)
        var breakdown = ContactRegistry.Instance?.GetRosterBreakdown(incursion);
        if (breakdown != null && breakdown.AnimalCount > 0)
        {
            chance += 0.15f;
        }

        // Distance Decay: Trail disperses / cools with distance (-5% per hex)
        float distPenalty = dist * 0.05f;
        chance -= distPenalty;

        // Weather Modifier: Rain washes away tracks (-20%)
        if (targetMap?.weatherManager != null)
        {
            if (targetMap.weatherManager.RainRate > 0.1f)
            {
                chance -= 0.20f;
            }
            else if (targetMap.weatherManager.SnowRate > 0.1f)
            {
                chance -= 0.15f;
            }
        }

        calculatedChance = UnityEngine.Mathf.Clamp(chance, 0.05f, 0.90f);

        bool rolledSuccess = Rand.Chance(calculatedChance);
        Log.Message($"[Project Vigilance] Tracking roll for incursion {incursion.TrackingId} against caravan {candidateCaravan.LabelCap} ({dist:F1} hexes): Tech={tech}, Base={baseChance:P0}, Final={calculatedChance:P0}, Succeeded={rolledSuccess}.");

        if (rolledSuccess)
        {
            targetCaravan = candidateCaravan;
            return true;
        }

        return false;
    }
}
