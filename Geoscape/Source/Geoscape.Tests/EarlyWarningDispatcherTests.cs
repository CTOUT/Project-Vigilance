using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape;
using ProjectVigilance.Geoscape.Sensory;
using ProjectVigilance.Geoscape.World;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Geoscape.Tests;

public class MockSensorProvider : ISensorProvider
{
    public PlanetTile SensorTile { get; set; } = PlanetTile.Invalid;
    public float DetectionRadiusInTiles { get; set; } = 24f;
    public int IdentificationRadiusInTiles { get; set; } = 10;
    public IntelligenceTier MaxIntelligenceTier { get; set; } = IntelligenceTier.Classified;
    public bool IsSensorActive { get; set; } = true;
    public bool IsTechnological { get; set; } = true;
    public bool IsStationary { get; set; } = true;
    public bool PenetratesTerrainMasking { get; set; } = false;
    public bool IsOceanicSonar { get; set; } = false;
    public string SensorLabel { get; set; } = "Early-Warning Radar";
    public WorldObject? Source => null;

    public bool CanDetect(IDetectableTarget target) => true;
}

[TestClass]
public sealed class EarlyWarningDispatcherTests
{
    [TestInitialize]
    public void Setup()
    {
        GeoscapeMod.Settings.enableEarlyWarningLetters = true;
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;
    }

    [TestMethod]
    public void ProcessDetection_Stage1ContactTier_DispatchesThreatSmallAndSetsFlags()
    {
        // Arrange
        var incursion = new WorldObject_Incursion();
        var sensor = new MockSensorProvider();
        LetterDef? dispatchedDef = null;
        TaggedString dispatchedTitle = default;

        EarlyWarningDispatcher.OnLetterDispatched += HandleLetter;
        try
        {
            // Act: Crosses R_detect into Contact tier
            EarlyWarningDispatcher.ProcessDetection(incursion, sensor, IntelligenceTier.Contact);

            // Assert
            Assert.AreEqual(IntelligenceTier.Contact, incursion.CurrentIntelligenceTier);
            Assert.IsTrue(incursion.HasDispatchedContactAlert, "Stage 1 contact alert must be marked dispatched.");
            Assert.IsFalse(incursion.HasDispatchedClassifiedAlert, "Stage 2 classified alert must not yet be dispatched.");
            Assert.IsFalse(incursion.IsIdentified, "Incursion must remain unidentified at Contact tier.");
            Assert.IsTrue(incursion.TrackingId > 0, "Tracking ID must be allocated.");

            Assert.AreEqual(LetterDefOf.ThreatSmall, dispatchedDef, "Stage 1 must dispatch LetterDefOf.ThreatSmall.");
            Assert.IsTrue(dispatchedTitle.ToString().Contains("Early Warning: Unidentified Contact [Contact-"),
                $"Title must match Stage 1 convention. Actual: {dispatchedTitle}");
        }
        finally
        {
            EarlyWarningDispatcher.OnLetterDispatched -= HandleLetter;
        }

        void HandleLetter(WorldObject_Incursion inc, LetterDef def, TaggedString title, TaggedString text)
        {
            if (inc != incursion) return;
            dispatchedDef = def;
            dispatchedTitle = title;
        }
    }

    [TestMethod]
    public void ProcessDetection_Stage2ClassifiedTier_HostileIncursion_EscalatesToThreatBig()
    {
        // Arrange
        var incursion = new WorldObject_Incursion();
        incursion.TrackingId = 5;
        incursion.HasDispatchedContactAlert = true; // Stage 1 already fired
        var sensor = new MockSensorProvider();
        LetterDef? dispatchedDef = null;
        TaggedString dispatchedTitle = default;

        EarlyWarningDispatcher.OnLetterDispatched += HandleLetter;
        try
        {
            // Act: Crosses R_id threshold into Classified tier
            EarlyWarningDispatcher.ProcessDetection(incursion, sensor, IntelligenceTier.Classified);

            // Assert
            Assert.AreEqual(IntelligenceTier.Classified, incursion.CurrentIntelligenceTier);
            Assert.IsTrue(incursion.HasDispatchedClassifiedAlert, "Stage 2 classified alert must be marked dispatched.");
            Assert.IsTrue(incursion.IsIdentified, "Incursion must now be identified.");

            Assert.AreEqual(LetterDefOf.ThreatBig, dispatchedDef, "Stage 2 hostile incursion must escalate to LetterDefOf.ThreatBig.");
            Assert.IsTrue(dispatchedTitle.ToString().Contains("Hostile Incursion"),
                $"Hostile incursion title must indicate Hostile Incursion. Actual: {dispatchedTitle}");
        }
        finally
        {
            EarlyWarningDispatcher.OnLetterDispatched -= HandleLetter;
        }

        void HandleLetter(WorldObject_Incursion inc, LetterDef def, TaggedString title, TaggedString text)
        {
            if (inc != incursion) return;
            dispatchedDef = def;
            dispatchedTitle = title;
        }
    }

    [TestMethod]
    public void ProcessDetection_Stage2ClassifiedTier_FriendlyTradeCaravan_DispatchesPositiveEvent()
    {
        // Arrange
        var incursion = new WorldObject_Incursion();
        incursion.TrackingId = 8;
        incursion.HasDispatchedContactAlert = true;
        incursion.SetExplicitGroupKind(IncursionGroupKind.TradeCaravan);

        var sensor = new MockSensorProvider();
        LetterDef? dispatchedDef = null;
        TaggedString dispatchedTitle = default;

        EarlyWarningDispatcher.OnLetterDispatched += HandleLetter;
        try
        {
            // Act: Crosses R_id threshold into Classified tier
            EarlyWarningDispatcher.ProcessDetection(incursion, sensor, IntelligenceTier.Classified);

            // Assert
            Assert.AreEqual(LetterDefOf.PositiveEvent, dispatchedDef, "Friendly trade caravan must dispatch LetterDefOf.PositiveEvent.");
            Assert.IsTrue(dispatchedTitle.ToString().Contains("Approaching Caravan") || dispatchedTitle.ToString().Contains("Trading Caravan"),
                $"Caravan title must indicate approaching caravan. Actual: {dispatchedTitle}");
            Assert.IsTrue(incursion.HasDispatchedClassifiedAlert);
            Assert.IsTrue(incursion.IsIdentified);
        }
        finally
        {
            EarlyWarningDispatcher.OnLetterDispatched -= HandleLetter;
        }

        void HandleLetter(WorldObject_Incursion inc, LetterDef def, TaggedString title, TaggedString text)
        {
            if (inc != incursion) return;
            dispatchedDef = def;
            dispatchedTitle = title;
        }
    }

    [TestMethod]
    public void ProcessDetection_Stage2ClassifiedTier_Visitors_DispatchesNeutralEvent()
    {
        // Arrange
        var incursion = new WorldObject_Incursion();
        incursion.TrackingId = 12;
        incursion.HasDispatchedContactAlert = true;
        incursion.SetExplicitGroupKind(IncursionGroupKind.Visitors);

        var sensor = new MockSensorProvider();
        LetterDef? dispatchedDef = null;
        TaggedString dispatchedTitle = default;

        EarlyWarningDispatcher.OnLetterDispatched += HandleLetter;
        try
        {
            // Act: Crosses R_id threshold into Classified tier
            EarlyWarningDispatcher.ProcessDetection(incursion, sensor, IntelligenceTier.Classified);

            // Assert
            Assert.AreEqual(LetterDefOf.NeutralEvent, dispatchedDef, "Visitors must dispatch LetterDefOf.NeutralEvent.");
            Assert.IsTrue(dispatchedTitle.ToString().Contains("Approaching Visitors"),
                $"Visitors title must indicate approaching visitors. Actual: {dispatchedTitle}");
        }
        finally
        {
            EarlyWarningDispatcher.OnLetterDispatched -= HandleLetter;
        }

        void HandleLetter(WorldObject_Incursion inc, LetterDef def, TaggedString title, TaggedString text)
        {
            if (inc != incursion) return;
            dispatchedDef = def;
            dispatchedTitle = title;
        }
    }

    [TestMethod]
    public void ProcessDetection_DuplicateAlerts_NeverDispatchedTwice()
    {
        // Arrange
        var incursion = new WorldObject_Incursion();
        var sensor = new MockSensorProvider();
        int dispatchCount = 0;

        EarlyWarningDispatcher.OnLetterDispatched += HandleLetter;
        try
        {
            // Act 1: Initial contact
            EarlyWarningDispatcher.ProcessDetection(incursion, sensor, IntelligenceTier.Contact);
            Assert.AreEqual(1, dispatchCount, "First contact should dispatch exactly 1 letter.");

            // Act 2: Subsequent pulse sweep while still at Contact tier
            EarlyWarningDispatcher.ProcessDetection(incursion, sensor, IntelligenceTier.Contact);
            Assert.AreEqual(1, dispatchCount, "Subsequent pulse at same tier must NOT duplicate letter.");

            // Act 3: Escalates to Classified
            EarlyWarningDispatcher.ProcessDetection(incursion, sensor, IntelligenceTier.Classified);
            Assert.AreEqual(2, dispatchCount, "Escalating to Classified should dispatch exactly 1 escalation letter.");

            // Act 4: Subsequent pulse at Classified tier
            EarlyWarningDispatcher.ProcessDetection(incursion, sensor, IntelligenceTier.Classified);
            Assert.AreEqual(2, dispatchCount, "Subsequent pulse at Classified tier must NOT duplicate letter.");
        }
        finally
        {
            EarlyWarningDispatcher.OnLetterDispatched -= HandleLetter;
        }

        void HandleLetter(WorldObject_Incursion inc, LetterDef def, TaggedString title, TaggedString text)
        {
            if (inc != incursion) return;
            dispatchCount++;
        }
    }

    [TestMethod]
    public void ProcessDetection_DirectHighSpecDetection_BypassesStage1AndMarksBothDispatched()
    {
        // Scenario: Incursion spawns directly inside R_id of a high-tech radar array.
        // It skips the vague Stage 1 contact alert and fires the detailed Stage 2 alert directly.

        // Arrange
        var incursion = new WorldObject_Incursion();
        var sensor = new MockSensorProvider();
        int dispatchCount = 0;
        LetterDef? dispatchedDef = null;

        EarlyWarningDispatcher.OnLetterDispatched += HandleLetter;
        try
        {
            // Act: Directly detected at Classified tier
            EarlyWarningDispatcher.ProcessDetection(incursion, sensor, IntelligenceTier.Classified);

            // Assert
            Assert.AreEqual(1, dispatchCount, "Direct classified detection should dispatch exactly 1 letter.");
            Assert.AreEqual(LetterDefOf.ThreatBig, dispatchedDef, "Should directly dispatch ThreatBig.");
            Assert.IsTrue(incursion.HasDispatchedContactAlert, "Contact alert should be marked dispatched to avoid later replay.");
            Assert.IsTrue(incursion.HasDispatchedClassifiedAlert, "Classified alert should be marked dispatched.");
        }
        finally
        {
            EarlyWarningDispatcher.OnLetterDispatched -= HandleLetter;
        }

        void HandleLetter(WorldObject_Incursion inc, LetterDef def, TaggedString title, TaggedString text)
        {
            if (inc != incursion) return;
            dispatchCount++;
            dispatchedDef = def;
        }
    }

    [TestMethod]
    public void TerrainMaskingGating_MountainousTile_GatedAtContactTierUntilExit()
    {
        // Scenario: Target is inside R_id (e.g. 5 steps <= 10 steps), but target is in Mountainous terrain.
        // SensoryTerrainUtility.EvaluateSensorTier gating check:
        // Sensor without penetration holds target at Contact.
        // Sensor with penetration resolves to Classified.

        var standardSensor = new MockSensorProvider
        {
            IdentificationRadiusInTiles = 10,
            MaxIntelligenceTier = IntelligenceTier.Classified,
            PenetratesTerrainMasking = false
        };

        var spacerSensor = new MockSensorProvider
        {
            IdentificationRadiusInTiles = 10,
            MaxIntelligenceTier = IntelligenceTier.Classified,
            PenetratesTerrainMasking = true
        };

        // Act 1: Standard sensor against masked target at travDist = 5 <= 10
        IntelligenceTier standardTier = SensoryTerrainUtility.EvaluateSensorTier(standardSensor, travDist: 5, isTargetMasked: true);

        // Assert 1: Held at Contact
        Assert.AreEqual(IntelligenceTier.Contact, standardTier,
            "Incursion in Mountainous terrain must be gated at Contact tier without penetration.");

        // Act 2: Spacer / SIGINT sensor against masked target at travDist = 5 <= 10
        IntelligenceTier spacerTier = SensoryTerrainUtility.EvaluateSensorTier(spacerSensor, travDist: 5, isTargetMasked: true);

        // Assert 2: Resolves to Classified
        Assert.AreEqual(IntelligenceTier.Classified, spacerTier,
            "Sensor with penetration must resolve to Classified despite Mountainous terrain.");
    }

    [TestMethod]
    public void ProcessDetection_OnceClassified_PreservesIsIdentifiedWhenDemotedToContactZone()
    {
        // Scenario: Incursion enters R_id and becomes Classified/Identified.
        // It later moves into a sector where the strongest sensor only covers Contact tier.
        // It must NOT lose its identified status (faction crest, red blip).

        var incursion = new WorldObject_Incursion();
        var sensor = new MockSensorProvider();

        // 1. Initial detection at Contact
        EarlyWarningDispatcher.ProcessDetection(incursion, sensor, IntelligenceTier.Contact);
        Assert.IsFalse(incursion.IsIdentified);

        // 2. Escalates to Classified
        EarlyWarningDispatcher.ProcessDetection(incursion, sensor, IntelligenceTier.Classified);
        Assert.IsTrue(incursion.IsIdentified);
        Assert.IsTrue(incursion.HasDispatchedClassifiedAlert);

        // 3. Moves to a sector where only Contact tier applies
        EarlyWarningDispatcher.ProcessDetection(incursion, sensor, IntelligenceTier.Contact);
        Assert.IsTrue(incursion.IsIdentified, "Incursion must preserve IsIdentified once classified.");
    }

    [TestMethod]
    public void SendIncursionLetter_FallbackVanillaDefs_DoesNotThrowInvalidCastException()
    {
        // Scenario: PV_LetterDefOf defs are missing/unloaded; fallback to vanilla LetterDefOf
        var incursion = new WorldObject_Incursion();

        // Dispatches event without throwing InvalidCastException
        EarlyWarningDispatcher.SendIncursionLetter(
            incursion,
            "Test Alert",
            "Test text",
            LetterDefOf.ThreatSmall,
            destinationMap: null,
            targetName: "Colony"
        );
    }
}
