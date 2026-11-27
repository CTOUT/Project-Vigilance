using System;
using ProjectVigilance.Core.Modules;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Provides unified distance evaluation and target detection logic across all sensor providers.
/// Eliminates code duplication across ISensorProvider default implementations, CompRadar,
/// ScoutHorizonSensor, and WorldObject_ListeningPost.
/// </summary>
public static class SensorDetectionUtility
{
    public static bool TryGetDetectionDistance(ISensorProvider sensor, IDetectableTarget target, out int distance)
    {
        distance = -1;
        if (sensor == null || !sensor.IsSensorActive || !sensor.SensorTile.Valid || target == null || !target.CurrentTile.Valid)
        {
            return false;
        }

        if (Find.World?.grid != null && sensor.SensorTile.Layer != target.CurrentTile.Layer)
        {
            return false;
        }

        if (sensor.IsOceanicSonar)
        {
            if (!SonarTraversalUtility.IsTileWaterCovered(target.CurrentTile))
            {
                return false;
            }

            int sonarMaxSteps = Mathf.Max(1, Mathf.RoundToInt(sensor.DetectionRadiusInTiles));
            if (SonarTraversalUtility.TryGetSonarDetectionDistance(sensor.SensorTile, target.CurrentTile, sonarMaxSteps, out int sonarDist))
            {
                distance = sonarDist;
                return true;
            }

            return false;
        }

        int maxSteps = Mathf.Max(1, Mathf.RoundToInt(sensor.DetectionRadiusInTiles));
        int d;
        if (VigilanceModuleManager.DistanceCalculationOverrideHook != null)
        {
            d = VigilanceModuleManager.DistanceCalculationOverrideHook(sensor.SensorTile, target.CurrentTile, maxSteps);
        }
        else if (Find.World?.grid != null)
        {
            // Fast spherical reject avoids costly BFS flood-fill when target is well beyond sensor horizon
            if (Find.World.grid.ApproxDistanceInTiles(sensor.SensorTile, target.CurrentTile) > maxSteps + 2f)
            {
                return false;
            }
            d = Find.World.grid.TraversalDistanceBetween(sensor.SensorTile, target.CurrentTile, passImpassable: true, maxDist: maxSteps);
        }
        else
        {
            d = Math.Abs(sensor.SensorTile.tileId - target.CurrentTile.tileId);
        }

        if (d <= maxSteps)
        {
            distance = d;
            return true;
        }

        return false;
    }
}
