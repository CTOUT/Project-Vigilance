using System;
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
public sealed class LandmarkSensoryTests
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
    public void IsManMadeLandmarkDef_DistinguishesStructuresFromGeography()
    {
        // Man-made and anomalous structures must return true
        string[] manMade = new[]
        {
            "Ruins",
            "FrozenRuins",
            "AncientLaunchSite",
            "AncientGarrison",
            "AncientWarehouse",
            "AncientChemfuelRefinery",
            "AncientInfestedSettlement",
            "AbandonedColonyTribal",
            "AbandonedColonyOutlander",
            "AncientSmokeVent",
            "AncientToxVent",
            "AncientHeatVent",
            "AncientQuarry",
            "Harbor",
            "TerraformingScar"
        };

        foreach (string defName in manMade)
        {
            Assert.IsTrue(
                LandmarkSensoryUtility.IsManMadeLandmarkDef(defName),
                $"Expected '{defName}' to be identified as a man-made landmark.");
        }

        // Mundane geographical and weather features must return false
        string[] geography = new[]
        {
            "Lake",
            "Pond",
            "Oasis",
            "DryLake",
            "HotSprings",
            "Archipelago",
            "Bay",
            "Fjord",
            "Iceberg",
            "Valley",
            "Cavern",
            "Chasm",
            "Plateau",
            "Cliffs",
            "Dunes"
        };

        foreach (string defName in geography)
        {
            Assert.IsFalse(
                LandmarkSensoryUtility.IsManMadeLandmarkDef(defName),
                $"Expected '{defName}' to NOT be classified as a man-made landmark.");
        }
    }

    [TestMethod]
    public void IsAnomalousStructure_IdentifiesKnownDefNames()
    {
        EnsureEnvironmentInitialised();

        WorldObjectDef monolithDef = new() { defName = "FallenMonolith" };
        WorldObjectDef ancientComplexDef = new() { defName = "AncientComplex" };
        WorldObjectDef ruinsDef = new() { defName = "AncientRuins" };
        WorldObjectDef normalSettlementDef = new() { defName = "Settlement" };

        WorldObject monolithObj = new() { def = monolithDef };
        WorldObject complexObj = new() { def = ancientComplexDef };
        WorldObject ruinsObj = new() { def = ruinsDef };
        WorldObject normalObj = new() { def = normalSettlementDef };

        Assert.IsTrue(LandmarkSensoryUtility.IsAnomalousStructure(monolithObj));
        Assert.IsTrue(LandmarkSensoryUtility.IsAnomalousStructure(complexObj));
        Assert.IsTrue(LandmarkSensoryUtility.IsAnomalousStructure(ruinsObj));
        Assert.IsFalse(LandmarkSensoryUtility.IsAnomalousStructure(normalObj));
        Assert.IsFalse(LandmarkSensoryUtility.IsAnomalousStructure(null));
    }

    [TestMethod]
    public void ProceduralExclamationMark_RasterisesWithCorrectGeometry()
    {
        Texture2D tex = LandmarkSensoryUtility.CreateProceduralExclamationMarkTexture();
        Assert.IsNotNull(tex);
        Assert.AreEqual(64, tex.width);
        Assert.AreEqual(64, tex.height);

        // Center stem pixel (31, 40) should be solid white
        Color stemColor = tex.GetPixel(31, 40);
        Assert.AreEqual(1f, stemColor.r, 0.05f);
        Assert.AreEqual(1f, stemColor.g, 0.05f);
        Assert.AreEqual(1f, stemColor.b, 0.05f);
        Assert.IsTrue(stemColor.a > 0.8f);

        // Center dot pixel (31, 11) should be solid white
        Color dotColor = tex.GetPixel(31, 11);
        Assert.AreEqual(1f, dotColor.r, 0.05f);
        Assert.AreEqual(1f, dotColor.g, 0.05f);
        Assert.AreEqual(1f, dotColor.b, 0.05f);
        Assert.IsTrue(dotColor.a > 0.8f);

        // Corner pixel (0, 0) should be transparent
        Color cornerColor = tex.GetPixel(0, 0);
        Assert.AreEqual(0f, cornerColor.a, 0.01f);
    }

    [TestMethod]
    public void SensoryColours_TacticalCyanDistinctFromAmber()
    {
        Color cyan = LandmarkSensoryUtility.TacticalCyan;
        Color amber = WorldObject_Visibility_Patch.UnknownSiteIconColor;

        // Tactical cyan is predominantly cyan/blue (high blue, low red)
        Assert.IsTrue(cyan.b > 0.9f);
        Assert.IsTrue(cyan.r < 0.5f);

        // Amber threat indicator is predominantly warm/red (high red, low blue)
        Assert.IsTrue(amber.r > 0.9f);
        Assert.IsTrue(amber.b < 0.3f);
    }
}
