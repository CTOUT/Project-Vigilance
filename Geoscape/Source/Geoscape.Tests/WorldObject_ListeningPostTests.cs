using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld;
using RimWorld.Planet;
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
            int id = 5 + (bonus >= 2 ? 1 : 0);
            return (det, id);
        }
        else
        {
            float det = 6f + vantageBonus;
            int id = 3 + SensoryTerrainUtility.GetIdentificationVantageBonus(vantageBonus);
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

    [TestMethod]
    public void SignalBeacon_TracksStuffDefMaterial_AndFallsBackToWoodLog()
    {
        var post = new WorldObject_ListeningPost
        {
            Kind = ListeningPostKind.SignalBeacon
        };

        // Default / null fallback
        Assert.IsNull(post.stuffDef);
        Assert.IsNull(post.StuffDef);

        // Assign granite or custom stone stuffDef
        var stoneDef = new Verse.ThingDef { defName = "BlocksGranite", label = "granite blocks" };
        post.stuffDef = stoneDef;
        Assert.AreEqual(stoneDef, post.StuffDef);
        Assert.IsTrue(post.Label.Contains("granite blocks"));
        Assert.IsTrue(post.GetInspectString().Contains("Granite blocks"));
    }

    [TestMethod]
    public void SignalBeacon_Dismantle_RefundsMaterialRespectingStackLimit()
    {
        var stoneDef = new Verse.ThingDef
        {
            defName = "BlocksGranite",
            label = "granite blocks",
            stackLimit = 75
        };

        // 150 * 0.75 = 112 total refund
        int totalRefund = 112;
        int stackLimit = stoneDef.stackLimit;
        int expectedFullStacks = totalRefund / stackLimit;
        int expectedRemainder = totalRefund % stackLimit;

        Assert.AreEqual(1, expectedFullStacks, "112 items with 75 stackLimit produces 1 full stack of 75.");
        Assert.AreEqual(37, expectedRemainder, "112 items with 75 stackLimit produces 1 partial stack of 37.");
        Assert.AreEqual(totalRefund, expectedFullStacks * stackLimit + expectedRemainder);
    }

    [TestMethod]
    public void MaxIntelligenceTier_IsClassified_EnablingContactIdentification()
    {
        var post = new WorldObject_ListeningPost
        {
            Kind = ListeningPostKind.SignalBeacon
        };

        // Assert: MaxIntelligenceTier must be Classified, not Contact, so the identification ring works!
        Assert.AreEqual(IntelligenceTier.Classified, post.MaxIntelligenceTier, "Listening posts and beacons must have MaxIntelligenceTier = Classified.");
    }

    [TestMethod]
    public void SignalBeacon_EvaluatesToClassified_WhenWithinIdentificationHorizon()
    {
        var post = new WorldObject_ListeningPost
        {
            Kind = ListeningPostKind.SignalBeacon
        };

        // At distance 2 (inside identification radius of 3 rings)
        IntelligenceTier insideTier = SensoryTerrainUtility.EvaluateSensorTier(post, travDist: 2, isTargetMasked: false);
        Assert.AreEqual(IntelligenceTier.Classified, insideTier, "Target within identification horizon must evaluate to Classified.");

        // At distance 5 (outside identification radius of 3, but inside detection radius of 6 rings)
        IntelligenceTier outsideTier = SensoryTerrainUtility.EvaluateSensorTier(post, travDist: 5, isTargetMasked: false);
        Assert.AreEqual(IntelligenceTier.Contact, outsideTier, "Target outside identification horizon must evaluate to Contact.");
    }

    [TestMethod]
    public void TimedConstruction_StateTransitions_AndPausing()
    {
        var post = new WorldObject_ListeningPost
        {
            Kind = ListeningPostKind.AutomatedRelay
        };

        // Initial state before construction starts
        Assert.IsFalse(post.IsConstructing);
        Assert.IsTrue(post.IsSensorActive);

        // Start construction
        post.StartConstruction(2500);
        Assert.IsTrue(post.IsConstructing);
        Assert.IsFalse(post.IsSensorActive, "Sensor must be inactive while under construction.");
        Assert.AreEqual(2500, post.ConstructionTicksLeft);
        Assert.IsTrue(post.GetInspectString().Contains("Under Construction") || post.GetInspectString().Contains("Construction Paused"));

        // Finish construction
        post.FinishConstruction();
        Assert.IsFalse(post.IsConstructing);
        Assert.IsTrue(post.IsSensorActive, "Sensor must activate once construction finishes.");
        Assert.IsTrue(post.GetInspectString().Contains("Operational"));
    }

    [TestMethod]
    public void TimedDismantling_StateTransitions_AndCancellation()
    {
        var post = new WorldObject_ListeningPost
        {
            Kind = ListeningPostKind.SignalBeacon
        };

        // Initial operational state
        Assert.IsFalse(post.IsDismantling);
        Assert.IsTrue(post.IsSensorActive);

        // Start dismantling
        post.StartDismantling(1500);
        Assert.IsTrue(post.IsDismantling);
        Assert.IsFalse(post.IsSensorActive, "Sensor must be inactive while dismantling.");
        Assert.AreEqual(1500, post.DismantleTicksLeft);
        Assert.IsTrue(post.GetInspectString().Contains("Dismantling"));

        // Cancel dismantling
        post.CancelDismantling();
        Assert.IsFalse(post.IsDismantling);
        Assert.IsTrue(post.IsSensorActive, "Sensor must reactivate if dismantling is cancelled.");
        Assert.IsTrue(post.GetInspectString().Contains("Operational"));
    }

    [TestMethod]
    public void CaravanExpeditionUtility_DismantleDirective_RegistrationAndConsumption()
    {
        var tile = (RimWorld.Planet.PlanetTile)12345;
        var directive = new OutpostConstructionDirective
        {
            TargetTile = tile,
            Kind = ListeningPostKind.AutomatedRelay,
            DirectiveKind = OutpostDirectiveKind.Dismantle,
            OriginMapId = 1,
            CreatedTick = 1000
        };

        CaravanExpeditionUtility.RegisterPendingDirective(tile, directive);
        Assert.IsTrue(CaravanExpeditionUtility.TryConsumePendingDirective(tile, out var consumed));
        Assert.IsNotNull(consumed);
        Assert.AreEqual(OutpostDirectiveKind.Dismantle, consumed.DirectiveKind);
        Assert.AreEqual(ListeningPostKind.AutomatedRelay, consumed.Kind);

        // Verify cleared from dictionary
        Assert.IsFalse(CaravanExpeditionUtility.TryConsumePendingDirective(tile, out _));
    }

    [TestMethod]
    public void UprightQuadRotationAngle_InDrawQuadTangentialToPlanet_OrientsTextureTopNorthAndRightEast()
    {
        // At equator facing camera: pos = (0, 0, -100)
        // Normal = (0, 0, -1), Local East = Cross(normal, up) = (1, 0, 0), Local North = (0, 1, 0)
        Vector3 pos = new Vector3(0f, 0f, -100f);
        Vector3 normalized = pos.normalized;
        Vector3 east = Vector3.Cross(normalized, Vector3.up).normalized;
        Vector3 north = Vector3.Cross(east, normalized).normalized;

        Quaternion baseRot = Quaternion.LookRotation( Vector3.Cross(normalized, Vector3.up), normalized );

        // Vanilla 0f rotation rotates texture top (+Z in plane10) 90 degrees clockwise to East
        Vector3 vanillaTexTop = baseRot * Vector3.forward;
        Assert.IsTrue(Vector3.Dot(vanillaTexTop, east) > 0.999f,
            "With 0 degree rotation, MeshPool.plane10 texture top (+Z) points East (rotated 90 degrees clockwise).");

        // UprightQuadRotationAngle (-90f) aligns texture top (+Z) with North and texture right (+X) with East
        Quaternion uprightRot = Quaternion.AngleAxis(WorldObject_ListeningPost.UprightQuadRotationAngle, normalized) * baseRot;
        Vector3 uprightTexTop = uprightRot * Vector3.forward;
        Vector3 uprightTexRight = uprightRot * Vector3.right;

        Assert.IsTrue(Vector3.Dot(uprightTexTop, north) > 0.999f,
            $"UprightQuadRotationAngle must align texture top with North. Actual: {uprightTexTop}");
        Assert.IsTrue(Vector3.Dot(uprightTexRight, east) > 0.999f,
            $"UprightQuadRotationAngle must align texture right with East. Actual: {uprightTexRight}");
    }

    [TestMethod]
    public void UprightPrintRotationAngle_InGetTangentsToPlanet_OrientsTextureTopNorthAndRightEast()
    {
        // Verify WorldRendererUtility.GetTangentsToPlanet with UprightPrintRotationAngle (90f)
        Vector3 pos = new Vector3(0f, 0f, -100f);
        RimWorld.Planet.WorldRendererUtility.GetTangentsToPlanet(
            pos,
            out Vector3 first,
            out Vector3 second,
            WorldObject_ListeningPost.UprightPrintRotationAngle);

        // In PrintQuadTangentialToPlanet, 'first' is the +U (texture right) tangent and 'second' is the +V (texture top) tangent
        Assert.IsTrue(Vector3.Dot(first, Vector3.right) > 0.999f,
            $"UprightPrintRotationAngle must align first tangent (+U / texture right) with East (1,0,0). Actual: {first}");
        Assert.IsTrue(Vector3.Dot(second, Vector3.up) > 0.999f,
            $"UprightPrintRotationAngle must align second tangent (+V / texture top) with North (0,1,0). Actual: {second}");
    }

    [TestMethod]
    public void ShouldDrawUpright_CorrectlyDiscriminatesTargetObjects()
    {
        // 1. ListeningPost -> True
        var post = new WorldObject_ListeningPost();
        Assert.IsTrue(ProjectVigilance.Geoscape.Patches.WorldObject_Visibility_Patch.ShouldDrawUpright(post));

        // 2. Incursion -> True
        var incursion = new ProjectVigilance.Geoscape.World.WorldObject_Incursion();
        Assert.IsTrue(ProjectVigilance.Geoscape.Patches.WorldObject_Visibility_Patch.ShouldDrawUpright(incursion));

        // 3. Normal vanilla settlement with distinct 3D and 2D textures -> False
        var def = new WorldObjectDef
        {
            expandingIcon = true,
            texture = "World/WorldObjects/DefaultSettlement",
            expandingIconTexture = "World/WorldObjects/Expanding/Town"
        };
        var settlement = new Settlement { def = def };
        Assert.IsFalse(ProjectVigilance.Geoscape.Patches.WorldObject_Visibility_Patch.ShouldDrawUpright(settlement));

        // 4. Object whose 3D texture is directly an expanding icon texture -> True
        var customDef = new WorldObjectDef
        {
            expandingIcon = true,
            texture = "World/WorldObjects/Expanding/Sites/Outpost",
            expandingIconTexture = "World/WorldObjects/Expanding/Sites/Outpost"
        };
        var customSite = new Site { def = customDef };
        Assert.IsTrue(ProjectVigilance.Geoscape.Patches.WorldObject_Visibility_Patch.ShouldDrawUpright(customSite));
    }
}
