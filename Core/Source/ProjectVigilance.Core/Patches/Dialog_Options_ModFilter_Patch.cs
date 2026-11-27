using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ProjectVigilance.Core.Patches;

/// <summary>
/// Filters Project Vigilance sub-module entries out of the RimWorld Mod Options grid
/// (<see cref="Dialog_Options"/>) so only the single primary "Project Vigilance" button appears,
/// while preserving per-mod "Mod options" links on the <see cref="Page_ModsConfig"/> screen.
/// </summary>
[HarmonyPatch]
public static class Dialog_Options_ModFilter_Patch
{
    private static readonly FieldInfo? CachedModsWithSettingsField =
        AccessTools.Field(typeof(Dialog_Options), "cachedModsWithSettings");

    private static readonly ConditionalWeakTable<Dialog_Options, List<Mod>> filteredDialogs = new();

    internal static IEnumerable<Mod> FilterModsForOptionsGrid(IEnumerable<Mod>? mods)
    {
        if (mods == null)
        {
            yield break;
        }

        foreach (Mod mod in mods)
        {
            if (ShouldIncludeInModOptionsGrid(mod))
            {
                yield return mod;
            }
        }
    }

    internal static bool ShouldIncludeInModOptionsGrid(Mod? mod)
    {
        if (mod == null)
        {
            return false;
        }

        if (mod is VigilanceCoreMod)
        {
            return true;
        }

        string category = mod.SettingsCategory();
        if (!string.IsNullOrEmpty(category) &&
            category.StartsWith("Project Vigilance — ", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    internal static void ApplyFilter(Dialog_Options instance)
    {
        if (instance == null || CachedModsWithSettingsField == null)
        {
            return;
        }

        object? raw = CachedModsWithSettingsField.GetValue(instance);
        if (raw != null && filteredDialogs.TryGetValue(instance, out List<Mod>? lastFiltered) && ReferenceEquals(raw, lastFiltered))
        {
            return;
        }

        List<Mod> filtered;
        if (raw is IEnumerable<Mod> current)
        {
            filtered = FilterModsForOptionsGrid(current).ToList();
        }
        else
        {
            filtered = FilterModsForOptionsGrid(
                    LoadedModManager.ModHandles.Where(m => !m.SettingsCategory().NullOrEmpty()))
                .OrderBy(m => m.SettingsCategory(), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        CachedModsWithSettingsField.SetValue(instance, filtered);
        filteredDialogs.AddOrUpdate(instance, filtered);
    }

    [HarmonyPatch(typeof(Dialog_Options), MethodType.Constructor)]
    [HarmonyPostfix]
    public static void Ctor_Default_Postfix(Dialog_Options __instance) => ApplyFilter(__instance);

    [HarmonyPatch(typeof(Dialog_Options), MethodType.Constructor, new[] { typeof(OptionCategoryDef) })]
    [HarmonyPostfix]
    public static void Ctor_Category_Postfix(Dialog_Options __instance) => ApplyFilter(__instance);

    [HarmonyPatch(typeof(Dialog_Options), "DoModOptions")]
    [HarmonyPrefix]
    public static void DoModOptions_Prefix(Dialog_Options __instance) => ApplyFilter(__instance);
}
