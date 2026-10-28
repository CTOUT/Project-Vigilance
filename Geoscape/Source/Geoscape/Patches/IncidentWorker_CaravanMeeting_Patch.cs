using HarmonyLib;
using RimWorld;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Intercepts abstract storyteller caravan meetings to ground them into persistent
/// travelling WorldObject_Incursion entities with living rosters and continuous travel paths.
/// </summary>
[HarmonyPatch(typeof(IncidentWorker_CaravanMeeting), "TryExecuteWorker")]
public static class IncidentWorker_CaravanMeeting_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(IncidentWorker_CaravanMeeting __instance, IncidentParms parms, ref bool __result)
    {
        return GroundedCaravanMeetingHelper.TryInterceptMeeting(__instance, parms, ref __result);
    }
}
