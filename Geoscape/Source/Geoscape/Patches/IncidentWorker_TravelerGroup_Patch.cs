using HarmonyLib;
using RimWorld;

namespace ProjectVigilance.Geoscape.Patches;

[HarmonyPatch(typeof(IncidentWorker_TravelerGroup), "TryExecuteWorker")]
public static class IncidentWorker_TravelerGroup_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(IncidentWorker_TravelerGroup __instance, IncidentParms parms, ref bool __result)
    {
        return NeutralGroupInterceptHelper.TryIntercept(__instance, parms, ref __result);
    }
}
