using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape.Sensory;
using ProjectVigilance.Geoscape.Settings;
using ProjectVigilance.Geoscape.Thermal;
using ProjectVigilance.Geoscape.World;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Geoscape.Tests;

[TestClass]
[DoNotParallelize]
public class FoundationMilestone15Tests : GeoscapeTestBase
{
    [TestInitialize]
    public override void Setup()
    {
        base.Setup();
        VigilanceSettingsRegistry.Clear();
    }

    [TestCleanup]
    public override void Cleanup()
    {
        VigilanceSettingsRegistry.Clear();
        base.Cleanup();
    }

    // =========================================================================
    // Stream 1: Unified Dynamic Options Interface (ISettingsTabProvider)
    // =========================================================================

    private class MockTabProvider : ISettingsTabProvider
    {
        public string TabKey { get; }
        public string TabLabelKey { get; }
        public string FallbackTabLabel { get; }
        public int SortOrder { get; }
        public bool DidRender { get; private set; }

        public MockTabProvider(string key, string labelKey, string fallback, int order)
        {
            TabKey = key;
            TabLabelKey = labelKey;
            FallbackTabLabel = fallback;
            SortOrder = order;
        }

        public void DoTabContents(Listing_Standard listing, Rect inRect)
        {
            DidRender = true;
        }
    }

    [TestMethod]
    public void SettingsRegistry_RegistersAndOrdersTabs()
    {
        var tabA = new MockTabProvider("TabA", "PV_TabA", "Tab A", 30);
        var tabB = new MockTabProvider("TabB", "PV_TabB", "Tab B", 10);
        var tabC = new MockTabProvider("TabC", "PV_TabC", "Tab C", 20);

        VigilanceSettingsRegistry.RegisterTab(tabA);
        VigilanceSettingsRegistry.RegisterTab(tabB);
        VigilanceSettingsRegistry.RegisterTab(tabC);

        var ordered = VigilanceSettingsRegistry.RegisteredTabs;
        Assert.AreEqual(3, ordered.Count);
        Assert.AreEqual("TabB", ordered[0].TabKey);
        Assert.AreEqual("TabC", ordered[1].TabKey);
        Assert.AreEqual("TabA", ordered[2].TabKey);
    }

    [TestMethod]
    public void SettingsRegistry_PreventsDuplicateTabKeys()
    {
        var tab1 = new MockTabProvider("DuplicateKey", "PV_Tab1", "Tab 1", 10);
        var tab2 = new MockTabProvider("DuplicateKey", "PV_Tab2", "Tab 2", 20);

        VigilanceSettingsRegistry.RegisterTab(tab1);
        VigilanceSettingsRegistry.RegisterTab(tab2);

        Assert.AreEqual(1, VigilanceSettingsRegistry.RegisteredTabs.Count);
        Assert.AreEqual("PV_Tab1", VigilanceSettingsRegistry.RegisteredTabs[0].TabLabelKey);
    }

    [TestMethod]
    public void SettingsRegistry_UnregisterTab_RemovesExpectedTab()
    {
        var tab = new MockTabProvider("Removable", "PV_Removable", "Removable", 10);
        VigilanceSettingsRegistry.RegisterTab(tab);
        Assert.AreEqual(1, VigilanceSettingsRegistry.RegisteredTabs.Count);

        VigilanceSettingsRegistry.UnregisterTab("Removable");
        Assert.AreEqual(0, VigilanceSettingsRegistry.RegisteredTabs.Count);
    }

    [TestMethod]
    public void SettingsRegistry_RequestTabSelection_DispatchesEventAndStoresPending()
    {
        string? selectedKey = null;
        VigilanceSettingsRegistry.OnTabSelectionRequested += key => selectedKey = key;

        VigilanceSettingsRegistry.RequestTabSelection("Aquatics");
        Assert.AreEqual("Aquatics", VigilanceSettingsRegistry.PendingTabSelection);
        Assert.AreEqual("Aquatics", selectedKey);

        VigilanceSettingsRegistry.ClearPendingSelection();
        Assert.IsNull(VigilanceSettingsRegistry.PendingTabSelection);
    }

    // =========================================================================
    // Stream 3: Mod Settings & Sensory Cosmetic Themes
    // =========================================================================

    [TestMethod]
    public void SensoryThemeUtility_ProvidesDistinctColorsForAllCuratedThemes()
    {
        foreach (SensoryTheme theme in Enum.GetValues(typeof(SensoryTheme)))
        {
            var (radar, id, scout, sonar) = SensoryThemeUtility.GetThemeColors(theme);
            Assert.IsTrue(radar.a > 0f, $"{theme} radar alpha must be positive.");
            Assert.IsTrue(id.a > 0f, $"{theme} identification alpha must be positive.");
            Assert.IsTrue(scout.a > 0f, $"{theme} scout alpha must be positive.");
            Assert.IsTrue(sonar.a > 0f, $"{theme} sonar alpha must be positive.");
        }
    }

    [TestMethod]
    public void GeoscapeSettings_ApplySensoryTheme_UpdatesColorsAndFlags()
    {
        var settings = new ProjectVigilance.Geoscape.GeoscapeSettings();
        settings.ApplySensoryTheme(SensoryTheme.EmeraldPhosphor);

        Assert.AreEqual(SensoryTheme.EmeraldPhosphor, settings.sensoryTheme);
        var expected = SensoryThemeUtility.GetThemeColors(SensoryTheme.EmeraldPhosphor);
        Assert.AreEqual(expected.radar, settings.radarCustomColor);
        Assert.AreEqual(expected.identification, settings.radarIdentificationColor);
        Assert.AreEqual(expected.scout, settings.scoutCustomColor);
        Assert.AreEqual(expected.sonar, settings.sonarCustomColor);
        Assert.IsTrue(settings.useCustomIdentificationColor);
    }

    [TestMethod]
    public void FeatureGating_OptimisticPathing_RespectsModSetting()
    {
        var settings = new ProjectVigilance.Geoscape.GeoscapeSettings
        {
            optimisticCaravanDetours = false
        };
        ProjectVigilance.Geoscape.GeoscapeMod.Settings = settings;

        OptimisticPathingUtility.ForceActive = true;
        Assert.IsFalse(OptimisticPathingUtility.ShouldUseOptimisticPathing(), "Optimistic pathing must disable when optimisticCaravanDetours is false.");

        settings.optimisticCaravanDetours = true;
        Assert.IsTrue(OptimisticPathingUtility.ShouldUseOptimisticPathing(), "Optimistic pathing must enable when optimisticCaravanDetours is true.");

        OptimisticPathingUtility.ForceActive = false;
    }

    // =========================================================================
    // Stream 4: Modder Extensibility Hooks & Foundation Library
    // =========================================================================

    [TestMethod]
    public void DefModExtension_Sensor_HasCorrectDefaults()
    {
        var ext = new DefModExtension_Sensor();
        Assert.AreEqual(20f, ext.detectionRadius);
        Assert.AreEqual(12f, ext.identificationRadius);
        Assert.IsTrue(ext.isTechnological);
        Assert.IsFalse(ext.penetratesTerrainMasking);
        Assert.AreEqual(SensorDomainFlags.Radar, ext.sensorDomain);
        Assert.AreEqual(IntelligenceTier.Analysed, ext.maxIntelligenceTier);
    }

    [TestMethod]
    public void DefModExtension_Detectable_HasCorrectDefaults()
    {
        var ext = new DefModExtension_Detectable();
        Assert.AreEqual(1.0f, ext.radarSignatureMultiplier);
        Assert.IsFalse(ext.isStealthCraft);
        Assert.AreEqual(IntelligenceTier.Contact, ext.defaultClassificationTier);
        Assert.IsTrue(ext.canBeMaskedByTerrain);
    }

    [TestMethod]
    public void IncursionManager_Events_FireCorrectly()
    {
        WorldObject_Incursion? spawnedTarget = null;
        WorldObject_Incursion? departedTarget = null;

        Action<WorldObject_Incursion> onSpawn = inc => spawnedTarget = inc;
        Action<WorldObject_Incursion> onDepart = inc => departedTarget = inc;

        IncursionManager.OnIncursionSpawned += onSpawn;
        IncursionManager.OnIncursionDeparted += onDepart;

        try
        {
            var mockInc = new WorldObject_Incursion();
            IncursionManager.Notify_IncursionSpawned(mockInc);
            Assert.AreSame(mockInc, spawnedTarget);

            IncursionManager.Notify_IncursionDeparted(mockInc);
            Assert.AreSame(mockInc, departedTarget);
        }
        finally
        {
            IncursionManager.OnIncursionSpawned -= onSpawn;
            IncursionManager.OnIncursionDeparted -= onDepart;
        }
    }

    [TestMethod]
    public void IncursionInterceptionUtility_Events_FireCorrectly()
    {
        WorldObject_Incursion? ambushInc = null;
        Caravan? ambushCaravan = null;

        Action<WorldObject_Incursion, Caravan?> onAmbush = (inc, c) =>
        {
            ambushInc = inc;
            ambushCaravan = c;
        };

        IncursionInterceptionUtility.OnAmbushSprung += onAmbush;

        try
        {
            var mockInc = new WorldObject_Incursion();
            IncursionInterceptionUtility.Notify_AmbushSprung(mockInc, null);
            Assert.AreSame(mockInc, ambushInc);
            Assert.IsNull(ambushCaravan);
        }
        finally
        {
            IncursionInterceptionUtility.OnAmbushSprung -= onAmbush;
        }
    }

    // =========================================================================
    // Stream 2: RimWorld Keyed Localisation XML Files
    // =========================================================================

    [TestMethod]
    public void KeyedLocalisationFiles_AreWellFormedXmlAndContainRequiredKeys()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        // Search upwards to find Geoscape/1.7/Languages/English/Keyed
        string? langDir = null;
        var dir = new DirectoryInfo(baseDir);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "Geoscape", "1.7", "Languages", "English", "Keyed");
            if (Directory.Exists(candidate))
            {
                langDir = candidate;
                break;
            }
            dir = dir.Parent;
        }

        Assert.IsNotNull(langDir, "Geoscape/1.7/Languages/English/Keyed directory must exist.");

        string[] requiredFiles =
        {
            "Geoscape_Letters.xml",
            "Geoscape_Dialog.xml",
            "Geoscape_Inspect.xml",
            "Geoscape_FloatMenus.xml",
            "Geoscape_Settings.xml"
        };

        foreach (string file in requiredFiles)
        {
            string filePath = Path.Combine(langDir, file);
            Assert.IsTrue(File.Exists(filePath), $"Keyed XML file {file} must exist.");

            XDocument doc = XDocument.Load(filePath);
            Assert.IsNotNull(doc.Root, $"{file} must have a root XML element.");
            Assert.AreEqual("LanguageData", doc.Root.Name.LocalName, $"{file} root element must be <LanguageData>.");

            var elements = doc.Root.Elements();
            int count = 0;
            foreach (var el in elements)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(el.Name.LocalName), $"Element in {file} must have a non-empty key name.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(el.Value), $"Key {el.Name.LocalName} in {file} must have non-empty text.");
                Assert.IsTrue(el.Name.LocalName.StartsWith("PV_"), $"Key {el.Name.LocalName} in {file} must follow 'PV_*' naming standard.");
                count++;
            }
            Assert.IsTrue(count > 0, $"{file} must contain at least one keyed string.");
        }
    }
}
