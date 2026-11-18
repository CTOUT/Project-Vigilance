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
    public const float ApproxDegreesPerHexTile = 0.65f;

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
    /// Converts latitude and longitude in degrees to a 3D unit direction vector matching RimWorld's WorldGrid coordinate system.
    /// In RimWorld: +Y is North Pole, -Z is Prime Meridian (lon=0), +X is 90° East meridian.
    /// </summary>
    public static Vector3 SphericalToDirection(float latitudeDeg, float longitudeDeg)
    {
        float latRad = latitudeDeg * Mathf.Deg2Rad;
        float lonRad = longitudeDeg * Mathf.Deg2Rad;

        float cosLat = Mathf.Cos(latRad);
        float x = cosLat * Mathf.Sin(lonRad);
        float y = Mathf.Sin(latRad);
        float z = -cosLat * Mathf.Cos(lonRad);

        return new Vector3(x, y, z).normalized;
    }

    public const float MinVisualAltitude = 4f;
    public const float MaxVisualAltitude = 34f;
    public const float MaxOrbitalRenderRadius = 135f;

    /// <summary>
    /// Maps physical orbital altitude (150 km to 40,000 km) to 3D world visual altitude units (4f to 34f).
    /// Uses 3 stratified altitude bands:
    /// - Low Earth Orbit (LEO): 150 km - 1,500 km -> 4f - 10f (skimming just above atmosphere)
    /// - Medium Earth Orbit (MEO): 1,500 km - 15,000 km -> 10f - 24f (stations, navigation)
    /// - Geostationary / High Orbit (GEO): 15,000 km - 40,000 km -> 24f - 34f (deep relays, outer rim)
    /// </summary>
    public static float AltitudeKmToVisualAltitude(float altitudeKm)
    {
        float clamped = Mathf.Clamp(altitudeKm, 150f, 40000f);
        if (clamped <= 1500f)
        {
            float t = (clamped - 150f) / (1500f - 150f);
            return Mathf.Lerp(4f, 10f, t);
        }
        if (clamped <= 15000f)
        {
            float t = (clamped - 1500f) / (15000f - 1500f);
            return Mathf.Lerp(10f, 24f, t);
        }
        float tGeo = (clamped - 15000f) / (40000f - 15000f);
        return Mathf.Lerp(24f, 34f, tGeo);
    }

    /// <summary>
    /// Inverse mapping: converts 3D world visual altitude units (4f to 34f) back to physical orbital altitude in km.
    /// </summary>
    public static float VisualAltitudeToAltitudeKm(float visualAlt)
    {
        float clamped = Mathf.Clamp(visualAlt, 4f, 34f);
        if (clamped <= 10f)
        {
            float t = (clamped - 4f) / (10f - 4f);
            return Mathf.Lerp(150f, 1500f, t);
        }
        if (clamped <= 24f)
        {
            float t = (clamped - 10f) / (24f - 10f);
            return Mathf.Lerp(1500f, 15000f, t);
        }
        float tGeo = (clamped - 24f) / (34f - 24f);
        return Mathf.Lerp(15000f, 40000f, tGeo);
    }

    /// <summary>
    /// Computes the 3D position vector for world rendering, using banded altitude scaling
    /// and deterministic orbital shell stratification above the planet surface.
    /// Clamps visual altitude between 4 and 34 units above the globe so high-orbit stations (35,786 km)
    /// remain gracefully framed around the planet while low-orbit satellites hug the planetary limb.
    /// </summary>
    public static Vector3 CalculateOrbitalPosition(
        OrbitalParameters parameters,
        int currentTick,
        float basePlanetRadius = 100f,
        float altitudeRenderOffset = 15f)
    {
        Vector3 dir = CalculateSubSatelliteDirection(parameters, currentTick);

        // Stratified altitude scaling based on orbital band
        float visualAlt = AltitudeKmToVisualAltitude(parameters.AltitudeKm);

        // Deterministic pseudo-random altitude jitter based on orbital parameters hash
        // Prevents satellites and stations with identical orbital parameters from sharing the exact same altitude ring
        int hash = parameters.GetHashCode() & 0x7FFFFFFF;
        float jitter = (((hash % 100) - 50) / 50f) * 0.5f;

        // Hard clamp enforces that orbital bodies never escape camera framing
        float renderRadius = Mathf.Clamp(
            basePlanetRadius + visualAlt + jitter,
            basePlanetRadius + MinVisualAltitude,
            basePlanetRadius + MaxVisualAltitude);
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
    /// Projects a camera screen ray onto the planetary sphere or surrounding orbital shell ring
    /// (radius [<paramref name="planetRadius"/>, <paramref name="maxOrbitalPickRadius"/>]) and outputs
    /// the unit direction from the planet centre directly beneath that screen position.
    /// Used when deep orbit view (<c>Orbit</c> layer) prevents <see cref="GenWorld.MouseTile"/> from hitting the Surface grid.
    /// </summary>
    public static bool TryProjectRayToPlanetDirection(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        out Vector3 planetDirection,
        float planetRadius = 100f,
        float maxOrbitalPickRadius = 145f)
    {
        planetDirection = Vector3.zero;
        if (rayDirection.sqrMagnitude < 1e-6f)
        {
            return false;
        }

        Vector3 dir = rayDirection.normalized;
        float tClosest = -Vector3.Dot(rayOrigin, dir);
        if (tClosest <= 0f)
        {
            return false;
        }

        Vector3 closestPoint = rayOrigin + dir * tClosest;
        float distFromCenterSq = closestPoint.sqrMagnitude;
        float planetRadiusSq = planetRadius * planetRadius;

        if (distFromCenterSq <= planetRadiusSq)
        {
            float halfChord = Mathf.Sqrt(Mathf.Max(0f, planetRadiusSq - distFromCenterSq));
            float tHit = tClosest - halfChord;
            Vector3 surfaceHit = rayOrigin + dir * (tHit > 0f ? tHit : tClosest);
            if (surfaceHit.sqrMagnitude < 1e-6f)
            {
                return false;
            }
            planetDirection = surfaceHit.normalized;
            return true;
        }

        if (distFromCenterSq <= maxOrbitalPickRadius * maxOrbitalPickRadius)
        {
            planetDirection = closestPoint.normalized;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves the closest valid tile on the planetary Surface layer (or specified <paramref name="surfaceLayer"/>)
    /// for a given unit world direction vector.
    /// </summary>
    public static PlanetTile FindClosestSurfaceTile(Vector3 worldDirection, PlanetLayer? surfaceLayer = null)
    {
        if (worldDirection.sqrMagnitude < 1e-6f)
        {
            return PlanetTile.Invalid;
        }

        PlanetLayer? layer = surfaceLayer ?? Find.World?.grid?.Surface;
        if (layer == null || layer.TilesCount == 0)
        {
            return PlanetTile.Invalid;
        }

        Vector3 normalizedDir = worldDirection.normalized;
        PlanetTile seedTile = CoarseFindClosestTile(layer, normalizedDir);
        return HillClimbClosestTile(layer, seedTile, normalizedDir);
    }

    /// <summary>
    /// Evaluates whether a target tile lies within the satellite's ground footprint.
    /// Uses RimWorld's WorldGrid spherical tile distance conversion when WorldGrid is available,
    /// ensuring exact parity with GenDraw.DrawWorldRadiusRing visual overlays.
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
        return IsTileWithinFootprint(satelliteDir, tileCenter, footprintRadiusInTiles);
    }

    /// <summary>
    /// Fast allocation-free footprint test against a pre-normalized target tile center vector.
    /// Eliminates repeated GetTileCenter lookups in multi-tick lookahead loops.
    /// </summary>
    public static bool IsTileWithinFootprint(
        Vector3 satelliteDir,
        Vector3 targetTileCenterNormalized,
        float footprintRadiusInTiles)
    {
        if (Find.World?.grid == null)
        {
            return false;
        }

        float dot = Mathf.Clamp(Vector3.Dot(satelliteDir.normalized, targetTileCenterNormalized), -1f, 1f);
        float angularDistanceRadians = Mathf.Acos(dot);

        float distInTiles = Find.WorldGrid.ApproxDistanceInTiles(angularDistanceRadians);
        return distInTiles <= footprintRadiusInTiles;
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
    /// Precomputes target tile normal to execute lookahead loop with zero allocation and zero redundant grid lookups.
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
        int endTick = fromTick + lookaheadTicks;
        for (int t = fromTick; t < endTick; t += stepTicks)
        {
            Vector3 satDir = CalculateSubSatelliteDirection(parameters, t);
            if (IsTileWithinFootprint(satDir, targetCenter, footprintRadiusInTiles))
            {
                return t;
            }
        }

        return null;
    }

    /// <summary>
    /// Constructs orbital parameters calibrated so the ground track passes directly over <paramref name="targetTile"/>
    /// at the current game tick. Solves for RAAN and mean anomaly algebraically.
    /// </summary>
    public static OrbitalParameters CreateOverheadOrbitParameters(
        PlanetTile targetTile,
        float altitudeKm,
        int periodTicks,
        float extraInclinationDeg = 5f,
        float precessionRateDegPerDay = -1.5f,
        float orbitalDriftDegPerDay = 1.5f)
    {
        float lon = Find.WorldGrid?.LongLatOf(targetTile).x ?? 0f;
        float lat = Find.WorldGrid?.LongLatOf(targetTile).y ?? 0f;
        int currentTick = Current.Game?.tickManager?.TicksGame ?? 0;
        return CreateOverheadOrbitParameters(lat, lon, currentTick, altitudeKm, periodTicks, extraInclinationDeg, precessionRateDegPerDay, orbitalDriftDegPerDay);
    }

    /// <summary>
    /// Constructs orbital parameters calibrated so the ground track passes directly over (<paramref name="lat"/>, <paramref name="lon"/>)
    /// at <paramref name="currentTick"/>. Solves for RAAN and mean anomaly algebraically.
    /// </summary>
    public static OrbitalParameters CreateOverheadOrbitParameters(
        float lat,
        float lon,
        int currentTick,
        float altitudeKm,
        int periodTicks,
        float extraInclinationDeg = 5f,
        float precessionRateDegPerDay = -1.5f,
        float orbitalDriftDegPerDay = 1.5f)
    {
        // Ensure inclination is strictly greater than latitude so ground track passes directly overhead
        float inclination = Mathf.Clamp(Mathf.Abs(lat) + extraInclinationDeg, 10f, 89.9f);

        // Solve for argument of latitude u at epoch such that latitudeDeg == lat at epochTick
        float incRad = inclination * Mathf.Deg2Rad;
        float latRad = lat * Mathf.Deg2Rad;
        float sinU = Mathf.Sin(latRad) / Mathf.Sin(incRad);
        sinU = Mathf.Clamp(sinU, -1f, 1f);
        float uRad = Mathf.Asin(sinU);
        float uDeg = uRad * Mathf.Rad2Deg;
        if (uDeg < 0f) uDeg += 360f;
        uRad = uDeg * Mathf.Deg2Rad;

        // Angular displacement from ascending node along equator
        float yNode = Mathf.Sin(uRad) * Mathf.Cos(incRad);
        float xNode = Mathf.Cos(uRad);
        float alphaNodeDeg = Mathf.Atan2(yNode, xNode) * Mathf.Rad2Deg;

        // Solve for Ascending Node RAAN such that longitudeDeg == lon at epochTick
        float raanDeg = ((lon - alphaNodeDeg + 180f) % 360f + 360f) % 360f - 180f;

        return new OrbitalParameters(
            altitudeKm: altitudeKm,
            periodTicks: periodTicks,
            inclinationDeg: inclination,
            ascendingNodeDeg: raanDeg,
            precessionRateDegPerDay: precessionRateDegPerDay,
            meanAnomalyEpochDeg: uDeg,
            orbitalDriftDegPerDay: orbitalDriftDegPerDay,
            epochTick: currentTick);
    }
}
