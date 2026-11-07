using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Pre-populates the destination waypoint when WorldRoutePlanner opens for a Dialog_FormCaravan
/// that already has a valid destination tile selected, visualizing the route path immediately.
/// </summary>
[HarmonyPatch(typeof(WorldRoutePlanner), nameof(WorldRoutePlanner.Start), typeof(Dialog_FormCaravan))]
public static class WorldRoutePlanner_Start_Patch
{
    [System.ThreadStatic]
    public static System.Action<PlanetTile>? OnWaypointAddedForTesting;

    public static void Postfix(WorldRoutePlanner __instance, Dialog_FormCaravan formCaravanDialog)
    {
        if (formCaravanDialog != null && __instance != null && __instance.Active)
        {
            PlanetTile dest = Traverse.Create(formCaravanDialog).Field("destinationTile").GetValue<PlanetTile>();
            Map? map = Traverse.Create(formCaravanDialog).Field("map").GetValue<Map>();
            PlanetTile currentTile = map != null ? map.Tile : PlanetTile.Invalid;

            if (dest.Valid && (!currentTile.Valid || dest != currentTile))
            {
                var waypoints = Traverse.Create(__instance).Field("waypoints").GetValue<List<RoutePlannerWaypoint>>();
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
                    }
                }
            }
        }
    }
}
