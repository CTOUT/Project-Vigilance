using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Intercepts caravan movement departure from world tiles containing active, incomplete listening post
/// construction or dismantling operations to prevent accidental abandonment or ghost-building while marching.
/// Prompts the player to leave the outpost part-built (pausing progress), cancel construction for 100%
/// material refund, or stay on-site.
/// </summary>
[HarmonyPatch(typeof(Caravan_PathFollower), nameof(Caravan_PathFollower.StartPath))]
public static class Caravan_PathFollower_StartPath_Patch
{
    [ThreadStatic]
    public static bool SuppressDeparturePrompt;

    /// <summary>
    /// Optional hook for resolving listening posts in headless tests.
    /// </summary>
    public static Func<PlanetTile, WorldObject_ListeningPost?>? ActivePostFinderOverride { get; set; }

    /// <summary>
    /// Optional hook for headless testing.
    /// </summary>
    public static Action<Caravan, WorldObject_ListeningPost, PlanetTile>? DepartureDialogOverride { get; set; }

    public static bool Prefix(
        Caravan_PathFollower __instance,
        Caravan? ___caravan,
        PlanetTile destTile,
        CaravanArrivalAction arrivalAction,
        bool repathImmediately,
        bool resetPauseStatus,
        ref bool __result)
    {
        if (SuppressDeparturePrompt)
        {
            return true;
        }

        Caravan? caravan = ___caravan ?? Traverse.Create(__instance).Field("caravan").GetValue<Caravan>();
        if (caravan == null)
        {
            return true;
        }

        bool isPlayer = false;
        try
        {
            isPlayer = caravan.Faction?.IsPlayer == true || caravan.IsPlayerControlled;
        }
        catch
        {
            isPlayer = caravan.Faction?.IsPlayer == true;
        }
        if (!isPlayer)
        {
            return true;
        }

        try
        {
            if (Current.Game != null && Find.World != null && !caravan.Spawned)
            {
                return true;
            }
        }
        catch
        {
            // Headless unit test without world
        }

        // Only prompt if departing to a different tile
        if (destTile == caravan.Tile)
        {
            return true;
        }

        // Only prompt if caravan was stationary on-site (not already travelling between tiles)
        bool isMoving = false;
        try
        {
            isMoving = __instance.Moving;
        }
        catch
        {
            isMoving = Traverse.Create(__instance).Field("moving").GetValue<bool>();
        }
        if (isMoving)
        {
            return true;
        }

        // Find active listening post on the origin tile
        WorldObject_ListeningPost? post = null;
        if (ActivePostFinderOverride != null)
        {
            post = ActivePostFinderOverride(caravan.Tile);
        }
        else
        {
            try
            {
                if (Current.Game != null && Find.World != null)
                {
                    post = Find.WorldObjects?.AllWorldObjects?
                        .OfType<WorldObject_ListeningPost>()
                        .FirstOrDefault(p => p.Tile == caravan.Tile && (p.IsConstructing || p.IsDismantling));
                }
            }
            catch
            {
                // Headless or uninitialised world
            }
        }

        if (post == null)
        {
            return true;
        }

        // Allow test override
        if (DepartureDialogOverride != null)
        {
            DepartureDialogOverride(caravan, post, destTile);
            __result = false;
            return false;
        }

        if (Current.ProgramState != ProgramState.Playing || Find.WindowStack == null)
        {
            return true;
        }

        ShowDepartureDialog(caravan, post, destTile, arrivalAction, repathImmediately, resetPauseStatus);
        __result = false;
        return false;
    }

    private static void ShowDepartureDialog(
        Caravan caravan,
        WorldObject_ListeningPost post,
        PlanetTile destTile,
        CaravanArrivalAction arrivalAction,
        bool repathImmediately,
        bool resetPauseStatus)
    {
        if (post.IsConstructing)
        {
            ShowConstructionDepartureDialog(caravan, post, destTile, arrivalAction, repathImmediately, resetPauseStatus);
        }
        else if (post.IsDismantling)
        {
            ShowDismantlingDepartureDialog(caravan, post, destTile, arrivalAction, repathImmediately, resetPauseStatus);
        }
    }

    private static void ShowConstructionDepartureDialog(
        Caravan caravan,
        WorldObject_ListeningPost post,
        PlanetTile destTile,
        CaravanArrivalAction arrivalAction,
        bool repathImmediately,
        bool resetPauseStatus)
    {
        int progressPct = post.ConstructionProgressPct;
        string materialName = post.stuffDef != null
            ? $"{OutpostMaterialUtility.SignalBeaconWoodOrStoneCost} {post.stuffDef.LabelCap}"
            : (post.Kind == ListeningPostKind.AutomatedRelay
                ? $"{OutpostMaterialUtility.AutomatedRelaySteelCost} Steel and {OutpostMaterialUtility.AutomatedRelayComponentCost} Components"
                : "materials");

        string title = "Outpost Construction Incomplete";
        string text = $"Construction of {post.LabelCap} at Tile {post.Tile.tileId} is currently underway ({progressPct}% complete).\n\n" +
                      $"Departing will pause construction. Alternatively, you can cancel construction to reclaim 100% of the invested materials ({materialName}) into caravan inventory.\n\n" +
                      "What would you like to do?";

        Action leavePartBuiltAction = () =>
        {
            SuppressDeparturePrompt = true;
            try
            {
                caravan.pather.StartPath(destTile, arrivalAction, repathImmediately, resetPauseStatus);
                Messages.Message($"Construction of {post.LabelCap} paused. Caravan departed Tile {caravan.Tile.tileId}.", caravan, MessageTypeDefOf.CautionInput, false);
            }
            finally
            {
                SuppressDeparturePrompt = false;
            }
        };

        Action cancelAndRefundAction = () =>
        {
            post.CancelConstruction(caravan);
            SuppressDeparturePrompt = true;
            try
            {
                caravan.pather.StartPath(destTile, arrivalAction, repathImmediately, resetPauseStatus);
            }
            finally
            {
                SuppressDeparturePrompt = false;
            }
        };

        Action stayAction = () =>
        {
            // Caravan stays stationary to finish assembly
        };

        var dialog = new Dialog_MessageBox(
            text: text,
            buttonAText: "Leave Part-Built",
            buttonAAction: leavePartBuiltAction,
            buttonBText: "Cancel & Refund",
            buttonBAction: cancelAndRefundAction,
            title: title,
            buttonADestructive: false,
            acceptAction: null,
            cancelAction: stayAction);

        dialog.buttonCText = "Stay On-Site";
        dialog.buttonCAction = stayAction;

        Find.WindowStack?.Add(dialog);
    }

    private static void ShowDismantlingDepartureDialog(
        Caravan caravan,
        WorldObject_ListeningPost post,
        PlanetTile destTile,
        CaravanArrivalAction arrivalAction,
        bool repathImmediately,
        bool resetPauseStatus)
    {
        string title = "Outpost Dismantling Incomplete";
        string text = $"Dismantling of {post.LabelCap} at Tile {post.Tile.tileId} is currently underway.\n\n" +
                      "Departing will pause dismantling. Alternatively, you can cancel dismantling to restore the outpost to operational status immediately.\n\n" +
                      "What would you like to do?";

        Action pauseDismantlingAction = () =>
        {
            SuppressDeparturePrompt = true;
            try
            {
                caravan.pather.StartPath(destTile, arrivalAction, repathImmediately, resetPauseStatus);
                Messages.Message($"Dismantling of {post.LabelCap} paused. Caravan departed Tile {caravan.Tile.tileId}.", caravan, MessageTypeDefOf.CautionInput, false);
            }
            finally
            {
                SuppressDeparturePrompt = false;
            }
        };

        Action cancelDismantlingAction = () =>
        {
            post.CancelDismantling();
            SuppressDeparturePrompt = true;
            try
            {
                caravan.pather.StartPath(destTile, arrivalAction, repathImmediately, resetPauseStatus);
            }
            finally
            {
                SuppressDeparturePrompt = false;
            }
        };

        Action stayAction = () =>
        {
        };

        var dialog = new Dialog_MessageBox(
            text: text,
            buttonAText: "Pause Dismantling",
            buttonAAction: pauseDismantlingAction,
            buttonBText: "Cancel Dismantle",
            buttonBAction: cancelDismantlingAction,
            title: title,
            buttonADestructive: false,
            acceptAction: null,
            cancelAction: stayAction);

        dialog.buttonCText = "Stay On-Site";
        dialog.buttonCAction = stayAction;

        Find.WindowStack?.Add(dialog);
    }
}
