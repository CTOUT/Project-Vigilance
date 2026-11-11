using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using ProjectVigilance.Geoscape.World;

namespace ProjectVigilance.Geoscape.Patches;

[HarmonyPatch(typeof(CaravanExitMapUtility), nameof(CaravanExitMapUtility.CanExitMapAndJoinOrCreateCaravanNow))]
public static class CaravanExitMapUtility_CanExitMap_Patch
{
    [HarmonyPostfix]
    public static void Postfix(Pawn pawn, ref bool __result)
    {
        if (__result || pawn == null || !pawn.Spawned)
        {
            return;
        }

        Map? map = pawn.Map;
        if (map == null)
        {
            return;
        }

        bool isTacticalEncounterMap = IncursionTacticalRetreatUtility.IsOverlandEncounterMap(map);
        bool hasHomeRetreatCondition = map.IsPlayerHome &&
                                       (GenHostility.AnyHostileActiveThreatToPlayer(map, countDormantPawnsAsHostile: false) ||
                                        CaravanExitMapUtility.FindCaravanToJoinFor(pawn) != null);

        if (!IncursionTacticalRetreatUtility.ShouldAllowRetreatExitGrid(
                map.IsPlayerHome,
                isTacticalEncounterMap,
                hasHomeRetreatCondition))
        {
            return;
        }

        if (pawn.IsColonist ||
            pawn.IsColonySubhumanPlayerControlled ||
            CaravanExitMapUtility.FindCaravanToJoinFor(pawn) != null)
        {
            __result = true;
        }
    }
}
