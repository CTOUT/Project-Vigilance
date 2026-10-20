using HarmonyLib;
using RimWorld;
using Verse;
using ProjectVigilance.Geoscape.World;

namespace ProjectVigilance.Geoscape.Patches;

[HarmonyPatch(typeof(Pawn), nameof(Pawn.ExitMap))]
public static class Pawn_ExitMap_Patch
{
    [HarmonyPrefix]
    public static void Prefix(Pawn __instance, bool allowedToJoinOrCreateCaravan, Rot4 exitDir)
    {
        if (__instance == null || __instance.Dead || __instance.Faction == null)
        {
            return;
        }

        // Only track hostile raiders departing player home maps
        if (__instance.Map != null && __instance.Map.IsPlayerHome && __instance.Faction.HostileTo(Faction.OfPlayer))
        {
            Pawn? captive = null;
            if (__instance.carryTracker?.CarriedThing is Pawn carriedPawn && carriedPawn.Faction == Faction.OfPlayer)
            {
                captive = carriedPawn;
            }

            IncursionRetreatManager.RegisterFleeingPawn(__instance, captive);
        }
    }
}
