using System.Linq;
using System.Reflection;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Discrete reconnaissance fidelity states for a planetary tile under Haze of War.
/// </summary>
public enum TileIntelLevel
{
    Unexplored = 0,
    DetectedOnly = 1,
    Identified = 2
}

/// <summary>
/// Evaluates terrain reconnaissance fidelity and formats coarse vs. full planetary telemetry
/// for inspect panes, terrain tabs, and the Left-Alt cell inspector.
/// </summary>
public static class TerrainTelemetryUtility
{
    public const string SignatureDetectedNotice = "Unidentified landmark signature detected";

    private static readonly FieldInfo? PrefsDataField =
        typeof(Prefs).GetField("data", BindingFlags.Static | BindingFlags.NonPublic);

    /// <summary>
    /// Computes the pure <see cref="TileIntelLevel"/> from explicit visibility flags for unit-testable telemetry gating.
    /// </summary>
    public static TileIntelLevel GetTileIntelLevel(VisibilityMode mode, bool isExplored, bool isIdentified)
    {
        if (mode != VisibilityMode.ClassicHazeOfWar)
        {
            return TileIntelLevel.Identified;
        }

        if (!isExplored)
        {
            return TileIntelLevel.Unexplored;
        }

        return isIdentified ? TileIntelLevel.Identified : TileIntelLevel.DetectedOnly;
    }

    /// <summary>
    /// Returns the reconnaissance fidelity level of <paramref name="tile"/> under current campaign rules.
    /// </summary>
    public static TileIntelLevel GetTileIntelLevel(PlanetTile tile)
    {
        if (GeoscapeRules.VisibilityMode != VisibilityMode.ClassicHazeOfWar)
        {
            return TileIntelLevel.Identified;
        }

        if (!tile.Valid)
        {
            return TileIntelLevel.Unexplored;
        }

        SensoryManager? sensory = SensoryManager.Instance;
        if (sensory == null && SensoryManager.IsExploredOverride == null && SensoryManager.IsIdentifiedOverride == null)
        {
            return TileIntelLevel.Identified;
        }

        bool explored = StartingSiteFeedbackUtility.IsTileExplored(tile);
        bool identified = sensory != null
            ? sensory.IsTileIdentified(tile)
            : (SensoryManager.IsIdentifiedOverride?.Invoke(tile) ?? explored);

        return GetTileIntelLevel(GeoscapeRules.VisibilityMode, explored, identified);
    }

    /// <summary>
    /// Returns true if an explored-but-unidentified tile hosts a man-made or anomalous Odyssey landmark
    /// whose identity must stay hidden behind a coarse signature notice.
    /// </summary>
    public static bool HasUnidentifiedManMadeLandmark(PlanetTile tile)
    {
        if (!tile.Valid ||
            Find.World?.landmarks?.landmarks == null ||
            Find.World.landmarks.landmarks.Count == 0 ||
            !ModsConfig.OdysseyActive)
        {
            return false;
        }

        if (!Find.World.landmarks.landmarks.TryGetValue(tile, out Landmark? landmark) || landmark?.def == null)
        {
            return false;
        }

        if (!LandmarkSensoryUtility.IsManMadeLandmarkDef(landmark.def.defName))
        {
            return false;
        }

        return GetTileIntelLevel(tile) == TileIntelLevel.DetectedOnly;
    }

    /// <summary>
    /// Rounds a display-unit temperature value to the nearest multiple of <paramref name="step"/>,
    /// normalising negative zero to positive zero.
    /// </summary>
    public static float RoundTemperatureToNearestStep(float temperatureInDisplayUnits, int step = 5)
    {
        if (step <= 1)
        {
            float unitRounded = Mathf.Round(temperatureInDisplayUnits);
            return unitRounded == 0f ? 0f : unitRounded;
        }

        float rounded = Mathf.Round(temperatureInDisplayUnits / step) * step;
        return rounded == 0f ? 0f : rounded;
    }

    /// <summary>
    /// Formats a Celsius temperature into an imprecise 5-degree-rounded estimate for outer-ring reconnaissance
    /// (e.g., "~5C", "~40F", "~280K") without decimal precision or seasonal min/max bounds.
    /// </summary>
    public static string FormatEstimatedTemperatureFromCelsius(float celsiusTemp, TemperatureDisplayMode? modeOverride = null)
    {
        TemperatureDisplayMode mode = modeOverride ?? (PrefsDataField?.GetValue(null) != null ? Prefs.TemperatureMode : TemperatureDisplayMode.Celsius);
        float converted = GenTemperature.CelsiusTo(celsiusTemp, mode);
        float rounded = RoundTemperatureToNearestStep(converted, step: 5);
        string unit = mode switch
        {
            TemperatureDisplayMode.Fahrenheit => "F",
            TemperatureDisplayMode.Kelvin => "K",
            _ => "C"
        };

        return "~" + rounded.ToString("F0") + unit;
    }

    /// <summary>
    /// Formats the coarse estimated surface temperature for <paramref name="tile"/>.
    /// </summary>
    public static string FormatEstimatedTemperature(PlanetTile tile, TemperatureDisplayMode? modeOverride = null)
    {
        float celsiusTemp = tile.Valid && Find.WorldGrid != null ? Find.WorldGrid[tile].temperature : 21f;
        return FormatEstimatedTemperatureFromCelsius(celsiusTemp, modeOverride);
    }

    /// <summary>
    /// Builds the coarse inspect-pane summary for a <see cref="TileIntelLevel.DetectedOnly"/> tile,
    /// including coordinates, hilliness, surface roads/rivers, 5-degree estimated temperature,
    /// and a signature notice when a hidden man-made landmark is present.
    /// </summary>
    public static string BuildDetectedTileInspectString(PlanetTile tile)
    {
        if (!tile.Valid || Find.WorldGrid == null)
        {
            return string.Empty;
        }

        Tile? tileData = Find.WorldGrid[tile];
        if (tileData == null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        Vector2 longLat = Find.WorldGrid.LongLatOf(tile);
        sb.Append(longLat.y.ToStringLatitude());
        sb.Append(' ');
        sb.Append(longLat.x.ToStringLongitude());

        if (!tileData.PrimaryBiome.impassable && tileData.hilliness != Hilliness.Undefined)
        {
            sb.AppendLine();
            sb.Append(tileData.hilliness.GetLabelCap());
        }

        if (tileData is SurfaceTile surfaceTile)
        {
            if (!surfaceTile.Roads.NullOrEmpty())
            {
                RoadDef? primaryRoad = surfaceTile.Roads.Select(rl => rl.road).MaxBy(r => r.priority);
                if (primaryRoad != null)
                {
                    sb.AppendLine();
                    sb.Append(primaryRoad.LabelCap);
                }
            }

            if (!surfaceTile.Rivers.NullOrEmpty() && surfaceTile.Rivers[0].river != null)
            {
                sb.AppendLine();
                sb.Append(surfaceTile.Rivers[0].river.LabelCap);
            }
        }

        sb.AppendLine();
        sb.Append("Estimated temperature: ");
        sb.Append(FormatEstimatedTemperature(tile));

        if (HasUnidentifiedManMadeLandmark(tile))
        {
            sb.AppendLine();
            sb.Append(SignatureDetectedNotice);
        }

        return sb.ToString();
    }
}
