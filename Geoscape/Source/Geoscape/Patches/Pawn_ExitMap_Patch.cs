using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using ProjectVigilance.Geoscape.World;

namespace ProjectVigilance.Geoscape.Patches;

[HarmonyPatch(typeof(Pawn), nameof(Pawn.ExitMap))]
public static class Pawn_ExitMap_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(Pawn __instance, out (Map? map, Pawn? captive) __state)
    {
        __state = (null, null);

        if (__instance == null || __instance.Dead || __instance.Faction == null)
        {
            return true;
        }

        Map? map = __instance.Map;
        if (map == null)
        {
            return true;
        }

        bool isTacticalEncounterMap = !map.IsPlayerHome &&
                                      (map.Parent is CaravansBattlefield ||
                                       map.GetComponent<MapComponent_OverlandAmbush>() != null);

        // Handle player colonists / animals falling back and reaching the map edge on a tactical encounter
        if (isTacticalEncounterMap && __instance.Faction == Faction.OfPlayer && (__instance.IsColonist || __instance.RaceProps.Animal))
        {
            PlanetTile tile = map.Tile;
            Pawn? carriedPawn = __instance.carryTracker?.CarriedThing as Pawn;

            // Find existing player caravan at this tile
            Caravan? caravan = Find.WorldObjects?.Caravans?.FirstOrDefault(c => c.Tile == tile && c.Faction == Faction.OfPlayer);

            if (caravan != null)
            {
                if (__instance.Spawned)
                {
                    __instance.DeSpawn(DestroyMode.Vanish);
                }
                caravan.AddPawn(__instance, addCarriedPawnToWorldPawnsIfAny: true);

                if (carriedPawn != null)
                {
                    if (carriedPawn.Spawned)
                    {
                        carriedPawn.DeSpawn(DestroyMode.Vanish);
                    }
                    caravan.AddPawn(carriedPawn, addCarriedPawnToWorldPawnsIfAny: true);
                }

                Messages.Message(
                    $"{__instance.LabelShort} reached the map edge and joined the overland caravan.",
                    caravan,
                    MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                var caravanPawns = new List<Pawn> { __instance };
                if (carriedPawn != null)
                {
                    caravanPawns.Add(carriedPawn);
                }

                if (__instance.Spawned)
                {
                    __instance.DeSpawn(DestroyMode.Vanish);
                }
                if (carriedPawn != null && carriedPawn.Spawned)
                {
                    carriedPawn.DeSpawn(DestroyMode.Vanish);
                }

                caravan = CaravanMaker.MakeCaravan(caravanPawns, Faction.OfPlayer, tile, addToWorldPawnsIfNotAlready: true);

                Messages.Message(
                    $"{__instance.LabelShort} reached the map edge and formed a retreating caravan on the Geoscape.",
                    caravan,
                    MessageTypeDefOf.PositiveEvent);
            }

            // Check if any conscious player colonists remain on the battlefield
            bool anyRemainingMobileColonists = map.mapPawns.AllPawns.Any(p => p != __instance && p != carriedPawn && p.Faction == Faction.OfPlayer && p.IsColonist && p.Spawned && !p.Dead && !p.Downed);
            if (!anyRemainingMobileColonists)
            {
                var ambushComp = map.GetComponent<MapComponent_OverlandAmbush>();
                if (ambushComp != null)
                {
                    bool hasAnyCaptives = ambushComp.CapturedColonists != null && ambushComp.CapturedColonists.Count > 0;

                    if (ambushComp.Engagements != null && ambushComp.Engagements.Count > 0)
                    {
                        foreach (var record in ambushComp.Engagements)
                        {
                            if (record.incursion != null && record.incursion.Spawned)
                            {
                                record.incursion.IsEngagedInTacticalMap = false;

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

                                    var livingHostiles = record.trackedPawns.Where(p => p != null && !p.Dead && !p.Destroyed).ToList();
                                    if (livingHostiles.Count > 0)
                                    {
                                        record.incursion.SetCommittedPawns(livingHostiles);
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

                        bool sourceHasCaptives = hasAnyCaptives ||
                            (sourceIncursion.KidnappedColonists != null && sourceIncursion.KidnappedColonists.Count > 0);

                        if (sourceHasCaptives)
                        {
                            sourceIncursion.IsRetreating = true;
                            if (ambushComp.CapturedColonists != null && ambushComp.CapturedColonists.Count > 0)
                            {
                                sourceIncursion.SetKidnappedColonists(new List<Pawn>(ambushComp.CapturedColonists));
                            }

                            var livingHostiles = ambushComp.GetLivingEnemies();
                            if (livingHostiles.Count > 0)
                            {
                                sourceIncursion.SetCommittedPawns(livingHostiles);
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

                // If no living player pawns are on the map at all, clean up the tactical map and return to Geoscape
                bool anyPlayerPawnsOnMap = map.mapPawns.AllPawns.Any(p => p != __instance && p != carriedPawn && p.Faction == Faction.OfPlayer && p.Spawned && !p.Dead);
                if (!anyPlayerPawnsOnMap)
                {
                    IncursionTacticalRetreatUtility.CleanUpEncounterMap(map);
                    CameraJumper.TryJumpAndSelect(caravan);
                }
            }

            return false; // Skip vanilla ExitMap discard
        }

        // Track hostile raiders departing player home maps OR caravan battlefields / ambush encounter maps
        bool isTrackedMap = map.IsPlayerHome ||
                            map.Parent is CaravansBattlefield ||
                            map.GetComponent<MapComponent_OverlandAmbush>() != null;

        if (isTrackedMap && __instance.Faction.HostileTo(Faction.OfPlayer))
        {
            Pawn? captive = null;
            if (__instance.carryTracker?.CarriedThing is Pawn carriedPawn && carriedPawn.Faction == Faction.OfPlayer)
            {
                captive = carriedPawn;
            }

            __state = (map, captive);

            var ambushComp = map.GetComponent<MapComponent_OverlandAmbush>();
            ambushComp?.Notify_PawnExitedMap(__instance, captive);
        }

        return true;
    }

    [HarmonyPostfix]
    public static void Postfix(Pawn __instance, (Map? map, Pawn? captive) __state)
    {
        if (__state.map != null && __instance != null && __instance.Faction != null)
        {
            var ambushComp = __state.map.GetComponent<MapComponent_OverlandAmbush>();
            if (ambushComp != null && ambushComp.HasTrackedIncursionFor(__instance))
            {
                if (!__instance.Destroyed && !Find.WorldPawns.Contains(__instance))
                {
                    Find.WorldPawns.PassToWorld(__instance, PawnDiscardDecideMode.KeepForever);
                }
            }
            else
            {
                IncursionRetreatManager.RegisterFleeingPawn(__instance, __state.map, __state.captive);
            }
        }
    }
}
