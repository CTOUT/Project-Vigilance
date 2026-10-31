using RimWorld;
using RimWorld.Planet;
using UnityEngine;

namespace ProjectVigilance.Geoscape.Orbital;

/// <summary>
/// Unifying interface implemented by all orbital actors (satellites, orbital stations, and future orbital vessels).
/// Allows mobile actors such as gravships, shuttles, and interceptors to interface with station service areas,
/// orbital logistics, and drifting flight dynamics without architectural coupling.
/// </summary>
public interface IOrbitalEntity
{
    /// <summary>
    /// Identifier or callsign displayed in orbital telemetry and communication consoles.
    /// </summary>
    string OrbitalCallsign { get; }

    /// <summary>
    /// The faction controlling this orbital entity.
    /// </summary>
    Faction? Faction { get; }

    /// <summary>
    /// The Keplerian flight parameters governing position, precession, and trajectory drift.
    /// </summary>
    OrbitalParameters? Orbit { get; }

    /// <summary>
    /// The surface tile directly beneath this orbital entity.
    /// </summary>
    PlanetTile CurrentSubSatelliteTile { get; }

    /// <summary>
    /// 3D position vector on the world rendering sphere.
    /// </summary>
    Vector3 CurrentOrbitalPosition { get; }

    /// <summary>
    /// Whether this entity is currently within the service/maintenance zone of its parent orbital station.
    /// </summary>
    bool InServiceArea { get; }

    /// <summary>
    /// Unique WorldObject ID of the parent orbital station, or -1 if operating fully independently.
    /// </summary>
    int ParentStationId { get; }

    /// <summary>
    /// Invoked when this entity moves into proximity of its parent station's service corridor.
    /// </summary>
    void OnEnterServiceArea(WorldObject_OrbitalStation station);

    /// <summary>
    /// Invoked when orbital drift or course adjustments take this entity outside the station's service corridor.
    /// </summary>
    void OnLeaveServiceArea(WorldObject_OrbitalStation station);
}
