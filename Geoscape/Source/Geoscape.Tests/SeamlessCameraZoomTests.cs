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
}
