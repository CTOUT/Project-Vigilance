using System;
using System.Collections;
using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// World overlay layer rendering tactical radar boundaries, detection envelopes, and honeycomb grids
/// on the Geoscape globe. Simulates an orbital or radar-station detection envelope as seen in classic
/// X-COM geoscape surveillance, leaving planetary geography and settlements fully visible while clearly
/// delineating the sensor perimeter where moving contacts are tracked.
/// </summary>
public class WorldDrawLayer_RadarCoverage : WorldDrawLayer
{
    private const int MaxVertsPerMeshSafe = 38000;
    private const float RadarAltitudeOffset = 0.040f;          // Above surface terrain (0.00), roads (0.015), and settlements (0.030)
    private const float PerimeterHalfWidth = 0.045f;           // Crisp glowing boundary ribbon width
    private const float IdentificationHalfWidth = 0.038f;      // Distinct bold optical classification horizon ring width
    private const float InteriorHalfWidth = 0.016f;            // Baseline holographic honeycomb grid line width

    private readonly HashSet<PlanetTile> activeTilesCache = new(256);
    private readonly HashSet<PlanetTile> identifiedTilesCache = new(64);
    private readonly Dictionary<PlanetTile, float> tileDistancesCache = new(256);
    private readonly Dictionary<EdgeKey, EdgeInfo> edgesCache = new(1024);
    private readonly List<int> tmpIndices = new(8);
    private readonly List<Vector3> tmpVerts = new(8);
    private readonly List<PlanetTile> tmpNeighbors = new(8);
    private readonly List<Vector3> tmpDimmingVerts = new(8);

    public static WorldDrawLayer_RadarCoverage? Instance { get; private set; }

    public WorldDrawLayer_RadarCoverage()
    {
        Instance = this;
    }

    public override bool VisibleWhenLayerNotSelected => true;

    public override bool Visible
    {
        get
        {
            if (GeoscapeMod.Settings.radarOutlineStyle == RadarOutlineStyle.None &&
                GeoscapeMod.Settings.surveillanceDimming < 0.005f)
            {
                return false;
            }

            if (GeoscapeMod.Settings.visibilityMode == VisibilityMode.Omniscient)
            {
                return false;
            }

            return planetLayer != null && planetLayer.Visible;
        }
    }

    public static void NotifyVisualsChanged()
    {
        if (Current.ProgramState != ProgramState.Playing || Find.World == null)
        {
            return;
        }

        if (Instance != null)
        {
            Instance.SetDirty();
            Instance.RegenerateNow();
        }

        if (Find.WorldGrid?.Surface?.WorldDrawLayers != null)
        {
            var layers = Find.WorldGrid.Surface.WorldDrawLayers;
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i] is WorldDrawLayer_RadarCoverage radarLayer)
                {
                    radarLayer.SetDirty();
                    radarLayer.RegenerateNow();
                }
            }
        }
    }

    public override IEnumerable Regenerate()
    {
        Instance = this;
        for (int s = 0; s < subMeshes.Count; s++)
        {
            subMeshes[s].Clear(MeshParts.All);
        }

        WorldGrid? grid = Find.WorldGrid;
        if (planetLayer == null)
        {
            planetLayer = grid?.Surface;
        }

        if (planetLayer == null || grid == null || !Visible)
        {
            FinalizeMesh(MeshParts.All);
            yield break;
        }

        SensoryManager? sensory = SensoryManager.Instance;
        if (sensory == null)
        {
            FinalizeMesh(MeshParts.All);
            yield break;
        }

        // 1. Gather all tiles currently within active sensor range and optical identification range
        activeTilesCache.Clear();
        identifiedTilesCache.Clear();
        tileDistancesCache.Clear();
        sensory.PopulateActiveVisionTiles(activeTilesCache, identifiedTilesCache, tileDistancesCache);

        int subMeshIndex = 0;
        bool drawOutlines = GeoscapeMod.Settings.radarOutlineStyle != RadarOutlineStyle.None;
        bool drawGrid = GeoscapeMod.Settings.radarOutlineStyle == RadarOutlineStyle.BoundaryAndGrid;
        float dimming = GeoscapeMod.Settings.surveillanceDimming;

        // 2. Background Surveillance Dimming
        if (dimming > 0.005f && planetLayer != null)
        {
            byte dimAlpha = (byte)(Mathf.Clamp01(dimming) * 255);
            Color32 dimColor = new(10, 14, 20, dimAlpha);
            int tilesCount = planetLayer.TilesCount;

            for (int i = 0; i < tilesCount; i++)
            {
                PlanetTile tile = planetLayer.PlanetTileForID(i);
                if (!tile.Valid || activeTilesCache.Contains(tile))
                {
                    continue;
                }

                tmpDimmingVerts.Clear();
                grid.GetTileVertices(tile, tmpDimmingVerts);
                int vertCount = tmpDimmingVerts.Count;
                if (vertCount < 3)
                {
                    continue;
                }

                Vector3 center = grid.GetTileCenter(tile);
                center += center.normalized * (RadarAltitudeOffset * 0.5f);

                for (int v = 0; v < vertCount; v++)
                {
                    Vector3 vert = tmpDimmingVerts[v];
                    tmpDimmingVerts[v] = vert + vert.normalized * (RadarAltitudeOffset * 0.5f);
                }

                LayerSubMesh dimSubMesh = GetOrCreateSubMesh(vertCount + 1, ref subMeshIndex);
                int centerIdx = dimSubMesh.verts.Count;
                dimSubMesh.verts.Add(center);
                dimSubMesh.colors.Add(dimColor);
                dimSubMesh.uvs.Add(new Vector2(0.5f, 0.5f));

                int cornerBaseIdx = dimSubMesh.verts.Count;
                for (int v = 0; v < vertCount; v++)
                {
                    dimSubMesh.verts.Add(tmpDimmingVerts[v]);
                    dimSubMesh.colors.Add(dimColor);
                    dimSubMesh.uvs.Add(new Vector2(0.5f, 0.5f));
                }

                for (int k = 0; k < vertCount; k++)
                {
                    Vector3 vA = tmpDimmingVerts[k];
                    Vector3 vB = tmpDimmingVerts[(k + 1) % vertCount];
                    Vector3 normal = Vector3.Cross(vA - center, vB - center);

                    if (Vector3.Dot(normal, center) > 0f)
                    {
                        dimSubMesh.tris.Add(centerIdx);
                        dimSubMesh.tris.Add(cornerBaseIdx + k);
                        dimSubMesh.tris.Add(cornerBaseIdx + ((k + 1) % vertCount));
                    }
                    else
                    {
                        dimSubMesh.tris.Add(centerIdx);
                        dimSubMesh.tris.Add(cornerBaseIdx + ((k + 1) % vertCount));
                        dimSubMesh.tris.Add(cornerBaseIdx + k);
                    }
                }
            }
        }

        // 3. Render tactical radar boundary rings, identification horizons, and honeycomb grid
        if (drawOutlines && activeTilesCache.Count > 0)
        {
            Color32 baseColor = (Color32)GeoscapeMod.Settings.EffectiveRadarColor;
            Color32 idColor = GetIdentificationHorizonColor(GeoscapeMod.Settings.EffectiveRadarColor);

            byte alphaPerimeter = (byte)(Mathf.Clamp01(GeoscapeMod.Settings.radarOutlineOpacity) * 255);
            Color32 perimeterColor = baseColor;
            perimeterColor.a = alphaPerimeter;

            Color32 idRingColor = idColor;
            idRingColor.a = (byte)(Mathf.Clamp01(GeoscapeMod.Settings.radarOutlineOpacity * 0.95f) * 255);

            // Extract all topological edges across active sensor tiles
            edgesCache.Clear();
            foreach (PlanetTile tile in activeTilesCache)
            {
                bool isIdentified = identifiedTilesCache.Contains(tile);
                tileDistancesCache.TryGetValue(tile, out float hexesFromPerim);

                tmpVerts.Clear();
                tmpNeighbors.Clear();
                grid.GetTileVertices(tile, tmpVerts);
                grid.GetTileNeighbors(tile, tmpNeighbors);

                int vertCount = tmpVerts.Count;
                int neighborCount = tmpNeighbors.Count;

                for (int n = 0; n < neighborCount; n++)
                {
                    PlanetTile neighbor = tmpNeighbors[n];
                    EdgeKey key = new(tile.tileId, neighbor.tileId);

                    if (edgesCache.TryGetValue(key, out EdgeInfo info))
                    {
                        info.Count = 2;
                        if (isIdentified) info.HasIdentifiedTile = true;
                        if (!isIdentified) info.HasUnidentifiedTile = true;
                        info.HexesFromPerimeterSum += hexesFromPerim;
                        edgesCache[key] = info;
                    }
                    else
                    {
                        Vector3 neighborCenter = grid.GetTileCenter(neighbor);
                        float bestSqrDist = float.MaxValue;
                        int bestIdx = 0;

                        for (int v = 0; v < vertCount; v++)
                        {
                            Vector3 vA = tmpVerts[v];
                            Vector3 vB = tmpVerts[(v + 1) % vertCount];
                            Vector3 mid = (vA + vB) * 0.5f;
                            float sqrDist = (mid - neighborCenter).sqrMagnitude;
                            if (sqrDist < bestSqrDist)
                            {
                                bestSqrDist = sqrDist;
                                bestIdx = v;
                            }
                        }

                        edgesCache[key] = new EdgeInfo
                        {
                            V0 = tmpVerts[bestIdx],
                            V1 = tmpVerts[(bestIdx + 1) % vertCount],
                            Count = 1,
                            HasIdentifiedTile = isIdentified,
                            HasUnidentifiedTile = !isIdentified,
                            HexesFromPerimeterSum = hexesFromPerim
                        };
                    }
                }
            }

            // Generate ribbon quads for perimeter boundaries, identification horizons, and interior honeycomb lines
            foreach (KeyValuePair<EdgeKey, EdgeInfo> kvp in edgesCache)
            {
                EdgeInfo info = kvp.Value;
                bool isOuterPerimeter = info.Count == 1;
                bool isIdentificationHorizon = GeoscapeMod.Settings.showIdentificationHorizon &&
                                               info.Count == 2 &&
                                               info.HasIdentifiedTile &&
                                               info.HasUnidentifiedTile;

                if (!isOuterPerimeter && !isIdentificationHorizon && !drawGrid)
                {
                    continue;
                }

                float halfWidth;
                Color32 color;

                if (isOuterPerimeter)
                {
                    halfWidth = PerimeterHalfWidth;
                    color = perimeterColor;
                }
                else if (isIdentificationHorizon)
                {
                    halfWidth = IdentificationHalfWidth;
                    color = idRingColor;
                }
                else
                {
                    float hexesFromPerimeter = info.HexesFromPerimeterSum * 0.5f;

                    // Opacity gradient & width falloff: Bright & wide at outermost hex, gradually reducing to dim baseline at center
                    float factor;
                    if (hexesFromPerimeter <= 0.6f)
                    {
                        factor = 0.80f;
                        halfWidth = 0.024f;
                    }
                    else if (hexesFromPerimeter <= 1.6f)
                    {
                        factor = 0.52f;
                        halfWidth = 0.020f;
                    }
                    else if (hexesFromPerimeter <= 2.6f)
                    {
                        factor = 0.32f;
                        halfWidth = 0.016f;
                    }
                    else
                    {
                        factor = 0.15f;
                        halfWidth = 0.012f;
                    }

                    byte interiorAlpha = (byte)(Mathf.Clamp01(GeoscapeMod.Settings.radarOutlineOpacity * factor) * 255);
                    color = baseColor;
                    color.a = interiorAlpha;
                }

                Vector3 v0 = info.V0;
                Vector3 v1 = info.V1;
                Vector3 mid = (v0 + v1) * 0.5f;
                Vector3 normal = mid.normalized;
                Vector3 dir = (v1 - v0).normalized;
                Vector3 side = Vector3.Cross(normal, dir).normalized;

                Vector3 p0 = v0 - side * halfWidth + normal * RadarAltitudeOffset;
                Vector3 p1 = v0 + side * halfWidth + normal * RadarAltitudeOffset;
                Vector3 p2 = v1 + side * halfWidth + normal * RadarAltitudeOffset;
                Vector3 p3 = v1 - side * halfWidth + normal * RadarAltitudeOffset;

                LayerSubMesh subMesh = GetOrCreateSubMesh(4, ref subMeshIndex);
                int baseIdx = subMesh.verts.Count;

                subMesh.verts.Add(p0);
                subMesh.verts.Add(p1);
                subMesh.verts.Add(p2);
                subMesh.verts.Add(p3);

                subMesh.colors.Add(color);
                subMesh.colors.Add(color);
                subMesh.colors.Add(color);
                subMesh.colors.Add(color);

                subMesh.uvs.Add(new Vector2(0f, 0f));
                subMesh.uvs.Add(new Vector2(1f, 0f));
                subMesh.uvs.Add(new Vector2(1f, 1f));
                subMesh.uvs.Add(new Vector2(0f, 1f));

                Vector3 triNormal = Vector3.Cross(p1 - p0, p2 - p0);
                if (Vector3.Dot(triNormal, normal) > 0f)
                {
                    subMesh.tris.Add(baseIdx);
                    subMesh.tris.Add(baseIdx + 1);
                    subMesh.tris.Add(baseIdx + 2);

                    subMesh.tris.Add(baseIdx);
                    subMesh.tris.Add(baseIdx + 2);
                    subMesh.tris.Add(baseIdx + 3);
                }
                else
                {
                    subMesh.tris.Add(baseIdx);
                    subMesh.tris.Add(baseIdx + 2);
                    subMesh.tris.Add(baseIdx + 1);

                    subMesh.tris.Add(baseIdx);
                    subMesh.tris.Add(baseIdx + 3);
                    subMesh.tris.Add(baseIdx + 2);
                }
            }
        }

        FinalizeMesh(MeshParts.All);
        yield break;
    }

    private static Color32 GetPaletteBaseColor(RadarColorPalette palette)
    {
        return palette switch
        {
            RadarColorPalette.TacticalCyan => new Color32(0, 230, 255, 255),
            RadarColorPalette.SurveillanceAmber => new Color32(255, 195, 40, 255),
            RadarColorPalette.EmeraldPhosphor => new Color32(20, 240, 110, 255),
            RadarColorPalette.CrimsonAlert => new Color32(255, 45, 75, 255),
            RadarColorPalette.GhostWhite => new Color32(230, 240, 250, 255),
            _ => new Color32(0, 230, 255, 255)
        };
    }

    private static Color32 GetIdentificationHorizonColor(Color baseColor)
    {
        Color.RGBToHSV(baseColor, out float h, out float s, out float v);
        float newHue;
        if (h >= 0.20f && h <= 0.80f)
        {
            // Cool color base (cyan, emerald, blue, purple): shift to golden telemetry amber
            newHue = 0.11f; // ~40 degrees, bright gold/amber
        }
        else
        {
            // Warm or neutral base (amber, red, pink, white): shift to electric tactical cyan
            newHue = 0.52f; // ~187 degrees, electric tactical cyan
        }

        Color idColor = Color.HSVToRGB(newHue, 0.95f, 1.0f);
        return (Color32)idColor;
    }

    private LayerSubMesh GetOrCreateSubMesh(int neededVerts, ref int subMeshIndex)
    {
        while (subMeshIndex < subMeshes.Count)
        {
            LayerSubMesh existing = subMeshes[subMeshIndex];
            if (existing.verts.Count + neededVerts <= MaxVertsPerMeshSafe)
            {
                return existing;
            }
            subMeshIndex++;
        }

        LayerSubMesh newSubMesh = new(new Mesh(), WorldMaterials.VertexColorTransparent);
        subMeshes.Add(newSubMesh);
        return newSubMesh;
    }

    private readonly struct EdgeKey : IEquatable<EdgeKey>
    {
        public readonly int A;
        public readonly int B;

        public EdgeKey(int a, int b)
        {
            if (a < b)
            {
                A = a;
                B = b;
            }
            else
            {
                A = b;
                B = a;
            }
        }

        public bool Equals(EdgeKey other) => A == other.A && B == other.B;
        public override bool Equals(object? obj) => obj is EdgeKey other && Equals(other);
        public override int GetHashCode() => (A * 397) ^ B;
    }

    private struct EdgeInfo
    {
        public Vector3 V0;
        public Vector3 V1;
        public int Count;
        public bool HasIdentifiedTile;
        public bool HasUnidentifiedTile;
        public float HexesFromPerimeterSum;
    }
}

/// <summary>
/// Pre-1.7 backward-compatible alias preserving naming conventions across XML and external mods.
/// </summary>
public class WorldLayer_RadarCoverage : WorldDrawLayer_RadarCoverage
{
}
