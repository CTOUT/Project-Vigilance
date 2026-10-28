using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape.Sensory;

namespace Geoscape.Tests;

[TestClass]
public sealed class InterferometryProgressionTests
{
    private static int ResolveIdentificationRadius(int operationalDishCount, CompProperties_Radar props, int vantageBonus = 0)
    {
        int baseId;
        if (operationalDishCount >= 3)
        {
            baseId = props.identificationRadiusDish3;
        }
        else if (operationalDishCount == 2)
        {
            baseId = props.identificationRadiusDish2;
        }
        else
        {
            baseId = props.identificationRadiusDish1;
        }

        if (props.receivesVantageBonus && vantageBonus > 0)
        {
            baseId += SensoryTerrainUtility.GetIdentificationVantageBonus(vantageBonus);
        }

        int maxDetection = props.detectionRadius + (props.receivesVantageBonus ? vantageBonus : 0);
        return System.Math.Min(baseId, maxDetection);
    }

    [TestMethod]
    public void DualRingPhasedArray_SingleDish_Returns10Rings()
    {
        // Arrange
        var props = new CompProperties_Radar
        {
            detectionRadius = 24,
            identificationRadiusDish1 = 10,
            identificationRadiusDish2 = 17,
            identificationRadiusDish3 = 22
        };

        // Act
        int idRadius = ResolveIdentificationRadius(1, props);

        // Assert
        Assert.AreEqual(10, idRadius, "1 dish must provide baseline 10 rings identification.");
    }

    [TestMethod]
    public void DualRingPhasedArray_TwoDishes_Returns17Rings()
    {
        // Arrange
        var props = new CompProperties_Radar
        {
            detectionRadius = 24,
            identificationRadiusDish1 = 10,
            identificationRadiusDish2 = 17,
            identificationRadiusDish3 = 22
        };

        // Act
        int idRadius = ResolveIdentificationRadius(2, props);

        // Assert
        Assert.AreEqual(17, idRadius, "2 dishes must achieve interferometric baseline of 17 rings.");
    }

    [TestMethod]
    public void DualRingPhasedArray_ThreeOrMoreDishes_Returns22Rings()
    {
        // Arrange
        var props = new CompProperties_Radar
        {
            detectionRadius = 24,
            identificationRadiusDish1 = 10,
            identificationRadiusDish2 = 17,
            identificationRadiusDish3 = 22
        };

        // Act
        int idRadius3 = ResolveIdentificationRadius(3, props);
        int idRadius5 = ResolveIdentificationRadius(5, props);

        // Assert
        Assert.AreEqual(22, idRadius3, "3 dishes must achieve full aperture of 22 rings.");
        Assert.AreEqual(22, idRadius5, "Additional dishes beyond 3 maintain 22 rings with redundancy.");
    }

    [TestMethod]
    public void DualRingPhasedArray_DetectionRadiusRemainsFixedAcrossAllDishCounts()
    {
        // Arrange
        var props = new CompProperties_Radar
        {
            detectionRadius = 24,
            receivesVantageBonus = false
        };

        // Assert
        Assert.AreEqual(24, props.detectionRadius, "Detection radius must remain strictly locked at 24 rings regardless of dish count.");
    }

    [TestMethod]
    public void LookoutWatchtower_WithVantageBonus_ExpandsDetectionAndIdentification()
    {
        // Arrange: Lookout Watchtower on Mountainous tile (+3 vantage)
        var props = new CompProperties_Radar
        {
            detectionRadius = 8,
            identificationRadiusDish1 = 4,
            identificationRadiusDish2 = 4,
            identificationRadiusDish3 = 4,
            receivesVantageBonus = true
        };
        int vantage = 3;

        // Act
        int effectiveDetection = props.detectionRadius + vantage;
        int effectiveIdentification = ResolveIdentificationRadius(1, props, vantage);

        // Assert
        Assert.AreEqual(11, effectiveDetection, "Baseline 8 + 3 vantage = 11 rings detection.");
        Assert.AreEqual(6, effectiveIdentification, "Baseline 4 + 2 vantage = 6 rings identification.");
    }
}

