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

    private sealed class MockTerrainSensor : ISensorProvider
    {
        public int IdentificationRadiusInTiles { get; set; } = 10;
        public float DetectionRadiusInTiles => 24f;
        public IntelligenceTier MaxIntelligenceTier { get; set; } = IntelligenceTier.Classified;
        public bool PenetratesTerrainMasking { get; set; }
        public int SigintPenetrationRadiusInTiles { get; set; } = 0;
        public bool IsSensorActive => true;
        public bool IsStationary => true;
        public bool IsTechnological => true;
        public bool IsOceanicSonar => false;
        public string SensorLabel => "Mock Sensor";
        public RimWorld.Planet.PlanetTile SensorTile => (RimWorld.Planet.PlanetTile)0;
        public RimWorld.Planet.WorldObject? Source => null;
    }

    [TestMethod]
    public void TerrainMaskingGating_MountainousTileWithoutPenetration_HeldAtContactTier()
    {
        var sensor = new MockTerrainSensor
        {
            IdentificationRadiusInTiles = 10,
            PenetratesTerrainMasking = false,
            MaxIntelligenceTier = IntelligenceTier.Classified
        };

        IntelligenceTier tier = SensoryTerrainUtility.EvaluateSensorTier(sensor, travDist: 5, isTargetMasked: true);

        Assert.AreEqual(IntelligenceTier.Contact, tier, "Masked target without penetration must be held at Contact tier.");
    }

    [TestMethod]
    public void TerrainMaskingGating_MountainousTileWithPenetration_ResolvesFullClassifiedTier()
    {
        var sensor = new MockTerrainSensor
        {
            IdentificationRadiusInTiles = 10,
            PenetratesTerrainMasking = true,
            MaxIntelligenceTier = IntelligenceTier.Classified
        };

        IntelligenceTier tier = SensoryTerrainUtility.EvaluateSensorTier(sensor, travDist: 5, isTargetMasked: true);

        Assert.AreEqual(IntelligenceTier.Classified, tier, "Masked target with penetration must resolve full Classified tier.");
    }

    [TestMethod]
    public void TerrainMaskingGating_UnmaskedTile_ResolvesFullAnalysedTier()
    {
        var sensor = new MockTerrainSensor
        {
            IdentificationRadiusInTiles = 10,
            PenetratesTerrainMasking = false,
            MaxIntelligenceTier = IntelligenceTier.Analysed
        };

        IntelligenceTier tier = SensoryTerrainUtility.EvaluateSensorTier(sensor, travDist: 5, isTargetMasked: false);

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
        var radarProps1Dish = new CompProperties_Radar
        {
            detectionRadius = 24,
            identificationRadiusDish1 = 10,
            identificationRadiusDish2 = 17,
            identificationRadiusDish3 = 22,
            receivesVantageBonus = true
        };
        Assert.AreEqual(10, radarProps1Dish.ResolveIdentificationRadius(1, 0), "1-dish radar on flat terrain gives 10 rings ID.");
        Assert.AreEqual(11, radarProps1Dish.ResolveIdentificationRadius(1, 1), "1-dish radar on hills gives 11 rings ID.");
        Assert.AreEqual(12, radarProps1Dish.ResolveIdentificationRadius(1, 3), "1-dish radar on mountains gives 12 rings ID.");

        // 2. High-Tech Radar with 3 Dishes (baseline 22) capped by detection (24)
        Assert.AreEqual(24, radarProps1Dish.ResolveIdentificationRadius(3, 3), "3-dish radar with vantage reaches maximum 24 detection boundary.");

        // 3. Automated Relay / Manned Outposts (baseline 5 ID) with vantage
        Assert.AreEqual(5, WorldObject_ListeningPost.CalculateRadii(ListeningPostKind.AutomatedRelay, 0).identification, "Automated relay baseline is 5 rings ID.");
        Assert.AreEqual(6, WorldObject_ListeningPost.CalculateRadii(ListeningPostKind.AutomatedRelay, 2).identification, "Automated relay on large hills reaches 6 rings ID.");
        Assert.AreEqual(6, WorldObject_ListeningPost.CalculateRadii(ListeningPostKind.AutomatedRelay, 3).identification, "Automated relay on mountains caps at 6 rings ID.");

        // 4. Scout Horizon Colony Lookouts (baseline 2 ID)
        int flatColonyId = 2 + SensoryTerrainUtility.GetIdentificationVantageBonus(0);
        int hillsColonyId = 2 + SensoryTerrainUtility.GetIdentificationVantageBonus(2);
        int mtnColonyId = 2 + SensoryTerrainUtility.GetIdentificationVantageBonus(3);
        Assert.AreEqual(2, flatColonyId, "Colony lookouts on flat terrain gives 2 rings ID.");
        Assert.AreEqual(3, hillsColonyId, "Colony lookouts on large hills gives 3 rings ID.");
        Assert.AreEqual(4, mtnColonyId, "Colony lookouts on mountains gives 4 rings ID.");
    }

    [TestMethod]
    public void ScaledSigintCorrelator_OneWiredDish_AnalysedInside10Rings_FallsBackBeyond10Rings()
    {
        // 3-dish array (22 rings ID horizon) with only 1 dish wired to SIGINT (10 rings bandwidth)
        var sensor = new MockTerrainSensor
        {
            IdentificationRadiusInTiles = 22,
            SigintPenetrationRadiusInTiles = 10,
            PenetratesTerrainMasking = true,
            MaxIntelligenceTier = IntelligenceTier.Analysed
        };

        // Within 10 rings: full Analysed tier & mountain penetration
        Assert.AreEqual(IntelligenceTier.Analysed, SensoryTerrainUtility.EvaluateSensorTier(sensor, travDist: 5, isTargetMasked: true),
            "Step 5 in mountains must resolve Analysed (penetrated by 10-ring SIGINT).");
        Assert.AreEqual(IntelligenceTier.Analysed, SensoryTerrainUtility.EvaluateSensorTier(sensor, travDist: 10, isTargetMasked: false),
            "Step 10 in open ground must resolve Analysed.");

        // Beyond 10 rings (steps 11-22): falls back to Classified in open ground, or Contact in mountains
        Assert.AreEqual(IntelligenceTier.Classified, SensoryTerrainUtility.EvaluateSensorTier(sensor, travDist: 15, isTargetMasked: false),
            "Step 15 in open ground must fall back to Classified (beyond 10-ring SIGINT).");
        Assert.AreEqual(IntelligenceTier.Contact, SensoryTerrainUtility.EvaluateSensorTier(sensor, travDist: 15, isTargetMasked: true),
            "Step 15 in mountains must fall back to Contact (beyond 10-ring SIGINT).");

        // Beyond 22 rings (detection boundary 24): Contact
        Assert.AreEqual(IntelligenceTier.Contact, SensoryTerrainUtility.EvaluateSensorTier(sensor, travDist: 23, isTargetMasked: false),
            "Step 23 must resolve Contact (outside 22-ring ID horizon, inside 24-ring detection).");
    }

    [TestMethod]
    public void ScaledSigintCorrelator_TwoWiredDishes_AnalysedInside17Rings_FallsBackBeyond17Rings()
    {
        // 3-dish array (22 rings ID horizon) with 2 dishes wired to SIGINT (17 rings bandwidth)
        var sensor = new MockTerrainSensor
        {
            IdentificationRadiusInTiles = 22,
            SigintPenetrationRadiusInTiles = 17,
            PenetratesTerrainMasking = true,
            MaxIntelligenceTier = IntelligenceTier.Analysed
        };

        // Within 17 rings: Analysed
        Assert.AreEqual(IntelligenceTier.Analysed, SensoryTerrainUtility.EvaluateSensorTier(sensor, travDist: 15, isTargetMasked: true),
            "Step 15 in mountains must resolve Analysed (penetrated by 17-ring SIGINT).");

        // Beyond 17 rings: Classified in open, Contact in mountains
        Assert.AreEqual(IntelligenceTier.Classified, SensoryTerrainUtility.EvaluateSensorTier(sensor, travDist: 20, isTargetMasked: false),
            "Step 20 in open ground must fall back to Classified (beyond 17-ring SIGINT).");
        Assert.AreEqual(IntelligenceTier.Contact, SensoryTerrainUtility.EvaluateSensorTier(sensor, travDist: 20, isTargetMasked: true),
            "Step 20 in mountains must fall back to Contact (beyond 17-ring SIGINT).");
    }

    [TestMethod]
    public void ScaledSigintCorrelator_ThreeWiredDishes_AnalysedOutTo22Rings()
    {
        // 3-dish array with 3 dishes wired to SIGINT (22 rings bandwidth)
        var sensor = new MockTerrainSensor
        {
            IdentificationRadiusInTiles = 22,
            SigintPenetrationRadiusInTiles = 22,
            PenetratesTerrainMasking = true,
            MaxIntelligenceTier = IntelligenceTier.Analysed
        };

        // Full 22-ring penetration
        Assert.AreEqual(IntelligenceTier.Analysed, SensoryTerrainUtility.EvaluateSensorTier(sensor, travDist: 20, isTargetMasked: true),
            "Step 20 in mountains must resolve Analysed with full 3-dish SIGINT.");
        Assert.AreEqual(IntelligenceTier.Analysed, SensoryTerrainUtility.EvaluateSensorTier(sensor, travDist: 22, isTargetMasked: true),
            "Step 22 in mountains must resolve Analysed with full 3-dish SIGINT.");
    }
}

