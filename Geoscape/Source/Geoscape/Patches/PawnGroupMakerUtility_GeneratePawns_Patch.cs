using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;
using ProjectVigilance.Geoscape.World;

namespace ProjectVigilance.Geoscape.Patches;

[HarmonyPatch(typeof(PawnGroupMakerUtility), nameof(PawnGroupMakerUtility.GeneratePawns))]
public static class PawnGroupMakerUtility_GeneratePawns_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(PawnGroupMakerParms parms, ref IEnumerable<Pawn> __result)
    {
        if (IncursionArrivalManager.IsExecutingArrival &&
            IncursionArrivalManager.CurrentExecutingIncursion?.CommittedPawns is { Count: > 0 } committed)
        {
            var surviving = committed.Where(p => p != null && !p.Dead && !p.Destroyed).ToList();
            if (surviving.Count > 0)
            {
                // Strip any existing/stale Lords before vanilla arrival and Lord creation
                foreach (var p in surviving)
                {
                    Lord? currentLord = p.GetLord();
                    if (currentLord != null)
                    {
                        currentLord.RemovePawn(p);
                    }
                }

                __result = surviving;
                Log.Message($"[Project Vigilance] Raid arrival reusing {surviving.Count} previously scouted/committed combatants (exact force persistence).");
                return false;
            }
        }

        return true;
    }

    [HarmonyPostfix]
    public static void Postfix(ref IEnumerable<Pawn> __result)
    {
        if (!IncursionArrivalManager.IsExecutingArrival ||
            IncursionArrivalManager.CurrentExecutingIncursion == null ||
            __result == null)
        {
            return;
        }

        var list = __result as List<Pawn> ?? __result.ToList();
        __result = list;

        var incursion = IncursionArrivalManager.CurrentExecutingIncursion;
        if ((incursion.CommittedPawns == null || incursion.CommittedPawns.Count == 0) && list.Count > 0)
        {
            incursion.SetCommittedPawns(list);
        }
    }
}
