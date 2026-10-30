using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Hooks right-click context menus on the Geoscape globe to allow player caravans
/// to deploy lightweight unmanned listening posts and signal beacons directly from carried inventory,
/// both immediately on the current tile or queued upon arrival at a designated distant tile.
/// Also provides clear menu interface on impassable terrain rather than silently auto-moving.
/// </summary>
[HarmonyPatch(typeof(FloatMenuMakerWorld), nameof(FloatMenuMakerWorld.ChoicesAtFor), typeof(Vector2), typeof(Caravan))]
public static class FloatMenuMakerWorld_ChoicesAtFor_Patch
{
    public static void Postfix(Vector2 mousePos, Caravan caravan, ref List<FloatMenuOption> __result)
    {
        if (caravan == null || !caravan.IsPlayerControlled || !caravan.Spawned)
        {
            return;
        }

        PlanetTile clickedTile = GenWorld.MouseTile(false);
        if (!clickedTile.Valid)
        {
            return;
        }

        // 1. If the player caravan is standing on the clicked hex (Immediate local deployment)
        if (caravan.Tile == clickedTile)
        {
            bool hasOutpost = Find.WorldObjects?.AllWorldObjects != null &&
                              Find.WorldObjects.AllWorldObjects.OfType<WorldObject_ListeningPost>().Any(p => p.Tile == clickedTile);

            if (!hasOutpost)
            {
                __result ??= new List<FloatMenuOption>();

                // 1. Deploy Automated Listening Post (100 Steel, 4 Components)
                bool canAffordRelay = OutpostMaterialUtility.CanDeployAutomatedRelay(caravan, out string missingRelay);
                string relayLabel = canAffordRelay
                    ? $"Deploy Automated Listening Post ({OutpostMaterialUtility.AutomatedRelaySteelCost} Steel, {OutpostMaterialUtility.AutomatedRelayComponentCost} Components)"
                    : $"Deploy Automated Listening Post ({missingRelay})";

                __result.Add(new FloatMenuOption(relayLabel, () =>
                {
                    if (OutpostMaterialUtility.CanDeployAutomatedRelay(caravan, out _))
                    {
                        OutpostMaterialUtility.ConsumeAutomatedRelayDeployment(caravan);
                        var def = DefDatabase<WorldObjectDef>.GetNamed("Vigilance_ListeningPost_Automated");
                        var post = (WorldObject_ListeningPost)WorldObjectMaker.MakeWorldObject(def);
                        post.Tile = clickedTile;
                        post.SetFaction(Faction.OfPlayer);
                        post.Kind = ListeningPostKind.AutomatedRelay;
                        post.StartConstruction(2500);
                        Find.WorldObjects?.Add(post);
                        Messages.Message("Automated listening post deployment commenced. Construction underway.", MessageTypeDefOf.PositiveEvent, false);
                    }
                }, MenuOptionPriority.Default)
                {
                    Disabled = !canAffordRelay
                });

                // 2. Construct Signal Beacon (150 Wood/Stone)
                bool canAffordBeacon = OutpostMaterialUtility.CanDeploySignalBeacon(caravan, out string missingBeacon);
                if (!canAffordBeacon)
                {
                    __result.Add(new FloatMenuOption($"Construct Signal Beacon ({missingBeacon})", null, MenuOptionPriority.Default)
                    {
                        Disabled = true
                    });
                }
                else
                {
                    List<ThingDef> materials = OutpostMaterialUtility.GetAvailableBeaconMaterials(caravan);
                    List<ThingDef> completeMaterials = materials.Where(m => OutpostMaterialUtility.CountItem(caravan, m) >= OutpostMaterialUtility.SignalBeaconWoodOrStoneCost).ToList();

                    if (completeMaterials.Count > 0)
                    {
                        for (int m = 0; m < completeMaterials.Count; m++)
                        {
                            ThingDef mat = completeMaterials[m];
                            string beaconLabel = $"Construct Signal Beacon ({OutpostMaterialUtility.SignalBeaconWoodOrStoneCost} {mat.LabelCap})";
                            __result.Add(new FloatMenuOption(beaconLabel, () =>
                            {
                                if (OutpostMaterialUtility.CountItem(caravan, mat) >= OutpostMaterialUtility.SignalBeaconWoodOrStoneCost)
                                {
                                    ThingDef? consumed = OutpostMaterialUtility.ConsumeSignalBeaconDeployment(caravan, mat);
                                    DeployBeacon(clickedTile, consumed ?? mat);
                                }
                            }, MenuOptionPriority.Default));
                        }
                    }
                    else
                    {
                        string beaconLabel = $"Construct Signal Beacon ({OutpostMaterialUtility.SignalBeaconWoodOrStoneCost} Wood/Stone)";
                        __result.Add(new FloatMenuOption(beaconLabel, () =>
                        {
                            if (OutpostMaterialUtility.CanDeploySignalBeacon(caravan, out _))
                            {
                                ThingDef? consumed = OutpostMaterialUtility.ConsumeSignalBeaconDeployment(caravan);
                                DeployBeacon(clickedTile, consumed ?? ThingDefOf.WoodLog);
                            }
                        }, MenuOptionPriority.Default));
                    }
                }
            }
            else
            {
                WorldObject_ListeningPost? post = Find.WorldObjects?.AllWorldObjects?.OfType<WorldObject_ListeningPost>().FirstOrDefault(p => p.Tile == clickedTile);
                if (post != null)
                {
                    __result ??= new List<FloatMenuOption>();

                    if (post.IsConstructing)
                    {
                        __result.Add(new FloatMenuOption($"Cancel Construction of {post.LabelCap} (Refund 100% Materials)", () =>
                        {
                            post.CancelConstruction(caravan);
                        }, MenuOptionPriority.High));
                    }
                    else if (post.IsDismantling)
                    {
                        __result.Add(new FloatMenuOption($"Cancel Dismantling of {post.LabelCap}", () =>
                        {
                            post.CancelDismantling();
                        }, MenuOptionPriority.High));
                    }
                    else
                    {
                        if (post.IsSabotaged)
                        {
                            bool canRepair = post.HasRepairMaterials(caravan, out string missingDesc);
                            string repairLabel = canRepair
                                ? $"Repair {post.LabelCap}"
                                : $"Repair {post.LabelCap} ({missingDesc})";

                            __result.Add(new FloatMenuOption(repairLabel, () =>
                            {
                                post.TryRepair(caravan);
                            }, MenuOptionPriority.High)
                            {
                                Disabled = !canRepair
                            });
                        }

                        __result.Add(new FloatMenuOption($"Dismantle {post.LabelCap} (Takes time, recover 75% materials)", () =>
                        {
                            post.StartDismantling(1500);
                        }, MenuOptionPriority.Low));
                    }
                }
            }
        }
        // 2. Distant Passable Tile: Allow queued travel + construction
        else if (Find.World != null && !Find.World.Impassable(clickedTile))
        {
            bool hasOutpost = Find.WorldObjects?.AllWorldObjects != null &&
                              Find.WorldObjects.AllWorldObjects.OfType<WorldObject_ListeningPost>().Any(p => p.Tile == clickedTile);

            if (!hasOutpost)
            {
                __result ??= new List<FloatMenuOption>();

                // Move here
                __result.Add(new FloatMenuOption($"Move to Tile {clickedTile}", () =>
                {
                    caravan.pather.StartPath(clickedTile, null, repathImmediately: true, resetPauseStatus: true);
                }, MenuOptionPriority.Default));

                // Travel and deploy Automated Listening Post
                bool canAffordRelay = OutpostMaterialUtility.CanDeployAutomatedRelay(caravan, out string missingRelay);
                string relayLabel = canAffordRelay
                    ? $"Travel and deploy Automated Listening Post ({OutpostMaterialUtility.AutomatedRelaySteelCost} Steel, {OutpostMaterialUtility.AutomatedRelayComponentCost} Components)"
                    : $"Travel and deploy Automated Listening Post ({missingRelay})";

                __result.Add(new FloatMenuOption(relayLabel, () =>
                {
                    if (OutpostMaterialUtility.CanDeployAutomatedRelay(caravan, out _))
                    {
                        caravan.pather.StartPath(
                            clickedTile,
                            new CaravanArrivalAction_ConstructOutpost(clickedTile, ListeningPostKind.AutomatedRelay),
                            repathImmediately: true,
                            resetPauseStatus: true);
                        Messages.Message($"Caravan {caravan.LabelCap} en route to deploy automated listening post at Tile {clickedTile}.", MessageTypeDefOf.PositiveEvent, false);
                    }
                }, MenuOptionPriority.Default)
                {
                    Disabled = !canAffordRelay
                });

                // Travel and construct Signal Beacon
                bool canAffordBeacon = OutpostMaterialUtility.CanDeploySignalBeacon(caravan, out string missingBeacon);
                if (!canAffordBeacon)
                {
                    __result.Add(new FloatMenuOption($"Travel and construct Signal Beacon ({missingBeacon})", null, MenuOptionPriority.Default)
                    {
                        Disabled = true
                    });
                }
                else
                {
                    List<ThingDef> materials = OutpostMaterialUtility.GetAvailableBeaconMaterials(caravan);
                    List<ThingDef> completeMaterials = materials.Where(m => OutpostMaterialUtility.CountItem(caravan, m) >= OutpostMaterialUtility.SignalBeaconWoodOrStoneCost).ToList();

                    if (completeMaterials.Count > 0)
                    {
                        for (int m = 0; m < completeMaterials.Count; m++)
                        {
                            ThingDef mat = completeMaterials[m];
                            string beaconLabel = $"Travel and construct Signal Beacon ({OutpostMaterialUtility.SignalBeaconWoodOrStoneCost} {mat.LabelCap})";
                            __result.Add(new FloatMenuOption(beaconLabel, () =>
                            {
                                if (OutpostMaterialUtility.CountItem(caravan, mat) >= OutpostMaterialUtility.SignalBeaconWoodOrStoneCost)
                                {
                                    caravan.pather.StartPath(
                                        clickedTile,
                                        new CaravanArrivalAction_ConstructOutpost(clickedTile, ListeningPostKind.SignalBeacon, mat),
                                        repathImmediately: true,
                                        resetPauseStatus: true);
                                    Messages.Message($"Caravan {caravan.LabelCap} en route to construct signal beacon at Tile {clickedTile}.", MessageTypeDefOf.PositiveEvent, false);
                                }
                            }, MenuOptionPriority.Default));
                        }
                    }
                    else
                    {
                        string beaconLabel = $"Travel and construct Signal Beacon ({OutpostMaterialUtility.SignalBeaconWoodOrStoneCost} Wood/Stone)";
                        __result.Add(new FloatMenuOption(beaconLabel, () =>
                        {
                            if (OutpostMaterialUtility.CanDeploySignalBeacon(caravan, out _))
                            {
                                caravan.pather.StartPath(
                                    clickedTile,
                                    new CaravanArrivalAction_ConstructOutpost(clickedTile, ListeningPostKind.SignalBeacon),
                                    repathImmediately: true,
                                    resetPauseStatus: true);
                                Messages.Message($"Caravan {caravan.LabelCap} en route to construct signal beacon at Tile {clickedTile}.", MessageTypeDefOf.PositiveEvent, false);
                            }
                        }, MenuOptionPriority.Default));
                    }
                }
            }
            else
            {
                WorldObject_ListeningPost? post = Find.WorldObjects?.AllWorldObjects?.OfType<WorldObject_ListeningPost>().FirstOrDefault(p => p.Tile == clickedTile);
                if (post != null)
                {
                    __result ??= new List<FloatMenuOption>();

                    // Move here
                    __result.Add(new FloatMenuOption($"Move to Tile {clickedTile}", () =>
                    {
                        caravan.pather.StartPath(clickedTile, null, repathImmediately: true, resetPauseStatus: true);
                    }, MenuOptionPriority.Default));

                    // Travel and Dismantle
                    __result.Add(new FloatMenuOption($"Travel and Dismantle {post.LabelCap} (Recover 75% Materials)", () =>
                    {
                        caravan.pather.StartPath(
                            clickedTile,
                            new CaravanArrivalAction_DismantleOutpost(post, clickedTile),
                            repathImmediately: true,
                            resetPauseStatus: true);
                        Messages.Message($"Caravan {caravan.LabelCap} en route to dismantle {post.LabelCap} at Tile {clickedTile}.", MessageTypeDefOf.PositiveEvent, false);
                    }, MenuOptionPriority.Default));
                }
            }
        }
        // 3. Impassable Tile: Provide clear menu options rather than silent auto-movement
        else if (Find.World != null && Find.World.Impassable(clickedTile))
        {
            __result ??= new List<FloatMenuOption>();

            __result.Add(new FloatMenuOption($"Tile {clickedTile} is impassable (Mountain / Ocean)", null, MenuOptionPriority.Default)
            {
                Disabled = true
            });

            PlanetTile bestNearby = CaravanUtility.BestGotoDestNear(clickedTile, caravan);
            if (bestNearby.Valid && bestNearby != caravan.Tile)
            {
                __result.Add(new FloatMenuOption($"Move to nearest passable tile (Tile {bestNearby})", () =>
                {
                    caravan.pather.StartPath(bestNearby, null, repathImmediately: true, resetPauseStatus: true);
                }, MenuOptionPriority.Default));

                bool hasOutpostNearby = Find.WorldObjects?.AllWorldObjects != null &&
                                        Find.WorldObjects.AllWorldObjects.OfType<WorldObject_ListeningPost>().Any(p => p.Tile == bestNearby);

                if (!hasOutpostNearby)
                {
                    bool canAffordRelay = OutpostMaterialUtility.CanDeployAutomatedRelay(caravan, out string missingRelay);
                    string relayLabel = canAffordRelay
                        ? $"Travel and deploy Automated Listening Post (Nearest: Tile {bestNearby})"
                        : $"Travel and deploy Automated Listening Post (Nearest: Tile {bestNearby}) ({missingRelay})";

                    __result.Add(new FloatMenuOption(relayLabel, () =>
                    {
                        if (OutpostMaterialUtility.CanDeployAutomatedRelay(caravan, out _))
                        {
                            caravan.pather.StartPath(
                                bestNearby,
                                new CaravanArrivalAction_ConstructOutpost(bestNearby, ListeningPostKind.AutomatedRelay),
                                repathImmediately: true,
                                resetPauseStatus: true);
                            Messages.Message($"Caravan {caravan.LabelCap} en route to deploy automated listening post at Tile {bestNearby}.", MessageTypeDefOf.PositiveEvent, false);
                        }
                    }, MenuOptionPriority.Default)
                    {
                        Disabled = !canAffordRelay
                    });

                    bool canAffordBeacon = OutpostMaterialUtility.CanDeploySignalBeacon(caravan, out string missingBeacon);
                    string beaconLabel = canAffordBeacon
                        ? $"Travel and construct Signal Beacon (Nearest: Tile {bestNearby})"
                        : $"Travel and construct Signal Beacon (Nearest: Tile {bestNearby}) ({missingBeacon})";

                    __result.Add(new FloatMenuOption(beaconLabel, () =>
                    {
                        if (OutpostMaterialUtility.CanDeploySignalBeacon(caravan, out _))
                        {
                            caravan.pather.StartPath(
                                bestNearby,
                                new CaravanArrivalAction_ConstructOutpost(bestNearby, ListeningPostKind.SignalBeacon),
                                repathImmediately: true,
                                resetPauseStatus: true);
                            Messages.Message($"Caravan {caravan.LabelCap} en route to construct signal beacon at Tile {bestNearby}.", MessageTypeDefOf.PositiveEvent, false);
                        }
                    }, MenuOptionPriority.Default)
                    {
                        Disabled = !canAffordBeacon
                    });
                }
            }
        }
    }

    private static void DeployBeacon(PlanetTile tile, ThingDef materialDef)
    {
        var def = DefDatabase<WorldObjectDef>.GetNamed("Vigilance_ListeningPost_Beacon");
        var post = (WorldObject_ListeningPost)WorldObjectMaker.MakeWorldObject(def);
        post.Tile = tile;
        post.SetFaction(Faction.OfPlayer);
        post.Kind = ListeningPostKind.SignalBeacon;
        post.stuffDef = materialDef;
        post.StartConstruction(2500);
        Find.WorldObjects?.Add(post);
        Messages.Message($"Signal beacon ({materialDef.LabelCap}) construction commenced.", MessageTypeDefOf.PositiveEvent, false);
    }
}
