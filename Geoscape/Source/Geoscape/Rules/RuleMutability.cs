namespace ProjectVigilance.Geoscape.Rules;

/// <summary>
/// Declares how and when a campaign rule may change once a campaign exists.
/// </summary>
public enum RuleMutability
{
    /// <summary>
    /// Freely changeable at any time without compromising simulation outcomes.
    /// (Presentation preferences, audio-visual options, and notification filters).
    /// </summary>
    Mutable,

    /// <summary>
    /// May only move forward along an explicit information/knowledge rank.
    /// (Information can be safely revealed; it cannot be cleanly unrevealed).
    /// </summary>
    Escalatable,

    /// <summary>
    /// Locked once the campaign exists.
    /// (Physical formulas, biological laws, tactical AI heuristics, and storyteller cadence).
    /// </summary>
    Immutable
}
