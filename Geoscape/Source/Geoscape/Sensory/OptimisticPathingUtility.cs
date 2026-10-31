using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Implements authentic 4X Fog-of-War "Optimistic Pathfinding" across the Geoscape globe.
/// Unexplored sectors shrouded in the black unknown are assumed traversible with baseline movement costs,
/// preventing the player or caravan pather from omnisciently sensing hidden impassable obstacles (mountains/oceans).
/// When a moving caravan uncovers a sector that is secretly impassable, the caravan dynamically repaths around the obstacle
/// while continuing optimistically through any remaining unexplored sectors.
/// </summary>
public static class OptimisticPathingUtility
{
    [ThreadStatic]
    public static bool Active;

    [ThreadStatic]
    public static bool ForceActive;

    public static Func<PlanetTile, bool>? IsTileUnexploredOverride;
    public static Func<PlanetTile, bool>? IsTileImpassableOverride;
    public static Action<Caravan, PlanetTile, string>? OnCaravanReroutedForTesting;
    public static Action<Caravan, PlanetTile, string>? OnCaravanHaltedForTesting;

    private static readonly System.Reflection.FieldInfo caravanField = HarmonyLib.AccessTools.Field(typeof(Caravan_PathFollower), "caravan");
    private static readonly System.Reflection.FieldInfo movingField = HarmonyLib.AccessTools.Field(typeof(Caravan_PathFollower), "moving");

    public static bool IsMoving(Caravan_PathFollower? pather)
    {
        if (pather == null) return false;
        if (Find.World?.worldObjects != null)
        {
            return pather.Moving;
        }
        return (bool)(movingField?.GetValue(pather) ?? false);
    }

    public static Caravan? GetCaravan(Caravan_PathFollower? pather)
    {
        if (pather == null) return null;
        return (Caravan?)caravanField?.GetValue(pather);
    }

    public static bool IsPlayerPather(Caravan_PathFollower? pather)
    {
        return GetCaravan(pather)?.Faction?.IsPlayer == true;
    }

    public static bool IsRoutePlannerActive => Find.WorldRoutePlanner != null && Find.WorldRoutePlanner.Active;

    public struct Scope : IDisposable
    {
        private readonly bool _previous;

        public Scope(bool activate = true)
        {
            _previous = Active;
            Active = activate;
        }

        public void Dispose()
        {
            Active = _previous;
        }
    }

    public static bool ShouldUseOptimisticPathing()
    {
        return (Active || ForceActive) &&
               GeoscapeMod.Settings.visibilityMode == VisibilityMode.ClassicHazeOfWar;
    }

    public static bool IsTileUnexplored(PlanetTile tile)
    {
        if (IsTileUnexploredOverride != null)
        {
            return IsTileUnexploredOverride(tile);
        }

        if (!tile.Valid || SensoryManager.Instance == null)
        {
            return false;
        }

        return !SensoryManager.Instance.IsExplored(tile);
    }

    public static bool IsTileImpassable(PlanetTile tile)
    {
        if (IsTileImpassableOverride != null)
        {
            return IsTileImpassableOverride(tile);
        }

        if (!tile.Valid || Find.World == null)
        {
            return false;
        }

        return Find.World.Impassable(tile);
    }

    /// <summary>
    /// Replaces real terrain movement difficulty multiplier with baseline traversal cost (1.0f)
    /// for tiles shrouded in fog-of-war during an active optimistic pathfinding search.
    /// </summary>
    public static float ModifyMovementDifficulty(float difficulty, PlanetTile tile)
    {
        if (ShouldUseOptimisticPathing() && IsTileUnexplored(tile))
        {
            return 1.0f;
        }

        return difficulty;
    }

    public static string GetImpassableReason(PlanetTile tile)
    {
        if (Find.World?.grid == null || !tile.Valid)
        {
            return "impassable terrain";
        }

        Tile tileData = Find.World.grid[tile];
        if (tileData == null)
        {
            return "impassable terrain";
        }

        if (tileData.hilliness == Hilliness.Impassable)
        {
            return "impassable mountains";
        }

        if (tileData.PrimaryBiome != null && tileData.PrimaryBiome.impassable)
        {
            return tileData.PrimaryBiome.LabelCap;
        }

        return "impassable terrain";
    }

    /// <summary>
    /// Invoked whenever vision reveals newly explored sectors around a player caravan.
    /// Checks if the destination or any upcoming node on the caravan's active path is now known to be impassable.
    /// If an obstacle is detected, triggers dynamic route recalculation around the discovered obstacle.
    /// </summary>
    public static void NotifyCaravanVisionUpdated(Caravan caravan)
    {
        if (caravan == null || caravan.pather == null || !IsMoving(caravan.pather))
        {
            return;
        }

        if (Find.World?.worldObjects != null && !caravan.Spawned)
        {
            return;
        }

        if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.ClassicHazeOfWar)
        {
            return;
        }

        PlanetTile destTile = caravan.pather.Destination;
        if (!destTile.Valid)
        {
            return;
        }

        // 1. Destination sector itself was discovered to be impassable
        if (!IsTileUnexplored(destTile) && IsTileImpassable(destTile))
        {
            string reason = GetImpassableReason(destTile);
            PlanetTile bestNearby = PlanetTile.Invalid;

            if (Find.World != null)
            {
                bestNearby = CaravanUtility.BestGotoDestNear(destTile, caravan);
            }

            if (bestNearby.Valid && bestNearby != caravan.Tile)
            {
                if (Find.World != null)
                {
                    caravan.pather.StartPath(bestNearby, caravan.pather.ArrivalAction, repathImmediately: true);
                }
                if (Current.ProgramState == ProgramState.Playing)
                {
                    Messages.Message(
                        $"Caravan {caravan.LabelCap} discovered destination sector {destTile.tileId} is impassable ({reason}). Rerouting to adjacent sector {bestNearby.tileId}.",
                        MessageTypeDefOf.CautionInput,
                        false);
                }
                OnCaravanReroutedForTesting?.Invoke(caravan, bestNearby, reason);
            }
            else
            {
                caravan.pather.StopDead();
                if (Current.ProgramState == ProgramState.Playing)
                {
                    Messages.Message(
                        $"Caravan {caravan.LabelCap} reached the edge of sector {destTile.tileId}; further passage is blocked by {reason}.",
                        MessageTypeDefOf.NegativeEvent,
                        false);
                }
                OnCaravanHaltedForTesting?.Invoke(caravan, destTile, reason);
            }
            return;
        }

        // 2. Intermediate path nodes discovered to be impassable
        WorldPath curPath = caravan.pather.curPath;
        if (curPath == null || !curPath.Found)
        {
            return;
        }

        PlanetTile blockedTile = PlanetTile.Invalid;
        PlanetTile nextTile = caravan.pather.nextTile;

        if (nextTile.Valid && !IsTileUnexplored(nextTile) && IsTileImpassable(nextTile))
        {
            blockedTile = nextTile;
        }
        else
        {
            int nodesToCheck = Mathf.Min(curPath.NodesLeftCount, 30);
            for (int i = 0; i < nodesToCheck; i++)
            {
                PlanetTile node = curPath.Peek(i);
                if (node.Valid && !IsTileUnexplored(node) && IsTileImpassable(node))
                {
                    blockedTile = node;
                    break;
                }
            }
        }

        if (blockedTile.Valid)
        {
            string reason = GetImpassableReason(blockedTile);

            // Repath to destination!
            // The newly discovered obstacle is now explored, so WorldPathing will path around it,
            // while continuing optimistically through any remaining unexplored sectors.
            if (Find.World != null)
            {
                caravan.pather.StartPath(caravan.pather.Destination, caravan.pather.ArrivalAction, repathImmediately: true);
            }

            if (IsMoving(caravan.pather))
            {
                if (Current.ProgramState == ProgramState.Playing)
                {
                    Messages.Message(
                        $"Caravan {caravan.LabelCap} encountered {reason} in sector {blockedTile.tileId} and adjusted its route.",
                        MessageTypeDefOf.NeutralEvent,
                        false);
                }
                OnCaravanReroutedForTesting?.Invoke(caravan, blockedTile, reason);
            }
            else
            {
                if (Current.ProgramState == ProgramState.Playing)
                {
                    Messages.Message(
                        $"Caravan {caravan.LabelCap} encountered {reason} in sector {blockedTile.tileId} with no passable route forward. Caravan halted.",
                        MessageTypeDefOf.NegativeEvent,
                        false);
                }
                OnCaravanHaltedForTesting?.Invoke(caravan, blockedTile, reason);
            }
        }
    }
}
