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
    public void MarkTileIdentified(PlanetTile tile)
    {
        MarkSurroundingTilesIdentified(tile, 0, isSonar: false);
    }

    private void IndexUnidentifiedObject(int objectId, PlanetTile tile)
    {
        if (!tile.Valid)
        {
            return;
        }

        if (unidentifiedObjectTileById.TryGetValue(objectId, out int previousTileId))
        {
            if (previousTileId == tile.tileId)
            {
                return;
            }
            UnindexUnidentifiedObject(objectId);
        }

        if (!unidentifiedObjectIdsByTile.TryGetValue(tile.tileId, out List<int>? list))
        {
            if (pooledIdLists.Count > 0)
            {
                int last = pooledIdLists.Count - 1;
                list = pooledIdLists[last];
                pooledIdLists.RemoveAt(last);
            }
            else
            {
                list = new List<int>(2);
            }
            unidentifiedObjectIdsByTile[tile.tileId] = list;
        }

        list.Add(objectId);
        unidentifiedObjectTileById[objectId] = tile.tileId;
    }

    private void UnindexUnidentifiedObject(int objectId)
    {
        if (!unidentifiedObjectTileById.TryGetValue(objectId, out int tileId))
        {
            return;
        }

        unidentifiedObjectTileById.Remove(objectId);
        if (unidentifiedObjectIdsByTile.TryGetValue(tileId, out List<int>? list))
        {
            list.Remove(objectId);
            if (list.Count == 0)
            {
                unidentifiedObjectIdsByTile.Remove(tileId);
                pooledIdLists.Add(list);
            }
        }
    }

    private void ClearUnidentifiedTileIndex()
    {
        foreach (KeyValuePair<int, List<int>> kvp in unidentifiedObjectIdsByTile)
        {
            kvp.Value.Clear();
            pooledIdLists.Add(kvp.Value);
        }
        unidentifiedObjectIdsByTile.Clear();
        unidentifiedObjectTileById.Clear();
    }

    private void EnsureUnidentifiedTileIndex()
    {
        if (!unidentifiedTileIndexDirty)
        {
            return;
        }

        unidentifiedTileIndexDirty = false;
        ClearUnidentifiedTileIndex();

        if (unidentifiedObjectIds.Count == 0)
        {
            return;
        }

        if (Find.World?.worldObjects != null)
        {
            var objs = Find.World.worldObjects.AllWorldObjects;
            for (int i = 0; i < objs.Count; i++)
            {
                WorldObject obj = objs[i];
                if (obj != null && unidentifiedObjectIds.Contains(obj.ID))
                {
                    IndexUnidentifiedObject(obj.ID, obj.Tile);
                }
            }
        }
        else
        {
            foreach (int objId in unidentifiedObjectIds)
            {
                WorldObject? obj = HazeReconnaissanceManager.FindWorldObjectById(objId);
                if (obj != null)
                {
                    IndexUnidentifiedObject(objId, obj.Tile);
                }
            }
        }
    }

    private void TryIdentifyUnidentifiedObjectsAt(int tileId, ref bool identifiedAnyObject, ref bool identifiedAnyTile)
    {
        if (unidentifiedObjectIds.Count == 0)
        {
            return;
        }

        EnsureUnidentifiedTileIndex();

        if (!unidentifiedObjectIdsByTile.TryGetValue(tileId, out List<int>? objectIds))
        {
            return;
        }

        for (int i = 0; i < objectIds.Count; i++)
        {
            int objId = objectIds[i];
            if (unidentifiedObjectIds.Remove(objId))
            {
                identifiedAnyObject = true;
                identifiedAnyTile = true;
            }
            unidentifiedObjectTileById.Remove(objId);
        }

        objectIds.Clear();
        pooledIdLists.Add(objectIds);
        unidentifiedObjectIdsByTile.Remove(tileId);
    }

    private void IdentifyTileAndObjects(PlanetTile tile, ref bool identifiedAnyTile, ref bool identifiedAnyObject, ref bool identifiedAnyLandmark)
    {
        if (identifiedBytes == null)
        {
            return;
        }

        int tileId = tile.tileId;
        if ((tileId >> 3) >= identifiedBytes.Length)
        {
            EnsureExploredArray(tileId + 1);
        }

        int byteIndex = tileId >> 3;
        int bitIndex = tileId & 7;

        if (exploredBytes != null && byteIndex < exploredBytes.Length)
        {
            exploredBytes[byteIndex] |= (byte)(1 << bitIndex);
        }

        if (byteIndex < identifiedBytes.Length)
        {
            bool wasUnidentified = (identifiedBytes[byteIndex] & (1 << bitIndex)) == 0;
            if (wasUnidentified)
            {
                identifiedBytes[byteIndex] |= (byte)(1 << bitIndex);
                identifiedAnyTile = true;
                tmpNewlyIdentifiedTileIds.Add(tileId);

                if (!identifiedAnyLandmark &&
                    Find.World?.landmarks?.landmarks != null &&
                    Find.World.landmarks.landmarks.Count > 0 &&
                    ModsConfig.OdysseyActive &&
                    Find.World.landmarks.landmarks.ContainsKey(tile))
                {
                    identifiedAnyLandmark = true;
                }
            }
        }

        TryIdentifyUnidentifiedObjectsAt(tileId, ref identifiedAnyObject, ref identifiedAnyTile);
    }

    public void MarkSurroundingTilesIdentified(PlanetTile centerTile, int radiusInTiles, bool isSonar = false)
    {
        if (!centerTile.Valid || radiusInTiles < 0)
        {
            return;
        }

        EnsureExploredArray(centerTile.tileId + 1);
        if (identifiedBytes == null)
        {
            return;
        }

        int maxSteps = radiusInTiles;
        bool identifiedAnyObject = false;
        bool identifiedAnyLandmark = false;
        bool identifiedAnyTile = false;

        tmpNewlyIdentifiedTileIds.Clear();
        bfsQueue.Clear();
        bfsVisitedTileIds.Clear();

        if (isSonar)
        {
            tmpSonarCoverageTileIds.Clear();
            tmpShadowCandidateTileIds.Clear();
            SonarTraversalUtility.PopulateSonarCoverage(centerTile, maxSteps, maxIdentificationRadius: maxSteps, outDetectionTileIds: tmpSonarCoverageTileIds, outIdentificationTileIds: tmpShadowCandidateTileIds);
            PlanetLayer? surfaceLayer = centerTile.Layer ?? Find.World?.grid?.Surface;
            foreach (int waterTileId in tmpShadowCandidateTileIds)
            {
                IdentifyTileAndObjects(ResolveTile(surfaceLayer, waterTileId), ref identifiedAnyTile, ref identifiedAnyObject, ref identifiedAnyLandmark);
            }
            tmpSonarCoverageTileIds.Clear();
            tmpShadowCandidateTileIds.Clear();
        }
        else
        {
            bfsQueue.Enqueue((centerTile, 0));
            bfsVisitedTileIds.Add(centerTile.tileId);

            while (bfsQueue.Count > 0)
            {
                var (currentTile, currentStep) = bfsQueue.Dequeue();
                IdentifyTileAndObjects(currentTile, ref identifiedAnyTile, ref identifiedAnyObject, ref identifiedAnyLandmark);

                if (currentStep < maxSteps && (TileNeighborsOverride != null || (Find.World?.grid != null && currentTile.Layer != null)))
                {
                    tmpNeighbors.Clear();
                    if (TileNeighborsOverride != null)
                    {
                        TileNeighborsOverride(currentTile, tmpNeighbors);
                    }
                    else
                    {
                        currentTile.Layer.GetTileNeighbors(currentTile, tmpNeighbors);
                    }
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

        if (!identifiedAnyObject && tmpNewlyIdentifiedTileIds.Count > 0 && Find.World?.worldObjects != null)
        {
            var objs = Find.World.worldObjects.AllWorldObjects;
            for (int o = 0; o < objs.Count; o++)
            {
                WorldObject? obj = objs[o];
                if (obj != null && obj.Tile.Valid && tmpNewlyIdentifiedTileIds.Contains(obj.Tile.tileId))
                {
                    identifiedAnyObject = true;
                    break;
                }
            }
        }
        tmpNewlyIdentifiedTileIds.Clear();

        if (identifiedAnyTile || identifiedAnyObject || identifiedAnyLandmark)
        {
            SweepRevision++;
        }

        if (identifiedAnyObject)
        {
            SetSurfaceLayerDirty<WorldDrawLayer_WorldObjects_Expandable>();
            SetSurfaceLayerDirty<WorldDrawLayer_WorldObjects_NonExpandable>();
        }

        if (identifiedAnyLandmark)
        {
            SetSurfaceLayerDirty<WorldDrawLayer_Landmarks>();
        }

        HazeReconnaissanceManager.Instance?.CheckUncoveredSites();
    }

    /// <summary>
    /// Invalidates cached sensory state and forces an immediate sweep and overlay regeneration
    /// so newly arrived colonies, caravans, and drop pods reveal terrain immediately.
    /// </summary>
    public void RefreshImmediateVision()
    {
        Notify_SensorRadiusChanged();
        if (WorldDrawLayer_ShroudShell.Instance is { IsGeometryBuilt: true } shell)
        {
            WorldDrawLayer_HazeOfWar.RequestLayerRefresh(shell);
        }
        if (WorldDrawLayer_HazeOfWar.Instance is { IsGeometryBuilt: true } haze)
        {
            WorldDrawLayer_HazeOfWar.RequestLayerRefresh(haze);
        }
        if (Find.World?.grid?.Surface != null)
        {
            WorldDrawLayer_HazeOfWar.RequestLayerRefresh(WorldDrawLayer_RadarCoverage.Instance);
        }
    }

    /// <summary>
    /// Triggered when scout or radar detection radii are adjusted in Mod Settings,
    /// immediately dirtying and regenerating the fog of war overlay to reflect the new visual range.
    /// </summary>
    public void Notify_SensorRadiusChanged()
    {
        stationaryExploredRadius.Clear();
        stationaryIdentifiedRadius.Clear();
        lastCaravanTiles.Clear();
        lastActiveSensorStates.Clear();
        currentActiveVisionTileIds.Clear();
        currentActiveIdentifiedTileIds.Clear();
        currentActiveTileDomains.Clear();
        InvalidateStationaryVisionCache();
        SweepRevision++;
        PerformSensorySweep();
        WorldDrawLayer_HazeOfWar.InvalidateHierarchySync();
        WorldDrawLayer_ShroudShell.Instance?.SetDirty();
        WorldDrawLayer_HazeOfWar.Instance?.SetDirty();
        WorldDrawLayer_RadarCoverage.Instance?.SetDirty();
        SetSurfaceLayerDirty<WorldDrawLayer_WorldObjects_Expandable>();
        SetSurfaceLayerDirty<WorldDrawLayer_WorldObjects_NonExpandable>();
        SetSurfaceLayerDirty<WorldDrawLayer_Landmarks>();
    }

    private static void SetSurfaceLayerDirty<T>() where T : WorldDrawLayer
    {
        if (Find.World?.grid?.Surface is { WorldDrawLayers: not null } surface)
        {
            surface.GetLayer<T>()?.SetDirty();
        }
    }

    private void EnsureExploredArray(int minTileCount = 0)
    {
        int totalTiles = Find.World?.grid != null ? Find.World.grid.TilesCount : Mathf.Max(minTileCount, 2048);
        if (totalTiles <= 0)
        {
            return;
        }

        int requiredBytes = (Mathf.Max(totalTiles, minTileCount) + 7) / 8;
        bool hasWorldGrid = Find.World?.grid != null && Find.World.grid.TilesCount > 0;
        if (exploredBytes == null)
        {
            exploredBytes = new byte[requiredBytes];
        }
        else if (exploredBytes.Length < requiredBytes || (hasWorldGrid && exploredBytes.Length > requiredBytes))
        {
            Array.Resize(ref exploredBytes, requiredBytes);
        }

        if (identifiedBytes == null)
        {
            identifiedBytes = new byte[requiredBytes];
        }
        else if (identifiedBytes.Length < requiredBytes || (hasWorldGrid && identifiedBytes.Length > requiredBytes))
        {
            Array.Resize(ref identifiedBytes, requiredBytes);
        }
    }

    private bool loadedFromSave;

    /// <summary>
    /// Indicates whether this component was deserialized from save XML.
    /// Returns false if instantiated dynamically on an existing save via World.FillComponents().
    /// </summary>
    public bool LoadedFromSave
    {
        get => loadedFromSave;
        internal set => loadedFromSave = value;
    }

    public override void ExposeData()
    {
        base.ExposeData();

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            loadedFromSave = true;
        }

        Scribe_Values.Look(ref nextContactTrackingId, "nextContactTrackingId", 1);

        if (Scribe.mode == LoadSaveMode.Saving)
        {
            if (exploredBytes != null)
            {
                DataExposeUtility.LookByteArray(ref exploredBytes, "exploredTilesBitset");
            }
            if (identifiedBytes != null)
            {
                DataExposeUtility.LookByteArray(ref identifiedBytes, "identifiedTilesBitset");
            }
        }
        else if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            DataExposeUtility.LookByteArray(ref exploredBytes, "exploredTilesBitset");
            DataExposeUtility.LookByteArray(ref identifiedBytes, "identifiedTilesBitset");
        }

        Scribe_Collections.Look(ref unidentifiedObjectIds, "unidentifiedObjectIds", LookMode.Value);
        unidentifiedObjectIds ??= new HashSet<int>();
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            unidentifiedTileIndexDirty = true;
        }
    }
}
