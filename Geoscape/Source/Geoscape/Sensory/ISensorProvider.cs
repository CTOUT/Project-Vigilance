using RimWorld.Planet;
using UnityEngine;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Contract implemented by any sensor source (colonies, caravans, listening posts, radar dishes).
/// </summary>
public interface ISensorProvider
{
    /// <summary>
    /// The backing world object providing this sensor (e.g. Colony settlement, Caravan, or Radar facility), if any.
    /// </summary>
    WorldObject? Source { get; }

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
    /// Maximum optical/electronic classification and identification radius in WorldGrid hex tiles.
    /// Within this inner horizon, targets are identified up to MaxIntelligenceTier.
    /// </summary>
    int IdentificationRadiusInTiles => DetectionRadiusInTiles > 0f ? Mathf.Max(1, Mathf.RoundToInt(DetectionRadiusInTiles * 0.45f)) : 0;

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
    /// Indicates whether this sensor represents an electronic, radar, or satellite system
    /// rather than biological lookouts, watchtowers, or field scouts.
    /// </summary>
    bool IsTechnological => false;

    /// <summary>
    /// Indicates whether this sensor can penetrate terrain masking (e.g. mountain shadows or deep canyons).
    /// </summary>
    bool PenetratesTerrainMasking => false;

    /// <summary>
    /// Indicates whether this sensor is an oceanic sonar or hydrophone system.
    /// </summary>
    bool IsOceanicSonar => false;

    /// <summary>
    /// Evaluates whether this sensor can detect the specified target.
    /// </summary>
    bool CanDetect(IDetectableTarget target);
}
