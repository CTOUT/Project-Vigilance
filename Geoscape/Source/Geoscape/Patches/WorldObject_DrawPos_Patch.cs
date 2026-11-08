using HarmonyLib;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Geoscape.Orbital;

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
        if (__instance == null)
        {
            return;
        }

        // Custom orbital entities calculate their own precise Keplerian flight positions
        if (__instance is IOrbitalEntity)
        {
            return;
        }

        bool isSpace = IsSpaceObjectOverride != null
            ? IsSpaceObjectOverride(__instance)
            : (Find.WorldGrid != null && (__instance.Tile.Layer?.Def?.isSpace == true || __instance.Tile.Layer?.Def?.defName == "Orbit"));

        if (isSpace)
        {
            float baseMag = __result.magnitude;
            if (baseMag > 1f)
            {
                // Deterministic pseudo-random offset between -5f and +7f
                int hash = (__instance.ID * 9973) ^ (__instance.def.shortHash * 101);
                float radialOffset = (((hash & 0x7FFFFFFF) % 1201) / 100f) - 5f; // -5.0f to +7.0f
                __result = __result.normalized * (baseMag + radialOffset);
            }
        }
    }
}
