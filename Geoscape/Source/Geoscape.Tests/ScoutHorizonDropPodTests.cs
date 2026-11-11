using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape;
using ProjectVigilance.Geoscape.Patches;
using ProjectVigilance.Geoscape.Rules;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld;
using Verse;

namespace Geoscape.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ScoutHorizonDropPodTests
{
    private VisibilityMode originalMode;

    [TestInitialize]
    public void Init()
    {
        GeoscapeCampaignRules.ResetStaticDataForTesting();
        originalMode = GeoscapeMod.Settings.visibilityMode;
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;
    }

    [TestCleanup]
    public void Cleanup()
    {
        GeoscapeMod.Settings.visibilityMode = originalMode;
        SensoryManager.IsExploredOverride = null;
        SensoryManager.IsIdentifiedOverride = null;
        SensoryManager.Instance = null;
        GeoscapeCampaignRules.ResetStaticDataForTesting();
    }

    [TestMethod]
    public void ContainsActiveOrArrivingColonist_IgnoresEmptyAndLeavingTransporters()
    {
        bool anyNightOwl = false;
        Assert.IsFalse(ScoutHorizonSensor.ContainsActiveOrArrivingColonist(null, requireAwake: false, ref anyNightOwl));
        Assert.IsFalse(ScoutHorizonSensor.ContainsActiveOrArrivingColonist(Array.Empty<Thing>(), requireAwake: true, ref anyNightOwl));

        var leaving = new FlyShipLeaving();
        var emptyIncoming = new DropPodIncoming();
        Assert.IsFalse(ScoutHorizonSensor.ContainsActiveOrArrivingColonist(new Thing[] { leaving, emptyIncoming }, requireAwake: true, ref anyNightOwl));
    }

    [TestMethod]
    public void ContainsActiveOrArrivingColonist_DetectsPlayerColonistInDropPodAndIgnoresHostiles()
    {
        Faction playerFaction = new() { def = new FactionDef { isPlayer = true } };
        Faction raiderFaction = new() { def = new FactionDef { isPlayer = false } };
        ThingDef humanDef = new() { race = new RaceProperties { intelligence = Intelligence.Humanlike } };

        Pawn playerColonist = new() { def = humanDef };
        Pawn raider = new() { def = humanDef };
        var factionField = AccessTools.Field(typeof(Thing), "factionInt");
        factionField.SetValue(playerColonist, playerFaction);
        factionField.SetValue(raider, raiderFaction);
        playerColonist.health = new Pawn_HealthTracker(playerColonist);
        raider.health = new Pawn_HealthTracker(raider);

        var innerListField = AccessTools.Field(typeof(ThingOwner<Thing>), "innerList");

        var hostileTransporter = new ActiveTransporter
        {
            Contents = new ActiveTransporterInfo()
        };
        ((List<Thing>)innerListField.GetValue(hostileTransporter.Contents.innerContainer)!).Add(raider);
        var hostilePod = new DropPodIncoming();
        ((List<Thing>)innerListField.GetValue(hostilePod.innerContainer)!).Add(hostileTransporter);

        bool nightOwl = false;
        Assert.IsFalse(ScoutHorizonSensor.ContainsActiveOrArrivingColonist(
            new Thing[] { hostilePod },
            requireAwake: true,
            ref nightOwl));

        var playerTransporter = new ActiveTransporter
        {
            Contents = new ActiveTransporterInfo()
        };
        ((List<Thing>)innerListField.GetValue(playerTransporter.Contents.innerContainer)!).Add(playerColonist);
        var playerPod = new DropPodIncoming();
        ((List<Thing>)innerListField.GetValue(playerPod.innerContainer)!).Add(playerTransporter);

        Assert.IsTrue(ScoutHorizonSensor.ContainsActiveOrArrivingColonist(
            new Thing[] { playerPod },
            requireAwake: false,
            ref nightOwl));

        Assert.IsTrue(ScoutHorizonSensor.ContainsActiveOrArrivingColonist(
            new Thing[] { playerPod },
            requireAwake: true,
            ref nightOwl));
    }

    [TestMethod]
    public void InitialVisionRefreshPatches_ResolveAllRimWorldTargetsWithoutError()
    {
        string id = $"Geoscape.Tests.InitialVision.{Guid.NewGuid():N}";
        Harmony harmony = new(id);
        try
        {
            Type[] patchTypes =
            {
                typeof(InitialVisionRefresh_Patch.Game_InitNewGame_Patch),
                typeof(InitialVisionRefresh_Patch.CaravanEnterMapUtility_Enter_Patch),
                typeof(InitialVisionRefresh_Patch.Pawn_SpawnSetup_Patch)
            };

            foreach (Type patchType in patchTypes)
            {
                harmony.CreateClassProcessor(patchType).Patch();
            }
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }

    [TestMethod]
    public void NotifySensorRadiusChanged_IncrementsSweepRevision()
    {
        var sensory = new SensoryManager(null!);
        SensoryManager.Instance = sensory;
        int initialRevision = sensory.SweepRevision;

        sensory.Notify_SensorRadiusChanged();

        Assert.IsTrue(sensory.SweepRevision > initialRevision);
    }
}
