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
    private const float IdentificationHalfWidth = 0.030f;      // Distinct optical classification horizon ring width
    private const float InteriorHalfWidth = 0.016f;            // Fine holographic honeycomb grid line width

    private readonly HashSet<PlanetTile> activeTilesCache = new(256);
    private readonly HashSet<PlanetTile> identifiedTilesCache = new(64);
    private readonly Dictionary<PlanetTile, float> tileDistancesCache = new(256);
    private readonly Dictionary<EdgeKey, EdgeInfo> edgesCache = new(1024);
    private readonly List<int> tmpIndices = new(8);
    private readonly List<Vector3> tmpVerts = new(8);
    private readonly List<Vector3> tmpDimmingVerts = new(8);

    public static WorldDrawLayer_RadarCoverage? Instance { get; private set; }

    public WorldDrawLayer_RadarCoverage()
    {
        Instance = this;
    }

    public override bool Visible
    {
        get
        {
            if (GeoscapeMod.Settings.radarOutlineStyle == RadarOutlineStyle.None &&
                GeoscapeMod.Settings.surveillanceDimming < 0.01f)
            {
                return false;
            }

            if (GeoscapeMod.Settings.visibilityMode == VisibilityMode.Omniscient)
            {
                return false;
            }

            return Find.World != null && WorldRendererUtility.WorldRendered;
        }
    }

    public override IEnumerable Regenerate()
    {
        Instance = this;
        for (int s = 0; s < subMeshes.Count; s++)
        {
            subMeshes[s].Clear(MeshParts.All);
        }

        if (planetLayer == null || !Visible)
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

        // 2. Optional Option 3 Hybrid / Background Surveillance Dimming
        if (dimming > 0.01f && planetLayer != null)
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
                Find.WorldGrid.GetTileVertices(tile, tmpDimmingVerts);
                int vertCount = tmpDimmingVerts.Count;
                if (vertCount < 3)
                {
                    continue;
                }

                Vector3 center = Find.WorldGrid.GetTileCenter(tile);
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
            Color32 baseColor = GetPaletteBaseColor(GeoscapeMod.Settings.radarColorPalette);
            Color32 idColor = GetIdentificationHorizonColor(GeoscapeMod.Settings.radarColorPalette);

            byte alphaPerimeter = (byte)(Mathf.Clamp01(GeoscapeMod.Settings.radarOutlineOpacity) * 255);
            Color32 perimeterColor = baseColor;
            perimeterColor.a = alphaPerimeter;

            Color32 idRingColor = idColor;
            idRingColor.a = (byte)(Mathf.Clamp01(GeoscapeMod.Settings.radarOutlineOpacity * 0.90f) * 255);

            // Extract all topological edges across active sensor tiles
            edgesCache.Clear();
            foreach (PlanetTile tile in activeTilesCache)
            {
                bool isIdentified = identifiedTilesCache.Contains(tile);
                tileDistancesCache.TryGetValue(tile, out float hexesFromPerim);

                tmpIndices.Clear();
                tmpVerts.Clear();
                Find.WorldGrid.GetTileVerticesIndices(tile, tmpIndices);
                Find.WorldGrid.GetTileVertices(tile, tmpVerts);

                int count = tmpIndices.Count;
                for (int i = 0; i < count; i++)
                {
                    int idxA = tmpIndices[i];
                    int idxB = tmpIndices[(i + 1) % count];
                    EdgeKey key = new(idxA, idxB);

                    if (edgesCache.TryGetValue(key, out EdgeInfo info))
                    {
                        info.Count++;
                        if (isIdentified) info.HasIdentifiedTile = true;
                        if (!isIdentified) info.HasUnidentifiedTile = true;
                        info.HexesFromPerimeterSum += hexesFromPerim;
                        edgesCache[key] = info;
                    }
                    else
                    {
                        edgesCache[key] = new EdgeInfo
                        {
                            V0 = tmpVerts[i],
                            V1 = tmpVerts[(i + 1) % count],
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
                    halfWidth = InteriorHalfWidth;
                    float hexesFromPerimeter = info.HexesFromPerimeterSum * 0.5f;

                    // Opacity gradient: Bright at outermost hex, gradually reducing to fixed opacity at 3-4 hexes from perimeter
                    const float maxFactor = 0.52f;
                    const float minFactor = 0.12f;

                    float factor;
                    if (hexesFromPerimeter <= 1.0f)
                    {
                        factor = maxFactor;
                    }
                    else if (hexesFromPerimeter < 3.5f)
                    {
                        float t = (hexesFromPerimeter - 1.0f) / 2.5f;
                        factor = Mathf.Lerp(maxFactor, minFactor, t);
                    }
                    else
                    {
                        factor = minFactor;
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

    private static Color32 GetIdentificationHorizonColor(RadarColorPalette palette)
    {
        return palette switch
        {
            RadarColorPalette.TacticalCyan => new Color32(255, 205, 50, 255),      // Golden Telemetry Amber
            RadarColorPalette.SurveillanceAmber => new Color32(0, 230, 255, 255), // Tactical Cyan
            RadarColorPalette.EmeraldPhosphor => new Color32(255, 195, 45, 255),   // Amber Orange
            RadarColorPalette.CrimsonAlert => new Color32(255, 225, 75, 255),      // High-Visibility Warning Yellow
            RadarColorPalette.GhostWhite => new Color32(0, 230, 255, 255),        // Ice Cyan
            _ => new Color32(255, 205, 50, 255)
        };
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
