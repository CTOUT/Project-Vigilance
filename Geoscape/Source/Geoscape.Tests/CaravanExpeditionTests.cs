using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
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
}

