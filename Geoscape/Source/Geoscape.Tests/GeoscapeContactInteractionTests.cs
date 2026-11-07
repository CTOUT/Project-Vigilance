using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Geoscape;
using ProjectVigilance.Geoscape.Patches;
using ProjectVigilance.Geoscape.Sensory;

namespace Geoscape.Tests;

[TestClass]
[DoNotParallelize]
public sealed class GeoscapeContactInteractionTests
{
    private static void EnsureEnvironmentInitialised()
    {
        var prefsField = typeof(Prefs).GetField("data", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        if (prefsField != null && prefsField.GetValue(null) == null)
        {
            var prefsDataType = typeof(Prefs).Assembly.GetType("Verse.PrefsData");
            if (prefsDataType != null)
            {
                prefsField.SetValue(null, System.Activator.CreateInstance(prefsDataType));
            }
        }
        if (LanguageDatabase.activeLanguage == null)
        {
            var lang = new LoadedLanguage("English");
            var infoField = typeof(LoadedLanguage).GetField("info");
            if (infoField != null)
            {
                infoField.SetValue(lang, System.Activator.CreateInstance(infoField.FieldType));
            }
            LanguageDatabase.activeLanguage = lang;
        }
    }

    [TestInitialize]
    public void Setup()
    {
        EnsureEnvironmentInitialised();
        GeoscapeMod.Settings.enableOverlappingEntitySelectionMenu = true;
        GeoscapeMod.Settings.enableIntelligentRightClick = true;
        WorldSelector_HandleWorldClicks_Patch.MouseTileOverride = null;
    }

    [TestCleanup]
    public void Cleanup()
    {
        WorldSelector_HandleWorldClicks_Patch.MouseTileOverride = null;
        WorldSelector_SelectFirstOrNextFrom_Patch.ShiftHeldOverride = null;
        SensoryManager.IsExploredOverride = null;
        SensoryManager.Instance = null;
    }

    // =========================================================================
    // 1. Overlapping Entity Selection Menu (X-COM Style Contact Disambiguation)
    // =========================================================================

    [TestMethod]
    public void SelectFirstOrNextFrom_SingleOrEmptyList_AllowsVanillaDefaultHandling()
    {
        var emptyList = new List<WorldObject>();
        bool emptyResult = WorldSelector_SelectFirstOrNextFrom_Patch.Prefix(null!, emptyList, (PlanetTile)100);
        Assert.IsTrue(emptyResult, "Empty list must fall through to vanilla selector.");

        var playerFaction = new Faction { def = new FactionDef { isPlayer = true } };
        var settlement = new Settlement { ID = 1001 };
        HarmonyLib.Traverse.Create(settlement).Field("tile").SetValue((PlanetTile)100);
        HarmonyLib.Traverse.Create(settlement).Field("factionInt").SetValue(playerFaction);

        var singleList = new List<WorldObject> { settlement };
        bool singleResult = WorldSelector_SelectFirstOrNextFrom_Patch.Prefix(null!, singleList, (PlanetTile)100);
        Assert.IsTrue(singleResult, "Single entity must select directly via vanilla without menu friction.");
    }

    [TestMethod]
    public void SelectFirstOrNextFrom_MultipleOverlappingEntities_InterceptsAndReturnsFalse()
    {
        var playerFaction = new Faction { def = new FactionDef { isPlayer = true } };

        var settlement = new Settlement { ID = 1001 };
        HarmonyLib.Traverse.Create(settlement).Field("tile").SetValue((PlanetTile)100);
        HarmonyLib.Traverse.Create(settlement).Field("factionInt").SetValue(playerFaction);
        HarmonyLib.Traverse.Create(settlement).Field("nameInt").SetValue("New Hope");

        var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
        HarmonyLib.Traverse.Create(caravan).Field("tile").SetValue((PlanetTile)100);
        HarmonyLib.Traverse.Create(caravan).Field("factionInt").SetValue(playerFaction);
        HarmonyLib.Traverse.Create(caravan).Field("nameInt").SetValue("Alpha Team");

        var multiList = new List<WorldObject> { settlement, caravan };
        bool result = WorldSelector_SelectFirstOrNextFrom_Patch.Prefix(null!, multiList, (PlanetTile)100);

        Assert.IsFalse(result, "Multiple overlapping entities must suppress vanilla cycling and display disambiguation menu.");
    }

    [TestMethod]
    public void SelectFirstOrNextFrom_DisabledInSettings_AllowsVanillaCycling()
    {
        GeoscapeMod.Settings.enableOverlappingEntitySelectionMenu = false;

        var playerFaction = new Faction { def = new FactionDef { isPlayer = true } };
        var settlement = new Settlement { ID = 1001 };
        var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));

        var multiList = new List<WorldObject> { settlement, caravan };
        bool result = WorldSelector_SelectFirstOrNextFrom_Patch.Prefix(null!, multiList, (PlanetTile)100);

        Assert.IsTrue(result, "When disabled in settings, vanilla cycling must remain intact.");
    }

    [TestMethod]
    public void BuildEntitySelectionOptions_OverlappingCaravanAndColony_ConstructsAccurateOptions()
    {
        var playerFaction = new Faction { def = new FactionDef { isPlayer = true } };

        var settlement = new Settlement { ID = 1001 };
        HarmonyLib.Traverse.Create(settlement).Field("tile").SetValue((PlanetTile)120);
        HarmonyLib.Traverse.Create(settlement).Field("factionInt").SetValue(playerFaction);
        HarmonyLib.Traverse.Create(settlement).Field("nameInt").SetValue("New Hope");

        var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
        HarmonyLib.Traverse.Create(caravan).Field("tile").SetValue((PlanetTile)120);
        HarmonyLib.Traverse.Create(caravan).Field("factionInt").SetValue(playerFaction);
        HarmonyLib.Traverse.Create(caravan).Field("nameInt").SetValue("Alpha Team");

        var postDef = new WorldObjectDef { defName = "Vigilance_ListeningPost_Automated", label = "Automated Relay" };
        var post = new WorldObject_ListeningPost { def = postDef, ID = 1003 };
        HarmonyLib.Traverse.Create(post).Field("tile").SetValue((PlanetTile)120);
        post.Kind = ListeningPostKind.AutomatedRelay;

        var objects = new List<WorldObject> { settlement, caravan, post };
        var options = WorldSelector_SelectFirstOrNextFrom_Patch.BuildEntitySelectionOptions(objects, (PlanetTile)120);

        // 3 entities + 1 ground tile option = 4 options
        Assert.IsNotNull(options);
        Assert.AreEqual(4, options.Count, "Must offer 3 entity selections plus 1 sector tile selection.");

        // Settlement option
        Assert.IsTrue(options.Any(o => o.Label.Contains("Player Colony")), "Must identify player settlement as Colony.");

        // Caravan option
        Assert.IsTrue(options.Any(o => o.Label.Contains("Caravan")), "Must identify caravan entity.");

        // Listening post option
        Assert.IsTrue(options.Any(o => o.Label.Contains("AutomatedRelay")), "Must identify listening post kind.");

        // Tile option
        Assert.IsTrue(options.Any(o => o.Label == "Select Tile 120"), "Must provide ground sector selection option.");
    }

    [TestMethod]
    public void BuildEntitySelectionOptions_DeduplicatesIdenticalEntities()
    {
        var settlement = new Settlement { ID = 1001 };
        HarmonyLib.Traverse.Create(settlement).Field("nameInt").SetValue("New Hope");
        var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
        HarmonyLib.Traverse.Create(caravan).Field("nameInt").SetValue("Alpha Team");

        // Duplicate list
        var duplicateList = new List<WorldObject> { settlement, caravan, settlement, caravan };
        var options = WorldSelector_SelectFirstOrNextFrom_Patch.BuildEntitySelectionOptions(duplicateList, (PlanetTile)200);

        Assert.AreEqual(3, options.Count, "Must deduplicate entities (2 unique entities + 1 tile option).");
    }

    [TestMethod]
    public void BuildEntitySelectionOptions_MaskedSite_ProtectsIntelligenceConcealment()
    {
        try
        {
            GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;
            SensoryManager.Instance = new SensoryManager(null!);

            var siteDef = new WorldObjectDef { defName = "OutpostSite", label = "Pirate Outpost" };
            var site = new Site { def = siteDef, ID = 1004 };
            HarmonyLib.Traverse.Create(site).Field("tile").SetValue((PlanetTile)350);

            // Site is detected by outer radar (explored), but un-scanned by identification ring
            SensoryManager.IsExploredOverride = t => t.tileId == 350;
            SensoryManager.IsIdentifiedOverride = _ => false;

            string label = WorldSelector_SelectFirstOrNextFrom_Patch.ResolveEntityLabel(site);
            Assert.AreEqual("Unidentified Contact", label, "Unidentified site must be masked as Unidentified Contact.");
        }
        finally
        {
            SensoryManager.IsExploredOverride = null;
            SensoryManager.IsIdentifiedOverride = null;
        }
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
        var playerFaction = new Faction { def = new FactionDef { isPlayer = true } };
        var map = (Map)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Map));
        var settlement = new Settlement { ID = 2001 };
        HarmonyLib.Traverse.Create(settlement).Field("tile").SetValue((PlanetTile)100);
        HarmonyLib.Traverse.Create(settlement).Field("factionInt").SetValue(playerFaction);
        HarmonyLib.Traverse.Create(settlement).Field("mapInt").SetValue(map);
        HarmonyLib.Traverse.Create(settlement).Field("nameInt").SetValue("Colony Alpha");

        var targetTile = (PlanetTile)115;
        var options = WorldSelector_HandleWorldClicks_Patch.BuildIntelligentContextualOptions(
            targetTile,
            Enumerable.Empty<Caravan>(),
            new List<Settlement> { settlement });

        Assert.IsNotNull(options);
        Assert.IsTrue(options.Count >= 1, "Must offer dispatch options from settlement.");

        // Phrasing: "Send caravan from [Settlement] to [Target] ([Dist] tiles away)"
        var sendOption = options.FirstOrDefault(o => o.Label.StartsWith("Send caravan from"));
        Assert.IsNotNull(sendOption, "Must offer intelligent option starting with 'Send caravan from'.");
        Assert.IsTrue(sendOption.Label.Contains("to Tile 115"), "Must target Tile 115.");
    }

    [TestMethod]
    public void BuildIntelligentContextualOptions_CaravanAndSettlement_IntelligentlyOrdersByDistance()
    {
        var playerFaction = new Faction { def = new FactionDef { isPlayer = true } };
        var map = (Map)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Map));

        // Settlement at Tile 100
        var settlement = new Settlement { ID = 2001 };
        HarmonyLib.Traverse.Create(settlement).Field("tile").SetValue((PlanetTile)100);
        HarmonyLib.Traverse.Create(settlement).Field("factionInt").SetValue(playerFaction);
        HarmonyLib.Traverse.Create(settlement).Field("mapInt").SetValue(map);
        HarmonyLib.Traverse.Create(settlement).Field("nameInt").SetValue("Colony Alpha");

        // Caravan at Tile 110
        var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
        HarmonyLib.Traverse.Create(caravan).Field("tile").SetValue((PlanetTile)110);
        HarmonyLib.Traverse.Create(caravan).Field("factionInt").SetValue(playerFaction);
        HarmonyLib.Traverse.Create(caravan).Field("nameInt").SetValue("Caravan One");

        // Target at Tile 112 (Caravan is closer: 2 tiles away; Settlement is 12 tiles away)
        var targetTile = (PlanetTile)112;

        var options = WorldSelector_HandleWorldClicks_Patch.BuildIntelligentContextualOptions(
            targetTile,
            new List<Caravan> { caravan },
            new List<Settlement> { settlement });

        Assert.IsNotNull(options);
        Assert.IsTrue(options.Count >= 2, "Must contain options for both caravan and settlement.");

        // First option must be for the closer caravan
        Assert.IsTrue(options[0].Label.StartsWith("Move caravan"),
            $"Closer caravan must appear before settlement. First option was: {options[0].Label}");

        // Subsequent option must be for the settlement
        Assert.IsTrue(options.Any(o => o.Label.StartsWith("Send caravan from")),
            "Must also contain 'Send caravan from' option for the settlement.");
    }

    [TestMethod]
    public void BuildIntelligentContextualOptions_UnexploredSector_SuppressesLeaksAndConstruction()
    {
        try
        {
            GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;
            SensoryManager.Instance = new SensoryManager(null!);
            SensoryManager.IsExploredOverride = t => t.tileId == 100; // Tile 600 is in the fog

            var playerFaction = new Faction { def = new FactionDef { isPlayer = true } };
            var map = (Map)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Map));

            var settlement = new Settlement { ID = 2001 };
            HarmonyLib.Traverse.Create(settlement).Field("tile").SetValue((PlanetTile)100);
            HarmonyLib.Traverse.Create(settlement).Field("factionInt").SetValue(playerFaction);
            HarmonyLib.Traverse.Create(settlement).Field("mapInt").SetValue(map);
            HarmonyLib.Traverse.Create(settlement).Field("nameInt").SetValue("Colony Alpha");

            var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
            HarmonyLib.Traverse.Create(caravan).Field("tile").SetValue((PlanetTile)100);
            HarmonyLib.Traverse.Create(caravan).Field("factionInt").SetValue(playerFaction);
            HarmonyLib.Traverse.Create(caravan).Field("nameInt").SetValue("Caravan One");

            var unexploredTile = (PlanetTile)600;

            var options = WorldSelector_HandleWorldClicks_Patch.BuildIntelligentContextualOptions(
                unexploredTile,
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
        finally
        {
            SensoryManager.IsExploredOverride = null;
        }
    }

    [TestMethod]
    public void BuildIntelligentContextualOptions_RightClickOwnColony_OffersFormCaravanLocally()
    {
        var playerFaction = new Faction { def = new FactionDef { isPlayer = true } };
        var map = (Map)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Map));

        var settlement = new Settlement { ID = 2001 };
        HarmonyLib.Traverse.Create(settlement).Field("tile").SetValue((PlanetTile)100);
        HarmonyLib.Traverse.Create(settlement).Field("factionInt").SetValue(playerFaction);
        HarmonyLib.Traverse.Create(settlement).Field("mapInt").SetValue(map);
        HarmonyLib.Traverse.Create(settlement).Field("nameInt").SetValue("Colony Alpha");

        var distantCaravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
        HarmonyLib.Traverse.Create(distantCaravan).Field("tile").SetValue((PlanetTile)140);
        HarmonyLib.Traverse.Create(distantCaravan).Field("factionInt").SetValue(playerFaction);
        HarmonyLib.Traverse.Create(distantCaravan).Field("nameInt").SetValue("Distant Expedition");

        // Right-clicking on own colony tile
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
        var playerFaction = new Faction { def = new FactionDef { isPlayer = true } };
        var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
        HarmonyLib.Traverse.Create(caravan).Field("tile").SetValue((PlanetTile)250);
        HarmonyLib.Traverse.Create(caravan).Field("factionInt").SetValue(playerFaction);
        HarmonyLib.Traverse.Create(caravan).Field("nameInt").SetValue("Surveyor Unit");

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
        var playerFaction = new Faction { def = new FactionDef { isPlayer = true } };

        var closeCaravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
        HarmonyLib.Traverse.Create(closeCaravan).Field("tile").SetValue((PlanetTile)102);
        HarmonyLib.Traverse.Create(closeCaravan).Field("factionInt").SetValue(playerFaction);
        HarmonyLib.Traverse.Create(closeCaravan).Field("nameInt").SetValue("Vanguard Recon");

        var farCaravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
        HarmonyLib.Traverse.Create(farCaravan).Field("tile").SetValue((PlanetTile)180);
        HarmonyLib.Traverse.Create(farCaravan).Field("factionInt").SetValue(playerFaction);
        HarmonyLib.Traverse.Create(farCaravan).Field("nameInt").SetValue("Rear Supply");

        var options = WorldSelector_HandleWorldClicks_Patch.BuildIntelligentContextualOptions(
            (PlanetTile)100,
            new List<Caravan> { farCaravan, closeCaravan }, // Intentionally passed in reverse order
            Enumerable.Empty<Settlement>());

        Assert.IsNotNull(options);
        Assert.IsTrue(options.Count >= 2);

        // First move option must be Vanguard Recon (closer)
        int vanguardIndex = options.FindIndex(o => o.Label.Contains("Vanguard Recon"));
        int rearIndex = options.FindIndex(o => o.Label.Contains("Rear Supply"));

        Assert.IsTrue(vanguardIndex < rearIndex,
            $"Closest caravan must precede distant caravan in intelligent suggestions. Vanguard index: {vanguardIndex}, Rear index: {rearIndex}");
    }

    [TestMethod]
    public void BuildEntitySelectionOptions_UnidentifiedContact_ConcealsFactionCrestAndColour()
    {
        try
        {
            GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;
            SensoryManager.Instance = new SensoryManager(null!);
            SensoryManager.IsExploredOverride = t => t.tileId == 350;
            SensoryManager.IsIdentifiedOverride = _ => false;

            var pirateFaction = new Faction
            {
                def = new FactionDef { isPlayer = false },
                color = Color.red
            };
            var siteDef = new WorldObjectDef { defName = "PirateFortress", label = "Secret Pirate Base" };
            var site = new Site { def = siteDef, ID = 9001 };
            HarmonyLib.Traverse.Create(site).Field("tile").SetValue((PlanetTile)350);
            HarmonyLib.Traverse.Create(site).Field("factionInt").SetValue(pirateFaction);

            var options = WorldSelector_SelectFirstOrNextFrom_Patch.BuildEntitySelectionOptions(
                new List<WorldObject> { site },
                (PlanetTile)350);

            Assert.IsNotNull(options);
            Assert.AreEqual(2, options.Count); // 1 site + 1 tile
            var siteOpt = options[0];
            Assert.AreEqual("Unidentified Contact", siteOpt.Label);
            Assert.AreNotEqual(Color.red, siteOpt.iconColor, "Must conceal pirate red faction colour for un-scanned contact.");
            Assert.AreEqual(WorldObject_Visibility_Patch.UnknownSiteIconColor, siteOpt.iconColor, "Must use mystery amber colour for un-scanned contact.");
        }
        finally
        {
            SensoryManager.IsExploredOverride = null;
            SensoryManager.IsIdentifiedOverride = null;
        }
    }

    [TestMethod]
    public void BuildIntelligentContextualOptions_UnidentifiedTargetSite_HidesRealSiteName()
    {
        try
        {
            GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;
            SensoryManager.Instance = new SensoryManager(null!);
            SensoryManager.IsExploredOverride = t => t.tileId == 400 || t.tileId == 100;
            SensoryManager.IsIdentifiedOverride = _ => false;

            var playerFaction = new Faction { def = new FactionDef { isPlayer = true } };
            var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
            HarmonyLib.Traverse.Create(caravan).Field("tile").SetValue((PlanetTile)100);
            HarmonyLib.Traverse.Create(caravan).Field("factionInt").SetValue(playerFaction);
            HarmonyLib.Traverse.Create(caravan).Field("nameInt").SetValue("Alpha Team");

            var enemyFaction = new Faction { def = new FactionDef { isPlayer = false } };
            var siteDef = new WorldObjectDef { defName = "PirateFortress", label = "Secret Pirate Base" };
            var site = new Site { def = siteDef, ID = 9002 };
            HarmonyLib.Traverse.Create(site).Field("tile").SetValue((PlanetTile)400);
            HarmonyLib.Traverse.Create(site).Field("factionInt").SetValue(enemyFaction);

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
        finally
        {
            SensoryManager.IsExploredOverride = null;
            SensoryManager.IsIdentifiedOverride = null;
        }
    }

    [TestMethod]
    public void BuildIntelligentContextualOptions_ImpassableTileNearColony_SuppressesDispatchToSelf()
    {
        var playerFaction = new Faction { def = new FactionDef { isPlayer = true } };
        var map = (Map)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Map));

        var settlement = new Settlement { ID = 3001 };
        HarmonyLib.Traverse.Create(settlement).Field("tile").SetValue((PlanetTile)100);
        HarmonyLib.Traverse.Create(settlement).Field("factionInt").SetValue(playerFaction);
        HarmonyLib.Traverse.Create(settlement).Field("mapInt").SetValue(map);
        HarmonyLib.Traverse.Create(settlement).Field("nameInt").SetValue("Coastal Base");

        // Target tile is 100 itself (or closest passable tile is 100)
        var options = WorldSelector_HandleWorldClicks_Patch.BuildIntelligentContextualOptions(
            (PlanetTile)100,
            Enumerable.Empty<Caravan>(),
            new List<Settlement> { settlement });

        Assert.IsNotNull(options);
        // Only "Form caravan at Coastal Base" should be offered, NOT "Send caravan from Coastal Base to Coastal Base"
        Assert.IsTrue(options.Any(o => o.Label == "Form caravan at Coastal Base"));
        Assert.IsFalse(options.Any(o => o.Label.StartsWith("Send caravan from") && o.Label.Contains("to Coastal Base")),
            "Colony must not offer sending caravan to its own tile.");
        Assert.IsFalse(options.Any(o => o.Label.StartsWith("Dispatch Construction Expedition")),
            "Colony must not offer deploying outposts on top of itself.");
    }

    [TestMethod]
    public void OpenCaravanFormingDialog_InvalidTargetTile_OpensWithoutThrowing()
    {
        var map = (Map)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Map));
        CaravanExpeditionUtility.OpenCaravanFormingDialog(map, PlanetTile.Invalid);
    }

    [TestMethod]
    public void SelectFirstOrNextFrom_ShiftHeld_ConfiguresAdditiveOptionAction()
    {
        var playerFaction = new Faction { def = new FactionDef { isPlayer = true } };
        var settlement = new Settlement { ID = 1001 };
        HarmonyLib.Traverse.Create(settlement).Field("tile").SetValue((PlanetTile)100);
        HarmonyLib.Traverse.Create(settlement).Field("factionInt").SetValue(playerFaction);
        HarmonyLib.Traverse.Create(settlement).Field("nameInt").SetValue("New Hope");

        var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
        HarmonyLib.Traverse.Create(caravan).Field("tile").SetValue((PlanetTile)100);
        HarmonyLib.Traverse.Create(caravan).Field("factionInt").SetValue(playerFaction);
        HarmonyLib.Traverse.Create(caravan).Field("nameInt").SetValue("Alpha Team");

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
        var playerFaction = new Faction { def = new FactionDef { isPlayer = true } };
        var settlement = new Settlement { ID = 1001 };
        HarmonyLib.Traverse.Create(settlement).Field("factionInt").SetValue(playerFaction);
        HarmonyLib.Traverse.Create(settlement).Field("nameInt").SetValue("New Hope");

        // When map is null (headless unit tests), safely formats label without throwing NullReferenceException
        string labelNoMap = WorldSelector_SelectFirstOrNextFrom_Patch.ResolveEntityLabel(settlement);
        Assert.AreEqual("New Hope (Player Colony)", labelNoMap);
    }

    [TestMethod]
    public void SelectUnderMouse_DisabledInSettings_ReturnsTrue()
    {
        GeoscapeMod.Settings.enableOverlappingEntitySelectionMenu = false;
        bool result = WorldSelector_SelectUnderMouse_Patch.Prefix(null!, true);
        Assert.IsTrue(result, "When disabled in settings, SelectUnderMouse prefix must allow vanilla execution.");
    }
}
