using HarmonyLib;
using RimWorld.Planet;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Removes despawned or destroyed dynamic world sites from HazeReconnaissanceManager
/// to prevent stale object references and suppressed letters in save games.
/// </summary>
[HarmonyPatch(typeof(WorldObject), nameof(WorldObject.PostRemove))]
public static class WorldObject_PostRemove_Patch
{
    public static void Postfix(WorldObject __instance)
    {
        if (__instance == null)
        {
            return;
        }

        HazeReconnaissanceManager.Instance?.Notify_WorldObjectRemoved(__instance);
        SensoryManager.Instance?.Notify_WorldObjectRemoved(__instance);
    }
}
