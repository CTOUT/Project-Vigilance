using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
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
        if (Current.ProgramState != ProgramState.Playing || __instance == null || __instance.Faction?.IsPlayer == true)
        {
            return;
        }

        HazeReconnaissanceManager.Instance?.Notify_WorldObjectSpawned(__instance);
    }
}
