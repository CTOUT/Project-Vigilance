namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Selectable planetary visibility models balancing realism, discovery, and player choice.
/// </summary>
public enum VisibilityMode
{
    /// <summary>
    /// Planetary geography (continents, oceans, cities) is known from orbital mapping.
    /// Dynamic military movement, raids, and incursions are hidden unless within active sensor coverage.
    /// </summary>
    PlanetarySurveillance = 0,

    /// <summary>
    /// 3-Tier RTS Fog of War: Unexplored tiles are dark, explored unmonitored tiles show a translucent grey haze,
    /// and active vision reveals live targets.
    /// </summary>
    ClassicHazeOfWar = 1,

    /// <summary>
    /// All planetary movement and incursions are visible at all times across the globe without fog or radar.
    /// </summary>
    Omniscient = 2
}
