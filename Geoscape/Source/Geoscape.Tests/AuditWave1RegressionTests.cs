using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Core.Modules;
using ProjectVigilance.Geoscape;
using ProjectVigilance.Geoscape.Rules;
using ProjectVigilance.Geoscape.Sensory;
using ProjectVigilance.Geoscape.World;
using RimWorld;
using RimWorld.Planet;
using Unity.Collections;
using UnityEngine;
using Verse;

namespace Geoscape.Tests;

[TestClass]
[DoNotParallelize]
public sealed class AuditWave1RegressionTests : GeoscapeTestBase
{
    private static readonly FieldInfo? ExploredBytesField =
        typeof(SensoryManager).GetField("exploredBytes", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo? IdentifiedBytesField =
        typeof(SensoryManager).GetField("identifiedBytes", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo? PlanetLayerTilesField =
        AccessTools.Field(typeof(PlanetLayer), "tiles");

    private static readonly FieldInfo? PlanetLayerNeighborsOffsetsField =
        AccessTools.Field(typeof(PlanetLayer), "tileIDToNeighbors_offsets");

    [TestMethod]
    public void IsOrbitalEntityDetected_UnhookedFallback_RequiresOmniscientForNonPlayer()
    {
        VigilanceModuleManager.IsOrbitalEntityDetectedHook = null;
        VisibilityMode savedMode = VisibilityMode.Omniscient;
        VigilanceModuleManager.GetVisibilityModeHook = () => savedMode;

        WorldObject dummyOrbital = new();

        savedMode = VisibilityMode.ClassicHazeOfWar;
        Assert.IsFalse(VigilanceModuleManager.IsOrbitalEntityDetected(dummyOrbital));

        savedMode = VisibilityMode.PlanetarySurveillance;
        Assert.IsFalse(VigilanceModuleManager.IsOrbitalEntityDetected(dummyOrbital));

        savedMode = VisibilityMode.Omniscient;
        Assert.IsTrue(VigilanceModuleManager.IsOrbitalEntityDetected(dummyOrbital));
    }

    [TestMethod]
    public void SonarGatingUtility_ShouldOfferSonarDeployment_RespectsDevModeAndAquaticsGate()
    {
        bool devMode = false;
        SonarGatingUtility.DevModeOverrideForTesting = () => devMode;
        try
        {
            VigilanceModuleManager.SetModuleActiveOverride(VigilanceModuleId.Aquatics, true);
            VigilanceModuleManager.AllowSonarInTests = true;
            Assert.IsFalse(SonarGatingUtility.ShouldOfferSonarDeployment(PlanetTile.Invalid));

            devMode = true;
            VigilanceModuleManager.AllowSonarInTests = false;
            VigilanceModuleManager.SetModuleActiveOverride(VigilanceModuleId.Aquatics, false);
            Assert.IsFalse(SonarGatingUtility.ShouldOfferSonarDeployment(PlanetTile.Invalid));
        }
        finally
        {
            SonarGatingUtility.DevModeOverrideForTesting = null;
        }
    }

    [TestMethod]
    public void RuleLedgerSigner_CanonicalV3PreventsPipeCollision_AndVerifiesLegacyV2()
    {
        const string campaignId = "camp-01";
        string hashA = RuleLedgerSigner.ComputeRecordHash(
            RuleLedgerSigner.GenesisHash, 250, RuleChangeKind.Escalation, "k", "a|b", "c", "n", campaignId);
        string hashB = RuleLedgerSigner.ComputeRecordHash(
            RuleLedgerSigner.GenesisHash, 250, RuleChangeKind.Escalation, "k", "a", "b|c", "n", campaignId);

        Assert.AreNotEqual(hashA, hashB);

        var v3Record = new RuleChangeRecord(250, RuleChangeKind.Escalation, "k", "a|b", "c", "n", hashA);
        Assert.IsTrue(RuleLedgerSigner.VerifyRecordHash(v3Record, RuleLedgerSigner.GenesisHash, campaignId));

        string legacyV2Hash = RuleLedgerSigner.ComputeLegacyV2RecordHash(
            RuleLedgerSigner.GenesisHash, 250, RuleChangeKind.Escalation, "k", "from", "to", "note", campaignId);
        var v2Record = new RuleChangeRecord(250, RuleChangeKind.Escalation, "k", "from", "to", "note", legacyV2Hash);
        Assert.IsTrue(RuleLedgerSigner.VerifyRecordHash(v2Record, RuleLedgerSigner.GenesisHash, campaignId));
    }

    [TestMethod]
    public void TacticalAmberPulseColor_HonoursReduceShroudMotion()
    {
        GeoscapeMod.Settings.reduceShroudMotion = true;
        Color c = WorldObject_Incursion.TacticalAmberPulseColor(0.5f);
        Assert.AreEqual(0.92f * 0.5f, c.a, 0.001f);
    }

    [TestMethod]
    public void SensoryManager_FinalizeInit_ClampsOversizedBitsetsToWorldTilesCount()
    {
        GeoscapeTestHarness.InitMockWorld(out PlanetLayer surfaceLayer);
        var tiles = new List<Tile>(64);
        for (int i = 0; i < 64; i++)
        {
            tiles.Add(new SurfaceTile(new PlanetTile(i, surfaceLayer)));
        }
        PlanetLayerTilesField!.SetValue(surfaceLayer, tiles);
        PlanetLayerNeighborsOffsetsField!.SetValue(surfaceLayer, new NativeArray<int>(64, Allocator.Temp));

        var mgr = new SensoryManager(Find.World);
        SensoryManager.Instance = mgr;

        ExploredBytesField!.SetValue(mgr, new byte[4096]);
        IdentifiedBytesField!.SetValue(mgr, new byte[4096]);

        mgr.FinalizeInit(fromLoad: true);

        byte[]? explored = ExploredBytesField.GetValue(mgr) as byte[];
        byte[]? identified = IdentifiedBytesField.GetValue(mgr) as byte[];
        int expectedBytes = (64 + 7) / 8;

        Assert.IsNotNull(explored);
        Assert.IsNotNull(identified);
        Assert.AreEqual(expectedBytes, explored.Length);
        Assert.AreEqual(expectedBytes, identified.Length);
    }
}
