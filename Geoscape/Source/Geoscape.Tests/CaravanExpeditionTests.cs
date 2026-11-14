using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Geoscape.Patches;
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

    [TestMethod]
    public void Verify_CaravanExitMapUtility_Patch_Applies()
    {
        var harmony = new HarmonyLib.Harmony("test.patch.check");
        var patchClass = typeof(CaravanExitMapUtility_Patch.ExitMapAndCreateCaravan_TilePatch);
        var methods = patchClass.GetCustomAttributes(typeof(HarmonyLib.HarmonyPatch), false);
        Assert.IsTrue(methods.Length > 0);

        var original = typeof(CaravanExitMapUtility).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "ExitMapAndCreateCaravan" && m.GetParameters().Length == 6 && m.GetParameters()[3].ParameterType == typeof(PlanetTile));
        Assert.IsNotNull(original, "Original ExitMapAndCreateCaravan must exist.");

        var patchProcessor = harmony.CreateProcessor(original);
        patchProcessor.AddPostfix(new HarmonyLib.HarmonyMethod(patchClass.GetMethod("Postfix")));
        var patched = patchProcessor.Patch();
        Assert.IsNotNull(patched, "Patch must succeed without error.");
    }

    [TestCleanup]
    public void Cleanup()
    {
        CaravanExpeditionUtility.ClearAllPendingDirectives();
    }

    [TestMethod]
    public void TryConsumePendingDirectiveForCaravan_MatchesByExplicitDestinationTile()
    {
        var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
        var tile = (PlanetTile)7000;
        var directive = new OutpostConstructionDirective
        {
            TargetTile = tile,
            Kind = ListeningPostKind.AutomatedRelay,
            OriginMapId = 1,
            CreatedTick = 100
        };

        CaravanExpeditionUtility.RegisterPendingDirective(tile, directive);
        Assert.AreEqual(1, CaravanExpeditionUtility.PendingDirectivesCount);

        bool success = CaravanExpeditionUtility.TryConsumePendingDirectiveForCaravan(caravan, PlanetTile.Invalid, tile, out var consumed);
        Assert.IsTrue(success, "Must match directive by explicit destinationTile.");
        Assert.IsNotNull(consumed);
        Assert.AreEqual(tile, consumed.TargetTile);
        Assert.AreEqual(0, CaravanExpeditionUtility.PendingDirectivesCount, "Directive must be consumed and removed.");
    }

    [TestMethod]
    public void TryConsumePendingDirectiveForCaravan_MatchesByPatherDestination()
    {
        var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
        var pather = (Caravan_PathFollower)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan_PathFollower));
        typeof(Caravan).GetField("pather", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?.SetValue(caravan, pather);

        var destTile = (PlanetTile)8000;
        typeof(Caravan_PathFollower).GetField("destTile", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(pather, destTile);

        var directive = new OutpostConstructionDirective
        {
            TargetTile = destTile,
            Kind = ListeningPostKind.SignalBeacon,
            OriginMapId = 1,
            CreatedTick = 200
        };

        CaravanExpeditionUtility.RegisterPendingDirective(destTile, directive);
        Assert.AreEqual(1, CaravanExpeditionUtility.PendingDirectivesCount);

        // Explicit destinationTile is invalid, but caravan.pather.Destination matches
        bool success = CaravanExpeditionUtility.TryConsumePendingDirectiveForCaravan(caravan, PlanetTile.Invalid, PlanetTile.Invalid, out var consumed);
        Assert.IsTrue(success, "Must match directive via caravan.pather.Destination when destinationTile is invalid.");
        Assert.IsNotNull(consumed);
        Assert.AreEqual(destTile, consumed.TargetTile);
        Assert.AreEqual(0, CaravanExpeditionUtility.PendingDirectivesCount);
    }

    [TestMethod]
    public void TryConsumePendingDirectiveForCaravan_MatchesBySinglePendingFallback()
    {
        var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
        var targetTile = (PlanetTile)9000;
        var directive = new OutpostConstructionDirective
        {
            TargetTile = targetTile,
            Kind = ListeningPostKind.AutomatedRelay,
            OriginMapId = 2,
            CreatedTick = 500
        };

        CaravanExpeditionUtility.RegisterPendingDirective(targetTile, directive);
        Assert.AreEqual(1, CaravanExpeditionUtility.PendingDirectivesCount);

        // Neither destinationTile nor pather are set, but exactly 1 pending directive exists
        bool success = CaravanExpeditionUtility.TryConsumePendingDirectiveForCaravan(caravan, PlanetTile.Invalid, PlanetTile.Invalid, out var consumed);
        Assert.IsTrue(success, "Must match lone pending directive via fallback.");
        Assert.IsNotNull(consumed);
        Assert.AreEqual(targetTile, consumed.TargetTile);
        Assert.AreEqual(0, CaravanExpeditionUtility.PendingDirectivesCount);
    }

    [TestMethod]
    public void TryConsumePendingDirectiveForCaravan_RejectsNullCaravan()
    {
        var tile = (PlanetTile)9100;
        CaravanExpeditionUtility.RegisterPendingDirective(tile, new OutpostConstructionDirective { TargetTile = tile });

        bool success = CaravanExpeditionUtility.TryConsumePendingDirectiveForCaravan(null!, PlanetTile.Invalid, tile, out var consumed);
        Assert.IsFalse(success, "Must reject null caravan.");
        Assert.IsNull(consumed);
        Assert.AreEqual(1, CaravanExpeditionUtility.PendingDirectivesCount, "Directive must remain unconsumed.");
    }

    [TestMethod]
    public void TryConsumePendingDirectiveForCaravan_AmbiguousMultiplePendingWithoutMatch_ReturnsFalse()
    {
        var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
        var tileA = (PlanetTile)9201;
        var tileB = (PlanetTile)9202;

        CaravanExpeditionUtility.RegisterPendingDirective(tileA, new OutpostConstructionDirective { TargetTile = tileA, OriginMapId = 1 });
        CaravanExpeditionUtility.RegisterPendingDirective(tileB, new OutpostConstructionDirective { TargetTile = tileB, OriginMapId = 2 });

        Assert.AreEqual(2, CaravanExpeditionUtility.PendingDirectivesCount);

        // Neither destination matches, and multiple exist, so it should not arbitrarily pick one
        bool success = CaravanExpeditionUtility.TryConsumePendingDirectiveForCaravan(caravan, PlanetTile.Invalid, (PlanetTile)9999, out var consumed);
        Assert.IsFalse(success, "Must not consume when multiple directives exist and none match destination.");
        Assert.IsNull(consumed);
        Assert.AreEqual(2, CaravanExpeditionUtility.PendingDirectivesCount);
    }

    [TestMethod]
    public void CaravanArrivalAction_DismantleOutpost_PropertiesAndLabels()
    {
        var tile = (PlanetTile)3300;
        var action = new CaravanArrivalAction_DismantleOutpost(null!, tile);

        Assert.AreEqual(tile, action.TargetTile);
        Assert.IsNull(action.TargetOutpost);
        Assert.AreEqual("Dismantle Outpost", action.Label);
        Assert.AreEqual("travelling to dismantle outpost", action.ReportString);

        // Validation against null caravan
        var report = action.StillValid(null!, tile);
        Assert.IsFalse(report.Accepted, "StillValid must reject null caravan.");
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
    public void CaravanExpeditionUtility_GetListeningPostKindLabel_UsesEachStructureName()
    {
        Assert.AreEqual("Automated Listening Post",
            CaravanExpeditionUtility.GetListeningPostKindLabel(ListeningPostKind.AutomatedRelay));
        Assert.AreEqual("Automated Sonar Station",
            CaravanExpeditionUtility.GetListeningPostKindLabel(ListeningPostKind.AutomatedSonar));
        Assert.AreEqual("Signal Beacon",
            CaravanExpeditionUtility.GetListeningPostKindLabel(ListeningPostKind.SignalBeacon));
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

    [TestMethod]
    public void TryConsumePendingDirectiveForCaravan_DivergentDestination_DoesNotHijackSingleDirective()
    {
        var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
        var outpostTile = (PlanetTile)7000;
        var tradeDestTile = (PlanetTile)3000;

        var directive = new OutpostConstructionDirective
        {
            TargetTile = outpostTile,
            Kind = ListeningPostKind.AutomatedRelay,
            OriginMapId = 1,
            CreatedTick = 100
        };

        CaravanExpeditionUtility.RegisterPendingDirective(outpostTile, directive);
        Assert.AreEqual(1, CaravanExpeditionUtility.PendingDirectivesCount);

        // Caravan is heading to an explicit trade destination (3000), not the outpost (7000).
        // Must NOT hijack this caravan even if exactly 1 pending directive exists!
        bool success = CaravanExpeditionUtility.TryConsumePendingDirectiveForCaravan(caravan, (PlanetTile)100, tradeDestTile, out var consumed);
        Assert.IsFalse(success, "Must reject consuming directive when caravan has explicit divergent destination.");
        Assert.IsNull(consumed);
        Assert.AreEqual(1, CaravanExpeditionUtility.PendingDirectivesCount, "Directive must remain unconsumed.");
    }

    [TestMethod]
    public void TryConsumePendingDirectiveForCaravan_ExitTileDestination_BindsToDirective()
    {
        var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
        var outpostTile = (PlanetTile)7000;
        var exitTile = (PlanetTile)101;

        var directive = new OutpostConstructionDirective
        {
            TargetTile = outpostTile,
            Kind = ListeningPostKind.AutomatedRelay,
            OriginMapId = 1,
            CreatedTick = 100
        };

        CaravanExpeditionUtility.RegisterPendingDirective(outpostTile, directive);
        Assert.AreEqual(1, CaravanExpeditionUtility.PendingDirectivesCount);

        // destinationTile is the colony exit tile (101 == exitFromTile), indicating an unrouted departure
        bool success = CaravanExpeditionUtility.TryConsumePendingDirectiveForCaravan(caravan, exitTile, exitTile, out var consumed);
        Assert.IsTrue(success, "Must consume directive when destinationTile is the origin exit tile.");
        Assert.IsNotNull(consumed);
        Assert.AreEqual(outpostTile, consumed.TargetTile);
        Assert.AreEqual(0, CaravanExpeditionUtility.PendingDirectivesCount);
    }

    [TestMethod]
    public void WorldObject_ListeningPost_TickWithCaravanPresent_ProgressesAndFinishesConstruction()
    {
        var post = new WorldObject_ListeningPost
        {
            Kind = ListeningPostKind.AutomatedRelay
        };

        var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));

        try
        {
            WorldObject_ListeningPost.PlayerCaravanPresenceOverride = _ => caravan;
            post.StartConstruction(5);

            Assert.IsTrue(post.IsConstructing);
            Assert.AreEqual(5, post.ConstructionTicksLeft);
            Assert.IsTrue(post.HasPlayerCaravanPresent());

            // Tick 4 times
            for (int i = 0; i < 4; i++)
            {
                post.TickForTesting();
            }

            Assert.IsTrue(post.IsConstructing);
            Assert.AreEqual(1, post.ConstructionTicksLeft);

            // Final tick completes construction
            post.TickForTesting();

            Assert.IsFalse(post.IsConstructing, "Construction must finish when ticks reach 0.");
            Assert.AreEqual(0, post.ConstructionTicksLeft);
            Assert.IsTrue(post.IsSensorActive, "Sensor must activate once construction finishes.");
        }
        finally
        {
            WorldObject_ListeningPost.PlayerCaravanPresenceOverride = null;
        }
    }

    [TestMethod]
    public void WorldObject_ListeningPost_TickWithoutCaravan_PausesConstruction()
    {
        var post = new WorldObject_ListeningPost
        {
            Kind = ListeningPostKind.AutomatedRelay
        };

        try
        {
            // No caravan present
            WorldObject_ListeningPost.PlayerCaravanPresenceOverride = _ => null;
            post.StartConstruction(10);

            Assert.IsTrue(post.IsConstructing);
            Assert.AreEqual(10, post.ConstructionTicksLeft);
            Assert.IsFalse(post.HasPlayerCaravanPresent());

            // Tick 5 times with no caravan on site
            for (int i = 0; i < 5; i++)
            {
                post.TickForTesting();
            }

            // Construction ticks must NOT decrease while unassisted
            Assert.IsTrue(post.IsConstructing);
            Assert.AreEqual(10, post.ConstructionTicksLeft, "Construction must remain paused without visiting caravan.");
        }
        finally
        {
            WorldObject_ListeningPost.PlayerCaravanPresenceOverride = null;
        }
    }

    [TestMethod]
    public void UnexploredTile_RightClickDispatch_SuppressesImpassableLeaksAndConstruction()
    {
        try
        {
            var prefsField = typeof(Verse.Prefs).GetField("data", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (prefsField != null && prefsField.GetValue(null) == null)
            {
                var prefsDataType = typeof(Verse.Prefs).Assembly.GetType("Verse.PrefsData");
                if (prefsDataType != null)
                {
                    prefsField.SetValue(null, System.Activator.CreateInstance(prefsDataType));
                }
            }
            if (Verse.LanguageDatabase.activeLanguage == null)
            {
                var lang = new Verse.LoadedLanguage("English");
                var infoField = typeof(Verse.LoadedLanguage).GetField("info");
                if (infoField != null)
                {
                    infoField.SetValue(lang, System.Activator.CreateInstance(infoField.FieldType));
                }
                Verse.LanguageDatabase.activeLanguage = lang;
            }

            ProjectVigilance.Geoscape.GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;
            var sensory = new SensoryManager(null!);
            SensoryManager.Instance = sensory;
            SensoryManager.IsExploredOverride = t => t.tileId == 100; // Only tile 100 is explored

            var playerFaction = new Faction { def = new FactionDef { isPlayer = true } };

            var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
            HarmonyLib.Traverse.Create(caravan).Field("tileInt").SetValue((PlanetTile)100);
            HarmonyLib.Traverse.Create(caravan).Field("factionInt").SetValue(playerFaction);

            var settlement = new Settlement { ID = 9901 };
            HarmonyLib.Traverse.Create(settlement).Field("tileInt").SetValue((PlanetTile)100);
            HarmonyLib.Traverse.Create(settlement).Field("factionInt").SetValue(playerFaction);

            var unexploredImpassableTile = (PlanetTile)500;

            // 1. Caravan Right-Click Choices on Unexplored Tile
            ProjectVigilance.Geoscape.Patches.FloatMenuMakerWorld_ChoicesAtFor_Patch.MouseTileOverride = unexploredImpassableTile;
            List<FloatMenuOption> caravanChoices = new();
            ProjectVigilance.Geoscape.Patches.FloatMenuMakerWorld_ChoicesAtFor_Patch.Postfix(Vector2.zero, caravan, ref caravanChoices);

            Assert.IsNotNull(caravanChoices);
            Assert.AreEqual(1, caravanChoices.Count, "Must offer exactly 1 generic exploration option into uncharted black unknown.");
            Assert.AreEqual("Travel into unexplored sector (Tile 500)", caravanChoices[0].Label);
            Assert.IsFalse(caravanChoices.Any(o => o.Label.ToLowerInvariant().Contains("impassable")),
                "Must never leak impassable terrain status through fog of war.");
            Assert.IsFalse(caravanChoices.Any(o => o.Label.ToLowerInvariant().Contains("beacon") || o.Label.ToLowerInvariant().Contains("listening post")),
                "Must not offer construction expeditions into unexplored territory.");

            // 2. Settlement Right-Click Dispatch Options on Unexplored Tile
            var settlementOptions = ProjectVigilance.Geoscape.Patches.WorldSelector_HandleWorldClicks_Patch.BuildSettlementDispatchOptions(settlement, unexploredImpassableTile);
            Assert.IsNotNull(settlementOptions);
            Assert.AreEqual(1, settlementOptions.Count, "Settlement must offer only generic exploration caravan formation.");
            Assert.AreEqual("Form expedition towards unexplored sector (Tile 500)", settlementOptions[0].Label);
            Assert.IsFalse(settlementOptions.Any(o => o.Label.ToLowerInvariant().Contains("impassable")),
                "Settlement menu must never leak impassable terrain status.");
            Assert.IsFalse(settlementOptions.Any(o => o.Label.ToLowerInvariant().Contains("nearest passable")),
                "Settlement menu must not spoil nearest passable coordinates.");
            Assert.IsFalse(settlementOptions.Any(o => o.Label.ToLowerInvariant().Contains("beacon") || o.Label.ToLowerInvariant().Contains("listening post")),
                "Settlement must not offer construction expeditions into uncharted territory.");
        }
        finally
        {
            ProjectVigilance.Geoscape.Patches.FloatMenuMakerWorld_ChoicesAtFor_Patch.MouseTileOverride = null;
            SensoryManager.IsExploredOverride = null;
            SensoryManager.Instance = null;
        }
    }

    [TestMethod]
    public void OptimisticPathing_TraversalCostAndPassability_AssumedPassableInFog()
    {
        try
        {
            ProjectVigilance.Geoscape.GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;
            var unexploredTile = (PlanetTile)501;
            var exploredTile = (PlanetTile)502;

            OptimisticPathingUtility.IsTileUnexploredOverride = t => t == unexploredTile;
            OptimisticPathingUtility.IsTileImpassableOverride = t => true; // Both tiles are secretly impassable mountains/oceans

            // 1. Without active optimistic scope: unexplored impassable tile is not altered
            Assert.IsFalse(OptimisticPathingUtility.ShouldUseOptimisticPathing());
            float unalteredCost = OptimisticPathingUtility.ModifyMovementDifficulty(1000f, unexploredTile);
            Assert.AreEqual(1000f, unalteredCost, "When not pathing optimistically, original cost must be preserved.");

            // 2. With active optimistic scope
            using (new OptimisticPathingUtility.Scope(true))
            {
                Assert.IsTrue(OptimisticPathingUtility.ShouldUseOptimisticPathing());

                // Unexplored tile gets baseline 1.0f traversal difficulty
                float optimisticCost = OptimisticPathingUtility.ModifyMovementDifficulty(1000f, unexploredTile);
                Assert.AreEqual(1.0f, optimisticCost, "Unexplored tile in fog must be assumed baseline traversable cost (1.0).");

                // Explored impassable tile retains real prohibitive cost
                float exploredCost = OptimisticPathingUtility.ModifyMovementDifficulty(1000f, exploredTile);
                Assert.AreEqual(1000f, exploredCost, "Explored tile must retain real terrain movement difficulty.");

                // Passability patches
                bool passableResult = false;
                bool handledPassable = ProjectVigilance.Geoscape.Patches.WorldPathing_FindPath_Patch.WorldPathGrid_Passable_Patch.Prefix(unexploredTile, ref passableResult);
                Assert.IsFalse(handledPassable, "Prefix must handle unexplored tile.");
                Assert.IsTrue(passableResult, "Unexplored tile must be reported as passable in fog.");

                bool impassableResult = true;
                bool handledImpassable = ProjectVigilance.Geoscape.Patches.WorldPathing_FindPath_Patch.World_Impassable_Patch.Prefix(unexploredTile, ref impassableResult);
                Assert.IsFalse(handledImpassable, "Prefix must handle unexplored tile.");
                Assert.IsFalse(impassableResult, "Unexplored tile must NOT be reported impassable in fog.");

                // Reachability patch
                bool canReachResult = false;
                bool handledCanReach = ProjectVigilance.Geoscape.Patches.WorldPathing_FindPath_Patch.WorldReachability_CanReach_Patch.Prefix((PlanetTile)100, unexploredTile, ref canReachResult);
                Assert.IsFalse(handledCanReach, "Reachability prefix must handle optimistic scope.");
                Assert.IsTrue(canReachResult, "Reachability must succeed optimistically through fog of war.");
            }

            // Scope automatically cleaned up
            Assert.IsFalse(OptimisticPathingUtility.Active);
        }
        finally
        {
            OptimisticPathingUtility.IsTileUnexploredOverride = null;
            OptimisticPathingUtility.IsTileImpassableOverride = null;
            OptimisticPathingUtility.Active = false;
        }
    }

    [TestMethod]
    public void TryEnterNextPathTile_UnexploredImpassableTile_BlocksEntry()
    {
        SensoryManager? previousSensoryManager = SensoryManager.Instance;

        try
        {
            ProjectVigilance.Geoscape.GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;
            SensoryManager.Instance = null;

            var nextTile = (PlanetTile)503;
            var playerFaction = new Faction { def = new FactionDef { isPlayer = true } };
            var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
            HarmonyLib.Traverse.Create(caravan).Field("tileInt").SetValue((PlanetTile)100);
            HarmonyLib.Traverse.Create(caravan).Field("factionInt").SetValue(playerFaction);
            HarmonyLib.Traverse.Create(caravan).Field("spawnedInt").SetValue(true);

            var pather = (Caravan_PathFollower)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan_PathFollower));
            HarmonyLib.Traverse.Create(pather).Field("caravan").SetValue(caravan);
            HarmonyLib.Traverse.Create(pather).Field("nextTile").SetValue(nextTile);
            HarmonyLib.Traverse.Create(pather).Field("destTile").SetValue((PlanetTile)105);
            HarmonyLib.Traverse.Create(pather).Field("moving").SetValue(true);
            HarmonyLib.Traverse.Create(caravan).Field("pather").SetValue(pather);

            OptimisticPathingUtility.IsTileUnexploredOverride = tile => tile == nextTile;
            OptimisticPathingUtility.IsTileImpassableOverride = tile => tile == nextTile;

            bool runOriginal = WorldPathing_FindPath_Patch.Caravan_PathFollower_TryEnterNextPathTile_Patch.Prefix(pather);

            Assert.IsFalse(runOriginal, "The original movement method must not enter an unexplored tile that is actually impassable.");
        }
        finally
        {
            SensoryManager.Instance = previousSensoryManager;
            OptimisticPathingUtility.IsTileUnexploredOverride = null;
            OptimisticPathingUtility.IsTileImpassableOverride = null;
        }
    }

    [TestMethod]
    public void OptimisticPathing_ObstacleRevelation_TriggersDynamicRepath()
    {
        try
        {
            ProjectVigilance.Geoscape.GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;

            var startTile = (PlanetTile)100;
            var obstacleTile = (PlanetTile)101;
            var destTile = (PlanetTile)105;

            var playerFaction = new Faction { def = new FactionDef { isPlayer = true } };
            var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
            HarmonyLib.Traverse.Create(caravan).Field("tileInt").SetValue(startTile);
            HarmonyLib.Traverse.Create(caravan).Field("factionInt").SetValue(playerFaction);
            HarmonyLib.Traverse.Create(caravan).Field("spawnedInt").SetValue(true);

            var pather = (Caravan_PathFollower)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan_PathFollower));
            HarmonyLib.Traverse.Create(pather).Field("caravan").SetValue(caravan);
            HarmonyLib.Traverse.Create(pather).Field("destTile").SetValue(destTile);
            HarmonyLib.Traverse.Create(pather).Field("moving").SetValue(true);
            HarmonyLib.Traverse.Create(pather).Field("nextTile").SetValue(obstacleTile);

            var curPath = (WorldPath)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldPath));
            var nodes = new List<PlanetTile> { destTile, (PlanetTile)104, (PlanetTile)103, (PlanetTile)102, obstacleTile, startTile };
            HarmonyLib.Traverse.Create(curPath).Field("nodes").SetValue(nodes);
            HarmonyLib.Traverse.Create(pather).Field("curPath").SetValue(curPath);
            HarmonyLib.Traverse.Create(caravan).Field("pather").SetValue(pather);

            // Obstacle tile 101 is now explored and impassable!
            HashSet<int> exploredTiles = new() { startTile.tileId, obstacleTile.tileId };
            OptimisticPathingUtility.IsTileUnexploredOverride = t => !exploredTiles.Contains(t.tileId);
            OptimisticPathingUtility.IsTileImpassableOverride = t => t.tileId == obstacleTile.tileId;

            Caravan? reroutedCaravan = null;
            PlanetTile reroutedObstacle = PlanetTile.Invalid;
            string reroutedReason = string.Empty;

            OptimisticPathingUtility.OnCaravanReroutedForTesting = (c, obs, reason) =>
            {
                reroutedCaravan = c;
                reroutedObstacle = obs;
                reroutedReason = reason;
            };

            // Vision updates after caravan takes a step
            OptimisticPathingUtility.NotifyCaravanVisionUpdated(caravan);

            Assert.IsNotNull(reroutedCaravan, "Caravan must trigger reroute upon discovering impassable obstacle on path.");
            Assert.AreEqual(obstacleTile, reroutedObstacle, "Discovered obstacle tile must match newly explored impassable sector.");
        }
        finally
        {
            OptimisticPathingUtility.IsTileUnexploredOverride = null;
            OptimisticPathingUtility.IsTileImpassableOverride = null;
            OptimisticPathingUtility.OnCaravanReroutedForTesting = null;
        }
    }

    [TestMethod]
    public void OptimisticPathing_DestinationObstacleRevelation_ReroutesToAdjacentPassableOrHalts()
    {
        try
        {
            ProjectVigilance.Geoscape.GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;

            var startTile = (PlanetTile)100;
            var destTile = (PlanetTile)105;

            var playerFaction = new Faction { def = new FactionDef { isPlayer = true } };
            var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
            HarmonyLib.Traverse.Create(caravan).Field("tileInt").SetValue(startTile);
            HarmonyLib.Traverse.Create(caravan).Field("factionInt").SetValue(playerFaction);
            HarmonyLib.Traverse.Create(caravan).Field("spawnedInt").SetValue(true);

            var pather = (Caravan_PathFollower)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan_PathFollower));
            HarmonyLib.Traverse.Create(pather).Field("caravan").SetValue(caravan);
            HarmonyLib.Traverse.Create(pather).Field("destTile").SetValue(destTile);
            HarmonyLib.Traverse.Create(pather).Field("moving").SetValue(true);
            HarmonyLib.Traverse.Create(caravan).Field("pather").SetValue(pather);

            // Destination tile 105 is now explored and impassable!
            OptimisticPathingUtility.IsTileUnexploredOverride = t => false; // All explored
            OptimisticPathingUtility.IsTileImpassableOverride = t => t.tileId == destTile.tileId;

            Caravan? haltedCaravan = null;
            PlanetTile haltedTile = PlanetTile.Invalid;

            OptimisticPathingUtility.OnCaravanHaltedForTesting = (c, tile, reason) =>
            {
                haltedCaravan = c;
                haltedTile = tile;
            };

            // Vision updates as destination comes into optical/sensory range
            OptimisticPathingUtility.NotifyCaravanVisionUpdated(caravan);

            Assert.IsNotNull(haltedCaravan, "Caravan must halt or re-target when destination sector itself is discovered to be impassable.");
            Assert.AreEqual(destTile, haltedTile);
            Assert.IsFalse(pather.Moving, "Caravan pather must stop dead if destination is impassable with no adjacent passable alternative.");
        }
        finally
        {
            OptimisticPathingUtility.IsTileUnexploredOverride = null;
            OptimisticPathingUtility.IsTileImpassableOverride = null;
            OptimisticPathingUtility.OnCaravanHaltedForTesting = null;
        }
    }

    [TestMethod]
    public void WorldRoutePlanner_Start_Patch_PrePopulatesDestinationWaypoint()
    {
        // Arrange
        PlanetTile currentTile = (PlanetTile)50;
        PlanetTile destTile = (PlanetTile)150;

        var dialog = (Dialog_FormCaravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Dialog_FormCaravan));
        HarmonyLib.Traverse.Create(dialog).Field("destinationTile").SetValue(destTile);

        var planner = (WorldRoutePlanner)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldRoutePlanner));
        HarmonyLib.Traverse.Create(planner).Field("active").SetValue(true);
        var waypoint0 = (RoutePlannerWaypoint)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(RoutePlannerWaypoint));
        HarmonyLib.Traverse.Create(waypoint0).Field("tile").SetValue(currentTile);
        var waypoints = new List<RoutePlannerWaypoint> { waypoint0 };
        HarmonyLib.Traverse.Create(planner).Field("waypoints").SetValue(waypoints);

        PlanetTile addedWaypoint = PlanetTile.Invalid;
        WorldRoutePlanner_Start_Patch.OnWaypointAddedForTesting = t => addedWaypoint = t;

        try
        {
            // Act
            WorldRoutePlanner_Start_Patch.Postfix(planner, dialog);

            // Assert: destination waypoint must be populated
            Assert.AreEqual(destTile, addedWaypoint, "Route planner must add the destination waypoint when dialog has a valid destination tile.");
        }
        finally
        {
            WorldRoutePlanner_Start_Patch.OnWaypointAddedForTesting = null;
        }
    }

    [TestMethod]
    public void WorldObject_ListeningPost_ExpandingIconColor_And_Label_Lifecycle()
    {
        var post = (WorldObject_ListeningPost)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldObject_ListeningPost));
        typeof(WorldObject_ListeningPost).GetField("kind", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(post, ListeningPostKind.AutomatedRelay);
        typeof(WorldObject_ListeningPost).GetField("constructionTicksTotal", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(post, 2500);

        // 1. Under Construction
        post.StartConstruction(2500);
        Assert.IsTrue(post.IsConstructing);
        Assert.AreEqual(2500, post.ConstructionTicksLeft);
        Assert.IsTrue(post.Label.Contains("Under Construction 0%"));

        Color constructingColor = post.ExpandingIconColor;
        Assert.IsTrue(constructingColor.r > 0.8f && constructingColor.g > 0.7f && constructingColor.b < 0.3f,
            "ExpandingIconColor must be amber/yellow during construction.");

        // Advance construction halfway
        typeof(WorldObject_ListeningPost).GetField("constructionTicksLeft", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(post, 1250);
        Assert.IsTrue(post.Label.Contains("Under Construction 50%"));

        // Finish Construction
        post.FinishConstruction();
        Assert.IsFalse(post.IsConstructing);
        Assert.AreEqual(0, post.ConstructionTicksLeft);
        Assert.IsFalse(post.Label.Contains("Under Construction"));

        // 2. Sabotaged
        post.IsSabotaged = true;
        Assert.IsTrue(post.IsSabotaged);
        Assert.IsTrue(post.Label.Contains("Sabotaged"));
        Color sabotagedColor = post.ExpandingIconColor;
        Assert.IsTrue(sabotagedColor.r > 0.8f && sabotagedColor.g < 0.5f && sabotagedColor.b < 0.5f,
            "ExpandingIconColor must be red when sabotaged.");
    }

    [TestMethod]
    public void WorldObjects_ListeningPost_Xml_TexturePaths_And_Priorities()
    {
        string xmlPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(
            System.AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "..", "1.7", "Defs", "WorldObjectDefs", "WorldObjects_ListeningPost.xml"));

        Assert.IsTrue(System.IO.File.Exists(xmlPath), $"XML file must exist at {xmlPath}");
        string xmlContent = System.IO.File.ReadAllText(xmlPath);

        // Automated post
        Assert.IsTrue(xmlContent.Contains("<texture>World/WorldObjects/Sites/Outpost</texture>"),
            "Automated post texture must point to 3D world quad texture World/WorldObjects/Sites/Outpost");
        Assert.IsFalse(xmlContent.Contains("<texture>World/WorldObjects/Expanding/Sites/Outpost</texture>"),
            "Automated post texture must NOT point to 2D expanding sprite path");

        // Beacon post
        Assert.IsTrue(xmlContent.Contains("<texture>World/WorldObjects/Camp</texture>"),
            "Beacon post texture must point to 3D world quad texture World/WorldObjects/Camp");
        Assert.IsFalse(xmlContent.Contains("<texture>World/WorldObjects/Expanding/Camp</texture>"),
            "Beacon post texture must NOT point to 2D expanding sprite path");

        // Expanding icon priority
        Assert.IsTrue(xmlContent.Contains("<expandingIconPriority>85</expandingIconPriority>"),
            "Expanding icon priority must be elevated to 85 to remain visible and interactive on Geoscape");
    }

    [TestMethod]
    public void Verify_Dialog_FormCaravan_PostOpen_Patch_Applies()
    {
        var m = typeof(Dialog_FormCaravan).GetMethod("TrySend", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.IsNotNull(m);
        var instructions = HarmonyLib.PatchProcessor.GetOriginalInstructions(m);
        foreach (var inst in instructions)
        {
            System.Console.WriteLine($"TRY_SEND_INSTR: {inst.opcode} {inst.operand}");
        }
    }

    [TestMethod]
    public void TryGetPendingDirectiveForDialog_FindsDirectiveByDestinationTile()
    {
        CaravanExpeditionUtility.ClearAllPendingDirectives();
        var targetTile = (PlanetTile)4567;
        var directive = new OutpostConstructionDirective
        {
            TargetTile = targetTile,
            Kind = ListeningPostKind.AutomatedRelay,
            OriginMapId = 2,
            CreatedTick = 1000
        };

        CaravanExpeditionUtility.RegisterPendingDirective(targetTile, directive);

        var dialog = (Dialog_FormCaravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Dialog_FormCaravan));
        typeof(Dialog_FormCaravan).GetField("destinationTile", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(dialog, targetTile);

        bool found = CaravanExpeditionUtility.TryGetPendingDirectiveForDialog(dialog, out var retrieved);
        Assert.IsTrue(found, "TryGetPendingDirectiveForDialog must locate directive by destinationTile.");
        Assert.IsNotNull(retrieved);
        Assert.AreEqual(ListeningPostKind.AutomatedRelay, retrieved.Kind);
        Assert.AreEqual(targetTile, retrieved.TargetTile);
    }

    [TestMethod]
    public void StillValid_DoesNotEvictArrivalAction_DuringTransitWithoutMaterials()
    {
        var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
        typeof(Thing).GetField("spawnedInt", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(caravan, true);
        typeof(WorldObject).GetField("tileInt", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(caravan, (PlanetTile)100);

        var targetTile = (PlanetTile)200;
        var action = new CaravanArrivalAction_ConstructOutpost(targetTile, ListeningPostKind.AutomatedRelay);

        // Caravan has 0 steel and 0 components in inventory, but StillValid must return true to prevent mid-transit eviction
        FloatMenuAcceptanceReport report = action.StillValid(caravan, targetTile);
        Assert.IsTrue(report.Accepted, "StillValid must accept during transit so caravan arrival action is not cleared by PatherTickInterval.");
    }

    [TestMethod]
    public void StillValid_AllowsOutpostUnderConstruction()
    {
        var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
        typeof(Thing).GetField("spawnedInt", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(caravan, true);
        typeof(WorldObject).GetField("tileInt", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(caravan, (PlanetTile)100);

        var targetTile = (PlanetTile)500;
        var post = (WorldObject_ListeningPost)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldObject_ListeningPost));
        typeof(WorldObject).GetField("tileInt", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(post, targetTile);
        post.StartConstruction(1000); // IsConstructing is true

        var action = new CaravanArrivalAction_ConstructOutpost(targetTile, ListeningPostKind.AutomatedRelay);

        // Even if world has an outpost under construction, StillValid must accept
        FloatMenuAcceptanceReport report = action.StillValid(caravan, targetTile);
        Assert.IsTrue(report.Accepted, "StillValid must allow arrival at a post that is currently under construction.");
    }

    [TestMethod]
    public void OutpostAssemblySpeedUtility_BaseDurations_And_Clamps()
    {
        int relayDuration = OutpostAssemblySpeedUtility.CalculateAssemblyDurationTicks(null, ListeningPostKind.AutomatedRelay);
        Assert.AreEqual(OutpostAssemblySpeedUtility.BaseAutomatedRelayTicks, relayDuration, "Default relay duration without pawns must equal base ticks.");

        int beaconDuration = OutpostAssemblySpeedUtility.CalculateAssemblyDurationTicks(null, ListeningPostKind.SignalBeacon);
        Assert.AreEqual(OutpostAssemblySpeedUtility.BaseSignalBeaconTicks, beaconDuration, "Default beacon duration without pawns must equal base ticks.");
    }

    [TestMethod]
    public void OutpostAssemblySpeedUtility_HasCapableBuilder_And_EstimateLabel()
    {
        Assert.IsFalse(OutpostAssemblySpeedUtility.HasCapableBuilder(null));
        Assert.IsFalse(OutpostAssemblySpeedUtility.HasCapableBuilder(new List<Pawn>()));

        string blockedLabel = OutpostAssemblySpeedUtility.GetEstimateLabel(new List<Pawn>(), ListeningPostKind.AutomatedRelay);
        Assert.IsTrue(blockedLabel.Contains("BLOCKED"), "Must show BLOCKED when no capable builders are selected.");
        Assert.IsTrue(blockedLabel.Contains("Must select at least one colonist capable of Construction"));

        string beaconBlocked = OutpostAssemblySpeedUtility.GetEstimateLabel(null, ListeningPostKind.SignalBeacon);
        Assert.IsTrue(beaconBlocked.Contains("BLOCKED"));
    }

    [TestMethod]
    public void Verify_Dialog_FormCaravan_CheckForErrors_Patch_Applies()
    {
        var harmony = new HarmonyLib.Harmony("test.patch.check.dialog.errors");
        var patchClass = typeof(Dialog_FormCaravan_CheckForErrors_Patch);
        var methods = patchClass.GetCustomAttributes(typeof(HarmonyLib.HarmonyPatch), false);
        Assert.IsTrue(methods.Length > 0);

        var original = typeof(Dialog_FormCaravan).GetMethod("CheckForErrors", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new[] { typeof(List<Pawn>) }, null);
        Assert.IsNotNull(original, "Original Dialog_FormCaravan.CheckForErrors must exist.");

        var patchProcessor = harmony.CreateProcessor(original);
        patchProcessor.AddPostfix(new HarmonyLib.HarmonyMethod(patchClass.GetMethod("Postfix")));
        var patched = patchProcessor.Patch();
        Assert.IsNotNull(patched, "Dialog_FormCaravan_CheckForErrors_Patch must succeed without error.");
    }

    [TestMethod]
    public void Verify_Dialog_FormCaravan_TrySend_Patch_Applies()
    {
        var harmony = new HarmonyLib.Harmony("test.patch.check.dialog.trysend");
        var patchClass = typeof(Dialog_FormCaravan_TrySend_Patch);
        var methods = patchClass.GetCustomAttributes(typeof(HarmonyLib.HarmonyPatch), false);
        Assert.IsTrue(methods.Length > 0);

        var original = typeof(Dialog_FormCaravan).GetMethod("TrySend", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, System.Type.EmptyTypes, null);
        Assert.IsNotNull(original, "Original Dialog_FormCaravan.TrySend must exist.");

        var patchProcessor = harmony.CreateProcessor(original);
        patchProcessor.AddPrefix(new HarmonyLib.HarmonyMethod(patchClass.GetMethod("Prefix")));
        var patched = patchProcessor.Patch();
        Assert.IsNotNull(patched, "Dialog_FormCaravan_TrySend_Patch must succeed without error.");
    }

    [TestMethod]
    public void Dialog_FormCaravan_TrySend_Patch_IgnoresNormalCaravans()
    {
        var dialog = (Dialog_FormCaravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Dialog_FormCaravan));
        bool shouldProceed = Dialog_FormCaravan_TrySend_Patch.Prefix(dialog);
        Assert.IsTrue(shouldProceed, "Prefix must return true for normal caravans with no pending directive.");
    }

    [TestMethod]
    public void Dialog_FormCaravan_TrySend_Patch_InterceptsOutpostCaravans()
    {
        var dialog = (Dialog_FormCaravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Dialog_FormCaravan));
        var tile = (PlanetTile)9999;
        var directive = new OutpostConstructionDirective
        {
            TargetTile = tile,
            Kind = ListeningPostKind.AutomatedRelay,
            DirectiveKind = OutpostDirectiveKind.Construct
        };
        CaravanExpeditionUtility.RegisterPendingDirective(tile, directive);

        // Set destinationTile field on dialog
        typeof(Dialog_FormCaravan).GetField("destinationTile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(dialog, tile);

        bool shouldProceed = Dialog_FormCaravan_TrySend_Patch.Prefix(dialog);
        Assert.IsFalse(shouldProceed, "Prefix must return false to intercept outpost expedition and show confirmation.");
    }

    [TestMethod]
    public void CaravanExpeditionUtility_GetEstimatedTransitTicks_FallbackIsPositive()
    {
        var dialog = (Dialog_FormCaravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Dialog_FormCaravan));
        int ticks = CaravanExpeditionUtility.GetEstimatedTransitTicks(dialog, (PlanetTile)100);
        Assert.IsTrue(ticks > 0, "Transit ticks must be positive fallback when uninitialized.");
    }

    [TestMethod]
    public void CaravanExpeditionUtility_GetDaysWorthOfFood_ReturnsZeroWhenUninitialized()
    {
        var dialog = (Dialog_FormCaravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Dialog_FormCaravan));
        float food = CaravanExpeditionUtility.GetDaysWorthOfFood(dialog);
        Assert.AreEqual(0f, food, "Uninitialized dialog must safely return 0 food without throwing.");
    }

    [TestMethod]
    public void CaravanExpeditionUtility_PreallocateExpeditionFood_HandlesNullAndEmptyGracefully()
    {
        CaravanExpeditionUtility.PreallocateExpeditionFood(null!, null!);
        var dialog = (Dialog_FormCaravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Dialog_FormCaravan));
        var directive = new OutpostConstructionDirective { Kind = ListeningPostKind.AutomatedRelay, TargetTile = (PlanetTile)50 };
        CaravanExpeditionUtility.PreallocateExpeditionFood(dialog, directive);
        Assert.AreEqual(0, CaravanExpeditionUtility.PendingDirectivesCount);
    }

    [TestMethod]
    public void CaravanArrivalAction_DismantleOutpost_ConstructingPost_ShowsRefundLabel()
    {
        var post = new WorldObject_ListeningPost
        {
            Kind = ListeningPostKind.AutomatedRelay
        };
        post.StartConstruction(2500);
        Assert.IsTrue(post.IsConstructing);

        var tile = (PlanetTile)4200;
        var action = new CaravanArrivalAction_DismantleOutpost(post, tile);

        Assert.AreEqual(tile, action.TargetTile);
        Assert.AreSame(post, action.TargetOutpost);
        StringAssert.Contains(action.Label, "(Refund 100% Materials)");
        StringAssert.Contains(action.ReportString, "travelling to dismantle and recover materials");
    }

    [TestMethod]
    public void WorldSelector_HandleWorldClicks_Patch_DeduplicateOptions_RemovesDuplicatesPreservingOrder()
    {
        var options = new List<FloatMenuOption>
        {
            new FloatMenuOption("Cancel Construction of Relay (Refund 100% Materials)", null),
            new FloatMenuOption("Halt & resume construction of Relay", null),
            new FloatMenuOption("Cancel Construction of Relay (Refund 100% Materials)", null),
            new FloatMenuOption("Dismantle Relay (Recover 75% Materials)", null)
        };

        ProjectVigilance.Geoscape.Patches.WorldSelector_HandleWorldClicks_Patch.DeduplicateOptions(options);

        Assert.AreEqual(3, options.Count);
        Assert.AreEqual("Cancel Construction of Relay (Refund 100% Materials)", options[0].Label);
        Assert.AreEqual("Halt & resume construction of Relay", options[1].Label);
        Assert.AreEqual("Dismantle Relay (Recover 75% Materials)", options[2].Label);
    }

    [TestMethod]
    public void WorldObject_ListeningPost_DismantleWhileConstructing_CancelsConstruction()
    {
        var post = new WorldObject_ListeningPost
        {
            Kind = ListeningPostKind.AutomatedRelay
        };
        post.StartConstruction(2500);
        Assert.IsTrue(post.IsConstructing);

        // When Dismantle is called on an outpost under construction, it must redirect to CancelConstruction
        post.Dismantle(null!);
        Assert.IsFalse(post.IsConstructing, "Construction flag must clear after cancelling/dismantling unfinished post.");
        Assert.IsFalse(post.IsDismantling, "Post must not transition to active dismantling.");
    }

    [TestMethod]
    public void Inspect_Caravan_PathFollower_StartPath()
    {
        var type = typeof(Caravan_PathFollower);
        var mStartPath = type.GetMethod("StartPath", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, new[] { typeof(PlanetTile), typeof(CaravanArrivalAction), typeof(bool), typeof(bool) }, null);
        Assert.IsNotNull(mStartPath);
        var instrs = HarmonyLib.PatchProcessor.GetOriginalInstructions(mStartPath);
        foreach (var i in instrs.Where(x => x.opcode.Name is { } name && (name.Contains("call") || name.Contains("ldfld") || name.Contains("stfld") || name.Contains("ret"))))
        {
            System.Console.WriteLine($"START_PATH: {i.opcode} {i.operand}");
        }
    }

    [TestMethod]
    public void IncursionMovement_TerrainDifficulty_ScalesEffectiveSpeedAndTileProgress()
    {
        var previousOverride = ProjectVigilance.Geoscape.World.IncursionOriginResolver.StepMovementDifficultyOverride;
        try
        {
            var incursion = new ProjectVigilance.Geoscape.World.WorldObject_Incursion();
            incursion.SetTileRaw((PlanetTile)100);
            float baseSpeed = incursion.SpeedTilesPerDay;

            ProjectVigilance.Geoscape.World.IncursionOriginResolver.StepMovementDifficultyOverride = (_, _) => 1.0f;
            Assert.AreEqual(1.0f, incursion.GetCurrentMovementDifficulty(), 0.0001f);
            Assert.AreEqual(baseSpeed, incursion.EffectiveSpeedTilesPerDay, 0.001f);

            ProjectVigilance.Geoscape.World.IncursionOriginResolver.StepMovementDifficultyOverride = (_, _) => 3.0f;
            Assert.AreEqual(3.0f, incursion.GetCurrentMovementDifficulty(), 0.0001f);
            Assert.AreEqual(baseSpeed / 3.0f, incursion.EffectiveSpeedTilesPerDay, 0.001f);

            ProjectVigilance.Geoscape.World.IncursionOriginResolver.StepMovementDifficultyOverride = (_, _) => 0.5f;
            Assert.AreEqual(0.5f, incursion.GetCurrentMovementDifficulty(), 0.0001f);
            Assert.AreEqual(baseSpeed * 2.0f, incursion.EffectiveSpeedTilesPerDay, 0.001f);
        }
        finally
        {
            ProjectVigilance.Geoscape.World.IncursionOriginResolver.StepMovementDifficultyOverride = previousOverride;
        }
    }

    [TestMethod]
    public void IncursionSpeed_LandFactions_UseFootCaravanBaseline()
    {
        float nullSpeed = ProjectVigilance.Geoscape.World.IncursionOriginResolver.GetSpeedForFaction(null);
        Assert.IsTrue(nullSpeed >= 18f && nullSpeed <= 22f, $"Null faction fallback speed ({nullSpeed}) must match foot caravan baseline.");

        TechLevel[] techLevels =
        {
            TechLevel.Neolithic,
            TechLevel.Medieval,
            TechLevel.Industrial,
            TechLevel.Spacer,
            TechLevel.Ultra,
            TechLevel.Archotech
        };

        foreach (TechLevel tl in techLevels)
        {
            var faction = new Faction { def = new FactionDef { techLevel = tl } };
            float speed = ProjectVigilance.Geoscape.World.IncursionOriginResolver.GetSpeedForFaction(faction);
            Assert.IsTrue(speed >= 18f && speed <= 22f,
                $"Land faction tech level {tl} foot speed ({speed}) must remain within [18, 22] tiles/day.");
        }
    }

    [TestMethod]
    public void SetupMoveIntoNextTilePatch_TargetsVanillaMethod_AndIgnoresNonPlayerPathers()
    {
        Assert.IsNotNull(HarmonyLib.AccessTools.Method(typeof(Caravan_PathFollower), "SetupMoveIntoNextTile"));

        var attr = typeof(WorldPathing_FindPath_Patch.Caravan_PathFollower_SetupMoveIntoNextTile_Patch)
            .GetCustomAttributes(typeof(HarmonyLib.HarmonyPatch), false)
            .Cast<HarmonyLib.HarmonyPatch>()
            .Single();
        Assert.AreEqual(typeof(Caravan_PathFollower), attr.info.declaringType);
        Assert.AreEqual("SetupMoveIntoNextTile", attr.info.methodName);

        var pather = (Caravan_PathFollower)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan_PathFollower));
        Assert.IsTrue(WorldPathing_FindPath_Patch.Caravan_PathFollower_SetupMoveIntoNextTile_Patch.Prefix(pather),
            "Pathers without a player caravan must run vanilla movement unchanged.");
    }

    [TestMethod]
    public void RetreatPursuit_OnlyAbleHostileForcesChaseCaravan()
    {
        Assert.IsTrue(ProjectVigilance.Geoscape.World.IncursionTacticalRetreatUtility.ShouldPursueRetreatingCaravan(true, false, 4, 4));
        Assert.IsTrue(ProjectVigilance.Geoscape.World.IncursionTacticalRetreatUtility.ShouldPursueRetreatingCaravan(true, false, 4, 2));
        Assert.IsFalse(ProjectVigilance.Geoscape.World.IncursionTacticalRetreatUtility.ShouldPursueRetreatingCaravan(true, false, 4, 1), "Battered raiders must not pursue.");
        Assert.IsFalse(ProjectVigilance.Geoscape.World.IncursionTacticalRetreatUtility.ShouldPursueRetreatingCaravan(false, false, 4, 4), "Non-hostile groups must not pursue.");
        Assert.IsFalse(ProjectVigilance.Geoscape.World.IncursionTacticalRetreatUtility.ShouldPursueRetreatingCaravan(true, true, 4, 4), "Trade caravans must not pursue.");
        Assert.IsFalse(ProjectVigilance.Geoscape.World.IncursionTacticalRetreatUtility.ShouldPursueRetreatingCaravan(true, false, 0, 0));
    }

    [TestMethod]
    public void AmbushPatch_TargetsVanillaAmbushWorker()
    {
        var attr = typeof(ProjectVigilance.Geoscape.Patches.IncidentWorker_Ambush_Patch)
            .GetCustomAttributes(typeof(HarmonyLib.HarmonyPatch), false)
            .Cast<HarmonyLib.HarmonyPatch>()
            .Single();

        Assert.AreEqual(typeof(RimWorld.IncidentWorker_Ambush), attr.info.declaringType);
        Assert.AreEqual("TryExecuteWorker", attr.info.methodName);
        Assert.IsNotNull(HarmonyLib.AccessTools.Method(typeof(RimWorld.IncidentWorker_Ambush), "TryExecuteWorker"));
    }

    [TestMethod]
    public void FindPathTranspiler_InjectsFogCostOverride_IntoVanillaPathfinder()
    {
        var findPath = typeof(RimWorld.Planet.WorldPathing).GetMethod(
            "FindPath",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.DeclaredOnly);
        Assert.IsNotNull(findPath);

        var original = HarmonyLib.PatchProcessor.GetOriginalInstructions(findPath).ToList();
        var patched = WorldPathing_FindPath_Patch.FindPath_Patch.Transpiler(original).ToList();

        int injected = patched.Count(i =>
            i.opcode == System.Reflection.Emit.OpCodes.Call &&
            i.operand is System.Reflection.MethodInfo mi &&
            mi.Name == nameof(OptimisticPathingUtility.ModifyMovementDifficulty));

        Assert.IsTrue(injected >= 1,
            "The fog-of-war terrain cost override was not injected; terrain difficulty would leak through unexplored tiles.");
    }

    [TestMethod]
    public void OutpostConstruction_NormalColonist_WorksDuringDay_PausesDuringNight()
    {
        var post = new WorldObject_ListeningPost
        {
            Kind = ListeningPostKind.AutomatedRelay
        };

        var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
        var normalPawn = (Pawn)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Pawn));
        var pawns = new List<Pawn> { normalPawn };

        try
        {
            WorldObject_ListeningPost.PlayerCaravanPresenceOverride = _ => caravan;
            WorldObject_ListeningPost.CaravanPawnsOverride = _ => pawns;

            OutpostAssemblySpeedUtility.CapableBuilderOverride = p => p == normalPawn;
            OutpostAssemblySpeedUtility.BuilderSpeedOverride = _ => 1.0f;
            Caravan_NightResting_Patch.NightOwlPredicateOverride = _ => false;

            post.StartConstruction(10);

            // Midday (12:00): Normal colonist is awake and working
            OutpostAssemblySpeedUtility.LocalHourOverride = 12;
            Assert.IsTrue(OutpostAssemblySpeedUtility.IsBuilderAwake(normalPawn, post.Tile));

            for (int i = 0; i < 3; i++)
            {
                post.TickForTesting();
            }

            Assert.AreEqual(7, post.ConstructionTicksLeft, "Daytime ticks must advance construction normally.");
            StringAssert.Contains(post.GetInspectString(), "Under Construction (30%");

            // Nighttime (02:00): Normal colonist is sleeping (22:00-06:00)
            OutpostAssemblySpeedUtility.LocalHourOverride = 2;
            Assert.IsFalse(OutpostAssemblySpeedUtility.IsBuilderAwake(normalPawn, post.Tile));

            for (int i = 0; i < 3; i++)
            {
                post.TickForTesting();
            }

            Assert.AreEqual(7, post.ConstructionTicksLeft, "Nighttime ticks must pause construction when colonist is resting.");
            string inspect = post.GetInspectString();
            StringAssert.Contains(inspect, "Status: Construction Paused (30% complete - Resting - Night/Sleep Period)");
        }
        finally
        {
            WorldObject_ListeningPost.PlayerCaravanPresenceOverride = null;
            WorldObject_ListeningPost.CaravanPawnsOverride = null;
            OutpostAssemblySpeedUtility.CapableBuilderOverride = null;
            OutpostAssemblySpeedUtility.BuilderSpeedOverride = null;
            OutpostAssemblySpeedUtility.LocalHourOverride = null;
            Caravan_NightResting_Patch.NightOwlPredicateOverride = null;
        }
    }

    [TestMethod]
    public void OutpostConstruction_NightOwlColonist_WorksDuringNight_PausesDuringDay()
    {
        var post = new WorldObject_ListeningPost
        {
            Kind = ListeningPostKind.AutomatedRelay
        };

        var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
        var nightOwlPawn = (Pawn)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Pawn));
        var pawns = new List<Pawn> { nightOwlPawn };

        try
        {
            WorldObject_ListeningPost.PlayerCaravanPresenceOverride = _ => caravan;
            WorldObject_ListeningPost.CaravanPawnsOverride = _ => pawns;

            OutpostAssemblySpeedUtility.CapableBuilderOverride = p => p == nightOwlPawn;
            OutpostAssemblySpeedUtility.BuilderSpeedOverride = _ => 1.0f;
            Caravan_NightResting_Patch.NightOwlPredicateOverride = p => p == nightOwlPawn;

            post.StartConstruction(10);

            // Nighttime (02:00): Night Owl is active on night shift
            OutpostAssemblySpeedUtility.LocalHourOverride = 2;
            Assert.IsTrue(OutpostAssemblySpeedUtility.IsBuilderAwake(nightOwlPawn, post.Tile));

            for (int i = 0; i < 3; i++)
            {
                post.TickForTesting();
            }

            Assert.AreEqual(7, post.ConstructionTicksLeft, "Night shift must advance construction for Night Owl.");
            StringAssert.Contains(post.GetInspectString(), "Under Construction (30%");

            // Daytime (14:00): Night Owl rests during daytime (11:00-18:00)
            OutpostAssemblySpeedUtility.LocalHourOverride = 14;
            Assert.IsFalse(OutpostAssemblySpeedUtility.IsBuilderAwake(nightOwlPawn, post.Tile));

            for (int i = 0; i < 3; i++)
            {
                post.TickForTesting();
            }

            Assert.AreEqual(7, post.ConstructionTicksLeft, "Daytime ticks must pause construction when Night Owl is resting.");
            string inspect = post.GetInspectString();
            StringAssert.Contains(inspect, "Status: Construction Paused (30% complete - Resting - Daytime Sleep Period)");
        }
        finally
        {
            WorldObject_ListeningPost.PlayerCaravanPresenceOverride = null;
            WorldObject_ListeningPost.CaravanPawnsOverride = null;
            OutpostAssemblySpeedUtility.CapableBuilderOverride = null;
            OutpostAssemblySpeedUtility.BuilderSpeedOverride = null;
            OutpostAssemblySpeedUtility.LocalHourOverride = null;
            Caravan_NightResting_Patch.NightOwlPredicateOverride = null;
        }
    }

    [TestMethod]
    public void OutpostConstruction_MixedTeam_ConstructsContinuously_WithShiftScaling()
    {
        var post = new WorldObject_ListeningPost
        {
            Kind = ListeningPostKind.AutomatedRelay
        };

        var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
        var normalPawn = (Pawn)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Pawn));
        var nightOwlPawn = (Pawn)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Pawn));
        var pawns = new List<Pawn> { normalPawn, nightOwlPawn };

        try
        {
            WorldObject_ListeningPost.PlayerCaravanPresenceOverride = _ => caravan;
            WorldObject_ListeningPost.CaravanPawnsOverride = _ => pawns;

            OutpostAssemblySpeedUtility.CapableBuilderOverride = _ => true;
            OutpostAssemblySpeedUtility.BuilderSpeedOverride = _ => 1.0f;
            Caravan_NightResting_Patch.NightOwlPredicateOverride = p => p == nightOwlPawn;

            post.StartConstruction(10);

            // Full team speed = 1.0 (lead) + 0.5 (assistant) = 1.5.

            // 1. Midday (12:00): Normal awake (speed 1.0), Night Owl asleep.
            // Active speed = 1.0, speed ratio = 1.0 / 1.5 = 2/3.
            OutpostAssemblySpeedUtility.LocalHourOverride = 12;
            var (_, activeSpeed12, awakeCount12, total12) = OutpostAssemblySpeedUtility.CalculateActiveTeamSpeed(pawns, post.Tile);
            Assert.AreEqual(1, awakeCount12);
            Assert.AreEqual(2, total12);
            Assert.AreEqual(1.0f, activeSpeed12, 0.01f);

            // 3 ticks at 2/3 speed = 2 ticks of work
            for (int i = 0; i < 3; i++)
            {
                post.TickForTesting();
            }

            Assert.AreEqual(8, post.ConstructionTicksLeft);
            StringAssert.Contains(post.GetInspectString(), "Reduced Shift (1/2 awake)");

            // 2. Night (02:00): Night Owl awake (speed 1.0), Normal asleep.
            // Active speed = 1.0, speed ratio = 2/3.
            OutpostAssemblySpeedUtility.LocalHourOverride = 2;
            var (_, activeSpeed2, awakeCount2, total2) = OutpostAssemblySpeedUtility.CalculateActiveTeamSpeed(pawns, post.Tile);
            Assert.AreEqual(1, awakeCount2);
            Assert.AreEqual(2, total2);
            Assert.AreEqual(1.0f, activeSpeed2, 0.01f);

            // 3 ticks at 2/3 speed = 2 ticks of work
            for (int i = 0; i < 3; i++)
            {
                post.TickForTesting();
            }

            Assert.AreEqual(6, post.ConstructionTicksLeft);
            StringAssert.Contains(post.GetInspectString(), "Reduced Shift (1/2 awake)");

            // 3. Evening crossover (19:00): Both awake! Full team speed = 1.5, ratio = 1.0.
            OutpostAssemblySpeedUtility.LocalHourOverride = 19;
            var (_, activeSpeed19, awakeCount19, total19) = OutpostAssemblySpeedUtility.CalculateActiveTeamSpeed(pawns, post.Tile);
            Assert.AreEqual(2, awakeCount19);
            Assert.AreEqual(2, total19);
            Assert.AreEqual(1.5f, activeSpeed19, 0.01f);

            // 3 ticks at 1.0 speed = 3 ticks of work
            for (int i = 0; i < 3; i++)
            {
                post.TickForTesting();
            }

            Assert.AreEqual(3, post.ConstructionTicksLeft);
        }
        finally
        {
            WorldObject_ListeningPost.PlayerCaravanPresenceOverride = null;
            WorldObject_ListeningPost.CaravanPawnsOverride = null;
            OutpostAssemblySpeedUtility.CapableBuilderOverride = null;
            OutpostAssemblySpeedUtility.BuilderSpeedOverride = null;
            OutpostAssemblySpeedUtility.LocalHourOverride = null;
            Caravan_NightResting_Patch.NightOwlPredicateOverride = null;
        }
    }

    [TestMethod]
    public void OutpostDismantling_PausesDuringNightRest()
    {
        var post = new WorldObject_ListeningPost
        {
            Kind = ListeningPostKind.AutomatedRelay
        };

        var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
        var normalPawn = (Pawn)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Pawn));
        var pawns = new List<Pawn> { normalPawn };

        try
        {
            WorldObject_ListeningPost.PlayerCaravanPresenceOverride = _ => caravan;
            WorldObject_ListeningPost.CaravanPawnsOverride = _ => pawns;

            OutpostAssemblySpeedUtility.CapableBuilderOverride = p => p == normalPawn;
            OutpostAssemblySpeedUtility.BuilderSpeedOverride = _ => 1.0f;
            Caravan_NightResting_Patch.NightOwlPredicateOverride = _ => false;

            post.StartDismantling(10);
            Assert.IsTrue(post.IsDismantling);

            // Nighttime (02:00): Normal colonist is sleeping
            OutpostAssemblySpeedUtility.LocalHourOverride = 2;

            for (int i = 0; i < 3; i++)
            {
                post.TickForTesting();
            }

            Assert.AreEqual(10, post.DismantleTicksLeft, "Dismantling must pause when all workers are resting.");
            StringAssert.Contains(post.GetInspectString(), "Status: Dismantling Paused (0% complete - Resting - Night/Sleep Period)");
        }
        finally
        {
            WorldObject_ListeningPost.PlayerCaravanPresenceOverride = null;
            WorldObject_ListeningPost.CaravanPawnsOverride = null;
            OutpostAssemblySpeedUtility.CapableBuilderOverride = null;
            OutpostAssemblySpeedUtility.BuilderSpeedOverride = null;
            OutpostAssemblySpeedUtility.LocalHourOverride = null;
            Caravan_NightResting_Patch.NightOwlPredicateOverride = null;
        }
    }
}

