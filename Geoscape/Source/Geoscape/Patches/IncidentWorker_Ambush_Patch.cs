using HarmonyLib;
using RimWorld;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Intercepts storyteller faction ambushes against caravans and grounds them into persistent
/// WorldObject_Incursion entities, so survivors can retreat or pursue after the tactical map closes.
/// Manhunter packs are left to vanilla handling.
/// </summary>
[HarmonyPatch(typeof(IncidentWorker_Ambush), "TryExecuteWorker")]
public static class IncidentWorker_Ambush_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(IncidentWorker_Ambush __instance, IncidentParms parms, ref bool __result)
    {
        if (__instance is not IncidentWorker_Ambush_EnemyFaction)
        {
            return true;
        }

        return GroundedCaravanMeetingHelper.TryInterceptAmbush(parms, ref __result);
    }
}
