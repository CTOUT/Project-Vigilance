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
    [System.ThreadStatic]
    public static PlanetTile? MouseTileOverride;

    /// <summary>
    /// Optional hook for settlement resolution during headless unit tests.
    /// </summary>
    public static System.Func<PlanetTile, Settlement?>? SettlementAtOverride { get; set; }

    private static WorldObjectsHolder? SafeWorldObjects => (Current.Game != null && Find.World != null) ? Find.WorldObjects : null;

    private static bool IsTileImpassable(PlanetTile tile)
    {
        if (Current.Game != null && Find.World != null)
        {
            return Find.World.Impassable(tile);
        }

        return false;
    }

    public static Settlement? GetSettlementAt(PlanetTile tile)
    {
        if (SettlementAtOverride != null)
        {
            return SettlementAtOverride(tile);
        }

        return SafeWorldObjects?.SettlementAt(tile);
    }

    public static bool IsPlayerCaravan(Caravan caravan)
    {
        if (caravan == null || caravan.Faction?.def?.isPlayer != true)
        {
            return false;
        }

        return true;
    }

    public static void Postfix(Vector2 mousePos, Caravan caravan, ref List<FloatMenuOption> __result)
    {
        if (!IsPlayerCaravan(caravan))
        {
            return;
        }

        PlanetTile clickedTile;
        Settlement? targetSettlement = null;

        if (MouseTileOverride.HasValue)
        {
            clickedTile = MouseTileOverride.Value;
            targetSettlement = GetSettlementAt(clickedTile);
        }
        else
        {
            try
            {
                var underMouse = GenWorldUI.WorldObjectsUnderMouse(mousePos);
                targetSettlement = underMouse?.OfType<Settlement>().FirstOrDefault();
                if (targetSettlement != null)
                {
                    clickedTile = targetSettlement.Tile;
                }
                else
                {
                    clickedTile = GenWorld.TileAt(mousePos, false);
                    if (clickedTile.Valid)
                    {
                        targetSettlement = GetSettlementAt(clickedTile);
                    }
                }
            }
            catch
            {
                clickedTile = GenWorld.TileAt(mousePos, false);
            }
        }

        if (!clickedTile.Valid)
        {
            return;
        }

        // 1. If the player caravan is standing on the clicked hex (Immediate local deployment)
        if (caravan.Tile == clickedTile)
        {
            Settlement? localSettlement = targetSettlement ?? GetSettlementAt(clickedTile);
            if (localSettlement != null)
            {
                if (caravan.pather != null && caravan.pather.Moving)
                {
                    __result ??= new List<FloatMenuOption>();
                    __result.Add(new FloatMenuOption($"Halt & hold position outside {localSettlement.Label}", () =>
                    {
                        caravan.pather.StopDead();
                        Messages.Message($"Caravan {caravan.LabelCap} halted outside {localSettlement.Label}.", caravan, MessageTypeDefOf.TaskCompletion, historical: false);
                    }, MenuOptionPriority.High));
                }
                return;
            }

            bool hasOutpost = SafeWorldObjects?.AllWorldObjects != null &&
                              SafeWorldObjects.AllWorldObjects.OfType<WorldObject_ListeningPost>().Any(p => p.Tile == clickedTile);

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
                        SafeWorldObjects?.Add(post);
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
                WorldObject_ListeningPost? post = SafeWorldObjects?.AllWorldObjects?.OfType<WorldObject_ListeningPost>().FirstOrDefault(p => p.Tile == clickedTile);
                if (post != null)
                {
                    __result ??= new List<FloatMenuOption>();
                    foreach (var opt in post.GetFloatMenuOptions(caravan))
                    {
                        AddOptionIfNotPresent(__result, opt);
                    }
                }
            }
        }
        else
        {
            // 2. Unexplored Tile in Black Shroud: Fog-of-war immersion
            // Do NOT leak impassable terrain, nearest passable tile coordinates, or offer construction expeditions into uncharted territory.
            bool isUnexplored = GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar &&
                                SensoryManager.Instance != null &&
                                !SensoryManager.Instance.IsExplored(clickedTile);

            if (isUnexplored)
            {
                __result = new List<FloatMenuOption>
                {
                    new FloatMenuOption($"Travel into unexplored sector (Tile {clickedTile.tileId})", () =>
                    {
                        caravan.pather.StartPath(clickedTile, null, repathImmediately: true, resetPauseStatus: true);
                        if (!caravan.pather.Moving)
                        {
                            Messages.Message($"Caravan {caravan.LabelCap} cannot find a route towards unexplored sector {clickedTile.tileId}.", MessageTypeDefOf.RejectInput, false);
                        }
                        else
                        {
                            Messages.Message($"Caravan {caravan.LabelCap} en route towards unexplored sector {clickedTile.tileId}.", MessageTypeDefOf.TaskCompletion, false);
                        }
                    }, MenuOptionPriority.Default)
                };
                return;
            }

            // 3. Distant Explored Passable Tile: Allow queued travel + construction / staging
            if (!IsTileImpassable(clickedTile))
            {
                __result ??= new List<FloatMenuOption>();

                targetSettlement ??= GetSettlementAt(clickedTile);
                if (targetSettlement != null)
                {
                    // Target tile contains an established settlement: offer staging outside the settlement
                    string settlementLabel = !string.IsNullOrEmpty(targetSettlement.Label)
                        ? (targetSettlement.Faction?.IsPlayer == true ? targetSettlement.Label : targetSettlement.LabelCap)
                        : "Colony";
                    string holdLabel = $"Move to hex (Hold position outside {settlementLabel})";

                    __result.AddRange(CaravanArrivalActionUtility.GetFloatMenuOptions(
                        () => CaravanArrivalAction_HoldPosition.CanHoldPosition(caravan, clickedTile),
                        () => new CaravanArrivalAction_HoldPosition(clickedTile, settlementLabel),
                        holdLabel,
                        caravan,
                        clickedTile,
                        targetSettlement));
                }
                else
                {
                    bool hasOutpost = SafeWorldObjects?.AllWorldObjects != null &&
                                      SafeWorldObjects.AllWorldObjects.OfType<WorldObject_ListeningPost>().Any(p => p.Tile == clickedTile);

                    if (!hasOutpost)
                    {
                        // Move here
                        __result.Add(new FloatMenuOption(
                            $"Move to Tile {clickedTile.tileId}",
                            () =>
                            {
                                caravan.pather.StartPath(clickedTile, null, repathImmediately: true, resetPauseStatus: true);
                            },
                            MenuOptionPriority.Default));

                        // Travel and deploy Automated Listening Post
                        bool canAffordRelay = OutpostMaterialUtility.CanDeployAutomatedRelay(caravan, out string missingRelay);
                        string relayLabel = canAffordRelay
                            ? $"Travel and deploy Automated Listening Post ({OutpostMaterialUtility.AutomatedRelaySteelCost} Steel, {OutpostMaterialUtility.AutomatedRelayComponentCost} Components)"
                            : $"Travel and deploy Automated Listening Post ({missingRelay})";

                        __result.Add(new FloatMenuOption(
                            relayLabel,
                            () =>
                            {
                                if (OutpostMaterialUtility.CanDeployAutomatedRelay(caravan, out _))
                                {
                                    caravan.pather.StartPath(
                                        clickedTile,
                                        new CaravanArrivalAction_ConstructOutpost(clickedTile, ListeningPostKind.AutomatedRelay),
                                        repathImmediately: true,
                                        resetPauseStatus: true);
                                    Messages.Message($"Caravan {caravan.LabelCap} en route to deploy automated listening post at Tile {clickedTile.tileId}.", MessageTypeDefOf.PositiveEvent, false);
                                }
                            },
                            MenuOptionPriority.Default)
                        {
                            Disabled = !canAffordRelay
                        });

                        // Travel and construct Signal Beacon
                        bool canAffordBeacon = OutpostMaterialUtility.CanDeploySignalBeacon(caravan, out string missingBeacon);
                        if (!canAffordBeacon)
                        {
                            __result.Add(new FloatMenuOption(
                                $"Travel and construct Signal Beacon ({missingBeacon})",
                                null,
                                MenuOptionPriority.Default)
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
                                    __result.Add(new FloatMenuOption(
                                        beaconLabel,
                                        () =>
                                        {
                                            if (OutpostMaterialUtility.CountItem(caravan, mat) >= OutpostMaterialUtility.SignalBeaconWoodOrStoneCost)
                                            {
                                                caravan.pather.StartPath(
                                                    clickedTile,
                                                    new CaravanArrivalAction_ConstructOutpost(clickedTile, ListeningPostKind.SignalBeacon, mat),
                                                    repathImmediately: true,
                                                    resetPauseStatus: true);
                                                Messages.Message($"Caravan {caravan.LabelCap} en route to construct signal beacon at Tile {clickedTile.tileId}.", MessageTypeDefOf.PositiveEvent, false);
                                            }
                                        },
                                        MenuOptionPriority.Default));
                                }
                            }
                            else
                            {
                                string beaconLabel = $"Travel and construct Signal Beacon ({OutpostMaterialUtility.SignalBeaconWoodOrStoneCost} Wood/Stone)";
                                __result.Add(new FloatMenuOption(
                                    beaconLabel,
                                    () =>
                                    {
                                        if (OutpostMaterialUtility.CanDeploySignalBeacon(caravan, out _))
                                        {
                                            caravan.pather.StartPath(
                                                clickedTile,
                                                new CaravanArrivalAction_ConstructOutpost(clickedTile, ListeningPostKind.SignalBeacon),
                                                repathImmediately: true,
                                                resetPauseStatus: true);
                                            Messages.Message($"Caravan {caravan.LabelCap} en route to construct signal beacon at Tile {clickedTile.tileId}.", MessageTypeDefOf.PositiveEvent, false);
                                        }
                                    },
                                    MenuOptionPriority.Default));
                            }
                        }
                    }
                    else
                    {
                        WorldObject_ListeningPost? post = SafeWorldObjects?.AllWorldObjects?.OfType<WorldObject_ListeningPost>().FirstOrDefault(p => p.Tile == clickedTile);
                        if (post != null)
                        {
                            if (post.IsConstructing)
                            {
                                // Travel and resume construction
                                AddOptionIfNotPresent(__result, new FloatMenuOption(
                                    $"Travel and resume construction of {post.LabelCap} ({post.ConstructionProgressPct}% complete)",
                                    () =>
                                    {
                                        caravan.pather.StartPath(clickedTile, null, repathImmediately: true, resetPauseStatus: true);
                                        Messages.Message($"Caravan {caravan.LabelCap} en route to resume construction of {post.LabelCap} at Tile {clickedTile.tileId}.", MessageTypeDefOf.PositiveEvent, false);
                                    },
                                    MenuOptionPriority.Default));

                                // Travel and Dismantle / Cancel (Refund 100% Materials)
                                AddOptionIfNotPresent(__result, new FloatMenuOption(
                                    $"Travel and Dismantle {post.LabelCap} (Refund 100% Materials)",
                                    () =>
                                    {
                                        caravan.pather.StartPath(
                                            clickedTile,
                                            new CaravanArrivalAction_DismantleOutpost(post, clickedTile),
                                            repathImmediately: true,
                                            resetPauseStatus: true);
                                        Messages.Message($"Caravan {caravan.LabelCap} en route to dismantle {post.LabelCap} at Tile {clickedTile.tileId}.", MessageTypeDefOf.PositiveEvent, false);
                                    },
                                    MenuOptionPriority.Default));
                            }
                            else if (post.IsDismantling)
                            {
                                // Travel and resume dismantling
                                AddOptionIfNotPresent(__result, new FloatMenuOption(
                                    $"Travel and resume dismantling of {post.LabelCap}",
                                    () =>
                                    {
                                        caravan.pather.StartPath(
                                            clickedTile,
                                            new CaravanArrivalAction_DismantleOutpost(post, clickedTile),
                                            repathImmediately: true,
                                            resetPauseStatus: true);
                                        Messages.Message($"Caravan {caravan.LabelCap} en route to resume dismantling of {post.LabelCap} at Tile {clickedTile.tileId}.", MessageTypeDefOf.PositiveEvent, false);
                                    },
                                    MenuOptionPriority.Default));
                            }
                            else
                            {
                                // Move here
                                AddOptionIfNotPresent(__result, new FloatMenuOption(
                                    $"Move to Tile {clickedTile.tileId}",
                                    () =>
                                    {
                                        caravan.pather.StartPath(clickedTile, null, repathImmediately: true, resetPauseStatus: true);
                                    },
                                    MenuOptionPriority.Default));

                                // Travel and Dismantle
                                AddOptionIfNotPresent(__result, new FloatMenuOption(
                                    $"Travel and Dismantle {post.LabelCap} (Recover 75% Materials)",
                                    () =>
                                    {
                                        caravan.pather.StartPath(
                                            clickedTile,
                                            new CaravanArrivalAction_DismantleOutpost(post, clickedTile),
                                            repathImmediately: true,
                                            resetPauseStatus: true);
                                        Messages.Message($"Caravan {caravan.LabelCap} en route to dismantle {post.LabelCap} at Tile {clickedTile.tileId}.", MessageTypeDefOf.PositiveEvent, false);
                                    },
                                    MenuOptionPriority.Default));
                            }
                        }
                    }
                }
            }
            // 4. Distant Explored Impassable Tile: Provide clear menu options rather than silent auto-movement
            else if (IsTileImpassable(clickedTile))
            {
                __result ??= new List<FloatMenuOption>();

                __result.Add(new FloatMenuOption($"Tile {clickedTile.tileId} is impassable (Mountain / Ocean)", null, MenuOptionPriority.Default)
                {
                    Disabled = true
                });

                PlanetTile bestNearby = CaravanUtility.BestGotoDestNear(clickedTile, caravan);
                if (bestNearby.Valid && bestNearby != caravan.Tile)
                {
                    __result.Add(new FloatMenuOption(
                        $"Move to nearest passable tile (Tile {bestNearby.tileId})",
                        () =>
                        {
                            caravan.pather.StartPath(bestNearby, null, repathImmediately: true, resetPauseStatus: true);
                        },
                        MenuOptionPriority.Default));

                    bool hasOutpostNearby = SafeWorldObjects?.AllWorldObjects != null &&
                                            SafeWorldObjects.AllWorldObjects.OfType<WorldObject_ListeningPost>().Any(p => p.Tile == bestNearby);

                    if (!hasOutpostNearby)
                    {
                        bool canAffordRelay = OutpostMaterialUtility.CanDeployAutomatedRelay(caravan, out string missingRelay);
                        string relayLabel = canAffordRelay
                            ? $"Travel and deploy Automated Listening Post (Nearest: Tile {bestNearby.tileId})"
                            : $"Travel and deploy Automated Listening Post (Nearest: Tile {bestNearby.tileId}) ({missingRelay})";

                        __result.Add(new FloatMenuOption(
                            relayLabel,
                            () =>
                            {
                                if (OutpostMaterialUtility.CanDeployAutomatedRelay(caravan, out _))
                                {
                                    caravan.pather.StartPath(
                                        bestNearby,
                                        new CaravanArrivalAction_ConstructOutpost(bestNearby, ListeningPostKind.AutomatedRelay),
                                        repathImmediately: true,
                                        resetPauseStatus: true);
                                    Messages.Message($"Caravan {caravan.LabelCap} en route to deploy automated listening post at Tile {bestNearby.tileId}.", MessageTypeDefOf.PositiveEvent, false);
                                }
                            },
                            MenuOptionPriority.Default)
                        {
                            Disabled = !canAffordRelay
                        });

                        bool canAffordBeacon = OutpostMaterialUtility.CanDeploySignalBeacon(caravan, out string missingBeacon);
                        string beaconLabel = canAffordBeacon
                            ? $"Travel and construct Signal Beacon (Nearest: Tile {bestNearby.tileId})"
                            : $"Travel and construct Signal Beacon (Nearest: Tile {bestNearby.tileId}) ({missingBeacon})";

                        __result.Add(new FloatMenuOption(
                            beaconLabel,
                            () =>
                            {
                                if (OutpostMaterialUtility.CanDeploySignalBeacon(caravan, out _))
                                {
                                    caravan.pather.StartPath(
                                        bestNearby,
                                        new CaravanArrivalAction_ConstructOutpost(bestNearby, ListeningPostKind.SignalBeacon),
                                        repathImmediately: true,
                                        resetPauseStatus: true);
                                    Messages.Message($"Caravan {caravan.LabelCap} en route to construct signal beacon at Tile {bestNearby.tileId}.", MessageTypeDefOf.PositiveEvent, false);
                                }
                            },
                            MenuOptionPriority.Default)
                        {
                            Disabled = !canAffordBeacon
                        });
                    }
                }
            }
        }

        DeduplicateOptions(__result);
    }

    private static void AddOptionIfNotPresent(List<FloatMenuOption> list, FloatMenuOption option)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i].Label, option.Label, System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }
        list.Add(option);
    }

    private static void DeduplicateOptions(List<FloatMenuOption>? list)
    {
        if (list == null || list.Count <= 1) return;
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        int i = 0;
        while (i < list.Count)
        {
            if (!seen.Add(list[i].Label))
            {
                list.RemoveAt(i);
            }
            else
            {
                i++;
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
        SafeWorldObjects?.Add(post);
        Messages.Message($"Signal beacon ({materialDef.LabelCap}) construction commenced.", MessageTypeDefOf.PositiveEvent, false);
    }
}
