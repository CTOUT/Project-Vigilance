using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectVigilance.Geoscape.Settings;

/// <summary>
/// Registry for dynamic mod settings tab providers within the unified Project Vigilance options window.
/// </summary>
public static class VigilanceSettingsRegistry
{
    private static readonly object syncLock = new();
    private static readonly List<ISettingsTabProvider> tabProviders = new();
    private static string? pendingTabSelection;

    public static event Action<string>? OnTabSelectionRequested;

    public static IReadOnlyList<ISettingsTabProvider> RegisteredTabs
    {
        get
        {
            lock (syncLock)
            {
                return tabProviders.OrderBy(t => t.SortOrder).ToList();
            }
        }
    }

    public static string? PendingTabSelection
    {
        get
        {
            lock (syncLock)
            {
                return pendingTabSelection;
            }
        }
    }

    public static void RegisterTab(ISettingsTabProvider provider)
    {
        if (provider == null) return;
        lock (syncLock)
        {
            if (!tabProviders.Any(t => t.TabKey == provider.TabKey))
            {
                tabProviders.Add(provider);
            }
        }
    }

    public static void UnregisterTab(string tabKey)
    {
        lock (syncLock)
        {
            tabProviders.RemoveAll(t => t.TabKey == tabKey);
        }
    }

    public static void Clear()
    {
        lock (syncLock)
        {
            tabProviders.Clear();
            pendingTabSelection = null;
        }
    }

    /// <summary>
    /// Pre-selects a specific tab when the settings window opens.
    /// Used when redirecting from a sub-mod's settings entry.
    /// </summary>
    public static void RequestTabSelection(string tabKey)
    {
        lock (syncLock)
        {
            pendingTabSelection = tabKey;
        }
        OnTabSelectionRequested?.Invoke(tabKey);
    }

    public static void ClearPendingSelection()
    {
        lock (syncLock)
        {
            pendingTabSelection = null;
        }
    }
}
