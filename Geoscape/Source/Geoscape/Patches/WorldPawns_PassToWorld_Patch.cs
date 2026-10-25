using HarmonyLib;
using RimWorld.Planet;
using Verse;
using ProjectVigilance.Geoscape.World;

namespace ProjectVigilance.Geoscape.Patches;

[HarmonyPatch(typeof(WorldPawns), nameof(WorldPawns.PassToWorld))]
public static class WorldPawns_PassToWorld_Patch
{
    [HarmonyPrefix]
    public static void Prefix(Pawn pawn, ref PawnDiscardDecideMode discardMode)
    {
        if (pawn != null)
        {
            if (IncursionTrackingHelper.IsTrackedByAnyIncursion(pawn))
            {
                discardMode = PawnDiscardDecideMode.KeepForever;
                Find.WorldPawns?.ForcefullyKeptPawns?.Add(pawn);
            }
        }
    }

    [HarmonyPostfix]
    public static void Postfix(Pawn pawn)
    {
        if (pawn != null && IncursionTrackingHelper.TryGetTraderIncursionFor(pawn, out var incursion) && incursion != null)
        {
            IncursionTrackingHelper.EnsureTraderComponents(pawn, incursion);
        }
    }
}
