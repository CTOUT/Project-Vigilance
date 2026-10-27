using HarmonyLib;
using RimWorld.Planet;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Suspends TimeoutComp countdown while a dynamic site remains concealed
/// in the unmonitored Haze of War so quests and dynamic camps do not invisibly expire before being uncovered.
/// </summary>
[HarmonyPatch(typeof(TimeoutComp), nameof(TimeoutComp.CompTick))]
public static class TimeoutComp_CompTick_Patch
{
    public static bool Prefix(TimeoutComp __instance)
    {
        if (__instance?.parent != null &&
            HazeReconnaissanceManager.Instance != null &&
            HazeReconnaissanceManager.Instance.IsSiteConcealed(__instance.parent))
        {
            // Suspended while concealed in the Haze so quests do not expire invisibly!
            return false;
        }

        return true;
    }
}
