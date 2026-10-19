using RimWorld;
using RimWorld.Planet;
using UnityEngine;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Contract implemented by any planetary entity that can be detected by sensors.
/// Decouples the detection framework from specific incursion or craft implementations.
/// </summary>
public interface IDetectableTarget
{
    /// <summary>
    /// Display label for notifications and telemetry.
    /// </summary>
    string TargetLabel { get; }

    /// <summary>
    /// The current planetary tile occupied by the target.
    /// </summary>
    PlanetTile CurrentTile { get; }

    /// <summary>
    /// Live 3D position of the target on the world sphere.
    /// </summary>
    Vector3 CurrentDrawPos { get; }

    /// <summary>
    /// Associated faction, if any.
    /// </summary>
    Faction? TargetFaction { get; }

    /// <summary>
    /// Whether this entity is hostile to the player colony.
    /// </summary>
    bool IsHostileToPlayer { get; }

    /// <summary>
    /// Current intelligence tier resolved by the sensory net.
    /// </summary>
    IntelligenceTier CurrentIntelligenceTier { get; set; }

    /// <summary>
    /// Callback invoked when a sensor detects this entity at a new or upgraded intelligence tier.
    /// </summary>
    void OnDetected(ISensorProvider sensor, IntelligenceTier newTier);
}
