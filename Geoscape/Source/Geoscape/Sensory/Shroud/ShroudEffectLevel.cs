namespace ProjectVigilance.Geoscape.Sensory
{
    /// <summary>
    /// Controls the atmospheric visual intensity applied across unexplored shroud surfaces
    /// and frontier boundaries on the Geoscape globe.
    /// </summary>
    public enum ShroudEffectLevel
    {
        /// <summary>
        /// Flat solid shroud fill without procedural noise or frontier glow modulation.
        /// </summary>
        Off = 0,

        /// <summary>
        /// Restrained nebula modulation and muted frontier glow calibrated for readability.
        /// </summary>
        Subtle = 1,

        /// <summary>
        /// Richer deep-nebula variation and stronger luminous frontier bloom.
        /// </summary>
        Atmospheric = 2
    }
}
