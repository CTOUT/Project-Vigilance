namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Visualisation styles for mountain-masked radar shadows inside the planetary identification horizon envelope.
/// </summary>
public enum RadarTerrainShadowStyle
{
    /// <summary>
    /// No shadow decoration; globe displays standard terrain within the identification horizon without shadow markers.
    /// </summary>
    None = 0,

    /// <summary>
    /// Approach A: Renders dashed amber ribbons enclosing internal mountain clusters and radar shadows within identification range.
    /// </summary>
    DashedContours = 1,

    /// <summary>
    /// Approach C: Renders subtle radar static and diagonal scanline cell hatching across masked mountain hex surfaces within identification range.
    /// </summary>
    CellHatching = 2,

    /// <summary>
    /// Both Approach A and C: Displays dashed boundary contours and diagonal scanline cell hatching across masked mountain clusters.
    /// </summary>
    Both = 3
}
