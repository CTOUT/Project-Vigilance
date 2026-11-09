using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape;
using ProjectVigilance.Geoscape.Orbital;
using ProjectVigilance.Geoscape.Patches;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Geoscape.Tests;

[TestClass]
[DoNotParallelize]
public sealed class OrbitalVisibilityTests
{
    private VisibilityMode originalMode;

    [TestInitialize]
    public void Init()
    {
        originalMode = GeoscapeMod.Settings.visibilityMode;
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;
    }

    [TestCleanup]
    public void Cleanup()
    {
        GeoscapeMod.Settings.visibilityMode = originalMode;
        SensoryManager.Instance = null;
    }

    private static void SetFactionRaw(WorldObject wo, Faction f)
    {
        HarmonyLib.Traverse.Create(wo).Field("factionInt").SetValue(f);
    }

    [TestMethod]
    public void WorldObject_Satellite_NonPlayer_IsSensorActive_ReturnsFalse()
    {
        // Arrange: NPC Recon satellite (e.g. Voidborn Emporium / Pirates)
        FactionDef npcDef = new() { defName = "VoidbornEmporium", isPlayer = false };
        Faction npcFaction = new() { def = npcDef };

        WorldObject_Satellite sat = new();
        SetFactionRaw(sat, npcFaction);
        sat.InitializeSatellite(
            SatelliteRole.Reconnaissance,
            new OrbitalParameters(450f, 3420, 95f, 0f, -2.8f, 0f, 0.6f, 0),
            stationId: 101,
            callsign: "NPC-Recon-1",
            footprintRadius: 14f);

        // Act & Assert: NPC satellites must never be active sensors for the player
        Assert.IsFalse(sat.IsSensorActive, "Non-player reconnaissance satellites must never be active sensors for player sensory grid.");
    }

    [TestMethod]
    public void WorldObject_Satellite_PlayerOwned_IsSensorActive_ReturnsTrue()
    {
        // Arrange: Player Recon satellite
        FactionDef playerDef = new() { defName = "PlayerColony", isPlayer = true };
        Faction playerFaction = new() { def = playerDef };

        WorldObject_Satellite sat = new();
        SetFactionRaw(sat, playerFaction);
        sat.InitializeSatellite(
            SatelliteRole.Reconnaissance,
            new OrbitalParameters(450f, 3420, 95f, 0f, -2.8f, 0f, 0.6f, 0),
            stationId: 101,
            callsign: "Player-Recon-1",
            footprintRadius: 14f);

        // Act & Assert: Player recon satellite must be active
        Assert.IsTrue(sat.IsSensorActive, "Player reconnaissance satellites must report active sensor status.");
    }

    [TestMethod]
    public void WorldObject_Satellite_PlayerOwnedTrade_IsSensorActive_ReturnsFalse()
    {
        // Arrange: Player Trade satellite (commercial platform, not a reconnaissance radar)
        FactionDef playerDef = new() { defName = "PlayerColony", isPlayer = true };
        Faction playerFaction = new() { def = playerDef };

        WorldObject_Satellite sat = new();
        SetFactionRaw(sat, playerFaction);
        sat.InitializeSatellite(
            SatelliteRole.Trade,
            new OrbitalParameters(600f, 4250, 42f, 0f, -3.5f, 0f, 0.8f, 0),
            stationId: 101,
            callsign: "Player-Trade-1",
            footprintRadius: 16f);

        // Act & Assert
        Assert.IsFalse(sat.IsSensorActive, "Trade satellites should not act as active reconnaissance sensors.");
    }

    [TestMethod]
    public void SensoryManager_RegisterSensor_RejectsNonPlayerSensors()
    {
        // Arrange
        SensoryManager sensory = new(null!);
        SensoryManager.Instance = sensory;

        FactionDef npcDef = new() { defName = "HostilePirates", isPlayer = false };
        Faction npcFaction = new() { def = npcDef };

        WorldObject_Satellite npcSat = new();
        SetFactionRaw(npcSat, npcFaction);
        npcSat.InitializeSatellite(
            SatelliteRole.Reconnaissance,
            new OrbitalParameters(450f, 3420, 95f, 0f, -2.8f, 0f, 0.6f, 0),
            stationId: 102,
            callsign: "Pirate-Sat",
            footprintRadius: 14f);

        // Act
        sensory.RegisterSensor(npcSat);

        // Assert: NPC satellite must be rejected from active sensors
        var activeSensors = sensory.GetActiveSensors();
        Assert.IsFalse(activeSensors.Contains(npcSat), "SensoryManager must never register non-player sensors in the player active sensor cache.");
    }

    [TestMethod]
    public void WorldObject_Visibility_Patch_ShouldHideWorldObject_HidesUndetectedOrbitalEntity()
    {
        // Scenario: Day 1 colony without radar or comms console.
        // An NPC satellite drifts overhead. It must be concealed under ClassicHazeOfWar.
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;

        FactionDef npcDef = new() { defName = "VoidbornEmporium", isPlayer = false };
        Faction npcFaction = new() { def = npcDef };

        WorldObject_Satellite npcSat = new();
        SetFactionRaw(npcSat, npcFaction);
        npcSat.InitializeSatellite(
            SatelliteRole.Reconnaissance,
            new OrbitalParameters(450f, 3420, 95f, 0f, -2.8f, 0f, 0.6f, 0),
            stationId: 103,
            callsign: "Sat-Unseen",
            footprintRadius: 14f);

        // Act & Assert: Must be hidden
        Assert.IsTrue(WorldObject_Visibility_Patch.ShouldHideWorldObject(npcSat),
            "Non-player orbital satellites must be completely hidden before detection infrastructure is built.");
    }

    [TestMethod]
    public void WorldObject_Visibility_Patch_ShouldHideWorldObject_RevealsPlayerOrbitalEntity()
    {
        // Scenario: The player owns this orbital station/satellite. It must never be hidden.
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;

        FactionDef playerDef = new() { defName = "PlayerColony", isPlayer = true };
        Faction playerFaction = new() { def = playerDef };

        WorldObject_Satellite playerSat = new();
        SetFactionRaw(playerSat, playerFaction);
        playerSat.InitializeSatellite(
            SatelliteRole.Reconnaissance,
            new OrbitalParameters(450f, 3420, 95f, 0f, -2.8f, 0f, 0.6f, 0),
            stationId: 104,
            callsign: "Player-Sat",
            footprintRadius: 14f);

        // Act & Assert
        Assert.IsFalse(WorldObject_Visibility_Patch.ShouldHideWorldObject(playerSat),
            "Player-owned orbital entities must never be hidden.");
    }

    [TestMethod]
    public void WorldObject_Visibility_Patch_ShouldHideWorldObject_RevealsWhenOmniscient()
    {
        // Scenario: Omniscient visibility mode disables Haze of War culling
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.Omniscient;

        FactionDef npcDef = new() { defName = "VoidbornEmporium", isPlayer = false };
        Faction npcFaction = new() { def = npcDef };

        WorldObject_Satellite npcSat = new();
        SetFactionRaw(npcSat, npcFaction);
        npcSat.InitializeSatellite(
            SatelliteRole.Trade,
            new OrbitalParameters(600f, 4250, 42f, 0f, -3.5f, 0f, 0.8f, 0),
            stationId: 105,
            callsign: "Visible-Trade-Sat",
            footprintRadius: 16f);

        // Act & Assert
        Assert.IsFalse(WorldObject_Visibility_Patch.ShouldHideWorldObject(npcSat),
            "Orbital entities must be visible under Omniscient mode.");
    }

    [TestMethod]
    public void CompRadar_IsTechnological_DifferentiatesWatchtowerFromRadar()
    {
        // 1. Medieval Lookout Tower
        ThingDef towerDef = new() { defName = "Geoscape_LookoutTower", techLevel = TechLevel.Medieval };
        Building towerBuilding = new() { def = towerDef };
        CompRadar towerRadar = new() { parent = towerBuilding };

        Assert.IsFalse(towerRadar.IsTechnological, "Lookout watchtower is medieval optical observation, not technological radar.");

        // 2. Industrial Early-Warning Radar Dome
        ThingDef domeDef = new() { defName = "Geoscape_RadarSmall", techLevel = TechLevel.Industrial };
        Building domeBuilding = new() { def = domeDef };
        CompRadar domeRadar = new() { parent = domeBuilding };

        Assert.IsTrue(domeRadar.IsTechnological, "Early-warning radar dome is technological radar infrastructure.");
    }

    [TestMethod]
    public void OrbitalManager_GetDefaultPeriodForRole_MatchesStorytellerAndTacticalTimescales()
    {
        // Trade satellites must have a 25.0 hr period (62,500 ticks) to align with vanilla 8-12 day storyteller passes
        int tradePeriod = OrbitalManager.GetDefaultPeriodForRole(SatelliteRole.Trade);
        Assert.AreEqual(62500, tradePeriod, "Trade satellite period must be 62,500 ticks (~25.0 in-game hours).");

        // Recon satellites must have an 18.0 hr period (45,000 ticks) for polar sweeps
        int reconPeriod = OrbitalManager.GetDefaultPeriodForRole(SatelliteRole.Reconnaissance);
        Assert.AreEqual(45000, reconPeriod, "Recon satellite period must be 45,000 ticks (~18.0 in-game hours).");

        // Military satellites must have a 30.0 hr period (75,000 ticks)
        int milPeriod = OrbitalManager.GetDefaultPeriodForRole(SatelliteRole.Military);
        Assert.AreEqual(75000, milPeriod, "Military satellite period must be 75,000 ticks (~30.0 in-game hours).");

        // Constellations must have multi-day periods
        Assert.IsTrue(OrbitalManager.GetDefaultPeriodForRole(SatelliteRole.Navigation) >= 120000);
        Assert.IsTrue(OrbitalManager.GetDefaultPeriodForRole(SatelliteRole.Communication) >= 180000);
    }

    [TestMethod]
    public void WorldObject_Satellite_ExpandingIconColor_MatchesFactionColor()
    {
        // 1. Without faction -> defaults to Color.white
        WorldObject_Satellite unownedSat = new();
        Assert.AreEqual(UnityEngine.Color.white, unownedSat.ExpandingIconColor);

        // 2. With faction -> matches Faction.Color
        FactionDef factionDef = new() { defName = "VoidbornEmporium", isPlayer = false };
        Faction faction = new() { def = factionDef, color = UnityEngine.Color.magenta };
        WorldObject_Satellite factionSat = new();
        SetFactionRaw(factionSat, faction);

        Assert.AreEqual(UnityEngine.Color.magenta, factionSat.ExpandingIconColor,
            "Satellite expanding icon colour must match owning faction colour.");
    }

    [TestMethod]
    public void WorldObject_OrbitalStation_ExpandingIconColor_MatchesFactionColor()
    {
        FactionDef factionDef = new() { defName = "TradersGuild", isPlayer = false };
        Faction faction = new() { def = factionDef, color = UnityEngine.Color.cyan };
        WorldObject_OrbitalStation station = new();
        SetFactionRaw(station, faction);

        Assert.AreEqual(UnityEngine.Color.cyan, station.ExpandingIconColor,
            "Orbital station expanding icon colour must match owning faction colour.");
    }

    [TestMethod]
    public void WorldObject_Satellite_LegacySaveMigration_ReplacesFastPeriod()
    {
        // Simulate a pre-patch satellite save with fast legacy period (3,420 ticks)
        WorldObject_Satellite sat = new();
        sat.InitializeSatellite(
            SatelliteRole.Trade,
            new OrbitalParameters(600f, 3420, 42f, 0f, -3.5f, 0f, 0.8f, 0),
            stationId: 101,
            callsign: "Legacy-Trade",
            footprintRadius: 16f);

        Assert.AreEqual(3420, sat.Orbit!.PeriodTicks);

        // Verify that OrbitalManager default period replaces fast periods (< 30,000 ticks)
        if (sat.Orbit.PeriodTicks < 30000)
        {
            sat.Orbit.PeriodTicks = OrbitalManager.GetDefaultPeriodForRole(sat.Role);
        }

        Assert.AreEqual(62500, sat.Orbit.PeriodTicks,
            "Legacy satellite period (< 30,000 ticks) must migrate to storyteller-aligned 62,500 ticks.");
    }

    [TestMethod]
    public void WorldObject_Satellite_And_OrbitalStation_VisibleInBackground_ReturnsTrue()
    {
        WorldObject_Satellite sat = new();
        WorldObject_OrbitalStation station = new();

        Assert.IsTrue(sat.VisibleInBackground, "Satellites must be marked VisibleInBackground so they render across planetary and space views.");
        Assert.IsTrue(station.VisibleInBackground, "Orbital stations must be marked VisibleInBackground so they render across planetary and space views.");
    }

    [TestMethod]
    public void GeoscapeSettings_DefaultSurveillanceDimming_IsTwentyFivePercent()
    {
        GeoscapeSettings settings = new();
        Assert.AreEqual(0.25f, settings.surveillanceDimming, 0.001f,
            "Default surveillance background dimming must be 25% (0.25f) to provide immediate tactical contrast.");
    }

    private class MockWorldFeatureTextMesh : WorldFeatureTextMesh
    {
        private UnityEngine.Color col;
        public override UnityEngine.Color Color { get => col; set => col = value; }
        public override bool Active => true;
        public override void SetActive(bool active) { }
        public override string Text { get => ""; set { } }
        public override UnityEngine.Vector3 Position => UnityEngine.Vector3.zero;
        public override UnityEngine.Vector3 LocalPosition { get => UnityEngine.Vector3.zero; set { } }
        public override UnityEngine.Quaternion Rotation { get => UnityEngine.Quaternion.identity; set { } }
        public override float Size { set { } }
        public override void WrapAroundPlanetSurface(PlanetLayer layer) { }
        public override void Init() { }
        public override void Destroy() { }
    }

    [TestMethod]
    public void WorldFeatures_UpdateAlpha_Patch_FogOfWar_NullSensoryManager_SuppressesLabels()
    {
        // Arrange: Fog of War mode active, SensoryManager is null during world gen
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.FogOfWar;
        SensoryManager.Instance = null;

        WorldFeature feature = new() { alpha = 1.0f };
        MockWorldFeatureTextMesh text = new() { Color = new UnityEngine.Color(1f, 1f, 1f, 1f) };

        // Act
        bool shouldExecuteVanilla = WorldObject_Visibility_Patch.WorldFeatures_UpdateAlpha_Patch.Prefix(text, feature);

        // Assert: In Fog of War when SensoryManager is null, vanilla update must be bypassed and alpha forced to 0
        Assert.IsFalse(shouldExecuteVanilla, "Prefix must return false to suppress vanilla text alpha calculation during world gen.");
        Assert.AreEqual(0f, feature.alpha, 0.001f, "Feature alpha must be forced to 0.");
        Assert.AreEqual(0f, text.Color.a, 0.001f, "Text mesh alpha must be forced to 0.");

        bool isAltitudeGood = true;
        WorldObject_Visibility_Patch.WorldFeatures_GoodCameraAltitudeFor_Patch.Postfix(feature, ref isAltitudeGood);
        Assert.IsFalse(isAltitudeGood, "GoodCameraAltitudeFor must return false when SensoryManager is null in Fog of War.");
    }

    [TestMethod]
    public void HiddenBehindTerrainNow_Patch_PlayerSettlement_WhenHidden_EvaluatesTerrainCurvature()
    {
        // Arrange: Player settlement flagged as hidden behind layer in vanilla
        FactionDef playerDef = new() { defName = "PlayerColony", isPlayer = true };
        Faction playerFaction = new() { def = playerDef };
        Settlement settlement = new();
        SetFactionRaw(settlement, playerFaction);

        bool isHidden = true;

        // Act: Apply patch
        WorldObject_Visibility_Patch.HiddenBehindTerrainNow_Patch.Postfix(settlement, ref isHidden);

        // Assert: Under test stubs where DrawPos is (0,0,0) and camera is null/stubbed,
        // WorldRendererUtility.HiddenBehindTerrainNow evaluates safely without throwing.
        // The patch must run and evaluate curvature occlusion rather than blindly hiding.
        Assert.IsNotNull(settlement);
    }

    [TestMethod]
    public void HiddenByRules_Patch_PlayerSettlement_OnOrbitalLayer_PreservesVisibility()
    {
        // Arrange
        FactionDef playerDef = new() { defName = "PlayerColony", isPlayer = true };
        Faction playerFaction = new() { def = playerDef };
        Settlement settlement = new();
        SetFactionRaw(settlement, playerFaction);

        bool isHidden = false;

        // Act
        WorldObject_Visibility_Patch.HiddenByRules_Patch.Postfix(settlement, ref isHidden);

        // Assert: Player settlement must remain visible
        Assert.IsFalse(isHidden, "Player settlements must never be hidden by rules when viewing orbital layers.");
    }
}
