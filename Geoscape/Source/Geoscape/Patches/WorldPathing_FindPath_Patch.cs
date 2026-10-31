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
                                  (caravan == null && (OptimisticPathingUtility.IsRoutePlannerActive || OptimisticPathingUtility.ForceActive));

            if (isPlayerContext && GeoscapeMod.Settings.visibilityMode == VisibilityMode.ClassicHazeOfWar)
            {
                OptimisticPathingUtility.Active = true;
            }
        }

        public static void Postfix(bool __state)
        {
            OptimisticPathingUtility.Active = __state;
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
            if (OptimisticPathingUtility.ShouldUseOptimisticPathing() ||
                (OptimisticPathingUtility.IsRoutePlannerActive && GeoscapeMod.Settings.visibilityMode == VisibilityMode.ClassicHazeOfWar))
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
            if (OptimisticPathingUtility.ShouldUseOptimisticPathing() ||
                (OptimisticPathingUtility.IsRoutePlannerActive && GeoscapeMod.Settings.visibilityMode == VisibilityMode.ClassicHazeOfWar))
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
            if (OptimisticPathingUtility.ShouldUseOptimisticPathing() ||
                (OptimisticPathingUtility.IsRoutePlannerActive && GeoscapeMod.Settings.visibilityMode == VisibilityMode.ClassicHazeOfWar))
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
            if (OptimisticPathingUtility.ShouldUseOptimisticPathing() ||
                (OptimisticPathingUtility.IsRoutePlannerActive && GeoscapeMod.Settings.visibilityMode == VisibilityMode.ClassicHazeOfWar))
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
            if (OptimisticPathingUtility.ShouldUseOptimisticPathing() ||
                (OptimisticPathingUtility.IsRoutePlannerActive && GeoscapeMod.Settings.visibilityMode == VisibilityMode.ClassicHazeOfWar))
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
                GeoscapeMod.Settings.visibilityMode == VisibilityMode.ClassicHazeOfWar)
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
                GeoscapeMod.Settings.visibilityMode == VisibilityMode.ClassicHazeOfWar)
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
                GeoscapeMod.Settings.visibilityMode == VisibilityMode.ClassicHazeOfWar)
            {
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

    [HarmonyPatch(typeof(WorldRoutePlanner), nameof(WorldRoutePlanner.TryAddWaypoint))]
    public static class WorldRoutePlanner_TryAddWaypoint_Patch
    {
        public static void Prefix(out bool __state)
        {
            __state = OptimisticPathingUtility.Active;

            if (GeoscapeMod.Settings.visibilityMode == VisibilityMode.ClassicHazeOfWar)
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

            if (GeoscapeMod.Settings.visibilityMode == VisibilityMode.ClassicHazeOfWar)
            {
                OptimisticPathingUtility.Active = true;
            }
        }

        public static void Postfix(bool __state)
        {
            OptimisticPathingUtility.Active = __state;
        }
    }
}
