using System;
using System.Collections.Generic;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape;

/// <summary>
/// Shared world float-menu option builders and deduplication helpers for planetary outpost deployment,
/// dismantling, and colony expeditions.
/// </summary>
internal static class OutpostFloatMenuHelper
{
    public static void AddOptionIfNotPresent(List<FloatMenuOption> list, FloatMenuOption option)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i].Label, option.Label, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }
        list.Add(option);
    }

    public static void DeduplicateByLabel(List<FloatMenuOption>? list)
    {
        if (list == null || list.Count <= 1) return;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

    public static void StartCaravanPath(
        Caravan caravan,
        PlanetTile destinationTile,
        CaravanArrivalAction? arrivalAction,
        string? statusMessage,
        bool selectCaravan = false)
    {
        if (caravan.pather == null)
        {
            return;
        }

        caravan.pather.StartPath(destinationTile, arrivalAction, repathImmediately: true, resetPauseStatus: true);
        if (selectCaravan && Find.WorldSelector != null)
        {
            Find.WorldSelector.ClearSelection();
            Find.WorldSelector.Select(caravan, playSound: true);
        }

        if (!string.IsNullOrEmpty(statusMessage))
        {
            Messages.Message(statusMessage, MessageTypeDefOf.PositiveEvent, false);
        }
    }

    public static void AddImmediateDeployOptions(
        List<FloatMenuOption> options,
        Caravan caravan,
        PlanetTile tile,
        WorldObjectsHolder? targetWorldObjects,
        bool includeCaravanNameInLabel,
        bool includeBeaconOptions)
    {
        string actorSuffix = includeCaravanNameInLabel ? $" with caravan {caravan.LabelCap}" : string.Empty;

        bool canAffordRelay = OutpostMaterialUtility.CanDeployAutomatedRelay(caravan, out string missingRelay);
        string relayLabel = canAffordRelay
            ? $"Deploy Automated Listening Post{actorSuffix} ({OutpostMaterialUtility.AutomatedRelaySteelCost} Steel, {OutpostMaterialUtility.AutomatedRelayComponentCost} Components)"
            : $"Deploy Automated Listening Post{actorSuffix} ({missingRelay})";

        options.Add(new FloatMenuOption(relayLabel, () =>
        {
            if (OutpostMaterialUtility.CanDeployAutomatedRelay(caravan, out _))
            {
                OutpostMaterialUtility.ConsumeAutomatedRelayDeployment(caravan);
                WorldObject_ListeningPost.SpawnNewOutpost(tile, ListeningPostKind.AutomatedRelay, targetWorldObjects: targetWorldObjects);
                Messages.Message("Automated listening post deployment commenced. Construction underway.", MessageTypeDefOf.PositiveEvent, false);
            }
        }, MenuOptionPriority.Default)
        {
            Disabled = !canAffordRelay
        });

        if (SonarGatingUtility.ShouldOfferSonarDeployment(tile))
        {
            bool canAffordSonar = OutpostMaterialUtility.CanDeployAutomatedSonar(caravan, out string missingSonar);
            string sonarLabel = canAffordSonar
                ? $"Deploy Automated Sonar Station{actorSuffix} ({OutpostMaterialUtility.AutomatedSonarSteelCost} Steel, {OutpostMaterialUtility.AutomatedSonarComponentCost} Components)"
                : $"Deploy Automated Sonar Station{actorSuffix} ({missingSonar})";

            options.Add(new FloatMenuOption(sonarLabel, () =>
            {
                if (OutpostMaterialUtility.CanDeployAutomatedSonar(caravan, out _))
                {
                    OutpostMaterialUtility.ConsumeAutomatedSonarDeployment(caravan);
                    WorldObject_ListeningPost.SpawnNewOutpost(tile, ListeningPostKind.AutomatedSonar, targetWorldObjects: targetWorldObjects);
                    Messages.Message("Automated sonar station deployment commenced. Construction underway.", MessageTypeDefOf.PositiveEvent, false);
                }
            }, MenuOptionPriority.Default)
            {
                Disabled = !canAffordSonar
            });
        }

        if (!includeBeaconOptions)
        {
            return;
        }

        bool canAffordBeacon = OutpostMaterialUtility.CanDeploySignalBeacon(caravan, out string missingBeacon);
        if (!canAffordBeacon)
        {
            options.Add(new FloatMenuOption($"Construct Signal Beacon ({missingBeacon})", null, MenuOptionPriority.Low)
            {
                Disabled = true
            });
            return;
        }

        List<ThingDef> materials = OutpostMaterialUtility.GetAvailableBeaconMaterials(caravan);
        var completeMaterials = new List<ThingDef>();
        for (int i = 0; i < materials.Count; i++)
        {
            ThingDef mat = materials[i];
            if (OutpostMaterialUtility.CountItem(caravan, mat) >= OutpostMaterialUtility.SignalBeaconWoodOrStoneCost)
            {
                completeMaterials.Add(mat);
            }
        }

        if (completeMaterials.Count > 0)
        {
            for (int m = 0; m < completeMaterials.Count; m++)
            {
                ThingDef mat = completeMaterials[m];
                string beaconLabel = $"Construct Signal Beacon ({OutpostMaterialUtility.SignalBeaconWoodOrStoneCost} {mat.LabelCap})";
                options.Add(new FloatMenuOption(beaconLabel, () =>
                {
                    if (OutpostMaterialUtility.CountItem(caravan, mat) >= OutpostMaterialUtility.SignalBeaconWoodOrStoneCost)
                    {
                        ThingDef? consumed = OutpostMaterialUtility.ConsumeSignalBeaconDeployment(caravan, mat);
                        ThingDef finalMat = consumed ?? mat;
                        WorldObject_ListeningPost.SpawnNewOutpost(tile, ListeningPostKind.SignalBeacon, finalMat, targetWorldObjects: targetWorldObjects);
                        Messages.Message($"Signal beacon ({finalMat.LabelCap}) construction commenced.", MessageTypeDefOf.PositiveEvent, false);
                    }
                }, MenuOptionPriority.Default));
            }
        }
        else
        {
            string beaconLabel = $"Construct Signal Beacon ({OutpostMaterialUtility.SignalBeaconWoodOrStoneCost} Wood/Stone)";
            options.Add(new FloatMenuOption(beaconLabel, () =>
            {
                if (OutpostMaterialUtility.CanDeploySignalBeacon(caravan, out _))
                {
                    ThingDef? consumed = OutpostMaterialUtility.ConsumeSignalBeaconDeployment(caravan);
                    ThingDef finalMat = consumed ?? ThingDefOf.WoodLog;
                    WorldObject_ListeningPost.SpawnNewOutpost(tile, ListeningPostKind.SignalBeacon, finalMat, targetWorldObjects: targetWorldObjects);
                    Messages.Message($"Signal beacon ({finalMat.LabelCap}) construction commenced.", MessageTypeDefOf.PositiveEvent, false);
                }
            }, MenuOptionPriority.Default));
        }
    }

    public static void AddTravelDeployOutpostOptions(
        List<FloatMenuOption> options,
        Caravan caravan,
        PlanetTile targetTile,
        bool useNearestTileLabel,
        bool includeCaravanNameInLabel,
        string? distSuffix,
        bool selectCaravanOnDispatch,
        bool includeBeacon)
    {
        string actor = includeCaravanNameInLabel ? $" with caravan {caravan.LabelCap}" : string.Empty;
        string nearestSuffix = useNearestTileLabel ? $" (Nearest: Tile {targetTile.tileId})" : string.Empty;
        string tailDist = string.IsNullOrEmpty(distSuffix) ? string.Empty : $" ({distSuffix})";

        bool canAffordRelay = OutpostMaterialUtility.CanDeployAutomatedRelay(caravan, out string missingRelay);
        string relayLabel;
        if (useNearestTileLabel && !includeCaravanNameInLabel)
        {
            relayLabel = canAffordRelay
                ? $"Travel and deploy Automated Listening Post{nearestSuffix}"
                : $"Travel and deploy Automated Listening Post{nearestSuffix} ({missingRelay})";
        }
        else
        {
            string costPart = canAffordRelay
                ? $"{OutpostMaterialUtility.AutomatedRelaySteelCost} Steel, {OutpostMaterialUtility.AutomatedRelayComponentCost} Components"
                : missingRelay;
            relayLabel = $"Travel and deploy Automated Listening Post{actor}{nearestSuffix} ({costPart}){tailDist}";
        }

        options.Add(new FloatMenuOption(relayLabel, () =>
        {
            if (OutpostMaterialUtility.CanDeployAutomatedRelay(caravan, out _))
            {
                StartCaravanPath(
                    caravan,
                    targetTile,
                    new CaravanArrivalAction_ConstructOutpost(targetTile, ListeningPostKind.AutomatedRelay),
                    $"Caravan {caravan.LabelCap} en route to deploy automated listening post at Tile {targetTile.tileId}.",
                    selectCaravanOnDispatch);
            }
        }, MenuOptionPriority.Default)
        {
            Disabled = !canAffordRelay
        });

        if (SonarGatingUtility.ShouldOfferSonarDeployment(targetTile))
        {
            bool canAffordSonar = OutpostMaterialUtility.CanDeployAutomatedSonar(caravan, out string missingSonar);
            string sonarLabel;
            if (useNearestTileLabel && !includeCaravanNameInLabel)
            {
                sonarLabel = canAffordSonar
                    ? $"Travel and deploy Automated Sonar Station{nearestSuffix}"
                    : $"Travel and deploy Automated Sonar Station{nearestSuffix} ({missingSonar})";
            }
            else
            {
                string costPart = canAffordSonar
                    ? $"{OutpostMaterialUtility.AutomatedSonarSteelCost} Steel, {OutpostMaterialUtility.AutomatedSonarComponentCost} Components"
                    : missingSonar;
                sonarLabel = $"Travel and deploy Automated Sonar Station{actor}{nearestSuffix} ({costPart}){tailDist}";
            }

            options.Add(new FloatMenuOption(sonarLabel, () =>
            {
                if (OutpostMaterialUtility.CanDeployAutomatedSonar(caravan, out _))
                {
                    StartCaravanPath(
                        caravan,
                        targetTile,
                        new CaravanArrivalAction_ConstructOutpost(targetTile, ListeningPostKind.AutomatedSonar),
                        $"Caravan {caravan.LabelCap} en route to deploy automated sonar station at Tile {targetTile.tileId}.",
                        selectCaravanOnDispatch);
                }
            }, MenuOptionPriority.Default)
            {
                Disabled = !canAffordSonar
            });
        }

        if (!includeBeacon)
        {
            return;
        }

        bool canAffordBeacon = OutpostMaterialUtility.CanDeploySignalBeacon(caravan, out string missingBeacon);
        if (useNearestTileLabel)
        {
            string beaconLabel = canAffordBeacon
                ? $"Travel and construct Signal Beacon{nearestSuffix}"
                : $"Travel and construct Signal Beacon{nearestSuffix} ({missingBeacon})";

            options.Add(new FloatMenuOption(beaconLabel, () =>
            {
                if (OutpostMaterialUtility.CanDeploySignalBeacon(caravan, out _))
                {
                    StartCaravanPath(
                        caravan,
                        targetTile,
                        new CaravanArrivalAction_ConstructOutpost(targetTile, ListeningPostKind.SignalBeacon),
                        $"Caravan {caravan.LabelCap} en route to construct signal beacon at Tile {targetTile.tileId}.",
                        selectCaravanOnDispatch);
                }
            }, MenuOptionPriority.Default)
            {
                Disabled = !canAffordBeacon
            });
            return;
        }

        if (!canAffordBeacon)
        {
            options.Add(new FloatMenuOption($"Travel and construct Signal Beacon ({missingBeacon})", null, MenuOptionPriority.Default)
            {
                Disabled = true
            });
            return;
        }

        List<ThingDef> materials = OutpostMaterialUtility.GetAvailableBeaconMaterials(caravan);
        var completeMaterials = new List<ThingDef>();
        for (int i = 0; i < materials.Count; i++)
        {
            ThingDef mat = materials[i];
            if (OutpostMaterialUtility.CountItem(caravan, mat) >= OutpostMaterialUtility.SignalBeaconWoodOrStoneCost)
            {
                completeMaterials.Add(mat);
            }
        }

        if (completeMaterials.Count > 0)
        {
            for (int m = 0; m < completeMaterials.Count; m++)
            {
                ThingDef mat = completeMaterials[m];
                string beaconLabel = $"Travel and construct Signal Beacon ({OutpostMaterialUtility.SignalBeaconWoodOrStoneCost} {mat.LabelCap})";
                options.Add(new FloatMenuOption(beaconLabel, () =>
                {
                    if (OutpostMaterialUtility.CountItem(caravan, mat) >= OutpostMaterialUtility.SignalBeaconWoodOrStoneCost)
                    {
                        StartCaravanPath(
                            caravan,
                            targetTile,
                            new CaravanArrivalAction_ConstructOutpost(targetTile, ListeningPostKind.SignalBeacon, mat),
                            $"Caravan {caravan.LabelCap} en route to construct signal beacon at Tile {targetTile.tileId}.",
                            selectCaravanOnDispatch);
                    }
                }, MenuOptionPriority.Default));
            }
        }
        else
        {
            string beaconLabel = $"Travel and construct Signal Beacon ({OutpostMaterialUtility.SignalBeaconWoodOrStoneCost} Wood/Stone)";
            options.Add(new FloatMenuOption(beaconLabel, () =>
            {
                if (OutpostMaterialUtility.CanDeploySignalBeacon(caravan, out _))
                {
                    StartCaravanPath(
                        caravan,
                        targetTile,
                        new CaravanArrivalAction_ConstructOutpost(targetTile, ListeningPostKind.SignalBeacon),
                        $"Caravan {caravan.LabelCap} en route to construct signal beacon at Tile {targetTile.tileId}.",
                        selectCaravanOnDispatch);
                }
            }, MenuOptionPriority.Default));
        }
    }

    public static void AddColonyOutpostDispatchOptions(
        List<FloatMenuOption> options,
        Settlement settlement,
        Map map,
        PlanetTile effectiveTile,
        bool isImpassable,
        bool includeSettlementNameInLabel,
        string? distSuffix,
        Func<List<FloatMenuOption>> rebuildParentOptions,
        string parentMenuTitle)
    {
        string prefixVerb = includeSettlementNameInLabel
            ? $"Dispatch Construction Expedition from {settlement.LabelCap}"
            : "Dispatch Construction Expedition";
        string nearestPart = isImpassable ? $" (Nearest: Tile {effectiveTile.tileId})" : string.Empty;
        string tailDist = string.IsNullOrEmpty(distSuffix) ? string.Empty : $" ({distSuffix})";

        bool canAffordRelay = CaravanExpeditionUtility.CanColonyDeployAutomatedRelay(map, out int mapSteel, out int mapComp);
        string relayPrefix = $"{prefixVerb}: Automated Listening Post{nearestPart}";
        string relayLabel = canAffordRelay
            ? $"{relayPrefix} ({OutpostMaterialUtility.AutomatedRelaySteelCost} Steel, {OutpostMaterialUtility.AutomatedRelayComponentCost} Components){tailDist}"
            : $"{relayPrefix} (Need {OutpostMaterialUtility.AutomatedRelaySteelCost} Steel, {OutpostMaterialUtility.AutomatedRelayComponentCost} Components - Have {mapSteel} Steel, {mapComp} Components){tailDist}";

        options.Add(new FloatMenuOption(relayLabel, () =>
        {
            if (CaravanExpeditionUtility.CanColonyDeployAutomatedRelay(map, out _, out _))
            {
                CaravanExpeditionUtility.StartConstructionExpedition(settlement, effectiveTile, ListeningPostKind.AutomatedRelay, null);
            }
        }, MenuOptionPriority.Default)
        {
            Disabled = !canAffordRelay
        });

        if (SonarGatingUtility.ShouldOfferSonarDeployment(effectiveTile))
        {
            bool canAffordSonar = CaravanExpeditionUtility.CanColonyDeployAutomatedSonar(map, out int mapSonarSteel, out int mapSonarComp);
            string sonarPrefix = $"{prefixVerb}: Automated Sonar Station{nearestPart}";
            string sonarLabel = canAffordSonar
                ? $"{sonarPrefix} ({OutpostMaterialUtility.AutomatedSonarSteelCost} Steel, {OutpostMaterialUtility.AutomatedSonarComponentCost} Components){tailDist}"
                : $"{sonarPrefix} (Need {OutpostMaterialUtility.AutomatedSonarSteelCost} Steel, {OutpostMaterialUtility.AutomatedSonarComponentCost} Components - Have {mapSonarSteel} Steel, {mapSonarComp} Components){tailDist}";

            options.Add(new FloatMenuOption(sonarLabel, () =>
            {
                if (CaravanExpeditionUtility.CanColonyDeployAutomatedSonar(map, out _, out _))
                {
                    CaravanExpeditionUtility.StartConstructionExpedition(settlement, effectiveTile, ListeningPostKind.AutomatedSonar, null);
                }
            }, MenuOptionPriority.Default)
            {
                Disabled = !canAffordSonar
            });
        }

        string beaconPrefix = $"{prefixVerb}: Signal Beacon{nearestPart}";
        List<ThingDef> beaconMaterials = CaravanExpeditionUtility.GetAvailableColonyBeaconMaterials(map);
        if (beaconMaterials.Count > 1)
        {
            string beaconSubmenuLabel = $"{beaconPrefix}... > ({beaconMaterials.Count} material types available){tailDist}";
            options.Add(new FloatMenuOption(beaconSubmenuLabel, () =>
            {
                var materialOptions = new List<FloatMenuOption>
                {
                    new FloatMenuOption("< Back", () =>
                    {
                        var parentOpts = rebuildParentOptions();
                        if (parentOpts.Count > 0 && Find.WindowStack != null)
                        {
                            Find.WindowStack.Add(new FloatMenu(parentOpts, parentMenuTitle));
                        }
                    }, MenuOptionPriority.High)
                };

                for (int m = 0; m < beaconMaterials.Count; m++)
                {
                    ThingDef mat = beaconMaterials[m];
                    int count = map.resourceCounter?.GetCount(mat) ?? 0;
                    materialOptions.Add(new FloatMenuOption(
                        $"Build with {mat.LabelCap} (Need {OutpostMaterialUtility.SignalBeaconWoodOrStoneCost} - Have {count})",
                        () => CaravanExpeditionUtility.StartConstructionExpedition(settlement, effectiveTile, ListeningPostKind.SignalBeacon, mat),
                        MenuOptionPriority.Default));
                }

                if (Find.WindowStack != null)
                {
                    Find.WindowStack.Add(new FloatMenu(materialOptions, $"{settlement.LabelCap}: Select Beacon Material"));
                }
            }, MenuOptionPriority.Default));
        }
        else if (beaconMaterials.Count == 1)
        {
            ThingDef mat = beaconMaterials[0];
            int count = map.resourceCounter?.GetCount(mat) ?? 0;
            options.Add(new FloatMenuOption(
                $"{beaconPrefix} ({OutpostMaterialUtility.SignalBeaconWoodOrStoneCost} {mat.LabelCap} - Have {count}){tailDist}",
                () => CaravanExpeditionUtility.StartConstructionExpedition(settlement, effectiveTile, ListeningPostKind.SignalBeacon, mat),
                MenuOptionPriority.Default));
        }
        else
        {
            options.Add(new FloatMenuOption(
                $"{beaconPrefix} (Need {OutpostMaterialUtility.SignalBeaconWoodOrStoneCost} Wood or Stone Blocks){tailDist}",
                null,
                MenuOptionPriority.Default)
            {
                Disabled = true
            });
        }
    }
}
