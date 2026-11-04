using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    private VisibilityMode originalMode;

    [TestInitialize]
    public void Init()
    {
        GeoscapeCampaignRules.ResetStaticDataForTesting();
        originalMode = GeoscapeMod.Settings.visibilityMode;
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.FogOfWar;
        SensoryManager.IsExploredOverride = null;
        SensoryManager.Instance = null;
    }

    [TestCleanup]
    public void Cleanup()
    {
        GeoscapeMod.Settings.visibilityMode = originalMode;
        SensoryManager.IsExploredOverride = null;
        SensoryManager.Instance = null;
        GeoscapeCampaignRules.ResetStaticDataForTesting();
    }

    // =========================================================================
    // 1. Ambiguous Telemetry Rejection Tests (Requirement 1)
    // =========================================================================

    [TestMethod]
    public void ShouldMaskRejectionReason_FogOfWar_UnexploredTile_ReturnsTrue()
    {
        PlanetTile tile = new(100);
        SensoryManager.IsExploredOverride = _ => false;

        bool shouldMask = StartingSiteFeedbackUtility.ShouldMaskRejectionReason(tile);

        Assert.IsTrue(shouldMask, "Unexplored tile under Fog of War must mask explicit terrain rejection.");
    }

    [TestMethod]
    public void ShouldMaskRejectionReason_FogOfWar_ExploredTile_ReturnsFalse()
    {
        PlanetTile tile = new(100);
        SensoryManager.IsExploredOverride = _ => true;

        bool shouldMask = StartingSiteFeedbackUtility.ShouldMaskRejectionReason(tile);

        Assert.IsFalse(shouldMask, "Explored tile under Fog of War must reveal standard vanilla rejection.");
    }

    [TestMethod]
    public void ShouldMaskRejectionReason_PlanetarySurveillance_UnexploredTile_ReturnsFalse()
    {
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.PlanetarySurveillance;
        PlanetTile tile = new(100);
        SensoryManager.IsExploredOverride = _ => false;

        bool shouldMask = StartingSiteFeedbackUtility.ShouldMaskRejectionReason(tile);

        Assert.IsFalse(shouldMask, "Planetary Surveillance has orbital cartography; rejection must be explicit.");
    }

    [TestMethod]
    public void ShouldMaskRejectionReason_Omniscient_UnexploredTile_ReturnsFalse()
    {
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.Omniscient;
        PlanetTile tile = new(100);
        SensoryManager.IsExploredOverride = _ => false;

        bool shouldMask = StartingSiteFeedbackUtility.ShouldMaskRejectionReason(tile);

        Assert.IsFalse(shouldMask, "Omniscient visibility must preserve vanilla rejection feedback.");
    }

    [TestMethod]
    public void TileFinder_IsValidTile_Patch_FogOfWar_Unexplored_ReplacesOceanRejection()
    {
        PlanetTile tile = new(42);
        SensoryManager.IsExploredOverride = _ => false;
        StringBuilder reason = new("Cannot land on ocean.");

        StartingSiteSelectionFeedback_Patch.TileFinder_IsValidTileForNewSettlement_Patch.Postfix(tile, reason, __result: false);

        Assert.AreEqual(
            StartingSiteFeedbackUtility.AmbiguousLandingRejectionMessage,
            reason.ToString(),
            "Explicit ocean rejection must be replaced with ambiguous telemetry under Fog of War.");
    }

    [TestMethod]
    public void TileFinder_IsValidTile_Patch_FogOfWar_Unexplored_ReplacesMountainsRejection()
    {
        PlanetTile tile = new(43);
        SensoryManager.IsExploredOverride = _ => false;
        StringBuilder reason = new("Cannot land on impassable mountains.");

        StartingSiteSelectionFeedback_Patch.TileFinder_IsValidTileForNewSettlement_Patch.Postfix(tile, reason, __result: false);

        Assert.AreEqual(
            StartingSiteFeedbackUtility.AmbiguousLandingRejectionMessage,
            reason.ToString(),
            "Explicit impassable mountains rejection must be replaced with ambiguous telemetry under Fog of War.");
    }

    [TestMethod]
    public void TileFinder_IsValidTile_Patch_FogOfWar_Explored_PreservesVanillaRejection()
    {
        PlanetTile tile = new(42);
        SensoryManager.IsExploredOverride = _ => true;
        const string vanillaReason = "Cannot land on ocean.";
        StringBuilder reason = new(vanillaReason);

        StartingSiteSelectionFeedback_Patch.TileFinder_IsValidTileForNewSettlement_Patch.Postfix(tile, reason, __result: false);

        Assert.AreEqual(
            vanillaReason,
            reason.ToString(),
            "Explored sector under Fog of War must keep standard vanilla rejection message.");
    }

    [TestMethod]
    public void TileFinder_IsValidTile_Patch_PlanetarySurveillance_PreservesVanillaRejection()
    {
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.PlanetarySurveillance;
        PlanetTile tile = new(42);
        SensoryManager.IsExploredOverride = _ => false;
        const string vanillaReason = "Cannot land on impassable mountains.";
        StringBuilder reason = new(vanillaReason);

        StartingSiteSelectionFeedback_Patch.TileFinder_IsValidTileForNewSettlement_Patch.Postfix(tile, reason, __result: false);

        Assert.AreEqual(
            vanillaReason,
            reason.ToString(),
            "Planetary Surveillance must keep standard vanilla rejection message.");
    }

    [TestMethod]
    public void TileFinder_IsValidTile_Patch_Omniscient_PreservesVanillaRejection()
    {
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.Omniscient;
        PlanetTile tile = new(42);
        SensoryManager.IsExploredOverride = _ => false;
        const string vanillaReason = "Cannot land on ocean.";
        StringBuilder reason = new(vanillaReason);

        StartingSiteSelectionFeedback_Patch.TileFinder_IsValidTileForNewSettlement_Patch.Postfix(tile, reason, __result: false);

        Assert.AreEqual(
            vanillaReason,
            reason.ToString(),
            "Omniscient mode must keep standard vanilla rejection message.");
    }

    [TestMethod]
    public void ShouldMaskRejectionReason_InvalidTile_ReturnsFalse()
    {
        PlanetTile tile = PlanetTile.Invalid;
        SensoryManager.IsExploredOverride = _ => false;

        bool shouldMask = StartingSiteFeedbackUtility.ShouldMaskRejectionReason(tile);

        Assert.IsFalse(shouldMask, "Invalid tiles must never be masked as an unviable sector drop.");
    }

    [TestMethod]
    public void TileFinder_IsValidTile_Patch_InvalidTile_DoesNotMaskRejection()
    {
        PlanetTile tile = PlanetTile.Invalid;
        SensoryManager.IsExploredOverride = _ => false;
        const string initialReason = "Must select a starting site.";
        StringBuilder reason = new(initialReason);

        StartingSiteSelectionFeedback_Patch.TileFinder_IsValidTileForNewSettlement_Patch.Postfix(tile, reason, __result: false);

        Assert.AreEqual(initialReason, reason.ToString(), "Invalid tiles must not mask standard selection errors.");
    }

    [TestMethod]
    public void TileFinder_IsValidTile_Patch_FogOfWar_Unexplored_EmptyReason_PopulatesAmbiguousMessage()
    {
        PlanetTile tile = new(44);
        SensoryManager.IsExploredOverride = _ => false;
        StringBuilder reason = new();

        StartingSiteSelectionFeedback_Patch.TileFinder_IsValidTileForNewSettlement_Patch.Postfix(tile, reason, __result: false);

        Assert.AreEqual(
            StartingSiteFeedbackUtility.AmbiguousLandingRejectionMessage,
            reason.ToString(),
            "Even if the reason StringBuilder was empty, an unviable unexplored tile must receive ambiguous telemetry.");
    }

    [TestMethod]
    public void TileFinder_IsValidTile_Patch_ValidTile_DoesNotModifyReason()
    {
        PlanetTile tile = new(42);
        SensoryManager.IsExploredOverride = _ => false;
        StringBuilder reason = new();

        StartingSiteSelectionFeedback_Patch.TileFinder_IsValidTileForNewSettlement_Patch.Postfix(tile, reason, __result: true);

        Assert.AreEqual(0, reason.Length, "Valid tiles must not have rejection reasons appended.");
    }

    [TestMethod]
    public void TileFinder_IsValidTile_Patch_NullReason_DoesNotThrow()
    {
        PlanetTile tile = new(42);
        SensoryManager.IsExploredOverride = _ => false;

        // Must execute cleanly without NullReferenceException when reason is null
        StartingSiteSelectionFeedback_Patch.TileFinder_IsValidTileForNewSettlement_Patch.Postfix(tile, null!, __result: false);
    }

    // =========================================================================
    // 2. Hazard Confirmation Suppression Tests (Requirement 2)
    // =========================================================================

    [TestMethod]
    public void ShouldSuppressHazardConfirmation_FogOfWar_UnexploredTile_ReturnsTrue()
    {
        PlanetTile tile = new(100);
        SensoryManager.IsExploredOverride = _ => false;

        bool shouldSuppress = StartingSiteFeedbackUtility.ShouldSuppressHazardConfirmation(tile);

        Assert.IsTrue(shouldSuppress, "Unexplored tile under Fog of War must suppress hazard confirmations.");
    }

    [TestMethod]
    public void ShouldSuppressHazardConfirmation_FogOfWar_ExploredTile_ReturnsFalse()
    {
        PlanetTile tile = new(100);
        SensoryManager.IsExploredOverride = _ => true;

        bool shouldSuppress = StartingSiteFeedbackUtility.ShouldSuppressHazardConfirmation(tile);

        Assert.IsFalse(shouldSuppress, "Explored tile under Fog of War must show hazard confirmations.");
    }

    [TestMethod]
    public void ShouldSuppressHazardConfirmation_PlanetarySurveillance_ReturnsFalse()
    {
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.PlanetarySurveillance;
        PlanetTile tile = new(100);
        SensoryManager.IsExploredOverride = _ => false;

        bool shouldSuppress = StartingSiteFeedbackUtility.ShouldSuppressHazardConfirmation(tile);

        Assert.IsFalse(shouldSuppress, "Planetary Surveillance must keep hazard confirmations active.");
    }

    [TestMethod]
    public void ShouldSuppressHazardConfirmation_Omniscient_ReturnsFalse()
    {
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.Omniscient;
        PlanetTile tile = new(100);
        SensoryManager.IsExploredOverride = _ => false;

        bool shouldSuppress = StartingSiteFeedbackUtility.ShouldSuppressHazardConfirmation(tile);

        Assert.IsFalse(shouldSuppress, "Omniscient mode must keep hazard confirmations active.");
    }

    [TestMethod]
    public void GetConfirmationDescriptions_Patch_FogOfWar_Unexplored_SuppressesDescriptions()
    {
        PlanetTile tile = new(55);
        SensoryManager.IsExploredOverride = _ => false;
        IEnumerable<TaggedString> descriptions = new List<TaggedString>
        {
            new("This tile is 65% polluted."),
            new("Extreme cold biome warning.")
        };

        StartingSiteSelectionFeedback_Patch.SettlementProximityGoodwillUtility_GetConfirmationDescriptions_Patch.Postfix(tile, ref descriptions);

        Assert.IsFalse(descriptions.Any(), "Confirmation descriptions must be completely suppressed on unexplored tiles under Fog of War.");
    }

    [TestMethod]
    public void GetConfirmationDescriptions_Patch_FogOfWar_Explored_PreservesDescriptions()
    {
        PlanetTile tile = new(55);
        SensoryManager.IsExploredOverride = _ => true;
        List<TaggedString> original = new()
        {
            new("This tile is 65% polluted.")
        };
        IEnumerable<TaggedString> descriptions = original;

        StartingSiteSelectionFeedback_Patch.SettlementProximityGoodwillUtility_GetConfirmationDescriptions_Patch.Postfix(tile, ref descriptions);

        Assert.AreEqual(1, descriptions.Count(), "Confirmation descriptions must be retained for explored tiles.");
    }

    [TestMethod]
    public void GetConfirmationDescriptions_Patch_PlanetarySurveillance_PreservesDescriptions()
    {
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.PlanetarySurveillance;
        PlanetTile tile = new(55);
        SensoryManager.IsExploredOverride = _ => false;
        List<TaggedString> original = new()
        {
            new("This tile is 65% polluted.")
        };
        IEnumerable<TaggedString> descriptions = original;

        StartingSiteSelectionFeedback_Patch.SettlementProximityGoodwillUtility_GetConfirmationDescriptions_Patch.Postfix(tile, ref descriptions);

        Assert.AreEqual(1, descriptions.Count(), "Confirmation descriptions must be retained under Planetary Surveillance.");
    }

    [TestMethod]
    public void CheckConfirmSettle_Patch_FogOfWar_Unexplored_InvokesSettleActionImmediately()
    {
        PlanetTile tile = new(77);
        SensoryManager.IsExploredOverride = _ => false;
        bool settleActionInvoked = false;

        bool prefixResult = StartingSiteSelectionFeedback_Patch.SettlementProximityGoodwillUtility_CheckConfirmSettle_Patch.Prefix(
            tile,
            () => settleActionInvoked = true);

        Assert.IsFalse(prefixResult, "Prefix must return false to bypass vanilla confirmation dialog.");
        Assert.IsTrue(settleActionInvoked, "Settle action must be invoked immediately for blind colony touchdown.");
    }

    [TestMethod]
    public void CheckConfirmSettle_Patch_FogOfWar_Explored_AllowsVanillaConfirmationFlow()
    {
        PlanetTile tile = new(77);
        SensoryManager.IsExploredOverride = _ => true;
        bool settleActionInvoked = false;

        bool prefixResult = StartingSiteSelectionFeedback_Patch.SettlementProximityGoodwillUtility_CheckConfirmSettle_Patch.Prefix(
            tile,
            () => settleActionInvoked = true);

        Assert.IsTrue(prefixResult, "Prefix must return true for explored tiles to allow vanilla confirmation dialog.");
        Assert.IsFalse(settleActionInvoked, "Settle action must not be prematurely invoked when confirmation dialog is required.");
    }

    [TestMethod]
    public void CheckConfirmSettle_Patch_PlanetarySurveillance_AllowsVanillaConfirmationFlow()
    {
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.PlanetarySurveillance;
        PlanetTile tile = new(77);
        SensoryManager.IsExploredOverride = _ => false;
        bool settleActionInvoked = false;

        bool prefixResult = StartingSiteSelectionFeedback_Patch.SettlementProximityGoodwillUtility_CheckConfirmSettle_Patch.Prefix(
            tile,
            () => settleActionInvoked = true);

        Assert.IsTrue(prefixResult, "Planetary Surveillance must allow vanilla confirmation dialog.");
        Assert.IsFalse(settleActionInvoked, "Settle action must not be prematurely invoked under Planetary Surveillance.");
    }

    [TestMethod]
    public void CheckConfirmSettle_Patch_Omniscient_AllowsVanillaConfirmationFlow()
    {
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.Omniscient;
        PlanetTile tile = new(77);
        SensoryManager.IsExploredOverride = _ => false;
        bool settleActionInvoked = false;

        bool prefixResult = StartingSiteSelectionFeedback_Patch.SettlementProximityGoodwillUtility_CheckConfirmSettle_Patch.Prefix(
            tile,
            () => settleActionInvoked = true);

        Assert.IsTrue(prefixResult, "Omniscient mode must allow vanilla confirmation dialog.");
        Assert.IsFalse(settleActionInvoked, "Settle action must not be prematurely invoked under Omniscient mode.");
    }

    [TestMethod]
    public void ShouldSuppressHazardConfirmation_InvalidTile_ReturnsFalse()
    {
        PlanetTile tile = PlanetTile.Invalid;
        SensoryManager.IsExploredOverride = _ => false;

        bool shouldSuppress = StartingSiteFeedbackUtility.ShouldSuppressHazardConfirmation(tile);

        Assert.IsFalse(shouldSuppress, "Invalid tiles must never suppress hazard confirmations or auto-invoke settle.");
    }

    [TestMethod]
    public void CheckConfirmSettle_Patch_InvalidTile_AllowsVanillaFlow()
    {
        PlanetTile tile = PlanetTile.Invalid;
        SensoryManager.IsExploredOverride = _ => false;
        bool settleActionInvoked = false;

        bool prefixResult = StartingSiteSelectionFeedback_Patch.SettlementProximityGoodwillUtility_CheckConfirmSettle_Patch.Prefix(
            tile,
            () => settleActionInvoked = true);

        Assert.IsTrue(prefixResult, "Invalid tiles must pass through to vanilla validation and not auto-settle.");
        Assert.IsFalse(settleActionInvoked, "Settle action must never be invoked on an invalid tile.");
    }

    [TestMethod]
    public void GetConfirmationEffects_Patch_FogOfWar_Unexplored_SuppressesEffects()
    {
        PlanetTile tile = new(88);
        SensoryManager.IsExploredOverride = _ => false;
        IEnumerable<string> effects = new List<string>
        {
            "-15 goodwill with Outlanders for settling too close"
        };

        StartingSiteSelectionFeedback_Patch.SettlementProximityGoodwillUtility_GetConfirmationEffects_Patch.Postfix(tile, ref effects);

        Assert.IsFalse(effects.Any(), "Goodwill impact descriptions must be suppressed on unexplored tiles to prevent leaking hidden faction bases.");
    }

    [TestMethod]
    public void GetConfirmationEffects_Patch_FogOfWar_Explored_PreservesEffects()
    {
        PlanetTile tile = new(88);
        SensoryManager.IsExploredOverride = _ => true;
        List<string> original = new()
        {
            "-15 goodwill with Outlanders for settling too close"
        };
        IEnumerable<string> effects = original;

        StartingSiteSelectionFeedback_Patch.SettlementProximityGoodwillUtility_GetConfirmationEffects_Patch.Postfix(tile, ref effects);

        Assert.AreEqual(1, effects.Count(), "Goodwill impact descriptions must be preserved on explored tiles.");
    }

    [TestMethod]
    public void GetConfirmationEffects_Patch_PlanetarySurveillance_PreservesEffects()
    {
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.PlanetarySurveillance;
        PlanetTile tile = new(88);
        SensoryManager.IsExploredOverride = _ => false;
        List<string> original = new()
        {
            "-15 goodwill with Outlanders for settling too close"
        };
        IEnumerable<string> effects = original;

        StartingSiteSelectionFeedback_Patch.SettlementProximityGoodwillUtility_GetConfirmationEffects_Patch.Postfix(tile, ref effects);

        Assert.AreEqual(1, effects.Count(), "Goodwill impact descriptions must be preserved under Planetary Surveillance.");
    }

    // =========================================================================
    // 3. SensoryManager & Bounds Robustness Tests
    // =========================================================================

    [TestMethod]
    public void Harmony_PatchAll_EntireGeoscapeAssembly_AppliesWithoutContractException()
    {
        HarmonyLib.Harmony harmony = new("test.projectvigilance.entireassembly");
        harmony.PatchAll(typeof(GeoscapeMod).Assembly);
    }

    [TestMethod]
    public void SensoryManager_IsTileIdExplored_NegativeTileId_ReturnsFalseWithoutThrowing()
    {
        SensoryManager manager = (SensoryManager)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(SensoryManager));

        // Negative tile ID must safely return false and not throw IndexOutOfRangeException
        bool result = manager.IsTileIdExplored(-1);

        Assert.IsFalse(result, "Negative tile IDs must safely evaluate as unexplored.");
    }

    [TestMethod]
    public void StartingSiteFeedbackUtility_IsTileExplored_InvalidTile_ReturnsFalse()
    {
        PlanetTile tile = PlanetTile.Invalid;

        bool result = StartingSiteFeedbackUtility.IsTileExplored(tile);

        Assert.IsFalse(result, "Invalid tile must always evaluate as unexplored.");
    }
}
