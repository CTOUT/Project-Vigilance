namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Represents the level of intelligence and telemetry known about a planetary contact.
/// </summary>
public enum IntelligenceTier
{
    /// <summary>
    /// Target is completely outside sensor coverage and unknown to the player.
    /// </summary>
    Undetected = 0,

    /// <summary>
    /// Target is indirectly suspected through distant cues (smoke, rumours, acoustic anomalies).
    /// </summary>
    Suspected = 1,

    /// <summary>
    /// Confirmed radar track or visual silhouette. Hex position, heading, and coarse speed known.
    /// </summary>
    Contact = 2,

    /// <summary>
    /// Faction identity and broad combat force classification known.
    /// </summary>
    Classified = 3,

    /// <summary>
    /// Full tactical breakdown: exact pawn headcount, weaponry, siege gear, target colony, and live arrival ETA.
    /// </summary>
    Analysed = 4
}
