using System;
using RimWorld.Planet;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Utility methods managing starting site feedback, telemetry masking, and hazard warning suppression
/// for uncharted worlds under Fog of War.
/// </summary>
public static class StartingSiteFeedbackUtility
{
    /// <summary>
    /// Ambiguous telemetry rejection displayed when a player attempts to embark on an unexplored/fogged tile
    /// that cannot physically support a colony (e.g. ocean, impassable mountains).
    /// Prevents 'sonar probing' coastlines and mountain chains prior to touchdown.
    /// </summary>
    public const string AmbiguousLandingRejectionMessage = "Unviable landing coordinates: Selected sector cannot support a colony drop.";

    /// <summary>
    /// Evaluates whether the specified planetary tile is explored under the active campaign rules.
    /// In FogOfWar mode, unexplored tiles return false. In PlanetarySurveillance and Omniscient modes, returns true.
    /// </summary>
    public static bool IsTileExplored(PlanetTile tile)
    {
        if (SensoryManager.IsExploredOverride != null)
        {
            return SensoryManager.IsExploredOverride(tile);
        }

        if (GeoscapeRules.VisibilityMode != VisibilityMode.FogOfWar)
        {
            return true;
        }

        if (!tile.Valid)
        {
            return false;
        }

        if (SensoryManager.Instance != null)
        {
            return SensoryManager.Instance.IsTileIdExplored(tile.tileId);
        }

        return false;
    }

    /// <summary>
    /// Determines whether explicit terrain rejection reasons (such as "Cannot land on ocean" or
    /// "Cannot land on impassable mountains") should be masked with ambiguous telemetry feedback.
    /// </summary>
    public static bool ShouldMaskRejectionReason(PlanetTile tile)
    {
        if (!tile.Valid || GeoscapeRules.VisibilityMode != VisibilityMode.FogOfWar)
        {
            return false;
        }

        return !IsTileExplored(tile);
    }

    /// <summary>
    /// Determines whether settlement confirmation dialogs (warning about extreme temperatures, toxic pollution,
    /// or harsh biomes) should be suppressed so the landing proceeds blind into the unknown sector.
    /// </summary>
    public static bool ShouldSuppressHazardConfirmation(PlanetTile tile)
    {
        if (!tile.Valid || GeoscapeRules.VisibilityMode != VisibilityMode.FogOfWar)
        {
            return false;
        }

        return !IsTileExplored(tile);
    }
}
