using HarmonyLib;
using RimWorld;

namespace ProjectVigilance.Geoscape.Patches;

[HarmonyPatch(typeof(IncidentWorker_VisitorGroup), "TryExecuteWorker")]
public static class IncidentWorker_VisitorGroup_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(IncidentWorker_VisitorGroup __instance, IncidentParms parms, ref bool __result)
    {
        return NeutralGroupInterceptHelper.TryIntercept(__instance, parms, ref __result);
    }
}
