using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RimWorld;
using RimWorld.Planet;
using Verse;
using ProjectVigilance.Geoscape.Sensory;

namespace Geoscape.Tests;

[TestClass]
[DoNotParallelize]
public sealed class CaravanExpeditionTests
{
    [TestInitialize]
    public void Setup()
    {
        CaravanExpeditionUtility.ClearAllPendingDirectives();
    }

    [TestCleanup]
    public void Cleanup()
    {
        CaravanExpeditionUtility.ClearAllPendingDirectives();
    }

    [TestMethod]
    public void CaravanArrivalAction_ConstructOutpost_PropertiesAndLabels_AutomatedRelay()
    {
        var action = new CaravanArrivalAction_ConstructOutpost((PlanetTile)4500, ListeningPostKind.AutomatedRelay);

        Assert.AreEqual((PlanetTile)4500, action.TargetTile);
        Assert.AreEqual(ListeningPostKind.AutomatedRelay, action.Kind);
        Assert.IsNull(action.StuffDef);
        Assert.AreEqual("Establish Automated Listening Post", action.Label);
        Assert.AreEqual("travelling to establish automated listening post", action.ReportString);

        // Validation against unspawned/null caravan
        var report = action.StillValid(null!, (PlanetTile)4500);
        Assert.IsFalse(report.Accepted, "StillValid must reject null caravan.");
    }

    [TestMethod]
    public void CaravanArrivalAction_ConstructOutpost_PropertiesAndLabels_SignalBeacon()
    {
        var woodDef = ThingDefOf.WoodLog ?? new ThingDef { defName = "WoodLog" };
        var action = new CaravanArrivalAction_ConstructOutpost((PlanetTile)8200, ListeningPostKind.SignalBeacon, woodDef);

        Assert.AreEqual((PlanetTile)8200, action.TargetTile);
        Assert.AreEqual(ListeningPostKind.SignalBeacon, action.Kind);
        Assert.AreEqual(woodDef, action.StuffDef);
        Assert.IsTrue(action.Label.Contains("Signal Beacon"), "Label must mention Signal Beacon.");
        Assert.IsTrue(action.ReportString.Contains("travelling to construct signal beacon"), "ReportString must describe active transit.");
    }

    [TestMethod]
    public void CaravanExpeditionUtility_DirectiveRegistrationAndConsumption_Lifecycle()
    {
        var tile = (PlanetTile)12345;
        var directive = new OutpostConstructionDirective
        {
            TargetTile = tile,
            Kind = ListeningPostKind.AutomatedRelay,
            StuffDef = null,
            OriginMapId = 1,
            CreatedTick = 1000
        };

        Assert.AreEqual(0, CaravanExpeditionUtility.PendingDirectivesCount);

        CaravanExpeditionUtility.RegisterPendingDirective(tile, directive);
        Assert.AreEqual(1, CaravanExpeditionUtility.PendingDirectivesCount);

        // First consume succeeds
        bool consumed = CaravanExpeditionUtility.TryConsumePendingDirective(tile, out var retrieved);
        Assert.IsTrue(consumed);
        Assert.IsNotNull(retrieved);
        Assert.AreEqual(tile, retrieved.TargetTile);
        Assert.AreEqual(ListeningPostKind.AutomatedRelay, retrieved.Kind);
        Assert.AreEqual(0, CaravanExpeditionUtility.PendingDirectivesCount);

        // Second consume returns false (already consumed)
        bool secondConsume = CaravanExpeditionUtility.TryConsumePendingDirective(tile, out var secondRetrieved);
        Assert.IsFalse(secondConsume);
        Assert.IsNull(secondRetrieved);
    }

    [TestMethod]
    public void CaravanExpeditionUtility_ClearPendingDirective_RemovesTargetOnly()
    {
        var tileA = (PlanetTile)100;
        var tileB = (PlanetTile)200;

        CaravanExpeditionUtility.RegisterPendingDirective(tileA, new OutpostConstructionDirective
        {
            TargetTile = tileA,
            Kind = ListeningPostKind.AutomatedRelay
        });

        CaravanExpeditionUtility.RegisterPendingDirective(tileB, new OutpostConstructionDirective
        {
            TargetTile = tileB,
            Kind = ListeningPostKind.SignalBeacon
        });

        Assert.AreEqual(2, CaravanExpeditionUtility.PendingDirectivesCount);

        CaravanExpeditionUtility.ClearPendingDirective(tileA);
        Assert.AreEqual(1, CaravanExpeditionUtility.PendingDirectivesCount);

        bool hasB = CaravanExpeditionUtility.TryConsumePendingDirective(tileB, out var dirB);
        Assert.IsTrue(hasB);
        Assert.AreEqual(tileB, dirB!.TargetTile);
    }

    [TestMethod]
    public void OutpostMaterialUtility_CostConstants_MatchSpecification()
    {
        int steel = OutpostMaterialUtility.AutomatedRelaySteelCost;
        int comp = OutpostMaterialUtility.AutomatedRelayComponentCost;
        int beaconMat = OutpostMaterialUtility.SignalBeaconWoodOrStoneCost;

        int steelSalvage = OutpostMaterialUtility.AutomatedRelaySteelSalvage;
        int compSalvage = OutpostMaterialUtility.AutomatedRelayComponentSalvage;
        int beaconSalvage = OutpostMaterialUtility.SignalBeaconWoodSalvage;

        Assert.IsTrue(steel >= 100, "Automated relay steel cost must be at least 100.");
        Assert.IsTrue(comp >= 4, "Automated relay component cost must be at least 4.");
        Assert.IsTrue(beaconMat >= 150, "Signal beacon material cost must be at least 150.");

        Assert.IsTrue(steelSalvage > 0 && steelSalvage < steel, "Steel salvage must be positive and less than initial build cost.");
        Assert.IsTrue(compSalvage > 0 && compSalvage < comp, "Component salvage must be positive and less than initial build cost.");
        Assert.IsTrue(beaconSalvage > 0 && beaconSalvage < beaconMat, "Beacon salvage must be positive and less than initial build cost.");
    }

    [TestMethod]
    public void OutpostMaterialUtility_IsWoodOrStone_IdentifiesMaterials()
    {
        var woodDef = ThingDefOf.WoodLog ?? new ThingDef { defName = "WoodLog" };
        var steelDef = ThingDefOf.Steel ?? new ThingDef { defName = "Steel" };
        Assert.IsTrue(OutpostMaterialUtility.IsWoodOrStone(woodDef), "WoodLog must be identified as valid beacon material.");
        Assert.IsFalse(OutpostMaterialUtility.IsWoodOrStone(steelDef), "Steel must not be identified as wood or stone.");
        Assert.IsFalse(OutpostMaterialUtility.IsWoodOrStone(null!), "Null def must safely return false.");
    }
}
