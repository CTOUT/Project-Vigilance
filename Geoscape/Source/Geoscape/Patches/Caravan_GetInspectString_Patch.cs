using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Appends active outpost construction and dismantling progress to the visiting caravan's inspect string,
/// providing clear, real-time feedback when stationed at an outpost site on the world map.
/// </summary>
[HarmonyPatch(typeof(Caravan), nameof(Caravan.GetInspectString))]
public static class Caravan_GetInspectString_Patch
{
    public static void Postfix(Caravan __instance, ref string __result)
    {
        if (__instance == null || !__instance.Spawned || __instance.Faction != Faction.OfPlayer || !__instance.Tile.Valid)
        {
            return;
        }

        var worldObjects = Current.Game?.World?.worldObjects;
        if (worldObjects == null) return;

        WorldObject_ListeningPost? post = worldObjects.AllWorldObjects?.OfType<WorldObject_ListeningPost>().FirstOrDefault(p => p.Tile == __instance.Tile);
        if (post != null)
        {
            if (post.IsConstructing)
            {
                int pct = Mathf.Clamp(Mathf.RoundToInt((1f - (float)post.ConstructionTicksLeft / Mathf.Max(1, post.ConstructionTicksTotal)) * 100f), 0, 99);
                float hoursLeft = post.ConstructionTicksLeft / 2500f;
                string line = $"Activity: Constructing {post.LabelCap} ({pct}%, ~{hoursLeft:F1}h remaining)";
                __result = string.IsNullOrEmpty(__result) ? line : $"{__result}\n{line}";
            }
            else if (post.IsDismantling)
            {
                int pct = Mathf.Clamp(Mathf.RoundToInt((1f - (float)post.DismantleTicksLeft / Mathf.Max(1, post.DismantleTicksTotal)) * 100f), 0, 99);
                float hoursLeft = post.DismantleTicksLeft / 2500f;
                string line = $"Activity: Dismantling {post.LabelCap} ({pct}%, ~{hoursLeft:F1}h remaining)";
                __result = string.IsNullOrEmpty(__result) ? line : $"{__result}\n{line}";
            }
        }
    }
}
