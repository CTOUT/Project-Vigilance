using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape;
using ProjectVigilance.Geoscape.Sensory;
using ProjectVigilance.Geoscape.World;
using RimWorld.Planet;

namespace Geoscape.Tests;

/// <summary>
/// Unit and stress test suite validating the Milestone 1.3 performance budget:
/// - Execution latency budget: complete steady-state sweep <= 1.0 ms under 20 active radars and 100 concurrent world incursions.
/// - Zero-allocation policy: exactly 0 bytes allocated per steady-state sweep (GC.GetAllocatedBytesForCurrentThread() delta == 0).
/// - Waylith DeepProfiler native instrumentation validation.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class PerformanceAndStressTests
{
    [TestInitialize]
    public void Setup()
    {
        var prefsType = typeof(Verse.Prefs);
        var dataField = HarmonyLib.AccessTools.Field(prefsType, "data");
        if (dataField != null && dataField.GetValue(null) == null)
        {
            var prefsDataType = dataField.FieldType;
            var dataInstance = Activator.CreateInstance(prefsDataType);
            dataField.SetValue(null, dataInstance);
        }
    }

    [TestCleanup]
    public void Cleanup()
    {
        SensoryManager.DistanceCalculationOverride = null;
        SensoryManager.IsExploredOverride = null;
        SensoryManager.Instance = null;
        InterFactionEncounterManager.IncursionProviderOverride = null;
        SensoryTerrainUtility.TargetMaskedOverride = null;
        SensoryTerrainUtility.VantageBonusOverride = null;
    }

    [TestMethod]
    public void SensorySweep_HighLoad_20Radars_100Incursions_ExecutesUnder1ms_AndZeroAllocations()
    {
        // 1. Arrange: High-load stress scenario
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;
        SensoryManager.DistanceCalculationOverride = static (a, b, _) => Math.Abs(a.tileId - b.tileId) % 30;

        var sensoryManager = new SensoryManager(null!);

        // 20 active radar installations across the planetary map
        var radars = new List<MockSensorProvider>(20);
        for (int i = 0; i < 20; i++)
        {
            var radar = new MockSensorProvider
            {
                SensorTile = (PlanetTile)(i * 120 + 50),
                DetectionRadiusInTiles = 24f,
                IdentificationRadiusInTiles = 17,
                MaxIntelligenceTier = IntelligenceTier.Classified,
                IsSensorActive = true,
                IsTechnological = true,
                IsStationary = true,
                PenetratesTerrainMasking = (i % 4 == 0)
            };
            radars.Add(radar);
            sensoryManager.RegisterSensor(radar);
        }

        // 100 concurrent world incursions traversing the globe
        var incursions = new List<WorldObject_Incursion>(100);
        for (int i = 0; i < 100; i++)
        {
            var inc = new WorldObject_Incursion();
            inc.SetTileRaw((PlanetTile)(i * 22));
            inc.SetSpawnedRaw(true);
            inc.TrackingId = i + 1;
            incursions.Add(inc);
            sensoryManager.RegisterTarget(inc);
        }

        // Warm up: First pass establishes steady-state target tiers and sensor cache
        sensoryManager.PerformSensorySweep();

        // JIT warm-up passes to ensure all call sites are compiled before measuring
        for (int i = 0; i < 30; i++)
        {
            sensoryManager.PerformSensorySweep();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // 2. Act: Measure memory allocations and execution latency during steady-state sweep
        long startAlloc = GC.GetAllocatedBytesForCurrentThread();
        long startTimestamp = Stopwatch.GetTimestamp();

        sensoryManager.PerformSensorySweep();

        long endTimestamp = Stopwatch.GetTimestamp();
        long endAlloc = GC.GetAllocatedBytesForCurrentThread();

        long allocatedBytes = endAlloc - startAlloc;
        double elapsedMs = Stopwatch.GetElapsedTime(startTimestamp, endTimestamp).TotalMilliseconds;

        // 3. Assert: 0-byte GC rule and <= 1.0 ms execution latency budget
        Assert.AreEqual(0L, allocatedBytes,
            $"SensoryManager.PerformSensorySweep must strictly adhere to the 0-byte allocation policy. Allocated: {allocatedBytes} bytes.");
        Assert.IsTrue(elapsedMs <= 1.0,
            $"SensoryManager.PerformSensorySweep exceeded 1.0 ms budget: took {elapsedMs:F4} ms.");
    }

    [TestMethod]
    public void InterFactionCollisions_HighLoad_100Incursions_ExecutesUnder1ms_AndZeroAllocations()
    {
        // 1. Arrange: 100 concurrent world incursions across 60 distinct tiles (some overlapping)
        var incursions = new List<WorldObject_Incursion>(100);
        for (int i = 0; i < 100; i++)
        {
            var inc = new WorldObject_Incursion();
            inc.SetTileRaw((PlanetTile)((i % 60) * 15));
            inc.SetSpawnedRaw(true);
            inc.TrackingId = i + 1;
            // Skirmish on cooldown to test steady-state collision scanning hot path
            inc.SkirmishCooldownExpiry = int.MaxValue;
            incursions.Add(inc);
        }

        InterFactionEncounterManager.IncursionProviderOverride = incursions;
        var encounterManager = new InterFactionEncounterManager(null!);

        // Warm up: initial pass exercises pooling and warms dictionary buckets
        encounterManager.CheckInterFactionCollisions();

        // JIT warm-up passes
        for (int i = 0; i < 30; i++)
        {
            encounterManager.CheckInterFactionCollisions();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // 2. Act: Measure allocations and latency on steady-state collision sweep
        long startAlloc = GC.GetAllocatedBytesForCurrentThread();
        long startTimestamp = Stopwatch.GetTimestamp();

        encounterManager.CheckInterFactionCollisions();

        long endTimestamp = Stopwatch.GetTimestamp();
        long endAlloc = GC.GetAllocatedBytesForCurrentThread();

        long allocatedBytes = endAlloc - startAlloc;
        double elapsedMs = Stopwatch.GetElapsedTime(startTimestamp, endTimestamp).TotalMilliseconds;

        // 3. Assert: 0-byte GC rule and <= 1.0 ms execution latency budget
        Assert.AreEqual(0L, allocatedBytes,
            $"InterFactionEncounterManager.CheckInterFactionCollisions must strictly adhere to the 0-byte allocation policy. Allocated: {allocatedBytes} bytes.");
        Assert.IsTrue(elapsedMs <= 1.0,
            $"InterFactionEncounterManager.CheckInterFactionCollisions exceeded 1.0 ms budget: took {elapsedMs:F4} ms.");
    }

    [TestMethod]
    public void CombinedFullTickRare_HighLoad_50ConsecutiveSweeps_ZeroMemoryGrowth()
    {
        // Arrange
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;
        SensoryManager.DistanceCalculationOverride = static (a, b, _) => Math.Abs(a.tileId - b.tileId) % 30;

        var sensoryManager = new SensoryManager(null!);
        for (int i = 0; i < 20; i++)
        {
            var radar = new MockSensorProvider
            {
                SensorTile = (PlanetTile)(i * 100),
                DetectionRadiusInTiles = 24f,
                IdentificationRadiusInTiles = 17,
                MaxIntelligenceTier = IntelligenceTier.Classified,
                IsSensorActive = true,
                IsTechnological = true,
                IsStationary = true
            };
            sensoryManager.RegisterSensor(radar);
        }

        var incursions = new List<WorldObject_Incursion>(100);
        for (int i = 0; i < 100; i++)
        {
            var inc = new WorldObject_Incursion();
            inc.SetTileRaw((PlanetTile)((i % 50) * 20));
            inc.SetSpawnedRaw(true);
            inc.TrackingId = i + 1;
            inc.SkirmishCooldownExpiry = int.MaxValue;
            incursions.Add(inc);
            sensoryManager.RegisterTarget(inc);
        }

        InterFactionEncounterManager.IncursionProviderOverride = incursions;
        var encounterManager = new InterFactionEncounterManager(null!);

        // Warm up both systems
        sensoryManager.PerformSensorySweep();
        encounterManager.CheckInterFactionCollisions();

        for (int i = 0; i < 3; i++)
        {
            sensoryManager.PerformSensorySweep();
            encounterManager.CheckInterFactionCollisions();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Act: Execute 50 consecutive TickRare steady-state sweeps
        long startAlloc = GC.GetAllocatedBytesForCurrentThread();
        long startTimestamp = Stopwatch.GetTimestamp();

        for (int tick = 0; tick < 50; tick++)
        {
            sensoryManager.PerformSensorySweep();
            encounterManager.CheckInterFactionCollisions();
        }

        long endTimestamp = Stopwatch.GetTimestamp();
        long endAlloc = GC.GetAllocatedBytesForCurrentThread();

        long totalAllocated = endAlloc - startAlloc;
        double totalMs = Stopwatch.GetElapsedTime(startTimestamp, endTimestamp).TotalMilliseconds;
        double avgMsPerSweep = totalMs / 50.0;

        // Assert: 0 bytes total memory allocated over 50 consecutive sweeps, avg latency <= 1.0 ms
        Assert.AreEqual(0L, totalAllocated,
            $"50 consecutive steady-state sweeps must allocate exactly 0 bytes. Allocated: {totalAllocated} bytes.");
        Assert.IsTrue(avgMsPerSweep <= 1.0,
            $"Average combined sweep latency per tick exceeded 1.0 ms budget: average {avgMsPerSweep:F4} ms.");
    }

    [TestMethod]
    public void SensorySweep_EdgeCase_EmptySensorsAndTargets_ExecutesInstantlyAndAllocatesZero()
    {
        var sensoryManager = new SensoryManager(null!);
        sensoryManager.PerformSensorySweep(); // JIT warm-up

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long startAlloc = GC.GetAllocatedBytesForCurrentThread();
        long startTimestamp = Stopwatch.GetTimestamp();

        sensoryManager.PerformSensorySweep();

        long endTimestamp = Stopwatch.GetTimestamp();
        long endAlloc = GC.GetAllocatedBytesForCurrentThread();

        long allocated = endAlloc - startAlloc;
        double elapsedMs = Stopwatch.GetElapsedTime(startTimestamp, endTimestamp).TotalMilliseconds;

        Assert.AreEqual(0L, allocated);
        Assert.IsTrue(elapsedMs < 0.5, $"Empty sweep took {elapsedMs:F4} ms.");
    }

    [TestMethod]
    public void InterFactionCollisions_EdgeCase_All100IncursionsOnSameTile_ResolvesSafelyWithoutAllocation()
    {
        var incursions = new List<WorldObject_Incursion>(100);
        for (int i = 0; i < 100; i++)
        {
            var inc = new WorldObject_Incursion();
            inc.SetTileRaw((PlanetTile)500); // All concentrated on single tile 500
            inc.SetSpawnedRaw(true);
            inc.TrackingId = i + 1;
            inc.SkirmishCooldownExpiry = int.MaxValue;
            incursions.Add(inc);
        }

        InterFactionEncounterManager.IncursionProviderOverride = incursions;
        var encounterManager = new InterFactionEncounterManager(null!);

        encounterManager.CheckInterFactionCollisions(); // Warmup

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long startAlloc = GC.GetAllocatedBytesForCurrentThread();
        encounterManager.CheckInterFactionCollisions();
        long endAlloc = GC.GetAllocatedBytesForCurrentThread();

        long allocated = endAlloc - startAlloc;
        Assert.AreEqual(0L, allocated, $"Single-tile concentration sweep allocated {allocated} bytes.");
    }

    [TestMethod]
    public void DeepProfiler_SensorySweep_AllNestedBlocksExecuteSymmetrically()
    {
        SensoryManager.DistanceCalculationOverride = static (a, b, _) => 5;
        var sensoryManager = new SensoryManager(null!);

        var radar = new MockSensorProvider
        {
            SensorTile = (PlanetTile)100,
            DetectionRadiusInTiles = 24f,
            IdentificationRadiusInTiles = 17,
            MaxIntelligenceTier = IntelligenceTier.Classified,
            IsSensorActive = true,
            IsStationary = true
        };
        sensoryManager.RegisterSensor(radar);

        var inc = new WorldObject_Incursion();
        inc.SetTileRaw((PlanetTile)102);
        inc.SetSpawnedRaw(true);
        sensoryManager.RegisterTarget(inc);

        // Runs sweep ensuring PerformSensorySweep, VisionUpdates, and TargetEvaluations blocks execute without imbalance
        sensoryManager.PerformSensorySweep();
    }

    [TestMethod]
    public void DeepProfiler_SkirmishResolution_BlockExecutesSymmetrically()
    {
        var manager = new InterFactionEncounterManager(null!);

        var incA = new WorldObject_Incursion();
        incA.SetTileRaw((PlanetTile)200);
        incA.SetSpawnedRaw(true);

        var incB = new WorldObject_Incursion();
        incB.SetTileRaw((PlanetTile)200);
        incB.SetSpawnedRaw(true);

        // Executes background skirmish ensuring SkirmishResolution DeepProfiler block executes cleanly
        manager.ExecuteBackgroundSkirmish(incA, incB);
    }

    [TestMethod]
    public void SensorySweep_MultipleRadarsOnSameSettlement_ReachesSteadyState_ZeroAllocationsAndNoSensorsChangedLoop()
    {
        // Arrange: Multiple radar dishes on the SAME colony sharing the same Source WorldObject ID
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;
        SensoryManager.DistanceCalculationOverride = static (a, b, _) => 5;

        var sensoryManager = new SensoryManager(null!);
        var sharedSettlementSource = new WorldObject_Incursion();
        HarmonyLib.Traverse.Create(sharedSettlementSource).Field("id").SetValue(42);

        // 3 radar dishes + 1 colony lookouts sharing the exact same Source (Settlement ID 42)
        var dish1 = new MockSensorProvider
        {
            SensorTile = (PlanetTile)100,
            DetectionRadiusInTiles = 24f,
            IdentificationRadiusInTiles = 10,
            Source = sharedSettlementSource,
            IsSensorActive = true,
            IsStationary = true
        };
        var dish2 = new MockSensorProvider
        {
            SensorTile = (PlanetTile)100,
            DetectionRadiusInTiles = 24f,
            IdentificationRadiusInTiles = 17,
            Source = sharedSettlementSource,
            IsSensorActive = true,
            IsStationary = true
        };
        var dish3 = new MockSensorProvider
        {
            SensorTile = (PlanetTile)100,
            DetectionRadiusInTiles = 24f,
            IdentificationRadiusInTiles = 22,
            Source = sharedSettlementSource,
            IsSensorActive = true,
            IsStationary = true
        };
        var lookouts = new MockSensorProvider
        {
            SensorTile = (PlanetTile)100,
            DetectionRadiusInTiles = 8f,
            IdentificationRadiusInTiles = 4,
            Source = sharedSettlementSource,
            IsSensorActive = true,
            IsStationary = true
        };

        sensoryManager.RegisterSensor(dish1);
        sensoryManager.RegisterSensor(dish2);
        sensoryManager.RegisterSensor(dish3);
        sensoryManager.RegisterSensor(lookouts);

        var inc = new WorldObject_Incursion();
        inc.SetTileRaw((PlanetTile)105);
        inc.SetSpawnedRaw(true);
        sensoryManager.RegisterTarget(inc);

        // Warm up pass: populates lastActiveSensorStates
        sensoryManager.PerformSensorySweep();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Act: Steady-state pass must not loop into sensorsChanged and must allocate 0 bytes
        long startAlloc = GC.GetAllocatedBytesForCurrentThread();
        long startTimestamp = Stopwatch.GetTimestamp();

        sensoryManager.PerformSensorySweep();

        long endTimestamp = Stopwatch.GetTimestamp();
        long endAlloc = GC.GetAllocatedBytesForCurrentThread();

        long allocated = endAlloc - startAlloc;
        double elapsedMs = Stopwatch.GetElapsedTime(startTimestamp, endTimestamp).TotalMilliseconds;

        // Assert: All 4 co-located sensors track independently; steady state achieved with 0 allocations
        Assert.AreEqual(0L, allocated,
            $"Multi-dish co-located radar sweep must achieve steady-state and allocate 0 bytes. Allocated: {allocated} bytes.");
        Assert.IsTrue(elapsedMs <= 1.0, $"Multi-dish sweep took {elapsedMs:F4} ms.");
        Assert.AreEqual(IntelligenceTier.Classified, inc.CurrentIntelligenceTier);
    }

    [TestMethod]
    public void SensorySweep_RealListeningPost_CanDetectWithoutNullReferenceException()
    {
        SensoryManager.DistanceCalculationOverride = static (a, b, _) => 4;

        var post = new WorldObject_ListeningPost();
        post.SetTileRaw((PlanetTile)300);
        post.SetSpawnedRaw(true);

        var inc = new WorldObject_Incursion();
        inc.SetTileRaw((PlanetTile)302);
        inc.SetSpawnedRaw(true);

        // Must evaluate CanDetect safely in headless environment without throwing NullReferenceException
        bool canDetect = post.CanDetect(inc);
        Assert.IsTrue(canDetect, "Real listening post should detect incursion within radius via DistanceCalculationOverride.");
    }

    [TestMethod]
    public void SensorySweep_RealCompRadar_CanDetectWithoutNullReferenceException()
    {
        SensoryManager.DistanceCalculationOverride = static (a, b, _) => 6;

        var radarProps = new CompProperties_Radar { detectionRadius = 24 };
        var compRadar = new CompRadar { props = radarProps };

        var inc = new WorldObject_Incursion();
        inc.SetTileRaw((PlanetTile)150);
        inc.SetSpawnedRaw(true);

        // Even unspawned/parentless, CanDetect must return false cleanly without crashing
        bool canDetectUnspawned = compRadar.CanDetect(inc);
        Assert.IsFalse(canDetectUnspawned, "Unspawned CompRadar cannot detect.");
    }

    [TestMethod]
    public void SensorySweep_TargetEvaluations_AnalysedEarlyExit_ExecutesCorrectly()
    {
        SensoryManager.DistanceCalculationOverride = static (a, b, _) => 2;
        var sensoryManager = new SensoryManager(null!);

        var phasedArrayWithSigint = new MockSensorProvider
        {
            SensorTile = (PlanetTile)100,
            DetectionRadiusInTiles = 24f,
            IdentificationRadiusInTiles = 22,
            MaxIntelligenceTier = IntelligenceTier.Analysed,
            PenetratesTerrainMasking = true,
            IsSensorActive = true,
            IsStationary = true
        };
        var secondaryDish = new MockSensorProvider
        {
            SensorTile = (PlanetTile)100,
            DetectionRadiusInTiles = 24f,
            IdentificationRadiusInTiles = 10,
            MaxIntelligenceTier = IntelligenceTier.Classified,
            IsSensorActive = true,
            IsStationary = true
        };

        sensoryManager.RegisterSensor(phasedArrayWithSigint);
        sensoryManager.RegisterSensor(secondaryDish);

        var inc = new WorldObject_Incursion();
        inc.SetTileRaw((PlanetTile)102);
        inc.SetSpawnedRaw(true);
        sensoryManager.RegisterTarget(inc);

        sensoryManager.PerformSensorySweep();
        Assert.AreEqual(IntelligenceTier.Analysed, inc.CurrentIntelligenceTier);
    }
}
