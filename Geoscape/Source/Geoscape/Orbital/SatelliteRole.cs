namespace ProjectVigilance.Geoscape.Orbital;

/// <summary>
/// Operational role and mission payload carried by a satellite in orbit.
/// Each role dictates distinct orbital inclination, period, footprint swath, and gameplay mechanics.
/// </summary>
public enum SatelliteRole
{
    /// <summary>
    /// Roving commercial platform. Provides trade windows to surface colonies with a powered Comms Console.
    /// </summary>
    Trade,

    /// <summary>
    /// Earth-observation sensory platform. Sweeps across latitudes clearing Haze/Fog of War and revealing hidden sites.
    /// </summary>
    Reconnaissance,

    /// <summary>
    /// Tactical surveillance and kinetic fire-control platform. Provides targeting data and suppresses hostile incursions.
    /// </summary>
    Military,

    /// <summary>
    /// Positioning and telemetry constellation node. Accelerates surface caravans passing beneath its footprint.
    /// </summary>
    Navigation,

    /// <summary>
    /// High-apogee communication relay. Connects isolated ground listening posts to the colony sensor grid across terrain and oceans.
    /// </summary>
    Communication
}
