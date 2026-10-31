using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Utility methods for evaluating planetary terrain elevation, hilliness, vantage bonuses,
/// and tactical terrain masking under Model B+ ("Vantage &amp; Masking").
/// </summary>
public static class SensoryTerrainUtility
{
    /// <summary>
    /// Optional override for target masking evaluation in tests / benchmarks without world grid.
    /// </summary>
    public static System.Func<PlanetTile, bool>? TargetMaskedOverride { get; set; }

    /// <summary>
    /// Optional override for vantage bonus evaluation in tests / benchmarks without world grid.
    /// </summary>
    public static System.Func<PlanetTile, int>? VantageBonusOverride { get; set; }

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
        if (VantageBonusOverride != null)
        {
            return VantageBonusOverride(tile);
        }

        if (!tile.Valid || Find.World?.grid == null)
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
    /// Calculates the radial traversal-ring identification vantage bonus granted to sensors
    /// stationed on elevated topography.
    /// Flat: +0 rings (baseline).
    /// SmallHills / LargeHills: +1 ring.
    /// Mountainous / Impassable: +2 rings.
    /// </summary>
    public static int GetIdentificationVantageBonus(int vantageBonus)
    {
        return vantageBonus switch
        {
            >= 3 => 2,
            >= 1 => 1,
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
        if (TargetMaskedOverride != null)
        {
            return TargetMaskedOverride(tile);
        }

        if (!tile.Valid || Find.World?.grid == null)
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
            // Close-range sightlines (travDist <= 1, immediate hex and adjacent) cannot be concealed by mountain masking.
            // Distant rugged valleys (travDist > 1) delay classification until entering open ground or penetrated by SIGINT.
            if (isTargetMasked && !sensor.PenetratesTerrainMasking && travDist > 1)
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

