using HarmonyLib;
using RimWorld;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Intercepts abstract storyteller caravan demands to ground them into persistent
/// travelling WorldObject_Incursion raider entities that maintain extortion inventory and retreat to bases.
/// </summary>
[HarmonyPatch(typeof(IncidentWorker_CaravanDemand), "TryExecuteWorker")]
public static class IncidentWorker_CaravanDemand_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(IncidentWorker_CaravanDemand __instance, IncidentParms parms, ref bool __result)
    {
        return GroundedCaravanMeetingHelper.TryInterceptDemand(__instance, parms, ref __result);
    }
}
