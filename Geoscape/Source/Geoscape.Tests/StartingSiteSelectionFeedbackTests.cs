using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape;
using ProjectVigilance.Geoscape.Patches;
using ProjectVigilance.Geoscape.Rules;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld.Planet;
using Verse;

namespace Geoscape.Tests;

[TestClass]
[DoNotParallelize]
public sealed class StartingSiteSelectionFeedbackTests
{
    [TestInitialize]
    public void Init()
    {
        GeoscapeTestHarness.ResetAllStaticState();
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.FogOfWar;
    }

    [TestCleanup]
    public void Cleanup() => GeoscapeTestHarness.ResetAllStaticState();

    [TestMethod]
    [DataRow(VisibilityMode.FogOfWar, false, true, true)]
    [DataRow(VisibilityMode.FogOfWar, true, true, false)]
    [DataRow(VisibilityMode.PlanetarySurveillance, false, true, false)]
    [DataRow(VisibilityMode.Omniscient, false, true, false)]
    [DataRow(VisibilityMode.FogOfWar, false, false, false)]
    public void ShouldMaskRejectionReason_ReturnsExpected(
        VisibilityMode mode,
        bool explored,
        bool validTile,
        bool expected)
    {
        GeoscapeMod.Settings.visibilityMode = mode;
        SensoryManager.IsExploredOverride = _ => explored;
        PlanetTile tile = validTile ? new(100) : PlanetTile.Invalid;

        Assert.AreEqual(expected, StartingSiteFeedbackUtility.ShouldMaskRejectionReason(tile));
    }

    [TestMethod]
    [DataRow(VisibilityMode.FogOfWar, false, true, false, "Cannot land on ocean.", true)]
    [DataRow(VisibilityMode.FogOfWar, false, true, false, "Cannot land on impassable mountains.", true)]
    [DataRow(VisibilityMode.FogOfWar, false, true, false, "", true)]
    [DataRow(VisibilityMode.FogOfWar, true, true, false, "Cannot land on ocean.", false)]
    [DataRow(VisibilityMode.PlanetarySurveillance, false, true, false, "Cannot land on impassable mountains.", false)]
    [DataRow(VisibilityMode.Omniscient, false, true, false, "Cannot land on ocean.", false)]
    [DataRow(VisibilityMode.FogOfWar, false, false, false, "Must select a starting site.", false)]
    [DataRow(VisibilityMode.FogOfWar, false, true, true, "", false)]
    public void TileFinder_IsValidTile_Patch_MasksOnlyUnexploredUnviableTilesUnderFog(
        VisibilityMode mode,
        bool explored,
        bool validTile,
        bool isValidResult,
        string initialReason,
        bool expectMasked)
    {
        GeoscapeMod.Settings.visibilityMode = mode;
        SensoryManager.IsExploredOverride = _ => explored;
        PlanetTile tile = validTile ? new(42) : PlanetTile.Invalid;
        StringBuilder reason = new(initialReason);

        StartingSiteSelectionFeedback_Patch.TileFinder_IsValidTileForNewSettlement_Patch.Postfix(
            tile, reason, __result: isValidResult);

        string expected = expectMasked
            ? StartingSiteFeedbackUtility.AmbiguousLandingRejectionMessage
            : initialReason;
        Assert.AreEqual(expected, reason.ToString());
    }

    [TestMethod]
    public void TileFinder_IsValidTile_Patch_NullReason_DoesNotThrow()
    {
        SensoryManager.IsExploredOverride = _ => false;
        StartingSiteSelectionFeedback_Patch.TileFinder_IsValidTileForNewSettlement_Patch.Postfix(
            new(42), null!, __result: false);
    }

    [TestMethod]
    [DataRow(VisibilityMode.FogOfWar, false, true, true)]
    [DataRow(VisibilityMode.FogOfWar, true, true, false)]
    [DataRow(VisibilityMode.PlanetarySurveillance, false, true, false)]
    [DataRow(VisibilityMode.Omniscient, false, true, false)]
    [DataRow(VisibilityMode.FogOfWar, false, false, false)]
    public void ShouldSuppressHazardConfirmation_ReturnsExpected(
        VisibilityMode mode,
        bool explored,
        bool validTile,
        bool expected)
    {
        GeoscapeMod.Settings.visibilityMode = mode;
        SensoryManager.IsExploredOverride = _ => explored;
        PlanetTile tile = validTile ? new(100) : PlanetTile.Invalid;

        Assert.AreEqual(expected, StartingSiteFeedbackUtility.ShouldSuppressHazardConfirmation(tile));
    }

    [TestMethod]
    [DataRow(VisibilityMode.FogOfWar, false, 0)]
    [DataRow(VisibilityMode.FogOfWar, true, 1)]
    [DataRow(VisibilityMode.PlanetarySurveillance, false, 1)]
    public void GetConfirmationDescriptions_Patch_FiltersDescriptions(
        VisibilityMode mode,
        bool explored,
        int expectedCount)
    {
        GeoscapeMod.Settings.visibilityMode = mode;
        SensoryManager.IsExploredOverride = _ => explored;
        IEnumerable<TaggedString> descriptions = new List<TaggedString>
        {
            new("This tile is 65% polluted.")
        };

        StartingSiteSelectionFeedback_Patch.SettlementProximityGoodwillUtility_GetConfirmationDescriptions_Patch.Postfix(
            new(55), ref descriptions);

        Assert.AreEqual(expectedCount, descriptions.Count());
    }

    [TestMethod]
    [DataRow(VisibilityMode.FogOfWar, false, true, false, true)]
    [DataRow(VisibilityMode.FogOfWar, true, true, true, false)]
    [DataRow(VisibilityMode.PlanetarySurveillance, false, true, true, false)]
    [DataRow(VisibilityMode.Omniscient, false, true, true, false)]
    [DataRow(VisibilityMode.FogOfWar, false, false, true, false)]
    public void CheckConfirmSettle_Patch_ControlsConfirmationDialogFlow(
        VisibilityMode mode,
        bool explored,
        bool validTile,
        bool expectedPrefixResult,
        bool expectedInvoked)
    {
        GeoscapeMod.Settings.visibilityMode = mode;
        SensoryManager.IsExploredOverride = _ => explored;
        PlanetTile tile = validTile ? new(77) : PlanetTile.Invalid;
        bool settleActionInvoked = false;

        bool prefixResult = StartingSiteSelectionFeedback_Patch.SettlementProximityGoodwillUtility_CheckConfirmSettle_Patch.Prefix(
            tile, () => settleActionInvoked = true);

        Assert.AreEqual(expectedPrefixResult, prefixResult);
        Assert.AreEqual(expectedInvoked, settleActionInvoked);
    }

    [TestMethod]
    [DataRow(VisibilityMode.FogOfWar, false, 0)]
    [DataRow(VisibilityMode.FogOfWar, true, 1)]
    [DataRow(VisibilityMode.PlanetarySurveillance, false, 1)]
    public void GetConfirmationEffects_Patch_FiltersEffects(
        VisibilityMode mode,
        bool explored,
        int expectedCount)
    {
        GeoscapeMod.Settings.visibilityMode = mode;
        SensoryManager.IsExploredOverride = _ => explored;
        IEnumerable<string> effects = new List<string>
        {
            "-15 goodwill with Outlanders for settling too close"
        };

        StartingSiteSelectionFeedback_Patch.SettlementProximityGoodwillUtility_GetConfirmationEffects_Patch.Postfix(
            new(88), ref effects);

        Assert.AreEqual(expectedCount, effects.Count());
    }

    [TestMethod]
    public void Harmony_PatchAll_EntireGeoscapeAssembly_AppliesWithoutContractException()
    {
        string id = $"test.projectvigilance.entireassembly.{Guid.NewGuid():N}";
        Harmony harmony = new(id);
        try
        {
            harmony.PatchAll(typeof(GeoscapeMod).Assembly);
            Assert.IsTrue(harmony.GetPatchedMethods().Any(), "PatchAll must resolve and patch RimWorld targets.");
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }

    [TestMethod]
    public void SensoryManager_IsTileIdExplored_NegativeTileId_ReturnsFalseWithoutThrowing()
    {
        SensoryManager manager = (SensoryManager)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(SensoryManager));
        Assert.IsFalse(manager.IsTileIdExplored(-1), "Negative tile IDs must safely evaluate as unexplored.");
    }

    [TestMethod]
    public void StartingSiteFeedbackUtility_IsTileExplored_InvalidTile_ReturnsFalse()
    {
        Assert.IsFalse(StartingSiteFeedbackUtility.IsTileExplored(PlanetTile.Invalid), "Invalid tile must always evaluate as unexplored.");
    }

    [TestMethod]
    public void EnsureStartingTileAssigned_WhenNotEntry_ExitsSafelyWithoutThrowing()
    {
        CampaignWizardPageUtility.EnsureStartingTileAssigned();
    }
}
