using System.Collections;
using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Native world mesh overlay rendering the 3-tier Classic Haze of War:
/// 1. Unexplored sectors: Deep obsidian shroud of war.
/// 2. Explored but unmonitored sectors: Translucent desaturated grey/slate veil showing static terrain.
/// 3. Active vision sectors: Crystal clear (zero mesh rendered).
/// Adheres to zero-allocation hot paths and regenerates asynchronously on dirty flag.
/// </summary>
public class WorldDrawLayer_HazeOfWar : WorldDrawLayer
{
    private const int MaxVertsPerMeshSafe = 38000;
    private const float HazeAltitudeOffset = 0.042f;                        // Altitude above WorldObject (0.03) and Caravan (0.035), below Motes (0.05)
    private static readonly Color32 ShroudColor = new(6, 8, 12, 255);      // 100% opaque dark obsidian shroud
    private static readonly Color32 HazeColor = new(36, 42, 50, 140);       // ~55% translucent slate grey

    private readonly List<Vector3> tmpTileVerts = new(8);

    public static WorldDrawLayer_HazeOfWar? Instance { get; private set; }

    public WorldDrawLayer_HazeOfWar()
    {
        Instance = this;
    }

    public override bool Visible
    {
        get
        {
            if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.ClassicHazeOfWar)
            {
                return false;
            }

            // Only render when the world camera driver is active on the globe
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

        if (planetLayer == null || GeoscapeMod.Settings.visibilityMode != VisibilityMode.ClassicHazeOfWar)
        {
            FinalizeMesh(MeshParts.All);
            yield break;
        }

        SensoryManager? sensory = SensoryManager.Instance;
        int tilesCount = planetLayer.TilesCount;
        int subMeshIndex = 0;

        for (int i = 0; i < tilesCount; i++)
        {
            PlanetTile tile = planetLayer.PlanetTileForID(i);
            if (!tile.Valid)
            {
                continue;
            }

            // 1. If actively monitored by lookouts or radar, sector is completely clear
            if (sensory != null && sensory.HasActiveVision(tile))
            {
                continue;
            }

            // 2. Determine whether tile is shrouded (unexplored) or hazed (explored but unmonitored)
            bool isExplored = sensory != null && sensory.IsExplored(tile);
            Color32 tileColor = isExplored ? HazeColor : ShroudColor;

            // 3. Assemble tile vertices offset above surface terrain, roads, and world objects
            tmpTileVerts.Clear();
            Find.WorldGrid.GetTileVertices(tile, tmpTileVerts);
            int vertCount = tmpTileVerts.Count;
            if (vertCount < 3)
            {
                continue;
            }

            Vector3 center = Find.WorldGrid.GetTileCenter(tile);
            center += center.normalized * HazeAltitudeOffset;

            for (int v = 0; v < vertCount; v++)
            {
                Vector3 vert = tmpTileVerts[v];
                tmpTileVerts[v] = vert + vert.normalized * HazeAltitudeOffset;
            }

            // 4. Retrieve or allocate a submesh with headroom for this polygon
            LayerSubMesh subMesh = GetOrCreateSubMesh(vertCount + 1, ref subMeshIndex);

            int centerIdx = subMesh.verts.Count;
            subMesh.verts.Add(center);
            subMesh.colors.Add(tileColor);
            subMesh.uvs.Add(new Vector2(0.5f, 0.5f));

            int cornerBaseIdx = subMesh.verts.Count;
            for (int v = 0; v < vertCount; v++)
            {
                subMesh.verts.Add(tmpTileVerts[v]);
                subMesh.colors.Add(tileColor);
                subMesh.uvs.Add(new Vector2(0.5f, 0.5f));
            }

            // 5. Construct fan triangles with outward normal orientation
            for (int k = 0; k < vertCount; k++)
            {
                Vector3 vA = tmpTileVerts[k];
                Vector3 vB = tmpTileVerts[(k + 1) % vertCount];
                Vector3 normal = Vector3.Cross(vA - center, vB - center);

                if (Vector3.Dot(normal, center) > 0f)
                {
                    subMesh.tris.Add(centerIdx);
                    subMesh.tris.Add(cornerBaseIdx + k);
                    subMesh.tris.Add(cornerBaseIdx + ((k + 1) % vertCount));
                }
                else
                {
                    subMesh.tris.Add(centerIdx);
                    subMesh.tris.Add(cornerBaseIdx + ((k + 1) % vertCount));
                    subMesh.tris.Add(cornerBaseIdx + k);
                }
            }
        }

        FinalizeMesh(MeshParts.All);
        yield break;
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
}

/// <summary>
/// Pre-1.7 backward-compatible alias preserving naming conventions across XML and external mods.
/// </summary>
public class WorldLayer_HazeOfWar : WorldDrawLayer_HazeOfWar
{
}
