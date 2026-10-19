using HarmonyLib;
using ProjectVigilance.Geoscape.World;
using RimWorld;
using Verse;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Bypasses vanilla IncidentWorker.TryExecute's requireColonistsPresent abort check
/// when an empty colony settlement is targeted by an overland neutral group (visitors or trade caravans).
/// </summary>
[HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.TryExecute))]
public static class IncidentWorker_TryExecute_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(IncidentWorker __instance, IncidentParms parms, ref bool __result)
    {
        if (IncursionArrivalManager.IsExecutingArrival)
        {
            return true;
        }

        if (__instance is IncidentWorker_NeutralGroup &&
            __instance.def.requireColonistsPresent &&
            parms.target is Map map && map.IsPlayerHome && map.Parent != null && !map.Parent.Destroyed &&
            map.mapPawns.FreeColonistsSpawnedCount == 0)
        {
            // Intercept empty colony arrivals before requireColonistsPresent early-returns
            return NeutralGroupInterceptHelper.TryIntercept(__instance, parms, ref __result);
        }

        return true;
    }
}
