using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld.Planet;
using ProjectVigilance.Geoscape.Orbital;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Preserves selected orbital entities and space objects across camera altitude/layer transitions.
/// Vanilla RimWorld's WorldSelector.set_SelectedLayer clears selected objects whenever switching
/// between Surface and Orbit layers; this patch restores active orbital selections.
/// </summary>
[HarmonyPatch(typeof(WorldSelector), "set_SelectedLayer")]
public static class WorldSelector_SelectedLayer_Patch
{
    [ThreadStatic]
    private static List<WorldObject>? cachedOrbitalSelections;

    public static void Prefix(WorldSelector __instance, PlanetLayer value)
    {
        if (__instance == null || __instance.SelectedLayer == value)
        {
            return;
        }

        cachedOrbitalSelections ??= new List<WorldObject>();
        cachedOrbitalSelections.Clear();

        List<WorldObject> selected = __instance.SelectedObjects;
        if (selected == null || selected.Count == 0)
        {
            return;
        }

        for (int i = 0; i < selected.Count; i++)
        {
            WorldObject obj = selected[i];
            if (obj != null && !obj.Destroyed && obj.Spawned)
            {
                if (obj is IOrbitalEntity || obj.Tile.Layer?.Def?.isSpace == true || obj.Tile.Layer?.Def?.defName == "Orbit")
                {
                    cachedOrbitalSelections.Add(obj);
                }
            }
        }
    }

    public static void Postfix(WorldSelector __instance)
    {
        if (cachedOrbitalSelections == null || cachedOrbitalSelections.Count == 0 || __instance == null)
        {
            return;
        }

        for (int i = 0; i < cachedOrbitalSelections.Count; i++)
        {
            WorldObject obj = cachedOrbitalSelections[i];
            if (obj != null && !obj.Destroyed && obj.Spawned)
            {
                if (!__instance.IsSelected(obj))
                {
                    __instance.Select(obj, playSound: false);
                }
            }
        }

        cachedOrbitalSelections.Clear();
    }
}
