using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RimWorld;
using RimWorld.Planet;
using Verse;
using ProjectVigilance.Geoscape.World;
using ProjectVigilance.Geoscape.Patches;
using ProjectVigilance.Geoscape.Sensory;

namespace Geoscape.Tests;

[TestClass]
public sealed class InterFactionEncounterTests
{
    [TestMethod]
    public void TechMultiplier_CalculatesAccuratelyAcrossAllTechLevels()
    {
        Assert.AreEqual(0.5f, InterFactionEncounterManager.GetTechMultiplier(TechLevel.Animal), 0.001f);
        Assert.AreEqual(0.8f, InterFactionEncounterManager.GetTechMultiplier(TechLevel.Neolithic), 0.001f);
        Assert.AreEqual(1.0f, InterFactionEncounterManager.GetTechMultiplier(TechLevel.Medieval), 0.001f);
        Assert.AreEqual(1.25f, InterFactionEncounterManager.GetTechMultiplier(TechLevel.Industrial), 0.001f);
        Assert.AreEqual(1.5f, InterFactionEncounterManager.GetTechMultiplier(TechLevel.Spacer), 0.001f);
        Assert.AreEqual(1.75f, InterFactionEncounterManager.GetTechMultiplier(TechLevel.Ultra), 0.001f);
        Assert.AreEqual(2.0f, InterFactionEncounterManager.GetTechMultiplier(TechLevel.Archotech), 0.001f);
        Assert.AreEqual(1.0f, InterFactionEncounterManager.GetTechMultiplier(TechLevel.Undefined), 0.001f);
    }

    [TestMethod]
    public void ResolveSkirmishAlgebraic_OverwhelmingAdvantage_CausesRoutAndHeavyLoserCasualties()
    {
        var sideA = new SkirmishSideData
        {
            CombatPoints = 2000f,
            TechLevel = TechLevel.Spacer, // 1.5x
            FactionName = "Spacer Empire",
            IsTradeCaravan = false
        };

        var sideB = new SkirmishSideData
        {
            CombatPoints = 300f,
            TechLevel = TechLevel.Neolithic, // 0.8x
            FactionName = "Tribal Warband",
            IsTradeCaravan = false
        };

        // Strength A: 2000 * 1.5 * 1.0 = 3000
        // Strength B: 300 * 0.8 * 1.0 = 240
        // Ratio = 3000 / 240 = 12.5 (> 2.0x)
        var result = InterFactionEncounterManager.ResolveSkirmishAlgebraic(sideA, sideB, forcedRngA: 1.0f, forcedRngB: 1.0f);

        Assert.IsTrue(result.SideAWon, "Side A (Spacer) must win decisively against small Neolithic band.");
        Assert.IsTrue(result.SideBRoutedOrDestroyed, "Side B must be marked as routed or destroyed due to extreme casualty ratio.");
        Assert.AreEqual(0.90f, result.SideBCasualtyFraction, 0.001f, "Overwhelming defeat yields 90% casualty fraction.");
        Assert.IsTrue(result.SideACasualtyFraction <= 0.10f, "Winner casualties must remain very low in overwhelming victory.");
        Assert.AreEqual(30f, result.SideBRemainingPoints, 0.01f, "Loser remaining combat points must reflect 90% losses.");
        Assert.IsTrue(result.SalvageLootValue > 0f, "Skirmish must yield salvage loot from defeated force.");
    }

    [TestMethod]
    public void ResolveSkirmishAlgebraic_CloseContest_ResultsInPyrrhicCasualtiesOnBothSides()
    {
        var sideA = new SkirmishSideData
        {
            CombatPoints = 600f,
            TechLevel = TechLevel.Industrial, // 1.25x -> 750
            FactionName = "Industrial Outlanders",
            IsTradeCaravan = false
        };

        var sideB = new SkirmishSideData
        {
            CombatPoints = 680f,
            TechLevel = TechLevel.Medieval, // 1.0x -> 680
            FactionName = "Medieval Kingdom",
            IsTradeCaravan = false
        };

        // Ratio: 750 / 680 = 1.10 (< 1.4x)
        var result = InterFactionEncounterManager.ResolveSkirmishAlgebraic(sideA, sideB, forcedRngA: 1.0f, forcedRngB: 1.0f);

        Assert.IsTrue(result.SideAWon, "Side A with 750 effective strength should defeat 680 strength.");
        Assert.AreEqual(0.55f, result.SideBCasualtyFraction, 0.001f, "Close contested fight yields 55% loser casualty fraction.");
        Assert.IsTrue(result.SideACasualtyFraction >= 0.25f, "Winner in close fight must take meaningful pyrrhic casualties (>= 25%).");
        Assert.IsFalse(result.SideBRoutedOrDestroyed, "Loser in close contest should retreat with surviving forces rather than be wiped out.");
    }

    [TestMethod]
    public void ResolveSkirmishAlgebraic_TradeCaravanParticipant_GeneratesIncreasedSalvageLoot()
    {
        var militaryA = new SkirmishSideData
        {
            CombatPoints = 500f,
            TechLevel = TechLevel.Industrial,
            FactionName = "Pirates",
            IsTradeCaravan = false
        };

        var militaryB = new SkirmishSideData
        {
            CombatPoints = 500f,
            TechLevel = TechLevel.Industrial,
            FactionName = "Hostile Tribe",
            IsTradeCaravan = false
        };

        var tradeCaravanB = new SkirmishSideData
        {
            CombatPoints = 500f,
            TechLevel = TechLevel.Industrial,
            FactionName = "Merchant Guild",
            IsTradeCaravan = true
        };

        var standardResult = InterFactionEncounterManager.ResolveSkirmishAlgebraic(militaryA, militaryB, 1.0f, 1.0f);
        var tradeResult = InterFactionEncounterManager.ResolveSkirmishAlgebraic(militaryA, tradeCaravanB, 1.0f, 1.0f);

        Assert.IsTrue(tradeResult.SalvageLootValue > standardResult.SalvageLootValue,
            "Trade caravan skirmishes must yield bonus salvage loot value from merchant stock.");
        Assert.AreEqual(standardResult.SalvageLootValue + 350f, tradeResult.SalvageLootValue, 0.001f);
    }

    [TestMethod]
    public void TileBucketing_OM_IncursionsIndexedCorrectlyAndPooledWithoutAllocation()
    {
        var manager = new InterFactionEncounterManager(null!);

        var inc1 = new WorldObject_Incursion();
        inc1.SetTileRaw((PlanetTile)100);
        var inc2 = new WorldObject_Incursion();
        inc2.SetTileRaw((PlanetTile)100);
        var inc3 = new WorldObject_Incursion();
        inc3.SetTileRaw((PlanetTile)205);

        manager.IndexIncursion(inc1);
        manager.IndexIncursion(inc2);
        manager.IndexIncursion(inc3);

        Assert.AreEqual(2, manager.TileBuckets.Count, "Must have exactly 2 distinct tile buckets (tile 100 and tile 205).");
        Assert.AreEqual(2, manager.TileBuckets[100].Count, "Tile 100 must contain 2 incursions.");
        Assert.AreEqual(1, manager.TileBuckets[205].Count, "Tile 205 must contain 1 incursion.");

        // Clear and verify zero-allocation list recycling
        manager.ClearBuckets();
        Assert.AreEqual(0, manager.TileBuckets.Count, "ClearBuckets must drain all active tile buckets.");

        // Re-index into recycled lists
        var inc4 = new WorldObject_Incursion();
        inc4.SetTileRaw((PlanetTile)550);
        manager.IndexIncursion(inc4);

        Assert.AreEqual(1, manager.TileBuckets.Count);
        Assert.AreEqual(1, manager.TileBuckets[550].Count);
    }

    [TestMethod]
    public void HarmonyPatches_CaravanMeetingAndDemand_CanBeAppliedCleanly()
    {
        var harmony = new HarmonyLib.Harmony("test.phase4.harmony.caravan.encounters");
        var patch1 = typeof(IncidentWorker_CaravanMeeting_Patch);
        var patch2 = typeof(IncidentWorker_CaravanDemand_Patch);

        harmony.CreateClassProcessor(patch1).Patch();
        harmony.CreateClassProcessor(patch2).Patch();
    }

    [TestMethod]
    public void GroundedCaravanMeetingHelper_NullOrInvalidTarget_SafelyFallsBackToVanilla()
    {
        var parms = new IncidentParms
        {
            target = null // Not a caravan
        };

        bool result = false;
        bool shouldProceedVanillaMeeting = GroundedCaravanMeetingHelper.TryInterceptMeeting(null!, parms, ref result);
        Assert.IsTrue(shouldProceedVanillaMeeting, "Must return true (fallback to vanilla) when target is not a spawned player caravan.");
        Assert.IsFalse(result);

        bool shouldProceedVanillaDemand = GroundedCaravanMeetingHelper.TryInterceptDemand(null!, parms, ref result);
        Assert.IsTrue(shouldProceedVanillaDemand, "Must return true (fallback to vanilla) when demand target is invalid.");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void WorldObject_Incursion_SetCurrentPathIndex_ClampsWithinPathBounds()
    {
        var inc = new WorldObject_Incursion();
        inc.SetCurrentPathIndex(5);
        Assert.AreEqual(0, inc.CurrentPathIndex, "Index must clamp to 0 on empty path.");
    }

    [TestMethod]
    public void WorldObject_Incursion_CurrentTile_MatchesTileProperty()
    {
        var inc = new WorldObject_Incursion();
        inc.SetTileRaw((PlanetTile)1492);
        Assert.AreEqual((PlanetTile)1492, inc.CurrentTile);
        Assert.AreEqual((PlanetTile)1492, inc.Tile);
    }

    [TestMethod]
    public void ResolveSkirmishAlgebraic_DecisiveVictory_ResultsInSeventyFivePercentLosses()
    {
        var sideA = new SkirmishSideData
        {
            CombatPoints = 1200f,
            TechLevel = TechLevel.Industrial, // 1.25 -> 1500
            FactionName = "Attackers",
            IsTradeCaravan = false
        };

        var sideB = new SkirmishSideData
        {
            CombatPoints = 800f,
            TechLevel = TechLevel.Medieval, // 1.0 -> 800
            FactionName = "Defenders",
            IsTradeCaravan = false
        };

        // Ratio: 1500 / 800 = 1.875 (between 1.4 and 2.0) -> 75% casualty fraction for loser
        var result = InterFactionEncounterManager.ResolveSkirmishAlgebraic(sideA, sideB, forcedRngA: 1.0f, forcedRngB: 1.0f);

        Assert.IsTrue(result.SideAWon);
        Assert.AreEqual(0.75f, result.SideBCasualtyFraction, 0.001f);
        Assert.AreEqual(200f, result.SideBRemainingPoints, 0.01f);
        Assert.IsFalse(result.SideBRoutedOrDestroyed, "200 remaining points (> 100) and 75% losses (< 80%) allows organized retreat.");
    }

    [TestMethod]
    public void WorldObject_EncounterAftermath_InspectString_ContainsExpectedSummaryKeywords()
    {
        var aftermath = new WorldObject_EncounterAftermath();
        aftermath.ExpiryTick = 10000;

        string inspect = aftermath.GetInspectString();
        Assert.IsTrue(inspect.Contains("Skirmish:"), "Inspect string must mention skirmish clash summary.");
        Assert.IsTrue(inspect.Contains("Aftermath:"), "Inspect string must mention cargo and survivor aftermath details.");
    }

    [TestMethod]
    public void IncursionInterceptionUtility_ExecuteHostileDemand_NullOrUnspawned_SafelyNoOps()
    {
        // Must not throw exception when passed nulls
        IncursionInterceptionUtility.ExecuteHostileDemand(null!, null!);
    }

    [TestMethod]
    public void Incursion_SkirmishCooldown_CanBeSetAndEvaluated()
    {
        var inc = new WorldObject_Incursion();
        Assert.AreEqual(0, inc.SkirmishCooldownExpiry);
        inc.SetSkirmishCooldown(15000);
        Assert.IsTrue(inc.SkirmishCooldownExpiry > 0, "Skirmish cooldown expiry must be positive when set.");
    }

    [TestMethod]
    public void Incursion_ApplySkirmishCasualties_ReturnsHarvestedPawnsList()
    {
        var inc = new WorldObject_Incursion();
        var casualties = inc.ApplySkirmishCasualties(0.5f, 200f);
        Assert.IsNotNull(casualties, "ApplySkirmishCasualties must return a non-null list of casualties.");
        Assert.AreEqual(0, casualties.Count, "Empty committed pawns should yield 0 extracted casualties.");
    }

    [TestMethod]
    public void CaravanArrivalAction_ScavengeAftermath_PropertiesAndValidation()
    {
        var action = new CaravanArrivalAction_ScavengeAftermath(null!);
        Assert.AreEqual("Scavenge aftermath", action.Label);
        Assert.AreEqual("travelling to encounter aftermath", action.ReportString);

        var report = action.StillValid(null!, (PlanetTile)100);
        Assert.IsFalse(report.Accepted, "StillValid must reject when aftermath object is null or unspawned.");
    }

    [TestMethod]
    public void HazeReconnaissanceManager_IsDynamicSite_RecognizesEncounterAftermath()
    {
        var aftermath = new WorldObject_EncounterAftermath();
        bool isDynamic = HazeReconnaissanceManager.IsDynamicSite(aftermath);
        Assert.IsTrue(isDynamic, "Encounter aftermath sites must be recognized as dynamic sites by HazeReconnaissanceManager.");
    }

    [TestMethod]
    public void WorldObject_EncounterAftermath_SetupAftermath_InitializesFactionsAndExpiry()
    {
        var aftermath = new WorldObject_EncounterAftermath();
        var res = new SkirmishResolution
        {
            SideAWon = true,
            SalvageLootValue = 400f,
            WoundedSurvivorCount = 2
        };

        aftermath.SetupAftermath(null, null, res, null, null, null);
        Assert.IsTrue(aftermath.ExpiryTick > 0, "Expiry tick must be scheduled into the future.");
        Assert.IsNotNull(aftermath.SalvageLoot, "SalvageLoot container must be initialized.");
    }
}
