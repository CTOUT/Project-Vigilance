using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Registers newly spawned dynamic world sites (quests, work sites, bandit camps)
/// in unmonitored Haze tiles into HazeReconnaissanceManager for letter suppression and timeout pausing.
/// </summary>
[HarmonyPatch(typeof(WorldObject), nameof(WorldObject.SpawnSetup))]
public static class WorldObject_SpawnSetup_ConcealmentPatch
{
    public static void Postfix(WorldObject __instance)
    {
        if (__instance == null || __instance.Faction == Faction.OfPlayer)
        {
            return;
        }

        HazeReconnaissanceManager.Instance?.Notify_WorldObjectSpawned(__instance);
    }
}
