using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape;
using ProjectVigilance.Geoscape.Patches;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Geoscape.Tests;

[TestClass]
[DoNotParallelize]
public sealed class GeoscapeContactInteractionTests
{
    [TestInitialize]
    public void Setup()
    {
        GeoscapeTestHarness.EnsureEnvironmentInitialised();
        GeoscapeMod.Settings.enableOverlappingEntitySelectionMenu = true;
        GeoscapeMod.Settings.enableIntelligentRightClick = true;
        WorldSelector_HandleWorldClicks_Patch.MouseTileOverride = null;
    }

    [TestCleanup]
    public void Cleanup()
    {
        GeoscapeTestHarness.ResetAllStaticState();
        GeoscapeMod.Settings.maxContextualCaravansShown = 3;
        GeoscapeMod.Settings.maxContextualColoniesShown = 2;
    }

    // =========================================================================
    // 1. Overlapping Entity Selection Menu (X-COM Style Contact Disambiguation)
    // =========================================================================

    [TestMethod]
    public void SelectFirstOrNextFrom_SingleOrEmptyList_AllowsVanillaDefaultHandling()
    {
        bool emptyResult = WorldSelector_SelectFirstOrNextFrom_Patch.Prefix(null!, new List<WorldObject>(), (PlanetTile)100);
        Assert.IsTrue(emptyResult, "Empty list must fall through to vanilla selector.");

        var settlement = GeoscapeTestHarness.CreateSettlement((PlanetTile)100, GeoscapeTestHarness.CreatePlayerFaction(), id: 1001);
        bool singleResult = WorldSelector_SelectFirstOrNextFrom_Patch.Prefix(null!, new List<WorldObject> { settlement }, (PlanetTile)100);
        Assert.IsTrue(singleResult, "Single entity must select directly via vanilla without menu friction.");
    }

    [TestMethod]
    public void SelectFirstOrNextFrom_MultipleOverlappingEntities_InterceptsAndReturnsFalse()
    {
        var playerFaction = GeoscapeTestHarness.CreatePlayerFaction();
        var settlement = GeoscapeTestHarness.CreateSettlement((PlanetTile)100, playerFaction, "New Hope", 1001);
        var caravan = GeoscapeTestHarness.CreateCaravan((PlanetTile)100, playerFaction, "Alpha Team");

        bool result = WorldSelector_SelectFirstOrNextFrom_Patch.Prefix(null!, new List<WorldObject> { settlement, caravan }, (PlanetTile)100);
        Assert.IsFalse(result, "Multiple overlapping entities must suppress vanilla cycling and display disambiguation menu.");
    }

    [TestMethod]
    public void SelectFirstOrNextFrom_DisabledInSettings_AllowsVanillaCycling()
    {
        GeoscapeMod.Settings.enableOverlappingEntitySelectionMenu = false;

        var settlement = new Settlement { ID = 1001 };
        var caravan = GeoscapeTestHarness.CreateCaravan((PlanetTile)100);

        bool result = WorldSelector_SelectFirstOrNextFrom_Patch.Prefix(null!, new List<WorldObject> { settlement, caravan }, (PlanetTile)100);
        Assert.IsTrue(result, "When disabled in settings, vanilla cycling must remain intact.");
    }

    [TestMethod]
    public void BuildEntitySelectionOptions_OverlappingCaravanAndColony_ConstructsAccurateOptions()
    {
        var playerFaction = GeoscapeTestHarness.CreatePlayerFaction();
        var settlement = GeoscapeTestHarness.CreateSettlement((PlanetTile)120, playerFaction, "New Hope", 1001);
        var caravan = GeoscapeTestHarness.CreateCaravan((PlanetTile)120, playerFaction, "Alpha Team");

        var postDef = new WorldObjectDef { defName = "Vigilance_ListeningPost_Automated", label = "Automated Relay" };
        var post = new WorldObject_ListeningPost { def = postDef, ID = 1003, Kind = ListeningPostKind.AutomatedRelay };
        Traverse.Create(post).Field("tile").SetValue((PlanetTile)120);

        var options = WorldSelector_SelectFirstOrNextFrom_Patch.BuildEntitySelectionOptions(
            new List<WorldObject> { settlement, caravan, post },
            (PlanetTile)120);

        Assert.IsNotNull(options);
        Assert.AreEqual(4, options.Count, "Must offer 3 entity selections plus 1 sector tile selection.");
        Assert.IsTrue(options.Any(o => o.Label.Contains("Player Colony")), "Must identify player settlement as Colony.");
        Assert.IsTrue(options.Any(o => o.Label.Contains("Caravan")), "Must identify caravan entity.");
        Assert.IsTrue(options.Any(o => o.Label.Contains("AutomatedRelay")), "Must identify listening post kind.");
        Assert.IsTrue(options.Any(o => o.Label == "Select Tile 120"), "Must provide ground sector selection option.");
    }

    [TestMethod]
    public void BuildEntitySelectionOptions_DeduplicatesIdenticalEntities()
    {
        var settlement = GeoscapeTestHarness.CreateSettlement((PlanetTile)200, name: "New Hope", id: 1001);
        var caravan = GeoscapeTestHarness.CreateCaravan((PlanetTile)200, name: "Alpha Team");

        var duplicateList = new List<WorldObject> { settlement, caravan, settlement, caravan };
        var options = WorldSelector_SelectFirstOrNextFrom_Patch.BuildEntitySelectionOptions(duplicateList, (PlanetTile)200);

        Assert.AreEqual(3, options.Count, "Must deduplicate entities (2 unique entities + 1 tile option).");
    }

    [TestMethod]
    public void BuildEntitySelectionOptions_MaskedSite_ProtectsIntelligenceConcealment()
    {
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.FogOfWar;
        SensoryManager.Instance = new SensoryManager(null!);

        var siteDef = new WorldObjectDef { defName = "OutpostSite", label = "Pirate Outpost" };
        var site = new Site { def = siteDef, ID = 1004 };
        Traverse.Create(site).Field("tile").SetValue((PlanetTile)350);

        SensoryManager.IsExploredOverride = t => t.tileId == 350;
        SensoryManager.IsIdentifiedOverride = _ => false;

        string label = WorldSelector_SelectFirstOrNextFrom_Patch.ResolveEntityLabel(site);
        Assert.AreEqual("Unidentified Contact", label, "Unidentified site must be masked as Unidentified Contact.");
    }

    // =========================================================================
    // 2. Intelligent Right-Click Functionality (Multi-Source Suggestions)
    // =========================================================================

    [TestMethod]
    public void BuildIntelligentContextualOptions_NoPlayerAssets_ReturnsEmpty()
    {
        var options = WorldSelector_HandleWorldClicks_Patch.BuildIntelligentContextualOptions(
            (PlanetTile)500,
            Enumerable.Empty<Caravan>(),
            Enumerable.Empty<Settlement>());

        Assert.IsNotNull(options);
        Assert.AreEqual(0, options.Count, "With no player assets available, suggestions list must be empty.");
    }

    [TestMethod]
    public void BuildIntelligentContextualOptions_SingleSettlement_OffersSendCaravanAndConstruction()
    {
        var settlement = GeoscapeTestHarness.CreateSettlement(
            (PlanetTile)100,
            GeoscapeTestHarness.CreatePlayerFaction(),
            "Colony Alpha",
            2001,
            GeoscapeTestHarness.CreateMockMap());

        var options = WorldSelector_HandleWorldClicks_Patch.BuildIntelligentContextualOptions(
            (PlanetTile)115,
            Enumerable.Empty<Caravan>(),
            new List<Settlement> { settlement });

        Assert.IsNotNull(options);
        Assert.IsTrue(options.Count >= 1, "Must offer dispatch options from settlement.");

        var sendOption = options.FirstOrDefault(o => o.Label.StartsWith("Send caravan from"));
        Assert.IsNotNull(sendOption, "Must offer intelligent option starting with 'Send caravan from'.");
        Assert.IsTrue(sendOption.Label.Contains("to Tile 115"), "Must target Tile 115.");
    }

    [TestMethod]
    public void BuildIntelligentContextualOptions_CaravanAndSettlement_IntelligentlyOrdersByDistance()
    {
        var playerFaction = GeoscapeTestHarness.CreatePlayerFaction();
        var settlement = GeoscapeTestHarness.CreateSettlement((PlanetTile)100, playerFaction, "Colony Alpha", 2001, GeoscapeTestHarness.CreateMockMap());
        var caravan = GeoscapeTestHarness.CreateCaravan((PlanetTile)110, playerFaction, "Caravan One");

        var options = WorldSelector_HandleWorldClicks_Patch.BuildIntelligentContextualOptions(
            (PlanetTile)112,
            new List<Caravan> { caravan },
            new List<Settlement> { settlement });

        Assert.IsNotNull(options);
        Assert.IsTrue(options.Count >= 2, "Must contain options for both caravan and settlement.");
        Assert.IsTrue(options[0].Label.StartsWith("Move caravan"),
            $"Closer caravan must appear before settlement. First option was: {options[0].Label}");
        Assert.IsTrue(options.Any(o => o.Label.StartsWith("Send caravan from")),
            "Must also contain 'Send caravan from' option for the settlement.");
    }

    [TestMethod]
    public void BuildIntelligentContextualOptions_UnexploredSector_SuppressesLeaksAndConstruction()
    {
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.FogOfWar;
        SensoryManager.Instance = new SensoryManager(null!);
        SensoryManager.IsExploredOverride = t => t.tileId == 100;

        var playerFaction = GeoscapeTestHarness.CreatePlayerFaction();
        var settlement = GeoscapeTestHarness.CreateSettlement((PlanetTile)100, playerFaction, "Colony Alpha", 2001, GeoscapeTestHarness.CreateMockMap());
        var caravan = GeoscapeTestHarness.CreateCaravan((PlanetTile)100, playerFaction, "Caravan One");

        var options = WorldSelector_HandleWorldClicks_Patch.BuildIntelligentContextualOptions(
            (PlanetTile)600,
            new List<Caravan> { caravan },
            new List<Settlement> { settlement });

        Assert.IsNotNull(options);
        Assert.AreEqual(2, options.Count, "Must offer exactly 2 generic exploration options (1 caravan + 1 settlement).");
        Assert.IsTrue(options[0].Label.Contains("towards unexplored sector (Tile 600)"));
        Assert.IsTrue(options[1].Label.Contains("towards unexplored sector (Tile 600)"));
        Assert.IsFalse(options.Any(o => o.Label.ToLowerInvariant().Contains("impassable")),
            "Must never leak terrain passability in uncharted black unknown.");
        Assert.IsFalse(options.Any(o => o.Label.ToLowerInvariant().Contains("beacon") || o.Label.ToLowerInvariant().Contains("listening post")),
            "Must not offer construction expeditions into unexplored territory.");
    }

    [TestMethod]
    public void BuildIntelligentContextualOptions_RightClickOwnColony_OffersFormCaravanLocally()
    {
        var playerFaction = GeoscapeTestHarness.CreatePlayerFaction();
        var settlement = GeoscapeTestHarness.CreateSettlement((PlanetTile)100, playerFaction, "Colony Alpha", 2001, GeoscapeTestHarness.CreateMockMap());
        var distantCaravan = GeoscapeTestHarness.CreateCaravan((PlanetTile)140, playerFaction, "Distant Expedition");

        var options = WorldSelector_HandleWorldClicks_Patch.BuildIntelligentContextualOptions(
            (PlanetTile)100,
            new List<Caravan> { distantCaravan },
            new List<Settlement> { settlement });

        Assert.IsNotNull(options);
        Assert.IsTrue(options.Any(o => o.Label.StartsWith("Form caravan at")),
            "Right clicking own colony must offer 'Form caravan at'.");
        Assert.IsTrue(options.Any(o => o.Label.StartsWith("Recall caravan") && o.Label.Contains("Colony Alpha")),
            "Right clicking own colony must offer recalling distant caravan back to the colony.");
    }

    [TestMethod]
    public void BuildEntitySelectionOptions_NullListAndNullElements_ReturnsSafelyWithoutThrowing()
    {
        var nullResult = WorldSelector_SelectFirstOrNextFrom_Patch.BuildEntitySelectionOptions(null!, PlanetTile.Invalid);
        Assert.IsNotNull(nullResult);
        Assert.AreEqual(0, nullResult.Count);

        var listWithNulls = new List<WorldObject?> { null, null };
        var options = WorldSelector_SelectFirstOrNextFrom_Patch.BuildEntitySelectionOptions(listWithNulls!, PlanetTile.Invalid);
        Assert.IsNotNull(options);
        Assert.AreEqual(0, options.Count, "Null elements and invalid tile must produce empty options list.");
    }

    [TestMethod]
    public void BuildIntelligentContextualOptions_CaravanAlreadyOnTile_OffersLocalDeployment()
    {
        var caravan = GeoscapeTestHarness.CreateCaravan((PlanetTile)250, GeoscapeTestHarness.CreatePlayerFaction(), "Surveyor Unit");

        var options = WorldSelector_HandleWorldClicks_Patch.BuildIntelligentContextualOptions(
            (PlanetTile)250,
            new List<Caravan> { caravan },
            Enumerable.Empty<Settlement>());

        Assert.IsNotNull(options);
        Assert.IsTrue(options.Any(o => o.Label.Contains("Deploy Automated Listening Post with caravan Surveyor Unit")),
            "Stationed caravan on clicked tile must offer local outpost deployment.");
    }

    [TestMethod]
    public void BuildIntelligentContextualOptions_MultipleCaravansAtDifferentDistances_SortsClosestFirst()
    {
        var playerFaction = GeoscapeTestHarness.CreatePlayerFaction();
        var closeCaravan = GeoscapeTestHarness.CreateCaravan((PlanetTile)102, playerFaction, "Vanguard Recon");
        var farCaravan = GeoscapeTestHarness.CreateCaravan((PlanetTile)180, playerFaction, "Rear Supply");

        var options = WorldSelector_HandleWorldClicks_Patch.BuildIntelligentContextualOptions(
            (PlanetTile)100,
            new List<Caravan> { farCaravan, closeCaravan },
            Enumerable.Empty<Settlement>());

        Assert.IsNotNull(options);
        Assert.IsTrue(options.Count >= 2);

        int vanguardIndex = options.FindIndex(o => o.Label.Contains("Vanguard Recon"));
        int rearIndex = options.FindIndex(o => o.Label.Contains("Rear Supply"));
        Assert.IsTrue(vanguardIndex < rearIndex,
            $"Closest caravan must precede distant caravan in intelligent suggestions. Vanguard index: {vanguardIndex}, Rear index: {rearIndex}");
    }

    [TestMethod]
    public void BuildEntitySelectionOptions_UnidentifiedContact_ConcealsFactionCrestAndColour()
    {
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.FogOfWar;
        SensoryManager.Instance = new SensoryManager(null!);
        SensoryManager.IsExploredOverride = t => t.tileId == 350;
        SensoryManager.IsIdentifiedOverride = _ => false;

        var pirateFaction = GeoscapeTestHarness.CreateNpcFaction(color: Color.red);
        var siteDef = new WorldObjectDef { defName = "PirateFortress", label = "Secret Pirate Base" };
        var site = new Site { def = siteDef, ID = 9001 };
        Traverse.Create(site).Field("tile").SetValue((PlanetTile)350);
        Traverse.Create(site).Field("factionInt").SetValue(pirateFaction);

        var options = WorldSelector_SelectFirstOrNextFrom_Patch.BuildEntitySelectionOptions(
            new List<WorldObject> { site },
            (PlanetTile)350);

        Assert.IsNotNull(options);
        Assert.AreEqual(2, options.Count);
        var siteOpt = options[0];
        Assert.AreEqual("Unidentified Contact", siteOpt.Label);
        Assert.AreNotEqual(Color.red, siteOpt.iconColor, "Must conceal pirate red faction colour for un-scanned contact.");
        Assert.AreEqual(WorldObject_Visibility_Patch.UnknownSiteIconColor, siteOpt.iconColor, "Must use mystery amber colour for un-scanned contact.");
    }

    [TestMethod]
    public void BuildIntelligentContextualOptions_UnidentifiedTargetSite_HidesRealSiteName()
    {
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.FogOfWar;
        SensoryManager.Instance = new SensoryManager(null!);
        SensoryManager.IsExploredOverride = t => t.tileId == 400 || t.tileId == 100;
        SensoryManager.IsIdentifiedOverride = _ => false;

        var caravan = GeoscapeTestHarness.CreateCaravan((PlanetTile)100, GeoscapeTestHarness.CreatePlayerFaction(), "Alpha Team");
        var enemyFaction = new Faction { def = new FactionDef { isPlayer = false } };
        var siteDef = new WorldObjectDef { defName = "PirateFortress", label = "Secret Pirate Base" };
        var site = new Site { def = siteDef, ID = 9002 };
        Traverse.Create(site).Field("tile").SetValue((PlanetTile)400);
        Traverse.Create(site).Field("factionInt").SetValue(enemyFaction);

        var options = WorldSelector_HandleWorldClicks_Patch.BuildIntelligentContextualOptions(
            (PlanetTile)400,
            new List<Caravan> { caravan },
            Enumerable.Empty<Settlement>());

        Assert.IsNotNull(options);
        Assert.IsTrue(options.Count >= 1);
        Assert.IsFalse(options.Any(o => o.Label.Contains("Secret Pirate Base")),
            "Right-click menu must never leak true name of unidentified enemy base.");
        Assert.IsTrue(options.Any(o => o.Label.Contains("Tile 400")),
            "Must refer to target hex without revealing classified site name.");
    }

    [TestMethod]
    public void BuildIntelligentContextualOptions_ImpassableTileNearColony_SuppressesDispatchToSelf()
    {
        var settlement = GeoscapeTestHarness.CreateSettlement(
            (PlanetTile)100,
            GeoscapeTestHarness.CreatePlayerFaction(),
            "Coastal Base",
            3001,
            GeoscapeTestHarness.CreateMockMap());

        var options = WorldSelector_HandleWorldClicks_Patch.BuildIntelligentContextualOptions(
            (PlanetTile)100,
            Enumerable.Empty<Caravan>(),
            new List<Settlement> { settlement });

        Assert.IsNotNull(options);
        Assert.IsTrue(options.Any(o => o.Label == "Form caravan at Coastal Base"));
        Assert.IsFalse(options.Any(o => o.Label.StartsWith("Send caravan from") && o.Label.Contains("to Coastal Base")),
            "Colony must not offer sending caravan to its own tile.");
        Assert.IsFalse(options.Any(o => o.Label.StartsWith("Dispatch Construction Expedition")),
            "Colony must not offer deploying outposts on top of itself.");
    }

    [TestMethod]
    public void OpenCaravanFormingDialog_InvalidTargetTile_OpensWithoutThrowing()
    {
        CaravanExpeditionUtility.OpenCaravanFormingDialog(GeoscapeTestHarness.CreateMockMap(), PlanetTile.Invalid);
    }

    [TestMethod]
    public void SelectFirstOrNextFrom_ShiftHeld_ConfiguresAdditiveOptionAction()
    {
        var playerFaction = GeoscapeTestHarness.CreatePlayerFaction();
        var settlement = GeoscapeTestHarness.CreateSettlement((PlanetTile)100, playerFaction, "New Hope", 1001);
        var caravan = GeoscapeTestHarness.CreateCaravan((PlanetTile)100, playerFaction, "Alpha Team");

        var options = WorldSelector_SelectFirstOrNextFrom_Patch.BuildEntitySelectionOptions(
            new List<WorldObject> { settlement, caravan },
            (PlanetTile)100,
            shiftHeldOnOpen: true);

        Assert.IsNotNull(options);
        Assert.AreEqual(3, options.Count);
        Assert.IsNotNull(options[0].action);
        options[0].action.Invoke();
    }

    [TestMethod]
    public void ResolveEntityLabel_PlayerColony_IncludesColonyTagWithoutCrashing()
    {
        var settlement = GeoscapeTestHarness.CreateSettlement(PlanetTile.Invalid, GeoscapeTestHarness.CreatePlayerFaction(), "New Hope", 1001);
        Assert.AreEqual("New Hope (Player Colony)", WorldSelector_SelectFirstOrNextFrom_Patch.ResolveEntityLabel(settlement));
    }

    [TestMethod]
    public void SelectUnderMouse_DisabledInSettings_ReturnsTrue()
    {
        GeoscapeMod.Settings.enableOverlappingEntitySelectionMenu = false;
        Assert.IsTrue(WorldSelector_SelectUnderMouse_Patch.Prefix(null!, true),
            "When disabled in settings, SelectUnderMouse prefix must allow vanilla execution.");
    }

    [TestMethod]
    public void BuildIntelligentContextualOptions_CaravansExceedLimit_ClampsDirectOptionsAndAddsDrilldown()
    {
        var playerFaction = GeoscapeTestHarness.CreatePlayerFaction();
        var caravans = Enumerable.Range(1, 5)
            .Select(i => GeoscapeTestHarness.CreateCaravan((PlanetTile)(200 + i), playerFaction, $"Caravan {i}"))
            .ToList();

        GeoscapeMod.Settings.maxContextualCaravansShown = 3;

        var options = WorldSelector_HandleWorldClicks_Patch.BuildIntelligentContextualOptions(
            (PlanetTile)200,
            caravans,
            Enumerable.Empty<Settlement>());

        Assert.IsNotNull(options);
        Assert.IsTrue(options.Any(o => o.Label.Contains("Caravan 1")));
        Assert.IsTrue(options.Any(o => o.Label.Contains("Caravan 2")));
        Assert.IsTrue(options.Any(o => o.Label.Contains("Caravan 3")));
        Assert.IsFalse(options.Any(o => o.Label.Contains("Caravan 4")));
        Assert.IsFalse(options.Any(o => o.Label.Contains("Caravan 5")));

        var moreOption = options.FirstOrDefault(o => o.Label.StartsWith("Other caravans (2 more)... >"));
        Assert.IsNotNull(moreOption, "Must offer 'Other caravans (2 more)... >' drilldown option.");
        Assert.IsNotNull(moreOption.action, "Drilldown option must have a valid non-null click action.");
        moreOption.action.Invoke();
    }

    [TestMethod]
    public void BuildIntelligentContextualOptions_ColoniesExceedLimit_ClampsDirectOptionsAndAddsDrilldown()
    {
        var playerFaction = GeoscapeTestHarness.CreatePlayerFaction();
        var settlements = Enumerable.Range(1, 4)
            .Select(i => GeoscapeTestHarness.CreateSettlement((PlanetTile)(300 + i * 2), playerFaction, $"Outpost {i}", 4000 + i, GeoscapeTestHarness.CreateMockMap()))
            .ToList();

        GeoscapeMod.Settings.maxContextualColoniesShown = 2;

        var options = WorldSelector_HandleWorldClicks_Patch.BuildIntelligentContextualOptions(
            (PlanetTile)300,
            Enumerable.Empty<Caravan>(),
            settlements);

        Assert.IsNotNull(options);
        Assert.IsTrue(options.Any(o => o.Label.Contains("Outpost 1")));
        Assert.IsTrue(options.Any(o => o.Label.Contains("Outpost 2")));
        Assert.IsFalse(options.Any(o => o.Label.Contains("Outpost 3")));
        Assert.IsFalse(options.Any(o => o.Label.Contains("Outpost 4")));

        var moreOption = options.FirstOrDefault(o => o.Label.StartsWith("Other colonies (2 more)... >"));
        Assert.IsNotNull(moreOption, "Must offer 'Other colonies (2 more)... >' drilldown option.");
        Assert.IsNotNull(moreOption.action, "Drilldown option must have a valid non-null click action.");
        moreOption.action.Invoke();
    }

    [TestMethod]
    public void BuildIntelligentContextualOptions_Unexplored_ClampsCaravansAndColonies()
    {
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.FogOfWar;
        SensoryManager.Instance = new SensoryManager(null!);
        SensoryManager.IsExploredOverride = _ => false;

        var playerFaction = GeoscapeTestHarness.CreatePlayerFaction();
        var caravans = Enumerable.Range(1, 4)
            .Select(i => GeoscapeTestHarness.CreateCaravan((PlanetTile)(500 + i), playerFaction, $"Scout {i}"))
            .ToList();
        var settlements = Enumerable.Range(1, 3)
            .Select(i => GeoscapeTestHarness.CreateSettlement((PlanetTile)(510 + i * 2), playerFaction, $"Base {i}", 5000 + i, GeoscapeTestHarness.CreateMockMap()))
            .ToList();

        GeoscapeMod.Settings.maxContextualCaravansShown = 2;
        GeoscapeMod.Settings.maxContextualColoniesShown = 1;

        var options = WorldSelector_HandleWorldClicks_Patch.BuildIntelligentContextualOptions(
            (PlanetTile)500,
            caravans,
            settlements);

        Assert.IsNotNull(options);
        Assert.IsTrue(options.Any(o => o.Label.Contains("Scout 1")));
        Assert.IsTrue(options.Any(o => o.Label.Contains("Scout 2")));
        Assert.IsFalse(options.Any(o => o.Label.Contains("Scout 3")));

        Assert.IsTrue(options.Any(o => o.Label.Contains("Base 1")));
        Assert.IsFalse(options.Any(o => o.Label.Contains("Base 2")));

        var moreCaravans = options.FirstOrDefault(o => o.Label.StartsWith("Other caravans towards unexplored (2 more)... >"));
        Assert.IsNotNull(moreCaravans, "Must offer unexplored caravan drilldown.");
        moreCaravans.action?.Invoke();

        var moreColonies = options.FirstOrDefault(o => o.Label.StartsWith("Other colonies towards unexplored (2 more)... >"));
        Assert.IsNotNull(moreColonies, "Must offer unexplored colony drilldown.");
        moreColonies.action?.Invoke();
    }

    [TestMethod]
    public void BuildSettlementDispatchOptions_MultipleBeaconMaterials_CollapsesToDrilldown()
    {
        CaravanExpeditionUtility.BeaconMaterialsOverride = _ => new List<ThingDef>
        {
            new() { defName = "BlocksSlate" },
            new() { defName = "BlocksGranite" }
        };

        var map = GeoscapeTestHarness.CreateMockMap();
        WorldSelector_HandleWorldClicks_Patch.SafeMapOverride = _ => map;
        var settlement = GeoscapeTestHarness.CreateSettlement((PlanetTile)100, GeoscapeTestHarness.CreatePlayerFaction(), "Mining Colony", 6001);

        var options = WorldSelector_HandleWorldClicks_Patch.BuildSettlementDispatchOptions(settlement, (PlanetTile)105);

        Assert.IsNotNull(options);
        Assert.AreEqual(0, options.Count(o => o.Label.Contains("BlocksSlate")), "Must not list materials on flat menu.");
        Assert.AreEqual(0, options.Count(o => o.Label.Contains("BlocksGranite")), "Must not list materials on flat menu.");

        var beaconSubmenu = options.FirstOrDefault(o => o.Label.Contains("Signal Beacon... > (2 material types available)"));
        Assert.IsNotNull(beaconSubmenu, "Must collapse multiple beacon materials into a single drilldown option.");
        beaconSubmenu.action?.Invoke();
    }

    [TestMethod]
    public void BuildSettlementContextualOptions_MultipleBeaconMaterials_CollapsesToDrilldown()
    {
        CaravanExpeditionUtility.BeaconMaterialsOverride = _ => new List<ThingDef>
        {
            new() { defName = "WoodLog" },
            new() { defName = "BlocksSlate" },
            new() { defName = "BlocksMarble" }
        };

        var map = GeoscapeTestHarness.CreateMockMap();
        WorldSelector_HandleWorldClicks_Patch.SafeMapOverride = _ => map;
        var settlement = GeoscapeTestHarness.CreateSettlement((PlanetTile)200, GeoscapeTestHarness.CreatePlayerFaction(), "Forward Base", 7001);

        var options = WorldSelector_HandleWorldClicks_Patch.BuildIntelligentContextualOptions(
            (PlanetTile)8921,
            Enumerable.Empty<Caravan>(),
            new List<Settlement> { settlement });

        Assert.IsNotNull(options);
        var beaconSubmenu = options.FirstOrDefault(o => o.Label.Contains("Signal Beacon... > (3 material types available)"));
        Assert.IsNotNull(beaconSubmenu, "Contextual right-click menu must collapse multiple beacon materials into drilldown.");
        beaconSubmenu.action?.Invoke();
    }
}
