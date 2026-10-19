using HarmonyLib;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld.Planet;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Ensures WorldDrawLayer_HazeOfWar is automatically attached to the Surface PlanetLayer
/// during layer initialization across all game scenarios (new world, loaded game, test bench).
/// </summary>
[HarmonyPatch(typeof(PlanetLayer), nameof(PlanetLayer.InitializeLayer))]
public static class PlanetLayer_InitializeLayer_Patch
{
    public static void Postfix(PlanetLayer __instance)
    {
        if (__instance?.Def?.defName == "Surface")
        {
            EnsureHazeLayerAttached(__instance);
        }
    }

    public static void EnsureHazeLayerAttached(PlanetLayer layer)
    {
        if (layer?.WorldDrawLayers == null)
        {
            return;
        }

        for (int i = 0; i < layer.WorldDrawLayers.Count; i++)
        {
            if (layer.WorldDrawLayers[i] is WorldDrawLayer_HazeOfWar)
            {
                return;
            }
        }

        WorldDrawLayer_HazeOfWar hazeLayer = new();
        hazeLayer.Initialize(layer);
        layer.WorldDrawLayers.Add(hazeLayer);
    }
}
