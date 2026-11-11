using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape;
using ProjectVigilance.Geoscape.Sensory;
using UnityEngine;

namespace Geoscape.Tests;

[TestClass]
public sealed class RadarCoverageAndColorSettingsTests
{
    [TestMethod]
    public void RadarSettings_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var settings = new GeoscapeSettings();

        // Assert
        Assert.AreEqual(0.50f, settings.radarOutlineOpacity, 0.001f, "Perimeter outline opacity must default to 50%.");
        Assert.AreEqual(0.65f, settings.identificationRingOpacity, 0.001f, "Identification ring opacity must default to 65%.");
        Assert.AreEqual(0.40f, settings.honeycombGridOpacity, 0.001f, "Honeycomb grid opacity must default to 40%.");
        Assert.IsFalse(settings.useCustomIdentificationColor, "Custom identification color must default to disabled.");
        Assert.AreEqual(new Color(1.0f, 0.82f, 0.15f), settings.radarIdentificationColor, "Default custom identification color must be telemetry amber/gold.");
    }

    [TestMethod]
    public void RadarSettings_ResetToDefaults_RestoresNewSensoryOptions()
    {
        // Arrange
        var settings = new GeoscapeSettings
        {
            radarOutlineOpacity = 0.99f,
            identificationRingOpacity = 0.20f,
            honeycombGridOpacity = 0.85f,
            useCustomIdentificationColor = true,
            radarIdentificationColor = Color.magenta
        };

        // Act
        settings.ResetToDefaults();

        // Assert
        Assert.AreEqual(0.50f, settings.radarOutlineOpacity, 0.001f);
        Assert.AreEqual(0.65f, settings.identificationRingOpacity, 0.001f);
        Assert.AreEqual(0.40f, settings.honeycombGridOpacity, 0.001f);
        Assert.IsFalse(settings.useCustomIdentificationColor);
        Assert.AreEqual(new Color(1.0f, 0.82f, 0.15f), settings.radarIdentificationColor);
    }

    [TestMethod]
    public void EffectiveIdentificationColor_AutomaticContrast_WhenCustomDisabled()
    {
        // Arrange
        var settings = new GeoscapeSettings
        {
            usePlayerFactionColor = false,
            useCustomIdentificationColor = false
        };

        // Act 1: Tactical Cyan base (cool hue ~0.50) -> Expect amber/gold hue (~0.11)
        settings.radarCustomColor = new Color(0f, 0.90f, 1f);
        Color cyanIdColor = settings.EffectiveIdentificationColor;
        Color.RGBToHSV(cyanIdColor, out float cyanH, out float _, out float _);
        Assert.AreEqual(0.11f, cyanH, 0.02f, "Cyan base must produce contrasting amber/gold identification hue (~0.11).");

        // Act 2: Amber base (warm hue ~0.11) -> Expect electric cyan hue (~0.52)
        settings.radarCustomColor = new Color(1f, 0.76f, 0.16f);
        Color amberIdColor = settings.EffectiveIdentificationColor;
        Color.RGBToHSV(amberIdColor, out float amberH, out float _, out float _);
        Assert.AreEqual(0.52f, amberH, 0.02f, "Amber base must produce contrasting electric cyan identification hue (~0.52).");

        // Act 3: Crimson base (warm hue ~0.98) -> Expect electric cyan hue (~0.52)
        settings.radarCustomColor = new Color(1f, 0.18f, 0.29f);
        Color crimsonIdColor = settings.EffectiveIdentificationColor;
        Color.RGBToHSV(crimsonIdColor, out float crimsonH, out float _, out float _);
        Assert.AreEqual(0.52f, crimsonH, 0.02f, "Crimson base must produce contrasting electric cyan identification hue (~0.52).");
    }

    [TestMethod]
    public void EffectiveIdentificationColor_ReturnsCustomColor_WhenCustomEnabled()
    {
        // Arrange
        Color customGold = new(1f, 0.82f, 0.15f);
        var settings = new GeoscapeSettings
        {
            usePlayerFactionColor = false,
            radarCustomColor = new Color(0f, 0.90f, 1f),
            useCustomIdentificationColor = true,
            radarIdentificationColor = customGold
        };

        // Act
        Color result = settings.EffectiveIdentificationColor;

        // Assert
        Assert.AreEqual(customGold, result, "EffectiveIdentificationColor must return custom color when useCustomIdentificationColor is true.");
    }

    [TestMethod]
    public void AutomaticIdentificationColor_CoolAndWarmBases_ProducesContrastingHues()
    {
        // Cool base: Emerald (h ~ 0.36)
        Color emerald = new(0.08f, 0.94f, 0.43f);
        Color emeraldResult = WorldDrawLayer_RadarCoverage.GetAutomaticIdentificationColor(emerald);
        Color.RGBToHSV(emeraldResult, out float emH, out _, out _);
        Assert.AreEqual(0.11f, emH, 0.02f, "Emerald base must shift to amber/gold.");

        // Warm base: Pure Red (h = 0.0)
        Color red = Color.red;
        Color redResult = WorldDrawLayer_RadarCoverage.GetAutomaticIdentificationColor(red);
        Color.RGBToHSV(redResult, out float redH, out _, out _);
        Assert.AreEqual(0.52f, redH, 0.02f, "Red base must shift to tactical cyan.");

        // Compatibility alias Check
        Color32 aliasResult = WorldDrawLayer_RadarCoverage.GetIdentificationHorizonColor(emerald);
        Assert.AreEqual((Color32)emeraldResult, aliasResult, "GetIdentificationHorizonColor alias must match GetAutomaticIdentificationColor.");
    }

    [TestMethod]
    public void RadarSettings_TabEnum_CoversExpectedTabs()
    {
        string[] names = System.Enum.GetNames(typeof(GeoscapeSettingsTab));
        CollectionAssert.AreEqual(
            new[] { "Sensory", "Radar", "Incursions", "Geoscape", "Diagnostics" },
            names,
            "GeoscapeSettingsTab must define all 5 expected navigation tabs.");
    }
}
