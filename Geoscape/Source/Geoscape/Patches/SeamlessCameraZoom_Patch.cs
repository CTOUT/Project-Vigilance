using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Enables seamless scroll-wheel transitions between a tactical battlescape/colony map and the
/// planetary Geoscape. Requires deliberate overscroll past the zoom boundary plus a cooldown to
/// prevent accidental view switches or bounce loops.
/// </summary>
public static class SeamlessCameraZoomUtility
{
    public const float TransitionCooldownSeconds = 0.45f;
    public const float OverscrollDecaySeconds = 0.65f;
    public const int DefaultSnapRadiusTiles = 2;
    public const float MapBoundaryEpsilon = 0.35f;
    public const float WorldBoundaryEpsilon = 0.35f;
    public const float MapLandingSizeRatio = 0.92f;
    public const float WorldLandingAltitudeMultiplier = 1.06f;

    private static readonly AccessTools.FieldRef<CameraDriver, float> CameraDesiredSizeRef =
        AccessTools.FieldRefAccess<CameraDriver, float>("desiredSize");

    private static readonly AccessTools.FieldRef<WorldCameraDriver, float> WorldDesiredAltitudeRef =
        AccessTools.FieldRefAccess<WorldCameraDriver, float>("desiredAltitude");

    private static float mapOverscrollNotches;
    private static float worldOverscrollNotches;
    private static float lastMapOverscrollTime = -10f;
    private static float lastWorldOverscrollTime = -10f;
    private static float lastTransitionTime = -10f;

    public static void ResetStateForTesting()
    {
        mapOverscrollNotches = 0f;
        worldOverscrollNotches = 0f;
        lastMapOverscrollTime = -10f;
        lastWorldOverscrollTime = -10f;
        lastTransitionTime = -10f;
    }

    public static bool TryAdvanceMapOverscroll(bool isAtBoundary, bool zoomingOut, float scrollDeltaY, int requiredNotches, float now)
    {
        return EvaluateBoundaryOverscroll(
            isAtBoundary,
            zoomingOut,
            scrollDeltaY,
            requiredNotches,
            now,
            ref mapOverscrollNotches,
            ref lastMapOverscrollTime);
    }

    public static bool TryAdvanceWorldOverscroll(bool isAtBoundary, bool zoomingIn, float scrollDeltaY, int requiredNotches, float now)
    {
        return EvaluateBoundaryOverscroll(
            isAtBoundary,
            zoomingIn,
            scrollDeltaY,
            requiredNotches,
            now,
            ref worldOverscrollNotches,
            ref lastWorldOverscrollTime);
    }

    public static void MarkTransitioned(float now)
    {
        lastTransitionTime = now;
        mapOverscrollNotches = 0f;
        worldOverscrollNotches = 0f;
    }

    internal static bool EvaluateBoundaryOverscroll(
        bool isAtBoundary,
        bool isDirectionMatching,
        float scrollDeltaY,
        int requiredNotches,
        float now,
        ref float accumulator,
        ref float lastOverscrollTime)
    {
        if (now - lastTransitionTime < TransitionCooldownSeconds)
        {
            accumulator = 0f;
            return false;
        }

        if (!isAtBoundary || !isDirectionMatching)
        {
            accumulator = 0f;
            return false;
        }

        if (now - lastOverscrollTime > OverscrollDecaySeconds)
        {
            accumulator = 0f;
        }

        lastOverscrollTime = now;
        // Clamp each IMGUI scroll event to at most 1 notch so fast flicks cannot bypass the overscroll guard.
        float notchStep = Mathf.Clamp(Mathf.Abs(scrollDeltaY) / 3f, 0.5f, 1.0f);
        accumulator += notchStep;

        int clampedRequired = Mathf.Clamp(requiredNotches, 1, 5);
        if (accumulator >= clampedRequired)
        {
            accumulator = 0f;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Selects the most appropriate loaded map for a Geoscape zoom-in transition.
    /// In CursorFirst mode, prefers the map nearest the mouse cursor within snap radius, falling back
    /// to screen centre when the cursor is not over an active map.
    /// </summary>
    public static Map? SelectTargetMap(
        IReadOnlyList<Map>? maps,
        PlanetTile? cursorTile,
        PlanetTile? centerTile,
        WorldObject? selectedObject,
        SeamlessZoomTargetMode targetMode,
        int snapRadiusTiles = DefaultSnapRadiusTiles)
    {
        if (maps == null || maps.Count == 0)
        {
            return null;
        }

        if (targetMode == SeamlessZoomTargetMode.CursorFirst)
        {
            Map? cursorMatch = FindBestMapNearTile(cursorTile, maps, selectedObject, snapRadiusTiles);
            if (cursorMatch != null)
            {
                return cursorMatch;
            }
        }

        return FindBestMapNearTile(centerTile, maps, selectedObject, snapRadiusTiles);
    }

    private static Map? FindBestMapNearTile(
        PlanetTile? referenceTile,
        IReadOnlyList<Map> maps,
        WorldObject? selectedObject,
        int snapRadiusTiles)
    {
        if (referenceTile == null || !referenceTile.Value.Valid)
        {
            return null;
        }

        PlanetTile target = referenceTile.Value;
        Map? bestMap = null;
        int bestDistance = int.MaxValue;
        bool bestIsSelected = false;

        for (int i = 0; i < maps.Count; i++)
        {
            Map map = maps[i];
            if (map == null || !map.Tile.Valid || map.Tile.Layer != target.Layer)
            {
                continue;
            }

            int dist = Find.WorldGrid != null
                ? Mathf.RoundToInt(Find.WorldGrid.ApproxDistanceInTiles(target, map.Tile))
                : (target == map.Tile ? 0 : int.MaxValue);

            if (dist > snapRadiusTiles)
            {
                continue;
            }

            bool isSelected = selectedObject != null && map.Parent == selectedObject;
            if (dist < bestDistance || (dist == bestDistance && isSelected && !bestIsSelected))
            {
                bestDistance = dist;
                bestMap = map;
                bestIsSelected = isSelected;
            }
        }

        return bestMap;
    }

    internal static bool IsScrollBlockedByUI()
    {
        if (LongEventHandler.AnyEventNowOrWaiting)
        {
            return true;
        }

        if (Find.WindowStack == null)
        {
            return false;
        }

        return Find.WindowStack.WindowsPreventCameraMotion
            || Find.WindowStack.GetWindowAt(UI.MousePositionOnUIInverted) != null;
    }

    internal static void HandleMapScroll(CameraDriver driver)
    {
        GeoscapeSettings? settings = GeoscapeMod.Settings;
        if (settings == null || !settings.enableSeamlessCameraZoom)
        {
            return;
        }

        if (Current.ProgramState != ProgramState.Playing || Current.Game == null || Find.World == null)
        {
            return;
        }

        if (WorldRendererUtility.WorldRendered || Find.CurrentMap == null || !Find.CurrentMap.Tile.Valid)
        {
            return;
        }

        Event? current = Event.current;
        if (current == null || current.type != EventType.ScrollWheel)
        {
            return;
        }

        if (IsScrollBlockedByUI())
        {
            return;
        }

        float maxSize = driver.config?.sizeRange.max ?? 60f;
        float desiredSize = CameraDesiredSizeRef(driver);
        bool isAtMaxZoomOut = driver.RootSize >= maxSize - MapBoundaryEpsilon
            && desiredSize >= maxSize - MapBoundaryEpsilon;
        bool zoomingOut = current.delta.y > 0f;

        if (!TryAdvanceMapOverscroll(
                isAtMaxZoomOut,
                zoomingOut,
                current.delta.y,
                settings.seamlessZoomOverscrollNotches,
                Time.realtimeSinceStartup))
        {
            return;
        }

        PlanetTile originTile = Find.CurrentMap.Tile;
        current.Use();
        MarkTransitioned(Time.realtimeSinceStartup);

        CameraJumper.TryShowWorld();
        WorldCameraDriver? worldCam = Find.WorldCameraDriver;
        if (worldCam != null)
        {
            worldCam.JumpTo(originTile);
            float landingAltitude = Mathf.Max(
                WorldCameraDriver.MinAltitude,
                WorldCameraDriver.MinAltitude * WorldLandingAltitudeMultiplier);
            worldCam.altitude = landingAltitude;
            WorldDesiredAltitudeRef(worldCam) = landingAltitude;
        }
    }

    internal static void HandleWorldScroll(WorldCameraDriver driver)
    {
        GeoscapeSettings? settings = GeoscapeMod.Settings;
        if (settings == null || !settings.enableSeamlessCameraZoom)
        {
            return;
        }

        if (Current.ProgramState != ProgramState.Playing || Current.Game == null || Find.World == null)
        {
            return;
        }

        if (!WorldRendererUtility.WorldRendered || Find.Maps == null || Find.Maps.Count == 0)
        {
            return;
        }

        Event? current = Event.current;
        if (current == null || current.type != EventType.ScrollWheel)
        {
            return;
        }

        if (IsScrollBlockedByUI())
        {
            return;
        }

        float minAlt = WorldCameraDriver.MinAltitude;
        float desiredAlt = WorldDesiredAltitudeRef(driver);
        bool isAtMinAlt = driver.altitude <= minAlt + WorldBoundaryEpsilon
            && desiredAlt <= minAlt + WorldBoundaryEpsilon;
        bool zoomingIn = current.delta.y < 0f;

        if (!TryAdvanceWorldOverscroll(
                isAtMinAlt,
                zoomingIn,
                current.delta.y,
                settings.seamlessZoomOverscrollNotches,
                Time.realtimeSinceStartup))
        {
            return;
        }

        PlanetTile cursorTile = GenWorld.MouseTile(true);
        PlanetTile centerTile = GenWorld.TileAt(new Vector2(UI.screenWidth * 0.5f, UI.screenHeight * 0.5f), true);
        Map? targetMap = SelectTargetMap(
            Find.Maps,
            cursorTile,
            centerTile,
            Find.WorldSelector?.SingleSelectedObject,
            settings.seamlessZoomTargetMode);

        if (targetMap == null)
        {
            worldOverscrollNotches = 0f;
            return;
        }

        current.Use();
        MarkTransitioned(Time.realtimeSinceStartup);

        CameraJumper.TryHideWorld();
        Current.Game.CurrentMap = targetMap;

        CameraDriver? mapCam = Find.CameraDriver;
        if (mapCam != null)
        {
            float maxSize = mapCam.config?.sizeRange.max ?? 60f;
            float minSize = mapCam.config?.sizeRange.min ?? 11f;
            float landingSize = Mathf.Clamp(maxSize * MapLandingSizeRatio, minSize, maxSize);
            mapCam.SetRootPosAndSize(targetMap.Center.ToVector3Shifted(), landingSize);
            CameraDesiredSizeRef(mapCam) = landingSize;
        }
    }
}

[HarmonyPatch(typeof(CameraDriver), nameof(CameraDriver.CameraDriverOnGUI))]
public static class CameraDriver_SeamlessZoomOut_Patch
{
    public static void Prefix(CameraDriver __instance)
    {
        SeamlessCameraZoomUtility.HandleMapScroll(__instance);
    }
}

[HarmonyPatch(typeof(WorldCameraDriver), nameof(WorldCameraDriver.WorldCameraDriverOnGUI))]
public static class WorldCameraDriver_SeamlessZoomIn_Patch
{
    public static void Prefix(WorldCameraDriver __instance)
    {
        SeamlessCameraZoomUtility.HandleWorldScroll(__instance);
    }
}
