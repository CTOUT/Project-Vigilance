using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Allows neutral and trade caravan incidents to fire when colonists are temporarily away on caravan,
/// enabling overland trade caravans and visitors to set off towards the colony and be intercepted.
/// </summary>
[HarmonyPatch(typeof(IncidentWorker_NeutralGroup), "CanFireNowSub")]
public static class IncidentWorker_NeutralGroup_CanFireNowSub_Patch
{
    [HarmonyPostfix]
    public static void Postfix(IncidentWorker_NeutralGroup __instance, IncidentParms parms, ref bool __result)
    {
        if (__result)
        {
            return;
        }

        // If CanFireNowSub failed solely because no conscious colonists are currently on the map,
        // allow the incident to fire if targeting an active player settlement with valid candidate factions.
        if (parms.target is Map map && map.IsPlayerHome && map.Parent != null && !map.Parent.Destroyed)
        {
            if (parms.faction != null || Find.FactionManager.AllFactions.Any(f => __instance.FactionCanBeGroupSource(f, parms, false)))
            {
                __result = true;
            }
        }
    }
}
