using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld.Planet;

namespace Geoscape.Tests;

[TestClass]
public sealed class SensoryTerrainModelTests
{
    [TestMethod]
    public void InvalidTiles_ReturnSafeDefaults()
    {
        Assert.AreEqual(0, SensoryTerrainUtility.GetVantageBonus(PlanetTile.Invalid));
        Assert.IsFalse(SensoryTerrainUtility.IsTargetMasked(PlanetTile.Invalid));
    }

    [TestMethod]
    [DataRow(10, 0, false, IntelligenceTier.Classified, 5, true, IntelligenceTier.Contact)]
    [DataRow(10, 0, true, IntelligenceTier.Classified, 5, true, IntelligenceTier.Classified)]
    [DataRow(10, 0, false, IntelligenceTier.Analysed, 5, false, IntelligenceTier.Analysed)]
    [DataRow(22, 10, true, IntelligenceTier.Analysed, 5, true, IntelligenceTier.Analysed)]
    [DataRow(22, 10, true, IntelligenceTier.Analysed, 10, false, IntelligenceTier.Analysed)]
    [DataRow(22, 10, true, IntelligenceTier.Analysed, 15, false, IntelligenceTier.Classified)]
    [DataRow(22, 10, true, IntelligenceTier.Analysed, 15, true, IntelligenceTier.Contact)]
    [DataRow(22, 10, true, IntelligenceTier.Analysed, 23, false, IntelligenceTier.Contact)]
    [DataRow(22, 17, true, IntelligenceTier.Analysed, 15, true, IntelligenceTier.Analysed)]
    [DataRow(22, 17, true, IntelligenceTier.Analysed, 20, false, IntelligenceTier.Classified)]
    [DataRow(22, 17, true, IntelligenceTier.Analysed, 20, true, IntelligenceTier.Contact)]
    [DataRow(22, 22, true, IntelligenceTier.Analysed, 20, true, IntelligenceTier.Analysed)]
    [DataRow(22, 22, true, IntelligenceTier.Analysed, 22, true, IntelligenceTier.Analysed)]
    public void EvaluateSensorTier_TerrainMaskingAndScaledSigint_ReturnsExpectedTier(
        int idRadius,
        int sigintRadius,
        bool penetratesMasking,
        IntelligenceTier maxTier,
        int travDist,
        bool isTargetMasked,
        IntelligenceTier expectedTier)
    {
        var sensor = new MockSensorProvider
        {
            SensorTile = (PlanetTile)0,
            DetectionRadiusInTiles = 24f,
            IdentificationRadiusInTiles = idRadius,
            SigintPenetrationRadiusInTiles = sigintRadius,
            PenetratesTerrainMasking = penetratesMasking,
            MaxIntelligenceTier = maxTier
        };

        Assert.AreEqual(
            expectedTier,
            SensoryTerrainUtility.EvaluateSensorTier(sensor, travDist, isTargetMasked));
    }

    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(1, 1)]
    [DataRow(2, 1)]
    [DataRow(3, 2)]
    [DataRow(4, 2)]
    public void GetIdentificationVantageBonus_ExplicitIntegerMapping_ReturnsExpectedRings(
        int detectionVantageBonus,
        int expectedIdRings)
    {
        Assert.AreEqual(
            expectedIdRings,
            SensoryTerrainUtility.GetIdentificationVantageBonus(detectionVantageBonus));
    }

    [TestMethod]
    public void OpticalAndRadarSensors_NormalizedIntegerRadii_MatchModelBPlusSpecifications()
    {
        var radarProps = new CompProperties_Radar
        {
            detectionRadius = 24,
            identificationRadiusDish1 = 10,
            identificationRadiusDish2 = 17,
            identificationRadiusDish3 = 22,
            maxIntelligenceTier = IntelligenceTier.Classified,
            receivesVantageBonus = true
        };
        Assert.AreEqual(10, radarProps.ResolveIdentificationRadius(1, 0), "1-dish radar on flat terrain gives 10 rings ID.");
        Assert.AreEqual(11, radarProps.ResolveIdentificationRadius(1, 1), "1-dish radar on hills gives 11 rings ID.");
        Assert.AreEqual(12, radarProps.ResolveIdentificationRadius(1, 3), "1-dish radar on mountains gives 12 rings ID.");
        Assert.AreEqual(24, radarProps.ResolveIdentificationRadius(3, 3), "3-dish radar with vantage reaches maximum 24 detection boundary.");

        Assert.AreEqual(5, WorldObject_ListeningPost.CalculateRadii(ListeningPostKind.AutomatedRelay, 0).identification, "Automated relay baseline is 5 rings ID.");
        Assert.AreEqual(6, WorldObject_ListeningPost.CalculateRadii(ListeningPostKind.AutomatedRelay, 2).identification, "Automated relay on large hills reaches 6 rings ID.");
        Assert.AreEqual(6, WorldObject_ListeningPost.CalculateRadii(ListeningPostKind.AutomatedRelay, 3).identification, "Automated relay on mountains caps at 6 rings ID.");
    }
}
