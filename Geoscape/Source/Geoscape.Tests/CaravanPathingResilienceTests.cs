using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RimWorld;
using RimWorld.Planet;
using Verse;
using ProjectVigilance.Geoscape;
using ProjectVigilance.Geoscape.Patches;
using ProjectVigilance.Geoscape.Sensory;

namespace Geoscape.Tests;

/// <summary>
/// Unit tests validating planetary caravan route resilience, fog-of-war optimistic pathing integration,
/// and defensive handling of unreachable destinations and NotFound world paths in RimWorld 1.7.
/// </summary>
[TestClass]
public class CaravanPathingResilienceTests
{
    [TestInitialize]
    public void Setup()
    {
        OptimisticPathingUtility.Active = false;
        OptimisticPathingUtility.ForceActive = false;
        OptimisticPathingUtility.ActiveFormCaravanCount = 0;
        OptimisticPathingUtility.IsTileUnexploredOverride = null;
        OptimisticPathingUtility.IsTileImpassableOverride = null;
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;
    }

    [TestCleanup]
    public void Teardown()
    {
        OptimisticPathingUtility.Active = false;
        OptimisticPathingUtility.ForceActive = false;
        OptimisticPathingUtility.ActiveFormCaravanCount = 0;
        OptimisticPathingUtility.IsTileUnexploredOverride = null;
        OptimisticPathingUtility.IsTileImpassableOverride = null;
    }

    [TestMethod]
    public void OptimisticPathing_FormCaravanContext_EnablesOptimisticPathing()
    {
        // Arrange: Dialog_FormCaravan is actively executing
        OptimisticPathingUtility.ActiveFormCaravanCount = 1;

        Assert.IsTrue(OptimisticPathingUtility.IsFormCaravanActive, "IsFormCaravanActive must be true when ActiveFormCaravanCount > 0.");
        Assert.IsTrue(OptimisticPathingUtility.IsPlayerPlanningRoute, "IsPlayerPlanningRoute must be true during caravan formation.");

        // Act: FindPath_Patch evaluates player context with null caravan
        WorldPathing_FindPath_Patch.FindPath_Patch.Prefix((PlanetTile)10, (PlanetTile)20, null!, out bool state);

        try
        {
            // Assert: OptimisticPathingUtility.Active must become true
            Assert.IsTrue(OptimisticPathingUtility.Active, "Optimistic pathing must activate for Dialog_FormCaravan route calculation.");
            Assert.IsTrue(OptimisticPathingUtility.ShouldUseOptimisticPathing(), "ShouldUseOptimisticPathing must return true under ClassicHazeOfWar.");
        }
        finally
        {
            WorldPathing_FindPath_Patch.FindPath_Patch.Finalizer(null, state);
            Assert.IsFalse(OptimisticPathingUtility.Active, "Finalizer must restore prior active state.");
        }
    }

    [TestMethod]
    public void Dialog_FormCaravan_DoWindowContents_Patch_ManagesScopeAndRestoresState()
    {
        // Arrange
        Assert.AreEqual(0, OptimisticPathingUtility.ActiveFormCaravanCount);
        Assert.IsFalse(OptimisticPathingUtility.Active);

        // Act & Assert Prefix
        CaravanPathingResiliencePatches.Dialog_FormCaravan_DoWindowContents_Patch.Prefix(out bool state);
        Assert.AreEqual(1, OptimisticPathingUtility.ActiveFormCaravanCount, "Prefix must increment ActiveFormCaravanCount.");
        Assert.IsTrue(OptimisticPathingUtility.Active, "Prefix must enable OptimisticPathingUtility.Active in ClassicHazeOfWar.");

        // Act & Assert Finalizer on normal exit (no exception)
        CaravanPathingResiliencePatches.Dialog_FormCaravan_DoWindowContents_Patch.Finalizer(null, state);
        Assert.AreEqual(0, OptimisticPathingUtility.ActiveFormCaravanCount, "Finalizer must decrement ActiveFormCaravanCount on normal exit.");
        Assert.IsFalse(OptimisticPathingUtility.Active, "Finalizer must restore prior active state.");

        // Act & Assert Finalizer handles exceptions
        CaravanPathingResiliencePatches.Dialog_FormCaravan_DoWindowContents_Patch.Prefix(out bool state2);
        var ex = new InvalidOperationException("Test exception in GUI render");
        var handledEx = CaravanPathingResiliencePatches.Dialog_FormCaravan_DoWindowContents_Patch.Finalizer(ex, state2);

        Assert.AreEqual(0, OptimisticPathingUtility.ActiveFormCaravanCount, "Finalizer must decrement ActiveFormCaravanCount on error.");
        Assert.IsFalse(OptimisticPathingUtility.Active, "Finalizer must restore prior active state on error.");
        Assert.AreSame(ex, handledEx, "Finalizer must rethrow the original exception.");
    }

    [TestMethod]
    public void Dialog_FormCaravan_Notify_ChoseRoute_Patch_ManagesScopeAndRestoresState()
    {
        // Act & Assert Prefix
        CaravanPathingResiliencePatches.Dialog_FormCaravan_Notify_ChoseRoute_Patch.Prefix(out bool state);
        Assert.AreEqual(1, OptimisticPathingUtility.ActiveFormCaravanCount);
        Assert.IsTrue(OptimisticPathingUtility.Active);

        // Act & Assert Finalizer
        CaravanPathingResiliencePatches.Dialog_FormCaravan_Notify_ChoseRoute_Patch.Finalizer(null, state);
        Assert.AreEqual(0, OptimisticPathingUtility.ActiveFormCaravanCount);
        Assert.IsFalse(OptimisticPathingUtility.Active);
    }

    [TestMethod]
    public void NestedCaravanFormingScopes_DoNotPrematurelyZeroCount()
    {
        // Arrange: Outer DoWindowContents starts
        CaravanPathingResiliencePatches.Dialog_FormCaravan_DoWindowContents_Patch.Prefix(out bool outerState);
        Assert.AreEqual(1, OptimisticPathingUtility.ActiveFormCaravanCount);

        // Act: Inner Notify_ChoseRoute starts and finishes
        CaravanPathingResiliencePatches.Dialog_FormCaravan_Notify_ChoseRoute_Patch.Prefix(out bool innerState);
        Assert.AreEqual(2, OptimisticPathingUtility.ActiveFormCaravanCount);

        CaravanPathingResiliencePatches.Dialog_FormCaravan_Notify_ChoseRoute_Patch.Finalizer(null, innerState);
        Assert.AreEqual(1, OptimisticPathingUtility.ActiveFormCaravanCount, "Inner scope exit must leave outer scope active.");
        Assert.IsTrue(OptimisticPathingUtility.IsFormCaravanActive);

        // Act: Outer DoWindowContents finishes
        CaravanPathingResiliencePatches.Dialog_FormCaravan_DoWindowContents_Patch.Finalizer(null, outerState);
        Assert.AreEqual(0, OptimisticPathingUtility.ActiveFormCaravanCount);
        Assert.IsFalse(OptimisticPathingUtility.IsFormCaravanActive);
    }

    [TestMethod]
    public void WorldPath_Peek_Patch_PreventsArgumentOutOfRangeExceptionOnNotFound()
    {
        // Arrange: WorldPath.NewNotFound() has nodes.Count == 0 and Found == false
        WorldPath notFoundPath = WorldPath.NewNotFound();
        PlanetTile result = (PlanetTile)999;

        // Act: Peek(0) on NotFound path
        bool continueOriginal = CaravanPathingResiliencePatches.WorldPath_Peek_Patch.Prefix(notFoundPath, 0, ref result);

        // Assert: Must intercept and return PlanetTile.Invalid without throwing ArgumentOutOfRangeException
        Assert.IsFalse(continueOriginal, "Prefix must suppress original Peek implementation for NotFound path.");
        Assert.AreEqual(PlanetTile.Invalid, result, "Prefix must return PlanetTile.Invalid for NotFound path.");

        // Act: Peek with negative index
        PlanetTile negativeResult = (PlanetTile)999;
        bool negativeContinue = CaravanPathingResiliencePatches.WorldPath_Peek_Patch.Prefix(notFoundPath, -1, ref negativeResult);
        Assert.IsFalse(negativeContinue);
        Assert.AreEqual(PlanetTile.Invalid, negativeResult);

        // Act: Peek with large index exceeding available nodes
        PlanetTile outOfBoundsResult = (PlanetTile)999;
        bool outOfBoundsContinue = CaravanPathingResiliencePatches.WorldPath_Peek_Patch.Prefix(notFoundPath, 50, ref outOfBoundsResult);
        Assert.IsFalse(outOfBoundsContinue);
        Assert.AreEqual(PlanetTile.Invalid, outOfBoundsResult);
    }

    [TestMethod]
    public void CaravanArrivalTimeEstimator_EstimatedTicksToArrive_Patch_HandlesNotFoundSafely()
    {
        // Arrange
        WorldPath notFoundPath = WorldPath.NewNotFound();
        int ticksResult = 99999;

        // Act
        bool continueOriginal = CaravanPathingResiliencePatches.CaravanArrivalTimeEstimator_EstimatedTicksToArrive_Patch.Prefix(
            (PlanetTile)10,
            (PlanetTile)20,
            notFoundPath,
            ref ticksResult);

        // Assert
        Assert.IsFalse(continueOriginal, "Prefix must intercept calculation for NotFound path.");
        Assert.AreEqual(0, ticksResult, "Estimated ticks to arrive must return 0 for NotFound path.");
    }

    [TestMethod]
    public void CaravanArrivalTimeEstimator_EstimatedTicksToArriveToEvery_Patch_PopulatesOnlyOriginSafely()
    {
        // Arrange
        WorldPath notFoundPath = WorldPath.NewNotFound();
        var outTicks = new List<ValueTuple<PlanetTile, int>>
        {
            new ValueTuple<PlanetTile, int>((PlanetTile)50, 1234)
        };

        // Act
        bool continueOriginal = CaravanPathingResiliencePatches.CaravanArrivalTimeEstimator_EstimatedTicksToArriveToEvery_Patch.Prefix(
            (PlanetTile)10,
            (PlanetTile)20,
            notFoundPath,
            outTicks);

        // Assert: Must populate ONLY the origin tile so TileIllBeInAt stays at origin and does not falsely evaluate at unreachable destination
        Assert.IsFalse(continueOriginal, "Prefix must intercept calculation for NotFound path.");
        Assert.AreEqual(1, outTicks.Count, "Must populate solely the origin tile.");
        Assert.AreEqual((PlanetTile)10, outTicks[0].Item1);
        Assert.AreEqual(0, outTicks[0].Item2);

        // Verify TileIllBeInAt returns origin tile, never destination or PlanetTile.Invalid
        PlanetTile tileAtTick0 = CaravanArrivalTimeEstimator.TileIllBeInAt(1000, outTicks, 1000);
        Assert.AreEqual((PlanetTile)10, tileAtTick0);

        PlanetTile tileAtTickFuture = CaravanArrivalTimeEstimator.TileIllBeInAt(50000, outTicks, 1000);
        Assert.AreEqual((PlanetTile)10, tileAtTickFuture);
    }

    [TestMethod]
    public void CaravanArrivalTimeEstimator_EstimatedTicksToArriveToEvery_Patch_GuardsInvalidOriginTile()
    {
        // Arrange
        WorldPath notFoundPath = WorldPath.NewNotFound();
        var outTicks = new List<ValueTuple<PlanetTile, int>>
        {
            new ValueTuple<PlanetTile, int>((PlanetTile)50, 1234)
        };

        // Act
        bool continueOriginal = CaravanPathingResiliencePatches.CaravanArrivalTimeEstimator_EstimatedTicksToArriveToEvery_Patch.Prefix(
            PlanetTile.Invalid,
            (PlanetTile)20,
            notFoundPath,
            outTicks);

        // Assert: When origin is invalid, must not poison outTicks with PlanetTile.Invalid
        Assert.IsFalse(continueOriginal);
        Assert.AreEqual(0, outTicks.Count, "Must not add PlanetTile.Invalid into outTicks.");
    }

    [TestMethod]
    public void DaysWorthOfFoodCalculator_AllOverloads_ResetNotFoundPathToNull()
    {
        // 1. Transferables overload
        WorldPath? path1 = WorldPath.NewNotFound();
        CaravanPathingResiliencePatches.DaysWorthOfFoodCalculator_ApproxDaysWorthOfFood_Transferables_Patch.Prefix(ref path1);
        Assert.IsNull(path1, "Transferables overload must reset NotFound path to null.");

        // 2. Pawns overload
        WorldPath? path2 = WorldPath.NewNotFound();
        CaravanPathingResiliencePatches.DaysWorthOfFoodCalculator_ApproxDaysWorthOfFood_Pawns_Patch.Prefix(ref path2);
        Assert.IsNull(path2, "Pawns overload must reset NotFound path to null.");

        // 3. LeftAfterTransfer overload
        WorldPath? path3 = WorldPath.NewNotFound();
        CaravanPathingResiliencePatches.DaysWorthOfFoodCalculator_ApproxDaysWorthOfFoodLeftAfterTransfer_Patch.Prefix(ref path3);
        Assert.IsNull(path3, "LeftAfterTransfer overload must reset NotFound path to null.");
    }

    [TestMethod]
    public void DaysWorthOfFoodCalculator_AllOverloads_PreserveValidFoundPath()
    {
        // Arrange
        WorldPath validPath = new WorldPath();
        validPath.AddNodeAtStart((PlanetTile)10);
        validPath.AddNodeAtStart((PlanetTile)11);
        validPath.SetupFound(150f, null!);

        WorldPath? path1 = validPath;
        CaravanPathingResiliencePatches.DaysWorthOfFoodCalculator_ApproxDaysWorthOfFood_Transferables_Patch.Prefix(ref path1);
        Assert.AreSame(validPath, path1);

        WorldPath? path2 = validPath;
        CaravanPathingResiliencePatches.DaysWorthOfFoodCalculator_ApproxDaysWorthOfFood_Pawns_Patch.Prefix(ref path2);
        Assert.AreSame(validPath, path2);

        WorldPath? path3 = validPath;
        CaravanPathingResiliencePatches.DaysWorthOfFoodCalculator_ApproxDaysWorthOfFoodLeftAfterTransfer_Patch.Prefix(ref path3);
        Assert.AreSame(validPath, path3);
    }

    [TestMethod]
    public void DaysUntilRotCalculator_AllOverloads_ResetNotFoundPathToNull()
    {
        WorldPath? path1 = WorldPath.NewNotFound();
        CaravanPathingResiliencePatches.DaysUntilRotCalculator_ApproxDaysUntilRot_Transferables_Patch.Prefix(ref path1);
        Assert.IsNull(path1);

        WorldPath? path2 = WorldPath.NewNotFound();
        CaravanPathingResiliencePatches.DaysUntilRotCalculator_ApproxDaysUntilRot_ThingCounts_Patch.Prefix(ref path2);
        Assert.IsNull(path2);

        WorldPath? path3 = WorldPath.NewNotFound();
        CaravanPathingResiliencePatches.DaysUntilRotCalculator_ApproxDaysUntilRot_Things_Patch.Prefix(ref path3);
        Assert.IsNull(path3);

        WorldPath? path4 = WorldPath.NewNotFound();
        CaravanPathingResiliencePatches.DaysUntilRotCalculator_ApproxDaysUntilRotLeftAfterTransfer_Patch.Prefix(ref path4);
        Assert.IsNull(path4);
    }

    [TestMethod]
    public void VirtualPlantsUtility_EnvironmentAllowsEatingVirtualPlantsAt_Patch_HandlesInvalidTileSafely()
    {
        PlanetTile invalidTile = PlanetTile.Invalid;
        bool result = true;

        bool continueOriginal = CaravanPathingResiliencePatches.VirtualPlantsUtility_EnvironmentAllowsEatingVirtualPlantsAt_Patch.Prefix(invalidTile, ref result);

        Assert.IsFalse(continueOriginal, "Prefix must intercept invalid planet tile.");
        Assert.IsFalse(result, "Invalid tile must not permit virtual plant consumption.");
    }

    [TestMethod]
    public void CaravanNightRestUtility_WouldBeRestingAt_Patch_HandlesInvalidTileSafely()
    {
        PlanetTile invalidTile = PlanetTile.Invalid;
        bool result = true;

        bool continueOriginal = CaravanPathingResiliencePatches.CaravanNightRestUtility_WouldBeRestingAt_Patch.Prefix(invalidTile, ref result);

        Assert.IsFalse(continueOriginal, "Prefix must intercept invalid planet tile.");
        Assert.IsFalse(result, "Invalid tile cannot evaluate night resting.");
    }

    [TestMethod]
    public void GenTemperature_GetTemperatureFromSeasonAtTile_Patch_HandlesInvalidTileSafely()
    {
        PlanetTile invalidTile = PlanetTile.Invalid;
        float result = 999f;

        bool continueOriginal = CaravanPathingResiliencePatches.GenTemperature_GetTemperatureFromSeasonAtTile_Patch.Prefix(invalidTile, ref result);

        Assert.IsFalse(continueOriginal, "Prefix must intercept invalid planet tile.");
        Assert.AreEqual(21f, result, 0.001f, "Invalid tile must return 21C standard temperature.");
    }

    [TestMethod]
    public void WorldPathGrid_CalculatedMovementDifficultyAt_Patch_HandlesInvalidTileSafely()
    {
        PlanetTile invalidTile = PlanetTile.Invalid;
        float result = 0f;

        bool continueOriginal = CaravanPathingResiliencePatches.WorldPathGrid_CalculatedMovementDifficultyAt_Patch.Prefix(invalidTile, ref result);

        Assert.IsFalse(continueOriginal, "Prefix must intercept invalid planet tile.");
        Assert.AreEqual(1000f, result, 0.001f, "Invalid tile must return impassable movement difficulty.");
    }

    [TestMethod]
    public void WorldReachability_CanReach_Patch_HandlesInvalidTileSafely()
    {
        bool result = true;

        bool continueInvalidStart = WorldPathing_FindPath_Patch.WorldReachability_CanReach_Patch.Prefix(PlanetTile.Invalid, (PlanetTile)10, ref result);
        Assert.IsFalse(continueInvalidStart);
        Assert.IsFalse(result);

        result = true;
        bool continueInvalidDest = WorldPathing_FindPath_Patch.WorldReachability_CanReach_Patch.Prefix((PlanetTile)10, PlanetTile.Invalid, ref result);
        Assert.IsFalse(continueInvalidDest);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void WorldPathGrid_PassabilityAndDifficultyPatches_HandleInvalidTileSafely()
    {
        // Passable
        bool passableResult = true;
        bool contPassable = WorldPathing_FindPath_Patch.WorldPathGrid_Passable_Patch.Prefix(PlanetTile.Invalid, ref passableResult);
        Assert.IsFalse(contPassable);
        Assert.IsFalse(passableResult);

        // PassableFast
        bool passableFastResult = true;
        bool contPassableFast = WorldPathing_FindPath_Patch.WorldPathGrid_PassableFast_Patch.Prefix(PlanetTile.Invalid, ref passableFastResult);
        Assert.IsFalse(contPassableFast);
        Assert.IsFalse(passableFastResult);

        // Impassable
        bool impassableResult = false;
        bool contImpassable = WorldPathing_FindPath_Patch.World_Impassable_Patch.Prefix(PlanetTile.Invalid, ref impassableResult);
        Assert.IsFalse(contImpassable);
        Assert.IsTrue(impassableResult);

        // PerceivedMovementDifficultyAt
        float diffResult = 0f;
        bool contDiff = WorldPathing_FindPath_Patch.WorldPathGrid_PerceivedMovementDifficultyAt_Patch.Prefix(PlanetTile.Invalid, ref diffResult);
        Assert.IsFalse(contDiff);
        Assert.AreEqual(1000f, diffResult, 0.001f);
    }

    [TestMethod]
    public void Dialog_FormCaravan_SelectApproximateBestTravelSupplies_Patch_GuardsNullTransferablesAndSetsScope()
    {
        // Arrange: Uninitialised dialog with null transferables field
        var dialog = (Dialog_FormCaravan)RuntimeHelpers.GetUninitializedObject(typeof(Dialog_FormCaravan));

        // Act: Null transferables
        bool runOriginalWhenNull = WorldPathing_FindPath_Patch.Dialog_FormCaravan_SelectApproximateBestTravelSupplies_Patch.Prefix(dialog, out bool stateNull);

        // Assert: Must abort without attempting calculation
        Assert.IsFalse(runOriginalWhenNull, "Prefix must abort when transferables field is null.");

        // Arrange: Initialise transferables field with empty list
        var transferables = new List<TransferableOneWay>();
        HarmonyLib.Traverse.Create(dialog).Field("transferables").SetValue(transferables);

        // Act: Valid transferables list
        bool runOriginalWhenValid = WorldPathing_FindPath_Patch.Dialog_FormCaravan_SelectApproximateBestTravelSupplies_Patch.Prefix(dialog, out bool stateValid);

        try
        {
            // Assert: Must continue and activate optimistic pathing scope
            Assert.IsTrue(runOriginalWhenValid, "Prefix must proceed when transferables list is initialised.");
            Assert.IsTrue(OptimisticPathingUtility.Active, "Must activate optimistic pathing scope in ClassicHazeOfWar.");
        }
        finally
        {
            WorldPathing_FindPath_Patch.Dialog_FormCaravan_SelectApproximateBestTravelSupplies_Patch.Finalizer(null, stateValid);
            Assert.IsFalse(OptimisticPathingUtility.Active, "Finalizer must restore prior active state.");
        }
    }
}
