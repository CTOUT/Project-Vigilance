using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape.Sensory;
using Verse;

namespace Geoscape.Tests;

[TestClass]
public sealed class CompProperties_RadarTests
{
    [TestMethod]
    public void ConfigErrors_ValidBaseline_ReturnsZeroErrors()
    {
        // Arrange
        var props = new CompProperties_Radar
        {
            detectionRadius = 24,
            identificationRadiusDish1 = 10,
            identificationRadiusDish2 = 17,
            identificationRadiusDish3 = 22,
            standbyPowerDraw = 800f,
            activePowerDraw = 1800f,
            maxIntelligenceTier = IntelligenceTier.Classified,
            penetratesTerrainMasking = false,
            receivesVantageBonus = false,
            isOceanicSonar = false
        };

        // Act
        var errors = props.ConfigErrors(new ThingDef()).ToList();

        // Assert
        Assert.AreEqual(0, errors.Count, "Valid configuration should yield 0 errors.");
    }

    [TestMethod]
    public void ConfigErrors_NonPositiveDetectionRadius_ReturnsError()
    {
        // Arrange
        var props = new CompProperties_Radar
        {
            detectionRadius = 0,
            identificationRadiusDish1 = 10,
            identificationRadiusDish2 = 17,
            identificationRadiusDish3 = 22
        };

        // Act
        var errors = props.ConfigErrors(new ThingDef()).ToList();

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("detectionRadius must be greater than 0")));
    }

    [TestMethod]
    public void ConfigErrors_DishRadiusExceedingDetection_ReturnsError()
    {
        // Arrange
        var props = new CompProperties_Radar
        {
            detectionRadius = 20,
            identificationRadiusDish1 = 10,
            identificationRadiusDish2 = 17,
            identificationRadiusDish3 = 22
        };

        // Act
        var errors = props.ConfigErrors(new ThingDef()).ToList();

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("identificationRadiusDish3 cannot exceed detectionRadius")));
    }

    [TestMethod]
    public void ConfigErrors_NonMonotonicProgression_ReturnsError()
    {
        // Arrange
        var props = new CompProperties_Radar
        {
            detectionRadius = 24,
            identificationRadiusDish1 = 15,
            identificationRadiusDish2 = 12,
            identificationRadiusDish3 = 22
        };

        // Act
        var errors = props.ConfigErrors(new ThingDef()).ToList();

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("monotonically non-decreasing")));
    }

    [TestMethod]
    public void ConfigErrors_ActivePowerLowerThanStandby_ReturnsError()
    {
        // Arrange
        var props = new CompProperties_Radar
        {
            detectionRadius = 24,
            identificationRadiusDish1 = 10,
            identificationRadiusDish2 = 17,
            identificationRadiusDish3 = 22,
            standbyPowerDraw = 500f,
            activePowerDraw = 300f
        };

        // Act
        var errors = props.ConfigErrors(new ThingDef()).ToList();

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("activePowerDraw must be greater than or equal to standbyPowerDraw")));
    }

    [TestMethod]
    public void ConfigErrors_NegativePowerDraw_ReturnsError()
    {
        // Arrange
        var props = new CompProperties_Radar
        {
            detectionRadius = 24,
            identificationRadiusDish1 = 10,
            identificationRadiusDish2 = 17,
            identificationRadiusDish3 = 22,
            standbyPowerDraw = -50f,
            activePowerDraw = 100f
        };

        // Act
        var errors = props.ConfigErrors(new ThingDef()).ToList();

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("standbyPowerDraw cannot be negative")));
    }

    [TestMethod]
    public void ConfigErrors_OceanicSonarWithVantageBonus_ReturnsError()
    {
        // Arrange
        var props = new CompProperties_Radar
        {
            detectionRadius = 18,
            identificationRadiusDish1 = 8,
            identificationRadiusDish2 = 8,
            identificationRadiusDish3 = 8,
            isOceanicSonar = true,
            receivesVantageBonus = true
        };

        // Act
        var errors = props.ConfigErrors(new ThingDef()).ToList();

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("isOceanicSonar is incompatible with receivesVantageBonus")));
    }

    [TestMethod]
    public void ConfigErrors_PenetratesTerrainMaskingWithoutClassifiedTier_ReturnsError()
    {
        // Arrange
        var props = new CompProperties_Radar
        {
            detectionRadius = 24,
            identificationRadiusDish1 = 10,
            identificationRadiusDish2 = 17,
            identificationRadiusDish3 = 22,
            penetratesTerrainMasking = true,
            maxIntelligenceTier = IntelligenceTier.Contact
        };

        // Act
        var errors = props.ConfigErrors(new ThingDef()).ToList();

        // Assert
        Assert.IsTrue(errors.Any(e => e.Contains("penetratesTerrainMasking requires maxIntelligenceTier to be at least Classified")));
    }
}
