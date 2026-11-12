using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Unit movement and traversal capability domain.
/// Determines which terrain types and environmental obstacles impede a given unit.
/// </summary>
public enum UnitTraversalCapability
{
    LandCaravan,
    NavalUnit,
    Aircraft
}

/// <summary>
/// Tracks per-expedition detour and obstacle notification state.
/// </summary>
public sealed class ExpeditionDetourState
{
    public float InitialDirectDistance { get; set; } = -1f;
    public float MaxDetourMultiplier { get; set; } = 2.0f;
    public HashSet<int> NotifiedObstacleTiles { get; } = new();
}

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
    public static Action<Caravan, PlanetTile, string>? OnCaravanHardStopPromptForTesting;

    private static readonly Dictionary<int, ExpeditionDetourState> expeditionStates = new();

    public static void ResetExpeditionState(int caravanId)
    {
        expeditionStates.Remove(caravanId);
    }

    public static ExpeditionDetourState GetOrCreateExpeditionState(Caravan caravan, PlanetTile destTile)
    {
        if (!expeditionStates.TryGetValue(caravan.ID, out var state))
        {
            state = new ExpeditionDetourState();
            if (caravan.Tile.Valid && destTile.Valid)
            {
                state.InitialDirectDistance = (Current.Game != null && Find.World != null && Find.WorldGrid != null)
                    ? Find.WorldGrid.ApproxDistanceInTiles(caravan.Tile, destTile)
                    : Math.Abs(caravan.Tile.tileId - destTile.tileId);
            }
            expeditionStates[caravan.ID] = state;
        }
        else if (state.InitialDirectDistance <= 0f && caravan.Tile.Valid && destTile.Valid)
        {
            state.InitialDirectDistance = (Current.Game != null && Find.World != null && Find.WorldGrid != null)
                ? Find.WorldGrid.ApproxDistanceInTiles(caravan.Tile, destTile)
                : Math.Abs(caravan.Tile.tileId - destTile.tileId);
        }
        return state;
    }

    public static bool IsTileObstacleForUnit(PlanetTile tile, UnitTraversalCapability capability = UnitTraversalCapability.LandCaravan)
    {
        switch (capability)
        {
            case UnitTraversalCapability.NavalUnit:
                return !SonarTraversalUtility.IsTileWaterCovered(tile);
            case UnitTraversalCapability.Aircraft:
                return false;
            case UnitTraversalCapability.LandCaravan:
            default:
                return IsTileImpassable(tile);
        }
    }

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

    [ThreadStatic]
    public static int ActiveFormCaravanCount;

    public static bool IsRoutePlannerActive
    {
        get
        {
            try
            {
                return Find.WorldRoutePlanner != null && Find.WorldRoutePlanner.Active;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Checks whether the player is actively forming a caravan via Dialog_FormCaravan.
    /// </summary>
    public static bool IsFormCaravanActive
    {
        get
        {
            if (ActiveFormCaravanCount > 0)
            {
                return true;
            }

            try
            {
                if (Find.WindowStack != null && Find.WindowStack.IsOpen<Dialog_FormCaravan>())
                {
                    return true;
                }
            }
            catch
            {
                // In headless tests or mock environments WindowStack may be null
            }

            return false;
        }
    }

    /// <summary>
    /// Evaluates whether the player is in any caravan route planning or caravan formation context.
    /// </summary>
    public static bool IsPlayerPlanningRoute =>
        IsRoutePlannerActive || IsFormCaravanActive;

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
               GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar;
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

        if (GeoscapeRules.VisibilityMode != VisibilityMode.ClassicHazeOfWar)
        {
            return;
        }

        PlanetTile destTile = caravan.pather.Destination;
        if (!destTile.Valid)
        {
            return;
        }

        ExpeditionDetourState detourState = GetOrCreateExpeditionState(caravan, destTile);

        // 1. Destination sector itself was discovered to be an obstacle
        if (!IsTileUnexplored(destTile) && IsTileObstacleForUnit(destTile, UnitTraversalCapability.LandCaravan))
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
                        "Caravan has encountered an obstacle and is searching for an alternative route.",
                        caravan,
                        MessageTypeDefOf.NeutralEvent,
                        false);
                }
                OnCaravanReroutedForTesting?.Invoke(caravan, bestNearby, reason);
            }
            else
            {
                TriggerHardStopPrompt(caravan, destTile, reason);
            }
            return;
        }

        // 2. Intermediate path nodes discovered to be obstacles
        WorldPath curPath = caravan.pather.curPath;
        if (curPath == null || !curPath.Found)
        {
            return;
        }

        PlanetTile blockedTile = PlanetTile.Invalid;
        PlanetTile nextTile = caravan.pather.nextTile;

        if (nextTile.Valid && !IsTileUnexplored(nextTile) && IsTileObstacleForUnit(nextTile, UnitTraversalCapability.LandCaravan))
        {
            blockedTile = nextTile;
        }
        else
        {
            int nodesToCheck = Mathf.Min(curPath.NodesLeftCount, 30);
            for (int i = 0; i < nodesToCheck; i++)
            {
                PlanetTile node = curPath.Peek(i);
                if (node.Valid && !IsTileUnexplored(node) && IsTileObstacleForUnit(node, UnitTraversalCapability.LandCaravan))
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

            bool stillMoving = IsMoving(caravan.pather);
            bool breakEvenExceeded = false;

            if (stillMoving && caravan.pather.curPath != null && caravan.pather.curPath.Found)
            {
                int remainingNodes = caravan.pather.curPath.NodesLeftCount;
                if (detourState.InitialDirectDistance > 0f &&
                    remainingNodes > detourState.InitialDirectDistance * detourState.MaxDetourMultiplier)
                {
                    breakEvenExceeded = true;
                }
            }

            if (stillMoving && !breakEvenExceeded)
            {
                if (detourState.NotifiedObstacleTiles.Add(blockedTile.tileId))
                {
                    if (Current.ProgramState == ProgramState.Playing)
                    {
                        Messages.Message(
                            "Caravan has encountered an obstacle and is searching for an alternative route.",
                            caravan,
                            MessageTypeDefOf.NeutralEvent,
                            false);
                    }
                }
                OnCaravanReroutedForTesting?.Invoke(caravan, blockedTile, reason);
            }
            else
            {
                TriggerHardStopPrompt(caravan, blockedTile, reason);
            }
        }
    }

    /// <summary>
    /// Executes a hard stop and triggers a high-priority prompt with 4 actionable player decisions
    /// when an expedition can no longer find a viable route or break-even against its destination objective.
    /// </summary>
    public static void TriggerHardStopPrompt(Caravan caravan, PlanetTile obstacleTile, string reason)
    {
        caravan.pather.StopDead();

        OnCaravanHaltedForTesting?.Invoke(caravan, obstacleTile, reason);
        OnCaravanHardStopPromptForTesting?.Invoke(caravan, obstacleTile, reason);

        if (Current.ProgramState == ProgramState.Playing && Find.WindowStack != null)
        {
            DiaNode root = new DiaNode("Caravan cannot determine a viable route to its destination and has halted awaiting new orders.");

            // 1. Select a new destination
            root.options.Add(new DiaOption("Select a new destination")
            {
                action = () =>
                {
                    CameraJumper.TryJumpAndSelect(caravan);
                },
                resolveTree = true
            });

            // 2. Authorise a wider search radius
            root.options.Add(new DiaOption("Authorise a wider search radius")
            {
                action = () =>
                {
                    ExpeditionDetourState state = GetOrCreateExpeditionState(caravan, caravan.pather.Destination);
                    state.MaxDetourMultiplier += 1.5f;
                    if (caravan.pather.Destination.Valid)
                    {
                        caravan.pather.StartPath(caravan.pather.Destination, caravan.pather.ArrivalAction, repathImmediately: true);
                    }
                },
                resolveTree = true
            });

            // 3. Cancel the expedition
            root.options.Add(new DiaOption("Cancel the expedition")
            {
                action = () =>
                {
                    Settlement? home = Find.WorldObjects?.Settlements?.FirstOrDefault(s => s.Faction == Faction.OfPlayer && s.HasMap);
                    if (home != null)
                    {
                        caravan.pather.StartPath(home.Tile, new CaravanArrivalAction_Enter(home), repathImmediately: true);
                    }
                },
                resolveTree = true
            });

            // 4. Continue manual exploration
            root.options.Add(new DiaOption("Continue manual exploration")
            {
                action = () =>
                {
                    // Leaves caravan halted awaiting direct player right-clicks
                },
                resolveTree = true
            });

            Find.WindowStack.Add(new Dialog_NodeTree(root, delayInteractivity: false, radioMode: false, title: "Expedition Blocked: " + caravan.LabelCap));
        }
        else if (Current.ProgramState == ProgramState.Playing)
        {
            Messages.Message(
                "Caravan cannot determine a viable route to its destination and has halted awaiting new orders.",
                caravan,
                MessageTypeDefOf.NegativeEvent,
                false);
        }
    }

    /// <summary>
    /// Forces all active moving player caravans to repath immediately.
    /// Called when planetary visibility rules transition (e.g. from Haze to Surveillance/Omniscient).
    /// </summary>
    public static void RepathMovingPlayerCaravans()
    {
        if (Find.WorldObjects == null) return;

        List<Caravan> caravans = Find.WorldObjects.Caravans;
        if (caravans == null) return;

        for (int i = 0; i < caravans.Count; i++)
        {
            Caravan caravan = caravans[i];
            if (caravan != null && caravan.Faction?.IsPlayer == true && caravan.Spawned && caravan.pather != null && IsMoving(caravan.pather))
            {
                PlanetTile dest = caravan.pather.Destination;
                if (dest.Valid)
                {
                    caravan.pather.StartPath(dest, caravan.pather.ArrivalAction, repathImmediately: true);
                }
            }
        }
    }
}
