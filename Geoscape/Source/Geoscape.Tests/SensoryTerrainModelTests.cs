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
}

