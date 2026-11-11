using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Pre-populates the destination waypoint when WorldRoutePlanner opens for a Dialog_FormCaravan
/// that already has a valid destination tile selected, visualizing the route path immediately.
/// </summary>
[HarmonyPatch(typeof(WorldRoutePlanner), nameof(WorldRoutePlanner.Start), typeof(Dialog_FormCaravan))]
public static class WorldRoutePlanner_Start_Patch
{
    private static readonly System.Reflection.FieldInfo? DestinationTileField =
        AccessTools.Field(typeof(Dialog_FormCaravan), "destinationTile");
    private static readonly System.Reflection.FieldInfo? MapField =
        AccessTools.Field(typeof(Dialog_FormCaravan), "map");
    private static readonly System.Reflection.FieldInfo? WaypointsField =
        AccessTools.Field(typeof(WorldRoutePlanner), "waypoints");

    [System.ThreadStatic]
    public static System.Action<PlanetTile>? OnWaypointAddedForTesting;

    public static void Postfix(WorldRoutePlanner __instance, Dialog_FormCaravan formCaravanDialog)
    {
        if (formCaravanDialog != null && __instance != null && __instance.Active)
        {
            PlanetTile dest = DestinationTileField != null && DestinationTileField.GetValue(formCaravanDialog) is PlanetTile pt
                ? pt
                : PlanetTile.Invalid;
            Map? map = MapField?.GetValue(formCaravanDialog) as Map;
            PlanetTile currentTile = map != null ? map.Tile : PlanetTile.Invalid;

            if (dest.Valid && (!currentTile.Valid || dest != currentTile))
            {
                var waypoints = WaypointsField?.GetValue(__instance) as List<RoutePlannerWaypoint>;
                if (waypoints != null && waypoints.Count == 1)
                {
                    if (OnWaypointAddedForTesting != null)
                    {
                        OnWaypointAddedForTesting(dest);
                        return;
                    }

                    if (Find.WorldObjects != null)
                    {
                        __instance.TryAddWaypoint(dest, playSound: false);

                        if (CaravanExpeditionUtility.TryGetPendingDirectiveForDialog(formCaravanDialog, out _))
                        {
                            __instance.Stop();
                            if (Find.WindowStack != null && !Find.WindowStack.IsOpen(formCaravanDialog))
                            {
                                Find.WindowStack.Add(formCaravanDialog);
                            }
                        }
                    }
                }
            }
        }
    }
}
