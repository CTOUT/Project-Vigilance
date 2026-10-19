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

    private byte[]? exploredBytes;

    public static SensoryManager? Instance => Find.World?.GetComponent<SensoryManager>();

    public SensoryManager(RimWorld.Planet.World world) : base(world)
    {
    }

    public override void WorldComponentTick()
    {
        base.WorldComponentTick();

        if (Find.TickManager.TicksGame % SweepIntervalTicks == 0)
        {
            PerformSensorySweep();
        }
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

    public List<ISensorProvider> GetActiveSensors()
    {
        List<ISensorProvider> sensors = new();

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
                        sensors.Add(sensor);
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
                        sensors.Add(sensor);
                    }
                }
            }
        }

        // 3. Registered Custom Sensors (Radar installations, listening posts, etc.)
        for (int i = 0; i < customSensors.Count; i++)
        {
            if (customSensors[i].IsSensorActive)
            {
                sensors.Add(customSensors[i]);
            }
        }

        return sensors;
    }

    public void PerformSensorySweep()
    {
        VisibilityMode mode = GeoscapeMod.Settings.visibilityMode;

        // Omniscient Mode: bypass all fog and radar requirements
        if (mode == VisibilityMode.Omniscient)
        {
            if (Find.WorldObjects?.AllWorldObjects != null)
            {
                foreach (WorldObject obj in Find.WorldObjects.AllWorldObjects)
                {
                    if (obj is IDetectableTarget target && target.CurrentIntelligenceTier != IntelligenceTier.Analysed)
                    {
                        target.CurrentIntelligenceTier = IntelligenceTier.Analysed;
                    }
                }
            }
            return;
        }

        List<ISensorProvider> activeSensors = GetActiveSensors();

        // If in Classic Haze of War, mark all tiles covered by active sensors as permanently explored
        if (mode == VisibilityMode.ClassicHazeOfWar)
        {
            EnsureExploredArray();
            foreach (var sensor in activeSensors)
            {
                MarkSurroundingTilesExplored(sensor.SensorTile, sensor.DetectionRadiusInTiles);
            }
        }

        // Evaluate all detectable targets against active sensors
        if (Find.WorldObjects?.AllWorldObjects != null)
        {
            foreach (WorldObject obj in Find.WorldObjects.AllWorldObjects)
            {
                if (obj is IDetectableTarget target)
                {
                    IntelligenceTier bestTier = IntelligenceTier.Undetected;
                    ISensorProvider? bestSensor = null;

                    for (int i = 0; i < activeSensors.Count; i++)
                    {
                        ISensorProvider sensor = activeSensors[i];
                        if (sensor.CanDetect(target))
                        {
                            float dist = Find.WorldGrid.ApproxDistanceInTiles(sensor.SensorTile, target.CurrentTile);
                            // Close range (within 40% of detection radius): Full sensor tier (e.g. Classified)
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
                }
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

        List<ISensorProvider> sensors = GetActiveSensors();
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

    public bool IsExplored(PlanetTile tile)
    {
        if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.ClassicHazeOfWar)
        {
            return true;
        }

        if (!tile.Valid || exploredBytes == null)
        {
            return true;
        }

        int tileId = tile.tileId;
        int byteIndex = tileId >> 3;
        int bitIndex = tileId & 7;

        if (byteIndex >= exploredBytes.Length)
        {
            return true;
        }

        return (exploredBytes[byteIndex] & (1 << bitIndex)) != 0;
    }

    private void MarkSurroundingTilesExplored(PlanetTile centerTile, float radiusInTiles)
    {
        if (!centerTile.Valid || exploredBytes == null)
        {
            return;
        }

        int intRadius = Mathf.CeilToInt(radiusInTiles);
        List<PlanetTile> tiles = new();
        centerTile.Layer.Filler.FloodFill(
            centerTile,
            t => Find.WorldGrid.ApproxDistanceInTiles(centerTile, t) <= radiusInTiles,
            t => tiles.Add(t),
            maxTilesToProcess: (intRadius * intRadius * 4));

        for (int i = 0; i < tiles.Count; i++)
        {
            int tileId = tiles[i].tileId;
            int byteIndex = tileId >> 3;
            int bitIndex = tileId & 7;
            if (byteIndex < exploredBytes.Length)
            {
                exploredBytes[byteIndex] |= (byte)(1 << bitIndex);
            }
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
