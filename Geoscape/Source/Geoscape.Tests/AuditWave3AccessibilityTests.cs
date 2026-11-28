using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Core.Localization;
using ProjectVigilance.Core.UI;
using ProjectVigilance.Geoscape;
using ProjectVigilance.Geoscape.Sensory;
using ProjectVigilance.Geoscape.Settings;
using UnityEngine;
using Verse;

namespace Geoscape.Tests;

[TestClass]
[DoNotParallelize]
public sealed class AuditWave3AccessibilityTests : GeoscapeTestBase
{
    private static readonly Color DarkPanelBg = new(33f / 255f, 37f / 255f, 43f / 255f);

    [TestMethod]
    public void SensoryThemeUtility_AllThemesAndSwatches_MeetWcagAaAgainstDarkPanel()
    {
        double bgLum = RelativeLuminance(DarkPanelBg);

        foreach (SensoryTheme theme in Enum.GetValues(typeof(SensoryTheme)))
        {
            var colors = SensoryThemeUtility.GetThemeColors(theme);
            AssertMeetsWcagAa(theme, "radar", colors.radar, bgLum);
            AssertMeetsWcagAa(theme, "identification", colors.identification, bgLum);
            AssertMeetsWcagAa(theme, "scout", colors.scout, bgLum);
            AssertMeetsWcagAa(theme, "sonar", colors.sonar, bgLum);
        }
    }

    [TestMethod]
    public void GeoscapeSettings_ApplySensoryTheme_DisablesFactionColorForCvdAndHighContrast()
    {
        var settings = new GeoscapeSettings { usePlayerFactionColor = true };

        settings.ApplySensoryTheme(SensoryTheme.TacticalCyan);
        Assert.IsTrue(settings.usePlayerFactionColor, "Standard TacticalCyan preset should preserve player faction colour preference.");

        settings.ApplySensoryTheme(SensoryTheme.ProtanopiaDeuteranopia);
        Assert.IsFalse(settings.usePlayerFactionColor, "Protanopia/Deuteranopia preset must disable player faction colour override.");

        settings.usePlayerFactionColor = true;
        settings.ApplySensoryTheme(SensoryTheme.Tritanopia);
        Assert.IsFalse(settings.usePlayerFactionColor, "Tritanopia preset must disable player faction colour override.");

        settings.usePlayerFactionColor = true;
        settings.ApplySensoryTheme(SensoryTheme.HighContrast);
        Assert.IsFalse(settings.usePlayerFactionColor, "HighContrast preset must disable player faction colour override.");
    }

    [TestMethod]
    public void WorldDrawLayerRadarCoverage_SonarAndVisualPerimeters_TrackActiveSensoryTheme()
    {
        var prev = GeoscapeMod.Settings;
        try
        {
            var settings = new GeoscapeSettings();
            GeoscapeMod.Settings = settings;

            settings.ApplySensoryTheme(SensoryTheme.HighContrast);
            var expected = SensoryThemeUtility.GetThemeColors(SensoryTheme.HighContrast);

            Color32 sonar = WorldDrawLayer_RadarCoverage.SonarPerimeterColorRgb;
            Color32 visual = WorldDrawLayer_RadarCoverage.VisualPerimeterColorRgb;

            Color32 expectedSonar = expected.sonar;
            Color32 expectedRadar = expected.radar;

            Assert.AreEqual(expectedSonar.r, sonar.r);
            Assert.AreEqual(expectedSonar.g, sonar.g);
            Assert.AreEqual(expectedSonar.b, sonar.b);

            Assert.AreEqual(expectedRadar.r, visual.r);
            Assert.AreEqual(expectedRadar.g, visual.g);
            Assert.AreEqual(expectedRadar.b, visual.b);
        }
        finally
        {
            GeoscapeMod.Settings = prev;
        }
    }

    [TestMethod]
    public void VigilanceLoc_And_ImguiStateScope_BehaveCleanlyInHeadlessMode()
    {
        string result = VigilanceLoc.Tr("PV_NonExistent_Audit_Key_12345", "Fallback Label");
        Assert.AreEqual("Fallback Label", result);

        GUI.color = Color.white;
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
        Text.WordWrap = true;

        using (new VigilanceUiWidgets.ImguiStateScope())
        {
            GUI.color = Color.red;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.WordWrap = false;
        }

        Assert.AreEqual(Color.white, GUI.color);
        Assert.AreEqual(GameFont.Small, Text.Font);
        Assert.AreEqual(TextAnchor.UpperLeft, Text.Anchor);
        Assert.IsTrue(Text.WordWrap);
    }

    private sealed class StubModuleTab : ISettingsTabProvider
    {
        public string TabKey => "Thermal";
        public string TabLabelKey => "PV_Settings_Tab_Thermal";
        public string FallbackTabLabel => "Thermal";
        public int SortOrder => 60;
        public void DoTabContents(Listing_Standard listing, Rect inRect) { }
    }

    [TestMethod]
    public void VigilanceSettingsRegistry_DiagnosticsTab_AlwaysSortsAfterModuleTabs()
    {
        try
        {
            var settings = new GeoscapeSettings();
            settings.EnsureBuiltinTabsRegistered();
            VigilanceSettingsRegistry.RegisterTab(new StubModuleTab());

            var tabs = VigilanceSettingsRegistry.RegisteredTabs;
            Assert.IsTrue(tabs.Count >= 2);
            Assert.AreEqual("Diagnostics", tabs[tabs.Count - 1].TabKey, "Diagnostics tab must always be the last tab.");
        }
        finally
        {
            VigilanceSettingsRegistry.UnregisterTab("Thermal");
        }
    }

    private static void AssertMeetsWcagAa(SensoryTheme theme, string role, Color color, double bgLum)
    {
        double fgLum = RelativeLuminance(color);
        double lighter = Math.Max(fgLum, bgLum);
        double darker = Math.Min(fgLum, bgLum);
        double ratio = (lighter + 0.05) / (darker + 0.05);
        Assert.IsTrue(ratio >= 4.5, $"{theme}.{role} contrast ratio {ratio:F2}:1 is below WCAG AA (4.5:1) against #21252B.");
    }

    private static double RelativeLuminance(Color c)
    {
        return 0.2126 * LinearizeChannel(c.r) +
               0.7152 * LinearizeChannel(c.g) +
               0.0722 * LinearizeChannel(c.b);
    }

    private static double LinearizeChannel(double c)
    {
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
