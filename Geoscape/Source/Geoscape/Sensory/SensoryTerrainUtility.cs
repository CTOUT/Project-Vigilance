using RimWorld.Planet;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Utility methods for evaluating planetary terrain elevation, hilliness, vantage bonuses,
/// and tactical terrain masking under Model B+ ("Vantage &amp; Masking").
/// </summary>
public static class SensoryTerrainUtility
{
    /// <summary>
    /// Calculates the radial traversal-ring vantage bonus granted to optical or forward sensors
    /// stationed on elevated topography.
    /// Flat: +0 rings (baseline).
    /// SmallHills: +1 ring.
    /// LargeHills: +2 rings.
    /// Mountainous / Impassable: +3 rings.
    /// </summary>
    public static int GetVantageBonus(PlanetTile tile)
    {
        if (!tile.Valid)
        {
            return 0;
        }

        Tile tileData = tile.Tile;
        if (tileData == null)
        {
            return 0;
        }

        return tileData.hilliness switch
        {
            Hilliness.SmallHills => 1,
            Hilliness.LargeHills => 2,
            Hilliness.Mountainous => 3,
            Hilliness.Impassable => 3,
            _ => 0
        };
    }

    /// <summary>
    /// Evaluates whether a target tile provides tactical terrain masking against identification.
    /// Incursions travelling through rugged Mountainous or Impassable terrain navigate deep valleys
    /// and canyons, delaying full optical/electronic classification until they exit into foothills or plains,
    /// or unless scanned by an installation with terrain penetration capabilities (Spacer Phased Array + SIGINT).
    /// </summary>
    public static bool IsTargetMasked(PlanetTile tile)
    {
        if (!tile.Valid)
        {
            return false;
        }

        Tile tileData = tile.Tile;
        if (tileData == null)
        {
            return false;
        }

        return tileData.hilliness == Hilliness.Mountainous || tileData.hilliness == Hilliness.Impassable;
    }

    /// <summary>
    /// Evaluates the resulting intelligence tier granted by a sensor against a target
    /// at a given traversal ring distance, accounting for identification radius,
    /// tactical terrain masking, and penetration capabilities.
    /// </summary>
    public static IntelligenceTier EvaluateSensorTier(ISensorProvider sensor, int travDist, bool isTargetMasked)
    {
        int idSteps = sensor.IdentificationRadiusInTiles;
        if (idSteps > 0 && travDist <= idSteps)
        {
            if (isTargetMasked && !sensor.PenetratesTerrainMasking)
            {
                return (sensor.MaxIntelligenceTier >= IntelligenceTier.Contact)
                    ? IntelligenceTier.Contact
                    : sensor.MaxIntelligenceTier;
            }

            return sensor.MaxIntelligenceTier;
        }

        return (sensor.MaxIntelligenceTier >= IntelligenceTier.Contact)
            ? IntelligenceTier.Contact
            : IntelligenceTier.Suspected;
    }
}

