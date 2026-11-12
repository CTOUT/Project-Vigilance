using System;
using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Evaluates coastal adjacency and water-connected directional V-shaped sonar propagation.
/// Sonar only reveals and interacts with WaterCovered tiles, cannot traverse land, and
/// originates from a station's or settlement's adjacent WaterCovered tiles.
/// </summary>
public static class SonarTraversalUtility
{
    public static Func<PlanetTile, bool>? IsTileWaterCoveredOverride;
    public static Action<PlanetTile, List<PlanetTile>>? GetTileNeighborsOverride;
    public static Func<PlanetTile, Vector3>? GetTileCenterOverride;

    /// <summary>
    /// Cosine threshold for a directional V-shaped projection (~60 degrees half-angle per adjacent water tile).
    /// Adjacent water tiles separated by 60 degrees overlap seamlessly from shoreline to shoreline,
    /// while a full ring of 6 adjacent water tiles on an island covers 360 degrees.
    /// </summary>
    public const float DirectionalSectorCosThreshold = 0.45f;

    [ThreadStatic]
    private static List<PlanetTile>? tmpNeighbors;

    [ThreadStatic]
    private static List<PlanetTile>? tmpAdjacentWater;

    public static void ResetTestOverrides()
    {
        IsTileWaterCoveredOverride = null;
        GetTileNeighborsOverride = null;
        GetTileCenterOverride = null;
    }

    public static bool CanEvaluateWorldGrid()
    {
        return IsTileWaterCoveredOverride != null ||
               GetTileNeighborsOverride != null ||
               (Current.Game != null && Find.World != null && Find.WorldGrid != null);
    }

    public static bool IsTileWaterCovered(PlanetTile tile)
    {
        if (!tile.Valid)
        {
            return false;
        }

        if (IsTileWaterCoveredOverride != null)
        {
            return IsTileWaterCoveredOverride(tile);
        }

        if (Current.Game == null || Find.World == null || Find.WorldGrid == null)
        {
            return false;
        }

        try
        {
            Tile worldTile = Find.WorldGrid[tile];
            return worldTile != null && worldTile.WaterCovered;
        }
        catch
        {
            return false;
        }
    }

    public static void GetTileNeighbors(PlanetTile tile, List<PlanetTile> outNeighbors)
    {
        outNeighbors.Clear();
        if (!tile.Valid)
        {
            return;
        }

        if (GetTileNeighborsOverride != null)
        {
            GetTileNeighborsOverride(tile, outNeighbors);
            return;
        }

        if (Current.Game != null && Find.World != null)
        {
            Find.WorldGrid?.GetTileNeighbors(tile, outNeighbors);
        }
    }

    public static bool TryGetTileCenter(PlanetTile tile, out Vector3 center)
    {
        if (!tile.Valid)
        {
            center = Vector3.zero;
            return false;
        }

        if (GetTileCenterOverride != null)
        {
            center = GetTileCenterOverride(tile);
            return center.sqrMagnitude > 0.0001f;
        }

        if (Current.Game != null && Find.World != null && Find.WorldGrid != null)
        {
            try
            {
                center = Find.WorldGrid.GetTileCenter(tile);
                return center.sqrMagnitude > 0.0001f;
            }
            catch
            {
                center = Vector3.zero;
                return false;
            }
        }

        center = Vector3.zero;
        return false;
    }

    public static void GetAdjacentWaterTiles(PlanetTile stationTile, List<PlanetTile> outWaterNeighbors)
    {
        outWaterNeighbors.Clear();
        if (!stationTile.Valid)
        {
            return;
        }

        tmpNeighbors ??= new List<PlanetTile>(8);
        GetTileNeighbors(stationTile, tmpNeighbors);
        for (int i = 0; i < tmpNeighbors.Count; i++)
        {
            PlanetTile neighbor = tmpNeighbors[i];
            if (neighbor.Valid && IsTileWaterCovered(neighbor))
            {
                outWaterNeighbors.Add(neighbor);
            }
        }
    }

    /// <summary>
    /// Returns true when at least one adjacent Geoscape tile is WaterCovered.
    /// Both Automated Sonar Stations and Settlement Sonar Facilities require direct coastal access.
    /// </summary>
    public static bool HasCoastalAccess(PlanetTile stationTile)
    {
        if (!stationTile.Valid)
        {
            return false;
        }

        tmpAdjacentWater ??= new List<PlanetTile>(8);
        GetAdjacentWaterTiles(stationTile, tmpAdjacentWater);
        return tmpAdjacentWater.Count > 0;
    }

    /// <summary>
    /// Computes the merged water-only sonar coverage originating independently from each adjacent WaterCovered tile.
    /// Land acts as an acoustic barrier and blocks propagation; each adjacent water tile projects outward in a
    /// directional V-shaped sector over its connected body of water.
    /// </summary>
    public static void PopulateSonarCoverage(
        PlanetTile stationTile,
        int maxDetectionRadius,
        int maxIdentificationRadius,
        HashSet<PlanetTile> outDetectionTiles,
        HashSet<PlanetTile>? outIdentificationTiles = null,
        Dictionary<PlanetTile, int>? outMinSteps = null)
    {
        if (!stationTile.Valid || maxDetectionRadius <= 0)
        {
            return;
        }

        var adjacentWater = new List<PlanetTile>(8);
        GetAdjacentWaterTiles(stationTile, adjacentWater);
        if (adjacentWater.Count == 0)
        {
            return;
        }

        bool hasStationCenter = TryGetTileCenter(stationTile, out Vector3 stationCenter);
        Vector3 stationNormal = hasStationCenter && stationCenter.sqrMagnitude > 0.0001f
            ? stationCenter.normalized
            : Vector3.up;

        var queue = new Queue<(int tileId, int step)>();
        var sectorSteps = new Dictionary<int, int>();
        var neighbors = new List<PlanetTile>(8);

        for (int i = 0; i < adjacentWater.Count; i++)
        {
            PlanetTile originWaterTile = adjacentWater[i];
            bool hasSectorDir = false;
            Vector3 sectorDir = Vector3.zero;

            if (hasStationCenter && TryGetTileCenter(originWaterTile, out Vector3 originWaterCenter))
            {
                Vector3 projected = Vector3.ProjectOnPlane(originWaterCenter - stationCenter, stationNormal);
                if (projected.sqrMagnitude > 0.000001f)
                {
                    sectorDir = projected.normalized;
                    hasSectorDir = true;
                }
            }

            queue.Clear();
            sectorSteps.Clear();

            queue.Enqueue((originWaterTile.tileId, 1));
            sectorSteps[originWaterTile.tileId] = 1;

            while (queue.Count > 0)
            {
                var (currentId, step) = queue.Dequeue();
                PlanetTile current = new PlanetTile(currentId);

                outDetectionTiles.Add(current);
                if (maxIdentificationRadius > 0 && step <= maxIdentificationRadius)
                {
                    outIdentificationTiles?.Add(current);
                }

                if (outMinSteps != null)
                {
                    if (!outMinSteps.TryGetValue(current, out int prevMin) || step < prevMin)
                    {
                        outMinSteps[current] = step;
                    }
                }

                if (step >= maxDetectionRadius)
                {
                    continue;
                }

                GetTileNeighbors(current, neighbors);
                for (int n = 0; n < neighbors.Count; n++)
                {
                    PlanetTile next = neighbors[n];
                    if (!next.Valid || next.tileId == stationTile.tileId)
                    {
                        continue;
                    }

                    int nextStep = step + 1;
                    if (sectorSteps.TryGetValue(next.tileId, out int existingStep) && existingStep <= nextStep)
                    {
                        continue;
                    }

                    // Sonar never traverses land
                    if (!IsTileWaterCovered(next))
                    {
                        continue;
                    }

                    // Enforce directional V-shaped cone projection for this adjacent water origin
                    if (hasSectorDir && TryGetTileCenter(next, out Vector3 nextCenter))
                    {
                        Vector3 candidateVec = Vector3.ProjectOnPlane(nextCenter - stationCenter, stationNormal);
                        if (candidateVec.sqrMagnitude > 0.000001f)
                        {
                            float dot = Vector3.Dot(sectorDir, candidateVec.normalized);
                            if (dot < DirectionalSectorCosThreshold)
                            {
                                continue;
                            }
                        }
                    }

                    sectorSteps[next.tileId] = nextStep;
                    queue.Enqueue((next.tileId, nextStep));
                }
            }
        }
    }

    public static bool TryGetSonarDetectionDistance(
        PlanetTile stationTile,
        PlanetTile targetTile,
        int maxRadius,
        out int distance)
    {
        distance = int.MaxValue;
        if (!stationTile.Valid || !targetTile.Valid || maxRadius <= 0)
        {
            return false;
        }

        if (!IsTileWaterCovered(targetTile))
        {
            return false;
        }

        var detectionTiles = new HashSet<PlanetTile>();
        var minSteps = new Dictionary<PlanetTile, int>();
        PopulateSonarCoverage(
            stationTile,
            maxRadius,
            maxIdentificationRadius: 0,
            outDetectionTiles: detectionTiles,
            outIdentificationTiles: null,
            outMinSteps: minSteps);

        if (minSteps.TryGetValue(targetTile, out int steps))
        {
            distance = steps;
            return true;
        }

        return false;
    }
}

