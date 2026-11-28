using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Geoscape.World;

namespace ProjectVigilance.Geoscape.Sensory;

public partial class SensoryManager
{
    public void PerformSensorySweep()
    {
        DeepProfiler.Start("ProjectVigilance.PerformSensorySweep");
        try
        {
            VisibilityMode mode = GeoscapeRules.VisibilityMode;

            // Clean up stale targets or populate if empty
            activeTargets.RemoveWhere(StaleTargetPredicate);
            if (activeTargets.Count == 0 && Find.World?.worldObjects?.AllWorldObjects != null)
            {
                var all = Find.World.worldObjects.AllWorldObjects;
                for (int i = 0; i < all.Count; i++)
                {
                    WorldObject wo = all[i];
                    if (wo is IDetectableTarget target && !wo.Destroyed && (Find.World == null || wo.Spawned))
                    {
                        activeTargets.Add(target);
                    }
                }
            }

            // Omniscient Mode: bypass all fog and radar requirements
            if (mode == VisibilityMode.Omniscient)
            {
                if (activeTargets.Count > 0)
                {
                    foreach (IDetectableTarget target in activeTargets)
                    {
                        if (target.CurrentIntelligenceTier != IntelligenceTier.Analysed)
                        {
                            target.CurrentIntelligenceTier = IntelligenceTier.Analysed;
                        }
                        target.UpdateSampledPosition();
                    }
                }
                return;
            }

            IReadOnlyList<ISensorProvider> activeSensors = GetActiveSensors();
            bool sensorsChanged = false;

            if (activeSensors.Count != lastActiveSensorStates.Count)
            {
                sensorsChanged = true;
            }

            for (int i = 0; i < activeSensors.Count; i++)
            {
                ISensorProvider sensor = activeSensors[i];
                if (!sensor.SensorTile.Valid)
                {
                    continue;
                }

                float currentRadius = sensor.DetectionRadiusInTiles;
                int currentIdRadius = sensor.IdentificationRadiusInTiles;
                int currentPenRadius = sensor.SigintPenetrationRadiusInTiles;
                PlanetTile currentTile = sensor.SensorTile;

                bool sensorChangedOrNewlyActive = !lastActiveSensorStates.TryGetValue(sensor, out var prevState) ||
                    prevState.tile != currentTile ||
                    Mathf.Abs(prevState.radius - currentRadius) > 0.05f ||
                    prevState.idRadius != currentIdRadius ||
                    prevState.sigintPenRadius != currentPenRadius ||
                    prevState.penetratesMasking != sensor.PenetratesTerrainMasking;

                if (sensorChangedOrNewlyActive)
                {
                    sensorsChanged = true;
                }

                if (mode == VisibilityMode.FogOfWar)
                {
                    EnsureExploredArray(currentTile.tileId + 1);
                    if (sensor.IsStationary)
                    {
                        // Stationary sensors (colonies, radar towers, sonar stations) only flood-fill once unless radius expanded
                        int intRadius = Mathf.CeilToInt(currentRadius);
                        var cacheKey = (currentTile.tileId, sensor.IsOceanicSonar);
                        if (!stationaryExploredRadius.TryGetValue(cacheKey, out int prevRadius) || prevRadius < intRadius)
                        {
                            stationaryExploredRadius[cacheKey] = intRadius;
                            MarkSurroundingTilesExplored(currentTile, currentRadius, sensor.IsOceanicSonar);
                            sensorsChanged = true;
                        }

                        if (!stationaryIdentifiedRadius.TryGetValue(cacheKey, out int prevIdRadius) ||
                            prevIdRadius < currentIdRadius ||
                            (sensorChangedOrNewlyActive && unidentifiedObjectIds.Count > 0))
                        {
                            stationaryIdentifiedRadius[cacheKey] = Mathf.Max(prevIdRadius, currentIdRadius);
                            MarkSurroundingTilesIdentified(currentTile, currentIdRadius, sensor.IsOceanicSonar);
                            sensorsChanged = true;
                        }
                    }
                    else
                    {
                        // Mobile sensors (caravans) only flood-fill when moving to a new tile
                        if (sensor is ScoutHorizonSensor scout && scout.Source is Caravan caravan)
                        {
                            int id = caravan.ID;
                            if (!lastCaravanTiles.TryGetValue(id, out PlanetTile lastTile) || lastTile != caravan.Tile)
                            {
                                lastCaravanTiles[id] = caravan.Tile;
                                MarkSurroundingTilesExplored(caravan.Tile, currentRadius, sensor.IsOceanicSonar);
                                MarkSurroundingTilesIdentified(caravan.Tile, currentIdRadius, sensor.IsOceanicSonar);
                                sensorsChanged = true;
                            }
                        }
                        else
                        {
                            MarkSurroundingTilesExplored(sensor.SensorTile, currentRadius, sensor.IsOceanicSonar);
                            MarkSurroundingTilesIdentified(sensor.SensorTile, currentIdRadius, sensor.IsOceanicSonar);
                            sensorsChanged = true;
                        }
                    }
                }
            }

            if (sensorsChanged)
            {
                lastActiveSensorStates.Clear();
                for (int i = 0; i < activeSensors.Count; i++)
                {
                    ISensorProvider s = activeSensors[i];
                    if (s.SensorTile.Valid)
                    {
                        lastActiveSensorStates[s] = (s.SensorTile, s.DetectionRadiusInTiles, s.IdentificationRadiusInTiles, s.SigintPenetrationRadiusInTiles, s.PenetratesTerrainMasking);
                    }
                }

                if (mode == VisibilityMode.FogOfWar)
                {
                    UpdateHazeActiveVision();
                    var allCaravans = Find.World?.worldObjects?.Caravans;
                    if (allCaravans != null)
                    {
                        for (int k = 0; k < allCaravans.Count; k++)
                        {
                            var c = allCaravans[k];
                            if (c != null && c.Spawned && c.Faction?.IsPlayer == true && c.pather != null && c.pather.Moving)
                            {
                                OptimisticPathingUtility.NotifyCaravanVisionUpdated(c);
                            }
                        }
                    }
                }
                WorldDrawLayer_RadarCoverage.Instance?.SetDirty();
            }

            if (mode == VisibilityMode.FogOfWar)
            {
                HazeReconnaissanceManager.Instance?.CheckUncoveredSites();
            }

            // Peacetime Early Exit: If all sensors updated and no targets exist, exit!
            if (activeTargets.Count == 0)
            {
                return;
            }

            DeepProfiler.Start("ProjectVigilance.SensorySweep.TargetEvaluations");
            try
            {
                bool anyTargetTierChanged = false;
                // Evaluate all detectable targets against active sensors
                foreach (IDetectableTarget target in activeTargets)
                {
                    PlanetTile targetTile = target.CurrentTile;
                    if (!targetTile.Valid)
                    {
                        continue;
                    }

                    IntelligenceTier bestTier = IntelligenceTier.Undetected;
                    ISensorProvider? bestSensor = null;

                    // Model B+ Tactical Terrain Masking:
                    // Incursions in Mountainous/Impassable tiles are masked by rugged valleys and canyons,
                    // preventing advancement beyond Contact tier unless scanned by a sensor that penetrates masking.
                    // Evaluated once per target rather than per-sensor.
                    bool isTargetMasked = SensoryTerrainUtility.IsTargetMasked(targetTile);

                    for (int i = 0; i < activeSensors.Count; i++)
                    {
                        ISensorProvider sensor = activeSensors[i];
                        if (sensor.TryGetDetectionDistance(target, out int travDist))
                        {
                            // Evaluate resulting intelligence tier based on identification radius, terrain masking, and sensor capabilities
                            IntelligenceTier tier = SensoryTerrainUtility.EvaluateSensorTier(sensor, travDist, isTargetMasked);

                            if (tier > bestTier)
                            {
                                bestTier = tier;
                                bestSensor = sensor;
                                if (bestTier == IntelligenceTier.Analysed)
                                {
                                    break; // Analysed is the maximum possible tier; early-exit sensor loop
                                }
                            }
                        }
                    }

                    // Special persistence: Retreating forces carrying captured player colonists remain actively tracked via colonist distress beacons
                    if (target is WorldObject_Incursion inc && inc.IsRetreating)
                    {
                        if (inc.KidnappedColonists != null && inc.KidnappedColonists.Count > 0)
                        {
                            if (bestTier < IntelligenceTier.Classified)
                            {
                                bestTier = IntelligenceTier.Classified;
                            }
                        }
                        else
                        {
                            // Fleeing raiders retain a contact blip on the geoscape for pursuit
                            if (bestTier < IntelligenceTier.Contact)
                            {
                                bestTier = IntelligenceTier.Contact;
                            }
                        }
                    }

                    if (bestTier > target.CurrentIntelligenceTier)
                    {
                        IntelligenceTier prevTier = target.CurrentIntelligenceTier;
                        target.OnDetected(bestSensor!, bestTier);
                        anyTargetTierChanged = true;

                        if (prevTier == IntelligenceTier.Undetected && bestTier >= IntelligenceTier.Contact)
                        {
                            OnContactDetected?.Invoke(bestSensor!, target, bestTier);
                        }
                        if (bestTier >= IntelligenceTier.Classified && prevTier < IntelligenceTier.Classified)
                        {
                            OnContactClassified?.Invoke(target, bestTier);
                        }
                    }
                    else if (bestTier == IntelligenceTier.Undetected && target.CurrentIntelligenceTier != IntelligenceTier.Undetected)
                    {
                        // Target has moved into a blind spot / unmonitored sector
                        target.CurrentIntelligenceTier = IntelligenceTier.Undetected;
                        anyTargetTierChanged = true;
                        OnContactLost?.Invoke(target);
                    }

                    // Discrete radar/sonar jump update on every pulse sweep for detected contacts
                    if (bestTier >= IntelligenceTier.Contact)
                    {
                        target.UpdateSampledPosition();
                    }
                }

                if (anyTargetTierChanged)
                {
                    SweepRevision++;
                }
            }
            finally
            {
                DeepProfiler.End();
            }
        }
        finally
        {
            DeepProfiler.End();
        }
    }

    public bool HasActiveVision(PlanetTile tile)
    {
        if (GeoscapeRules.VisibilityMode == VisibilityMode.Omniscient)
        {
            return true;
        }

        if (!tile.Valid)
        {
            return false;
        }

        if (currentActiveVisionTileIds.Count > 0)
        {
            return currentActiveVisionTileIds.Contains(tile.tileId);
        }

        IReadOnlyList<ISensorProvider> sensors = GetActiveSensors();
        for (int i = 0; i < sensors.Count; i++)
        {
            ISensorProvider sensor = sensors[i];
            if (sensor.SensorTile.Valid && (Find.World?.grid == null || sensor.SensorTile.Layer == tile.Layer))
            {
                if (sensor.IsOceanicSonar)
                {
                    if (SonarTraversalUtility.IsTileWaterCovered(tile))
                    {
                        int sonarMaxSteps = Mathf.Max(1, Mathf.RoundToInt(sensor.DetectionRadiusInTiles));
                        if (SonarTraversalUtility.TryGetSonarDetectionDistance(sensor.SensorTile, tile, sonarMaxSteps, out _))
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    int maxSteps = Mathf.Max(1, Mathf.RoundToInt(sensor.DetectionRadiusInTiles));
                    if (Find.World?.grid != null && Find.World.grid.TraversalDistanceBetween(sensor.SensorTile, tile, passImpassable: true, maxDist: maxSteps) <= maxSteps)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public bool HasActiveIdentification(PlanetTile tile)
    {
        if (GeoscapeRules.VisibilityMode == VisibilityMode.Omniscient)
        {
            return true;
        }

        if (isIdentifiedOverride != null)
        {
            return isIdentifiedOverride(tile);
        }

        if (isExploredOverride != null)
        {
            return isExploredOverride(tile);
        }

        if (!tile.Valid)
        {
            return false;
        }

        if (currentActiveIdentifiedTileIds.Count > 0)
        {
            return currentActiveIdentifiedTileIds.Contains(tile.tileId);
        }

        IReadOnlyList<ISensorProvider> sensors = GetActiveSensors();
        for (int i = 0; i < sensors.Count; i++)
        {
            ISensorProvider sensor = sensors[i];
            int idSteps = sensor.MaxIntelligenceTier >= IntelligenceTier.Classified
                ? sensor.IdentificationRadiusInTiles
                : 0;
            if (idSteps > 0 && sensor.SensorTile.Valid && (Find.World?.grid == null || sensor.SensorTile.Layer == tile.Layer))
            {
                if (sensor.IsOceanicSonar)
                {
                    if (SonarTraversalUtility.IsTileWaterCovered(tile))
                    {
                        if (SonarTraversalUtility.TryGetSonarDetectionDistance(sensor.SensorTile, tile, idSteps, out _))
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    int dist = DistanceCalculationOverride != null
                        ? DistanceCalculationOverride(sensor.SensorTile, tile, idSteps)
                        : (Find.World?.grid != null
                            ? Find.World.grid.TraversalDistanceBetween(sensor.SensorTile, tile, passImpassable: true, maxDist: idSteps)
                            : Math.Abs(sensor.SensorTile.tileId - tile.tileId));
                    if (dist <= idSteps)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Optional hook for overriding neighbour expansion during topological BFS in unit tests.
    /// </summary>
    public static System.Action<PlanetTile, List<PlanetTile>>? TileNeighborsOverride { get; set; }

    private static System.Func<IReadOnlyList<ISensorProvider>>? activeSensorsOverride;

    /// <summary>
    /// Optional hook for active sensor providers in unit tests.
    /// </summary>
    public static System.Func<IReadOnlyList<ISensorProvider>>? ActiveSensorsOverride
    {
        get => activeSensorsOverride;
        set
        {
            activeSensorsOverride = value;
            Instance?.InvalidateStationaryVisionCache();
        }
    }

    /// <summary>
    /// Classifies an <see cref="ISensorProvider"/> into its primary sensory domain.
    /// </summary>
    public static SensorDomainFlags GetSensorDomain(ISensorProvider? sensor)
    {
        if (sensor == null)
        {
            return SensorDomainFlags.None;
        }

        if (sensor.IsOceanicSonar)
        {
            return SensorDomainFlags.Sonar;
        }

        return sensor.IsTechnological ? SensorDomainFlags.Radar : SensorDomainFlags.Visual;
    }

    /// <summary>
    /// Formats active <see cref="SensorDomainFlags"/> into a compact player-facing string
    /// (e.g. "Visual", "Radar", "Sonar", or "Radar + Sonar").
    /// </summary>
    public static string FormatSensorDomainFlags(SensorDomainFlags domains)
    {
        if (domains == SensorDomainFlags.None)
        {
            return "None";
        }

        var parts = new List<string>(3);
        if ((domains & SensorDomainFlags.Visual) != 0) parts.Add("Visual");
        if ((domains & SensorDomainFlags.Radar) != 0) parts.Add("Radar");
        if ((domains & SensorDomainFlags.Sonar) != 0) parts.Add("Sonar");
        return string.Join(" + ", parts);
    }

    /// <summary>
    /// Returns all active sensory domains currently covering <paramref name="tile"/>.
    /// </summary>
    public SensorDomainFlags GetActiveDomainsForTile(PlanetTile tile)
    {
        if (!tile.Valid)
        {
            return SensorDomainFlags.None;
        }

        if (ActiveSensorsOverride == null && currentActiveTileDomains.TryGetValue(tile.tileId, out SensorDomainFlags cached))
        {
            return cached;
        }

        SensorDomainFlags domains = SensorDomainFlags.None;
        IReadOnlyList<ISensorProvider> sensors = ActiveSensorsOverride != null ? ActiveSensorsOverride() : GetActiveSensors();
        for (int i = 0; i < sensors.Count; i++)
        {
            ISensorProvider sensor = sensors[i];
            if (!sensor.IsSensorActive || !sensor.SensorTile.Valid)
            {
                continue;
            }

            if (TileNeighborsOverride == null && Find.World?.grid != null && sensor.SensorTile.Layer != tile.Layer)
            {
                continue;
            }

            int maxSteps = Mathf.Max(1, Mathf.RoundToInt(sensor.DetectionRadiusInTiles));
            if (sensor.IsOceanicSonar)
            {
                if (SonarTraversalUtility.IsTileWaterCovered(tile) &&
                    SonarTraversalUtility.TryGetSonarDetectionDistance(sensor.SensorTile, tile, maxSteps, out _))
                {
                    domains |= SensorDomainFlags.Sonar;
                }
            }
            else
            {
                int dist;
                if (DistanceCalculationOverride != null)
                {
                    dist = DistanceCalculationOverride(sensor.SensorTile, tile, maxSteps);
                }
                else
                {
                    try
                    {
                        dist = Find.World?.grid != null
                            ? Find.World.grid.TraversalDistanceBetween(sensor.SensorTile, tile, passImpassable: true, maxDist: maxSteps)
                            : Math.Abs(sensor.SensorTile.tileId - tile.tileId);
                    }
                    catch
                    {
                        dist = int.MaxValue;
                    }
                }

                if (dist <= maxSteps)
                {
                    domains |= GetSensorDomain(sensor);
                }
            }
        }

        if (domains == SensorDomainFlags.None && currentActiveTileDomains.TryGetValue(tile.tileId, out SensorDomainFlags fallback))
        {
            return fallback;
        }

        return domains;
    }

    /// <summary>
    /// Returns all active sensory domains currently capable of detecting <paramref name="target"/>.
    /// </summary>
    public SensorDomainFlags GetDetectingDomainsForTarget(IDetectableTarget? target)
    {
        if (target == null || !target.CurrentTile.Valid)
        {
            return SensorDomainFlags.None;
        }

        SensorDomainFlags domains = SensorDomainFlags.None;
        IReadOnlyList<ISensorProvider> sensors = ActiveSensorsOverride != null ? ActiveSensorsOverride() : GetActiveSensors();
        for (int i = 0; i < sensors.Count; i++)
        {
            ISensorProvider sensor = sensors[i];
            if (!sensor.IsSensorActive || !sensor.SensorTile.Valid)
            {
                continue;
            }

            bool detected;
            try
            {
                detected = sensor.CanDetect(target);
            }
            catch
            {
                detected = false;
            }

            if (detected)
            {
                domains |= GetSensorDomain(sensor);
            }
        }

        if (domains == SensorDomainFlags.None)
        {
            domains = GetActiveDomainsForTile(target.CurrentTile);
        }

        return domains;
    }

    private static PlanetTile ResolveTile(PlanetLayer? surfaceLayer, int tileId) =>
        surfaceLayer != null ? surfaceLayer.PlanetTileForID(tileId) : new PlanetTile(tileId);

    /// <summary>
    /// Populates the provided set with all planet tile IDs currently enveloped by active sensors (colonies, caravans, radar stations).
    /// Used by WorldDrawLayer_RadarCoverage to render tactical perimeter boundaries, identification horizons, and holographic honeycomb grids.
    /// Outputs tiles within optical identification range, closest distance to perimeter using exact topological rings,
    /// mountain-masked radar shadow tiles inside identification range, and per-tile active sensory domains.
    /// </summary>
    public void PopulateActiveVisionTiles(
        HashSet<int> outTileIds,
        HashSet<int>? outIdentifiedTileIds = null,
        Dictionary<int, float>? outTileDistancesFromPerimeter = null,
        HashSet<int>? outMaskedShadowTileIds = null,
        Dictionary<int, SensorDomainFlags>? outTileDomains = null)
    {
        outTileIds.Clear();
        outIdentifiedTileIds?.Clear();
        outTileDistancesFromPerimeter?.Clear();
        outMaskedShadowTileIds?.Clear();
        outTileDomains?.Clear();
        tmpShadowCandidateTileIds.Clear();
        tmpPenetratedMaskedTileIds.Clear();

        if (Find.World?.grid == null && TileNeighborsOverride == null)
        {
            return;
        }

        IReadOnlyList<ISensorProvider> sensors = ActiveSensorsOverride != null ? ActiveSensorsOverride() : GetActiveSensors();
        bool trackMaskedShadow = outMaskedShadowTileIds != null;
        for (int i = 0; i < sensors.Count; i++)
        {
            AccumulateSensorVisionTiles(sensors[i], outTileIds, outIdentifiedTileIds, outTileDomains, trackMaskedShadow);
        }

        if (outMaskedShadowTileIds != null)
        {
            foreach (int tileId in tmpShadowCandidateTileIds)
            {
                if (!tmpPenetratedMaskedTileIds.Contains(tileId))
                {
                    outMaskedShadowTileIds.Add(tileId);
                }
            }
        }

        tmpShadowCandidateTileIds.Clear();
        tmpPenetratedMaskedTileIds.Clear();

        if (outTileDistancesFromPerimeter != null && outTileIds.Count > 0)
        {
            ComputeInwardPerimeterDistances(outTileIds, outTileDistancesFromPerimeter);
        }
    }

    private static bool IsStationarySensor(ISensorProvider sensor) =>
        sensor is CompRadar ||
        sensor is WorldObject_ListeningPost ||
        (sensor is ScoutHorizonSensor shs && shs.Source is MapParent);

    private void AccumulateSensorVisionTiles(
        ISensorProvider sensor,
        HashSet<int> outTileIds,
        HashSet<int>? outIdentifiedTileIds,
        Dictionary<int, SensorDomainFlags>? outTileDomains,
        bool trackMaskedShadow = false)
    {
        if (!sensor.SensorTile.Valid)
        {
            return;
        }

        if (TileNeighborsOverride == null && sensor.SensorTile.Layer == null)
        {
            return;
        }

        PlanetTile centerTile = sensor.SensorTile;
        float radius = sensor.DetectionRadiusInTiles;
        int radiusSteps = Mathf.Max(1, Mathf.RoundToInt(radius));
        int idRadiusSteps = sensor.MaxIntelligenceTier >= IntelligenceTier.Classified
            ? sensor.IdentificationRadiusInTiles
            : 0;
        SensorDomainFlags sensorDomain = GetSensorDomain(sensor);

        if (sensor.IsOceanicSonar)
        {
            if (outTileDomains != null)
            {
                tmpSonarCoverageTileIds.Clear();
                SonarTraversalUtility.PopulateSonarCoverage(
                    centerTile,
                    radiusSteps,
                    idRadiusSteps,
                    tmpSonarCoverageTileIds,
                    outIdentifiedTileIds);

                foreach (int waterTileId in tmpSonarCoverageTileIds)
                {
                    outTileIds.Add(waterTileId);
                    outTileDomains.TryGetValue(waterTileId, out SensorDomainFlags existing);
                    outTileDomains[waterTileId] = existing | SensorDomainFlags.Sonar;
                }
                tmpSonarCoverageTileIds.Clear();
            }
            else
            {
                SonarTraversalUtility.PopulateSonarCoverage(
                    centerTile,
                    radiusSteps,
                    idRadiusSteps,
                    outTileIds,
                    outIdentifiedTileIds);
            }
            return;
        }

        bfsQueue.Clear();
        bfsVisitedTileIds.Clear();

        bfsQueue.Enqueue((centerTile, 0));
        bfsVisitedTileIds.Add(centerTile.tileId);

        while (bfsQueue.Count > 0)
        {
            var (currentTile, currentStep) = bfsQueue.Dequeue();
            int currentTileId = currentTile.tileId;
            outTileIds.Add(currentTileId);
            if (outTileDomains != null)
            {
                outTileDomains.TryGetValue(currentTileId, out SensorDomainFlags existing);
                outTileDomains[currentTileId] = existing | sensorDomain;
            }

            if (idRadiusSteps > 0 && currentStep <= idRadiusSteps)
            {
                outIdentifiedTileIds?.Add(currentTileId);

                if (trackMaskedShadow && SensoryTerrainUtility.IsTargetMasked(currentTile))
                {
                    int penSteps = sensor.SigintPenetrationRadiusInTiles;
                    bool penetratesAtStep = sensor.PenetratesTerrainMasking && (penSteps <= 0 || currentStep <= penSteps);
                    if (penetratesAtStep || currentStep <= 1)
                    {
                        tmpPenetratedMaskedTileIds.Add(currentTileId);
                    }
                    else
                    {
                        tmpShadowCandidateTileIds.Add(currentTileId);
                    }
                }
            }

            if (currentStep < radiusSteps)
            {
                tmpNeighbors.Clear();
                if (TileNeighborsOverride != null)
                {
                    TileNeighborsOverride(currentTile, tmpNeighbors);
                }
                else if (currentTile.Layer != null)
                {
                    currentTile.Layer.GetTileNeighbors(currentTile, tmpNeighbors);
                }
                int nextStep = currentStep + 1;
                for (int n = 0; n < tmpNeighbors.Count; n++)
                {
                    PlanetTile neighbor = tmpNeighbors[n];
                    if (neighbor.Valid && bfsVisitedTileIds.Add(neighbor.tileId))
                    {
                        bfsQueue.Enqueue((neighbor, nextStep));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Executes a multi-source BFS starting from the true outer boundary of all active vision tiles,
    /// propagating inward so that each tile's recorded distance reflects its true topological hex distance
    /// from the outer perimeter of the unified sensor envelope.
    /// </summary>
    internal void ComputeInwardPerimeterDistances(
        HashSet<int> activeTileIds,
        Dictionary<int, float> outDistances)
    {
        outDistances.Clear();
        bfsQueue.Clear();
        PlanetLayer? surfaceLayer = Find.World?.grid?.Surface;

        // 1. Identify all tiles that lie on the true outer perimeter of activeTileIds.
        // A tile is on the perimeter if at least one of its neighbours is outside activeTileIds.
        foreach (int tileId in activeTileIds)
        {
            PlanetTile tile = ResolveTile(surfaceLayer, tileId);
            if (TileNeighborsOverride == null && tile.Layer == null)
            {
                continue;
            }

            tmpNeighbors.Clear();
            if (TileNeighborsOverride != null)
            {
                TileNeighborsOverride(tile, tmpNeighbors);
            }
            else if (tile.Layer != null)
            {
                tile.Layer.GetTileNeighbors(tile, tmpNeighbors);
            }

            bool isPerimeter = false;
            for (int n = 0; n < tmpNeighbors.Count; n++)
            {
                PlanetTile neighbor = tmpNeighbors[n];
                if (!neighbor.Valid || !activeTileIds.Contains(neighbor.tileId))
                {
                    isPerimeter = true;
                    break;
                }
            }

            if (isPerimeter)
            {
                outDistances[tileId] = 0f;
                bfsQueue.Enqueue((tile, 0));
            }
        }

        // Fail-safe for total planetary coverage (all tiles active)
        if (bfsQueue.Count == 0 && activeTileIds.Count > 0)
        {
            foreach (int tileId in activeTileIds)
            {
                outDistances[tileId] = 3f;
            }
            return;
        }

        // 2. Propagate multi-source BFS inward from the outer perimeter
        while (bfsQueue.Count > 0)
        {
            var (currentTile, currentDist) = bfsQueue.Dequeue();
            if (TileNeighborsOverride == null && currentTile.Layer == null)
            {
                continue;
            }

            tmpNeighbors.Clear();
            if (TileNeighborsOverride != null)
            {
                TileNeighborsOverride(currentTile, tmpNeighbors);
            }
            else if (currentTile.Layer != null)
            {
                currentTile.Layer.GetTileNeighbors(currentTile, tmpNeighbors);
            }
            int nextDist = currentDist + 1;

            for (int n = 0; n < tmpNeighbors.Count; n++)
            {
                PlanetTile neighbor = tmpNeighbors[n];
                int neighborId = neighbor.tileId;
                if (neighbor.Valid && activeTileIds.Contains(neighborId) && !outDistances.ContainsKey(neighborId))
                {
                    outDistances[neighborId] = nextDist;
                    bfsQueue.Enqueue((neighbor, nextDist));
                }
            }
        }
    }

}
