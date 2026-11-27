using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape.Patches;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.Tests;

[TestClass]
[DoNotParallelize]
public class SeamlessCameraZoomTests
{
    [TestInitialize]
    public void SetUp()
    {
        SeamlessCameraZoomUtility.ResetStateForTesting();
    }

    [TestMethod]
    public void Overscroll_RequiresConfiguredNotchesPastBoundary()
    {
        // Reaching the boundary itself (not yet at boundary) must not accumulate overscroll
        Assert.IsFalse(SeamlessCameraZoomUtility.TryAdvanceMapOverscroll(
            isAtBoundary: false,
            zoomingOut: true,
            scrollDeltaY: 3f,
            requiredNotches: 2,
            now: 1.0f));

        // First overscroll notch at boundary primes without triggering
        Assert.IsFalse(SeamlessCameraZoomUtility.TryAdvanceMapOverscroll(
            isAtBoundary: true,
            zoomingOut: true,
            scrollDeltaY: 3f,
            requiredNotches: 2,
            now: 1.1f));

        // Second overscroll notch triggers transition
        Assert.IsTrue(SeamlessCameraZoomUtility.TryAdvanceMapOverscroll(
            isAtBoundary: true,
            zoomingOut: true,
            scrollDeltaY: 3f,
            requiredNotches: 2,
            now: 1.2f));
    }

    [TestMethod]
    public void Overscroll_ClampsHighDeltaSingleEventToOneNotch()
    {
        // Even a huge single scroll flick should count as at most 1 notch when 2 are required
        Assert.IsFalse(SeamlessCameraZoomUtility.TryAdvanceWorldOverscroll(
            isAtBoundary: true,
            zoomingIn: true,
            scrollDeltaY: -30f,
            requiredNotches: 2,
            now: 2.0f));

        Assert.IsTrue(SeamlessCameraZoomUtility.TryAdvanceWorldOverscroll(
            isAtBoundary: true,
            zoomingIn: true,
            scrollDeltaY: -30f,
            requiredNotches: 2,
            now: 2.1f));
    }

    [TestMethod]
    public void Overscroll_ResetsAfterDecayOrOppositeScroll()
    {
        Assert.IsFalse(SeamlessCameraZoomUtility.TryAdvanceMapOverscroll(true, true, 3f, 2, 1.0f));

        // Opposite direction resets accumulator
        Assert.IsFalse(SeamlessCameraZoomUtility.TryAdvanceMapOverscroll(true, false, -3f, 2, 1.1f));

        // Next scroll out starts from 1 again instead of triggering immediately
        Assert.IsFalse(SeamlessCameraZoomUtility.TryAdvanceMapOverscroll(true, true, 3f, 2, 1.2f));

        // Wait past decay window -> resets again
        Assert.IsFalse(SeamlessCameraZoomUtility.TryAdvanceMapOverscroll(
            true,
            true,
            3f,
            2,
            1.2f + SeamlessCameraZoomUtility.OverscrollDecaySeconds + 0.1f));
    }

    [TestMethod]
    public void Overscroll_RespectsPostTransitionCooldown()
    {
        SeamlessCameraZoomUtility.MarkTransitioned(5.0f);

        Assert.IsFalse(SeamlessCameraZoomUtility.TryAdvanceWorldOverscroll(
            isAtBoundary: true,
            zoomingIn: true,
            scrollDeltaY: -3f,
            requiredNotches: 1,
            now: 5.2f));

        Assert.IsTrue(SeamlessCameraZoomUtility.TryAdvanceWorldOverscroll(
            isAtBoundary: true,
            zoomingIn: true,
            scrollDeltaY: -3f,
            requiredNotches: 1,
            now: 5.0f + SeamlessCameraZoomUtility.TransitionCooldownSeconds + 0.05f));
    }

    [TestMethod]
    public void SelectTargetMap_ReturnsNullWhenNoMapsOrNoTileMatch()
    {
        Assert.IsNull(SeamlessCameraZoomUtility.SelectTargetMap(
            new List<Map>(),
            cursorTile: null,
            centerTile: null,
            selectedObject: null,
            SeamlessZoomTargetMode.CursorFirst));
    }

    [TestMethod]
    public void TransitionStyle_HonoursReduceShroudMotionAndAllThreeModes()
    {
        var settings = new GeoscapeSettings
        {
            seamlessZoomTransitionStyle = SeamlessZoomTransitionStyle.Traverse,
            reduceShroudMotion = false
        };
        Assert.AreEqual(SeamlessZoomTransitionStyle.Traverse, SeamlessCameraZoomUtility.ResolveEffectiveTransitionStyle(settings));

        settings.seamlessZoomTransitionStyle = SeamlessZoomTransitionStyle.Fade;
        Assert.AreEqual(SeamlessZoomTransitionStyle.Fade, SeamlessCameraZoomUtility.ResolveEffectiveTransitionStyle(settings));

        settings.reduceShroudMotion = true;
        Assert.AreEqual(SeamlessZoomTransitionStyle.Instant, SeamlessCameraZoomUtility.ResolveEffectiveTransitionStyle(settings));

        settings.seamlessZoomTransitionStyle = SeamlessZoomTransitionStyle.Traverse;
        Assert.AreEqual(SeamlessZoomTransitionStyle.Instant, SeamlessCameraZoomUtility.ResolveEffectiveTransitionStyle(settings));

        settings.reduceShroudMotion = false;
        settings.seamlessZoomTransitionStyle = SeamlessZoomTransitionStyle.Instant;
        Assert.AreEqual(SeamlessZoomTransitionStyle.Instant, SeamlessCameraZoomUtility.ResolveEffectiveTransitionStyle(settings));
    }

    [TestMethod]
    public void TraverseTransition_SwapsViewOnceAtSwapPointAndCompletesCleanly()
    {
        SeamlessCameraZoomUtility.BeginTransition(
            SeamlessZoomTransitionStyle.Traverse,
            SeamlessCameraZoomUtility.TransitionDirection.ToGeoscape,
            new PlanetTile(12),
            null,
            now: 10.0f);

        Assert.IsTrue(SeamlessCameraZoomUtility.IsTransitionActive);

        // Before swap point: active, no swap yet
        var early = SeamlessCameraZoomUtility.StepTransition(
            10.0f + SeamlessCameraZoomUtility.CloudTransitionDurationSeconds * (SeamlessCameraZoomUtility.CloudSwapPointNormalized * 0.5f));
        Assert.IsTrue(early.IsActive);
        Assert.IsFalse(early.ShouldSwapView);

        // At/past swap point: triggers view swap exactly once
        var swap = SeamlessCameraZoomUtility.StepTransition(
            10.0f + SeamlessCameraZoomUtility.CloudTransitionDurationSeconds * (SeamlessCameraZoomUtility.CloudSwapPointNormalized + 0.05f));
        Assert.IsTrue(swap.IsActive);
        Assert.IsTrue(swap.ShouldSwapView);

        // During peripheral wisp drift: still active, does not swap a second time
        var late = SeamlessCameraZoomUtility.StepTransition(
            10.0f + SeamlessCameraZoomUtility.CloudTransitionDurationSeconds * 0.65f);
        Assert.IsTrue(late.IsActive);
        Assert.IsFalse(late.ShouldSwapView);

        // Completion: clears active state
        var done = SeamlessCameraZoomUtility.StepTransition(10.0f + SeamlessCameraZoomUtility.CloudTransitionDurationSeconds + 0.01f);
        Assert.IsFalse(done.IsActive);
        Assert.IsFalse(done.ShouldSwapView);
        Assert.IsFalse(SeamlessCameraZoomUtility.IsTransitionActive);
    }

    [TestMethod]
    public void FadeTransition_SwapsViewOnceAtFadeSwapPointAndCompletesCleanly()
    {
        SeamlessCameraZoomUtility.BeginTransition(
            SeamlessZoomTransitionStyle.Fade,
            SeamlessCameraZoomUtility.TransitionDirection.ToBattlescape,
            PlanetTile.Invalid,
            null,
            now: 30.0f);

        var early = SeamlessCameraZoomUtility.StepTransition(30.0f + 0.03f);
        Assert.IsTrue(early.IsActive);
        Assert.IsFalse(early.ShouldSwapView);

        var swap = SeamlessCameraZoomUtility.StepTransition(30.0f + 0.09f);
        Assert.IsTrue(swap.IsActive);
        Assert.IsTrue(swap.ShouldSwapView);

        var done = SeamlessCameraZoomUtility.StepTransition(30.0f + SeamlessCameraZoomUtility.FadeTransitionDurationSeconds + 0.01f);
        Assert.IsFalse(done.IsActive);
        Assert.IsFalse(SeamlessCameraZoomUtility.IsTransitionActive);
    }

    [TestMethod]
    public void Overscroll_BlockedWhilePreSwapTransitionActive()
    {
        SeamlessCameraZoomUtility.BeginTransition(
            SeamlessZoomTransitionStyle.Traverse,
            SeamlessCameraZoomUtility.TransitionDirection.ToGeoscape,
            new PlanetTile(12),
            null,
            now: 20.0f);

        Assert.IsFalse(SeamlessCameraZoomUtility.TryAdvanceWorldOverscroll(
            isAtBoundary: true,
            zoomingIn: true,
            scrollDeltaY: -3f,
            requiredNotches: 1,
            now: 20.05f));
    }
}
