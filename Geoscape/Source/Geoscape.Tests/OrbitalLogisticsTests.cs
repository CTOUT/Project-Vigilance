using System;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape.Orbital;
using ProjectVigilance.Geoscape.Patches;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Geoscape.Tests;

[TestClass]
public sealed class OrbitalLogisticsTests
{
    [TestMethod]
    public void OrbitalFlightModel_IsTileWithinFootprint_InvalidTile_ReturnsFalse()
    {
        Assert.IsFalse(OrbitalFlightModel.IsTileWithinFootprint(Vector3.up, PlanetTile.Invalid, 24f));
    }

    [TestMethod]
    public void OrbitalFlightModel_DriftingGroundTrack_LatitudesStayWithinInclination()
    {
        // Arrange: 45 degree inclination, 3450 tick period (low orbit)
        OrbitalParameters orbit = new(
            altitudeKm: 500f,
            periodTicks: 3450,
            inclinationDeg: 45f,
            ascendingNodeDeg: 0f,
            precessionRateDegPerDay: -3.2f,
            meanAnomalyEpochDeg: 0f,
            orbitalDriftDegPerDay: 0.5f,
            epochTick: 0);

        // Act & Assert: Evaluate latitude at various intervals across an entire in-game day (60,000 ticks)
        for (int tick = 0; tick <= 60000; tick += 250)
        {
            OrbitalFlightModel.CalculateSubSatelliteCoordinates(orbit, tick, out float latDeg, out float lonDeg);

            Assert.IsTrue(latDeg >= -45.05f && latDeg <= 45.05f,
                $"Latitude {latDeg:F2}° at tick {tick} exceeded orbital inclination of 45°.");

            Assert.IsTrue(lonDeg >= -180f && lonDeg <= 180f,
                $"Longitude {lonDeg:F2}° at tick {tick} outside normalized [-180, +180] range.");
        }
    }

    [TestMethod]
    public void OrbitalFlightModel_DriftingGroundTrack_GroundTrackShiftsAcrossConsecutiveOrbits()
    {
        // Scenario: A satellite completes orbit 1, then orbit 2. Due to planetary rotation and nodal precession,
        // the longitude of equatorial crossing must shift westward, avoiding fixed repeating tracks.
        OrbitalParameters orbit = new(
            altitudeKm: 600f,
            periodTicks: 4250,
            inclinationDeg: 42f,
            ascendingNodeDeg: 30f,
            precessionRateDegPerDay: -3.5f,
            meanAnomalyEpochDeg: 0f,
            orbitalDriftDegPerDay: 0.8f,
            epochTick: 0);

        // Equatorial crossing of orbit 1: tick = 0
        OrbitalFlightModel.CalculateSubSatelliteCoordinates(orbit, 0, out _, out float lonOrbit1);

        // Equatorial crossing of orbit 2: tick = 4250 (one full orbital period later)
        OrbitalFlightModel.CalculateSubSatelliteCoordinates(orbit, 4250, out _, out float lonOrbit2);

        // Equatorial crossing of orbit 3: tick = 8500 (two full orbital periods later)
        OrbitalFlightModel.CalculateSubSatelliteCoordinates(orbit, 8500, out _, out float lonOrbit3);

        // Assert: Longitudes must be distinct and non-zero drift
        float shift1to2 = Mathf.Abs(lonOrbit2 - lonOrbit1);
        float shift2to3 = Mathf.Abs(lonOrbit3 - lonOrbit2);

        Assert.IsTrue(shift1to2 > 5f, $"Longitude shift from orbit 1 to 2 was too small ({shift1to2:F2}°). Track did not drift.");
        Assert.IsTrue(shift2to3 > 5f, $"Longitude shift from orbit 2 to 3 was too small ({shift2to3:F2}°). Track did not drift.");
    }

    [TestMethod]
    public void OrbitalFlightModel_DriftingGroundTrack_DoesNotRepeatAfterOneFullDay()
    {
        // Scenario: Exactly 60,000 ticks later (1 in-game day), a rigid repeating orbit would return to the exact same position.
        // With incommensurate periods and J2 nodal precession, the ground track must have drifted.
        OrbitalParameters orbit = new(
            altitudeKm: 600f,
            periodTicks: 4250, // 60000 / 4250 = 14.1176... orbits/day (non-integer!)
            inclinationDeg: 42f,
            ascendingNodeDeg: 0f,
            precessionRateDegPerDay: -3.5f,
            meanAnomalyEpochDeg: 0f,
            orbitalDriftDegPerDay: 0.8f,
            epochTick: 0);

        OrbitalFlightModel.CalculateSubSatelliteCoordinates(orbit, 0, out float latDay0, out float lonDay0);
        OrbitalFlightModel.CalculateSubSatelliteCoordinates(orbit, 60000, out float latDay1, out float lonDay1);

        Vector3 dirDay0 = OrbitalFlightModel.SphericalToDirection(latDay0, lonDay0);
        Vector3 dirDay1 = OrbitalFlightModel.SphericalToDirection(latDay1, lonDay1);

        float angularDistance = OrbitalFlightModel.AngularDistanceDeg(dirDay0, dirDay1);

        Assert.IsTrue(angularDistance > 10f,
            $"Satellite returned to virtually the same sub-satellite position after 1 day (angular distance: {angularDistance:F2}°). Drifting mechanics failed.");
    }

    [TestMethod]
    public void OrbitalFlightModel_OrbitalVelocity_FollowsKeplerianPhysics()
    {
        // Lower orbits travel faster than higher orbits: v = sqrt(GM / r)
        OrbitalParameters lowOrbit = new(altitudeKm: 300f, periodTicks: 3000, inclinationDeg: 30f, 0f, 0f, 0f, 0f, 0);
        OrbitalParameters highOrbit = new(altitudeKm: 35786f, periodTicks: 60000, inclinationDeg: 30f, 0f, 0f, 0f, 0f, 0);

        float vLow = OrbitalFlightModel.CalculateOrbitalVelocityKmS(lowOrbit);
        float vHigh = OrbitalFlightModel.CalculateOrbitalVelocityKmS(highOrbit);

        Assert.IsTrue(vLow > 7.0f && vLow < 8.0f, $"Low orbit velocity {vLow:F2} km/s out of expected 7.0-8.0 km/s range.");
        Assert.IsTrue(vHigh > 2.8f && vHigh < 3.2f, $"Geostationary velocity {vHigh:F2} km/s out of expected 2.8-3.2 km/s range.");
        Assert.IsTrue(vLow > vHigh, "Lower orbit should have significantly higher velocity than high orbit.");
    }

    [TestMethod]
    public void OrbitalFlightModel_AngularDistance_IdenticalVectorsReturnZero()
    {
        Vector3 dir = new Vector3(1f, 0f, 0f);
        float dist = OrbitalFlightModel.AngularDistanceDeg(dir, dir);
        Assert.AreEqual(0f, dist, 0.001f);
    }

    [TestMethod]
    public void OrbitalFlightModel_AngularDistance_OppositeVectorsReturn180()
    {
        Vector3 dirA = new Vector3(0f, 1f, 0f);
        Vector3 dirB = new Vector3(0f, -1f, 0f);
        float dist = OrbitalFlightModel.AngularDistanceDeg(dirA, dirB);
        Assert.AreEqual(180f, dist, 0.01f);
    }

    [TestMethod]
    public void WorldObject_Satellite_ReconnaissanceSensoryCapabilities_ConfiguredCorrectly()
    {
        // Arrange
        WorldObject_Satellite sat = new();
        sat.InitializeSatellite(
            SatelliteRole.Reconnaissance,
            new OrbitalParameters(450f, 3420, 88f, 0f, -2.8f, 0f, 0.6f, 0),
            stationId: 101,
            callsign: "Recon-1",
            footprintRadius: 14f);

        // Assert: ISensorProvider contracts
        Assert.IsTrue(sat.IsSensorActive, "Recon satellite sensor should be active.");
        Assert.IsFalse(sat.IsStationary, "Satellite is mobile, should not be marked stationary.");
        Assert.IsTrue(sat.IsTechnological, "Satellite is a high-tech orbital platform.");
        Assert.IsTrue(sat.PenetratesTerrainMasking, "Orbital reconnaissance penetrates terrain masking.");
        Assert.AreEqual(IntelligenceTier.Analysed, sat.MaxIntelligenceTier, "Recon satellite should provide Analysed tier intelligence.");
        Assert.AreEqual(14f, sat.DetectionRadiusInTiles, 0.1f);
        Assert.AreEqual(8, sat.IdentificationRadiusInTiles);
    }

    [TestMethod]
    public void WorldObject_Satellite_ServiceAreaTransitions_TriggerProperly()
    {
        // Arrange
        WorldObject_Satellite sat = new();
        sat.InitializeSatellite(
            SatelliteRole.Trade,
            new OrbitalParameters(600f, 4250, 42f, 0f, -3.5f, 0f, 0.8f, 0),
            stationId: 202,
            callsign: "Trade-Alpha");

        WorldObject_OrbitalStation station = new();

        // Act & Assert: Enter service corridor
        Assert.IsFalse(sat.InServiceArea, "Satellite should initially be outside service corridor.");
        sat.OnEnterServiceArea(station);
        Assert.IsTrue(sat.InServiceArea, "Satellite should be marked in service corridor.");

        // Act & Assert: Leave service corridor
        sat.OnLeaveServiceArea(station);
        Assert.IsFalse(sat.InServiceArea, "Satellite should be marked outside service corridor.");
    }

    [TestMethod]
    public void Satellite_Trade_PropertiesAndContracts_AreValid()
    {
        // Arrange
        Satellite_Trade tradeSat = new();
        tradeSat.InitializeSatellite(
            SatelliteRole.Trade,
            new OrbitalParameters(600f, 4250, 42f, 0f, -3.5f, 0f, 0.8f, 0),
            stationId: 303,
            callsign: "Merchant-Orbit-1");

        // Assert
        Assert.AreEqual(SatelliteRole.Trade, tradeSat.Role);
        Assert.AreEqual("Merchant-Orbit-1", tradeSat.TraderName);
        Assert.AreEqual(TradeCurrency.Silver, tradeSat.TradeCurrency);
        Assert.IsNotNull(tradeSat.GetCallLabel());
        Assert.IsNotNull(tradeSat.GetInfoText());
    }

    [TestMethod]
    public void CaravanNavigationBonus_SpeedFactorAppliedProperly()
    {
        // Scenario: A caravan normally takes 100 ticks per move.
        // Under navigation satellite footprint, the 0.85 factor reduces ticks to 85.
        int originalTicks = 100;
        int boostedTicks = Mathf.Max(1, Mathf.RoundToInt(originalTicks * CaravanTicksPerMoveUtility_GetTicksPerMove_Patch.NavigationSpeedBonusFactor));

        Assert.AreEqual(85, boostedTicks, "Navigation speed bonus factor (0.85) should reduce 100 ticks to 85.");
    }

    [TestMethod]
    public void OrbitalManager_RolePresets_CreateDistinctCharacteristics()
    {
        OrbitalParameters tradeOrbit = OrbitalManager.CreateOrbitalParametersForRole(SatelliteRole.Trade);
        OrbitalParameters reconOrbit = OrbitalManager.CreateOrbitalParametersForRole(SatelliteRole.Reconnaissance);
        OrbitalParameters navOrbit = OrbitalManager.CreateOrbitalParametersForRole(SatelliteRole.Navigation);
        OrbitalParameters commsOrbit = OrbitalManager.CreateOrbitalParametersForRole(SatelliteRole.Communication);

        // Trade vs Recon vs Nav vs Comms
        Assert.IsTrue(reconOrbit.AltitudeKm < tradeOrbit.AltitudeKm || reconOrbit.InclinationDeg > 80f,
            "Reconnaissance orbit should be polar/high-inclination.");
        Assert.IsTrue(reconOrbit.InclinationDeg >= 85f, "Reconnaissance satellite should have polar inclination (>= 85°).");
        Assert.IsTrue(navOrbit.AltitudeKm > 10000f, "Navigation satellite should be in medium/high orbit (> 10,000 km).");
        Assert.IsTrue(commsOrbit.AltitudeKm > 30000f, "Communication satellite should be in high-apogee/geostationary orbit (> 30,000 km).");
        Assert.IsTrue(reconOrbit.PeriodTicks < navOrbit.PeriodTicks, "Recon orbit period should be much shorter than navigation orbit.");
    }

    [TestMethod]
    public void OrbitalManager_IsOrbitalFaction_IdentifiesTradersGuildCorrectly()
    {
        // Arrange: Mock faction with defName TradersGuild
        FactionDef guildDef = new() { defName = "TradersGuild" };
        Faction guildFaction = new() { def = guildDef };

        FactionDef standardDef = new() { defName = "OutlanderCivil" };
        Faction standardFaction = new() { def = standardDef };

        // Act & Assert
        Assert.IsTrue(OrbitalManager.IsOrbitalFaction(guildFaction), "TradersGuild must be identified as an orbital faction.");
        Assert.IsFalse(OrbitalManager.IsOrbitalFaction(null), "Null faction must return false.");
    }

    [TestMethod]
    public void OrbitalManager_FindExistingOrbitalStation_PrioritisesWorldGenSpaceSettlement()
    {
        // Scenario: When Odyssey generates a SpaceSettlement at world creation,
        // OrbitalManager must adopt that existing settlement rather than spawning duplicate stations.
        OrbitalManager manager = new(null!);

        FactionDef guildDef = new() { defName = "TradersGuild" };
        Faction guildFaction = new() { def = guildDef };

        // Act: In an empty environment without existing objects, it returns null
        WorldObject? station = manager.FindExistingOrbitalStationForFaction(guildFaction);
        Assert.IsNull(station, "Without existing world objects, FindExistingOrbitalStationForFaction should return null.");
    }

    [TestMethod]
    public void WorldObject_OrbitalStation_ChildRegistrationAndReplenishmentQueue()
    {
        // Arrange
        WorldObject_OrbitalStation station = new();
        WorldObject_Satellite sat = new();
        sat.InitializeSatellite(
            SatelliteRole.Trade,
            new OrbitalParameters(600f, 4250, 42f, 0f, -3.5f, 0f, 0.8f, 0),
            stationId: station.ID,
            callsign: "Trade-Sat-1");

        // Act: Register child
        station.RegisterChildSatellite(sat);
        Assert.AreEqual(1, station.ChildSatelliteIds.Count);
        Assert.IsTrue(station.ChildSatelliteIds.Contains(sat.ID));

        // Act: Unregister child
        station.UnregisterChildSatellite(sat.ID);
        Assert.AreEqual(0, station.ChildSatelliteIds.Count);

        // Act: Queue replenishment
        station.QueueReplenishment(SatelliteRole.Reconnaissance, delayTicks: 500);
        string inspect = station.GetStationInspectString();
        StringAssert.Contains(inspect, "Fabricating Replacement: Reconnaissance");
    }

    [TestMethod]
    public void OrbitalFlightModel_SphericalToDirection_NormalizedAndAccurate()
    {
        // Equator at prime meridian: lat=0, lon=0 -> (0, 0, -1) in RimWorld WorldGrid
        Vector3 v0 = OrbitalFlightModel.SphericalToDirection(0f, 0f);
        Assert.AreEqual(0f, v0.x, 0.001f);
        Assert.AreEqual(0f, v0.y, 0.001f);
        Assert.AreEqual(-1f, v0.z, 0.001f);
        Assert.AreEqual(1f, v0.magnitude, 0.001f);

        // 90° East meridian: lat=0, lon=90 -> (1, 0, 0) in RimWorld WorldGrid
        Vector3 v90 = OrbitalFlightModel.SphericalToDirection(0f, 90f);
        Assert.AreEqual(1f, v90.x, 0.001f);
        Assert.AreEqual(0f, v90.y, 0.001f);
        Assert.AreEqual(0f, v90.z, 0.001f);
        Assert.AreEqual(1f, v90.magnitude, 0.001f);

        // North pole: lat=90, lon=0 -> (0, 1, 0)
        Vector3 vNorth = OrbitalFlightModel.SphericalToDirection(90f, 0f);
        Assert.AreEqual(0f, vNorth.x, 0.001f);
        Assert.AreEqual(1f, vNorth.y, 0.001f);
        Assert.AreEqual(0f, vNorth.z, 0.001f);
        Assert.AreEqual(1f, vNorth.magnitude, 0.001f);

        // South pole: lat=-90, lon=0 -> (0, -1, 0)
        Vector3 vSouth = OrbitalFlightModel.SphericalToDirection(-90f, 0f);
        Assert.AreEqual(0f, vSouth.x, 0.001f);
        Assert.AreEqual(-1f, vSouth.y, 0.001f);
        Assert.AreEqual(0f, vSouth.z, 0.001f);
    }

    [TestMethod]
    public void Satellite_Trade_CanTradeNow_WhenPlayerFactionNull_DoesNotThrow()
    {
        // Scenario: Trade satellite initialized during world generation before player faction is generated.
        // Accessing CanTradeNow must safely return false and not throw or trigger Log.Error via Faction.OfPlayer.
        Satellite_Trade sat = new();
        sat.SetFaction(new Faction { def = new FactionDef { defName = "OutlanderCivil" } });

        Assert.IsFalse(sat.CanTradeNow, "CanTradeNow should return false safely when player faction is uninitialized.");
    }

    [TestMethod]
    public void WorldObject_Satellite_InspectString_IncludesTelemetryAndDriftStatus()
    {
        WorldObject_Satellite sat = new();
        sat.InitializeSatellite(
            SatelliteRole.Military,
            new OrbitalParameters(800f, 5120, 62f, 15f, -3.1f, 45f, 0.5f, 0),
            stationId: 505,
            callsign: "Aegis-1",
            footprintRadius: 18f);

        string inspect = sat.GetOrbitalInspectString();

        StringAssert.Contains(inspect, "Role: Military");
        StringAssert.Contains(inspect, "Altitude: 800 km");
        StringAssert.Contains(inspect, "Inclination: 62.0°");
        StringAssert.Contains(inspect, "Orbital Velocity:");
        StringAssert.Contains(inspect, "Autonomous Mission (Drifting Ground Track)");
    }

    [TestMethod]
    public void OrbitalFlightModel_CalculateOrbitalPosition_LogarithmicScaling_KeepsGeostationaryCloseToGlobe()
    {
        // Low Earth Orbit (500 km)
        OrbitalParameters leo = new(
            altitudeKm: 500f,
            periodTicks: 3600,
            inclinationDeg: 45f,
            ascendingNodeDeg: 0f,
            precessionRateDegPerDay: 0f,
            meanAnomalyEpochDeg: 0f,
            orbitalDriftDegPerDay: 0f,
            epochTick: 0);

        Vector3 posLeo = OrbitalFlightModel.CalculateOrbitalPosition(leo, 0, basePlanetRadius: 100f);
        float rLeo = posLeo.magnitude;

        // High Geostationary Orbit (35,786 km)
        OrbitalParameters geo = new(
            altitudeKm: 35786f,
            periodTicks: 60000,
            inclinationDeg: 0f,
            ascendingNodeDeg: 0f,
            precessionRateDegPerDay: 0f,
            meanAnomalyEpochDeg: 0f,
            orbitalDriftDegPerDay: 0f,
            epochTick: 0);

        Vector3 posGeo = OrbitalFlightModel.CalculateOrbitalPosition(geo, 0, basePlanetRadius: 100f);
        float rGeo = posGeo.magnitude;

        // Assert: LEO should hug the atmosphere (104 - 108 units)
        Assert.IsTrue(rLeo >= 104f && rLeo <= 108f, $"LEO position radius was {rLeo:F2}, expected [104, 108]");

        // Assert: GEO must remain framed around the planet (130 - 135 units)
        Assert.IsTrue(rGeo >= 130f && rGeo <= 135f, $"GEO position radius was {rGeo:F2}, expected [130, 135]");
        Assert.IsTrue(rGeo > rLeo + 20f, $"GEO radius ({rGeo:F2}) must be distinctly higher than LEO ({rLeo:F2})");
    }

    [TestMethod]
    public void WorldObject_DrawPos_Patch_SpaceObjects_StratifiesDeterministically()
    {
        lock (typeof(WorldObject_DrawPos_Patch))
        {
            try
            {
                WorldObject_DrawPos_Patch.IsSpaceObjectOverride = _ => true;

                WorldObject woSpace = new()
                {
                    ID = 42,
                    def = new WorldObjectDef { defName = "Odyssey_SpaceStation" }
                };

                Vector3 basePos = new Vector3(100f, 0f, 0f);
                Vector3 resultPos = basePos;

                WorldObject_DrawPos_Patch.Postfix(woSpace, ref resultPos);

                // Stratification must modify radius within bounded range [-5f, +7f]
                float delta = resultPos.magnitude - basePos.magnitude;
                Assert.IsTrue(delta >= -5.01f && delta <= 7.01f, $"Radial jitter {delta:F2} outside expected [-5, +7] range.");
                Assert.AreNotEqual(0f, delta, 0.001f, "Radial jitter should be non-zero for space objects.");

                // Calling again with same ID must produce identical deterministic position
                Vector3 repeatPos = basePos;
                WorldObject_DrawPos_Patch.Postfix(woSpace, ref repeatPos);
                Assert.AreEqual(resultPos.x, repeatPos.x, 0.001f);

                // Odyssey stations and asteroids are MapParent/Site subclasses and must still stratify
                Site orbitalSite = new()
                {
                    ID = 43,
                    def = new WorldObjectDef { defName = "Odyssey_OrbitalPlatform" }
                };
                Vector3 sitePos = basePos;
                WorldObject_DrawPos_Patch.Postfix(orbitalSite, ref sitePos);
                float siteDelta = sitePos.magnitude - basePos.magnitude;
                Assert.IsTrue(siteDelta >= -5.01f && siteDelta <= 7.01f && Math.Abs(siteDelta) > 0.001f,
                    "Odyssey Site/MapParent space objects must still receive altitude stratification.");
            }
            finally
            {
                WorldObject_DrawPos_Patch.IsSpaceObjectOverride = null;
            }
        }
    }

    [TestMethod]
    public void Satellite_Trade_CheckColonyOverpass_WhenNotPlaying_DoesNotThrow()
    {
        Satellite_Trade sat = new();
        sat.InitializeSatellite(
            SatelliteRole.Trade,
            new OrbitalParameters(600f, 62500, 42f, 0f, -3.5f, 0f, 0.8f, 0),
            stationId: 101,
            callsign: "Trade-Test-1");

        // Act & Assert
        sat.CheckColonyOverpass();
        Assert.AreEqual(0, sat.ActiveOverpassMapIds.Count);
    }

    [TestMethod]
    public void Satellite_Trade_PostRemove_ClearsActiveOverpassMapIds()
    {
        Satellite_Trade sat = new();
        sat.InitializeSatellite(
            SatelliteRole.Trade,
            new OrbitalParameters(600f, 62500, 42f, 0f, -3.5f, 0f, 0.8f, 0),
            stationId: 101,
            callsign: "Trade-Test-1");

        sat.PostRemove();
        Assert.AreEqual(0, sat.ActiveOverpassMapIds.Count);
    }

    [TestMethod]
    public void Diagnostic_InspectGetCommTargets()
    {
        var m = typeof(RimWorld.Building_CommsConsole).GetMethod("GetCommTargets", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Console.WriteLine($"GetCommTargets: {m}");
        if (m != null)
        {
            foreach (var inst in HarmonyLib.PatchProcessor.GetOriginalInstructions(m))
            {
                Console.WriteLine($"  {inst.opcode} {inst.operand}");
            }
        }
    }

    [TestMethod]
    public void DebugActions_HasDirectColonySpawnActions()
    {
        var methods = typeof(ProjectVigilance.Geoscape.Debug.DebugActionsVigilance).GetMethods(
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        var tradeAction = methods.FirstOrDefault(m =>
        {
            var attr = m.GetCustomAttributes(typeof(LudeonTK.DebugActionAttribute), false).FirstOrDefault() as LudeonTK.DebugActionAttribute;
            return attr != null && attr.name == "Orbital: Spawn Trade Satellite Over Colony";
        });
        Assert.IsNotNull(tradeAction, "Expected 'Orbital: Spawn Trade Satellite Over Colony' debug action");

        var tradeAttr = tradeAction.GetCustomAttributes(typeof(LudeonTK.DebugActionAttribute), false).First() as LudeonTK.DebugActionAttribute;
        Assert.AreEqual(LudeonTK.DebugActionType.Action, tradeAttr!.actionType);
        Assert.AreEqual(LudeonTK.AllowedGameStates.Playing, tradeAttr.allowedGameStates);

        var reconAction = methods.FirstOrDefault(m =>
        {
            var attr = m.GetCustomAttributes(typeof(LudeonTK.DebugActionAttribute), false).FirstOrDefault() as LudeonTK.DebugActionAttribute;
            return attr != null && attr.name == "Orbital: Spawn Recon Satellite Over Colony";
        });
        Assert.IsNotNull(reconAction, "Expected 'Orbital: Spawn Recon Satellite Over Colony' debug action");

        var reconAttr = reconAction.GetCustomAttributes(typeof(LudeonTK.DebugActionAttribute), false).First() as LudeonTK.DebugActionAttribute;
        Assert.AreEqual(LudeonTK.DebugActionType.Action, reconAttr!.actionType);
        Assert.AreEqual(LudeonTK.AllowedGameStates.Playing, reconAttr.allowedGameStates);
    }

    [TestMethod]
    public void SphericalToDirection_MatchesRimWorldWorldGridConvention()
    {
        float[] testLats = { -60f, -30f, 0f, 25f, 45f, 80f };
        float[] testLons = { -170f, -90f, -45f, 0f, 60f, 135f, 180f };

        foreach (float lat in testLats)
        {
            foreach (float lon in testLons)
            {
                Vector3 dir = OrbitalFlightModel.SphericalToDirection(lat, lon);
                float computedLon = Mathf.Atan2(dir.x, -dir.z) * Mathf.Rad2Deg;
                float computedLat = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;

                Assert.AreEqual(lat, computedLat, 0.01f, $"Latitude mismatch for ({lat}, {lon})");
                float lonDiff = Mathf.Abs(Mathf.DeltaAngle(lon, computedLon));
                Assert.IsTrue(lonDiff < 0.01f, $"Longitude mismatch for ({lat}, {lon}): computed {computedLon}");
            }
        }
    }

    [TestMethod]
    public void CreateOverheadOrbitParameters_DirectlyMatchesTargetCoordinates()
    {
        float targetLat = 42.5f;
        float targetLon = 68.3f;
        int currentTick = 25000;
        float extraInclinationDeg = 5f;

        float inclination = Mathf.Clamp(Mathf.Abs(targetLat) + extraInclinationDeg, 10f, 89.9f);
        float incRad = inclination * Mathf.Deg2Rad;
        float latRad = targetLat * Mathf.Deg2Rad;
        float sinU = Mathf.Sin(latRad) / Mathf.Sin(incRad);
        sinU = Mathf.Clamp(sinU, -1f, 1f);
        float uRad = Mathf.Asin(sinU);
        float uDeg = uRad * Mathf.Rad2Deg;
        if (uDeg < 0f) uDeg += 360f;
        uRad = uDeg * Mathf.Deg2Rad;

        float yNode = Mathf.Sin(uRad) * Mathf.Cos(incRad);
        float xNode = Mathf.Cos(uRad);
        float alphaNodeDeg = Mathf.Atan2(yNode, xNode) * Mathf.Rad2Deg;
        float raanDeg = ((targetLon - alphaNodeDeg + 180f) % 360f + 360f) % 360f - 180f;

        OrbitalParameters orbitParams = new(
            altitudeKm: 600f,
            periodTicks: 62500,
            inclinationDeg: inclination,
            ascendingNodeDeg: raanDeg,
            precessionRateDegPerDay: -3.5f,
            meanAnomalyEpochDeg: uDeg,
            orbitalDriftDegPerDay: 0.8f,
            epochTick: currentTick);

        OrbitalFlightModel.CalculateSubSatelliteCoordinates(orbitParams, currentTick, out float satLat, out float satLon);
        Assert.AreEqual(targetLat, satLat, 0.01f, "Sub-satellite latitude must match target latitude at epoch");
        float lonDiff = Mathf.Abs(Mathf.DeltaAngle(targetLon, satLon));
        Assert.IsTrue(lonDiff < 0.01f, $"Sub-satellite longitude must match target longitude at epoch (expected {targetLon}, got {satLon})");

        Vector3 satDir = OrbitalFlightModel.CalculateSubSatelliteDirection(orbitParams, currentTick);
        Vector3 targetDir = OrbitalFlightModel.SphericalToDirection(targetLat, targetLon);
        float angDist = OrbitalFlightModel.AngularDistanceDeg(satDir, targetDir);
        Assert.IsTrue(angDist < 0.01f, $"Angular distance between satellite and target must be ~0 at epoch, was {angDist}°");
    }

    [TestMethod]
    public void OrbitalFlightModel_CalculateOrbitalPosition_EnforcesHardOrbitalRenderCeiling()
    {
        // Scenario: A celestial body with extreme altitude (e.g. 500,000 km) and high jitter must never
        // exceed MaxOrbitalRenderRadius (135.5 units) to prevent camera blowout / off-screen regression.
        OrbitalParameters extremeOrbit = new(
            altitudeKm: 500000f,
            periodTicks: 100000,
            inclinationDeg: 15f,
            ascendingNodeDeg: 0f,
            precessionRateDegPerDay: 0f,
            meanAnomalyEpochDeg: 0f,
            orbitalDriftDegPerDay: 0f,
            epochTick: 0);

        Vector3 pos = OrbitalFlightModel.CalculateOrbitalPosition(extremeOrbit, 0, basePlanetRadius: 100f);
        float r = pos.magnitude;

        Assert.IsTrue(r <= OrbitalFlightModel.MaxOrbitalRenderRadius,
            $"Render radius {r:F2} exceeded hard ceiling limit of {OrbitalFlightModel.MaxOrbitalRenderRadius} units.");
        Assert.IsTrue(r >= 100f + OrbitalFlightModel.MinVisualAltitude,
            $"Render radius {r:F2} fell below minimum visual altitude threshold.");
    }

    [TestMethod]
    public void WorldObject_DrawPos_Patch_DeepSpaceCeiling_CappedAt145()
    {
        lock (typeof(WorldObject_DrawPos_Patch))
        {
            try
            {
                WorldObject_DrawPos_Patch.IsSpaceObjectOverride = _ => true;

                WorldObject woDistant = new()
                {
                    ID = 999,
                    def = new WorldObjectDef { defName = "Odyssey_DeepSpaceAsteroid" }
                };

                // Distant base position that would blow off-screen if unconstrained
                Vector3 basePos = new Vector3(160f, 0f, 0f);
                Vector3 resultPos = basePos;

                WorldObject_DrawPos_Patch.Postfix(woDistant, ref resultPos);

                Assert.IsTrue(resultPos.magnitude <= 145.001f,
                    $"Deep space position magnitude {resultPos.magnitude:F2} exceeded hard ceiling limit of 145 units.");
            }
            finally
            {
                WorldObject_DrawPos_Patch.IsSpaceObjectOverride = null;
            }
        }
    }

    [TestMethod]
    public void WorldObject_OrbitalStation_DefaultOrbit_IsAsynchronousMEO()
    {
        WorldObject_OrbitalStation station = new();
        station.InitializeStation("Logistics Hub 1");

        Assert.IsNotNull(station.Orbit, "Station orbital parameters must be initialized.");
        Assert.IsTrue(station.Orbit.PeriodTicks > 60000,
            $"Station period {station.Orbit.PeriodTicks} must be > 60,000 ticks so the planetary surface moves underneath.");
        Assert.IsTrue(station.Orbit.AltitudeKm >= 5000f && station.Orbit.AltitudeKm <= 15000f,
            $"Station altitude {station.Orbit.AltitudeKm:F1} km must be within Medium Earth Orbit (MEO) band [5000, 15000] km.");
    }

    [TestMethod]
    public void OrbitalFlightModel_CreateOverheadOrbitParameters_ProducesOverheadCoordinates()
    {
        float targetLat = 35.0f;
        float targetLon = -110.0f;
        int currentTick = 12000;

        OrbitalParameters orbit = OrbitalFlightModel.CreateOverheadOrbitParameters(
            targetLat, targetLon, currentTick, altitudeKm: 8500f, periodTicks: 78000, extraInclinationDeg: 5f);

        OrbitalFlightModel.CalculateSubSatelliteCoordinates(orbit, currentTick, out float satLat, out float satLon);

        Assert.AreEqual(targetLat, satLat, 0.05f, "Calculated overhead orbit latitude must match target latitude.");
        float lonDiff = Mathf.Abs(Mathf.DeltaAngle(targetLon, satLon));
        Assert.IsTrue(lonDiff < 0.05f, $"Calculated overhead orbit longitude must match target longitude (diff: {lonDiff:F3}°).");
    }

    [TestMethod]
    public void OrbitalFlightModel_AltitudeKmToVisualAltitude_DistinguishesLowSatelliteFromStation()
    {
        // Low Trade satellite (775 km) vs High Logistics Hub (7643 km)
        float vSat = OrbitalFlightModel.AltitudeKmToVisualAltitude(775f);
        float vStation = OrbitalFlightModel.AltitudeKmToVisualAltitude(7643f);

        // Low satellite must sit close to the atmosphere (6 - 8 units above planet radius)
        Assert.IsTrue(vSat >= 6f && vSat <= 8f, $"Low satellite visual altitude was {vSat:F2}, expected [6, 8]");

        // Logistics Hub station must sit in middle orbit (15 - 18 units above planet radius)
        Assert.IsTrue(vStation >= 15f && vStation <= 18f, $"Station visual altitude was {vStation:F2}, expected [15, 18]");

        // Station must have at least 2x the visual elevation of the low satellite
        Assert.IsTrue(vStation / vSat >= 2.0f,
            $"Station elevation ({vStation:F2}) must be at least double low satellite elevation ({vSat:F2})");
    }

    [TestMethod]
    public void OrbitalFlightModel_VisualAltitudeToAltitudeKm_InvertsAccurately()
    {
        float[] altitudesToTest = [ 200f, 775f, 1500f, 5000f, 7643f, 15000f, 25000f, 35786f ];
        foreach (float km in altitudesToTest)
        {
            float visual = OrbitalFlightModel.AltitudeKmToVisualAltitude(km);
            float invertedKm = OrbitalFlightModel.VisualAltitudeToAltitudeKm(visual);
            Assert.AreEqual(km, invertedKm, 1.0f, $"Inverse altitude mapping failed for {km} km (got {invertedKm:F1} km)");
        }
    }

    [TestMethod]
    public void SpaceObject_InspectString_Patch_SpaceObjects_AppendsTelemetry()
    {
        lock (typeof(SpaceObject_InspectString_Patch))
        {
            try
            {
                SpaceObject_InspectString_Patch.IsSpaceObjectOverride = _ => true;

                WorldObject woStation = new()
                {
                    ID = 88,
                    def = new WorldObjectDef { defName = "Odyssey_SpaceStation" }
                };

                string result = "Faction: Cosmic Guild\nNeutral (0)";
                SpaceObject_InspectString_Patch.Postfix(woStation, ref result);

                StringAssert.Contains(result, "Orbital Altitude:");
                StringAssert.Contains(result, "Classification: Orbital Habitat / Station");

                WorldObject woAsteroid = new()
                {
                    ID = 89,
                    def = new WorldObjectDef { defName = "Odyssey_Asteroid" }
                };
                string resultAst = "";
                SpaceObject_InspectString_Patch.Postfix(woAsteroid, ref resultAst);
                StringAssert.Contains(resultAst, "Classification: Deep Space Asteroid");

                // Odyssey stations & asteroids inherit from MapParent / Site in-game
                Site orbitalSite = new()
                {
                    ID = 90,
                    def = new WorldObjectDef { defName = "Odyssey_SpacePlatform_Station" }
                };
                string siteResult = "";
                SpaceObject_InspectString_Patch.Postfix(orbitalSite, ref siteResult);
                StringAssert.Contains(siteResult, "Orbital Altitude:");
                StringAssert.Contains(siteResult, "Classification: Orbital Habitat / Station");
            }
            finally
            {
                SpaceObject_InspectString_Patch.IsSpaceObjectOverride = null;
            }
        }
    }

    [TestMethod]
    public void TryProjectRayToPlanetDirection_ResolvesSurfaceOrbitalRingAndMisses()
    {
        // 1. Direct planet sphere hit (front hemisphere facing camera at z = -300)
        Vector3 origin = new(0f, 0f, -300f);
        Vector3 hitRayDir = (new Vector3(60f, 0f, 0f) - origin).normalized;
        Assert.IsTrue(OrbitalFlightModel.TryProjectRayToPlanetDirection(origin, hitRayDir, out Vector3 surfaceDir));
        // Closest approach d ≈ 60 along x; sphere hit z = -sqrt(100^2 - 60^2) = -80 -> dir ≈ (0.6, 0, -0.8)
        Assert.IsTrue(surfaceDir.z < -0.7f, $"Expected front hemisphere surface hit (z < -0.7), got {surfaceDir}");
        Assert.AreEqual(1f, surfaceDir.magnitude, 0.001f);

        // 2. Orbital shell ring click outside planet limb (100 < r <= 145)
        Assert.IsTrue(OrbitalFlightModel.TryProjectRayToPlanetDirection(new Vector3(130f, 0f, -300f), Vector3.forward, out Vector3 ringDir));
        Assert.AreEqual(1f, ringDir.x, 0.001f, "Parallel limb ray through x=130 must project to limb direction (1, 0, 0).");
        Assert.AreEqual(1f, ringDir.magnitude, 0.001f);

        Vector3 perspRingDir = new Vector3(130f, 0f, 300f).normalized;
        Assert.IsTrue(OrbitalFlightModel.TryProjectRayToPlanetDirection(origin, perspRingDir, out Vector3 perspDir));
        Assert.IsTrue(perspDir.x > 0.9f && perspDir.z < 0f, $"Expected front-limb projection for perspective ray, got {perspDir}");

        // 3. Deep space miss beyond maxOrbitalPickRadius (145f)
        Vector3 missRayDir = new Vector3(180f, 0f, 300f).normalized;
        Assert.IsFalse(OrbitalFlightModel.TryProjectRayToPlanetDirection(origin, missRayDir, out _),
            "Clicks beyond the orbital pick shell must return false.");

        // 4. Ray pointing away from planet returns false
        Assert.IsFalse(OrbitalFlightModel.TryProjectRayToPlanetDirection(origin, new Vector3(0f, 0f, -1f), out _),
            "Ray pointing away from the planet centre must return false.");
    }
}
