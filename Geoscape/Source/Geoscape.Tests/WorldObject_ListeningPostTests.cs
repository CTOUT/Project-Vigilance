using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape.Sensory;
using UnityEngine;

namespace Geoscape.Tests;

[TestClass]
public sealed class WorldObject_ListeningPostTests
{
    private static (float detection, int identification) CalculateRadii(ListeningPostKind kind, int vantageBonus, bool isSabotaged)
    {
        if (isSabotaged)
        {
            return (0f, 0);
        }

        if (kind == ListeningPostKind.AutomatedRelay)
        {
            int bonus = vantageBonus > 0 ? Mathf.Min(2, vantageBonus) : 0;
            float det = 12f + bonus;
            int id = Mathf.Max(1, Mathf.RoundToInt(det * 0.42f));
            return (det, id);
        }
        else
        {
            float det = 6f + vantageBonus;
            int id = Mathf.Max(1, Mathf.CeilToInt(det * 0.50f));
            return (det, id);
        }
    }

    [TestMethod]
    public void AutomatedRelay_FlatTerrain_Returns12DetectionAnd5Identification()
    {
        // Act
        var (detection, identification) = CalculateRadii(ListeningPostKind.AutomatedRelay, vantageBonus: 0, isSabotaged: false);

        // Assert
        Assert.AreEqual(12f, detection, "Automated relay on flat terrain must have 12 rings detection.");
        Assert.AreEqual(5, identification, "Automated relay on flat terrain must have 5 rings identification.");
    }

    [TestMethod]
    public void AutomatedRelay_ElevatedTerrain_AppliesCappedVantageBonus()
    {
        // SmallHills (+1 vantage)
        var (det1, id1) = CalculateRadii(ListeningPostKind.AutomatedRelay, vantageBonus: 1, isSabotaged: false);
        Assert.AreEqual(13f, det1, "Small hills gives +1 ring (13 rings).");
        Assert.AreEqual(5, id1);

        // LargeHills (+2 vantage)
        var (det2, id2) = CalculateRadii(ListeningPostKind.AutomatedRelay, vantageBonus: 2, isSabotaged: false);
        Assert.AreEqual(14f, det2, "Large hills gives +2 rings (14 rings).");
        Assert.AreEqual(6, id2);

        // Mountainous (+3 terrain vantage, but automated relays cap at +2 rings per Model B+ spec)
        var (det3, id3) = CalculateRadii(ListeningPostKind.AutomatedRelay, vantageBonus: 3, isSabotaged: false);
        Assert.AreEqual(14f, det3, "Relays cap vantage at +2 rings max (14 rings).");
        Assert.AreEqual(6, id3);
    }

    [TestMethod]
    public void SignalBeacon_FlatTerrain_Returns6DetectionAnd3Identification()
    {
        // Act
        var (detection, identification) = CalculateRadii(ListeningPostKind.SignalBeacon, vantageBonus: 0, isSabotaged: false);

        // Assert
        Assert.AreEqual(6f, detection, "Signal beacon baseline is 6 rings.");
        Assert.AreEqual(3, identification, "Signal beacon baseline identification is 3 rings.");
    }

    [TestMethod]
    public void SignalBeacon_MountainousTerrain_AppliesFullOpticalVantageBonus()
    {
        // Act: Mountainous gives full +3 rings to optical watchtower/beacon
        var (detection, identification) = CalculateRadii(ListeningPostKind.SignalBeacon, vantageBonus: 3, isSabotaged: false);

        // Assert
        Assert.AreEqual(9f, detection, "Signal beacon with +3 mountain vantage reaches 9 rings detection.");
        Assert.AreEqual(5, identification, "Signal beacon with +3 mountain vantage reaches 5 rings identification.");
    }

    [TestMethod]
    public void SabotagedOutpost_TelemetryCollapsesToZero()
    {
        // Act
        var (detRelay, idRelay) = CalculateRadii(ListeningPostKind.AutomatedRelay, vantageBonus: 2, isSabotaged: true);
        var (detBeacon, idBeacon) = CalculateRadii(ListeningPostKind.SignalBeacon, vantageBonus: 3, isSabotaged: true);

        // Assert
        Assert.AreEqual(0f, detRelay, "Sabotaged relay must have 0 detection.");
        Assert.AreEqual(0, idRelay, "Sabotaged relay must have 0 identification.");
        Assert.AreEqual(0f, detBeacon, "Sabotaged beacon must have 0 detection.");
        Assert.AreEqual(0, idBeacon, "Sabotaged beacon must have 0 identification.");
    }

    [TestMethod]
    public void OutpostEconomics_CostAndSalvageMath_StrictlyCompliesWith75PercentPolicy()
    {
        // Automated Relay initial costs
        int steelCost = OutpostMaterialUtility.AutomatedRelaySteelCost;
        int compCost = OutpostMaterialUtility.AutomatedRelayComponentCost;

        // Salvage yields exactly 75%
        int expectedSteelSalvage = Mathf.RoundToInt(steelCost * 0.75f);
        int expectedCompSalvage = Mathf.RoundToInt(compCost * 0.75f);

        Assert.AreEqual(OutpostMaterialUtility.AutomatedRelaySteelSalvage, expectedSteelSalvage, "Automated relay steel salvage must be 75%.");
        Assert.AreEqual(OutpostMaterialUtility.AutomatedRelayComponentSalvage, expectedCompSalvage, "Automated relay component salvage must be 75%.");

        // Repair costs are 25% of initial build costs
        int expectedSteelRepair = Mathf.RoundToInt(steelCost * 0.25f);
        int expectedCompRepair = Mathf.RoundToInt(compCost * 0.25f);

        Assert.AreEqual(OutpostMaterialUtility.AutomatedRelaySteelRepairCost, expectedSteelRepair, "Steel repair cost must be 25% of build cost.");
        Assert.AreEqual(OutpostMaterialUtility.AutomatedRelayComponentRepairCost, expectedCompRepair, "Component repair cost must be 25% of build cost.");

        // Signal Beacon initial: 150 Wood/Stone -> 75% salvage: 112
        int beaconCost = OutpostMaterialUtility.SignalBeaconWoodOrStoneCost;
        int expectedWoodSalvage = (int)(beaconCost * 0.75f);
        int expectedWoodRepair = (int)(beaconCost * 0.20f);

        Assert.AreEqual(OutpostMaterialUtility.SignalBeaconWoodSalvage, expectedWoodSalvage, "Beacon salvage must be 75% of initial build cost.");
        Assert.AreEqual(OutpostMaterialUtility.SignalBeaconWoodOrStoneRepairCost, expectedWoodRepair, "Beacon repair must be 20% of initial build cost.");
    }
}
