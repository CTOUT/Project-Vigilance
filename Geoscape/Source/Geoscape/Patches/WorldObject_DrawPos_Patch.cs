using HarmonyLib;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Geoscape.Orbital;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Stratifies orbital assets across natural altitude bands rather than a single uniform flat ring.
/// Applies deterministic pseudo-random radial offsets to Odyssey space objects (stations, asteroids)
/// based on their unique ID and def hash.
/// </summary>
[HarmonyPatch(typeof(WorldObject), "get_DrawPos")]
public static class WorldObject_DrawPos_Patch
{
    public static System.Func<WorldObject, bool>? IsSpaceObjectOverride { get; set; }

    public static void Postfix(WorldObject __instance, ref Vector3 __result)
    {
        if (__instance == null || __instance is IOrbitalEntity || __instance is Caravan || __instance is WorldObject_ListeningPost)
        {
            return;
        }

        bool isSpace = false;
        if (IsSpaceObjectOverride != null)
        {
            isSpace = IsSpaceObjectOverride(__instance);
        }
        else if (Find.World != null && __instance.Tile.Valid)
        {
            try
            {
                var layer = __instance.Tile.Layer;
                if (Find.WorldGrid?.Surface != null && layer == Find.WorldGrid.Surface)
                {
                    return;
                }
                isSpace = layer?.Def?.isSpace == true || layer?.Def?.defName == "Orbit";
            }
            catch
            {
                isSpace = false;
            }
        }
        else if (__instance.def?.defName?.Contains("Odyssey") == true || __instance.def?.defName?.Contains("Asteroid") == true)
        {
            isSpace = true;
        }

        if (isSpace)
        {
            float baseMag = __result.magnitude;
            if (baseMag > 1f)
            {
                // Deterministic pseudo-random offset between -5f and +7f
                int hash = (__instance.ID * 9973) ^ ((__instance.def?.shortHash ?? 0) * 101);
                float radialOffset = (((hash & 0x7FFFFFFF) % 1201) / 100f) - 5f; // -5.0f to +7.0f

                // Hard ceiling limit clamps deep orbit bodies so celestial assets never regress off-screen
                float finalRadius = Mathf.Min(baseMag + radialOffset, 145f);
                __result = __result.normalized * finalRadius;
            }
        }
    }
}
