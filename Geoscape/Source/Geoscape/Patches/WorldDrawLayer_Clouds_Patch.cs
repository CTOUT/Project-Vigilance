using System;
using System.Reflection;
using HarmonyLib;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld.Planet;
using UnityEngine;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Clamps <see cref="WorldDrawLayer_Clouds"/> material renderQueue below the sensory shroud stack
/// (<see cref="WorldDrawLayer_RadarCoverage.DimmingRenderQueue"/> = 3544) while Fog of War is active,
/// so zoomed-out globe clouds render beneath unexplored shroud and dimmed haze.
/// </summary>
[HarmonyPatch(typeof(WorldDrawLayer_Clouds), nameof(WorldDrawLayer_Clouds.Render))]
public static class WorldDrawLayer_Clouds_Patch
{
    public const int MaxCloudsRenderQueueUnderShroud = WorldDrawLayer_RadarCoverage.DimmingRenderQueue - 1;

    private static readonly FieldInfo? MaterialField =
        AccessTools.Field(typeof(WorldDrawLayer_Clouds), "material");

    private static int s_originalRenderQueue = -1;

    public static void Prefix(WorldDrawLayer_Clouds? __instance)
    {
        if (__instance == null || MaterialField == null)
        {
            return;
        }

        if (MaterialField.GetValue(__instance) is not Material mat)
        {
            return;
        }

        if (s_originalRenderQueue < 0)
        {
            s_originalRenderQueue = mat.renderQueue;
        }

        int targetQueue = ComputeTargetRenderQueue(
            s_originalRenderQueue,
            GeoscapeRules.VisibilityMode == VisibilityMode.ClassicHazeOfWar);

        if (mat.renderQueue != targetQueue)
        {
            mat.renderQueue = targetQueue;
        }
    }

    internal static int ComputeTargetRenderQueue(int originalRenderQueue, bool shroudActive)
    {
        if (!shroudActive)
        {
            return originalRenderQueue;
        }

        return Math.Min(originalRenderQueue, MaxCloudsRenderQueueUnderShroud);
    }
}

/// <summary>
/// Keeps <see cref="WorldDrawLayer_Clouds"/> subtly visible when zoomed in to the Geoscape
/// according to <see cref="GeoscapeSettings.showGeoscapeClouds"/> and
/// <see cref="GeoscapeSettings.geoscapeCloudOpacity"/>, ramping smoothly up to vanilla orbital
/// cloud opacity when zooming out.
/// </summary>
[HarmonyPatch(typeof(WorldDrawLayer_Clouds), "GetTargetOpacity")]
public static class WorldDrawLayer_Clouds_GetTargetOpacity_Patch
{
    public const float MaxSurfaceCloudOpacity = 0.75f;

    public static void Postfix(ref float __result)
    {
        GeoscapeSettings? settings = GeoscapeMod.Settings;
        if (settings == null)
        {
            return;
        }

        __result = ComputeTargetOpacity(__result, settings.showGeoscapeClouds, settings.geoscapeCloudOpacity);
    }

    internal static float ComputeTargetOpacity(float vanillaOpacity, bool showCloudsOnSurface, float surfaceCloudOpacity)
    {
        if (!showCloudsOnSurface)
        {
            return vanillaOpacity;
        }

        float clampedSurfaceOpacity = Mathf.Clamp(surfaceCloudOpacity, 0f, MaxSurfaceCloudOpacity);
        return Math.Max(vanillaOpacity, clampedSurfaceOpacity);
    }
}
