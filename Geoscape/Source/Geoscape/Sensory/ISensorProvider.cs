using RimWorld.Planet;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Contract implemented by any sensor source (colonies, caravans, listening posts, radar dishes).
/// </summary>
public interface ISensorProvider
{
    /// <summary>
    /// Display label of the sensor installation or scouting unit.
    /// </summary>
    string SensorLabel { get; }

    /// <summary>
    /// Planetary tile where the sensor is positioned.
    /// </summary>
    PlanetTile SensorTile { get; }

    /// <summary>
    /// Maximum detection radius in WorldGrid hex tiles.
    /// </summary>
    float DetectionRadiusInTiles { get; }

    /// <summary>
    /// The highest intelligence tier this sensor can resolve (e.g. basic lookouts can only provide Contact/Classified, whereas radar decoders can provide Analysed).
    /// </summary>
    IntelligenceTier MaxIntelligenceTier { get; }

    /// <summary>
    /// Whether the sensor is actively operational (e.g., powered, manned, not destroyed).
    /// </summary>
    bool IsSensorActive { get; }

    /// <summary>
    /// Whether this sensor is fixed in place (e.g. colony, listening post) or mobile (e.g. caravan, aerial scout).
    /// Used by sensory caching to skip redundant tile flood-filling.
    /// </summary>
    bool IsStationary { get; }

    /// <summary>
    /// Evaluates whether this sensor can detect the specified target.
    /// </summary>
    bool CanDetect(IDetectableTarget target);
}
