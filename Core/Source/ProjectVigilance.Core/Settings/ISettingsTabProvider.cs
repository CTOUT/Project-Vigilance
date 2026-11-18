using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Settings;

/// <summary>
/// Defines a modular settings tab for the unified Project Vigilance options interface.
/// </summary>
public interface ISettingsTabProvider
{
    /// <summary>
    /// Unique identifier for this tab (e.g. "Sensory", "Radar", "Incursions", "Aquatics").
    /// </summary>
    string TabKey { get; }

    /// <summary>
    /// Keyed translation key for tab label (e.g. "PV_Settings_Tab_Sensory").
    /// </summary>
    string TabLabelKey { get; }

    /// <summary>
    /// Default English label displayed if translation key is missing.
    /// </summary>
    string FallbackTabLabel { get; }

    /// <summary>
    /// Order in which the tab appears in the unified header (lower values appear first).
    /// </summary>
    int SortOrder { get; }

    /// <summary>
    /// Renders the scrollable tab contents.
    /// </summary>
    void DoTabContents(Listing_Standard listing, Rect inRect);
}
