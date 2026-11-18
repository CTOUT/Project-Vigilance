using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ProjectVigilance.Core.Migration;
using ProjectVigilance.Core.Modules;
using ProjectVigilance.Geoscape.Settings;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ProjectVigilance.Core;

/// <summary>
/// Root RimWorld Mod entry point for Project Vigilance - Core Framework.
/// Hosts the single unified "Project Vigilance" Mod Options container and registers savegame backwards compatibility converters.
/// </summary>
public class VigilanceCoreMod : Mod
{
    public const string PackageId = "ProjectVigilance.Core";
    private static bool compatibilityRegistered;

    private static string currentTabKey = string.Empty;
    private static readonly Dictionary<string, Vector2> tabScrolls = new();
    private static readonly Dictionary<string, float> tabHeights = new();

    public VigilanceCoreMod(ModContentPack content) : base(content)
    {
        VigilanceModuleManager.RegisterActiveModule(VigilanceModuleId.Core);
        RegisterBackCompatibilityConverter();

        Log.Message("[Project Vigilance] Initialized Core Framework. Unified options host & modular migration ready.");
    }

    public static void RegisterBackCompatibilityConverter()
    {
        if (compatibilityRegistered)
        {
            return;
        }
        compatibilityRegistered = true;

        try
        {
            FieldInfo? chainField = AccessTools.Field(typeof(BackCompatibility), "conversionChain");
            if (chainField?.GetValue(null) is List<BackCompatibilityConverter> chain)
            {
                bool exists = false;
                for (int i = 0; i < chain.Count; i++)
                {
                    if (chain[i] is BackCompatibilityConverter_VigilanceModular)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    chain.Insert(0, new BackCompatibilityConverter_VigilanceModular());
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[Project Vigilance] Could not register modular BackCompatibilityConverter: {ex.Message}");
        }
    }

    public override string SettingsCategory() => "Project Vigilance";

    public override void DoSettingsWindowContents(Rect inRect)
    {
        if (VigilanceModuleManager.DoUnifiedSettingsWindowContentsHook != null)
        {
            VigilanceModuleManager.DoUnifiedSettingsWindowContentsHook(inRect);
            return;
        }

        DrawFallbackUnifiedRegistryWindow(inRect);
    }

    public override void WriteSettings()
    {
        base.WriteSettings();
        VigilanceSettingsRegistry.NotifyWriteAllSettings();
    }

    private static void DrawFallbackUnifiedRegistryWindow(Rect inRect)
    {
        var tabs = VigilanceSettingsRegistry.RegisteredTabs;
        if (tabs.Count == 0)
        {
            Widgets.Label(inRect, "Project Vigilance Core Framework active. Enable Geoscape, Orbital, Aquatics, or Thermal tabs to configure options.");
            return;
        }

        if (VigilanceSettingsRegistry.PendingTabSelection != null)
        {
            currentTabKey = VigilanceSettingsRegistry.PendingTabSelection;
            VigilanceSettingsRegistry.ClearPendingSelection();
        }

        if (string.IsNullOrEmpty(currentTabKey) || !tabs.Any(t => t.TabKey == currentTabKey))
        {
            currentTabKey = tabs[0].TabKey;
        }

        Rect tabRowRect = new(inRect.x, inRect.y, inRect.width, 34f);
        float spacing = 6f;
        float tabWidth = (tabRowRect.width - (tabs.Count - 1) * spacing) / tabs.Count;

        for (int i = 0; i < tabs.Count; i++)
        {
            var tab = tabs[i];
            Rect btnRect = new(tabRowRect.x + i * (tabWidth + spacing), tabRowRect.y, tabWidth, 30f);
            string label = tab.TabLabelKey.CanTranslate() ? tab.TabLabelKey.Translate().ToString() : tab.FallbackTabLabel;

            if (currentTabKey == tab.TabKey)
            {
                Widgets.DrawHighlightSelected(btnRect);
                TextAnchor prev = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(btnRect, label);
                Text.Anchor = prev;
            }
            else if (Widgets.ButtonText(btnRect, label))
            {
                currentTabKey = tab.TabKey;
                SoundDefOf.Click?.PlayOneShotOnCamera();
            }
        }

        Widgets.DrawLineHorizontal(inRect.x, inRect.y + 36f, inRect.width);
        Rect contentRect = new(inRect.x, inRect.y + 40f, inRect.width, inRect.height - 40f);
        if (!tabScrolls.ContainsKey(currentTabKey)) tabScrolls[currentTabKey] = Vector2.zero;
        if (!tabHeights.ContainsKey(currentTabKey)) tabHeights[currentTabKey] = 480f;

        float requiredHeight = Mathf.Max(tabHeights[currentTabKey], contentRect.height);
        bool needsScroll = requiredHeight > contentRect.height;
        Rect viewRect = new(0f, 0f, contentRect.width - (needsScroll ? 16f : 0f), requiredHeight);

        Vector2 scroll = tabScrolls[currentTabKey];
        Widgets.BeginScrollView(contentRect, ref scroll, viewRect);
        tabScrolls[currentTabKey] = scroll;

        Listing_Standard listing = new() { maxOneColumn = true };
        listing.Begin(viewRect);
        var activeTab = tabs.FirstOrDefault(t => t.TabKey == currentTabKey) ?? tabs[0];
        activeTab.DoTabContents(listing, viewRect);
        tabHeights[currentTabKey] = Mathf.Max(listing.CurHeight + 20f, contentRect.height);
        listing.End();
        Widgets.EndScrollView();
    }
}

[StaticConstructorOnStartup]
internal static class VigilanceCoreStartup
{
    static VigilanceCoreStartup()
    {
        VigilanceModuleManager.RegisterActiveModule(VigilanceModuleId.Core);
        VigilanceCoreMod.RegisterBackCompatibilityConverter();
    }
}
