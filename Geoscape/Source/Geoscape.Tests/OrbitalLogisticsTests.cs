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
        // Equator at prime meridian: lat=0, lon=0 -> (1, 0, 0)
        Vector3 v0 = OrbitalFlightModel.SphericalToDirection(0f, 0f);
        Assert.AreEqual(1f, v0.x, 0.001f);
        Assert.AreEqual(0f, v0.y, 0.001f);
        Assert.AreEqual(0f, v0.z, 0.001f);
        Assert.AreEqual(1f, v0.magnitude, 0.001f);

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

        // Assert: LEO should be just above surface (115 - 122 units)
        Assert.IsTrue(rLeo >= 115f && rLeo <= 122f, $"LEO position radius was {rLeo:F2}, expected [115, 122]");

        // Assert: GEO must not blow out to deep space (~472 units), but remain framed around the planet (130 - 137 units)
        Assert.IsTrue(rGeo >= 130f && rGeo <= 137f, $"GEO position radius was {rGeo:F2}, expected [130, 137]");
        Assert.IsTrue(rGeo > rLeo + 10f, $"GEO radius ({rGeo:F2}) must be distinctly higher than LEO ({rLeo:F2})");
    }

    [TestMethod]
    public void WorldObject_DrawPos_Patch_SpaceObjects_StratifiesDeterministically()
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
        }
        finally
        {
            WorldObject_DrawPos_Patch.IsSpaceObjectOverride = null;
        }
    }
}
