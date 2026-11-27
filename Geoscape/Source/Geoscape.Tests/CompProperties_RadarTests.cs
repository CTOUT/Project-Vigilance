using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape.Sensory;
using Verse;

namespace Geoscape.Tests;

[TestClass]
public sealed class CompProperties_RadarTests
{
    private static CompProperties_Radar CreateBaselineProps(
        int detectionRadius = 24,
        int id1 = 10,
        int id2 = 17,
        int id3 = 22,
        float standbyPowerDraw = 800f,
        float activePowerDraw = 1800f,
        IntelligenceTier maxTier = IntelligenceTier.Classified,
        bool penetratesMasking = false,
        bool receivesVantage = false,
        bool isSonar = false) => new()
    {
        detectionRadius = detectionRadius,
        identificationRadiusDish1 = id1,
        identificationRadiusDish2 = id2,
        identificationRadiusDish3 = id3,
        standbyPowerDraw = standbyPowerDraw,
        activePowerDraw = activePowerDraw,
        maxIntelligenceTier = maxTier,
        penetratesTerrainMasking = penetratesMasking,
        receivesVantageBonus = receivesVantage,
        isOceanicSonar = isSonar
    };

    [TestMethod]
    public void ConfigErrors_ValidBaseline_ReturnsZeroErrors()
    {
        var errors = CreateBaselineProps().ConfigErrors(new ThingDef()).ToList();
        Assert.AreEqual(0, errors.Count, "Valid configuration should yield 0 errors.");
    }

    [TestMethod]
    public void ConfigErrors_NonPositiveDetectionRadius_ReturnsError()
    {
        var errors = CreateBaselineProps(detectionRadius: 0).ConfigErrors(new ThingDef()).ToList();
        Assert.IsTrue(errors.Any(e => e.Contains("detectionRadius must be greater than 0")));
    }

    [TestMethod]
    public void ConfigErrors_DishRadiusExceedingDetection_ReturnsError()
    {
        var errors = CreateBaselineProps(detectionRadius: 20).ConfigErrors(new ThingDef()).ToList();
        Assert.IsTrue(errors.Any(e => e.Contains("identificationRadiusDish3 cannot exceed detectionRadius")));
    }

    [TestMethod]
    public void ConfigErrors_NonMonotonicProgression_ReturnsError()
    {
        var errors = CreateBaselineProps(id1: 15, id2: 12, id3: 22).ConfigErrors(new ThingDef()).ToList();
        Assert.IsTrue(errors.Any(e => e.Contains("monotonically non-decreasing")));
    }

    [TestMethod]
    public void ConfigErrors_ActivePowerLowerThanStandby_ReturnsError()
    {
        var errors = CreateBaselineProps(standbyPowerDraw: 500f, activePowerDraw: 300f).ConfigErrors(new ThingDef()).ToList();
        Assert.IsTrue(errors.Any(e => e.Contains("activePowerDraw must be greater than or equal to standbyPowerDraw")));
    }

    [TestMethod]
    public void ConfigErrors_NegativePowerDraw_ReturnsError()
    {
        var errors = CreateBaselineProps(standbyPowerDraw: -50f, activePowerDraw: 100f).ConfigErrors(new ThingDef()).ToList();
        Assert.IsTrue(errors.Any(e => e.Contains("standbyPowerDraw cannot be negative")));
    }

    [TestMethod]
    public void ConfigErrors_OceanicSonarWithVantageBonus_ReturnsError()
    {
        var errors = CreateBaselineProps(detectionRadius: 18, id1: 8, id2: 8, id3: 8, receivesVantage: true, isSonar: true)
            .ConfigErrors(new ThingDef()).ToList();
        Assert.IsTrue(errors.Any(e => e.Contains("isOceanicSonar is incompatible with receivesVantageBonus")));
    }

    [TestMethod]
    public void ConfigErrors_PenetratesTerrainMaskingWithoutClassifiedTier_ReturnsError()
    {
        var errors = CreateBaselineProps(penetratesMasking: true, maxTier: IntelligenceTier.Contact)
            .ConfigErrors(new ThingDef()).ToList();
        Assert.IsTrue(errors.Any(e => e.Contains("penetratesTerrainMasking requires maxIntelligenceTier to be at least Classified")));
    }

    [TestMethod]
    [DataRow(1, 24, 10, 17, 22, false, 0, 10)]
    [DataRow(2, 24, 10, 17, 22, false, 0, 17)]
    [DataRow(3, 24, 10, 17, 22, false, 0, 22)]
    [DataRow(5, 24, 10, 17, 22, false, 0, 22)]
    [DataRow(1, 8, 4, 4, 4, true, 3, 6)]
    public void ResolveIdentificationRadius_DishCountAndVantage_ReturnsExpectedRadius(
        int dishCount,
        int detectionRadius,
        int id1,
        int id2,
        int id3,
        bool receivesVantage,
        int vantageBonus,
        int expectedIdRadius)
    {
        var props = CreateBaselineProps(
            detectionRadius: detectionRadius,
            id1: id1,
            id2: id2,
            id3: id3,
            receivesVantage: receivesVantage);

        Assert.AreEqual(expectedIdRadius, props.ResolveIdentificationRadius(dishCount, vantageBonus));
    }

    [TestMethod]
    public void ConfigErrors_ContactOnlySensorWithZeroIdentificationRadius_ReturnsZeroErrors()
    {
        var props = CreateBaselineProps(
            detectionRadius: 14,
            id1: 0,
            id2: 0,
            id3: 0,
            maxTier: IntelligenceTier.Contact);

        var errors = props.ConfigErrors(new ThingDef()).ToList();
        Assert.AreEqual(0, errors.Count, "Contact-only early warning sensors with 0 identification radius must validate cleanly.");
        Assert.AreEqual(0, props.ResolveIdentificationRadius(1, vantageBonus: 3));
    }

    [TestMethod]
    public void ResolveIdentificationRadius_ContactOnlyTier_AlwaysReturnsZeroEvenIfRadiiConfigured()
    {
        var props = CreateBaselineProps(
            detectionRadius: 14,
            id1: 6,
            id2: 6,
            id3: 6,
            maxTier: IntelligenceTier.Contact,
            receivesVantage: true);

        Assert.AreEqual(0, props.ResolveIdentificationRadius(1, vantageBonus: 2));
        Assert.AreEqual(0, props.ResolveIdentificationRadius(3, vantageBonus: 3));
    }

    [TestMethod]
    public void CompSigintConsole_GetOfflineReason_Unspawned_ReturnsUnspawned()
    {
        var console = new CompSigintConsole();
        Assert.IsFalse(console.IsOperational);
        Assert.AreEqual("Unspawned", console.GetOfflineReason());
        Assert.AreEqual(0, console.LinkedDishesCount);
    }
}
