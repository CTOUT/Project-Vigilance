using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Implements 4X Fog-of-War optimistic pathfinding hooks across RimWorld's planetary pathfinder,
/// reachability cache, movement grids, and caravan path follower.
/// </summary>
public static class WorldPathing_FindPath_Patch
{
    [HarmonyPatch(typeof(WorldPathing), nameof(WorldPathing.FindPath),
        new[] { typeof(PlanetTile), typeof(PlanetTile), typeof(Caravan), typeof(Func<float, bool>) })]
    public static class FindPath_Patch
    {
        public static void Prefix(PlanetTile startTile, PlanetTile destTile, Caravan caravan, out bool __state)
        {
            __state = OptimisticPathingUtility.Active;

            bool isPlayerContext = (caravan != null && caravan.Faction?.IsPlayer == true) ||
                                  (caravan == null && (OptimisticPathingUtility.IsPlayerPlanningRoute || OptimisticPathingUtility.ForceActive));

            if (isPlayerContext && GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar)
            {
                OptimisticPathingUtility.Active = true;
            }
        }

        public static Exception? Finalizer(Exception? __exception, bool __state)
        {
            OptimisticPathingUtility.Active = __state;
            return __exception;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            var modifyMethod = AccessTools.Method(typeof(OptimisticPathingUtility), nameof(OptimisticPathingUtility.ModifyMovementDifficulty));

            for (int i = 0; i < list.Count; i++)
            {
                yield return list[i];

                // Match:
                // [i - 2] ldloc.s <neighborTile>
                // [i - 1] ldfld Int32 tileId
                // [i]     ldelem.r4
                if (i >= 2 &&
                    list[i].opcode == OpCodes.Ldelem_R4 &&
                    list[i - 1].opcode == OpCodes.Ldfld &&
                    list[i - 1].operand is System.Reflection.FieldInfo fi &&
                    fi.Name == "tileId")
                {
                    CodeInstruction loadNeighborTile = list[i - 2].Clone();
                    yield return loadNeighborTile;
                    yield return new CodeInstruction(OpCodes.Call, modifyMethod);
                }
            }
        }
    }

    [HarmonyPatch(typeof(WorldReachability), nameof(WorldReachability.CanReach),
        new[] { typeof(PlanetTile), typeof(PlanetTile) })]
    public static class WorldReachability_CanReach_Patch
    {
        public static bool Prefix(PlanetTile startTile, PlanetTile destTile, ref bool __result)
        {
            if (!startTile.Valid || !destTile.Valid)
            {
                __result = false;
                return false;
            }

            if (OptimisticPathingUtility.ShouldUseOptimisticPathing() ||
                (OptimisticPathingUtility.IsPlayerPlanningRoute && GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar))
            {
                __result = true;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(WorldPathGrid), nameof(WorldPathGrid.Passable),
        new[] { typeof(PlanetTile) })]
    public static class WorldPathGrid_Passable_Patch
    {
        public static bool Prefix(PlanetTile tile, ref bool __result)
        {
            if (!tile.Valid)
            {
                __result = false;
                return false;
            }

            if (OptimisticPathingUtility.ShouldUseOptimisticPathing() ||
                (OptimisticPathingUtility.IsPlayerPlanningRoute && GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar))
            {
                if (OptimisticPathingUtility.IsTileUnexplored(tile))
                {
                    __result = true;
                    return false;
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(WorldPathGrid), nameof(WorldPathGrid.PassableFast),
        new[] { typeof(PlanetTile) })]
    public static class WorldPathGrid_PassableFast_Patch
    {
        public static bool Prefix(PlanetTile tile, ref bool __result)
        {
            if (!tile.Valid)
            {
                __result = false;
                return false;
            }

            if (OptimisticPathingUtility.ShouldUseOptimisticPathing() ||
                (OptimisticPathingUtility.IsPlayerPlanningRoute && GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar))
            {
                if (OptimisticPathingUtility.IsTileUnexplored(tile))
                {
                    __result = true;
                    return false;
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.World), nameof(RimWorld.Planet.World.Impassable),
        new[] { typeof(PlanetTile) })]
    public static class World_Impassable_Patch
    {
        public static bool Prefix(PlanetTile tileID, ref bool __result)
        {
            if (!tileID.Valid)
            {
                __result = true;
                return false;
            }

            if (OptimisticPathingUtility.ShouldUseOptimisticPathing() ||
                (OptimisticPathingUtility.IsPlayerPlanningRoute && GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar))
            {
                if (OptimisticPathingUtility.IsTileUnexplored(tileID))
                {
                    __result = false;
                    return false;
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(WorldPathGrid), nameof(WorldPathGrid.PerceivedMovementDifficultyAt),
        new[] { typeof(PlanetTile) })]
    public static class WorldPathGrid_PerceivedMovementDifficultyAt_Patch
    {
        public static bool Prefix(PlanetTile tile, ref float __result)
        {
            if (!tile.Valid)
            {
                __result = 1000f;
                return false;
            }

            if (OptimisticPathingUtility.ShouldUseOptimisticPathing() ||
                (OptimisticPathingUtility.IsPlayerPlanningRoute && GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar))
            {
                if (OptimisticPathingUtility.IsTileUnexplored(tile))
                {
                    __result = 1.0f;
                    return false;
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Caravan_PathFollower), "NeedNewPath")]
    public static class Caravan_PathFollower_NeedNewPath_Patch
    {
        public static void Prefix(Caravan_PathFollower __instance, out bool __state)
        {
            __state = OptimisticPathingUtility.Active;

            if (OptimisticPathingUtility.IsPlayerPather(__instance) &&
                GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar)
            {
                OptimisticPathingUtility.Active = true;
            }
        }

        public static void Postfix(bool __state)
        {
            OptimisticPathingUtility.Active = __state;
        }
    }

    [HarmonyPatch(typeof(Caravan_PathFollower), "IsPassable")]
    public static class Caravan_PathFollower_IsPassable_Patch
    {
        public static bool Prefix(Caravan_PathFollower __instance, PlanetTile tile, ref bool __result)
        {
            if (OptimisticPathingUtility.IsPlayerPather(__instance) &&
                GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar)
            {
                if (OptimisticPathingUtility.IsTileUnexplored(tile))
                {
                    __result = true;
                    return false;
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Caravan_PathFollower), "TryEnterNextPathTile")]
    public static class Caravan_PathFollower_TryEnterNextPathTile_Patch
    {
        public static bool Prefix(Caravan_PathFollower __instance)
        {
            if (OptimisticPathingUtility.IsPlayerPather(__instance) &&
                GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar)
            {
                PlanetTile nextTile = __instance.nextTile;
                if (OptimisticPathingUtility.IsTileUnexplored(nextTile) &&
                    OptimisticPathingUtility.IsTileActuallyImpassable(nextTile))
                {
                    SensoryManager.Instance?.MarkSurroundingTilesExplored(nextTile, 0f);

                    Caravan? caravan = OptimisticPathingUtility.GetCaravan(__instance);
                    if (caravan != null)
                    {
                        OptimisticPathingUtility.NotifyCaravanVisionUpdated(caravan);
                    }

                    return false;
                }

                if (!__instance.IsNextTilePassable())
                {
                    Caravan? caravan = OptimisticPathingUtility.GetCaravan(__instance);
                    if (caravan != null)
                    {
                        OptimisticPathingUtility.NotifyCaravanVisionUpdated(caravan);
                    }
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Vanilla consumes the next path node inside SetupMoveIntoNextTile, i.e. after the caravan has already
    /// entered its current tile. Checking only the tile being entered misses a hidden obstacle one step further
    /// along, so this guard inspects the node that is about to become the next tile.
    /// </summary>
    [HarmonyPatch(typeof(Caravan_PathFollower), "SetupMoveIntoNextTile")]
    public static class Caravan_PathFollower_SetupMoveIntoNextTile_Patch
    {
        public static bool Prefix(Caravan_PathFollower __instance)
        {
            if (!OptimisticPathingUtility.IsPlayerPather(__instance) ||
                GeoscapeRules.VisibilityMode != VisibilityMode.ClassicHazeOfWar)
            {
                return true;
            }

            WorldPath? path = __instance.curPath;
            if (path == null || !path.Found || path.NodesLeftCount < 2)
            {
                return true;
            }

            PlanetTile upcoming = path.Peek(1);
            if (!upcoming.Valid || !OptimisticPathingUtility.IsTileActuallyImpassable(upcoming))
            {
                return true;
            }

            SensoryManager.Instance?.MarkSurroundingTilesExplored(upcoming, 0f);

            Caravan? caravan = OptimisticPathingUtility.GetCaravan(__instance);
            if (caravan != null)
            {
                OptimisticPathingUtility.NotifyCaravanVisionUpdated(caravan);

                // If recovery did not replace the stale path, halt rather than step into the obstacle.
                if (ReferenceEquals(path, __instance.curPath) && OptimisticPathingUtility.IsMoving(__instance))
                {
                    __instance.StopDead();
                }
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(WorldRoutePlanner), nameof(WorldRoutePlanner.TryAddWaypoint))]
    public static class WorldRoutePlanner_TryAddWaypoint_Patch
    {
        public static void Prefix(out bool __state)
        {
            __state = OptimisticPathingUtility.Active;

            if (GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar)
            {
                OptimisticPathingUtility.Active = true;
            }
        }

        public static void Postfix(bool __state)
        {
            OptimisticPathingUtility.Active = __state;
        }
    }

    [HarmonyPatch(typeof(WorldRoutePlanner), "RecreatePaths")]
    public static class WorldRoutePlanner_RecreatePaths_Patch
    {
        public static void Prefix(out bool __state)
        {
            __state = OptimisticPathingUtility.Active;

            if (GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar)
            {
                OptimisticPathingUtility.Active = true;
            }
        }

        public static void Postfix(bool __state)
        {
            OptimisticPathingUtility.Active = __state;
        }
    }

    /// <summary>
    /// Guards against null transferables list in Dialog_FormCaravan if Notify_ChoseRoute
    /// is called prior to Window.PostOpen() executing, and establishes optimistic pathing context.
    /// </summary>
    [HarmonyPatch(typeof(Dialog_FormCaravan), "SelectApproximateBestTravelSupplies")]
    public static class Dialog_FormCaravan_SelectApproximateBestTravelSupplies_Patch
    {
        private static readonly System.Reflection.FieldInfo transferablesField =
            AccessTools.Field(typeof(Dialog_FormCaravan), "transferables");

        public static bool Prefix(Dialog_FormCaravan __instance, out bool __state)
        {
            __state = OptimisticPathingUtility.Active;
            var transferables = (List<TransferableOneWay>?)transferablesField?.GetValue(__instance);
            if (transferables == null)
            {
                return false;
            }

            if (GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar)
            {
                OptimisticPathingUtility.Active = true;
            }

            return true;
        }

        public static Exception? Finalizer(Exception? __exception, bool __state)
        {
            OptimisticPathingUtility.Active = __state;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Caravan_PathFollower), nameof(Caravan_PathFollower.StartPath))]
    public static class Caravan_PathFollower_StartPath_Optimistic_Patch
    {
        public static void Prefix(Caravan_PathFollower __instance, out bool __state)
        {
            __state = OptimisticPathingUtility.Active;

            if (OptimisticPathingUtility.IsPlayerPather(__instance) &&
                GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar)
            {
                OptimisticPathingUtility.Active = true;
            }
        }

        public static Exception? Finalizer(Exception? __exception, bool __state)
        {
            OptimisticPathingUtility.Active = __state;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Dialog_FormCaravan), "CheckForErrors", new[] { typeof(List<Pawn>) })]
    public static class Dialog_FormCaravan_CheckForErrors_Optimistic_Patch
    {
        public static void Prefix(out bool __state)
        {
            __state = OptimisticPathingUtility.Active;

            if (GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar)
            {
                OptimisticPathingUtility.Active = true;
            }
        }

        public static Exception? Finalizer(Exception? __exception, bool __state)
        {
            OptimisticPathingUtility.Active = __state;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Dialog_FormCaravan), "TrySend")]
    public static class Dialog_FormCaravan_TrySend_Optimistic_Patch
    {
        public static void Prefix(out bool __state)
        {
            __state = OptimisticPathingUtility.Active;

            if (GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar)
            {
                OptimisticPathingUtility.Active = true;
            }
        }

        public static Exception? Finalizer(Exception? __exception, bool __state)
        {
            OptimisticPathingUtility.Active = __state;
            return __exception;
        }
    }

    /// <summary>
    /// Hides road discounts on unexplored tiles during optimistic searches, so routes cannot
    /// follow roads the player has not discovered.
    /// </summary>
    [HarmonyPatch(typeof(WorldGrid), nameof(WorldGrid.GetRoadMovementDifficultyMultiplier),
        new[] { typeof(PlanetTile), typeof(PlanetTile), typeof(System.Text.StringBuilder) })]
    public static class WorldGrid_GetRoadMovementDifficultyMultiplier_Patch
    {
        public static void Postfix(PlanetTile fromTile, PlanetTile toTile, ref float __result)
        {
            if (__result >= 1f)
            {
                return;
            }

            if (!(OptimisticPathingUtility.ShouldUseOptimisticPathing() ||
                  (OptimisticPathingUtility.IsPlayerPlanningRoute &&
                   GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar)))
            {
                return;
            }

            if ((fromTile.Valid && OptimisticPathingUtility.IsTileUnexplored(fromTile)) ||
                (toTile.Valid && OptimisticPathingUtility.IsTileUnexplored(toTile)))
            {
                __result = 1f;
            }
        }
    }
}
