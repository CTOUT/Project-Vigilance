namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Visualization styles for active planetary sensor and radar coverage on the Geoscape globe.
/// </summary>
public enum RadarOutlineStyle
{
    /// <summary>
    /// Renders both the outer perimeter boundary ring and a subtle internal honeycomb grid,
    /// matching the holographic command center display in the Project Vigilance preview branding.
    /// </summary>
    BoundaryAndGrid = 0,

    /// <summary>
    /// Renders only the outer perimeter boundary ring around active detection envelopes.
    /// </summary>
    BoundaryOnly = 1,

    /// <summary>
    /// Radar outlines disabled; globe displays plain terrain without geometric sensory overlays.
    /// </summary>
    None = 2
}

/// <summary>
/// Selectable color palettes for tactical radar ranges and sensor overlays.
/// </summary>
public enum RadarColorPalette
{
    /// <summary>
    /// Holographic tactical cyan (#00E5FF) - matching the Project Vigilance preview and sci-fi HUDs.
    /// </summary>
    TacticalCyan = 0,

    /// <summary>
    /// Warm surveillance amber (#FFC107) - military command center radar aesthetic.
    /// </summary>
    SurveillanceAmber = 1,

    /// <summary>
    /// Military phosphor emerald (#00E676) - classic green CRT radar sweep look.
    /// </summary>
    EmeraldPhosphor = 2,

    /// <summary>
    /// Threat perimeter crimson (#FF1744) - high-visibility tactical alert border.
    /// </summary>
    CrimsonAlert = 3,

    /// <summary>
    /// Neutral monochrome HUD white (#ECEFF1) - sleek clean aesthetic.
    /// </summary>
    GhostWhite = 4
}
