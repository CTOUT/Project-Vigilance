using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml;
using ProjectVigilance.Core;
using ProjectVigilance.Core.Migration;
using ProjectVigilance.Core.Modules;
using ProjectVigilance.Geoscape;
using ProjectVigilance.Geoscape.Orbital;
using ProjectVigilance.Geoscape.Rules;
using ProjectVigilance.Geoscape.Sensory;
using ProjectVigilance.Geoscape.Thermal;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Geoscape.Tests;

[TestClass]
[DoNotParallelize]
public class ModularMigrationAndPackagingTests
{
    private static string RepoRoot
    {
        get
        {
            string fiveUp = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", ".."));
            if (Directory.Exists(Path.Combine(fiveUp, "Core", "About")))
            {
                return fiveUp;
            }

            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", ".."));
        }
    }

    [TestInitialize]
    public void Setup()
    {
        GeoscapeTestHarness.EnsureEnvironmentInitialised();
        GeoscapeTestHarness.InitMockWorld(out _);
    }

    [TestCleanup]
    public void Cleanup() => GeoscapeTestHarness.ResetAllStaticState();

    [TestMethod]
    public void AllFiveModuleManifests_HaveValidDependenciesAndPreservedWorkshopId()
    {
        string[] modules = { "Core", "Geoscape", "Orbital", "Aquatics", "Thermal" };
        foreach (string module in modules)
        {
            string aboutPath = Path.Combine(RepoRoot, module, "About", "About.xml");
            Assert.IsTrue(File.Exists(aboutPath), $"Missing About.xml for module '{module}' at {aboutPath}");

            var doc = new XmlDocument();
            doc.Load(aboutPath);

            var depNodes = doc.SelectNodes("//modDependencies/li/packageId");
            var deps = new List<string>();
            if (depNodes != null)
            {
                foreach (XmlNode node in depNodes)
                {
                    deps.Add(node.InnerText.Trim());
                }
            }

            if (module == "Core")
            {
                Assert.IsTrue(deps.Contains("brrainz.harmony"), "Core module must declare brrainz.harmony dependency.");
                Assert.IsFalse(deps.Any(d => d.StartsWith("ProjectVigilance.", StringComparison.OrdinalIgnoreCase)),
                    "Core module must not depend on any Project Vigilance sub-module.");
            }
            else if (module == "Geoscape")
            {
                Assert.IsTrue(deps.Contains("brrainz.harmony"),
                    "Geoscape must declare brrainz.harmony so existing Workshop subscribers can boot standalone via LoadFolders EmbeddedCore.");
                Assert.IsFalse(deps.Contains("ProjectVigilance.Core"),
                    "Geoscape must not hard-require ProjectVigilance.Core in modDependencies so existing subscribers upgrade with zero red errors.");

                var loadAfterNodes = doc.SelectNodes("//loadAfter/li");
                var loadAfter = new List<string>();
                if (loadAfterNodes != null)
                {
                    foreach (XmlNode node in loadAfterNodes)
                    {
                        loadAfter.Add(node.InnerText.Trim());
                    }
                }
                Assert.IsTrue(loadAfter.Contains("ProjectVigilance.Core"), "Geoscape must loadAfter ProjectVigilance.Core when standalone Core is active.");
            }
            else
            {
                Assert.IsTrue(deps.Contains("ProjectVigilance.Core"), $"Module '{module}' must declare ProjectVigilance.Core dependency.");
                Assert.IsFalse(deps.Contains("brrainz.harmony"), $"Module '{module}' should inherit Harmony transitively through Core rather than duplicating brrainz.harmony.");
            }
        }

        string loadFoldersPath = Path.Combine(RepoRoot, "Geoscape", "LoadFolders.xml");
        Assert.IsTrue(File.Exists(loadFoldersPath), "Geoscape must include LoadFolders.xml for seamless EmbeddedCore fallback.");
        var loadFoldersDoc = new XmlDocument();
        loadFoldersDoc.Load(loadFoldersPath);
        XmlNodeList? folderEntries = loadFoldersDoc.SelectNodes("//v1.7/li");
        Assert.IsNotNull(folderEntries);

        int embeddedIdx = -1;
        int mainIdx = -1;
        for (int i = 0; i < folderEntries!.Count; i++)
        {
            XmlNode node = folderEntries[i]!;
            string text = node.InnerText.Trim();
            string? ifNotActive = node.Attributes?["IfModNotActive"]?.Value;
            if (text == "1.7/EmbeddedCore" && ifNotActive == "ProjectVigilance.Core")
            {
                embeddedIdx = i;
            }
            else if (text == "1.7" && ifNotActive == null)
            {
                mainIdx = i;
            }
        }
        Assert.IsTrue(embeddedIdx >= 0, "LoadFolders.xml must declare <li IfModNotActive=\"ProjectVigilance.Core\">1.7/EmbeddedCore</li>.");
        Assert.IsTrue(mainIdx > embeddedIdx, "1.7/EmbeddedCore must load before 1.7 when standalone Core is inactive.");

        string publishedIdPath = Path.Combine(RepoRoot, "Geoscape", "About", "PublishedFileId.txt");
        Assert.IsTrue(File.Exists(publishedIdPath), "Geoscape must retain PublishedFileId.txt for existing Steam Workshop subscribers.");
        Assert.AreEqual("3822378226", File.ReadAllText(publishedIdPath).Trim());
    }

    [TestMethod]
    public void AquaticsXmlDefs_AreCleanlyPartitionedFromGeoscape()
    {
        var aquaticsBuildings = new XmlDocument();
        aquaticsBuildings.Load(Path.Combine(RepoRoot, "Aquatics", "1.7", "Defs", "ThingDefs_Buildings", "Buildings_Aquatics.xml"));
        Assert.IsNotNull(aquaticsBuildings.SelectSingleNode("//ThingDef[defName='Geoscape_SonarArray']"));

        var aquaticsWorldObjects = new XmlDocument();
        aquaticsWorldObjects.Load(Path.Combine(RepoRoot, "Aquatics", "1.7", "Defs", "WorldObjectDefs", "WorldObjects_Aquatics.xml"));
        Assert.IsNotNull(aquaticsWorldObjects.SelectSingleNode("//WorldObjectDef[defName='Vigilance_ListeningPost_Sonar']"));

        var aquaticsPatch = new XmlDocument();
        aquaticsPatch.Load(Path.Combine(RepoRoot, "Aquatics", "1.7", "Patches", "Aquatics_GeoscapeIntegration.xml"));
        XmlNode? findMod = aquaticsPatch.SelectSingleNode("//Patch/Operation/mods/li");
        Assert.IsNotNull(findMod);
        Assert.AreEqual("Project Vigilance - Geoscape & Early Warning", findMod!.InnerText.Trim());

        var geoscapeBuildings = new XmlDocument();
        geoscapeBuildings.Load(Path.Combine(RepoRoot, "Geoscape", "1.7", "Defs", "ThingDefs_Buildings", "Buildings_Radar.xml"));
        Assert.IsNull(geoscapeBuildings.SelectSingleNode("//ThingDef[defName='Geoscape_SonarArray']"));

        var geoscapePosts = new XmlDocument();
        geoscapePosts.Load(Path.Combine(RepoRoot, "Geoscape", "1.7", "Defs", "WorldObjectDefs", "WorldObjects_ListeningPost.xml"));
        Assert.IsNull(geoscapePosts.SelectSingleNode("//WorldObjectDef[defName='Vigilance_ListeningPost_Sonar']"));
    }

    [TestMethod]
    public void BackCompatibilityConverter_WhenOrbitalInactive_RedirectsOrbitalDefsAndTypesAndRecordsInterception()
    {
        VigilanceModuleManager.SetModuleActiveOverride(VigilanceModuleId.Orbital, false);
        var converter = new BackCompatibilityConverter_VigilanceModular();

        string? stationDef = converter.BackCompatibleDefName(typeof(WorldObjectDef), "Vigilance_OrbitalStation", false, null!);
        string? tradeSatDef = converter.BackCompatibleDefName(typeof(WorldObjectDef), "Vigilance_Satellite_Trade", false, null!);

        Assert.AreEqual(BackCompatibilityConverter_VigilanceModular.MissingModuleWorldObjectDefName, stationDef);
        Assert.AreEqual(BackCompatibilityConverter_VigilanceModular.MissingModuleWorldObjectDefName, tradeSatDef);

        Type? managerType = converter.GetBackCompatibleType(typeof(WorldComponent), "ProjectVigilance.Geoscape.Orbital.OrbitalManager", null!);
        Assert.AreEqual(typeof(DeprecatedWorldComponentStub), managerType);

        Type? gameCompType = converter.GetBackCompatibleType(typeof(GameComponent), "ProjectVigilance.Geoscape.Orbital.GameComponent_OrbitalLifecycle", null!);
        Assert.AreEqual(typeof(DeprecatedGameComponentStub), gameCompType);

        var xml = new XmlDocument();
        xml.LoadXml("<worldObject><def>Vigilance_Satellite_Trade</def></worldObject>");
        Type? satType = converter.GetBackCompatibleType(typeof(WorldObject), "ProjectVigilance.Geoscape.Orbital.Satellite_Trade", xml.DocumentElement!);
        Assert.AreEqual(typeof(MissingModuleWorldObjectStub), satType);

        var pending = VigilanceMigrationTracker.GetPendingInterceptionsSnapshot();
        Assert.IsTrue(pending.ContainsKey(VigilanceModuleId.Orbital));
        Assert.IsTrue(pending[VigilanceModuleId.Orbital].Contains("Vigilance_OrbitalStation"));
        Assert.IsTrue(pending[VigilanceModuleId.Orbital].Contains("Vigilance_Satellite_Trade"));
        Assert.IsTrue(pending[VigilanceModuleId.Orbital].Contains("OrbitalManager"));
        Assert.IsTrue(pending[VigilanceModuleId.Orbital].Contains("GameComponent_OrbitalLifecycle"));
    }

    [TestMethod]
    public void BackCompatibilityConverter_WhenAquaticsInactive_RedirectsSonarPostAndRecordsThingDef()
    {
        VigilanceModuleManager.SetModuleActiveOverride(VigilanceModuleId.Aquatics, false);
        var converter = new BackCompatibilityConverter_VigilanceModular();

        string? sonarWoDef = converter.BackCompatibleDefName(typeof(WorldObjectDef), "Vigilance_ListeningPost_Sonar", false, null!);
        string? sonarThingDef = converter.BackCompatibleDefName(typeof(ThingDef), "Geoscape_SonarArray", false, null!);

        Assert.AreEqual(BackCompatibilityConverter_VigilanceModular.MissingModuleWorldObjectDefName, sonarWoDef);
        Assert.IsNull(sonarThingDef);

        var sonarXml = new XmlDocument();
        sonarXml.LoadXml("<worldObject><def>Vigilance_ListeningPost_Sonar</def></worldObject>");
        Type? sonarPostType = converter.GetBackCompatibleType(typeof(WorldObject), "ProjectVigilance.Geoscape.Sensory.WorldObject_ListeningPost", sonarXml.DocumentElement!);
        Assert.AreEqual(typeof(MissingModuleWorldObjectStub), sonarPostType);

        var radarXml = new XmlDocument();
        radarXml.LoadXml("<worldObject><def>Vigilance_ListeningPost_Automated</def></worldObject>");
        Type? radarPostType = converter.GetBackCompatibleType(typeof(WorldObject), "ProjectVigilance.Geoscape.Sensory.WorldObject_ListeningPost", radarXml.DocumentElement!);
        Assert.IsNull(radarPostType);

        var pending = VigilanceMigrationTracker.GetPendingInterceptionsSnapshot();
        Assert.IsTrue(pending.ContainsKey(VigilanceModuleId.Aquatics));
        Assert.IsTrue(pending[VigilanceModuleId.Aquatics].Contains("Vigilance_ListeningPost_Sonar"));
        Assert.IsTrue(pending[VigilanceModuleId.Aquatics].Contains("Geoscape_SonarArray"));
    }

    [TestMethod]
    public void BackCompatibilityConverter_WhenModulesActive_DoesNotInterceptOrbitalOrAquatics()
    {
        var converter = new BackCompatibilityConverter_VigilanceModular();

        Assert.IsNull(converter.BackCompatibleDefName(typeof(WorldObjectDef), "Vigilance_OrbitalStation", false, null!));
        Assert.IsNull(converter.BackCompatibleDefName(typeof(WorldObjectDef), "Vigilance_ListeningPost_Sonar", false, null!));
        Assert.IsNull(converter.GetBackCompatibleType(typeof(WorldComponent), "ProjectVigilance.Geoscape.Orbital.OrbitalManager", null!));

        Assert.AreEqual(0, VigilanceMigrationTracker.GetPendingInterceptionsSnapshot().Count);
    }

    [TestMethod]
    public void VigilanceMigrationTracker_PreSplitLegacySave_DetectsMissingModules()
    {
        VigilanceModuleManager.SetModuleActiveOverride(VigilanceModuleId.Orbital, false);
        VigilanceModuleManager.SetModuleActiveOverride(VigilanceModuleId.Aquatics, false);
        VigilanceModuleManager.SetModuleActiveOverride(VigilanceModuleId.Thermal, false);
        VigilanceModuleManager.HasPriorGeoscapeSaveDataHook = static () => true;

        var tracker = new VigilanceMigrationTracker(Find.World);
        tracker.SetSavedActiveModulesForTesting(Array.Empty<string>(), schemaVersion: 0, markLoaded: true);
        tracker.EvaluateMigrationState(fromLoad: true);

        Assert.IsTrue(tracker.IsPreSplitLegacySave);
        Assert.IsTrue(tracker.RequiresMigrationAdvisory);
        CollectionAssert.Contains(tracker.MissingModules.ToList(), VigilanceModuleId.Orbital);
        CollectionAssert.Contains(tracker.MissingModules.ToList(), VigilanceModuleId.Aquatics);
        CollectionAssert.Contains(tracker.MissingModules.ToList(), VigilanceModuleId.Thermal);
    }

    [TestMethod]
    public void VigilanceMigrationTracker_RetireMissingModuleObjects_RemovesStubsRecordsLedgerAndClearsAdvisory()
    {
        VigilanceModuleManager.SetModuleActiveOverride(VigilanceModuleId.Orbital, false);

        string? recordedMigrationNote = null;
        VigilanceModuleManager.RecordModuleMigrationHook = note => recordedMigrationNote = note;

        var tracker = new VigilanceMigrationTracker(Find.World);
        tracker.SetSavedActiveModulesForTesting(new[] { "ProjectVigilance.Core", "ProjectVigilance.Geoscape", "ProjectVigilance.Orbital" });
        VigilanceMigrationTracker.RecordInterceptedEntity(VigilanceModuleId.Orbital, "Vigilance_Satellite_Trade");
        tracker.EvaluateMigrationState(fromLoad: true);

        Assert.IsTrue(tracker.RequiresMigrationAdvisory);
        CollectionAssert.Contains(tracker.MissingModules.ToList(), VigilanceModuleId.Orbital);

        var worldObjects = new List<WorldObject>
        {
            new MissingModuleWorldObjectStub(),
            GeoscapeTestHarness.CreateSettlement(GeoscapeTestHarness.MakeTile(10))
        };

        int retired = tracker.RetireMissingModuleObjects(worldObjects);

        Assert.AreEqual(1, retired);
        Assert.AreEqual(1, worldObjects.Count);
        Assert.IsFalse(worldObjects[0] is MissingModuleWorldObjectStub);
        Assert.IsFalse(tracker.RequiresMigrationAdvisory);
        Assert.AreEqual(0, tracker.InterceptedEntitiesByModule.Count);
        Assert.IsFalse(tracker.SavedActiveModules.Contains("ProjectVigilance.Orbital"));
        Assert.IsNotNull(recordedMigrationNote);
        StringAssert.Contains(recordedMigrationNote, "Retired 1");
    }

    [TestMethod]
    public void ModularSettingsArchitecture_OnlyCoreHostsSettingsCategory()
    {
#pragma warning disable SYSLIB0050 // RuntimeHelpers.GetUninitializedObject replacement is fine; RuntimeHelpers is already standard in harness
        var coreMod = (VigilanceCoreMod)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(VigilanceCoreMod));
        var geoscapeMod = (GeoscapeMod)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(GeoscapeMod));
        var orbitalMod = (OrbitalMod)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(OrbitalMod));
        var aquaticsMod = (AquaticsMod)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(AquaticsMod));
        var thermalMod = (ThermalMod)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ThermalMod));
#pragma warning restore SYSLIB0050

        Assert.AreEqual("Project Vigilance", coreMod.SettingsCategory());
        Assert.AreEqual("Project Vigilance — Geoscape", geoscapeMod.SettingsCategory());
        Assert.AreEqual("Project Vigilance — Orbital", orbitalMod.SettingsCategory());
        Assert.AreEqual("Project Vigilance — Aquatics", aquaticsMod.SettingsCategory());
        Assert.AreEqual("Project Vigilance — Thermal", thermalMod.SettingsCategory());

        var filtered = ProjectVigilance.Core.Patches.Dialog_Options_ModFilter_Patch
            .FilterModsForOptionsGrid(new Mod[] { coreMod, geoscapeMod, orbitalMod, aquaticsMod, thermalMod })
            .ToList();
        Assert.AreEqual(1, filtered.Count, "Dialog_Options Mod Options grid must show only the primary Project Vigilance entry.");
        Assert.AreSame(coreMod, filtered[0]);
    }

    [TestMethod]
    public void VigilanceCoreMod_EmbeddedFallbackMode_SetsFlagAndFlagsCoreMissingOnLegacySave()
    {
        VigilanceModuleManager.SetCoreEmbeddedFallbackOverride(true);
        VigilanceModuleManager.SetModuleActiveOverride(VigilanceModuleId.Core, false);
        VigilanceModuleManager.SetModuleActiveOverride(VigilanceModuleId.Orbital, false);
        VigilanceModuleManager.HasPriorGeoscapeSaveDataHook = static () => true;

        var tracker = new VigilanceMigrationTracker(Find.World);
        tracker.SetSavedActiveModulesForTesting(Array.Empty<string>(), schemaVersion: 0, markLoaded: true);
        tracker.EvaluateMigrationState(fromLoad: true);

        Assert.IsTrue(VigilanceModuleManager.IsCoreEmbeddedFallback);
        Assert.IsTrue(tracker.RequiresMigrationAdvisory);
        CollectionAssert.Contains(tracker.MissingModules.ToList(), VigilanceModuleId.Core);
    }

    [TestMethod]
    public void StageCleanDistribution_GeoscapeIncludesLoadFoldersAndEmbeddedCore()
    {
        string distGeoscape = Path.Combine(RepoRoot, "dist", "ProjectVigilance-Geoscape");
        Assert.IsTrue(File.Exists(Path.Combine(distGeoscape, "LoadFolders.xml")),
            "Staged Geoscape package must include LoadFolders.xml.");
        Assert.IsTrue(File.Exists(Path.Combine(distGeoscape, "1.7", "EmbeddedCore", "Assemblies", "ProjectVigilance.Core.dll")),
            "Staged Geoscape package must include EmbeddedCore/Assemblies/ProjectVigilance.Core.dll.");
        Assert.IsTrue(File.Exists(Path.Combine(distGeoscape, "1.7", "EmbeddedCore", "Defs", "WorldObjectDefs", "WorldObjects_MigrationStubs.xml")),
            "Staged Geoscape package must include EmbeddedCore migration stub Defs.");
        Assert.IsTrue(File.Exists(Path.Combine(distGeoscape, "1.7", "EmbeddedCore", "Languages", "English", "Keyed", "Core_Migration.xml")),
            "Staged Geoscape package must include EmbeddedCore keyed migration strings.");
    }
}
