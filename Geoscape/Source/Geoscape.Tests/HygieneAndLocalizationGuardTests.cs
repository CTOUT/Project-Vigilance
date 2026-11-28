using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape.Rules;
using ProjectVigilance.Geoscape.Sensory;

namespace Geoscape.Tests;

[TestClass]
public sealed class HygieneAndLocalizationGuardTests
{
    private const int ProductionFileLineCeiling = 1200;

    private static readonly string[] ModuleRoots = { "Core", "Geoscape", "Orbital", "Aquatics", "Thermal" };

    // Grandfathered pre-Wave-4 files awaiting future decomposition; no new files may be added here.
    private static readonly HashSet<string> GrandfatheredLargeFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "IncursionInterceptionUtility.cs",
        "MapComponent_OverlandAmbush.cs"
    };

    private static string ResolveRepoRoot()
    {
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Directory.Build.props")) &&
                Directory.Exists(Path.Combine(dir.FullName, "Geoscape")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        Assert.Fail("Could not locate repository root containing Directory.Build.props.");
        return string.Empty;
    }

    private static Dictionary<string, string> LoadAllKeyedXmlKeys(string repoRoot)
    {
        var keys = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string module in ModuleRoots)
        {
            string keyedDir = Path.Combine(repoRoot, module, "1.7", "Languages", "English", "Keyed");
            if (!Directory.Exists(keyedDir))
            {
                continue;
            }

            foreach (string xmlFile in Directory.GetFiles(keyedDir, "*.xml", SearchOption.AllDirectories))
            {
                XDocument doc = XDocument.Load(xmlFile);
                if (doc.Root == null)
                {
                    continue;
                }

                foreach (XElement elem in doc.Root.Elements())
                {
                    string key = elem.Name.LocalName;
                    Assert.IsFalse(
                        keys.ContainsKey(key),
                        $"Duplicate Keyed XML key '{key}' in '{xmlFile}' (already defined in '{keys[key]}').");
                    keys[key] = Path.GetRelativePath(repoRoot, xmlFile);
                }
            }
        }

        return keys;
    }

    private static string LoadAllProductionSourceText(string repoRoot)
    {
        var chunks = new List<string>();
        foreach (string module in ModuleRoots)
        {
            string srcDir = Path.Combine(repoRoot, module, "Source");
            if (!Directory.Exists(srcDir))
            {
                continue;
            }

            foreach (string csFile in Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories))
            {
                if (csFile.Contains("Geoscape.Tests", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                chunks.Add(File.ReadAllText(csFile));
            }
        }

        return string.Join("\n", chunks);
    }

    [TestMethod]
    public void AllKeyedXmlKeys_AreReferencedInProductionCode()
    {
        string repoRoot = ResolveRepoRoot();
        Dictionary<string, string> xmlKeys = LoadAllKeyedXmlKeys(repoRoot);
        string sourceText = LoadAllProductionSourceText(repoRoot);

        Assert.IsTrue(xmlKeys.Count > 0, "Expected at least one Keyed XML entry across modules.");

        var orphans = new List<string>();
        foreach (KeyValuePair<string, string> kvp in xmlKeys)
        {
            string key = kvp.Key;
            bool referenced = sourceText.Contains(key, StringComparison.Ordinal)
                || (key.StartsWith("PV_Settings_Theme_", StringComparison.Ordinal) && sourceText.Contains("PV_Settings_Theme_", StringComparison.Ordinal))
                || (key.StartsWith("PV_Shroud_Effects_", StringComparison.Ordinal) && sourceText.Contains("PV_Shroud_Effects_", StringComparison.Ordinal));

            if (!referenced)
            {
                orphans.Add($"{key} ({kvp.Value})");
            }
        }

        Assert.AreEqual(
            0,
            orphans.Count,
            $"Orphaned Keyed XML keys found (wire in C# or remove): {string.Join(", ", orphans)}");
    }

    [TestMethod]
    public void AllProductionCodePvKeys_ExistInKeyedXml()
    {
        string repoRoot = ResolveRepoRoot();
        Dictionary<string, string> xmlKeys = LoadAllKeyedXmlKeys(repoRoot);
        string sourceText = LoadAllProductionSourceText(repoRoot);

        // Localisation keys always use PascalCase after PV_ (e.g. PV_Settings_*, PV_Menu_*), distinguishing them from camelCase Scribe tags (PV_destinationTile).
        var regex = new Regex(@"""(PV_[A-Z][A-Za-z0-9_]+)""", RegexOptions.Compiled);
        var missing = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in regex.Matches(sourceText))
        {
            string key = match.Groups[1].Value;
            // Skip interpolated key prefixes (e.g. "PV_Settings_Theme_")
            if (key.EndsWith("_", StringComparison.Ordinal))
            {
                continue;
            }

            if (!xmlKeys.ContainsKey(key))
            {
                missing.Add(key);
            }
        }

        Assert.AreEqual(
            0,
            missing.Count,
            $"Production C# references PV_* keys missing from Languages/English/Keyed/*.xml: {string.Join(", ", missing.OrderBy(k => k))}");
    }

    [TestMethod]
    public void LegacyEnumAliases_StillParseForScribeBackwardsCompatibility()
    {
        Assert.AreEqual(VisibilityMode.FogOfWar, Enum.Parse<VisibilityMode>("ClassicHazeOfWar"));
        Assert.AreEqual(CampaignProfile.FogOfWar, Enum.Parse<CampaignProfile>("ClassicHaze"));
    }

    [TestMethod]
    public void ProductionSourceFiles_StayBelowMonolithCeiling()
    {
        string repoRoot = ResolveRepoRoot();
        var violations = new List<string>();

        foreach (string module in ModuleRoots)
        {
            string srcDir = Path.Combine(repoRoot, module, "Source");
            if (!Directory.Exists(srcDir))
            {
                continue;
            }

            foreach (string csFile in Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories))
            {
                if (csFile.Contains("Geoscape.Tests", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string fileName = Path.GetFileName(csFile);
                if (GrandfatheredLargeFiles.Contains(fileName))
                {
                    continue;
                }

                int lineCount = File.ReadLines(csFile).Count();
                if (lineCount > ProductionFileLineCeiling)
                {
                    violations.Add($"{Path.GetRelativePath(repoRoot, csFile)} ({lineCount} lines > {ProductionFileLineCeiling})");
                }
            }
        }

        Assert.AreEqual(
            0,
            violations.Count,
            $"Production C# files exceed {ProductionFileLineCeiling}-line monolith ceiling (split via partial class or domain helper): {string.Join(", ", violations)}");
    }
}
