using System;
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
/// Intercepts right-clicks on the Geoscape globe to provide intelligent 4X-style contextual dispatch options.
/// When a player colony settlement is selected, provides targeted dispatch options.
/// When no specific source is selected, intelligently suggests actions from all available player caravans
/// and settlements (e.g. "Move caravan X to...", "Send caravan from X to..."), sorted by distance.
/// </summary>
[HarmonyPatch(typeof(WorldSelector), "HandleWorldClicks")]
public static class WorldSelector_HandleWorldClicks_Patch
{
    [System.ThreadStatic]
    public static PlanetTile? MouseTileOverride;

    public static bool Prefix(WorldSelector __instance)
    {
        if (Event.current.type == EventType.MouseDown && Event.current.button == 1)
        {
            var world = Current.Game != null ? Find.World : null;
            if (world == null)
            {
                return true;
            }

            if (GeoscapeMod.Settings != null && !GeoscapeMod.Settings.enableIntelligentRightClick)
            {
                // Fallback to targeted settlement dispatch only if intelligent right-click is disabled
                if (__instance.SelectedObjects.Count == 1 &&
                    __instance.SelectedObjects[0] is Settlement selectedSettlement &&
                    selectedSettlement.Faction?.IsPlayer == true &&
                    selectedSettlement.Map != null)
                {
                    PlanetTile targetTile = MouseTileOverride ?? GenWorld.MouseTile(false);
                    if (targetTile.Valid && targetTile != selectedSettlement.Tile)
                    {
                        OpenSettlementDispatchMenu(selectedSettlement, targetTile);
                        Event.current.Use();
                        return false;
                    }
                }

                return true;
            }

            // 1. If a player caravan is selected, let vanilla RimWorld & FloatMenuMakerWorld_ChoicesAtFor handle it
            if (__instance.SelectedObjects.Count == 1 &&
                __instance.SelectedObjects[0] is Caravan caravan &&
                caravan.IsPlayerControlled)
            {
                return true;
            }

            PlanetTile clickedTile = MouseTileOverride ?? GenWorld.MouseTile(false);
            if (!clickedTile.Valid)
            {
                return true;
            }

            // 2. If a single player settlement is selected AND user right-clicked a different tile, open targeted settlement menu
            if (__instance.SelectedObjects.Count == 1 &&
                __instance.SelectedObjects[0] is Settlement settlement &&
                settlement.Faction?.IsPlayer == true &&
                settlement.Map != null &&
                clickedTile != settlement.Tile)
            {
                OpenSettlementDispatchMenu(settlement, clickedTile);
                Event.current.Use();
                return false;
            }

            // 3. Otherwise (no source selected, or clicked own colony, or non-controllable object selected):
            // Intelligently suggest actions from all player caravans and colonies
            var intelligentOptions = BuildIntelligentContextualOptions(clickedTile);
            if (intelligentOptions != null && intelligentOptions.Count > 0)
            {
                OpenIntelligentDispatchMenu(intelligentOptions, clickedTile);
                Event.current.Use();
                return false;
            }
        }

        return true;
    }

    private static Map? GetSafeMap(Settlement? settlement)
    {
        if (settlement == null) return null;
        try
        {
            if (settlement.HasMap)
            {
                return settlement.Map;
            }
        }
        catch
        {
        }
        return null;
    }

    private static int GetApproxDistance(WorldGrid? grid, PlanetTile tileA, PlanetTile tileB)
    {
        if (grid != null)
        {
            return Mathf.RoundToInt(grid.ApproxDistanceInTiles(tileA, tileB));
        }
        return Math.Abs(tileA.tileId - tileB.tileId);
    }

    private static void OpenSettlementDispatchMenu(Settlement settlement, PlanetTile clickedTile)
    {
        var options = BuildSettlementDispatchOptions(settlement, clickedTile);
        if (options != null && options.Count > 0 && Find.WindowStack != null)
        {
            Find.WindowStack.Add(new FloatMenu(options, settlement.LabelCap));
        }
    }

    private static void OpenIntelligentDispatchMenu(List<FloatMenuOption> options, PlanetTile clickedTile)
    {
        if (options != null && options.Count > 0 && Find.WindowStack != null)
        {
            Find.WindowStack.Add(new FloatMenu(options, $"Target: Tile {clickedTile.tileId}"));
        }
    }

    /// <summary>
    /// Intelligently builds dispatch and movement suggestions for a target tile across all available player
    /// colonies and caravans without requiring a specific source entity to be selected beforehand.
    /// </summary>
    public static List<FloatMenuOption> BuildIntelligentContextualOptions(
        PlanetTile clickedTile,
        IEnumerable<Caravan>? caravans = null,
        IEnumerable<Settlement>? settlements = null)
    {
        var options = new List<FloatMenuOption>();

        var world = Current.Game != null ? Find.World : null;
        var worldObjectsHolder = world != null ? world.worldObjects : null;
        var worldGrid = world != null ? world.grid : null;

        var playerCaravans = (caravans ?? (worldObjectsHolder?.Caravans?.Where(c => c != null && c.Faction?.IsPlayer == true && c.Spawned) ?? Enumerable.Empty<Caravan>())).ToList();
        var playerSettlements = (settlements ?? (worldObjectsHolder?.Settlements?.Where(s => s != null && s.Faction?.IsPlayer == true && s.Map != null) ?? Enumerable.Empty<Settlement>())).ToList();

        if (playerCaravans.Count == 0 && playerSettlements.Count == 0)
        {
            return options;
        }

        bool isUnexplored = GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar &&
                            SensoryManager.Instance != null &&
                            !SensoryManager.Instance.IsExplored(clickedTile);

        if (isUnexplored)
        {
            // Unexplored sector in the black unknown: do not reveal impassable terrain,
            // nearest passable tile coordinates, or offer construction expeditions into uncharted territory.
            var unexploredSources = new List<(int dist, FloatMenuOption option)>();

            foreach (var caravan in playerCaravans)
            {
                int dist = GetApproxDistance(worldGrid, caravan.Tile, clickedTile);
                string distStr = dist == 1 ? "1 tile away" : $"{dist} tiles away";
                string label = $"Move caravan {caravan.LabelCap} towards unexplored sector (Tile {clickedTile.tileId}) ({distStr})";

                unexploredSources.Add((dist, new FloatMenuOption(label, () =>
                {
                    if (caravan.pather != null)
                    {
                        caravan.pather.StartPath(clickedTile, null, repathImmediately: true, resetPauseStatus: true);
                        if (Find.WorldSelector != null)
                        {
                            Find.WorldSelector.ClearSelection();
                            Find.WorldSelector.Select(caravan, playSound: true);
                        }
                        if (!caravan.pather.Moving)
                        {
                            Messages.Message($"Caravan {caravan.LabelCap} cannot find a route towards unexplored sector {clickedTile.tileId}.", MessageTypeDefOf.RejectInput, false);
                        }
                        else
                        {
                            Messages.Message($"Caravan {caravan.LabelCap} en route towards unexplored sector {clickedTile.tileId}.", MessageTypeDefOf.TaskCompletion, false);
                        }
                    }
                }, MenuOptionPriority.Default)));
            }

            foreach (var settlement in playerSettlements)
            {
                int dist = GetApproxDistance(worldGrid, settlement.Tile, clickedTile);
                string distStr = dist == 1 ? "1 tile away" : $"{dist} tiles away";
                string label = $"Send caravan from {settlement.LabelCap} towards unexplored sector (Tile {clickedTile.tileId}) ({distStr})";

                unexploredSources.Add((dist, new FloatMenuOption(label, () =>
                {
                    Map? map = GetSafeMap(settlement);
                    if (map != null)
                    {
                        CaravanExpeditionUtility.OpenCaravanFormingDialog(map, clickedTile);
                    }
                }, MenuOptionPriority.Default)));
            }

            foreach (var item in unexploredSources.OrderBy(x => x.dist))
            {
                options.Add(item.option);
            }

            return options;
        }

        bool isImpassable = world != null && world.Impassable(clickedTile);
        PlanetTile effectiveTile = clickedTile;

        if (isImpassable)
        {
            options.Add(new FloatMenuOption($"Tile {clickedTile.tileId} is impassable (Mountain / Ocean)", null, MenuOptionPriority.Default)
            {
                Disabled = true
            });

            if (GenWorldClosest.TryFindClosestPassableTile(clickedTile, out PlanetTile closestPassable) && closestPassable.Valid)
            {
                effectiveTile = closestPassable;
            }
            else
            {
                return options;
            }
        }

        var allObjects = worldObjectsHolder?.AllWorldObjects;
        Settlement? playerColonyAtDest = playerSettlements.FirstOrDefault(s => s.Tile == clickedTile);
        WorldObject_ListeningPost? post = allObjects?.OfType<WorldObject_ListeningPost>().FirstOrDefault(p => p.Tile == effectiveTile);
        WorldObject? destObj = playerColonyAtDest ?? allObjects?.FirstOrDefault(w => w.Tile == clickedTile && !(w is WorldObject_ListeningPost));

        string destName = $"Tile {effectiveTile.tileId}";
        if (destObj != null)
        {
            bool isDestMasked = SensoryManager.ShouldMaskWorldObjectAsUnknown(destObj);
            destName = isDestMasked
                ? $"Unidentified Contact (Tile {effectiveTile.tileId})"
                : destObj.LabelCap.ToString();
        }

        string targetName = isImpassable
            ? $"Tile {clickedTile.tileId} (Nearest passable: Tile {effectiveTile.tileId})"
            : (post != null ? post.LabelCap.ToString() : destName);

        var candidateGroups = new List<(int dist, List<FloatMenuOption> groupOptions)>();

        // 1. Evaluate Caravans
        foreach (var caravan in playerCaravans)
        {
            int dist = GetApproxDistance(worldGrid, caravan.Tile, effectiveTile);
            string distStr = dist == 1 ? "1 tile away" : $"{dist} tiles away";
            var caravanOptions = new List<FloatMenuOption>();

            if (caravan.Tile == effectiveTile)
            {
                // Local actions on current tile
                if (post != null)
                {
                    if (post.IsSabotaged)
                    {
                        bool canRepair = post.HasRepairMaterials(caravan, out string missingDesc);
                        string repairLabel = canRepair
                            ? $"Repair {post.LabelCap} with caravan {caravan.LabelCap}"
                            : $"Repair {post.LabelCap} with caravan {caravan.LabelCap} ({missingDesc})";

                        caravanOptions.Add(new FloatMenuOption(repairLabel, () =>
                        {
                            post.TryRepair(caravan);
                        }, MenuOptionPriority.High) { Disabled = !canRepair });
                    }

                    caravanOptions.Add(new FloatMenuOption($"Dismantle {post.LabelCap} with caravan {caravan.LabelCap} (Recover 75% materials)", () =>
                    {
                        post.StartDismantling(1500);
                    }, MenuOptionPriority.Low));
                }
                else
                {
                    bool canAffordRelay = OutpostMaterialUtility.CanDeployAutomatedRelay(caravan, out string missingRelay);
                    string relayLabel = canAffordRelay
                        ? $"Deploy Automated Listening Post with caravan {caravan.LabelCap} ({OutpostMaterialUtility.AutomatedRelaySteelCost} Steel, {OutpostMaterialUtility.AutomatedRelayComponentCost} Components)"
                        : $"Deploy Automated Listening Post with caravan {caravan.LabelCap} ({missingRelay})";

                    caravanOptions.Add(new FloatMenuOption(relayLabel, () =>
                    {
                        if (OutpostMaterialUtility.CanDeployAutomatedRelay(caravan, out _))
                        {
                            OutpostMaterialUtility.ConsumeAutomatedRelayDeployment(caravan);
                            var def = DefDatabase<WorldObjectDef>.GetNamed("Vigilance_ListeningPost_Automated");
                            var newPost = (WorldObject_ListeningPost)WorldObjectMaker.MakeWorldObject(def);
                            newPost.Tile = effectiveTile;
                            newPost.SetFaction(Faction.OfPlayer);
                            newPost.Kind = ListeningPostKind.AutomatedRelay;
                            newPost.StartConstruction(2500);
                            Find.WorldObjects?.Add(newPost);
                            Messages.Message("Automated listening post deployment commenced. Construction underway.", MessageTypeDefOf.PositiveEvent, false);
                        }
                    }, MenuOptionPriority.Default) { Disabled = !canAffordRelay });
                }
            }
            else
            {
                // Movement / Recall to target
                if (playerColonyAtDest != null)
                {
                    string recallLabel = $"Recall caravan {caravan.LabelCap} to {playerColonyAtDest.LabelCap} ({distStr})";
                    caravanOptions.Add(new FloatMenuOption(recallLabel, () =>
                    {
                        if (caravan.pather != null)
                        {
                            caravan.pather.StartPath(
                                effectiveTile,
                                new CaravanArrivalAction_Enter(playerColonyAtDest),
                                repathImmediately: true,
                                resetPauseStatus: true);
                            if (Find.WorldSelector != null)
                            {
                                Find.WorldSelector.ClearSelection();
                                Find.WorldSelector.Select(caravan, playSound: true);
                            }
                            Messages.Message($"Caravan {caravan.LabelCap} recalled to {playerColonyAtDest.LabelCap}.", MessageTypeDefOf.TaskCompletion, false);
                        }
                    }, MenuOptionPriority.Default));
                }
                else
                {
                    string moveLabel = $"Move caravan {caravan.LabelCap} to {targetName} ({distStr})";
                    caravanOptions.Add(new FloatMenuOption(moveLabel, () =>
                    {
                        if (caravan.pather != null)
                        {
                            caravan.pather.StartPath(effectiveTile, null, repathImmediately: true, resetPauseStatus: true);
                            if (Find.WorldSelector != null)
                            {
                                Find.WorldSelector.ClearSelection();
                                Find.WorldSelector.Select(caravan, playSound: true);
                            }
                            Messages.Message($"Caravan {caravan.LabelCap} en route towards {targetName}.", MessageTypeDefOf.TaskCompletion, false);
                        }
                    }, MenuOptionPriority.Default));
                }

                if (post != null)
                {
                    string dismantleLabel = $"Send caravan {caravan.LabelCap} to dismantle {post.LabelCap} (Recover 75% Materials) ({distStr})";
                    caravanOptions.Add(new FloatMenuOption(dismantleLabel, () =>
                    {
                        if (caravan.pather != null)
                        {
                            caravan.pather.StartPath(
                                effectiveTile,
                                new CaravanArrivalAction_DismantleOutpost(post, effectiveTile),
                                repathImmediately: true,
                                resetPauseStatus: true);
                            if (Find.WorldSelector != null)
                            {
                                Find.WorldSelector.ClearSelection();
                                Find.WorldSelector.Select(caravan, playSound: true);
                            }
                            Messages.Message($"Caravan {caravan.LabelCap} en route to dismantle {post.LabelCap} at Tile {effectiveTile.tileId}.", MessageTypeDefOf.PositiveEvent, false);
                        }
                    }, MenuOptionPriority.Default));
                }
                else if (destObj == null)
                {
                    bool canAffordRelay = OutpostMaterialUtility.CanDeployAutomatedRelay(caravan, out string missingRelay);
                    string relayPrefix = isImpassable
                        ? $"Travel and deploy Automated Listening Post with caravan {caravan.LabelCap} (Nearest: Tile {effectiveTile.tileId})"
                        : $"Travel and deploy Automated Listening Post with caravan {caravan.LabelCap}";
                    string relayLabel = canAffordRelay
                        ? $"{relayPrefix} ({OutpostMaterialUtility.AutomatedRelaySteelCost} Steel, {OutpostMaterialUtility.AutomatedRelayComponentCost} Components) ({distStr})"
                        : $"{relayPrefix} ({missingRelay}) ({distStr})";

                    caravanOptions.Add(new FloatMenuOption(relayLabel, () =>
                    {
                        if (OutpostMaterialUtility.CanDeployAutomatedRelay(caravan, out _) && caravan.pather != null)
                        {
                            caravan.pather.StartPath(
                                effectiveTile,
                                new CaravanArrivalAction_ConstructOutpost(effectiveTile, ListeningPostKind.AutomatedRelay),
                                repathImmediately: true,
                                resetPauseStatus: true);
                            if (Find.WorldSelector != null)
                            {
                                Find.WorldSelector.ClearSelection();
                                Find.WorldSelector.Select(caravan, playSound: true);
                            }
                            Messages.Message($"Caravan {caravan.LabelCap} en route to deploy automated listening post at Tile {effectiveTile.tileId}.", MessageTypeDefOf.PositiveEvent, false);
                        }
                    }, MenuOptionPriority.Default) { Disabled = !canAffordRelay });
                }
            }

            if (caravanOptions.Count > 0)
            {
                candidateGroups.Add((dist, caravanOptions));
            }
        }

        // 2. Evaluate Settlements
        foreach (var settlement in playerSettlements)
        {
            int dist = GetApproxDistance(worldGrid, settlement.Tile, effectiveTile);
            string distStr = dist == 1 ? "1 tile away" : $"{dist} tiles away";
            var settlementOptions = new List<FloatMenuOption>();

            if (settlement.Tile == clickedTile)
            {
                settlementOptions.Add(new FloatMenuOption($"Form caravan at {settlement.LabelCap}", () =>
                {
                    Map? map = GetSafeMap(settlement);
                    if (map != null)
                    {
                        CaravanExpeditionUtility.OpenCaravanFormingDialog(map, PlanetTile.Invalid);
                    }
                }, MenuOptionPriority.Default));
            }
            else if (settlement.Tile != effectiveTile)
            {
                string formCaravanLabel = $"Send caravan from {settlement.LabelCap} to {targetName} ({distStr})";
                settlementOptions.Add(new FloatMenuOption(formCaravanLabel, () =>
                {
                    Map? map = GetSafeMap(settlement);
                    if (map != null)
                    {
                        CaravanExpeditionUtility.OpenCaravanFormingDialog(map, effectiveTile);
                    }
                }, MenuOptionPriority.Default));

                if (post != null)
                {
                    settlementOptions.Add(new FloatMenuOption($"Dispatch caravan from {settlement.LabelCap} to dismantle {post.LabelCap} (Recover 75% Materials) ({distStr})", () =>
                    {
                        CaravanExpeditionUtility.StartDismantleExpedition(settlement, post);
                    }, MenuOptionPriority.Default));
                }
                else if (destObj == null)
                {
                    Map? map = GetSafeMap(settlement);
                    if (map != null)
                    {
                        bool canAffordRelay = CaravanExpeditionUtility.CanColonyDeployAutomatedRelay(map, out int mapSteel, out int mapComp);
                        string relayPrefix = isImpassable
                            ? $"Dispatch Construction Expedition from {settlement.LabelCap}: Automated Listening Post (Nearest: Tile {effectiveTile.tileId})"
                            : $"Dispatch Construction Expedition from {settlement.LabelCap}: Automated Listening Post";

                        string relayLabel = canAffordRelay
                            ? $"{relayPrefix} ({OutpostMaterialUtility.AutomatedRelaySteelCost} Steel, {OutpostMaterialUtility.AutomatedRelayComponentCost} Components) ({distStr})"
                            : $"{relayPrefix} (Need {OutpostMaterialUtility.AutomatedRelaySteelCost} Steel, {OutpostMaterialUtility.AutomatedRelayComponentCost} Components - Have {mapSteel} Steel, {mapComp} Components) ({distStr})";

                        settlementOptions.Add(new FloatMenuOption(relayLabel, () =>
                        {
                            if (CaravanExpeditionUtility.CanColonyDeployAutomatedRelay(map, out _, out _))
                            {
                                CaravanExpeditionUtility.StartConstructionExpedition(settlement, effectiveTile, ListeningPostKind.AutomatedRelay, null);
                            }
                        }, MenuOptionPriority.Default) { Disabled = !canAffordRelay });

                        string beaconPrefix = isImpassable
                            ? $"Dispatch Construction Expedition from {settlement.LabelCap}: Signal Beacon (Nearest: Tile {effectiveTile.tileId})"
                            : $"Dispatch Construction Expedition from {settlement.LabelCap}: Signal Beacon";

                        List<ThingDef> beaconMaterials = CaravanExpeditionUtility.GetAvailableColonyBeaconMaterials(map);
                        if (beaconMaterials.Count > 0)
                        {
                            foreach (ThingDef mat in beaconMaterials)
                            {
                                int count = map.resourceCounter?.GetCount(mat) ?? 0;
                                settlementOptions.Add(new FloatMenuOption($"{beaconPrefix} ({OutpostMaterialUtility.SignalBeaconWoodOrStoneCost} {mat.LabelCap} - Have {count}) ({distStr})", () =>
                                {
                                    CaravanExpeditionUtility.StartConstructionExpedition(settlement, effectiveTile, ListeningPostKind.SignalBeacon, mat);
                                }, MenuOptionPriority.Default));
                            }
                        }
                        else
                        {
                            settlementOptions.Add(new FloatMenuOption($"{beaconPrefix} (Need {OutpostMaterialUtility.SignalBeaconWoodOrStoneCost} Wood or Stone Blocks) ({distStr})", null, MenuOptionPriority.Default)
                            {
                                Disabled = true
                            });
                        }
                    }
                }
            }

            if (settlementOptions.Count > 0)
            {
                candidateGroups.Add((dist, settlementOptions));
            }
        }

        // Sort all source candidate groups by proximity to target
        foreach (var group in candidateGroups.OrderBy(g => g.dist))
        {
            options.AddRange(group.groupOptions);
        }

        return options;
    }

    public static List<FloatMenuOption> BuildSettlementDispatchOptions(Settlement settlement, PlanetTile clickedTile)
    {
        var options = new List<FloatMenuOption>();

        bool isUnexplored = GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar &&
                            SensoryManager.Instance != null &&
                            !SensoryManager.Instance.IsExplored(clickedTile);

        var world = Current.Game != null ? Find.World : null;
        var worldObjectsHolder = world != null ? world.worldObjects : null;
        bool isImpassable = world != null && world.Impassable(clickedTile);
        PlanetTile effectiveTile = clickedTile;

        if (isUnexplored)
        {
            // Unexplored sector in the black unknown: do not reveal impassable terrain,
            // nearest passable tile coordinates, or offer construction expeditions into uncharted territory.
            options.Add(new FloatMenuOption($"Form expedition towards unexplored sector (Tile {clickedTile.tileId})", () =>
            {
                CaravanExpeditionUtility.OpenCaravanFormingDialog(settlement.Map, clickedTile);
            }, MenuOptionPriority.Default));

            return options;
        }

        if (isImpassable)
        {
            options.Add(new FloatMenuOption($"Tile {clickedTile.tileId} is impassable (Mountain / Ocean)", null, MenuOptionPriority.Default)
            {
                Disabled = true
            });

            if (GenWorldClosest.TryFindClosestPassableTile(clickedTile, out PlanetTile closestPassable) &&
                closestPassable.Valid &&
                closestPassable != settlement.Tile)
            {
                effectiveTile = closestPassable;
            }
            else
            {
                return options;
            }
        }

        string destName = $"Tile {effectiveTile.tileId}";
        WorldObject? destObj = worldObjectsHolder?.AllWorldObjects?.FirstOrDefault(w => w.Tile == clickedTile && !(w is WorldObject_ListeningPost));
        if (destObj != null)
        {
            bool isDestMasked = SensoryManager.ShouldMaskWorldObjectAsUnknown(destObj);
            destName = isDestMasked ? $"Unidentified Contact (Tile {effectiveTile.tileId})" : destObj.LabelCap.ToString();
        }

        string formCaravanLabel = isImpassable
            ? $"Form caravan towards Tile {clickedTile.tileId} (Nearest passable: Tile {effectiveTile.tileId})"
            : $"Form caravan to {destName}";

        // 1. Standard Form Caravan to designated hex
        options.Add(new FloatMenuOption(formCaravanLabel, () =>
        {
            CaravanExpeditionUtility.OpenCaravanFormingDialog(settlement.Map, effectiveTile);
        }, MenuOptionPriority.Default));

        bool hasOutpost = worldObjectsHolder?.AllWorldObjects != null &&
                          worldObjectsHolder.AllWorldObjects.OfType<WorldObject_ListeningPost>().Any(p => p.Tile == effectiveTile);

        if (!hasOutpost)
        {
            // 2. Dispatch Construction Expedition: Automated Listening Post (100 Steel, 4 Components)
            bool canAffordRelay = CaravanExpeditionUtility.CanColonyDeployAutomatedRelay(settlement.Map, out int mapSteel, out int mapComp);
            string relayPrefix = isImpassable
                ? $"Dispatch Construction Expedition: Automated Listening Post (Nearest: Tile {effectiveTile.tileId})"
                : "Dispatch Construction Expedition: Automated Listening Post";

            string relayLabel = canAffordRelay
                ? $"{relayPrefix} ({OutpostMaterialUtility.AutomatedRelaySteelCost} Steel, {OutpostMaterialUtility.AutomatedRelayComponentCost} Components)"
                : $"{relayPrefix} (Need {OutpostMaterialUtility.AutomatedRelaySteelCost} Steel, {OutpostMaterialUtility.AutomatedRelayComponentCost} Components - Have {mapSteel} Steel, {mapComp} Components)";

            options.Add(new FloatMenuOption(relayLabel, () =>
            {
                if (CaravanExpeditionUtility.CanColonyDeployAutomatedRelay(settlement.Map, out _, out _))
                {
                    CaravanExpeditionUtility.StartConstructionExpedition(settlement, effectiveTile, ListeningPostKind.AutomatedRelay, null);
                }
            }, MenuOptionPriority.Default)
            {
                Disabled = !canAffordRelay
            });

            // 3. Dispatch Construction Expedition: Signal Beacon (150 Wood/Stone)
            string beaconPrefix = isImpassable
                ? $"Dispatch Construction Expedition: Signal Beacon (Nearest: Tile {effectiveTile.tileId})"
                : "Dispatch Construction Expedition: Signal Beacon";

            List<ThingDef> beaconMaterials = CaravanExpeditionUtility.GetAvailableColonyBeaconMaterials(settlement.Map);
            if (beaconMaterials.Count > 0)
            {
                foreach (ThingDef mat in beaconMaterials)
                {
                    int count = settlement.Map.resourceCounter?.GetCount(mat) ?? 0;
                    options.Add(new FloatMenuOption($"{beaconPrefix} ({OutpostMaterialUtility.SignalBeaconWoodOrStoneCost} {mat.LabelCap} - Have {count})", () =>
                    {
                        CaravanExpeditionUtility.StartConstructionExpedition(settlement, effectiveTile, ListeningPostKind.SignalBeacon, mat);
                    }, MenuOptionPriority.Default));
                }
            }
            else
            {
                options.Add(new FloatMenuOption($"{beaconPrefix} (Need {OutpostMaterialUtility.SignalBeaconWoodOrStoneCost} Wood or Stone Blocks)", null, MenuOptionPriority.Default)
                {
                    Disabled = true
                });
            }
        }
        else
        {
            WorldObject_ListeningPost? post = worldObjectsHolder?.AllWorldObjects?.OfType<WorldObject_ListeningPost>().FirstOrDefault(p => p.Tile == effectiveTile);
            if (post != null)
            {
                options.Add(new FloatMenuOption($"Dispatch Caravan to Dismantle {post.LabelCap} (Recover 75% Materials)", () =>
                {
                    CaravanExpeditionUtility.StartDismantleExpedition(settlement, post);
                }, MenuOptionPriority.Default));
            }
            else
            {
                options.Add(new FloatMenuOption($"Tile {effectiveTile.tileId} already has an active outpost", null, MenuOptionPriority.Default)
                {
                    Disabled = true
                });
            }
        }

        return options;
    }
}
