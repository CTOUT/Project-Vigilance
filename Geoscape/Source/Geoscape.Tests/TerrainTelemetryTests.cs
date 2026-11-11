using System;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape;
using ProjectVigilance.Geoscape.Patches;
using ProjectVigilance.Geoscape.Rules;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld.Planet;
using Verse;

namespace Geoscape.Tests;

[TestClass]
[DoNotParallelize]
public sealed class TerrainTelemetryTests
{
    private VisibilityMode originalMode;

    [TestInitialize]
    public void Init()
    {
        GeoscapeCampaignRules.ResetStaticDataForTesting();
        originalMode = GeoscapeMod.Settings.visibilityMode;
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;
        SensoryManager.Instance = new SensoryManager(null!);
        SensoryManager.IsExploredOverride = null;
        SensoryManager.IsIdentifiedOverride = null;
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
    public void GetTileIntelLevel_ClassifiesThreeTiersUnderHazeOfWar()
    {
        Assert.AreEqual(
            TileIntelLevel.Unexplored,
            TerrainTelemetryUtility.GetTileIntelLevel(VisibilityMode.ClassicHazeOfWar, isExplored: false, isIdentified: false));

        Assert.AreEqual(
            TileIntelLevel.DetectedOnly,
            TerrainTelemetryUtility.GetTileIntelLevel(VisibilityMode.ClassicHazeOfWar, isExplored: true, isIdentified: false));

        Assert.AreEqual(
            TileIntelLevel.Identified,
            TerrainTelemetryUtility.GetTileIntelLevel(VisibilityMode.ClassicHazeOfWar, isExplored: true, isIdentified: true));

        Assert.AreEqual(
            TileIntelLevel.Identified,
            TerrainTelemetryUtility.GetTileIntelLevel(VisibilityMode.PlanetarySurveillance, isExplored: false, isIdentified: false));

        Assert.AreEqual(
            TileIntelLevel.Identified,
            TerrainTelemetryUtility.GetTileIntelLevel(VisibilityMode.Omniscient, isExplored: false, isIdentified: false));
    }

    [TestMethod]
    public void RoundTemperatureToNearestStep_NormalisesAndRoundsCleanly()
    {
        Assert.AreEqual(5f, TerrainTelemetryUtility.RoundTemperatureToNearestStep(6.6f, 5));
        Assert.AreEqual(10f, TerrainTelemetryUtility.RoundTemperatureToNearestStep(7.6f, 5));
        Assert.AreEqual(-15f, TerrainTelemetryUtility.RoundTemperatureToNearestStep(-14.2f, 5));
        Assert.AreEqual(25f, TerrainTelemetryUtility.RoundTemperatureToNearestStep(27f, 5));

        float zero = TerrainTelemetryUtility.RoundTemperatureToNearestStep(-0.4f, 5);
        Assert.AreEqual(0f, zero);
        Assert.IsFalse(float.IsNegative(zero), "Negative zero must normalise to positive zero.");
    }

    [TestMethod]
    public void FormatEstimatedTemperatureFromCelsius_FormatsAcrossDisplayModes()
    {
        Assert.AreEqual(
            "~5C",
            TerrainTelemetryUtility.FormatEstimatedTemperatureFromCelsius(6.6f, TemperatureDisplayMode.Celsius));

        // 6.6C = 43.88F -> rounded to nearest 5 = ~45F
        Assert.AreEqual(
            "~45F",
            TerrainTelemetryUtility.FormatEstimatedTemperatureFromCelsius(6.6f, TemperatureDisplayMode.Fahrenheit));

        // 6.6C = 279.75K -> rounded to nearest 5 = ~280K
        Assert.AreEqual(
            "~280K",
            TerrainTelemetryUtility.FormatEstimatedTemperatureFromCelsius(6.6f, TemperatureDisplayMode.Kelvin));
    }

    [TestMethod]
    public void TelemetryPatches_ResolveAllRimWorldTargetsWithoutError()
    {
        string id = $"Geoscape.Tests.Telemetry.{Guid.NewGuid():N}";
        Harmony harmony = new(id);
        try
        {
            Type[] patchTypes =
            {
                typeof(WorldObject_Visibility_Patch.Landmarks_DrawStandard_Patch),
                typeof(WorldObject_Visibility_Patch.LandmarksToShow_Patch),
                typeof(WorldObject_Visibility_Patch.ExpandableLandmarksUtility_ExpandableLandmarksOnGUI_Patch),
                typeof(WorldObject_Visibility_Patch.UnknownSite_Label_Patch),
                typeof(WorldObject_Visibility_Patch.WorldInspectPane_GetLabel_Patch),
                typeof(WorldObject_Visibility_Patch.WorldInspectPane_TileInspectString_Patch),
                typeof(WorldObject_Visibility_Patch.WITab_Terrain_IsVisible_Patch),
                typeof(WorldObject_Visibility_Patch.WITab_Terrain_FillTab_Patch),
                typeof(WorldObject_Visibility_Patch.CellInspectorDrawer_DrawWorldInspector_Patch)
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
}
