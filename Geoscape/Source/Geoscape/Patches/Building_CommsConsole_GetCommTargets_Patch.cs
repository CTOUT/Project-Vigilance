using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using ProjectVigilance.Geoscape.Orbital;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Appends active trade satellites passing overhead the colony's planetary tile to the Comms Console's
/// communication targets list, creating dynamic, time-limited orbital trade windows.
/// </summary>
[HarmonyPatch(typeof(Building_CommsConsole), nameof(Building_CommsConsole.GetCommTargets))]
public static class Building_CommsConsole_GetCommTargets_Patch
{
    public static IEnumerable<ICommunicable> Postfix(IEnumerable<ICommunicable> __result, Building_CommsConsole __instance, Pawn myPawn)
    {
        if (__result != null)
        {
            foreach (ICommunicable communicable in __result)
            {
                yield return communicable;
            }
        }

        if (__instance?.Map == null || !__instance.Map.Tile.Valid)
        {
            yield break;
        }

        PlanetTile mapTile = __instance.Map.Tile;
        List<Satellite_Trade>? activeTradeSats = OrbitalManager.Instance?.GetActiveTradeSatellitesOverTile(mapTile);

        if (activeTradeSats != null)
        {
            for (int i = 0; i < activeTradeSats.Count; i++)
            {
                yield return activeTradeSats[i];
            }
        }
    }
}
