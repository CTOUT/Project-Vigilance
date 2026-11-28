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

            bool hasOutpost = OutpostTileOccupancyUtility.TryGetListeningPostAt(clickedTile, out WorldObject_ListeningPost? post);
            WorldObject? blockingSite = OutpostTileOccupancyUtility.GetBlockingFacilityOrSiteAt(clickedTile);

            if (!hasOutpost && blockingSite == null)
            {
                __result ??= new List<FloatMenuOption>();
                OutpostFloatMenuHelper.AddImmediateDeployOptions(
                    __result,
                    caravan,
                    clickedTile,
                    SafeWorldObjects,
                    includeCaravanNameInLabel: false,
                    includeBeaconOptions: true);
            }
            else if (post != null)
            {
                __result ??= new List<FloatMenuOption>();
                foreach (var opt in post.GetFloatMenuOptions(caravan))
                {
                    OutpostFloatMenuHelper.AddOptionIfNotPresent(__result, opt);
                }
            }
        }
        else
        {
            // 2. Unexplored Tile in Black Shroud: Fog-of-war immersion
            // Do NOT leak impassable terrain, nearest passable tile coordinates, or offer construction expeditions into uncharted territory.
            bool isUnexplored = GeoscapeRules.VisibilityMode == VisibilityMode.FogOfWar &&
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
                    }, MenuOptionPriority.High)
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
                    bool hasOutpost = OutpostTileOccupancyUtility.TryGetListeningPostAt(clickedTile, out WorldObject_ListeningPost? post);
                    WorldObject? blockingSite = OutpostTileOccupancyUtility.GetBlockingFacilityOrSiteAt(clickedTile);

                    if (!hasOutpost && blockingSite == null)
                    {
                        // Move here
                        __result.Add(new FloatMenuOption(
                            $"Move to Tile {clickedTile.tileId}",
                            () =>
                            {
                                caravan.pather.StartPath(clickedTile, null, repathImmediately: true, resetPauseStatus: true);
                            },
                            MenuOptionPriority.Default));

                        OutpostFloatMenuHelper.AddTravelDeployOutpostOptions(
                            __result,
                            caravan,
                            clickedTile,
                            useNearestTileLabel: false,
                            includeCaravanNameInLabel: false,
                            distSuffix: null,
                            selectCaravanOnDispatch: false,
                            includeBeacon: true);
                    }
                    else
                    {
                        if (post != null)
                        {
                            if (post.IsConstructing)
                            {
                                // Travel and resume construction
                                OutpostFloatMenuHelper.AddOptionIfNotPresent(__result, new FloatMenuOption(
                                    $"Travel and resume construction of {post.LabelCap} ({post.ConstructionProgressPct}% complete)",
                                    () =>
                                    {
                                        caravan.pather.StartPath(clickedTile, null, repathImmediately: true, resetPauseStatus: true);
                                        Messages.Message($"Caravan {caravan.LabelCap} en route to resume construction of {post.LabelCap} at Tile {clickedTile.tileId}.", MessageTypeDefOf.PositiveEvent, false);
                                    },
                                    MenuOptionPriority.Default));

                                // Travel and Dismantle / Cancel (Refund 100% Materials)
                                OutpostFloatMenuHelper.AddOptionIfNotPresent(__result, new FloatMenuOption(
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
                                OutpostFloatMenuHelper.AddOptionIfNotPresent(__result, new FloatMenuOption(
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
                                OutpostFloatMenuHelper.AddOptionIfNotPresent(__result, new FloatMenuOption(
                                    $"Move to Tile {clickedTile.tileId}",
                                    () =>
                                    {
                                        caravan.pather.StartPath(clickedTile, null, repathImmediately: true, resetPauseStatus: true);
                                    },
                                    MenuOptionPriority.Default));

                                // Travel and Dismantle
                                OutpostFloatMenuHelper.AddOptionIfNotPresent(__result, new FloatMenuOption(
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
                        else if (blockingSite != null)
                        {
                            // Tile is occupied by a camp, site, or ruins: allow movement without outpost deployment
                            __result.Add(new FloatMenuOption(
                                $"Move to Tile {clickedTile.tileId}",
                                () =>
                                {
                                    caravan.pather.StartPath(clickedTile, null, repathImmediately: true, resetPauseStatus: true);
                                },
                                MenuOptionPriority.Default));
                        }
                    }
                }
            }
            // 4. Distant Explored Impassable Tile: Provide clear menu options rather than silent auto-movement
            else if (IsTileImpassable(clickedTile))
            {
                __result ??= new List<FloatMenuOption>();

                __result.Add(new FloatMenuOption($"Tile {clickedTile.tileId} is impassable (Mountain / Ocean)", null, MenuOptionPriority.Low)
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

                    bool hasOutpostNearby = OutpostTileOccupancyUtility.TryGetListeningPostAt(bestNearby, out _);
                    WorldObject? blockingSiteNearby = OutpostTileOccupancyUtility.GetBlockingFacilityOrSiteAt(bestNearby);

                    if (!hasOutpostNearby && blockingSiteNearby == null)
                    {
                        OutpostFloatMenuHelper.AddTravelDeployOutpostOptions(
                            __result,
                            caravan,
                            bestNearby,
                            useNearestTileLabel: true,
                            includeCaravanNameInLabel: false,
                            distSuffix: null,
                            selectCaravanOnDispatch: false,
                            includeBeacon: true);
                    }
                }
            }
        }

        OutpostFloatMenuHelper.DeduplicateByLabel(__result);
    }
}
