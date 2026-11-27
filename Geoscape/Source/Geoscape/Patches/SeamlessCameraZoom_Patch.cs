using System;
using System.Collections.Generic;
using HarmonyLib;
using ProjectVigilance.Core.Localization;
using ProjectVigilance.Core.UI;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Enables seamless scroll-wheel transitions between a tactical battlescape/colony map and the
/// planetary Geoscape, with configurable boundary detent cues and a "through the clouds" sweep.
/// </summary>
public static class SeamlessCameraZoomUtility
{
    public const float TransitionCooldownSeconds = 0.45f;
    public const float OverscrollDecaySeconds = 0.65f;
    public const float CloudTransitionDurationSeconds = 0.70f;
    public const int DefaultSnapRadiusTiles = 2;
    public const float MapBoundaryEpsilon = 0.35f;
    public const float WorldBoundaryEpsilon = 0.35f;
    public const float MapLandingSizeRatio = 0.92f;
    public const float WorldLandingAltitudeMultiplier = 1.06f;

    internal enum TransitionDirection
    {
        None = 0,
        ToGeoscape = 1,
        ToBattlescape = -1
    }

    internal readonly struct TransitionStepResult
    {
        public readonly bool IsActive;
        public readonly bool ShouldSwapView;
        public readonly float Progress;
        public readonly TransitionDirection Direction;

        public TransitionStepResult(bool isActive, bool shouldSwapView, float progress, TransitionDirection direction)
        {
            IsActive = isActive;
            ShouldSwapView = shouldSwapView;
            Progress = progress;
            Direction = direction;
        }
    }

    private readonly struct CloudPuffSeed
    {
        public readonly float CosAngle;
        public readonly float SinAngle;
        public readonly float DistNorm;
        public readonly float BaseRadius;
        public readonly float Shade;

        public CloudPuffSeed(float angle, float distNorm, float baseRadius, float shade)
        {
            CosAngle = Mathf.Cos(angle);
            SinAngle = Mathf.Sin(angle);
            DistNorm = distNorm;
            BaseRadius = baseRadius;
            Shade = shade;
        }
    }

    private static readonly AccessTools.FieldRef<CameraDriver, float> CameraDesiredSizeRef =
        AccessTools.FieldRefAccess<CameraDriver, float>("desiredSize");

    private static readonly AccessTools.FieldRef<WorldCameraDriver, float> WorldDesiredAltitudeRef =
        AccessTools.FieldRefAccess<WorldCameraDriver, float>("desiredAltitude");

    private static readonly CloudPuffSeed[] CloudSeeds = BuildCloudSeeds();

    private static float mapOverscrollNotches;
    private static float worldOverscrollNotches;
    private static float lastMapOverscrollTime = -10f;
    private static float lastWorldOverscrollTime = -10f;
    private static float lastTransitionTime = -10f;
    private static string? armedWorldTargetLabel;

    private static bool isTransitionActive;
    private static bool transitionSwapped;
    private static float transitionStartTime = -10f;
    private static TransitionDirection transitionDirection = TransitionDirection.None;
    private static PlanetTile pendingGeoscapeTile = PlanetTile.Invalid;
    private static Map? pendingBattlescapeMap;

    private static Texture2D? cachedCloudPuffTex;
    private static Texture2D? cachedBorderGradHorizontal;
    private static Texture2D? cachedBorderGradVertical;

    public static bool IsTransitionActive => isTransitionActive;

    internal static bool HasActiveVisualOverlay =>
        isTransitionActive || mapOverscrollNotches > 0f || worldOverscrollNotches > 0f;

    public static void ResetStateForTesting()
    {
        mapOverscrollNotches = 0f;
        worldOverscrollNotches = 0f;
        lastMapOverscrollTime = -10f;
        lastWorldOverscrollTime = -10f;
        lastTransitionTime = -10f;
        armedWorldTargetLabel = null;

        isTransitionActive = false;
        transitionSwapped = false;
        transitionStartTime = -10f;
        transitionDirection = TransitionDirection.None;
        pendingGeoscapeTile = PlanetTile.Invalid;
        pendingBattlescapeMap = null;
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
        armedWorldTargetLabel = null;
    }

    internal static bool ShouldUseCloudTransition(GeoscapeSettings? settings)
    {
        return settings != null
            && settings.seamlessZoomTransitionStyle == SeamlessZoomTransitionStyle.ThroughClouds
            && !settings.reduceShroudMotion;
    }

    internal static void BeginCloudTransition(
        TransitionDirection direction,
        PlanetTile geoscapeTile,
        Map? battlescapeMap,
        float now)
    {
        MarkTransitioned(now);
        isTransitionActive = true;
        transitionSwapped = false;
        transitionStartTime = now;
        transitionDirection = direction;
        pendingGeoscapeTile = geoscapeTile;
        pendingBattlescapeMap = battlescapeMap;
    }

    internal static TransitionStepResult StepCloudTransition(float now)
    {
        if (!isTransitionActive)
        {
            return default;
        }

        float progress = (now - transitionStartTime) / CloudTransitionDurationSeconds;
        if (progress >= 1f)
        {
            bool missedSwap = !transitionSwapped;
            TransitionDirection dir = transitionDirection;
            isTransitionActive = false;
            transitionSwapped = true;
            transitionDirection = TransitionDirection.None;
            return new TransitionStepResult(isActive: false, shouldSwapView: missedSwap, progress: 1f, direction: dir);
        }

        bool shouldSwap = false;
        if (progress >= 0.5f && !transitionSwapped)
        {
            transitionSwapped = true;
            shouldSwap = true;
        }

        return new TransitionStepResult(isActive: true, shouldSwapView: shouldSwap, progress: Mathf.Clamp01(progress), direction: transitionDirection);
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
        if (isTransitionActive || now - lastTransitionTime < TransitionCooldownSeconds)
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

        float now = Time.realtimeSinceStartup;
        bool triggered = TryAdvanceMapOverscroll(
            isAtMaxZoomOut,
            zoomingOut,
            current.delta.y,
            settings.seamlessZoomOverscrollNotches,
            now);

        if (!triggered)
        {
            if (mapOverscrollNotches > 0f && settings.seamlessZoomIndicatorStyle != SeamlessZoomIndicatorStyle.Off)
            {
                SoundDefOf.Tick_Tiny?.PlayOneShotOnCamera();
            }
            return;
        }

        PlanetTile originTile = Find.CurrentMap.Tile;
        current.Use();

        if (ShouldUseCloudTransition(settings))
        {
            SoundDefOf.Click?.PlayOneShotOnCamera();
            BeginCloudTransition(TransitionDirection.ToGeoscape, originTile, null, now);
        }
        else
        {
            MarkTransitioned(now);
            ExecuteSwitchToGeoscape(originTile);
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

        float now = Time.realtimeSinceStartup;
        bool triggered = TryAdvanceWorldOverscroll(
            isAtMinAlt,
            zoomingIn,
            current.delta.y,
            settings.seamlessZoomOverscrollNotches,
            now);

        if (!triggered && worldOverscrollNotches <= 0f)
        {
            armedWorldTargetLabel = null;
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
            armedWorldTargetLabel = null;
            return;
        }

        if (!triggered)
        {
            armedWorldTargetLabel = targetMap.Parent?.LabelCap ?? "Colony";
            if (settings.seamlessZoomIndicatorStyle != SeamlessZoomIndicatorStyle.Off)
            {
                SoundDefOf.Tick_Tiny?.PlayOneShotOnCamera();
            }
            return;
        }

        current.Use();

        if (ShouldUseCloudTransition(settings))
        {
            SoundDefOf.Click?.PlayOneShotOnCamera();
            BeginCloudTransition(TransitionDirection.ToBattlescape, PlanetTile.Invalid, targetMap, now);
        }
        else
        {
            MarkTransitioned(now);
            ExecuteSwitchToBattlescape(targetMap);
        }
    }

    internal static void DrawOverlayOnGUI()
    {
        // O(1) fast-bail when idle
        if (!HasActiveVisualOverlay)
        {
            return;
        }

        if (Current.ProgramState != ProgramState.Playing || Current.Game == null || Find.World == null)
        {
            ResetStateForTesting();
            return;
        }

        GeoscapeSettings? settings = GeoscapeMod.Settings;
        if (settings == null || !settings.enableSeamlessCameraZoom)
        {
            ResetStateForTesting();
            return;
        }

        float now = Time.realtimeSinceStartup;

        // 1. Advance and render active cloud transition
        if (isTransitionActive)
        {
            TransitionStepResult step = StepCloudTransition(now);
            if (step.ShouldSwapView)
            {
                if (step.Direction == TransitionDirection.ToGeoscape && pendingGeoscapeTile.Valid)
                {
                    ExecuteSwitchToGeoscape(pendingGeoscapeTile);
                }
                else if (step.Direction == TransitionDirection.ToBattlescape && pendingBattlescapeMap != null)
                {
                    ExecuteSwitchToBattlescape(pendingBattlescapeMap);
                }
                pendingGeoscapeTile = PlanetTile.Invalid;
                pendingBattlescapeMap = null;
            }

            if (step.IsActive && Event.current != null && Event.current.type == EventType.Repaint)
            {
                DrawCloudSweepOverlay(step.Progress, step.Direction);
            }
            return;
        }

        // 2. Render boundary detent indicator (Border Flash / Bottom Pill / Hybrid)
        bool isWorld = WorldRendererUtility.WorldRendered;
        float notches = isWorld ? worldOverscrollNotches : mapOverscrollNotches;
        float lastTime = isWorld ? lastWorldOverscrollTime : lastMapOverscrollTime;
        float elapsed = now - lastTime;

        if (notches <= 0f || elapsed > OverscrollDecaySeconds)
        {
            if (isWorld)
            {
                worldOverscrollNotches = 0f;
                armedWorldTargetLabel = null;
            }
            else
            {
                mapOverscrollNotches = 0f;
            }
            return;
        }

        if (Event.current == null || Event.current.type != EventType.Repaint)
        {
            return;
        }

        SeamlessZoomIndicatorStyle style = settings.seamlessZoomIndicatorStyle;
        if (style == SeamlessZoomIndicatorStyle.Off)
        {
            return;
        }

        float remainPct = Mathf.Clamp01(1f - elapsed / OverscrollDecaySeconds);
        Color accent = settings.EffectiveRadarColor;

        using (new VigilanceUiWidgets.ImguiStateScope())
        {
            if (style is SeamlessZoomIndicatorStyle.BorderFlash or SeamlessZoomIndicatorStyle.Hybrid)
            {
                float alpha = settings.reduceShroudMotion ? 0.55f : (0.20f + remainPct * 0.72f);
                DrawBorderFlash(accent, alpha);
            }

            if (style is SeamlessZoomIndicatorStyle.BottomPill or SeamlessZoomIndicatorStyle.Hybrid)
            {
                string prompt = isWorld
                    ? VigilanceLoc.Tr("PV_SeamlessZoom_PromptToMap", $"Scroll in again → {armedWorldTargetLabel ?? "Colony"}", armedWorldTargetLabel ?? "Colony")
                    : VigilanceLoc.Tr("PV_SeamlessZoom_PromptToGeoscape", "Scroll out again → Geoscape");

                int required = Mathf.Clamp(settings.seamlessZoomOverscrollNotches, 1, 5);
                int filled = Mathf.Clamp(Mathf.FloorToInt(notches + 0.01f), 1, required);
                DrawBottomPromptPill(prompt, filled, required, remainPct, accent);
            }
        }
    }

    private static void ExecuteSwitchToGeoscape(PlanetTile originTile)
    {
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

    private static void ExecuteSwitchToBattlescape(Map targetMap)
    {
        if (targetMap == null || Find.Maps == null || !Find.Maps.Contains(targetMap))
        {
            return;
        }

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

    private static void DrawBorderFlash(Color accent, float alpha)
    {
        EnsureBorderGradientTextures();
        float w = UI.screenWidth;
        float h = UI.screenHeight;
        const float glowDepth = 26f;
        const float rimThickness = 3f;

        // Soft inward border glow on all 4 viewport edges
        GUI.color = new Color(accent.r, accent.g, accent.b, alpha * 0.55f);
        if (cachedBorderGradVertical != null)
        {
            GUI.DrawTexture(new Rect(0f, 0f, w, glowDepth), cachedBorderGradVertical);
            GUI.DrawTextureWithTexCoords(new Rect(0f, h - glowDepth, w, glowDepth), cachedBorderGradVertical, new Rect(0f, 1f, 1f, -1f));
        }
        if (cachedBorderGradHorizontal != null)
        {
            GUI.DrawTexture(new Rect(0f, 0f, glowDepth, h), cachedBorderGradHorizontal);
            GUI.DrawTextureWithTexCoords(new Rect(w - glowDepth, 0f, glowDepth, h), cachedBorderGradHorizontal, new Rect(1f, 0f, -1f, 1f));
        }

        // Crisp 3px inner rim for instant peripheral visibility
        GUI.color = new Color(
            Mathf.Lerp(accent.r, 1f, 0.35f),
            Mathf.Lerp(accent.g, 1f, 0.35f),
            Mathf.Lerp(accent.b, 1f, 0.35f),
            alpha * 0.90f);
        GUI.DrawTexture(new Rect(0f, 0f, w, rimThickness), BaseContent.WhiteTex);
        GUI.DrawTexture(new Rect(0f, h - rimThickness, w, rimThickness), BaseContent.WhiteTex);
        GUI.DrawTexture(new Rect(0f, 0f, rimThickness, h), BaseContent.WhiteTex);
        GUI.DrawTexture(new Rect(w - rimThickness, 0f, rimThickness, h), BaseContent.WhiteTex);
    }

    private static void DrawBottomPromptPill(string label, int filledNotches, int totalNotches, float remainPct, Color accent)
    {
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        Text.WordWrap = false;

        float labelWidth = Text.CalcSize(label).x;
        float pipsWidth = (totalNotches * 10f) + Math.Max(0, totalNotches - 1) * 4f;
        float pillW = Mathf.Max(230f, labelWidth + pipsWidth + 36f);
        float pillH = 38f;
        float x = (UI.screenWidth - pillW) * 0.5f;
        float y = UI.screenHeight - 96f;

        Rect pillRect = new(x, y, pillW, pillH);
        Widgets.DrawBoxSolid(pillRect, new Color(0.08f, 0.10f, 0.13f, 0.92f));

        GUI.color = new Color(accent.r, accent.g, accent.b, 0.72f);
        Widgets.DrawBox(pillRect, 1);

        GUI.color = new Color(0.95f, 0.97f, 0.99f, 1f);
        Widgets.Label(new Rect(x + 12f, y + 4f, labelWidth + 8f, 22f), label);

        // Segmented notch pips
        float pipX = pillRect.xMax - 12f - pipsWidth;
        float pipY = y + 11f;
        for (int i = 0; i < totalNotches; i++)
        {
            Rect pipRect = new(pipX + i * 14f, pipY, 8f, 8f);
            GUI.color = i < filledNotches ? accent : new Color(0.35f, 0.40f, 0.46f, 0.85f);
            GUI.DrawTexture(pipRect, BaseContent.WhiteTex);
        }

        // Shrinking decay timer bar
        Rect trackRect = new(x + 12f, pillRect.yMax - 7f, pillW - 24f, 3f);
        GUI.color = new Color(0.25f, 0.30f, 0.36f, 0.85f);
        GUI.DrawTexture(trackRect, BaseContent.WhiteTex);

        Rect fillRect = new(trackRect.x, trackRect.y, trackRect.width * remainPct, trackRect.height);
        GUI.color = accent;
        GUI.DrawTexture(fillRect, BaseContent.WhiteTex);
    }

    private static void DrawCloudSweepOverlay(float progress, TransitionDirection direction)
    {
        EnsureCloudPuffTexture();
        if (cachedCloudPuffTex == null)
        {
            return;
        }

        float w = UI.screenWidth;
        float h = UI.screenHeight;
        // Smooth bell curve so clouds roll in gently, cover the midpoint swap, and part smoothly
        float sinCurve = Mathf.Sin(progress * Mathf.PI);
        float density = sinCurve * sinCurve;
        float ease = progress * progress * (3f - 2f * progress);

        using (new VigilanceUiWidgets.ImguiStateScope())
        {
            // Muted atmospheric twilight cloud-deck veil (avoids harsh white flashbang glare)
            GUI.color = new Color(0.20f, 0.25f, 0.31f, density * 0.62f);
            GUI.DrawTexture(new Rect(0f, 0f, w, h), BaseContent.WhiteTex);

            float cx = w * 0.5f;
            float cy = h * 0.5f;
            float maxDim = Mathf.Sqrt(w * w + h * h) * 0.56f;
            bool ascending = direction == TransitionDirection.ToGeoscape;

            for (int i = 0; i < CloudSeeds.Length; i++)
            {
                CloudPuffSeed seed = CloudSeeds[i];
                float radialShift = ascending
                    ? (0.28f + ease * 0.92f)
                    : (1.20f - ease * 0.92f);
                float rDist = seed.DistNorm * maxDim * radialShift;
                float px = cx + seed.CosAngle * rDist;
                float py = cy + seed.SinAngle * rDist * 0.72f;
                float radius = seed.BaseRadius * (w / 960f) * (0.95f + density * 0.65f);

                // Layered cool-slate cloud puffs: darker underside puffs + muted silver-grey mist
                float r = seed.Shade * 0.92f;
                float g = seed.Shade * 0.97f;
                float b = Mathf.Min(1f, seed.Shade * 1.04f);
                GUI.color = new Color(r, g, b, density * 0.34f);
                GUI.DrawTexture(new Rect(px - radius, py - radius, radius * 2f, radius * 2f), cachedCloudPuffTex);
            }
        }
    }

    private static CloudPuffSeed[] BuildCloudSeeds()
    {
        const int count = 26;
        var seeds = new CloudPuffSeed[count];
        for (int i = 0; i < count; i++)
        {
            float angle = (i / (float)count) * Mathf.PI * 2f + (i % 3) * 0.21f;
            float dist = 0.14f + ((i * 37) % 100) / 145f;
            float radius = 140f + ((i * 29) % 110);
            // Alternate deeper shadow-base cloud puffs and softer silver-slate upper mist
            float shade = (i % 3 == 0)
                ? (0.22f + (i % 4) * 0.03f)
                : (0.44f + (i % 4) * 0.04f);
            seeds[i] = new CloudPuffSeed(angle, dist, radius, shade);
        }
        return seeds;
    }

    private static void EnsureCloudPuffTexture()
    {
        if (cachedCloudPuffTex != null)
        {
            return;
        }

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color32[size * size];
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);
                // Soft wispy cloud edge modulation
                float wisp = 1f + 0.10f * Mathf.Sin(angle * 3f) + 0.06f * Mathf.Cos(angle * 5f + 1.1f);
                float norm = Mathf.Clamp01(dist / wisp);
                float alpha = Mathf.Pow(1f - norm, 1.85f);
                byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        cachedCloudPuffTex = tex;
    }

    private static void EnsureBorderGradientTextures()
    {
        if (cachedBorderGradVertical != null && cachedBorderGradHorizontal != null)
        {
            return;
        }

        const int len = 32;
        var vert = new Texture2D(1, len, TextureFormat.RGBA32, mipChain: false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var horiz = new Texture2D(len, 1, TextureFormat.RGBA32, mipChain: false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var vertPixels = new Color32[len];
        var horizPixels = new Color32[len];
        for (int i = 0; i < len; i++)
        {
            // Unity Texture2D y=0 is bottom, so top-edge fade puts highest alpha at y = len - 1
            float tTop = i / (float)(len - 1);
            float alphaTop = tTop * tTop;
            vertPixels[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alphaTop * 255f));

            // Left-edge fade puts highest alpha at x = 0
            float tLeft = 1f - tTop;
            float alphaLeft = tLeft * tLeft;
            horizPixels[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alphaLeft * 255f));
        }

        vert.SetPixels32(vertPixels);
        vert.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        horiz.SetPixels32(horizPixels);
        horiz.Apply(updateMipmaps: false, makeNoLongerReadable: true);

        cachedBorderGradVertical = vert;
        cachedBorderGradHorizontal = horiz;
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

[HarmonyPatch(typeof(UIRoot_Play), nameof(UIRoot_Play.UIRootOnGUI))]
public static class UIRoot_Play_SeamlessZoomOverlay_Patch
{
    public static void Postfix()
    {
        SeamlessCameraZoomUtility.DrawOverlayOnGUI();
    }
}
