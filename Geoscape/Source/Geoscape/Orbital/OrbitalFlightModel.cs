using System;
using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Orbital;

/// <summary>
/// High-performance algebraic solver for Keplerian orbital positions, drifting ground tracks,
/// and planetary footprint coverage. All calculations are closed-form and allocation-free.
/// </summary>
public static class OrbitalFlightModel
{
    public const int TicksPerRimWorldDay = 60000;
    public const float DegreesPerTickPlanetaryRotation = 360f / TicksPerRimWorldDay;
    public const float ApproxDegreesPerHexTile = 1.8f;

    private static readonly List<PlanetTile> tmpNeighbors = new(8);

    /// <summary>
    /// Calculates sub-satellite planetary latitude and longitude coordinates in degrees at the given game tick.
    /// Latitude: [-90, +90], Longitude: [-180, +180].
    /// </summary>
    public static void CalculateSubSatelliteCoordinates(
        OrbitalParameters parameters,
        int currentTick,
        out float latitudeDeg,
        out float longitudeDeg)
    {
        if (parameters == null)
        {
            latitudeDeg = 0f;
            longitudeDeg = 0f;
            return;
        }

        int deltaTicks = currentTick - parameters.EpochTick;
        float meanMotionDegPerTick = 360f / parameters.PeriodTicks;

        // In-plane argument of latitude (mean anomaly for near-circular orbits)
        float uDeg = (parameters.MeanAnomalyEpochDeg + meanMotionDegPerTick * deltaTicks) % 360f;
        if (uDeg < 0f) uDeg += 360f;

        float uRad = uDeg * Mathf.Deg2Rad;
        float incRad = parameters.InclinationDeg * Mathf.Deg2Rad;

        // Latitude from orbital inclination and argument of latitude
        float sinLat = Mathf.Sin(incRad) * Mathf.Sin(uRad);
        sinLat = Mathf.Clamp(sinLat, -1f, 1f);
        latitudeDeg = Mathf.Asin(sinLat) * Mathf.Rad2Deg;

        // Angular displacement from the ascending node along the equator
        float yNode = Mathf.Sin(uRad) * Mathf.Cos(incRad);
        float xNode = Mathf.Cos(uRad);
        float alphaNodeDeg = Mathf.Atan2(yNode, xNode) * Mathf.Rad2Deg;

        // Nodal precession due to J2 oblateness and secular orbital drift
        float elapsedDays = (float)deltaTicks / TicksPerRimWorldDay;
        float currentRaanDeg = parameters.AscendingNodeDeg + (parameters.PrecessionRateDegPerDay + parameters.OrbitalDriftDegPerDay) * elapsedDays;

        // Right ascension in inertial frame
        float inertialRaDeg = currentRaanDeg + alphaNodeDeg;

        // Sub-satellite longitude in the rotating planetary reference frame
        float planetaryRotationDeg = DegreesPerTickPlanetaryRotation * deltaTicks;
        float rawLonDeg = inertialRaDeg - planetaryRotationDeg;

        // Normalise longitude into [-180, +180]
        longitudeDeg = ((rawLonDeg + 180f) % 360f + 360f) % 360f - 180f;
    }

    /// <summary>
    /// Computes the unit direction vector pointing from the planet's core toward the sub-satellite point.
    /// </summary>
    public static Vector3 CalculateSubSatelliteDirection(OrbitalParameters parameters, int currentTick)
    {
        CalculateSubSatelliteCoordinates(parameters, currentTick, out float latDeg, out float lonDeg);
        return SphericalToDirection(latDeg, lonDeg);
    }

    /// <summary>
    /// Converts latitude and longitude in degrees to a 3D unit direction vector.
    /// </summary>
    public static Vector3 SphericalToDirection(float latitudeDeg, float longitudeDeg)
    {
        float latRad = latitudeDeg * Mathf.Deg2Rad;
        float lonRad = longitudeDeg * Mathf.Deg2Rad;

        float cosLat = Mathf.Cos(latRad);
        float x = cosLat * Mathf.Cos(lonRad);
        float y = Mathf.Sin(latRad);
        float z = cosLat * Mathf.Sin(lonRad);

        return new Vector3(x, y, z).normalized;
    }

    /// <summary>
    /// Computes the 3D position vector for world rendering, using clamped logarithmic altitude scaling
    /// and deterministic orbital shell stratification above the planet surface.
    /// Clamps visual altitude between 14 and 35 units above the globe so high-orbit stations (35,786 km)
    /// remain gracefully framed around the planet rather than lost in deep space.
    /// </summary>
    public static Vector3 CalculateOrbitalPosition(
        OrbitalParameters parameters,
        int currentTick,
        float basePlanetRadius = 100f,
        float altitudeRenderOffset = 15f)
    {
        Vector3 dir = CalculateSubSatelliteDirection(parameters, currentTick);

        // Non-linear logarithmic altitude scaling:
        // Clamped between 150 km (Low LEO) and 40,000 km (High GEO)
        float clampedAlt = Mathf.Clamp(parameters.AltitudeKm, 150f, 40000f);
        float logNorm = Mathf.Clamp01((Mathf.Log10(clampedAlt) - 2.176f) / (4.602f - 2.176f));

        // Base visual altitude ranges from 14f (LEO, r ~ 114) to 34f (GEO, r ~ 134)
        float visualAlt = Mathf.Lerp(14f, 34f, logNorm);

        // Deterministic pseudo-random altitude jitter based on orbital parameters hash
        // Prevents satellites and stations with identical orbital parameters from sharing the exact same altitude ring
        int hash = parameters.GetHashCode() & 0x7FFFFFFF;
        float jitter = (((hash % 100) - 50) / 50f) * 1.5f;

        float renderRadius = basePlanetRadius + visualAlt + jitter;
        return dir * renderRadius;
    }

    /// <summary>
    /// Determines the sub-satellite surface tile on the RimWorld WorldGrid using greedy hill-climbing search.
    /// </summary>
    public static PlanetTile CalculateSubSatelliteTile(
        OrbitalParameters parameters,
        int currentTick,
        PlanetTile previousTile = default,
        PlanetLayer? targetLayer = null)
    {
        Vector3 targetDir = CalculateSubSatelliteDirection(parameters, currentTick);
        PlanetLayer? layer = targetLayer ?? (Find.World?.grid != null ? Find.World.grid.Surface : null);

        if (layer == null || layer.TilesCount == 0)
        {
            return previousTile.Valid ? previousTile : (PlanetTile)0;
        }

        // 1. If previous tile is valid and on same layer, greedy hill climb from previous tile
        if (previousTile.Valid && previousTile.Layer == layer)
        {
            return HillClimbClosestTile(layer, previousTile, targetDir);
        }

        // 2. Coarse search to find seed tile, then hill climb
        PlanetTile seedTile = CoarseFindClosestTile(layer, targetDir);
        return HillClimbClosestTile(layer, seedTile, targetDir);
    }

    private static PlanetTile CoarseFindClosestTile(PlanetLayer layer, Vector3 targetDir)
    {
        int totalTiles = layer.TilesCount;
        int step = Mathf.Max(1, totalTiles / 64);
        int bestTileId = 0;
        float bestDot = -2f;

        for (int i = 0; i < totalTiles; i += step)
        {
            Vector3 center = layer.GetTileCenter(i);
            float dot = Vector3.Dot(targetDir, center.normalized);
            if (dot > bestDot)
            {
                bestDot = dot;
                bestTileId = i;
            }
        }

        return layer.PlanetTileForID(bestTileId);
    }

    private static PlanetTile HillClimbClosestTile(PlanetLayer layer, PlanetTile startTile, Vector3 targetDir)
    {
        PlanetTile current = startTile;
        Vector3 curCenter = layer.GetTileCenter(current);
        float curDot = Vector3.Dot(targetDir, curCenter.normalized);

        int maxIterations = 32;
        while (maxIterations-- > 0)
        {
            tmpNeighbors.Clear();
            layer.GetTileNeighbors(current, tmpNeighbors);

            PlanetTile bestNeighbor = current;
            float bestNeighborDot = curDot;

            for (int i = 0; i < tmpNeighbors.Count; i++)
            {
                PlanetTile neighbor = tmpNeighbors[i];
                Vector3 nCenter = layer.GetTileCenter(neighbor);
                float nDot = Vector3.Dot(targetDir, nCenter.normalized);

                if (nDot > bestNeighborDot)
                {
                    bestNeighborDot = nDot;
                    bestNeighbor = neighbor;
                }
            }

            if (bestNeighbor == current)
            {
                // Reached local maximum on convex sphere
                break;
            }

            current = bestNeighbor;
            curDot = bestNeighborDot;
        }

        return current;
    }

    /// <summary>
    /// Evaluates whether a target tile lies within the satellite's ground footprint using zero-allocation angular dot-product math.
    /// </summary>
    public static bool IsTileWithinFootprint(
        Vector3 satelliteDir,
        PlanetTile targetTile,
        float footprintRadiusInTiles)
    {
        if (!targetTile.Valid || Find.World?.grid == null)
        {
            return false;
        }

        Vector3 tileCenter = Find.World.grid.GetTileCenter(targetTile).normalized;
        float dot = Vector3.Dot(satelliteDir.normalized, tileCenter);
        float maxAngularRadiusDeg = footprintRadiusInTiles * ApproxDegreesPerHexTile;
        float minCos = Mathf.Cos(maxAngularRadiusDeg * Mathf.Deg2Rad);

        return dot >= minCos;
    }

    /// <summary>
    /// Evaluates angular separation in degrees between two unit vectors.
    /// </summary>
    public static float AngularDistanceDeg(Vector3 dirA, Vector3 dirB)
    {
        float dot = Mathf.Clamp(Vector3.Dot(dirA.normalized, dirB.normalized), -1f, 1f);
        return Mathf.Acos(dot) * Mathf.Rad2Deg;
    }

    /// <summary>
    /// Evaluates the orbital velocity in kilometres per second based on standard Keplerian mechanics.
    /// v = sqrt(GM / r).
    /// </summary>
    public static float CalculateOrbitalVelocityKmS(OrbitalParameters parameters, float planetaryMassFactor = 1.0f)
    {
        float rKm = 6371f + parameters.AltitudeKm;
        float mu = 398600f * planetaryMassFactor;
        return Mathf.Sqrt(mu / rKm);
    }

    /// <summary>
    /// Scans future ticks to predict when the next satellite overpass will cover the target colony or outpost tile.
    /// </summary>
    public static int? PredictNextPassTick(
        OrbitalParameters parameters,
        PlanetTile targetTile,
        int fromTick,
        int lookaheadTicks = 120000,
        float footprintRadiusInTiles = 16f,
        int stepTicks = 100)
    {
        if (!targetTile.Valid || Find.World?.grid == null)
        {
            return null;
        }

        Vector3 targetCenter = Find.World.grid.GetTileCenter(targetTile).normalized;
        float maxAngularRadiusDeg = footprintRadiusInTiles * ApproxDegreesPerHexTile;
        float minCos = Mathf.Cos(maxAngularRadiusDeg * Mathf.Deg2Rad);

        int endTick = fromTick + lookaheadTicks;
        for (int t = fromTick; t < endTick; t += stepTicks)
        {
            Vector3 satDir = CalculateSubSatelliteDirection(parameters, t);
            if (Vector3.Dot(satDir, targetCenter) >= minCos)
            {
                return t;
            }
        }

        return null;
    }
}
