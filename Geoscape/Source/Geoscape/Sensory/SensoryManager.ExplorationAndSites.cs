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
    public bool IsExplored(PlanetTile tile)
    {
        if (!tile.Valid)
        {
            return true;
        }

        return IsTileIdExplored(tile.tileId);
    }

    public bool IsTileIdExplored(int tileId)
    {
        if (tileId < 0)
        {
            return false;
        }

        if (isExploredOverride != null)
        {
            return isExploredOverride(new PlanetTile(tileId));
        }

        if (GeoscapeRules.VisibilityMode != VisibilityMode.ClassicHazeOfWar)
        {
            return true;
        }

        if (ExternalFogCompatibility.IsExternalFogModActive && ExternalFogCompatibility.IsTileRevealedByExternalMod(tileId))
        {
            return true;
        }

        if (exploredBytes == null)
        {
            EnsureExploredArray(tileId + 1);
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

    /// <summary>
    /// Returns true if the given tile has ever been scanned within an active sensor's inner
    /// Identification ring (or revealed by a traveller rumour).
    /// Tiles that are only explored by the outer Detection ring return false until identified.
    /// </summary>
    public bool IsTileIdentified(PlanetTile tile)
    {
        if (!tile.Valid)
        {
            return true;
        }

        return IsTileIdIdentified(tile.tileId);
    }

    public bool IsTileIdIdentified(int tileId)
    {
        if (tileId < 0)
        {
            return true;
        }

        if (isIdentifiedOverride != null)
        {
            return isIdentifiedOverride(new PlanetTile(tileId));
        }

        if (isExploredOverride != null)
        {
            return isExploredOverride(new PlanetTile(tileId));
        }

        if (GeoscapeRules.VisibilityMode != VisibilityMode.ClassicHazeOfWar)
        {
            return true;
        }

        if (identifiedBytes == null)
        {
            EnsureExploredArray(tileId + 1);
            if (identifiedBytes == null)
            {
                return false;
            }
        }

        int byteIndex = tileId >> 3;
        int bitIndex = tileId & 7;

        if (byteIndex >= identifiedBytes.Length)
        {
            return false;
        }

        return (identifiedBytes[byteIndex] & (1 << bitIndex)) != 0;
    }

    /// <summary>
    /// Returns true if the world object represents a non-player static site, settlement, camp,
    /// outpost, or aftermath that requires scanning by the inner Identification ring before its
    /// identity, faction, and details are known.
    /// </summary>
    public static bool RequiresSiteIdentification(WorldObject? wo)
    {
        if (wo == null || wo.Faction?.IsPlayer == true)
        {
            return false;
        }

        if (wo is WorldObject_Incursion ||
            wo is Caravan ||
            wo is TravellingTransporters ||
            wo is RoutePlannerWaypoint)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns true if the given WorldObject has been identified by an inner Identification ring
    /// or traveller intelligence rumour.
    /// Objects that are only in the outer Detection layer (or spawned dynamically outside active
    /// identification coverage) return false until identified.
    /// </summary>
    public bool IsWorldObjectIdentified(WorldObject? wo)
    {
        if (wo == null)
        {
            return true;
        }

        if (isIdentifiedOverride != null)
        {
            return isIdentifiedOverride(wo.Tile);
        }

        if (isExploredOverride != null)
        {
            return isExploredOverride(wo.Tile);
        }

        if (GeoscapeRules.VisibilityMode != VisibilityMode.ClassicHazeOfWar)
        {
            return true;
        }

        if (!RequiresSiteIdentification(wo))
        {
            return true;
        }

        if (unidentifiedObjectIds.Contains(wo.ID))
        {
            return false;
        }

        return IsTileIdentified(wo.Tile);
    }

    /// <summary>
    /// Returns true when a WorldObject on the Geoscape should be masked as an "Unidentified site" /
    /// "Unknown site" because it has been detected in the outer Detection layer (out of the black shroud)
    /// but has not yet been scanned by the inner Identification ring.
    /// </summary>
    public static bool ShouldMaskWorldObjectAsUnknown(WorldObject? wo)
    {
        if (suppressUnknownMasking || wo == null)
        {
            return false;
        }

        VisibilityMode mode = GeoscapeRules.VisibilityMode;
        if (mode != VisibilityMode.ClassicHazeOfWar)
        {
            return false;
        }

        if (!RequiresSiteIdentification(wo))
        {
            return false;
        }

        SensoryManager? instance = Instance;
        if (instance == null)
        {
            return false;
        }

        if (isExploredOverride != null || isIdentifiedOverride != null)
        {
            return instance.IsExplored(wo.Tile) && !instance.IsWorldObjectIdentified(wo);
        }

        int revision = instance.SweepRevision;
        if (shouldMaskCacheRevision != revision ||
            shouldMaskCacheMode != mode ||
            !ReferenceEquals(shouldMaskCacheInstance, instance))
        {
            shouldMaskCache.Clear();
            shouldMaskCacheRevision = revision;
            shouldMaskCacheMode = mode;
            shouldMaskCacheInstance = instance;
        }

        int tileId = wo.Tile.Valid ? wo.Tile.tileId : -1;
        int objectId = wo.ID;
        if (shouldMaskCache.TryGetValue(wo, out CachedMaskEntry entry) &&
            entry.TileId == tileId &&
            entry.ObjectId == objectId)
        {
            return entry.Result;
        }

        bool masked = instance.IsExplored(wo.Tile) && !instance.IsWorldObjectIdentified(wo);
        if (shouldMaskCache.Count < MaxVisibilityCacheEntries)
        {
            shouldMaskCache[wo] = new CachedMaskEntry(tileId, objectId, masked);
        }

        return masked;
    }

    public void MarkWorldObjectUnidentified(WorldObject? wo)
    {
        if (wo == null || !RequiresSiteIdentification(wo))
        {
            return;
        }

        EnsureUnidentifiedTileIndex();
        if (unidentifiedObjectIds.Add(wo.ID))
        {
            IndexUnidentifiedObject(wo.ID, wo.Tile);
            SweepRevision++;
            SetSurfaceLayerDirty<WorldDrawLayer_WorldObjects_Expandable>();
            SetSurfaceLayerDirty<WorldDrawLayer_WorldObjects_NonExpandable>();
        }
        else
        {
            IndexUnidentifiedObject(wo.ID, wo.Tile);
        }
    }

    public void MarkWorldObjectIdentified(WorldObject? wo)
    {
        if (wo == null)
        {
            return;
        }

        UnindexUnidentifiedObject(wo.ID);
        bool changed = unidentifiedObjectIds.Remove(wo.ID);
        bool tileWasLandmark = false;
        if (wo.Tile.Valid)
        {
            EnsureExploredArray(wo.Tile.tileId + 1);
            int tileId = wo.Tile.tileId;
            int byteIndex = tileId >> 3;
            int bitIndex = tileId & 7;
            if (identifiedBytes != null && byteIndex < identifiedBytes.Length)
            {
                if ((identifiedBytes[byteIndex] & (1 << bitIndex)) == 0)
                {
                    identifiedBytes[byteIndex] |= (byte)(1 << bitIndex);
                    changed = true;
                }
            }

            tileWasLandmark = Find.World?.landmarks?.landmarks != null &&
                Find.World.landmarks.landmarks.Count > 0 &&
                ModsConfig.OdysseyActive &&
                Find.World.landmarks.landmarks.ContainsKey(wo.Tile);
        }

        if (changed)
        {
            SweepRevision++;
            SetSurfaceLayerDirty<WorldDrawLayer_WorldObjects_Expandable>();
            SetSurfaceLayerDirty<WorldDrawLayer_WorldObjects_NonExpandable>();
            if (tileWasLandmark)
            {
                SetSurfaceLayerDirty<WorldDrawLayer_Landmarks>();
            }
        }
    }

    public void Notify_WorldObjectRemoved(WorldObject? wo)
    {
        if (wo == null)
        {
            return;
        }

        UnindexUnidentifiedObject(wo.ID);
        unidentifiedObjectIds.Remove(wo.ID);
    }

    private readonly Dictionary<int, int> featureCenterTileCache = new();
    private readonly Dictionary<int, bool> featureExploredCache = new();
    private int featureExploredCacheRevision = -1;
    private VisibilityMode featureExploredCacheMode = (VisibilityMode)(-1);
    private readonly HashSet<int> featureCenterTilesSet = new();
    private bool featureCenterTilesIndexed = false;

    private void EnsureFeatureCenterTilesIndexed()
    {
        if (featureCenterTilesIndexed || Find.World?.features == null) return;
        featureCenterTilesSet.Clear();
        var features = Find.World.features.features;
        for (int i = 0; i < features.Count; i++)
        {
            int c = GetFeatureCenterTile(features[i]);
            if (c >= 0)
            {
                featureCenterTilesSet.Add(c);
            }
        }
        featureCenterTilesIndexed = true;
    }

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
            Vector3 center = Find.World?.grid != null ? Find.World.grid.GetTileCenter(tileId) : Vector3.zero;
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

        VisibilityMode mode = GeoscapeRules.VisibilityMode;
        if (mode != VisibilityMode.ClassicHazeOfWar)
        {
            return true;
        }

        if (exploredBytes == null)
        {
            return false;
        }

        if (isExploredOverride != null)
        {
            int overrideCenter = GetFeatureCenterTile(feature);
            return overrideCenter >= 0 && IsTileIdExplored(overrideCenter);
        }

        if (featureExploredCacheRevision != SweepRevision || featureExploredCacheMode != mode)
        {
            featureExploredCache.Clear();
            featureExploredCacheRevision = SweepRevision;
            featureExploredCacheMode = mode;
        }

        if (featureExploredCache.TryGetValue(feature.uniqueID, out bool cachedExplored))
        {
            return cachedExplored;
        }

        int centerTile = GetFeatureCenterTile(feature);
        bool explored = centerTile >= 0 && IsTileIdExplored(centerTile);
        if (featureExploredCache.Count < MaxVisibilityCacheEntries)
        {
            featureExploredCache[feature.uniqueID] = explored;
        }

        return explored;
    }

    internal void UpdateHazeActiveVision(bool mobileOnly = false)
    {
        DeepProfiler.Start("ProjectVigilance.SensorySweep.VisionUpdates");
        try
        {
            tmpActiveVisionTileIds.Clear();
            tmpActiveIdentifiedTileIds.Clear();
            tmpActiveTileDomains.Clear();
            newActiveVisionTileIds.Clear();

            if (mobileOnly && stationaryCacheValid)
            {
                tmpActiveVisionTileIds.UnionWith(stationaryVisionTileIds);
                tmpActiveIdentifiedTileIds.UnionWith(stationaryIdentifiedTileIds);
                foreach (KeyValuePair<int, SensorDomainFlags> kvp in stationaryTileDomains)
                {
                    tmpActiveTileDomains[kvp.Key] = kvp.Value;
                }

                if (Find.World?.grid != null || TileNeighborsOverride != null)
                {
                    IReadOnlyList<ISensorProvider> sensors = ActiveSensorsOverride != null ? ActiveSensorsOverride() : GetActiveSensors();
                    for (int i = 0; i < sensors.Count; i++)
                    {
                        ISensorProvider sensor = sensors[i];
                        if (!IsStationarySensor(sensor))
                        {
                            AccumulateSensorVisionTiles(sensor, tmpActiveVisionTileIds, tmpActiveIdentifiedTileIds, tmpActiveTileDomains, trackMaskedShadow: false);
                        }
                    }
                }
            }
            else
            {
                stationaryVisionTileIds.Clear();
                stationaryIdentifiedTileIds.Clear();
                stationaryTileDomains.Clear();

                if (Find.World?.grid != null || TileNeighborsOverride != null)
                {
                    IReadOnlyList<ISensorProvider> sensors = ActiveSensorsOverride != null ? ActiveSensorsOverride() : GetActiveSensors();
                    for (int i = 0; i < sensors.Count; i++)
                    {
                        ISensorProvider sensor = sensors[i];
                        if (IsStationarySensor(sensor))
                        {
                            AccumulateSensorVisionTiles(sensor, stationaryVisionTileIds, stationaryIdentifiedTileIds, stationaryTileDomains, trackMaskedShadow: false);
                        }
                        else
                        {
                            AccumulateSensorVisionTiles(sensor, tmpActiveVisionTileIds, tmpActiveIdentifiedTileIds, tmpActiveTileDomains, trackMaskedShadow: false);
                        }
                    }
                }

                stationaryCacheValid = true;
                tmpActiveVisionTileIds.UnionWith(stationaryVisionTileIds);
                tmpActiveIdentifiedTileIds.UnionWith(stationaryIdentifiedTileIds);
                foreach (KeyValuePair<int, SensorDomainFlags> kvp in stationaryTileDomains)
                {
                    tmpActiveTileDomains.TryGetValue(kvp.Key, out SensorDomainFlags existing);
                    tmpActiveTileDomains[kvp.Key] = existing | kvp.Value;
                }
            }

            newActiveVisionTileIds.UnionWith(tmpActiveVisionTileIds);

            bool activeVisionChanged = newActiveVisionTileIds.Count != currentActiveVisionTileIds.Count ||
                !newActiveVisionTileIds.SetEquals(currentActiveVisionTileIds);

            currentActiveIdentifiedTileIds.Clear();
            currentActiveIdentifiedTileIds.UnionWith(tmpActiveIdentifiedTileIds);

            currentActiveTileDomains.Clear();
            foreach (KeyValuePair<int, SensorDomainFlags> kvp in tmpActiveTileDomains)
            {
                currentActiveTileDomains[kvp.Key] = kvp.Value;
            }

            if (activeVisionChanged)
            {
                SweepRevision++;
            }

            var haze = WorldDrawLayer_HazeOfWar.Instance;
            if (haze == null || !haze.IsGeometryBuilt)
            {
                currentActiveVisionTileIds.Clear();
                currentActiveVisionTileIds.UnionWith(newActiveVisionTileIds);
                WorldDrawLayer_HazeOfWar.InvalidateHierarchySync();
                WorldDrawLayer_ShroudShell.Instance?.SetDirty();
                haze?.SetDirty();
                return;
            }

            // 1. Tiles leaving active vision: restore to Haze (if explored) or Shroud (if unexplored)
            foreach (int oldTileId in currentActiveVisionTileIds)
            {
                if (!newActiveVisionTileIds.Contains(oldTileId))
                {
                    byte nextState = IsTileIdExplored(oldTileId)
                        ? ShroudHierarchy.StateHaze
                        : ShroudHierarchy.StateShroud;
                    haze.NotifyTileState(oldTileId, nextState);
                }
            }

            // 2. Tiles entering active vision: set to Clear (100% transparent)
            foreach (int newTileId in newActiveVisionTileIds)
            {
                if (!currentActiveVisionTileIds.Contains(newTileId))
                {
                    haze.NotifyTileState(newTileId, ShroudHierarchy.StateClear);
                }
            }

            currentActiveVisionTileIds.Clear();
            currentActiveVisionTileIds.UnionWith(newActiveVisionTileIds);

            haze.FlushShroudUpdates();
        }
        finally
        {
            DeepProfiler.End();
        }
    }

    public void MarkTileExplored(PlanetTile tile)
    {
        MarkSurroundingTilesExplored(tile, 1f, isSonar: false);
    }

    public void MarkSurroundingTilesExplored(PlanetTile centerTile, float radiusInTiles, bool isSonar = false)
    {
        if (!centerTile.Valid)
        {
            return;
        }

        EnsureExploredArray(centerTile.tileId + 1);
        if (exploredBytes == null)
        {
            return;
        }

        bool externalModActive = ExternalFogCompatibility.IsExternalFogModActive;
        int maxSteps = radiusInTiles <= 0f ? 0 : Mathf.Max(1, Mathf.RoundToInt(radiusInTiles));
        bool revealedAnyObject = false;
        bool revealedAnyFeature = false;
        bool revealedAnyLandmark = false;
        bool revealedAnyTile = false;
        var haze = WorldDrawLayer_HazeOfWar.Instance;
        bool hazeReady = haze != null && haze.IsGeometryBuilt;

        bfsQueue.Clear();
        bfsVisitedTileIds.Clear();

        if (isSonar)
        {
            tmpSonarCoverageTileIds.Clear();
            SonarTraversalUtility.PopulateSonarCoverage(centerTile, maxSteps, maxIdentificationRadius: 0, tmpSonarCoverageTileIds);
            PlanetLayer? surfaceLayer = centerTile.Layer ?? Find.World?.grid?.Surface;
            foreach (int tileId in tmpSonarCoverageTileIds)
            {
                PlanetTile waterTile = ResolveTile(surfaceLayer, tileId);
                if ((tileId >> 3) >= exploredBytes.Length)
                {
                    EnsureExploredArray(tileId + 1);
                }
                int byteIndex = tileId >> 3;
                int bitIndex = tileId & 7;

                if (byteIndex < exploredBytes.Length)
                {
                    bool wasUnexplored = (exploredBytes[byteIndex] & (1 << bitIndex)) == 0;
                    if (wasUnexplored)
                    {
                        exploredBytes[byteIndex] |= (byte)(1 << bitIndex);
                        revealedAnyTile = true;

                        if (hazeReady && !currentActiveVisionTileIds.Contains(tileId))
                        {
                            haze?.NotifyTileState(tileId, ShroudHierarchy.StateHaze);
                        }

                        if (!revealedAnyObject && Find.World?.worldObjects != null && Find.World.worldObjects.AnyWorldObjectAt(waterTile))
                        {
                            revealedAnyObject = true;
                        }

                        if (!revealedAnyFeature && Find.World?.features != null)
                        {
                            EnsureFeatureCenterTilesIndexed();
                            if (featureCenterTilesSet.Contains(tileId))
                            {
                                revealedAnyFeature = true;
                            }
                        }

                        if (!revealedAnyLandmark &&
                            Find.World?.landmarks?.landmarks != null &&
                            Find.World.landmarks.landmarks.Count > 0 &&
                            ModsConfig.OdysseyActive &&
                            Find.World.landmarks.landmarks.ContainsKey(waterTile))
                        {
                            revealedAnyLandmark = true;
                        }
                    }
                }

                if (externalModActive)
                {
                    ExternalFogCompatibility.NotifyExternalModTileRevealed(tileId);
                }
            }
        }
        else
        {
            bfsQueue.Enqueue((centerTile, 0));
            bfsVisitedTileIds.Add(centerTile.tileId);

            while (bfsQueue.Count > 0)
            {
                var (currentTile, currentStep) = bfsQueue.Dequeue();
                int tileId = currentTile.tileId;
                if ((tileId >> 3) >= exploredBytes.Length)
                {
                    EnsureExploredArray(tileId + 1);
                }
                int byteIndex = tileId >> 3;
                int bitIndex = tileId & 7;

                if (byteIndex < exploredBytes.Length)
                {
                    bool wasUnexplored = (exploredBytes[byteIndex] & (1 << bitIndex)) == 0;
                    if (wasUnexplored)
                    {
                        exploredBytes[byteIndex] |= (byte)(1 << bitIndex);
                        revealedAnyTile = true;

                        // If not currently under active vision (transparent), transition from Shroud to Haze
                        if (hazeReady && !currentActiveVisionTileIds.Contains(tileId))
                        {
                            haze?.NotifyTileState(tileId, ShroudHierarchy.StateHaze);
                        }

                        // Check if an unrevealed world object or settlement was on this tile
                        if (!revealedAnyObject && Find.World?.worldObjects != null && Find.World.worldObjects.AnyWorldObjectAt(currentTile))
                        {
                            revealedAnyObject = true;
                        }

                        // Check if a landmark or mountain range center was on this tile
                        if (!revealedAnyFeature && Find.World?.features != null)
                        {
                            EnsureFeatureCenterTilesIndexed();
                            if (featureCenterTilesSet.Contains(tileId))
                            {
                                revealedAnyFeature = true;
                            }
                        }

                        if (!revealedAnyLandmark &&
                            Find.World?.landmarks?.landmarks != null &&
                            Find.World.landmarks.landmarks.Count > 0 &&
                            ModsConfig.OdysseyActive &&
                            Find.World.landmarks.landmarks.ContainsKey(currentTile))
                        {
                            revealedAnyLandmark = true;
                        }
                    }
                }

                if (externalModActive)
                {
                    ExternalFogCompatibility.NotifyExternalModTileRevealed(tileId);
                }

                if (currentStep < maxSteps && Find.World?.grid != null && currentTile.Layer != null)
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

        if (hazeReady)
        {
            haze?.FlushShroudUpdates();
        }
        else
        {
            WorldDrawLayer_HazeOfWar.InvalidateHierarchySync();
            WorldDrawLayer_ShroudShell.Instance?.SetDirty();
            haze?.SetDirty();
        }

        if (revealedAnyTile)
        {
            SweepRevision++;
        }

        // Selective dirtying: only notify vanilla layers if an actual object, feature, or landmark was uncovered
        if (revealedAnyObject)
        {
            SetSurfaceLayerDirty<WorldDrawLayer_WorldObjects_Expandable>();
            SetSurfaceLayerDirty<WorldDrawLayer_WorldObjects_NonExpandable>();
        }

        if (revealedAnyFeature || revealedAnyLandmark)
        {
            SetSurfaceLayerDirty<WorldDrawLayer_Landmarks>();
        }

        HazeReconnaissanceManager.Instance?.CheckUncoveredSites();
    }

}
