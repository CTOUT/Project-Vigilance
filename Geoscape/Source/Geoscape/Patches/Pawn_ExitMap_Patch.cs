using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;
using ProjectVigilance.Geoscape.World;

namespace ProjectVigilance.Geoscape.Patches;

[HarmonyPatch(typeof(Pawn), nameof(Pawn.ExitMap))]
public static class Pawn_ExitMap_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(Pawn __instance, bool allowedToJoinOrCreateCaravan)
    {
        if (__instance == null || __instance.Dead || __instance.Faction == null)
        {
            return true;
        }

        Map? map = __instance.Map;
        if (map == null)
        {
            return true;
        }

        var ambushComp = map.GetComponent<MapComponent_OverlandAmbush>();
        bool isOverlandEncounter = IncursionTacticalRetreatUtility.IsOverlandEncounterMap(map, ambushComp);
        bool isInsideTransporter = ThingOwnerUtility.AnyParentIs<ActiveTransporterInfo>(__instance) ||
                                   ThingOwnerUtility.AnyParentIs<TravellingTransporters>(__instance);
        bool hasHomeRetreatCondition = map.IsPlayerHome &&
                                       (GenHostility.AnyHostileActiveThreatToPlayer(map, countDormantPawnsAsHostile: false) ||
                                        CaravanExitMapUtility.FindCaravanToJoinFor(__instance) != null);
        bool isTacticalOrSettlementRetreat = IncursionTacticalRetreatUtility.ShouldInterceptPlayerExitMap(
            isPlayerHome: map.IsPlayerHome,
            isOverlandEncounterMap: isOverlandEncounter,
            allowedToJoinOrCreateCaravan: allowedToJoinOrCreateCaravan,
            isCaravanMember: __instance.IsCaravanMember(),
            isTeleporting: __instance.teleporting,
            isInsideTransporter: isInsideTransporter,
            hasHomeRetreatCondition: hasHomeRetreatCondition);

        // Handle player colonists / animals / mechs falling back and reaching the map edge (when not being carried off by a kidnapper)
        if (isTacticalOrSettlementRetreat &&
            __instance.CarriedBy == null &&
            __instance.Faction == Faction.OfPlayer &&
            (__instance.IsColonist || __instance.RaceProps.Animal || __instance.IsColonyMech))
        {
            PlanetTile tile = map.Tile;
            Pawn? carriedPawn = __instance.carryTracker?.CarriedThing as Pawn;
            if (carriedPawn != null && __instance.carryTracker?.innerContainer != null && __instance.carryTracker.innerContainer.Contains(carriedPawn))
            {
                __instance.carryTracker.innerContainer.Remove(carriedPawn);
            }

            Caravan? existingCaravan = Find.WorldObjects?.Caravans?.FirstOrDefault(c => c.Tile == tile && c.Faction == Faction.OfPlayer);
            bool hadExistingCaravan = existingCaravan != null;

            var exitingPawns = new List<Pawn> { __instance };
            if (carriedPawn != null && carriedPawn.Faction == Faction.OfPlayer)
            {
                exitingPawns.Add(carriedPawn);
            }

            Caravan? caravan = IncursionTacticalRetreatUtility.AddOrFormPlayerCaravan(
                exitingPawns,
                tile,
                existingCaravan,
                wakeRemainingAnimalsOnMap: true);

            if (caravan != null)
            {
                Messages.Message(
                    hadExistingCaravan
                        ? $"{__instance.LabelShort} reached the map edge and joined the overland caravan."
                        : $"{__instance.LabelShort} reached the map edge and formed a retreating caravan on the Geoscape.",
                    caravan,
                    MessageTypeDefOf.PositiveEvent);
            }

            // Check if any conscious player colonists remain on the battlefield
            bool anyRemainingMobileColonists = map.mapPawns.AllPawns.Any(p =>
                p != __instance &&
                p != carriedPawn &&
                p.Faction == Faction.OfPlayer &&
                p.IsColonist &&
                p.Spawned &&
                !p.Dead &&
                !p.Downed);

            if (!anyRemainingMobileColonists)
            {
                // Gather any remaining player animals / mechs so pets are never left stranded when all colonists have retreated
                var remainingNonColonists = map.mapPawns.AllPawns
                    .Where(p => p != __instance && p != carriedPawn && p.Faction == Faction.OfPlayer && !p.IsColonist && p.Spawned && !p.Dead)
                    .ToList();
                if (remainingNonColonists.Count > 0)
                {
                    caravan = IncursionTacticalRetreatUtility.AddOrFormPlayerCaravan(remainingNonColonists, tile, caravan);
                }

                // Handle any downed player colonists remaining on the map
                var remainingDownedColonists = map.mapPawns.AllPawns
                    .Where(p => p != __instance && p != carriedPawn && p.Faction == Faction.OfPlayer && p.IsColonist && p.Spawned && !p.Dead && p.Downed)
                    .ToList();

                bool anyActiveHostiles = GenHostility.AnyHostileActiveThreatToPlayer(map, countDormantPawnsAsHostile: false);
                if (remainingDownedColonists.Count > 0 && !anyActiveHostiles)
                {
                    // No active hostiles left: recover downed colonists into the caravan
                    caravan = IncursionTacticalRetreatUtility.AddOrFormPlayerCaravan(remainingDownedColonists, tile, caravan);
                    remainingDownedColonists.Clear();
                }

                bool hasEncounterState = ambushComp != null && ambushComp.HasEncounterState;
                bool hasActiveIncursions = hasEncounterState && ambushComp!.GetActiveIncursionSummaries().Any();
                if (remainingDownedColonists.Count > 0 &&
                    IncursionTacticalRetreatUtility.ShouldAbductDownedColonistsOnExit(
                        map.Parent is CaravansBattlefield,
                        hasActiveIncursions))
                {
                    if (ambushComp != null && hasEncounterState)
                    {
                        foreach (var downed in remainingDownedColonists)
                        {
                            if (!ambushComp.CapturedColonists.Contains(downed))
                            {
                                ambushComp.CapturedColonists.Add(downed);
                            }
                        }
                    }
                    IncursionTacticalRetreatUtility.PassCapturedColonistsToWorld(remainingDownedColonists);
                }

                if (hasEncounterState)
                {
                    bool hasAnyCaptives = ambushComp!.CapturedColonists != null && ambushComp.CapturedColonists.Count > 0;

                    foreach (var (incursion, livingHostiles, capturedPawns, contactLabel) in ambushComp.GetActiveIncursionSummaries())
                    {
                        incursion.IsEngagedInTacticalMap = false;

                        if (livingHostiles.Count > 0)
                        {
                            IncursionTacticalRetreatUtility.DespawnAndPassHostilesToWorld(livingHostiles);
                            incursion.SetCommittedPawns(livingHostiles);
                        }

                        bool recordHasCaptives = hasAnyCaptives ||
                            (capturedPawns != null && capturedPawns.Count > 0) ||
                            (incursion.KidnappedColonists != null && incursion.KidnappedColonists.Count > 0);

                        if (recordHasCaptives)
                        {
                            var captives = (ambushComp.CapturedColonists != null && ambushComp.CapturedColonists.Count > 0)
                                ? ambushComp.CapturedColonists
                                : capturedPawns;

                            incursion.TransitionToOverlandRetreat(livingHostiles, captives, map);

                            Log.Message($"[Project Vigilance] Incursion {incursion.TrackingId} ({contactLabel}) abducted colonist(s) and is retreating on Geoscape towards {incursion.DestinationTile}.");
                            Messages.Message($"Enemy raid {contactLabel} has abducted colonist(s) and is retreating on the Geoscape!", incursion, MessageTypeDefOf.NegativeEvent);
                        }
                        else
                        {
                            IncursionTacticalRetreatUtility.ResumeIncursionAfterCaravanRetreat(incursion, caravan, livingHostiles);
                        }

                        if (caravan != null)
                        {
                            incursion.MarkCaravanMet(caravan.ID);
                            incursion.SetCaravanCooldown(caravan.ID, Find.TickManager.TicksGame + 7500);
                        }
                    }
                }

                // Clean up the tactical map and return to Geoscape
                bool anyPlayerPawnsOnMap = map.mapPawns.AllPawns.Any(p => p != __instance && p != carriedPawn && p.Faction == Faction.OfPlayer && p.Spawned && !p.Dead);
                if (!anyPlayerPawnsOnMap)
                {
                    IncursionTacticalRetreatUtility.CleanUpEncounterMap(map);
                    if (caravan != null)
                    {
                        CameraJumper.TryJumpAndSelect(caravan);
                    }
                }
            }

            return false; // Skip vanilla ExitMap discard
        }

        // Track all incursion pawns (including friendly trade caravans, pack camels, visitors, and hostile raiders)
        // departing player home maps OR caravan battlefields / ambush encounter maps.
        // IMPORTANT: We must despawn and pass them to WorldPawns with KeepForever BEFORE vanilla Pawn.ExitMap runs,
        // because vanilla Pawn.ExitMap calls WorldPawns.PassToWorld(this, PawnDiscardDecideMode.Decide) which
        // immediately destroys pack animals (camels/muffalo), solo visitors, and generic guards!
        bool isTrackedIncursionPawn = ambushComp != null && ambushComp.HasTrackedIncursionFor(__instance);
        bool isHostile = __instance.Faction.HostileTo(Faction.OfPlayer);

        if (IncursionTacticalRetreatUtility.ShouldTrackNonPlayerExitMap(
                map.IsPlayerHome,
                isOverlandEncounter,
                isTrackedIncursionPawn,
                isHostile))
        {
            Pawn? captive = null;
            if (__instance.carryTracker?.CarriedThing is Pawn carriedPawn && carriedPawn.Faction == Faction.OfPlayer)
            {
                captive = carriedPawn;
                if (__instance.carryTracker.innerContainer != null && __instance.carryTracker.innerContainer.Contains(carriedPawn))
                {
                    __instance.carryTracker.innerContainer.Remove(carriedPawn);
                }
                IncursionTacticalRetreatUtility.PassCapturedColonistsToWorld(new List<Pawn> { carriedPawn });
            }

            Lord? lord = __instance.GetLord();
            if (lord != null)
            {
                lord.RemovePawn(__instance);
                if (lord.ownedPawns.Count == 0 && map.lordManager != null && map.lordManager.lords.Contains(lord))
                {
                    map.lordManager.RemoveLord(lord);
                }
            }

            if (__instance.Spawned)
            {
                __instance.DeSpawn(DestroyMode.Vanish);
            }

            if (!__instance.Destroyed && !Find.WorldPawns.Contains(__instance))
            {
                Find.WorldPawns.PassToWorld(__instance, PawnDiscardDecideMode.KeepForever);
            }

            if (IncursionTrackingHelper.TryGetTraderIncursionFor(__instance, out var exitIncursion) && exitIncursion != null)
            {
                IncursionTrackingHelper.EnsureTraderComponents(__instance, exitIncursion);
            }

            if (isTrackedIncursionPawn)
            {
                ambushComp!.Notify_PawnExitedMap(__instance, captive);
            }
            else if (isHostile)
            {
                IncursionRetreatManager.RegisterFleeingPawn(__instance, map, captive);
            }

            return false;
        }

        return true;
    }
}
