using Microsoft.VisualStudio.TestTools.UnitTesting;
using RimWorld;
using RimWorld.Planet;
using Verse;
using ProjectVigilance.Geoscape.Sensory;
using ProjectVigilance.Geoscape.Patches;

namespace Geoscape.Tests;

[TestClass]
[DoNotParallelize]
public sealed class CaravanHoldPositionTests
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
            LanguageDatabase.activeLanguage = new LoadedLanguage("English");
        }
    }

    [TestMethod]
    public void CaravanHoldPosition_LabelAndReportString_FormatCorrectly()
    {
        EnsureEnvironmentInitialised();

        PlanetTile testTile = new(42100);
        var actionWithLabel = new CaravanArrivalAction_HoldPosition(testTile, "New Hope Settlement");
        var actionNoLabel = new CaravanArrivalAction_HoldPosition(testTile);

        Assert.AreEqual("Hold position outside New Hope Settlement", actionWithLabel.Label);
        Assert.AreEqual("travelling to hold position outside New Hope Settlement", actionWithLabel.ReportString);
        Assert.AreEqual(testTile, actionWithLabel.TargetTile);
        Assert.AreEqual("New Hope Settlement", actionWithLabel.LocationLabel);

        Assert.AreEqual("Hold position at Tile 42100", actionNoLabel.Label);
        Assert.AreEqual("travelling to Tile 42100", actionNoLabel.ReportString);
        Assert.AreEqual(testTile, actionNoLabel.TargetTile);
        Assert.IsNull(actionNoLabel.LocationLabel);
    }

    [TestMethod]
    public void CaravanHoldPosition_StillValid_ValidatesCaravanState()
    {
        EnsureEnvironmentInitialised();

        PlanetTile testTile = new(51000);
        var action = new CaravanArrivalAction_HoldPosition(testTile, "Forward Outpost");

        // Null caravan is invalid
        Assert.IsFalse(action.StillValid(null!, testTile).Accepted);
    }

    [TestMethod]
    public void CaravanHoldPosition_CanHoldPosition_ValidatesCaravanAndTile()
    {
        EnsureEnvironmentInitialised();

        PlanetTile validTile = new(51000);
        PlanetTile invalidTile = PlanetTile.Invalid;

        // 1. Null caravan is rejected
        Assert.IsFalse(CaravanArrivalAction_HoldPosition.CanHoldPosition(null!, validTile).Accepted);

        // 2. Caravan with invalid tile is rejected
        var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
        Assert.IsFalse(CaravanArrivalAction_HoldPosition.CanHoldPosition(caravan, invalidTile).Accepted);

        // 3. Caravan with valid tile is accepted
        Assert.IsTrue(CaravanArrivalAction_HoldPosition.CanHoldPosition(caravan, validTile).Accepted);
    }

    [TestMethod]
    public void FloatMenuMakerWorld_HoldPositionOption_ConstructedWithRevalidationTargetAndNonDisabled()
    {
        EnsureEnvironmentInitialised();

        try
        {
            var playerFaction = new Faction { def = new FactionDef { isPlayer = true } };

            var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
            HarmonyLib.Traverse.Create(caravan).Field("tileInt").SetValue((PlanetTile)100);
            HarmonyLib.Traverse.Create(caravan).Field("factionInt").SetValue(playerFaction);

            var settlementDef = new WorldObjectDef { label = "Colony" };
            var settlement = new Settlement { ID = 9920, def = settlementDef };
            HarmonyLib.Traverse.Create(settlement).Field("tileInt").SetValue((PlanetTile)200);
            HarmonyLib.Traverse.Create(settlement).Field("factionInt").SetValue(playerFaction);

            ProjectVigilance.Geoscape.Patches.FloatMenuMakerWorld_ChoicesAtFor_Patch.SettlementAtOverride = t => t.tileId == 200 ? settlement : null;
            ProjectVigilance.Geoscape.Patches.FloatMenuMakerWorld_ChoicesAtFor_Patch.MouseTileOverride = (PlanetTile)200;

            System.Collections.Generic.List<FloatMenuOption> choices = new();
            ProjectVigilance.Geoscape.Patches.FloatMenuMakerWorld_ChoicesAtFor_Patch.Postfix(UnityEngine.Vector2.zero, caravan, ref choices);

            Assert.IsNotNull(choices);
            var holdOption = choices.FirstOrDefault(c => c.Label.Contains("Hold position outside"));
            Assert.IsNotNull(holdOption, "Menu must contain Hold position outside option.");
            Assert.IsFalse(holdOption.Disabled, "Hold position option must NOT be disabled.");
            Assert.AreEqual(settlement, holdOption.revalidateWorldClickTarget, "Option must set revalidateWorldClickTarget to prevent FloatMenuWorld desync.");
        }
        finally
        {
            ProjectVigilance.Geoscape.Patches.FloatMenuMakerWorld_ChoicesAtFor_Patch.SettlementAtOverride = null;
            ProjectVigilance.Geoscape.Patches.FloatMenuMakerWorld_ChoicesAtFor_Patch.MouseTileOverride = null;
        }
    }

    [TestMethod]
    public void ListeningPost_HasPlayerCaravanPresent_PausesWhileMoving()
    {
        EnsureEnvironmentInitialised();

        PlanetTile tile = new(45000);
        var post = new WorldObject_ListeningPost
        {
            Kind = ListeningPostKind.AutomatedRelay
        };
        HarmonyLib.Traverse.Create(post).Field("tile").SetValue(tile);
        post.StartConstruction(2500);

        var playerFaction = new Faction { def = new FactionDef { isPlayer = true } };
        var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
        HarmonyLib.Traverse.Create(caravan).Field("tile").SetValue(tile);
        HarmonyLib.Traverse.Create(caravan).Field("factionInt").SetValue(playerFaction);
        HarmonyLib.Traverse.Create(caravan).Field("spawnedInt").SetValue(true);

        var pather = (Caravan_PathFollower)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan_PathFollower));
        HarmonyLib.Traverse.Create(caravan).Field("pather").SetValue(pather);
        HarmonyLib.Traverse.Create(pather).Field("caravan").SetValue(caravan);

        WorldObject_ListeningPost.PlayerCaravanPresenceOverride = p =>
        {
            bool isMoving = false;
            try
            {
                isMoving = pather.Moving;
            }
            catch
            {
                isMoving = HarmonyLib.Traverse.Create(pather).Field("moving").GetValue<bool>();
            }

            if (isMoving) return null;
            return caravan;
        };

        try
        {
            // 1. Caravan is stationary: present and building
            HarmonyLib.Traverse.Create(pather).Field("moving").SetValue(false);
            Assert.IsTrue(post.HasPlayerCaravanPresent());

            int ticksBefore = post.ConstructionTicksLeft;
            post.TickForTesting();
            Assert.AreEqual(ticksBefore - 1, post.ConstructionTicksLeft, "Stationary caravan must advance construction.");

            // 2. Caravan begins marching: presence is false and construction paused
            HarmonyLib.Traverse.Create(pather).Field("moving").SetValue(true);
            Assert.IsFalse(post.HasPlayerCaravanPresent(), "Moving caravan must NOT be considered present for construction.");

            int ticksPaused = post.ConstructionTicksLeft;
            post.TickForTesting();
            Assert.AreEqual(ticksPaused, post.ConstructionTicksLeft, "Moving caravan must pause construction progress.");
        }
        finally
        {
            WorldObject_ListeningPost.PlayerCaravanPresenceOverride = null;
        }
    }

    [TestMethod]
    public void ListeningPost_ConstructionProgressPct_CalculatesAccurately()
    {
        EnsureEnvironmentInitialised();

        var marbleDef = new ThingDef { defName = "BlocksMarble", label = "marble blocks" };
        var beacon = new WorldObject_ListeningPost
        {
            Kind = ListeningPostKind.SignalBeacon,
            stuffDef = marbleDef
        };
        HarmonyLib.Traverse.Create(beacon).Field("tile").SetValue(new PlanetTile(33000));
        beacon.StartConstruction(2500);

        Assert.AreEqual(marbleDef, beacon.stuffDef, "Invested material must match consumed stone type.");
        Assert.IsTrue(beacon.IsConstructing);
        Assert.AreEqual(0, beacon.ConstructionProgressPct);

        // Advance 50%
        HarmonyLib.Traverse.Create(beacon).Field("constructionTicksLeft").SetValue(1250);
        Assert.AreEqual(50, beacon.ConstructionProgressPct);

        // Advance 90%
        HarmonyLib.Traverse.Create(beacon).Field("constructionTicksLeft").SetValue(250);
        Assert.AreEqual(90, beacon.ConstructionProgressPct);
    }

    [TestMethod]
    public void CaravanDeparture_ActiveConstructionTile_TriggersDepartureInterception()
    {
        EnsureEnvironmentInitialised();

        PlanetTile originTile = new(40000);
        PlanetTile destTile = new(40001);

        var playerFaction = new Faction { def = new FactionDef { isPlayer = true } };
        var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
        HarmonyLib.Traverse.Create(caravan).Field("tile").SetValue(originTile);
        HarmonyLib.Traverse.Create(caravan).Field("factionInt").SetValue(playerFaction);

        var pather = (Caravan_PathFollower)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan_PathFollower));
        HarmonyLib.Traverse.Create(caravan).Field("pather").SetValue(pather);
        HarmonyLib.Traverse.Create(pather).Field("caravan").SetValue(caravan);
        HarmonyLib.Traverse.Create(pather).Field("moving").SetValue(false);

        var post = new WorldObject_ListeningPost
        {
            Kind = ListeningPostKind.AutomatedRelay
        };
        HarmonyLib.Traverse.Create(post).Field("tile").SetValue(originTile);
        post.StartConstruction(2500);

        bool promptIntercepted = false;
        Caravan_PathFollower_StartPath_Patch.ActivePostFinderOverride = t => (t == originTile) ? post : null;
        Caravan_PathFollower_StartPath_Patch.DepartureDialogOverride = (c, p, d) =>
        {
            promptIntercepted = true;
            Assert.AreEqual(caravan, c);
            Assert.AreEqual(destTile, d);
        };

        try
        {
            bool prefixResult = true;
            // Test 1: Moving away triggers departure prompt when active outpost exists
            bool allowNormalStart = Caravan_PathFollower_StartPath_Patch.Prefix(
                pather, caravan, destTile, null!, false, true, ref prefixResult);
            Assert.IsFalse(allowNormalStart, "Departure from active construction tile must be intercepted.");
            Assert.IsTrue(promptIntercepted, "Departure prompt callback must be triggered.");
            Assert.IsFalse(prefixResult);

            // Test 2: Suppressed prompt allows normal StartPath execution
            promptIntercepted = false;
            Caravan_PathFollower_StartPath_Patch.SuppressDeparturePrompt = true;
            allowNormalStart = Caravan_PathFollower_StartPath_Patch.Prefix(
                pather, caravan, destTile, null!, false, true, ref prefixResult);
            Assert.IsTrue(allowNormalStart, "Suppressed prompt must allow normal StartPath execution.");
            Assert.IsFalse(promptIntercepted);

            // Test 3: Same tile proceeds without prompt
            Caravan_PathFollower_StartPath_Patch.SuppressDeparturePrompt = false;
            allowNormalStart = Caravan_PathFollower_StartPath_Patch.Prefix(
                pather, caravan, originTile, null!, false, true, ref prefixResult);
            Assert.IsTrue(allowNormalStart, "Re-targeting same tile must proceed without prompt.");
            Assert.IsFalse(promptIntercepted);
        }
        finally
        {
            Caravan_PathFollower_StartPath_Patch.ActivePostFinderOverride = null;
            Caravan_PathFollower_StartPath_Patch.DepartureDialogOverride = null;
            Caravan_PathFollower_StartPath_Patch.SuppressDeparturePrompt = false;
        }
    }

    [TestMethod]
    public void CaravanHoldPosition_DefaultConstructor_InstantiatesSuccessfully()
    {
        var action = new CaravanArrivalAction_HoldPosition();
        Assert.IsNotNull(action);
        Assert.IsNull(action.LocationLabel);
    }
}
