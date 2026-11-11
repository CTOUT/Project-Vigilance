using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Refreshes planetary sensory sweeps immediately on new-game initialisation, caravan entry,
/// and initial player colonist spawning so home tiles and surrounding terrain reveal on tick 0.
/// </summary>
public static class InitialVisionRefresh_Patch
{
    [HarmonyPatch(typeof(Game), nameof(Game.InitNewGame))]
    public static class Game_InitNewGame_Patch
    {
        public static void Postfix()
        {
            SensoryManager.Instance?.RefreshImmediateVision();
        }
    }

    [HarmonyPatch]
    public static class CaravanEnterMapUtility_Enter_Patch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetDeclaredMethods(typeof(CaravanEnterMapUtility))
                .Where(m => m.Name == nameof(CaravanEnterMapUtility.Enter));
        }

        public static void Postfix()
        {
            if (Current.ProgramState != ProgramState.Playing)
            {
                return;
            }

            SensoryManager.Instance?.RefreshImmediateVision();
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup), typeof(Map), typeof(bool))]
    public static class Pawn_SpawnSetup_Patch
    {
        public static void Postfix(Pawn __instance, Map map, bool respawningAfterLoad)
        {
            if (__instance == null || respawningAfterLoad || map == null)
            {
                return;
            }

            if (Current.ProgramState != ProgramState.Playing || GeoscapeRules.VisibilityMode != VisibilityMode.ClassicHazeOfWar)
            {
                return;
            }

            if (__instance.Faction?.IsPlayer != true || !__instance.IsColonist)
            {
                return;
            }

            if (!map.IsPlayerHome && map.Parent?.Faction?.IsPlayer != true)
            {
                return;
            }

            SensoryManager? sensory = SensoryManager.Instance;
            if (sensory == null)
            {
                return;
            }

            if (!map.Tile.Valid || !sensory.HasActiveVision(map.Tile))
            {
                sensory.RefreshImmediateVision();
            }
            else
            {
                sensory.PerformSensorySweep();
            }
        }
    }
}
