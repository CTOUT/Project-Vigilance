using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Freezes quest timeout countdowns while the associated dynamic world site remains concealed
/// in the unmonitored Haze of War.
/// </summary>
[HarmonyPatch(typeof(QuestPart_Delay), nameof(QuestPart_Delay.QuestPartTick))]
public static class QuestPart_Delay_QuestPartTick_Patch
{
    public static bool Prefix(QuestPart_Delay __instance, ref int ___enableTick)
    {
        HazeReconnaissanceManager? mgr = HazeReconnaissanceManager.Instance;
        if (__instance == null || mgr == null || !mgr.HasAnyConcealedSites) return true;

        WorldObject? targetWo = (__instance as QuestPart_WorldObjectTimeout)?.worldObject;
        if (targetWo == null && __instance.quest != null)
        {
            targetWo = mgr.GetConcealedSiteForQuest(__instance.quest);
        }

        if (targetWo != null && mgr.IsSiteConcealed(targetWo))
        {
            // Freeze quest countdown: advance enableTick in lockstep with current game tick so TicksLeft remains static
            if (___enableTick >= 0)
            {
                ___enableTick++;
            }
            return false;
        }

        return true;
    }
}
