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
    public static bool Prefix(Pawn __instance)
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

        bool isTacticalOrSettlementRetreat = map.Parent is CaravansBattlefield ||
                                             map.GetComponent<MapComponent_OverlandAmbush>() != null ||
                                             map.IsPlayerHome;

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

                var ambushComp = map.GetComponent<MapComponent_OverlandAmbush>();
                if (ambushComp != null)
                {
                    if (remainingDownedColonists.Count > 0)
                    {
                        foreach (var downed in remainingDownedColonists)
                        {
                            if (!ambushComp.CapturedColonists.Contains(downed))
                            {
                                ambushComp.CapturedColonists.Add(downed);
                            }
                        }
                        IncursionTacticalRetreatUtility.PassCapturedColonistsToWorld(remainingDownedColonists);
                    }

                    bool hasAnyCaptives = ambushComp.CapturedColonists != null && ambushComp.CapturedColonists.Count > 0;

                    if (ambushComp.Engagements != null && ambushComp.Engagements.Count > 0)
                    {
                        foreach (var record in ambushComp.Engagements)
                        {
                            if (record.incursion != null && record.incursion.Spawned)
                            {
                                record.incursion.IsEngagedInTacticalMap = false;

                                var livingHostiles = record.trackedPawns
                                    .Concat(record.escapedPawns ?? Enumerable.Empty<Pawn>())
                                    .Where(p => p != null && !p.Dead && !p.Destroyed)
                                    .Distinct()
                                    .ToList();
                                if (livingHostiles.Count > 0)
                                {
                                    IncursionTacticalRetreatUtility.DespawnAndPassHostilesToWorld(livingHostiles);
                                    record.incursion.SetCommittedPawns(livingHostiles);
                                }

                                bool recordHasCaptives = hasAnyCaptives ||
                                    (record.capturedColonists != null && record.capturedColonists.Count > 0) ||
                                    (record.incursion.KidnappedColonists != null && record.incursion.KidnappedColonists.Count > 0);

                                if (recordHasCaptives)
                                {
                                    record.incursion.IsRetreating = true;
                                    if (ambushComp.CapturedColonists != null && ambushComp.CapturedColonists.Count > 0)
                                    {
                                        record.incursion.SetKidnappedColonists(new List<Pawn>(ambushComp.CapturedColonists));
                                    }
                                    else if (record.capturedColonists != null && record.capturedColonists.Count > 0)
                                    {
                                        record.incursion.SetKidnappedColonists(new List<Pawn>(record.capturedColonists));
                                    }

                                    if (livingHostiles.Count > 0)
                                    {
                                        record.incursion.SetRetreatingPawns(livingHostiles);
                                    }

                                    if (IncursionOriginResolver.TryResolveRetreatRoute(map.Tile, record.incursion.Faction, out var destTile, out var retreatPath, out var speed))
                                    {
                                        record.incursion.SetRetreatRoute(destTile, retreatPath, speed);
                                    }
                                    else
                                    {
                                        record.incursion.ReverseCourse("escaping with captives after engagement");
                                    }

                                    Log.Message($"[Project Vigilance] Incursion {record.incursion.TrackingId} ({record.contactLabel}) abducted colonist(s) and is retreating on Geoscape towards {record.incursion.DestinationTile}.");
                                    Messages.Message($"Enemy raid {record.contactLabel} has abducted colonist(s) and is retreating on the Geoscape!", record.incursion, MessageTypeDefOf.NegativeEvent);
                                }
                                else
                                {
                                    record.incursion.AdvancePathTile();
                                }

                                if (caravan != null)
                                {
                                    record.incursion.MarkCaravanMet(caravan.ID);
                                    record.incursion.SetCaravanCooldown(caravan.ID, Find.TickManager.TicksGame + 7500);
                                }
                            }
                        }
                    }
                    else if (ambushComp.SourceIncursion != null && ambushComp.SourceIncursion.Spawned)
                    {
                        var sourceIncursion = ambushComp.SourceIncursion;
                        sourceIncursion.IsEngagedInTacticalMap = false;

                        var livingHostiles = ambushComp.GetLivingEnemies();
                        if (livingHostiles.Count > 0)
                        {
                            IncursionTacticalRetreatUtility.DespawnAndPassHostilesToWorld(livingHostiles);
                            sourceIncursion.SetCommittedPawns(livingHostiles);
                        }

                        bool sourceHasCaptives = hasAnyCaptives ||
                            (sourceIncursion.KidnappedColonists != null && sourceIncursion.KidnappedColonists.Count > 0);

                        if (sourceHasCaptives)
                        {
                            sourceIncursion.IsRetreating = true;
                            if (ambushComp.CapturedColonists != null && ambushComp.CapturedColonists.Count > 0)
                            {
                                sourceIncursion.SetKidnappedColonists(new List<Pawn>(ambushComp.CapturedColonists));
                            }

                            if (livingHostiles.Count > 0)
                            {
                                sourceIncursion.SetRetreatingPawns(livingHostiles);
                            }

                            if (IncursionOriginResolver.TryResolveRetreatRoute(map.Tile, sourceIncursion.Faction, out var destTile, out var retreatPath, out var speed))
                            {
                                sourceIncursion.SetRetreatRoute(destTile, retreatPath, speed);
                            }
                            else
                            {
                                sourceIncursion.ReverseCourse("escaping with captives after engagement");
                            }

                            Log.Message($"[Project Vigilance] Incursion {sourceIncursion.TrackingId} abducted colonist(s) and is retreating on Geoscape towards {sourceIncursion.DestinationTile}.");
                            Messages.Message($"Enemy raid has abducted colonist(s) and is retreating on the Geoscape!", sourceIncursion, MessageTypeDefOf.NegativeEvent);
                        }
                        else
                        {
                            sourceIncursion.AdvancePathTile();
                        }

                        if (caravan != null)
                        {
                            sourceIncursion.MarkCaravanMet(caravan.ID);
                            sourceIncursion.SetCaravanCooldown(caravan.ID, Find.TickManager.TicksGame + 7500);
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
        var ambushCompForExit = map.GetComponent<MapComponent_OverlandAmbush>();
        bool isTrackedIncursionPawn = ambushCompForExit != null && ambushCompForExit.HasTrackedIncursionFor(__instance);
        bool isTrackedMap = map.IsPlayerHome ||
                            map.Parent is CaravansBattlefield ||
                            ambushCompForExit != null;
        bool isHostile = __instance.Faction.HostileTo(Faction.OfPlayer);

        if (isTrackedMap && (isTrackedIncursionPawn || isHostile))
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
                ambushCompForExit!.Notify_PawnExitedMap(__instance, captive);
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
