using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using ProjectVigilance.Geoscape.World;

namespace ProjectVigilance.Geoscape.Patches;

[HarmonyPatch(typeof(IncidentWorker_Raid), "TryGenerateRaidInfo")]
public static class IncidentWorker_Raid_TryGenerateRaidInfo_Patch
{
    [HarmonyPrefix]
    public static void Prefix(IncidentParms parms)
    {
        if (IncursionArrivalManager.IsExecutingArrival && IncursionArrivalManager.CurrentExecutingIncursion != null)
        {
            var incursion = IncursionArrivalManager.CurrentExecutingIncursion;
            if (incursion.Faction != null)
            {
                parms.faction = incursion.Faction;
            }
        }
    }

    [HarmonyPostfix]
    public static void Postfix(IncidentParms parms, ref List<Pawn> pawns, bool __result)
    {
        if (__result && IncursionArrivalManager.IsExecutingArrival && IncursionArrivalManager.CurrentExecutingIncursion != null)
        {
            var incursion = IncursionArrivalManager.CurrentExecutingIncursion;
            if (pawns != null && pawns.Count > 0)
            {
                incursion.SetCommittedPawns(pawns);
                incursion.IsEngagedInTacticalMap = true;

                if (parms.target is Map map)
                {
                    var ambushComp = map.GetComponent<MapComponent_OverlandAmbush>();
                    ambushComp?.RegisterIncursion(incursion, pawns);
                }
            }
        }
    }
}
