using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Defensive resilience patches ensuring RimWorld 1.7's planetary caravan route and provision
/// estimators safely handle unreachable destinations, unexplored fog-of-war sectors, and empty
/// or NotFound world paths without throwing unhandled exceptions.
/// </summary>
public static class CaravanPathingResiliencePatches
{
    /// <summary>
    /// Activates optimistic pathing context while Dialog_FormCaravan renders window contents
    /// to ensure route calculations (travel ticks, days of food, and supply allocations)
    /// navigate unexplored sectors optimistically under Fog of War.
    /// Uses Finalizer exclusively to manage scope lifecycle safely across normal and exceptional exits.
    /// </summary>
    [HarmonyPatch(typeof(Dialog_FormCaravan), nameof(Dialog_FormCaravan.DoWindowContents))]
    public static class Dialog_FormCaravan_DoWindowContents_Patch
    {
        public static void Prefix(out bool __state)
        {
            OptimisticPathingUtility.ActiveFormCaravanCount++;
            __state = OptimisticPathingUtility.Active;

            if (GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar)
            {
                OptimisticPathingUtility.Active = true;
            }
        }

        public static Exception? Finalizer(Exception? __exception, bool __state)
        {
            OptimisticPathingUtility.Active = __state;
            if (OptimisticPathingUtility.ActiveFormCaravanCount > 0)
            {
                OptimisticPathingUtility.ActiveFormCaravanCount--;
            }
            return __exception;
        }
    }

    /// <summary>
    /// Activates optimistic pathing context while Dialog_FormCaravan selects a target route.
    /// Uses Finalizer exclusively to manage scope lifecycle safely across normal and exceptional exits.
    /// </summary>
    [HarmonyPatch(typeof(Dialog_FormCaravan), nameof(Dialog_FormCaravan.Notify_ChoseRoute))]
    public static class Dialog_FormCaravan_Notify_ChoseRoute_Patch
    {
        public static void Prefix(out bool __state)
        {
            OptimisticPathingUtility.ActiveFormCaravanCount++;
            __state = OptimisticPathingUtility.Active;

            if (GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar)
            {
                OptimisticPathingUtility.Active = true;
            }
        }

        public static Exception? Finalizer(Exception? __exception, bool __state)
        {
            OptimisticPathingUtility.Active = __state;
            if (OptimisticPathingUtility.ActiveFormCaravanCount > 0)
            {
                OptimisticPathingUtility.ActiveFormCaravanCount--;
            }
            return __exception;
        }
    }

    /// <summary>
    /// Guards WorldPath.Peek against unhandled ArgumentOutOfRangeException when called on
    /// NotFound paths or when index exceeds available nodes.
    /// </summary>
    [HarmonyPatch(typeof(WorldPath), nameof(WorldPath.Peek))]
    public static class WorldPath_Peek_Patch
    {
        public static bool Prefix(WorldPath __instance, int nodesAhead, ref PlanetTile __result)
        {
            if (!__instance.Found || nodesAhead < 0 || __instance.NodesLeftCount <= nodesAhead)
            {
                __result = PlanetTile.Invalid;
                return false;
            }

            var nodes = __instance.NodesReversed;
            int targetIndex = __instance.NodesLeftCount - 1 - nodesAhead;
            if (nodes == null || targetIndex < 0 || targetIndex >= nodes.Count)
            {
                __result = PlanetTile.Invalid;
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Guards CaravanArrivalTimeEstimator.EstimatedTicksToArrive when path is NotFound or empty,
    /// returning 0 rather than throwing an unhandled ArgumentOutOfRangeException in Peek(0).
    /// </summary>
    [HarmonyPatch(typeof(CaravanArrivalTimeEstimator), nameof(CaravanArrivalTimeEstimator.EstimatedTicksToArrive),
        new[] { typeof(PlanetTile), typeof(PlanetTile), typeof(WorldPath), typeof(float), typeof(int), typeof(int) })]
    public static class CaravanArrivalTimeEstimator_EstimatedTicksToArrive_Patch
    {
        public static bool Prefix(PlanetTile from, PlanetTile to, WorldPath path, ref int __result)
        {
            if (path == null || !path.Found || path.NodesLeftCount == 0)
            {
                __result = 0;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Guards CaravanArrivalTimeEstimator.EstimatedTicksToArriveToEvery against NotFound or empty paths,
    /// populating only the origin endpoint with zero ticks rather than propagating unreachable or invalid
    /// destination tiles into the arrival schedule.
    /// </summary>
    [HarmonyPatch(typeof(CaravanArrivalTimeEstimator), nameof(CaravanArrivalTimeEstimator.EstimatedTicksToArriveToEvery))]
    public static class CaravanArrivalTimeEstimator_EstimatedTicksToArriveToEvery_Patch
    {
        public static bool Prefix(PlanetTile from, PlanetTile to, WorldPath path, List<ValueTuple<PlanetTile, int>> outTicksToArrive)
        {
            if (path == null || !path.Found || path.NodesLeftCount == 0)
            {
                outTicksToArrive.Clear();
                if (from.Valid)
                {
                    outTicksToArrive.Add(new ValueTuple<PlanetTile, int>(from, 0));
                }
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Guards DaysWorthOfFoodCalculator.ApproxDaysWorthOfFood (Transferables overload) against invalid or NotFound paths.
    /// If the path is not found, resets path parameter to null so food estimation cleanly evaluates
    /// stationary sustenance at origin tile rather than querying TileIllBeInAt on an empty list
    /// which returns PlanetTile.Invalid.
    /// </summary>
    [HarmonyPatch(typeof(DaysWorthOfFoodCalculator), nameof(DaysWorthOfFoodCalculator.ApproxDaysWorthOfFood),
        new[] { typeof(List<TransferableOneWay>), typeof(PlanetTile), typeof(IgnorePawnsInventoryMode), typeof(Faction), typeof(WorldPath), typeof(float), typeof(int) })]
    public static class DaysWorthOfFoodCalculator_ApproxDaysWorthOfFood_Transferables_Patch
    {
        public static void Prefix(ref WorldPath? path)
        {
            if (path != null && (!path.Found || path.NodesLeftCount == 0))
            {
                path = null;
            }
        }
    }

    /// <summary>
    /// Guards DaysWorthOfFoodCalculator.ApproxDaysWorthOfFood (Pawns overload) against invalid or NotFound paths.
    /// </summary>
    [HarmonyPatch(typeof(DaysWorthOfFoodCalculator), "ApproxDaysWorthOfFood",
        new[] { typeof(List<Pawn>), typeof(List<ThingDefCount>), typeof(PlanetTile), typeof(IgnorePawnsInventoryMode), typeof(Faction), typeof(WorldPath), typeof(float), typeof(int), typeof(bool) })]
    public static class DaysWorthOfFoodCalculator_ApproxDaysWorthOfFood_Pawns_Patch
    {
        public static void Prefix(ref WorldPath? path)
        {
            if (path != null && (!path.Found || path.NodesLeftCount == 0))
            {
                path = null;
            }
        }
    }

    /// <summary>
    /// Guards DaysWorthOfFoodCalculator.ApproxDaysWorthOfFoodLeftAfterTransfer against invalid or NotFound paths.
    /// </summary>
    [HarmonyPatch(typeof(DaysWorthOfFoodCalculator), nameof(DaysWorthOfFoodCalculator.ApproxDaysWorthOfFoodLeftAfterTransfer),
        new[] { typeof(List<TransferableOneWay>), typeof(PlanetTile), typeof(IgnorePawnsInventoryMode), typeof(Faction), typeof(WorldPath), typeof(float), typeof(int) })]
    public static class DaysWorthOfFoodCalculator_ApproxDaysWorthOfFoodLeftAfterTransfer_Patch
    {
        public static void Prefix(ref WorldPath? path)
        {
            if (path != null && (!path.Found || path.NodesLeftCount == 0))
            {
                path = null;
            }
        }
    }

    /// <summary>
    /// Guards DaysUntilRotCalculator.ApproxDaysUntilRot (Transferables overload) against invalid or NotFound paths,
    /// ensuring food rot estimation falls back to origin tile when no traversable path exists.
    /// </summary>
    [HarmonyPatch(typeof(DaysUntilRotCalculator), nameof(DaysUntilRotCalculator.ApproxDaysUntilRot),
        new[] { typeof(List<TransferableOneWay>), typeof(PlanetTile), typeof(IgnorePawnsInventoryMode), typeof(WorldPath), typeof(float), typeof(int) })]
    public static class DaysUntilRotCalculator_ApproxDaysUntilRot_Transferables_Patch
    {
        public static void Prefix(ref WorldPath? path)
        {
            if (path != null && (!path.Found || path.NodesLeftCount == 0))
            {
                path = null;
            }
        }
    }

    /// <summary>
    /// Guards DaysUntilRotCalculator.ApproxDaysUntilRot (ThingCount overload) against invalid or NotFound paths.
    /// </summary>
    [HarmonyPatch(typeof(DaysUntilRotCalculator), nameof(DaysUntilRotCalculator.ApproxDaysUntilRot),
        new[] { typeof(List<ThingCount>), typeof(PlanetTile), typeof(WorldPath), typeof(float), typeof(int) })]
    public static class DaysUntilRotCalculator_ApproxDaysUntilRot_ThingCounts_Patch
    {
        public static void Prefix(ref WorldPath? path)
        {
            if (path != null && (!path.Found || path.NodesLeftCount == 0))
            {
                path = null;
            }
        }
    }

    /// <summary>
    /// Guards DaysUntilRotCalculator.ApproxDaysUntilRot (Things overload) against invalid or NotFound paths.
    /// </summary>
    [HarmonyPatch(typeof(DaysUntilRotCalculator), nameof(DaysUntilRotCalculator.ApproxDaysUntilRot),
        new[] { typeof(List<Thing>), typeof(PlanetTile), typeof(WorldPath), typeof(float), typeof(int) })]
    public static class DaysUntilRotCalculator_ApproxDaysUntilRot_Things_Patch
    {
        public static void Prefix(ref WorldPath? path)
        {
            if (path != null && (!path.Found || path.NodesLeftCount == 0))
            {
                path = null;
            }
        }
    }

    /// <summary>
    /// Guards DaysUntilRotCalculator.ApproxDaysUntilRotLeftAfterTransfer against invalid or NotFound paths.
    /// </summary>
    [HarmonyPatch(typeof(DaysUntilRotCalculator), nameof(DaysUntilRotCalculator.ApproxDaysUntilRotLeftAfterTransfer),
        new[] { typeof(List<TransferableOneWay>), typeof(PlanetTile), typeof(IgnorePawnsInventoryMode), typeof(WorldPath), typeof(float), typeof(int) })]
    public static class DaysUntilRotCalculator_ApproxDaysUntilRotLeftAfterTransfer_Patch
    {
        public static void Prefix(ref WorldPath? path)
        {
            if (path != null && (!path.Found || path.NodesLeftCount == 0))
            {
                path = null;
            }
        }
    }

    /// <summary>
    /// Guards VirtualPlantsUtility.EnvironmentAllowsEatingVirtualPlantsAt against PlanetTile.Invalid,
    /// preventing ArgumentOutOfRangeException when indexing Find.WorldGrid with an invalid tile ID (-1).
    /// </summary>
    [HarmonyPatch(typeof(VirtualPlantsUtility), nameof(VirtualPlantsUtility.EnvironmentAllowsEatingVirtualPlantsAt))]
    public static class VirtualPlantsUtility_EnvironmentAllowsEatingVirtualPlantsAt_Patch
    {
        public static bool Prefix(PlanetTile tile, ref bool __result)
        {
            if (!tile.Valid)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Guards CaravanNightRestUtility.WouldBeRestingAt against PlanetTile.Invalid,
    /// preventing ArgumentOutOfRangeException when calculating longitude of an invalid tile ID.
    /// </summary>
    [HarmonyPatch(typeof(CaravanNightRestUtility), nameof(CaravanNightRestUtility.WouldBeRestingAt))]
    public static class CaravanNightRestUtility_WouldBeRestingAt_Patch
    {
        public static bool Prefix(PlanetTile tile, ref bool __result)
        {
            if (!tile.Valid)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Guards GenTemperature.GetTemperatureFromSeasonAtTile against PlanetTile.Invalid,
    /// preventing ArgumentOutOfRangeException on Find.WorldGrid when an invalid tile is queried.
    /// </summary>
    [HarmonyPatch(typeof(GenTemperature), nameof(GenTemperature.GetTemperatureFromSeasonAtTile))]
    public static class GenTemperature_GetTemperatureFromSeasonAtTile_Patch
    {
        public static bool Prefix(PlanetTile tile, ref float __result)
        {
            if (!tile.Valid)
            {
                __result = 21f;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Guards WorldPathGrid.CalculatedMovementDifficultyAt against PlanetTile.Invalid,
    /// preventing ArgumentOutOfRangeException on Find.WorldGrid when an invalid tile is queried.
    /// </summary>
    [HarmonyPatch(typeof(WorldPathGrid), nameof(WorldPathGrid.CalculatedMovementDifficultyAt))]
    public static class WorldPathGrid_CalculatedMovementDifficultyAt_Patch
    {
        public static bool Prefix(PlanetTile tile, ref float __result)
        {
            if (!tile.Valid)
            {
                __result = 1000f;
                return false;
            }
            return true;
        }
    }
}
