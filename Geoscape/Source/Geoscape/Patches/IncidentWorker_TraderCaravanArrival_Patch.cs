using HarmonyLib;
using RimWorld;

namespace ProjectVigilance.Geoscape.Patches;

[HarmonyPatch(typeof(IncidentWorker_TraderCaravanArrival), "TryExecuteWorker")]
public static class IncidentWorker_TraderCaravanArrival_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(IncidentWorker_TraderCaravanArrival __instance, IncidentParms parms, ref bool __result)
    {
        return NeutralGroupInterceptHelper.TryIntercept(__instance, parms, ref __result);
    }
}
