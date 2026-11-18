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
