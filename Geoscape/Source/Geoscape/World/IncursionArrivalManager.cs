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
        if (incursion.IsRetreating)
        {
            string fName = incursion.Faction?.Name ?? "enemy forces";
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

            incursion.Destroy();
            return true;
        }

        if (incursion.IsTurningBack)
        {
            string groupDesc = incursion.GetIdentifiedGroupLabel();
            string fName = incursion.Faction?.Name ?? "peaceful travellers";
            Log.Message($"[Project Vigilance] Incursion {incursion.TrackingId} ({incursion.Label}) returned home safely after being warned away.");
            Messages.Message($"The {groupDesc} from {fName} returned to their home territory safely.", new GlobalTargetInfo(incursion.Tile), MessageTypeDefOf.NeutralEvent, historical: true);
            incursion.Destroy();
            return true;
        }

        if (incursion.IncidentDef == null || incursion.IncidentParms == null)
        {
            Log.Error("[Project Vigilance] Cannot execute incursion arrival: IncidentDef or IncidentParms is null.");
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

            string msg;
            if (isIdentified)
            {
                string groupDesc = incursion.GetIdentifiedGroupLabel();
                msg = isHostile
                    ? $"The {groupDesc} from {fName} arrived at the abandoned settlement, found no defenders or structures of value, and dispersed."
                    : $"The {groupDesc} from {fName} arrived at the abandoned settlement, found no one there, and returned home.";
            }
            else
            {
                msg = "An unidentified force arrived at the abandoned settlement, found no defenders, and dispersed.";
            }

            Messages.Message(msg, MessageTypeDefOf.NeutralEvent, false);
            incursion.Destroy();
            return false;
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
            string settlementName = targetMap.Parent?.LabelCap ?? "the settlement";
            string groupDesc = incursion.GetIdentifiedGroupLabel();
            string fName = incursion.Faction?.Name ?? (isHostileToPlayer ? "enemy forces" : "peaceful travellers");
            string emptyMsg = isHostileToPlayer
                ? $"The {groupDesc} from {fName} arrived at {settlementName}, found no defenders present, and dispersed."
                : $"The {groupDesc} from {fName} arrived at {settlementName}, found no colonists present, and returned home.";
            Log.Message($"[Project Vigilance] {emptyMsg}");
            Messages.Message(emptyMsg, new GlobalTargetInfo(targetMap.Tile), MessageTypeDefOf.NeutralEvent, historical: true);
            incursion.Destroy();
            return false;
        }

        // If the incursion is now hostile to the player and either already has generated/committed pawns
        // or was originally a non-raid incident (e.g. a Trade Caravan or Traveler Group whose faction turned hostile en route),
        // execute a direct settlement assault instead of calling the neutral IncidentWorker (which would fail and claim they dispersed).
        bool hasPreGeneratedPawns = (incursion.CommittedPawns != null && incursion.CommittedPawns.Count > 0) ||
                                    (incursion.RetreatingPawns != null && incursion.RetreatingPawns.Count > 0);
        bool isConvertedNeutralIncident = isHostileToPlayer && incursion.IncidentDef.category != IncidentCategoryDefOf.ThreatBig;

        if (isHostileToPlayer && (hasPreGeneratedPawns || isConvertedNeutralIncident))
        {
            Log.Message($"[Project Vigilance] Hostile incursion {incursion.TrackingId} ({incursion.Label}) arrived at {targetMap.Parent?.LabelCap ?? "settlement"}. Executing direct settlement assault.");
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

            if (success && GeoscapeMod.Settings.resetThreatCooldownOnArrival &&
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
            else if (!success)
            {
                string settlementName = targetMap.Parent?.LabelCap ?? "the settlement";
                string groupDesc = incursion.GetIdentifiedGroupLabel();
                string fName = incursion.Faction?.Name ?? "approaching force";
                string failMsg = incursion.GroupKind == IncursionGroupKind.HostileWarband
                    ? $"The {groupDesc} from {fName} arrived at {settlementName}, but could not find a path to attack and dispersed."
                    : $"The {groupDesc} from {fName} arrived at {settlementName}, but departed without incident.";
                Log.Warning($"[Project Vigilance] Incursion arrival incident {incursion.IncidentDef.defName} did not execute successfully. Notifying player.");
                Messages.Message(failMsg, new GlobalTargetInfo(incursion.DestinationTile), MessageTypeDefOf.NeutralEvent, historical: true);
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
            if (incursion.Spawned && !incursion.IsEngagedInTacticalMap)
            {
                incursion.Destroy();
            }
        }

        return success;
    }
}
