using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

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

    private byte[]? exploredBytes;
    private int nextContactTrackingId = 1;

    public static SensoryManager? Instance => Find.World?.GetComponent<SensoryManager>();

    public SensoryManager(RimWorld.Planet.World world) : base(world)
    {
        ExternalFogCompatibility.Initialize();
    }

    public override void FinalizeInit(bool fromLoad)
    {
        base.FinalizeInit(fromLoad);
        ExternalFogCompatibility.Initialize();

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

        if (Find.TickManager.TicksGame % SweepIntervalTicks == 0)
        {
            PerformSensorySweep();
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

    /// <summary>
    /// Allocates a monotonic sequential tracking ID (e.g. Contact-1, Contact-2)
    /// representing an X-COM style persistent target designation that remains for the incursion's lifetime.
    /// </summary>
    public int AllocateContactTrackingId()
    {
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

        // 1. Natural Colony Scout Horizons (Settlements with active maps)
        if (Find.Maps != null)
        {
            foreach (Map map in Find.Maps)
            {
                if (map.IsPlayerHome && map.Parent != null && map.Parent.Tile.Valid)
                {
                    int parentId = map.Parent.ID;
                    if (!colonySensors.TryGetValue(parentId, out var sensor))
                    {
                        sensor = new ScoutHorizonSensor(map.Parent, isColony: true);
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
                WorldDrawLayer_HazeOfWar.Instance?.SetDirty();
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
                    float dist = Find.WorldGrid.ApproxDistanceInTiles(sensor.SensorTile, target.CurrentTile);
                    // Close range (within 45% of detection radius): Full sensor tier (e.g. Classified)
                    // Outer range: Generic blip (Contact)
                    IntelligenceTier tier = (dist <= sensor.DetectionRadiusInTiles * 0.45f)
                        ? sensor.MaxIntelligenceTier
                        : (sensor.MaxIntelligenceTier >= IntelligenceTier.Contact ? IntelligenceTier.Contact : IntelligenceTier.Suspected);

                    if (tier > bestTier)
                    {
                        bestTier = tier;
                        bestSensor = sensor;
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

        IReadOnlyList<ISensorProvider> sensors = GetActiveSensors();
        for (int i = 0; i < sensors.Count; i++)
        {
            ISensorProvider sensor = sensors[i];
            if (sensor.SensorTile.Valid && sensor.SensorTile.Layer == tile.Layer)
            {
                if (Find.WorldGrid.ApproxDistanceInTiles(sensor.SensorTile, tile) <= sensor.DetectionRadiusInTiles)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Populates the provided set with all planet tiles currently enveloped by active sensors (colonies, caravans, radar stations).
    /// Used by WorldDrawLayer_RadarCoverage to render tactical perimeter boundaries and holographic honeycomb grids.
    /// </summary>
    public void PopulateActiveVisionTiles(HashSet<PlanetTile> outTiles)
    {
        outTiles.Clear();
        IReadOnlyList<ISensorProvider> sensors = GetActiveSensors();
        for (int i = 0; i < sensors.Count; i++)
        {
            ISensorProvider sensor = sensors[i];
            if (!sensor.SensorTile.Valid)
            {
                continue;
            }

            PlanetTile centerTile = sensor.SensorTile;
            float radius = sensor.DetectionRadiusInTiles;
            int intRadius = Mathf.CeilToInt(radius);

            int maxTiles = Mathf.Max(128, intRadius * intRadius * 6);
            centerTile.Layer.Filler.FloodFill(
                centerTile,
                t => Find.WorldGrid.ApproxDistanceInTiles(centerTile, t) <= radius,
                t =>
                {
                    outTiles.Add(t);
                },
                maxTilesToProcess: maxTiles);
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

    private void MarkSurroundingTilesExplored(PlanetTile centerTile, float radiusInTiles)
    {
        if (!centerTile.Valid || exploredBytes == null)
        {
            return;
        }

        bool externalModActive = ExternalFogCompatibility.IsExternalFogModActive;
        int intRadius = Mathf.CeilToInt(radiusInTiles);
        centerTile.Layer.Filler.FloodFill(
            centerTile,
            t => Find.WorldGrid.ApproxDistanceInTiles(centerTile, t) <= radiusInTiles,
            t =>
            {
                int tileId = t.tileId;
                int byteIndex = tileId >> 3;
                int bitIndex = tileId & 7;
                if (byteIndex < exploredBytes.Length)
                {
                    exploredBytes[byteIndex] |= (byte)(1 << bitIndex);
                }

                if (externalModActive)
                {
                    ExternalFogCompatibility.NotifyExternalModTileRevealed(tileId);
                }
            },
            maxTilesToProcess: (intRadius * intRadius * 4));

        WorldDrawLayer_HazeOfWar.Instance?.SetDirty();
        if (Find.WorldGrid?.Surface != null)
        {
            Find.WorldGrid.Surface.GetLayer<WorldDrawLayer_WorldObjects_Expandable>()?.SetDirty();
            Find.WorldGrid.Surface.GetLayer<WorldDrawLayer_WorldObjects_NonExpandable>()?.SetDirty();
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
