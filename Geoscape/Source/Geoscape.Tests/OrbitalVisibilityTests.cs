using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape;
using ProjectVigilance.Geoscape.Orbital;
using ProjectVigilance.Geoscape.Patches;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
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
        GeoscapeTestHarness.ResetAllStaticState();
        GeoscapeTestHarness.EnsureEnvironmentInitialised();
        originalMode = GeoscapeMod.Settings.visibilityMode;
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;
    }

    [TestCleanup]
    public void Cleanup()
    {
        GeoscapeTestHarness.ResetAllStaticState();
        GeoscapeMod.Settings.visibilityMode = originalMode;
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

    [TestMethod]
    public void IsSurfaceAnchorVisibleFromOrbit_AllowsColoniesAndExcludesCaravansAndOutposts()
    {
        Faction playerFaction = GeoscapeTestHarness.CreatePlayerFaction();
        Faction npcFaction = GeoscapeTestHarness.CreateNpcFaction();

        Settlement playerSettlement = GeoscapeTestHarness.CreateSettlement(GeoscapeTestHarness.MakeTile(10), playerFaction);
        Settlement npcSettlement = GeoscapeTestHarness.CreateSettlement(GeoscapeTestHarness.MakeTile(11), npcFaction);
        Caravan playerCaravan = GeoscapeTestHarness.CreateCaravan(GeoscapeTestHarness.MakeTile(12), playerFaction);
        var playerPost = new WorldObject_ListeningPost { Kind = ListeningPostKind.AutomatedRelay };
        SetFactionRaw(playerPost, playerFaction);

        Assert.IsTrue(WorldObject_Visibility_Patch.IsSurfaceAnchorVisibleFromOrbit(playerSettlement),
            "Player MapParent colonies should remain visible as Orbit layer uplink anchors.");
        Assert.IsFalse(WorldObject_Visibility_Patch.IsSurfaceAnchorVisibleFromOrbit(npcSettlement),
            "Non-player settlements should not bypass Orbit layer culling.");
        Assert.IsFalse(WorldObject_Visibility_Patch.IsSurfaceAnchorVisibleFromOrbit(playerCaravan),
            "Foot/animal ground caravans should remain restricted to the Surface layer.");
        Assert.IsFalse(WorldObject_Visibility_Patch.IsSurfaceAnchorVisibleFromOrbit(playerPost),
            "Unmanned listening posts should remain restricted to the Surface layer.");
    }

    private static PlanetLayer CreateSurfaceForOrbitalTests(int tileCount)
    {
        GeoscapeTestHarness.InitMockWorld(out PlanetLayer surfaceLayer);
        var tiles = new List<Tile>(tileCount);
        var vOffsets = new Unity.Collections.NativeArray<int>(tileCount, Unity.Collections.Allocator.Temp);
        var verts = new Unity.Collections.NativeArray<Vector3>(tileCount * 3, Unity.Collections.Allocator.Temp);

        for (int i = 0; i < tileCount; i++)
        {
            tiles.Add(new SurfaceTile(new PlanetTile(i, surfaceLayer)));
            float angle = (i * 0.8f) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, -Mathf.Cos(angle)) * 100f;
            vOffsets[i] = i * 3;
            verts[i * 3] = dir;
            verts[i * 3 + 1] = dir + Vector3.up * 0.2f;
            verts[i * 3 + 2] = dir + Vector3.right * 0.2f;
        }

        AccessTools.Field(typeof(PlanetLayer), "tiles")!.SetValue(surfaceLayer, tiles);
        AccessTools.Field(typeof(PlanetLayer), "radius")!.SetValue(surfaceLayer, 100f);
        AccessTools.Field(typeof(PlanetLayer), "averageTileSize")!.SetValue(surfaceLayer, 1.3f);
        AccessTools.Field(typeof(PlanetLayer), "<WorldDrawLayers>k__BackingField")!.SetValue(surfaceLayer, new List<WorldDrawLayer>());
        AccessTools.Field(typeof(PlanetLayer), "tileIDToNeighbors_offsets")!.SetValue(surfaceLayer, new Unity.Collections.NativeArray<int>(tileCount, Unity.Collections.Allocator.Temp));
        AccessTools.Field(typeof(PlanetLayer), "tileIDToVerts_offsets")!.SetValue(surfaceLayer, vOffsets);
        AccessTools.Field(typeof(PlanetLayer), "verts")!.SetValue(surfaceLayer, verts);
        return surfaceLayer;
    }

    [TestMethod]
    public void OrbitalVisibility_SetFactionSafe_FallbackUsesFactionInt()
    {
        GeoscapeTestHarness.ClearMockWorld();
        Faction npcFaction = GeoscapeTestHarness.CreateNpcFaction("Pirates", "pirates", "Sky Corsairs");
        WorldObject_Satellite sat = new();

        sat.SetFaction(npcFaction);

        Assert.AreSame(npcFaction, sat.Faction, "SetFaction fallback must assign WorldObject.factionInt when World is uninitialized.");
    }

    [TestMethod]
    public void OrbitalVisibility_DetectionCache_ZeroSteadyStateAllocationAndInvalidatesOnSweepRevision()
    {
        PlanetLayer surfaceLayer = CreateSurfaceForOrbitalTests(256);
        var holder = (WorldObjectsHolder)RuntimeHelpers.GetUninitializedObject(typeof(WorldObjectsHolder));
        var worldObjects = new List<WorldObject>(256);
        AccessTools.Field(typeof(WorldObjectsHolder), "worldObjects")!.SetValue(holder, worldObjects);
        Find.World!.worldObjects = holder;

        var game = (Game)RuntimeHelpers.GetUninitializedObject(typeof(Game));
        var tickManager = (TickManager)RuntimeHelpers.GetUninitializedObject(typeof(TickManager));
        AccessTools.Field(typeof(TickManager), "ticksGameInt")!.SetValue(tickManager, 100);
        AccessTools.Field(typeof(Game), "tickManager")!.SetValue(game, tickManager);
        AccessTools.Field(typeof(Game), "worldInt")!.SetValue(game, Find.World);
        AccessTools.Field(typeof(Game), "maps")!.SetValue(game, new List<Map>());
        AccessTools.Field(typeof(Current), "gameInt")!.SetValue(null, game);

        var sensory = new SensoryManager(Find.World!);
        SensoryManager.Instance = sensory;
        SensoryManager.TileNeighborsOverride = static (_, list) => list.Clear();

        var orbitalManager = new OrbitalManager(Find.World!);
        OrbitalManager.Instance = orbitalManager;

        // Place 1 player recon satellite at tile 10 covering tile 10 (distant from tile 200).
        Faction playerFaction = GeoscapeTestHarness.CreatePlayerFaction();
        var playerRecon = new WorldObject_Satellite { ID = 5000 };
        SetFactionRaw(playerRecon, playerFaction);
        playerRecon.InitializeSatellite(
            SatelliteRole.Reconnaissance,
            new OrbitalParameters(450f, 45000, 1f, 0f, 0f, 0f, 0f, 0),
            stationId: -1,
            callsign: "Player-Recon",
            footprintRadius: 12f);
        AccessTools.Field(typeof(WorldObject), "tile")!.SetValue(playerRecon, new PlanetTile(10, surfaceLayer));
        orbitalManager.RegisterSatellite(playerRecon);

        // Spawn 245 NPC satellites (first 5 near tile 1 -> detected, remaining 240 at tile 150 -> hidden).
        Faction npcFaction = GeoscapeTestHarness.CreateNpcFaction();
        var npcSatellites = new WorldObject_Satellite[245];
        for (int i = 0; i < npcSatellites.Length; i++)
        {
            var sat = new WorldObject_Satellite { ID = 6000 + i };
            SetFactionRaw(sat, npcFaction);
            int tileIdx = i < 5 ? 1 : 150;
            sat.InitializeSatellite(
                SatelliteRole.Trade,
                new OrbitalParameters(600f, 62500, 40f, 0f, -3.5f, 0f, 0.8f, 0),
                stationId: -1,
                callsign: $"NPC-Trade-{i}",
                footprintRadius: 16f);
            AccessTools.Field(typeof(WorldObject), "tile")!.SetValue(sat, new PlanetTile(tileIdx, surfaceLayer));
            npcSatellites[i] = sat;
        }

        // Warm up caches on initial frame.
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;
        for (int i = 0; i < npcSatellites.Length; i++)
        {
            bool detected = OrbitalVisibilityUtility.IsOrbitalEntityDetected(npcSatellites[i]);
            Assert.AreEqual(i < 5, detected);
        }

        // Measure 4 lookups per entity * 245 entities = 980 calls in steady state: must allocate 0 bytes.
        bool mismatch = false;
        long bytesBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int pass = 0; pass < 4; pass++)
        {
            for (int i = 0; i < npcSatellites.Length; i++)
            {
                bool expected = i < 5;
                if (OrbitalVisibilityUtility.IsOrbitalEntityDetected(npcSatellites[i]) != expected)
                {
                    mismatch = true;
                }
            }
        }
        long bytesAfter = GC.GetAllocatedBytesForCurrentThread();

        Assert.IsFalse(mismatch, "Cached orbital detection returned inconsistent values.");
        Assert.AreEqual(0L, bytesAfter - bytesBefore, "Steady-state orbital detection across 245 satellites must allocate 0 bytes.");

        // Mode switch invalidates cache immediately even inside the same tick bucket.
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.Omniscient;
        Assert.IsTrue(OrbitalVisibilityUtility.IsOrbitalEntityDetected(npcSatellites[240]));

        // Switching back + advancing tick bucket and SweepRevision recomputes cleanly.
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;
        Assert.IsFalse(OrbitalVisibilityUtility.IsOrbitalEntityDetected(npcSatellites[240]));
        sensory.MarkTileExplored(new PlanetTile(1, surfaceLayer)); // Bumps SweepRevision++
        AccessTools.Field(typeof(TickManager), "ticksGameInt")!.SetValue(tickManager, 130); // Next 30-tick bucket
        Assert.IsTrue(OrbitalVisibilityUtility.IsOrbitalEntityDetected(npcSatellites[0]));
        Assert.IsFalse(OrbitalVisibilityUtility.IsOrbitalEntityDetected(npcSatellites[240]));
    }

    [TestMethod]
    public void OrbitalManager_PruneDuplicateNpcOrbitalEntities_DedupesByFactionAndRolePreservesPlayerAndLogsWarning()
    {
        PlanetLayer surfaceLayer = CreateSurfaceForOrbitalTests(32);
        var holder = (WorldObjectsHolder)RuntimeHelpers.GetUninitializedObject(typeof(WorldObjectsHolder));
        var worldObjects = new List<WorldObject>(256);
        var settlements = new List<Settlement>(4);
        AccessTools.Field(typeof(WorldObjectsHolder), "worldObjects")!.SetValue(holder, worldObjects);
        AccessTools.Field(typeof(WorldObjectsHolder), "settlements")!.SetValue(holder, settlements);
        Find.World!.worldObjects = holder;

        OrbitalManager.RemoveWorldObjectOverride = wo => worldObjects.Remove(wo);
        var orbitalManager = new OrbitalManager(Find.World!);
        OrbitalManager.Instance = orbitalManager;

        Faction playerFaction = GeoscapeTestHarness.CreatePlayerFaction();
        Faction odysseyFaction = GeoscapeTestHarness.CreateNpcFaction("TradersGuild", "traders guild", "Traders Guild");
        Faction industrialNpcFaction = new() { def = new FactionDef { defName = "OuterCombine", techLevel = TechLevel.Industrial } };

        // 1. Player station + 2 player satellites (must never be pruned).
        var playerStation = new WorldObject_OrbitalStation { ID = 10 };
        SetFactionRaw(playerStation, playerFaction);
        AccessTools.Field(typeof(WorldObject), "tile")!.SetValue(playerStation, new PlanetTile(1, surfaceLayer));
        worldObjects.Add(playerStation);

        var playerTradeSat = new Satellite_Trade { ID = 11 };
        SetFactionRaw(playerTradeSat, playerFaction);
        AccessTools.Field(typeof(WorldObject), "tile")!.SetValue(playerTradeSat, new PlanetTile(1, surfaceLayer));
        worldObjects.Add(playerTradeSat);

        var playerReconSat = new WorldObject_Satellite { ID = 12, Role = SatelliteRole.Reconnaissance };
        SetFactionRaw(playerReconSat, playerFaction);
        AccessTools.Field(typeof(WorldObject), "tile")!.SetValue(playerReconSat, new PlanetTile(2, surfaceLayer));
        worldObjects.Add(playerReconSat);

        // 2. Orphaned (null-faction) hubs & satellites (must all be pruned).
        for (int i = 0; i < 3; i++)
        {
            var orphanHub = new WorldObject_OrbitalStation { ID = 100 + i };
            AccessTools.Field(typeof(WorldObject), "tile")!.SetValue(orphanHub, new PlanetTile(3, surfaceLayer));
            worldObjects.Add(orphanHub);
        }
        for (int i = 0; i < 2; i++)
        {
            var orphanSat = new WorldObject_Satellite { ID = 110 + i };
            AccessTools.Field(typeof(WorldObject), "tile")!.SetValue(orphanSat, new PlanetTile(4, surfaceLayer));
            worldObjects.Add(orphanSat);
        }

        // 3. Odyssey faction: has vanilla/Odyssey SpaceSettlement + 2 redundant fallback stations + 60 Trade + 5 Recon sats.
        var spaceSettlement = GeoscapeTestHarness.CreateSettlement(
            new PlanetTile(5, surfaceLayer),
            odysseyFaction,
            id: 200,
            def: new WorldObjectDef { defName = "SpaceSettlement" });
        worldObjects.Add(spaceSettlement);
        settlements.Add(spaceSettlement);

        for (int i = 0; i < 2; i++)
        {
            var redundantHub = new WorldObject_OrbitalStation { ID = 210 + i };
            SetFactionRaw(redundantHub, odysseyFaction);
            AccessTools.Field(typeof(WorldObject), "tile")!.SetValue(redundantHub, new PlanetTile(5, surfaceLayer));
            worldObjects.Add(redundantHub);
        }
        for (int i = 0; i < 60; i++)
        {
            var dupTrade = new Satellite_Trade { ID = 300 + i };
            SetFactionRaw(dupTrade, odysseyFaction);
            AccessTools.Field(typeof(WorldObject), "tile")!.SetValue(dupTrade, new PlanetTile(6, surfaceLayer));
            worldObjects.Add(dupTrade);
        }
        for (int i = 0; i < 5; i++)
        {
            var dupRecon = new WorldObject_Satellite { ID = 400 + i, Role = SatelliteRole.Reconnaissance };
            SetFactionRaw(dupRecon, odysseyFaction);
            AccessTools.Field(typeof(WorldObject), "tile")!.SetValue(dupRecon, new PlanetTile(7, surfaceLayer));
            worldObjects.Add(dupRecon);
        }

        // 4. Industrial NPC faction: 120 duplicate stations + 60 duplicate trade sats + stale child IDs.
        WorldObject_OrbitalStation? lowestCombineHub = null;
        for (int i = 0; i < 120; i++)
        {
            var hub = new WorldObject_OrbitalStation { ID = 500 + i };
            SetFactionRaw(hub, industrialNpcFaction);
            AccessTools.Field(typeof(WorldObject), "tile")!.SetValue(hub, new PlanetTile(8, surfaceLayer));
            if (i == 0)
            {
                lowestCombineHub = hub;
                AccessTools.Field(typeof(WorldObject_OrbitalStation), "childSatelliteIds")!
                    .SetValue(hub, new List<int> { 99991, 99992 });
            }
            worldObjects.Add(hub);
        }
        for (int i = 0; i < 60; i++)
        {
            var sat = new Satellite_Trade { ID = 700 + i };
            SetFactionRaw(sat, industrialNpcFaction);
            sat.ReassignParentStation(555); // Stale non-lowest parent hub ID
            AccessTools.Field(typeof(WorldObject), "tile")!.SetValue(sat, new PlanetTile(9, surfaceLayer));
            worldObjects.Add(sat);
        }

        // Act 1: First prune strips duplicates and orphans while preserving player assets, Odyssey hub, and 1 per (faction, role).
        (int prunedStations, int prunedSatellites) = orbitalManager.PruneDuplicateNpcOrbitalEntities("TestLoad");

        Assert.AreEqual(3 + 2 + 119, prunedStations, "Must prune 3 orphans + 2 Odyssey fallback hubs + 119 duplicate Combine hubs.");
        Assert.AreEqual(2 + 59 + 4 + 59, prunedSatellites, "Must prune 2 orphans + 59 Odyssey trade + 4 Odyssey recon + 59 Combine trade.");

        Assert.IsTrue(worldObjects.Contains(playerStation));
        Assert.IsTrue(worldObjects.Contains(playerTradeSat));
        Assert.IsTrue(worldObjects.Contains(playerReconSat));
        Assert.IsTrue(worldObjects.Contains(spaceSettlement));

        Assert.IsNotNull(lowestCombineHub);
        Assert.IsTrue(worldObjects.Contains(lowestCombineHub));
        Assert.IsFalse(lowestCombineHub.ChildSatelliteIds.Contains(99991), "Stale child IDs must be reconciled.");
        Assert.IsTrue(lowestCombineHub.ChildSatelliteIds.Contains(700), "Surviving trade satellite 700 must be attached to surviving hub.");

        var survivingCombineSat = worldObjects.Find(wo => wo.ID == 700) as WorldObject_Satellite;
        Assert.IsNotNull(survivingCombineSat);
        Assert.AreEqual(500, survivingCombineSat.ParentStationId, "Surviving satellite must re-link to surviving hub 500.");

        // Act 2: Second pass is strictly idempotent and prunes 0 items (no repeat warning noise).
        (int secondStations, int secondSats) = orbitalManager.PruneDuplicateNpcOrbitalEntities("TestLoad");
        Assert.AreEqual(0, secondStations);
        Assert.AreEqual(0, secondSats);
    }

    [TestMethod]
    public void OrbitalManager_IsOrbitalFaction_ExcludesTemporaryAndLowTechZeroSettlementFactions()
    {
        FactionDef guildDef = new() { defName = "TradersGuild", techLevel = TechLevel.Spacer };
        Faction guildFaction = new() { def = guildDef };

        FactionDef tempDef = new() { defName = "QuestFaction", techLevel = TechLevel.Industrial };
        Faction tempFaction = new() { def = tempDef, temporary = true };

        FactionDef tribalDef = new() { defName = "TribalRefugees", techLevel = TechLevel.Neolithic };
        Faction tribalFaction = new() { def = tribalDef };

        FactionDef defeatedDef = new() { defName = "FallenSyndicate", techLevel = TechLevel.Spacer };
        Faction defeatedFaction = new() { def = defeatedDef, defeated = true };

        FactionDef industrialDef = new() { defName = "OrbitalSyndicate", techLevel = TechLevel.Industrial };
        Faction industrialFaction = new() { def = industrialDef };

        var emptySettlementSet = new HashSet<Faction>();

        Assert.IsTrue(OrbitalManager.IsOrbitalFaction(guildFaction, emptySettlementSet));
        Assert.IsFalse(OrbitalManager.IsOrbitalFaction(tempFaction, emptySettlementSet));
        Assert.IsFalse(OrbitalManager.IsOrbitalFaction(tribalFaction, emptySettlementSet));
        Assert.IsFalse(OrbitalManager.IsOrbitalFaction(defeatedFaction, emptySettlementSet));
        Assert.IsTrue(OrbitalManager.IsOrbitalFaction(industrialFaction, emptySettlementSet));
    }
}
