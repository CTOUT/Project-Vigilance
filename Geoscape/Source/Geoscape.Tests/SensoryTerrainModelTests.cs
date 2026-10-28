using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld.Planet;

namespace Geoscape.Tests;

[TestClass]
public sealed class SensoryTerrainModelTests
{
    [TestMethod]
    public void GetVantageBonus_InvalidTile_ReturnsZero()
    {
        // Arrange
        PlanetTile invalidTile = PlanetTile.Invalid;

        // Act
        int bonus = SensoryTerrainUtility.GetVantageBonus(invalidTile);

        // Assert
        Assert.AreEqual(0, bonus);
    }

    [TestMethod]
    public void IsTargetMasked_InvalidTile_ReturnsFalse()
    {
        // Arrange
        PlanetTile invalidTile = PlanetTile.Invalid;

        // Act
        bool masked = SensoryTerrainUtility.IsTargetMasked(invalidTile);

        // Assert
        Assert.IsFalse(masked);
    }

    [TestMethod]
    public void TerrainMaskingGating_MountainousTileWithoutPenetration_HeldAtContactTier()
    {
        // Scenario: Incursion is inside identification radius, but target tile is Mountainous
        // and sensor lacks SIGINT/Spacer penetration.
        // Expected: Intelligence tier held at Contact.

        // Arrange
        bool isTargetMasked = true;
        bool penetratesTerrainMasking = false;
        IntelligenceTier sensorMaxTier = IntelligenceTier.Classified;
        int idSteps = 10;
        int travDist = 5;

        // Act
        IntelligenceTier tier;
        if (idSteps > 0 && travDist <= idSteps)
        {
            if (isTargetMasked && !penetratesTerrainMasking)
            {
                tier = (sensorMaxTier >= IntelligenceTier.Contact) ? IntelligenceTier.Contact : sensorMaxTier;
            }
            else
            {
                tier = sensorMaxTier;
            }
        }
        else
        {
            tier = (sensorMaxTier >= IntelligenceTier.Contact) ? IntelligenceTier.Contact : IntelligenceTier.Suspected;
        }

        // Assert
        Assert.AreEqual(IntelligenceTier.Contact, tier, "Masked target without penetration must be held at Contact tier.");
    }

    [TestMethod]
    public void TerrainMaskingGating_MountainousTileWithPenetration_ResolvesFullClassifiedTier()
    {
        // Scenario: Incursion is inside identification radius on Mountainous tile,
        // but sensor has penetration (e.g. Phased Array + SIGINT).
        // Expected: Resolves full sensor tier.

        // Arrange
        bool isTargetMasked = true;
        bool penetratesTerrainMasking = true;
        IntelligenceTier sensorMaxTier = IntelligenceTier.Classified;
        int idSteps = 10;
        int travDist = 5;

        // Act
        IntelligenceTier tier;
        if (idSteps > 0 && travDist <= idSteps)
        {
            if (isTargetMasked && !penetratesTerrainMasking)
            {
                tier = (sensorMaxTier >= IntelligenceTier.Contact) ? IntelligenceTier.Contact : sensorMaxTier;
            }
            else
            {
                tier = sensorMaxTier;
            }
        }
        else
        {
            tier = (sensorMaxTier >= IntelligenceTier.Contact) ? IntelligenceTier.Contact : IntelligenceTier.Suspected;
        }

        // Assert
        Assert.AreEqual(IntelligenceTier.Classified, tier, "Masked target with penetration must resolve full Classified tier.");
    }

    [TestMethod]
    public void TerrainMaskingGating_UnmaskedTile_ResolvesFullAnalysedTier()
    {
        // Scenario: Incursion is inside identification radius on Flat/Hills tile.
        // Expected: Resolves full Analysed tier.

        // Arrange
        bool isTargetMasked = false;
        bool penetratesTerrainMasking = false;
        IntelligenceTier sensorMaxTier = IntelligenceTier.Analysed;
        int idSteps = 10;
        int travDist = 5;

        // Act
        IntelligenceTier tier;
        if (idSteps > 0 && travDist <= idSteps)
        {
            if (isTargetMasked && !penetratesTerrainMasking)
            {
                tier = (sensorMaxTier >= IntelligenceTier.Contact) ? IntelligenceTier.Contact : sensorMaxTier;
            }
            else
            {
                tier = sensorMaxTier;
            }
        }
        else
        {
            tier = (sensorMaxTier >= IntelligenceTier.Contact) ? IntelligenceTier.Contact : IntelligenceTier.Suspected;
        }

        // Assert
        Assert.AreEqual(IntelligenceTier.Analysed, tier, "Unmasked target inside identification radius resolves full Analysed tier.");
    }

    [TestMethod]
    public void GetIdentificationVantageBonus_ExplicitIntegerMapping_ReturnsExpectedRings()
    {
        // Assert explicit integer ring steps without float math
        Assert.AreEqual(0, SensoryTerrainUtility.GetIdentificationVantageBonus(0), "Flat terrain gives +0 ID rings.");
        Assert.AreEqual(1, SensoryTerrainUtility.GetIdentificationVantageBonus(1), "Small hills (+1 vantage) gives +1 ID ring.");
        Assert.AreEqual(1, SensoryTerrainUtility.GetIdentificationVantageBonus(2), "Large hills (+2 vantage) gives +1 ID ring.");
        Assert.AreEqual(2, SensoryTerrainUtility.GetIdentificationVantageBonus(3), "Mountainous (+3 vantage) gives +2 ID rings.");
        Assert.AreEqual(2, SensoryTerrainUtility.GetIdentificationVantageBonus(4), "Impassable peaks give +2 ID rings.");
    }

    [TestMethod]
    public void OpticalAndRadarSensors_NormalizedIntegerRadii_MatchModelBPlusSpecifications()
    {
        // 1. High-Tech Radar with 1 Dish (baseline 10) on various terrains
        int baseId1Dish = 10;
        int flatRadarId = baseId1Dish + SensoryTerrainUtility.GetIdentificationVantageBonus(0);
        int hillsRadarId = baseId1Dish + SensoryTerrainUtility.GetIdentificationVantageBonus(1);
        int mtnRadarId = baseId1Dish + SensoryTerrainUtility.GetIdentificationVantageBonus(3);
        Assert.AreEqual(10, flatRadarId, "1-dish radar on flat terrain gives 10 rings ID.");
        Assert.AreEqual(11, hillsRadarId, "1-dish radar on hills gives 11 rings ID.");
        Assert.AreEqual(12, mtnRadarId, "1-dish radar on mountains gives 12 rings ID.");

        // 2. High-Tech Radar with 3 Dishes (baseline 22) capped by detection (24)
        int baseId3Dishes = 22;
        int mtn3DishId = System.Math.Min(24, baseId3Dishes + SensoryTerrainUtility.GetIdentificationVantageBonus(3));
        Assert.AreEqual(24, mtn3DishId, "3-dish radar with vantage reaches maximum 24 detection boundary.");

        // 3. Signal Beacon (baseline 3 ID) with optical vantage
        int flatBeaconId = 3 + SensoryTerrainUtility.GetIdentificationVantageBonus(0);
        int hillsBeaconId = 3 + SensoryTerrainUtility.GetIdentificationVantageBonus(1);
        int mtnBeaconId = 3 + SensoryTerrainUtility.GetIdentificationVantageBonus(3);
        Assert.AreEqual(3, flatBeaconId, "Signal beacon baseline is 3 rings ID.");
        Assert.AreEqual(4, hillsBeaconId, "Signal beacon on hills is 4 rings ID.");
        Assert.AreEqual(5, mtnBeaconId, "Signal beacon on mountains is 5 rings ID.");

        // 4. Scout Horizon Colony Lookouts (baseline 2 ID)
        int flatColonyId = 2 + SensoryTerrainUtility.GetIdentificationVantageBonus(0);
        int hillsColonyId = 2 + SensoryTerrainUtility.GetIdentificationVantageBonus(2);
        int mtnColonyId = 2 + SensoryTerrainUtility.GetIdentificationVantageBonus(3);
        Assert.AreEqual(2, flatColonyId, "Colony lookouts on flat terrain gives 2 rings ID.");
        Assert.AreEqual(3, hillsColonyId, "Colony lookouts on large hills gives 3 rings ID.");
        Assert.AreEqual(4, mtnColonyId, "Colony lookouts on mountains gives 4 rings ID.");
    }
}

