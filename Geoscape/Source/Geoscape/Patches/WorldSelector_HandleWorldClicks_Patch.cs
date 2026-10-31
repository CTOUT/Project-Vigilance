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
/// Intercepts right-clicks on the Geoscape when a player colony settlement is selected,
/// providing 4X-style contextual dispatch options (Form Caravan, Dispatch Automated Relay Expedition,
/// Dispatch Signal Beacon Expedition).
/// Uses standard FloatMenu so it is not auto-removed by FloatMenuWorld's caravan check.
/// </summary>
[HarmonyPatch(typeof(WorldSelector), "HandleWorldClicks")]
public static class WorldSelector_HandleWorldClicks_Patch
{
    public static bool Prefix(WorldSelector __instance)
    {
        if (Event.current.type == EventType.MouseDown && Event.current.button == 1)
        {
            if (__instance.SelectedObjects.Count == 1 &&
                __instance.SelectedObjects[0] is Settlement settlement &&
                settlement.Faction?.IsPlayer == true &&
                settlement.Map != null)
            {
                PlanetTile clickedTile = GenWorld.MouseTile(false);
                if (clickedTile.Valid && clickedTile != settlement.Tile && Find.World != null)
                {
                    OpenSettlementDispatchMenu(settlement, clickedTile);
                    Event.current.Use();
                    return false;
                }
            }
        }

        return true;
    }

    private static void OpenSettlementDispatchMenu(Settlement settlement, PlanetTile clickedTile)
    {
        var options = BuildSettlementDispatchOptions(settlement, clickedTile);
        if (options != null && options.Count > 0 && Find.WindowStack != null)
        {
            Find.WindowStack.Add(new FloatMenu(options, settlement.LabelCap));
        }
    }

    public static List<FloatMenuOption> BuildSettlementDispatchOptions(Settlement settlement, PlanetTile clickedTile)
    {
        var options = new List<FloatMenuOption>();

        bool isUnexplored = GeoscapeMod.Settings.visibilityMode == VisibilityMode.ClassicHazeOfWar &&
                            SensoryManager.Instance != null &&
                            !SensoryManager.Instance.IsExplored(clickedTile);

        bool isImpassable = Find.World != null && Find.World.Impassable(clickedTile);
        PlanetTile effectiveTile = clickedTile;

        if (isUnexplored)
        {
            // Unexplored sector in the black unknown: do not reveal impassable terrain,
            // nearest passable tile coordinates, or offer construction expeditions into uncharted territory.
            if (isImpassable)
            {
                if (GenWorldClosest.TryFindClosestPassableTile(clickedTile, out PlanetTile closestPassable) &&
                    closestPassable.Valid &&
                    closestPassable != settlement.Tile)
                {
                    effectiveTile = closestPassable;
                }
            }

            options.Add(new FloatMenuOption($"Form expedition towards unexplored sector (Tile {clickedTile.tileId})", () =>
            {
                CaravanExpeditionUtility.OpenCaravanFormingDialog(settlement.Map, effectiveTile);
            }, MenuOptionPriority.Default));

            return options;
        }

        if (isImpassable)
        {
            options.Add(new FloatMenuOption($"Tile {clickedTile} is impassable (Mountain / Ocean)", null, MenuOptionPriority.Default)
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

        string formCaravanLabel = isImpassable
            ? $"Form caravan towards Tile {clickedTile} (Nearest passable: Tile {effectiveTile})"
            : $"Form caravan to Tile {effectiveTile}";

        // 1. Standard Form Caravan to designated hex
        options.Add(new FloatMenuOption(formCaravanLabel, () =>
        {
            CaravanExpeditionUtility.OpenCaravanFormingDialog(settlement.Map, effectiveTile);
        }, MenuOptionPriority.Default));

        bool hasOutpost = Find.WorldObjects?.AllWorldObjects != null &&
                          Find.WorldObjects.AllWorldObjects.OfType<WorldObject_ListeningPost>().Any(p => p.Tile == effectiveTile);

        if (!hasOutpost)
        {
            // 2. Dispatch Construction Expedition: Automated Listening Post (100 Steel, 4 Components)
            bool canAffordRelay = CaravanExpeditionUtility.CanColonyDeployAutomatedRelay(settlement.Map, out int mapSteel, out int mapComp);
            string relayPrefix = isImpassable
                ? $"Dispatch Construction Expedition: Automated Listening Post (Nearest: Tile {effectiveTile})"
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
                ? $"Dispatch Construction Expedition: Signal Beacon (Nearest: Tile {effectiveTile})"
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
            WorldObject_ListeningPost? post = Find.WorldObjects?.AllWorldObjects?.OfType<WorldObject_ListeningPost>().FirstOrDefault(p => p.Tile == effectiveTile);
            if (post != null)
            {
                options.Add(new FloatMenuOption($"Dispatch Caravan to Dismantle {post.LabelCap} (Recover 75% Materials)", () =>
                {
                    CaravanExpeditionUtility.StartDismantleExpedition(settlement, post);
                }, MenuOptionPriority.Default));
            }
            else
            {
                options.Add(new FloatMenuOption($"Tile {effectiveTile} already has an active outpost", null, MenuOptionPriority.Default)
                {
                    Disabled = true
                });
            }
        }

        return options;
    }
}
