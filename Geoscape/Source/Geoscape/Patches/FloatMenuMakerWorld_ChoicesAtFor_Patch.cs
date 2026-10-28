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
                        Find.WorldObjects?.Add(post);
                        Messages.Message("Automated listening post deployed and active on geoscape.", MessageTypeDefOf.PositiveEvent, false);
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
        }
        // 2. Distant Tile: Allow queued travel + construction
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
        Find.WorldObjects?.Add(post);
        Messages.Message($"Signal beacon ({materialDef.LabelCap}) constructed and scanning the sector.", MessageTypeDefOf.PositiveEvent, false);
    }
}
