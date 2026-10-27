using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Helper utilities for caravan inventory queries and resource consumption when establishing,
/// repairing, or dismantling planetary listening posts.
/// </summary>
public static class OutpostMaterialUtility
{
    public const int AutomatedRelaySteelCost = 100;
    public const int AutomatedRelayComponentCost = 4;
    public const int SignalBeaconWoodOrStoneCost = 150;

    public const int AutomatedRelaySteelRepairCost = 25;
    public const int AutomatedRelayComponentRepairCost = 1;
    public const int SignalBeaconWoodOrStoneRepairCost = 30;

    public const int AutomatedRelaySteelSalvage = 75;
    public const int AutomatedRelayComponentSalvage = 3;
    public const int SignalBeaconWoodSalvage = 112;

    public static int CountItem(Caravan caravan, ThingDef def)
    {
        if (caravan == null || def == null)
        {
            return 0;
        }

        int total = 0;
        List<Thing> items = CaravanInventoryUtility.AllInventoryItems(caravan);
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].def == def)
            {
                total += items[i].stackCount;
            }
        }

        return total;
    }

    public static void TakeItem(Caravan caravan, ThingDef def, int count)
    {
        if (caravan == null || def == null || count <= 0)
        {
            return;
        }

        int remaining = count;
        CaravanInventoryUtility.TakeThings(caravan, t =>
        {
            if (t.def == def && remaining > 0)
            {
                int toTake = Mathf.Min(remaining, t.stackCount);
                remaining -= toTake;
                return toTake;
            }
            return 0;
        });
    }

    public static int CountWoodOrStone(Caravan caravan)
    {
        if (caravan == null)
        {
            return 0;
        }

        int total = 0;
        List<Thing> items = CaravanInventoryUtility.AllInventoryItems(caravan);
        for (int i = 0; i < items.Count; i++)
        {
            Thing t = items[i];
            if (IsWoodOrStone(t.def))
            {
                total += t.stackCount;
            }
        }

        return total;
    }

    public static void TakeWoodOrStone(Caravan caravan, int count)
    {
        if (caravan == null || count <= 0)
        {
            return;
        }

        int remaining = count;
        CaravanInventoryUtility.TakeThings(caravan, t =>
        {
            if (IsWoodOrStone(t.def) && remaining > 0)
            {
                int toTake = Mathf.Min(remaining, t.stackCount);
                remaining -= toTake;
                return toTake;
            }
            return 0;
        });
    }

    public static bool IsWoodOrStone(ThingDef def)
    {
        if (def == null)
        {
            return false;
        }

        if (def == ThingDefOf.WoodLog)
        {
            return true;
        }

        if (def.stuffProps?.categories != null && def.stuffProps.categories.Contains(StuffCategoryDefOf.Stony))
        {
            return true;
        }

        return false;
    }

    public static bool CanDeployAutomatedRelay(Caravan caravan, out string missingReason)
    {
        int steel = CountItem(caravan, ThingDefOf.Steel);
        int comp = CountItem(caravan, ThingDefOf.ComponentIndustrial);

        if (steel < AutomatedRelaySteelCost || comp < AutomatedRelayComponentCost)
        {
            missingReason = $"Requires {AutomatedRelaySteelCost} Steel (have {steel}) and {AutomatedRelayComponentCost} Components (have {comp}).";
            return false;
        }

        missingReason = string.Empty;
        return true;
    }

    public static bool CanDeploySignalBeacon(Caravan caravan, out string missingReason)
    {
        int materials = CountWoodOrStone(caravan);
        if (materials < SignalBeaconWoodOrStoneCost)
        {
            missingReason = $"Requires {SignalBeaconWoodOrStoneCost} Wood or Stone blocks (have {materials}).";
            return false;
        }

        missingReason = string.Empty;
        return true;
    }

    public static void ConsumeAutomatedRelayDeployment(Caravan caravan)
    {
        TakeItem(caravan, ThingDefOf.Steel, AutomatedRelaySteelCost);
        TakeItem(caravan, ThingDefOf.ComponentIndustrial, AutomatedRelayComponentCost);
    }

    public static void ConsumeSignalBeaconDeployment(Caravan caravan)
    {
        TakeWoodOrStone(caravan, SignalBeaconWoodOrStoneCost);
    }
}

