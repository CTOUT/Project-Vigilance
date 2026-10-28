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
                if (clickedTile.Valid && clickedTile != settlement.Tile && Find.World != null && !Find.World.Impassable(clickedTile))
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
        var options = new List<FloatMenuOption>();

        // 1. Standard Form Caravan to designated hex
        options.Add(new FloatMenuOption($"Form caravan to Tile {clickedTile}", () =>
        {
            CaravanExpeditionUtility.OpenCaravanFormingDialog(settlement.Map, clickedTile);
        }, MenuOptionPriority.Default));

        bool hasOutpost = Find.WorldObjects?.AllWorldObjects != null &&
                          Find.WorldObjects.AllWorldObjects.OfType<WorldObject_ListeningPost>().Any(p => p.Tile == clickedTile);

        if (!hasOutpost)
        {
            // 2. Dispatch Construction Expedition: Automated Listening Post (100 Steel, 4 Components)
            bool canAffordRelay = CaravanExpeditionUtility.CanColonyDeployAutomatedRelay(settlement.Map, out int mapSteel, out int mapComp);
            string relayLabel = canAffordRelay
                ? $"Dispatch Construction Expedition: Automated Listening Post ({OutpostMaterialUtility.AutomatedRelaySteelCost} Steel, {OutpostMaterialUtility.AutomatedRelayComponentCost} Components)"
                : $"Dispatch Construction Expedition: Automated Listening Post (Need {OutpostMaterialUtility.AutomatedRelaySteelCost} Steel, {OutpostMaterialUtility.AutomatedRelayComponentCost} Components - Have {mapSteel} Steel, {mapComp} Components)";

            options.Add(new FloatMenuOption(relayLabel, () =>
            {
                if (CaravanExpeditionUtility.CanColonyDeployAutomatedRelay(settlement.Map, out _, out _))
                {
                    CaravanExpeditionUtility.StartConstructionExpedition(settlement, clickedTile, ListeningPostKind.AutomatedRelay, null);
                }
            }, MenuOptionPriority.Default)
            {
                Disabled = !canAffordRelay
            });

            // 3. Dispatch Construction Expedition: Signal Beacon (150 Wood/Stone)
            List<ThingDef> beaconMaterials = CaravanExpeditionUtility.GetAvailableColonyBeaconMaterials(settlement.Map);
            if (beaconMaterials.Count > 0)
            {
                foreach (ThingDef mat in beaconMaterials)
                {
                    int count = settlement.Map.resourceCounter?.GetCount(mat) ?? 0;
                    options.Add(new FloatMenuOption($"Dispatch Construction Expedition: Signal Beacon ({OutpostMaterialUtility.SignalBeaconWoodOrStoneCost} {mat.LabelCap} - Have {count})", () =>
                    {
                        CaravanExpeditionUtility.StartConstructionExpedition(settlement, clickedTile, ListeningPostKind.SignalBeacon, mat);
                    }, MenuOptionPriority.Default));
                }
            }
            else
            {
                options.Add(new FloatMenuOption($"Dispatch Construction Expedition: Signal Beacon (Need {OutpostMaterialUtility.SignalBeaconWoodOrStoneCost} Wood or Stone Blocks)", null, MenuOptionPriority.Default)
                {
                    Disabled = true
                });
            }
        }
        else
        {
            options.Add(new FloatMenuOption("Tile already has an active outpost", null, MenuOptionPriority.Default)
            {
                Disabled = true
            });
        }

        Find.WindowStack.Add(new FloatMenuWorld(options, settlement.LabelCap, UI.MousePositionOnUI));
    }
}
