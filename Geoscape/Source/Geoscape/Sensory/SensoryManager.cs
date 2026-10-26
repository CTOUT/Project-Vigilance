using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Geoscape.World;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// WorldComponent managing planetary early-warning detection sweeps, sensory coverage, and fog of war.
/// Evaluates contacts at a realistic pulse interval (TickRare: 250 ticks) with zero allocation.
/// </summary>
public class SensoryManager : WorldComponent
{
    public const int SweepIntervalTicks = 250; // ~4.16 seconds (realistic radar rotation / visual sweep rate)

    private readonly List<ISensorProvider> customSensors = new();
    private readonly Dictionary<int, ScoutHorizonSensor> colonySensors = new();
    private readonly Dictionary<int, ScoutHorizonSensor> caravanSensors = new();
    private readonly HashSet<IDetectableTarget> activeTargets = new();
    private readonly Dictionary<PlanetTile, int> stationaryExploredRadius = new();
    private readonly Dictionary<int, PlanetTile> lastCaravanTiles = new();
    private readonly Dictionary<int, (PlanetTile tile, float radius)> lastActiveSensorStates = new();
    private readonly List<ISensorProvider> activeSensorsCache = new();
    private readonly HashSet<int> currentActiveVisionTileIds = new();
    private readonly HashSet<PlanetTile> tmpActiveVisionTiles = new();
    private readonly HashSet<int> newActiveVisionTileIds = new();
    private readonly List<PlanetTile> tmpNeighbors = new(6);
    private readonly Queue<(PlanetTile tile, int step)> bfsQueue = new(128);
    private readonly HashSet<int> bfsVisitedTileIds = new(128);

    private byte[]? exploredBytes;
    private int nextContactTrackingId = 1;

    public static SensoryManager? Instance => Find.World?.GetComponent<SensoryManager>();

    public SensoryManager(RimWorld.Planet.World world) : base(world)
    {
        ExternalFogCompatibility.ResetAndRescan();
    }

    public override void FinalizeInit(bool fromLoad)
    {
        base.FinalizeInit(fromLoad);
        ExternalFogCompatibility.ResetAndRescan();

        // Eagerly initialize explored bitset and execute sensory sweep
        // so player colonies and caravans are revealed before the globe renders.
        featureCenterTileCache.Clear();
        EnsureExploredArray();
        PerformSensorySweep();

        if (Find.WorldGrid?.Surface != null)
        {
            Patches.PlanetLayer_InitializeLayer_Patch.EnsureLayersAttached(Find.WorldGrid.Surface);
            WorldDrawLayer_HazeOfWar.Instance?.RegenerateNow();
            WorldDrawLayer_RadarCoverage.Instance?.RegenerateNow();
        }
    }

    public override void WorldComponentTick()
    {
        base.WorldComponentTick();

        // Responsive movement check: if an active player caravan steps onto a new hex,
        // instantly reveal fog and update active vision without waiting for the 250-tick sweep
        if (GeoscapeMod.Settings.visibilityMode == VisibilityMode.ClassicHazeOfWar && Find.TickManager.TicksGame % 5 == 0)
        {
            CheckPlayerCaravanMovement();
        }

        if (Find.TickManager.TicksGame % SweepIntervalTicks == 0)
        {
            PerformSensorySweep();
        }
    }

    private void CheckPlayerCaravanMovement()
    {
        var caravans = Find.WorldObjects?.Caravans;
        if (caravans == null || caravans.Count == 0) return;

        bool anyMoved = false;
        for (int i = 0; i < caravans.Count; i++)
        {
            Caravan c = caravans[i];
            if (c != null && c.Spawned && c.Faction == Faction.OfPlayer)
            {
                int id = c.ID;
                if (!lastCaravanTiles.TryGetValue(id, out PlanetTile lastTile) || lastTile != c.Tile)
                {
                    lastCaravanTiles[id] = c.Tile;
                    float radius = GeoscapeMod.Settings.caravanScoutRadius;
                    if (caravanSensors.TryGetValue(id, out var sensor) && sensor.IsSensorActive)
                    {
                        radius = sensor.DetectionRadiusInTiles;
                    }
                    MarkSurroundingTilesExplored(c.Tile, radius);
                    anyMoved = true;
                }
            }
        }

        if (anyMoved)
        {
            UpdateHazeActiveVision();
            WorldDrawLayer_RadarCoverage.Instance?.SetDirty();
        }
    }

    public void RegisterTarget(IDetectableTarget target)
    {
        if (target != null)
        {
            activeTargets.Add(target);
        }
    }

    public void DeregisterTarget(IDetectableTarget target)
    {
        if (target != null)
        {
            activeTargets.Remove(target);
        }
    }

    public int NextContactTrackingId => nextContactTrackingId;

    /// <summary>
    /// Allocates a monotonic sequential tracking ID (e.g. Contact-1, Contact-2)
    /// representing an X-COM style persistent target designation that remains for the incursion's lifetime.
    /// Delegated to ContactRegistry for central indexing.
    /// </summary>
    public int AllocateContactTrackingId()
    {
        if (ContactRegistry.Instance != null)
        {
            return ContactRegistry.Instance.AllocateTrackingId();
        }
        return nextContactTrackingId++;
    }

    public void RegisterSensor(ISensorProvider sensor)
    {
        if (sensor != null && !customSensors.Contains(sensor))
        {
            customSensors.Add(sensor);
        }
    }

    public void DeregisterSensor(ISensorProvider sensor)
    {
        if (sensor != null)
        {
            customSensors.Remove(sensor);
        }
    }

    public IReadOnlyList<ISensorProvider> GetActiveSensors()
    {
        activeSensorsCache.Clear();

        // 1. Natural Colony & Camp Scout Horizons (Settlements, Camps, and Outposts with active maps)
        if (Find.Maps != null)
        {
            foreach (Map map in Find.Maps)
            {
                if (map.Parent != null && map.Parent.Tile.Valid &&
                    (map.IsPlayerHome || (map.Parent.Faction == Faction.OfPlayer) || (map.mapPawns != null && map.mapPawns.AnyFreeColonistSpawned)))
                {
                    int parentId = map.Parent.ID;
                    if (!colonySensors.TryGetValue(parentId, out var sensor))
                    {
                        sensor = new ScoutHorizonSensor(map.Parent, isColony: map.IsPlayerHome);
                        colonySensors[parentId] = sensor;
                    }
                    if (sensor.IsSensorActive)
                    {
                        activeSensorsCache.Add(sensor);
                    }
                }
            }
        }

        // 2. Natural Caravan Lookouts (Player caravans)
        if (Find.WorldObjects?.Caravans != null)
        {
            foreach (Caravan caravan in Find.WorldObjects.Caravans)
            {
                if (caravan.Faction == Faction.OfPlayer && caravan.Tile.Valid && caravan.Spawned && !caravan.Destroyed)
                {
                    int caravanId = caravan.ID;
                    if (!caravanSensors.TryGetValue(caravanId, out var sensor))
                    {
                        sensor = new ScoutHorizonSensor(caravan, isColony: false);
                        caravanSensors[caravanId] = sensor;
                    }
                    if (sensor.IsSensorActive)
                    {
                        activeSensorsCache.Add(sensor);
                    }
                }
            }
        }

        // 3. Registered Custom Sensors (Radar installations, listening posts, etc.)
        for (int i = 0; i < customSensors.Count; i++)
        {
            if (customSensors[i].IsSensorActive)
            {
                activeSensorsCache.Add(customSensors[i]);
            }
        }

        return activeSensorsCache;
    }

    public void PerformSensorySweep()
    {
        VisibilityMode mode = GeoscapeMod.Settings.visibilityMode;

        // Clean up stale targets or populate if empty
        activeTargets.RemoveWhere(t => t is WorldObject wo && (wo.Destroyed || !wo.Spawned));
        if (activeTargets.Count == 0 && Find.WorldObjects?.AllWorldObjects != null)
        {
            foreach (WorldObject wo in Find.WorldObjects.AllWorldObjects)
            {
                if (wo is IDetectableTarget target && !wo.Destroyed && wo.Spawned)
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

            int sensorId = sensor.Source?.ID ?? i;
            float currentRadius = sensor.DetectionRadiusInTiles;
            PlanetTile currentTile = sensor.SensorTile;

            if (!lastActiveSensorStates.TryGetValue(sensorId, out var prevState) ||
                prevState.tile != currentTile ||
                Mathf.Abs(prevState.radius - currentRadius) > 0.05f)
            {
                sensorsChanged = true;
            }

            if (mode == VisibilityMode.ClassicHazeOfWar)
            {
                EnsureExploredArray();
                if (sensor.IsStationary)
                {
                    // Stationary sensors (colonies, radar towers) only flood-fill once unless radius expanded
                    int intRadius = Mathf.CeilToInt(currentRadius);
                    if (!stationaryExploredRadius.TryGetValue(currentTile, out int prevRadius) || prevRadius < intRadius)
                    {
                        stationaryExploredRadius[currentTile] = intRadius;
                        MarkSurroundingTilesExplored(currentTile, currentRadius);
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
                            MarkSurroundingTilesExplored(caravan.Tile, currentRadius);
                            sensorsChanged = true;
                        }
                    }
                    else
                    {
                        MarkSurroundingTilesExplored(sensor.SensorTile, currentRadius);
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
                    int id = s.Source?.ID ?? i;
                    lastActiveSensorStates[id] = (s.SensorTile, s.DetectionRadiusInTiles);
                }
            }

            if (mode == VisibilityMode.ClassicHazeOfWar)
            {
                UpdateHazeActiveVision();
            }
            WorldDrawLayer_RadarCoverage.Instance?.SetDirty();
        }

        // Peacetime Early Exit: If all sensors updated and no targets exist, exit!
        if (activeTargets.Count == 0)
        {
            return;
        }

        // Evaluate all detectable targets against active sensors
        foreach (IDetectableTarget target in activeTargets)
        {
            IntelligenceTier bestTier = IntelligenceTier.Undetected;
            ISensorProvider? bestSensor = null;

            for (int i = 0; i < activeSensors.Count; i++)
            {
                ISensorProvider sensor = activeSensors[i];
                if (sensor.CanDetect(target))
                {
                    int maxSteps = Mathf.Max(1, Mathf.RoundToInt(sensor.DetectionRadiusInTiles));
                    int idSteps = Mathf.Max(1, Mathf.RoundToInt(sensor.DetectionRadiusInTiles * 0.45f));
                    int travDist = Find.WorldGrid.TraversalDistanceBetween(sensor.SensorTile, target.CurrentTile, passImpassable: true, maxDist: maxSteps);

                    // Close range (within 45% of detection radius, step <= idSteps): Full sensor tier (e.g. Classified)
                    // Outer range: Generic blip (Contact)
                    IntelligenceTier tier = (travDist <= idSteps)
                        ? sensor.MaxIntelligenceTier
                        : (sensor.MaxIntelligenceTier >= IntelligenceTier.Contact ? IntelligenceTier.Contact : IntelligenceTier.Suspected);

                    if (tier > bestTier)
                    {
                        bestTier = tier;
                        bestSensor = sensor;
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
                target.OnDetected(bestSensor!, bestTier);
            }
            else if (bestTier == IntelligenceTier.Undetected && target.CurrentIntelligenceTier != IntelligenceTier.Undetected)
            {
                // Target has moved into a blind spot / unmonitored sector
                target.CurrentIntelligenceTier = IntelligenceTier.Undetected;
            }

            // Discrete radar/sonar jump update on every pulse sweep for detected contacts
            if (bestTier >= IntelligenceTier.Contact)
            {
                target.UpdateSampledPosition();
            }
        }
    }

    public bool HasActiveVision(PlanetTile tile)
    {
        if (GeoscapeMod.Settings.visibilityMode == VisibilityMode.Omniscient)
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
            if (sensor.SensorTile.Valid && sensor.SensorTile.Layer == tile.Layer)
            {
                int maxSteps = Mathf.Max(1, Mathf.RoundToInt(sensor.DetectionRadiusInTiles));
                if (Find.WorldGrid.TraversalDistanceBetween(sensor.SensorTile, tile, passImpassable: true, maxDist: maxSteps) <= maxSteps)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Executes a deterministic, zero-allocation topological BFS flood fill out to exactly maxSteps hops.
    /// Guarantees mathematically symmetric hexagonal perimeters without spherical geodesic distortion or corner clipping.
    /// </summary>
    public void FloodFillTopological(PlanetTile root, int maxSteps, Action<PlanetTile, int> processor)
    {
        if (!root.Valid || root.Layer == null || maxSteps < 0) return;

        bfsQueue.Clear();
        bfsVisitedTileIds.Clear();

        bfsQueue.Enqueue((root, 0));
        bfsVisitedTileIds.Add(root.tileId);

        while (bfsQueue.Count > 0)
        {
            var (currentTile, currentStep) = bfsQueue.Dequeue();
            processor(currentTile, currentStep);

            if (currentStep < maxSteps)
            {
                tmpNeighbors.Clear();
                currentTile.Layer.GetTileNeighbors(currentTile, tmpNeighbors);
                int nextStep = currentStep + 1;
                for (int i = 0; i < tmpNeighbors.Count; i++)
                {
                    PlanetTile neighbor = tmpNeighbors[i];
                    if (neighbor.Valid && bfsVisitedTileIds.Add(neighbor.tileId))
                    {
                        bfsQueue.Enqueue((neighbor, nextStep));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Populates the provided set with all planet tiles currently enveloped by active sensors (colonies, caravans, radar stations).
    /// Used by WorldDrawLayer_RadarCoverage to render tactical perimeter boundaries and holographic honeycomb grids.
    /// Outputs tiles within optical identification range (45% detection radius) and closest distance to perimeter using exact topological rings.
    /// </summary>
    public void PopulateActiveVisionTiles(
        HashSet<PlanetTile> outTiles,
        HashSet<PlanetTile>? outIdentifiedTiles = null,
        Dictionary<PlanetTile, float>? outTileDistancesFromPerimeter = null)
    {
        outTiles.Clear();
        outIdentifiedTiles?.Clear();
        outTileDistancesFromPerimeter?.Clear();

        IReadOnlyList<ISensorProvider> sensors = GetActiveSensors();
        for (int i = 0; i < sensors.Count; i++)
        {
            ISensorProvider sensor = sensors[i];
            if (!sensor.SensorTile.Valid || sensor.SensorTile.Layer == null)
            {
                continue;
            }

            PlanetTile centerTile = sensor.SensorTile;
            float radius = sensor.DetectionRadiusInTiles;
            int radiusSteps = Mathf.Max(1, Mathf.RoundToInt(radius));
            int idRadiusSteps = Mathf.Max(1, Mathf.RoundToInt(radius * 0.45f));

            FloodFillTopological(
                centerTile,
                radiusSteps,
                (t, step) =>
                {
                    outTiles.Add(t);
                    if (outIdentifiedTiles != null && step <= idRadiusSteps)
                    {
                        outIdentifiedTiles.Add(t);
                    }
                    if (outTileDistancesFromPerimeter != null)
                    {
                        float hexesFromPerim = Mathf.Max(0f, radiusSteps - step);
                        if (outTileDistancesFromPerimeter.TryGetValue(t, out float curMin))
                        {
                            if (hexesFromPerim < curMin)
                            {
                                outTileDistancesFromPerimeter[t] = hexesFromPerim;
                            }
                        }
                        else
                        {
                            outTileDistancesFromPerimeter[t] = hexesFromPerim;
                        }
                    }
                });
        }
    }

    public bool IsExplored(PlanetTile tile)
    {
        if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.ClassicHazeOfWar)
        {
            return true;
        }

        if (!tile.Valid)
        {
            return true;
        }

        // Soft compatibility: respect external community fog mods (such as D4egon's World Fog of War)
        if (ExternalFogCompatibility.IsExternalFogModActive && ExternalFogCompatibility.IsTileRevealedByExternalMod(tile.tileId))
        {
            return true;
        }

        if (exploredBytes == null)
        {
            EnsureExploredArray();
            if (exploredBytes == null)
            {
                return false;
            }
        }

        int tileId = tile.tileId;
        int byteIndex = tileId >> 3;
        int bitIndex = tileId & 7;

        if (byteIndex >= exploredBytes.Length)
        {
            return false;
        }

        return (exploredBytes[byteIndex] & (1 << bitIndex)) != 0;
    }

    public bool IsTileIdExplored(int tileId)
    {
        if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.ClassicHazeOfWar)
        {
            return true;
        }

        if (ExternalFogCompatibility.IsExternalFogModActive && ExternalFogCompatibility.IsTileRevealedByExternalMod(tileId))
        {
            return true;
        }

        if (exploredBytes == null)
        {
            EnsureExploredArray();
            if (exploredBytes == null)
            {
                return false;
            }
        }

        int byteIndex = tileId >> 3;
        int bitIndex = tileId & 7;

        if (byteIndex >= exploredBytes.Length)
        {
            return false;
        }

        return (exploredBytes[byteIndex] & (1 << bitIndex)) != 0;
    }

    private readonly Dictionary<int, int> featureCenterTileCache = new();

    public int GetFeatureCenterTile(WorldFeature feature)
    {
        if (feature == null) return -1;
        if (featureCenterTileCache.TryGetValue(feature.uniqueID, out int cached))
        {
            return cached;
        }

        int closestTile = -1;
        float minSqrDist = float.MaxValue;
        Vector3 targetPos = feature.drawCenter;

        foreach (int tileId in feature.Tiles)
        {
            Vector3 center = Find.WorldGrid.GetTileCenter(tileId);
            float sqrDist = (center - targetPos).sqrMagnitude;
            if (sqrDist < minSqrDist)
            {
                minSqrDist = sqrDist;
                closestTile = tileId;
            }
        }

        featureCenterTileCache[feature.uniqueID] = closestTile;
        return closestTile;
    }

    public bool IsFeatureExplored(WorldFeature feature)
    {
        if (feature == null)
        {
            return true;
        }

        if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.ClassicHazeOfWar)
        {
            return true;
        }

        if (exploredBytes == null)
        {
            return false;
        }

        int centerTile = GetFeatureCenterTile(feature);
        if (centerTile >= 0)
        {
            return IsTileIdExplored(centerTile);
        }

        return false;
    }

    private void UpdateHazeActiveVision()
    {
        var haze = WorldDrawLayer_HazeOfWar.Instance;
        if (haze == null || !haze.IsGeometryBuilt)
        {
            haze?.SetDirty();
            return;
        }

        tmpActiveVisionTiles.Clear();
        newActiveVisionTileIds.Clear();
        PopulateActiveVisionTiles(tmpActiveVisionTiles);

        foreach (PlanetTile pt in tmpActiveVisionTiles)
        {
            newActiveVisionTileIds.Add(pt.tileId);
        }

        // 1. Tiles leaving active vision: restore to Haze (if explored) or Shroud (if unexplored)
        foreach (int oldTileId in currentActiveVisionTileIds)
        {
            if (!newActiveVisionTileIds.Contains(oldTileId))
            {
                Color32 color = IsTileIdExplored(oldTileId)
                    ? WorldDrawLayer_HazeOfWar.HazeColor
                    : WorldDrawLayer_HazeOfWar.ShroudColor;
                haze.SetTileColor(oldTileId, color);
            }
        }

        // 2. Tiles entering active vision: set to ClearColor (100% transparent)
        foreach (int newTileId in newActiveVisionTileIds)
        {
            if (!currentActiveVisionTileIds.Contains(newTileId))
            {
                haze.SetTileColor(newTileId, WorldDrawLayer_HazeOfWar.ClearColor);
            }
        }

        currentActiveVisionTileIds.Clear();
        currentActiveVisionTileIds.UnionWith(newActiveVisionTileIds);

        haze.ApplyDirtyColors();
    }

    private void MarkSurroundingTilesExplored(PlanetTile centerTile, float radiusInTiles)
    {
        if (!centerTile.Valid || exploredBytes == null)
        {
            return;
        }

        bool externalModActive = ExternalFogCompatibility.IsExternalFogModActive;
        int maxSteps = Mathf.Max(1, Mathf.RoundToInt(radiusInTiles));
        bool revealedAnyObject = false;
        bool revealedAnyFeature = false;
        var haze = WorldDrawLayer_HazeOfWar.Instance;
        bool hazeReady = haze != null && haze.IsGeometryBuilt;

        FloodFillTopological(
            centerTile,
            maxSteps,
            (t, step) =>
            {
                int tileId = t.tileId;
                int byteIndex = tileId >> 3;
                int bitIndex = tileId & 7;
                if (byteIndex < exploredBytes.Length)
                {
                    bool wasUnexplored = (exploredBytes[byteIndex] & (1 << bitIndex)) == 0;
                    if (wasUnexplored)
                    {
                        exploredBytes[byteIndex] |= (byte)(1 << bitIndex);

                        // If not currently under active vision (transparent), transition from Shroud to Haze
                        if (hazeReady && !currentActiveVisionTileIds.Contains(tileId))
                        {
                            haze?.SetTileColor(tileId, WorldDrawLayer_HazeOfWar.HazeColor);
                        }

                        // Check if an unrevealed world object or settlement was on this tile
                        if (!revealedAnyObject && Find.WorldObjects != null && Find.WorldObjects.AnyWorldObjectAt(t))
                        {
                            revealedAnyObject = true;
                        }

                        // Check if a landmark or mountain range center was on this tile
                        if (!revealedAnyFeature && Find.WorldFeatures != null)
                        {
                            var features = Find.WorldFeatures.features;
                            for (int f = 0; f < features.Count; f++)
                            {
                                if (GetFeatureCenterTile(features[f]) == tileId)
                                {
                                    revealedAnyFeature = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (externalModActive)
                {
                    ExternalFogCompatibility.NotifyExternalModTileRevealed(tileId);
                }
            });

        if (hazeReady)
        {
            haze?.ApplyDirtyColors();
        }
        else
        {
            haze?.SetDirty();
        }

        // Selective dirtying: only notify vanilla layers if an actual object or feature was uncovered
        if (revealedAnyObject && Find.WorldGrid?.Surface != null)
        {
            Find.WorldGrid.Surface.GetLayer<WorldDrawLayer_WorldObjects_Expandable>()?.SetDirty();
            Find.WorldGrid.Surface.GetLayer<WorldDrawLayer_WorldObjects_NonExpandable>()?.SetDirty();
        }

        if (revealedAnyFeature && Find.WorldGrid?.Surface != null)
        {
            Find.WorldGrid.Surface.GetLayer<WorldDrawLayer_Landmarks>()?.SetDirty();
        }
    }

    /// <summary>
    /// Triggered when scout or radar detection radii are adjusted in Mod Settings,
    /// immediately dirtying and regenerating the fog of war overlay to reflect the new visual range.
    /// </summary>
    public void Notify_SensorRadiusChanged()
    {
        stationaryExploredRadius.Clear();
        lastCaravanTiles.Clear();
        lastActiveSensorStates.Clear();
        currentActiveVisionTileIds.Clear();
        PerformSensorySweep();
        WorldDrawLayer_HazeOfWar.Instance?.SetDirty();
        WorldDrawLayer_RadarCoverage.Instance?.SetDirty();
        if (Find.WorldGrid?.Surface != null)
        {
            Find.WorldGrid.Surface.GetLayer<WorldDrawLayer_WorldObjects_Expandable>()?.SetDirty();
            Find.WorldGrid.Surface.GetLayer<WorldDrawLayer_WorldObjects_NonExpandable>()?.SetDirty();
            Find.WorldGrid.Surface.GetLayer<WorldDrawLayer_Landmarks>()?.SetDirty();
        }
    }

    private void EnsureExploredArray()
    {
        if (exploredBytes == null && Find.WorldGrid != null)
        {
            int totalTiles = Find.WorldGrid.TilesCount;
            exploredBytes = new byte[(totalTiles + 7) / 8];
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref nextContactTrackingId, "nextContactTrackingId", 1);

        if (Scribe.mode == LoadSaveMode.Saving && exploredBytes != null)
        {
            DataExposeUtility.LookByteArray(ref exploredBytes, "exploredTilesBitset");
        }
        else if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            DataExposeUtility.LookByteArray(ref exploredBytes, "exploredTilesBitset");
        }
    }
}
