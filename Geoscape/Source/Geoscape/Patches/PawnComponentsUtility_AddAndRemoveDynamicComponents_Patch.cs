using HarmonyLib;
using RimWorld;
using Verse;
using ProjectVigilance.Geoscape.World;

namespace ProjectVigilance.Geoscape.Patches;

[HarmonyPatch(typeof(PawnComponentsUtility), nameof(PawnComponentsUtility.AddAndRemoveDynamicComponents))]
public static class PawnComponentsUtility_AddAndRemoveDynamicComponents_Patch
{
    [HarmonyPrefix]
    public static void Prefix(Pawn pawn)
    {
        if (pawn != null && IncursionTrackingHelper.TryGetTraderIncursionFor(pawn, out var incursion) && incursion != null)
        {
            IncursionTrackingHelper.EnsureTraderComponents(pawn, incursion);
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
