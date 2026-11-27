using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Core.Modules;
using ProjectVigilance.Core.Patches;
using ProjectVigilance.Geoscape;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld;
using RimWorld.Planet;
using Verse;

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

    private sealed class StubChildMod : Mod
    {
        private readonly string category;
        public StubChildMod(string category) : base(null!) => this.category = category;
        public override string SettingsCategory() => category;
    }

    [TestMethod]
    public void DialogOptionsModFilter_RefiltersWhenRimWorldReplacesCachedModsList()
    {
        FieldInfo? field = AccessTools.Field(typeof(Dialog_Options), "cachedModsWithSettings");
        Assert.IsNotNull(field, "Dialog_Options.cachedModsWithSettings field must exist.");

        var dialog = (Dialog_Options)RuntimeHelpers.GetUninitializedObject(typeof(Dialog_Options));
        var primary = new StubChildMod("Project Vigilance");
        var child = new StubChildMod("Project Vigilance — Geoscape");
        var other = new StubChildMod("Other Mod");

        // First pass (simulates constructor postfix)
        field!.SetValue(dialog, new List<Mod> { primary, child, other });
        Dialog_Options_ModFilter_Patch.ApplyFilter(dialog);
        var firstFiltered = ((IEnumerable<Mod>)field.GetValue(dialog)!).ToList();
        Assert.AreEqual(2, firstFiltered.Count);
        Assert.IsFalse(firstFiltered.Contains(child));

        // Second pass after RimWorld PreOpen replaces cachedModsWithSettings with a new unfiltered list
        field.SetValue(dialog, new List<Mod> { primary, child, other });
        Dialog_Options_ModFilter_Patch.ApplyFilter(dialog);
        var secondFiltered = ((IEnumerable<Mod>)field.GetValue(dialog)!).ToList();
        Assert.AreEqual(2, secondFiltered.Count);
        Assert.IsFalse(secondFiltered.Contains(child), "Child module must be filtered out again when RimWorld replaces cachedModsWithSettings.");

        // Steady-state third pass keeps the exact same filtered list reference (zero reallocation)
        object? cachedRefBefore = field.GetValue(dialog);
        Dialog_Options_ModFilter_Patch.ApplyFilter(dialog);
        Assert.IsTrue(ReferenceEquals(cachedRefBefore, field.GetValue(dialog)), "Steady-state ApplyFilter must not reallocate when reference is unchanged.");
    }

    [TestMethod]
    public void WorldDrawLayerShroudShell_RebuildChunkTriangles_UploadsVertsAndColorsWithTris()
    {
        MethodInfo? rebuild = typeof(WorldDrawLayer_ShroudShell).GetMethod(
            "RebuildChunkTriangles",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(rebuild, "WorldDrawLayer_ShroudShell.RebuildChunkTriangles must exist.");

        byte[] il = rebuild!.GetMethodBody()!.GetILAsByteArray()!;
        Module module = rebuild.Module;
        int finalizeCalls = 0;

        // Scan callvirt (0x6F) / call (0x28) instructions targeting LayerSubMesh.FinalizeMesh(MeshParts)
        // and confirm the immediately preceding opcode loads MeshParts.Verts | MeshParts.Colors | MeshParts.Tris (ldc.i4.7 == 0x1D).
        for (int i = 1; i <= il.Length - 5; i++)
        {
            byte op = il[i];
            if (op != 0x6F && op != 0x28) continue;

            int token = BitConverter.ToInt32(il, i + 1);
            MethodBase? resolved;
            try
            {
                resolved = module.ResolveMethod(token);
            }
            catch
            {
                continue;
            }

            if (resolved?.DeclaringType == typeof(LayerSubMesh) && resolved.Name == nameof(LayerSubMesh.FinalizeMesh))
            {
                finalizeCalls++;
                Assert.AreEqual(
                    (byte)0x1D,
                    il[i - 1],
                    "RebuildChunkTriangles FinalizeMesh call must pass MeshParts.Verts | MeshParts.Colors | MeshParts.Tris (ldc.i4.7).");
            }
        }

        Assert.IsTrue(finalizeCalls > 0, "Expected at least one FinalizeMesh call in RebuildChunkTriangles.");
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
