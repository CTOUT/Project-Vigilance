using Microsoft.VisualStudio.TestTools.UnitTesting;
using RimWorld;
using RimWorld.Planet;
using Verse;
using ProjectVigilance.Geoscape.Sensory;

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
    public void CaravanHoldPosition_DefaultConstructor_InstantiatesSuccessfully()
    {
        var action = new CaravanArrivalAction_HoldPosition();
        Assert.IsNotNull(action);
        Assert.IsNull(action.LocationLabel);
    }
}
