using HarmonyLib;
using ProjectVigilance.Geoscape.World;
using Verse;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Prevents vanilla pawn generation from picking Geoscape-tracked world pawns out of
/// <see cref="RimWorld.Planet.WorldPawns"/> for unrelated redressing.
/// </summary>
[HarmonyPatch(typeof(PawnGenerator), "IsValidCandidateToRedress")]
public static class PawnGenerator_IsValidCandidateToRedress_Patch
{
    public static void Postfix(Pawn pawn, ref bool __result)
    {
        if (__result && WorldPawnIntegrityUtility.IsTrackedByAnyGeoscapeSystem(pawn))
        {
            __result = false;
        }
    }
}
