using UnityEngine;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Curated colour theme presets for tactical radar, sonar, and scout sensory perimeters.
/// Designed for high visual clarity and colourblind accessibility (WCAG AA compliant).
/// </summary>
public enum SensoryTheme
{
    TacticalCyan,
    AmberCRT,
    EmeraldPhosphor,
    CrimsonAlert,
    GhostWhite,
    HighContrast,
    ProtanopiaDeuteranopia,
    Tritanopia
}

/// <summary>
/// Helper utility providing standard palette colours for each sensory theme.
/// </summary>
public static class SensoryThemeUtility
{
    public static (Color radar, Color identification, Color scout, Color sonar) GetThemeColors(SensoryTheme theme)
    {
        return theme switch
        {
            SensoryTheme.TacticalCyan => (
                new Color(0f, 0.90f, 1.0f),       // Cyan
                new Color(1.0f, 0.82f, 0.15f),    // Gold
                new Color(1.0f, 0.75f, 0.20f),    // Amber
                new Color(0.12f, 0.45f, 0.85f)    // Cobalt
            ),
            SensoryTheme.AmberCRT => (
                new Color(1.0f, 0.70f, 0.10f),    // Warm Amber
                new Color(1.0f, 0.88f, 0.35f),    // Bright Amber
                new Color(0.95f, 0.85f, 0.40f),   // Pale Yellow
                new Color(0.65f, 0.42f, 0.15f)    // Bronze
            ),
            SensoryTheme.EmeraldPhosphor => (
                new Color(0.15f, 0.95f, 0.35f),   // Classic Green
                new Color(0.35f, 1.0f, 0.65f),    // Mint
                new Color(0.70f, 1.0f, 0.25f),    // Lime
                new Color(0.10f, 0.55f, 0.45f)    // Dark Teal
            ),
            SensoryTheme.CrimsonAlert => (
                new Color(1.0f, 0.25f, 0.15f),    // Blood Orange
                new Color(1.0f, 0.50f, 0.40f),    // Coral
                new Color(1.0f, 0.35f, 0.25f),    // Scarlet
                new Color(0.65f, 0.12f, 0.20f)    // Dark Ruby
            ),
            SensoryTheme.GhostWhite => (
                new Color(0.85f, 0.90f, 0.95f),   // Slate White
                new Color(0.95f, 0.95f, 0.95f),   // Pure White
                new Color(0.80f, 0.82f, 0.85f),   // Silver
                new Color(0.50f, 0.55f, 0.60f)    // Ash Grey
            ),
            SensoryTheme.HighContrast => (
                new Color(1.0f, 1.0f, 1.0f),       // Bright White
                new Color(0f, 1.0f, 1.0f),        // Cyan
                new Color(1.0f, 1.0f, 0.2f),      // Bright Yellow
                new Color(0.3f, 0.6f, 1.0f)       // Electric Blue
            ),
            SensoryTheme.ProtanopiaDeuteranopia => (
                new Color(0.15f, 0.70f, 1.0f),    // High-Luminance Blue
                new Color(1.0f, 0.85f, 0.10f),    // Yellow
                new Color(1.0f, 0.90f, 0.30f),    // Bright Sand
                new Color(0.05f, 0.40f, 0.85f)    // Deep Azure
            ),
            SensoryTheme.Tritanopia => (
                new Color(0.95f, 0.20f, 0.70f),   // Magenta
                new Color(0.15f, 0.90f, 0.85f),   // Teal
                new Color(0.95f, 0.40f, 0.80f),   // Pink
                new Color(0.10f, 0.65f, 0.70f)    // Cyan Sonar
            ),
            _ => (
                new Color(0f, 0.90f, 1.0f),
                new Color(1.0f, 0.82f, 0.15f),
                new Color(1.0f, 0.75f, 0.20f),
                new Color(0.12f, 0.45f, 0.85f)
            )
        };
    }
}
