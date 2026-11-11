using HarmonyLib;
using ProjectVigilance.Geoscape.World;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Ensures <see cref="WorldPawnGC"/> treats all Geoscape-tracked pawns as critical even if
/// external mod calls or state changes unpin them from <see cref="WorldPawns.ForcefullyKeptPawns"/>.
/// </summary>
[HarmonyPatch(typeof(WorldPawnGC), "GetCriticalPawnReason")]
public static class WorldPawnGC_GetCriticalPawnReason_Patch
{
    public static void Postfix(Pawn pawn, ref string? __result)
    {
        if (__result == null && WorldPawnIntegrityUtility.IsTrackedByAnyGeoscapeSystem(pawn))
        {
            __result = "ProjectVigilance_Tracked";
        }
    }
}
