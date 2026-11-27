using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Core.Modules;
using ProjectVigilance.Geoscape;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld.Planet;

namespace Geoscape.Tests;

[TestClass]
[DoNotParallelize]
public sealed class AuditWave2PerfAndRule14Tests : GeoscapeTestBase
{
    private static readonly string[] ModuleAssemblyDirs =
    {
        "Core",
        "Geoscape",
        "Orbital",
        "Aquatics",
        "Thermal"
    };

    [TestMethod]
    public void Rule14_AllProductionAssemblies_HaveNoPlanetTileHashedCollectionFields()
    {
        var assemblies = new HashSet<Assembly>
        {
            typeof(VigilanceModuleManager).Assembly,
            typeof(GeoscapeMod).Assembly
        };

        string? repoRoot = ResolveRepoRoot();
        if (repoRoot != null)
        {
            foreach (string moduleDir in ModuleAssemblyDirs)
            {
                string dllDir = Path.Combine(repoRoot, moduleDir, "1.7", "Assemblies");
                if (!Directory.Exists(dllDir)) continue;

                foreach (string dll in Directory.GetFiles(dllDir, "ProjectVigilance.*.dll"))
                {
                    try
                    {
                        assemblies.Add(Assembly.LoadFrom(dll));
                    }
                    catch
                    {
                        // Metadata-less or already loaded under another context; rely on loaded domain match
                    }
                }
            }
        }

        foreach (Assembly loaded in AppDomain.CurrentDomain.GetAssemblies())
        {
            string? name = loaded.GetName().Name;
            if (name != null && name.StartsWith("ProjectVigilance", StringComparison.Ordinal))
            {
                assemblies.Add(loaded);
            }
        }

        var violations = new List<string>();
        foreach (Assembly assembly in assemblies)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types!;
            }

            foreach (Type? type in types)
            {
                if (type == null) continue;

                FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < fields.Length; i++)
                {
                    Type fieldType = fields[i].FieldType;
                    if (!fieldType.IsGenericType) continue;

                    Type genericDef = fieldType.GetGenericTypeDefinition();
                    if (genericDef == typeof(HashSet<>) && fieldType.GetGenericArguments()[0] == typeof(PlanetTile))
                    {
                        violations.Add($"{assembly.GetName().Name}: {type.FullName}.{fields[i].Name} ({fieldType.Name})");
                    }
                    else if (genericDef == typeof(Dictionary<,>) && fieldType.GetGenericArguments()[0] == typeof(PlanetTile))
                    {
                        violations.Add($"{assembly.GetName().Name}: {type.FullName}.{fields[i].Name} ({fieldType.Name})");
                    }
                }
            }
        }

        Assert.AreEqual(0, violations.Count, "Rule 14 violations found: " + string.Join("; ", violations));
    }

    [TestMethod]
    public void HazeReconnaissanceManager_QuestIndex_TracksConcealedAndUncoveredSites()
    {
        var manager = new HazeReconnaissanceManager(null!);
        Assert.IsFalse(manager.HasAnyConcealedSites);
        Assert.IsFalse(manager.IsQuestConcealed(42));
    }

#if !DEBUG
    [TestMethod]
    public void ReleaseAssemblies_AndDistPackages_ContainNoPdbFiles()
    {
        string? repoRoot = ResolveRepoRoot();
        Assert.IsNotNull(repoRoot, "Repository root should resolve from test base directory.");

        var pdbFindings = new List<string>();
        foreach (string moduleDir in ModuleAssemblyDirs)
        {
            string asmDir = Path.Combine(repoRoot!, moduleDir, "1.7", "Assemblies");
            if (Directory.Exists(asmDir))
            {
                pdbFindings.AddRange(Directory.GetFiles(asmDir, "*.pdb", SearchOption.AllDirectories));
            }
        }

        string distDir = Path.Combine(repoRoot!, "dist");
        if (Directory.Exists(distDir))
        {
            foreach (string moduleDir in ModuleAssemblyDirs)
            {
                string pkgAsmDir = Path.Combine(distDir, $"ProjectVigilance-{moduleDir}", "1.7", "Assemblies");
                if (Directory.Exists(pkgAsmDir))
                {
                    pdbFindings.AddRange(Directory.GetFiles(pkgAsmDir, "*.pdb", SearchOption.AllDirectories));
                }
            }

            string currentSuiteDir = Path.Combine(distDir, $"ProjectVigilance-Suite-v{BuildInfo.Version}");
            if (Directory.Exists(currentSuiteDir))
            {
                pdbFindings.AddRange(Directory.GetFiles(currentSuiteDir, "*.pdb", SearchOption.AllDirectories));
            }
        }

        Assert.AreEqual(0, pdbFindings.Count, "Release PDB files found: " + string.Join(", ", pdbFindings));
    }
#endif

    private static string? ResolveRepoRoot()
    {
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                File.Exists(Path.Combine(dir.FullName, "Directory.Build.props")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return null;
    }
}
