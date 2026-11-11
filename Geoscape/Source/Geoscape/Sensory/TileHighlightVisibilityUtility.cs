using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Controls visibility of vanilla single-tile highlight overlays (<see cref="WorldDrawLayer_MouseTile"/> and
/// <see cref="WorldDrawLayer_SelectedTile"/>) so uncharted shroud never reveals geodesic tile geometry during gameplay.
/// </summary>
public static class TileHighlightVisibilityUtility
{
    /// <summary>
    /// Optional test override replacing <see cref="Current.ProgramState"/> checks in headless unit tests.
    /// Always reset to <see langword="null"/> during test cleanup.
    /// </summary>
    internal static bool? IsPlayingOverride { get; set; }

    /// <summary>
    /// Determines whether a single-tile highlight ring should render for the supplied state inputs.
    /// Invalid tiles, non-fog visibility modes (<see cref="VisibilityMode.PlanetarySurveillance"/> and
    /// <see cref="VisibilityMode.Omniscient"/>), and non-playing states (<see cref="ProgramState.Entry"/>)
    /// pass through unmodified so manual starting-site selection remains visible.
    /// </summary>
    public static bool ShouldRenderSingleTileHighlight(
        PlanetTile tile,
        VisibilityMode mode,
        bool isPlaying,
        bool isExplored)
    {
        if (!tile.Valid || mode != VisibilityMode.FogOfWar || !isPlaying)
        {
            return true;
        }

        return isExplored;
    }

    /// <summary>
    /// Evaluates live single-tile highlight visibility against active campaign visibility rules,
    /// <see cref="Current.ProgramState"/>, and tile exploration state.
    /// </summary>
    public static bool ShouldRenderSingleTileHighlight(PlanetTile tile)
    {
        VisibilityMode mode = GeoscapeRules.VisibilityMode;
        bool isPlaying = IsPlayingOverride ?? (Current.ProgramState == ProgramState.Playing);
        bool isExplored = tile.Valid
            && mode == VisibilityMode.FogOfWar
            && isPlaying
            && StartingSiteFeedbackUtility.IsTileExplored(tile);

        return ShouldRenderSingleTileHighlight(tile, mode, isPlaying, isExplored);
    }

    /// <summary>
    /// Filters a <see cref="WorldDrawLayer_SingleTile"/> highlight target, returning <see cref="PlanetTile.Invalid"/>
    /// when hidden beneath unexplored shroud while keeping <see cref="WorldSelector.SelectedTile"/> intact for
    /// inspect panes and blind caravan orders.
    /// </summary>
    public static PlanetTile FilterHighlightTile(PlanetTile tile)
    {
        return ShouldRenderSingleTileHighlight(tile) ? tile : PlanetTile.Invalid;
    }
}
