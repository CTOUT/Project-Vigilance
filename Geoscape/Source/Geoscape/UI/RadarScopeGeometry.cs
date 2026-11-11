using UnityEngine;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Pure, allocation-free pointy-top hexagonal geometry helpers for the Mod Settings
/// live radar scope preview. Shared by the honeycomb grid and the terrain masking
/// mountain cluster so shadowed cells align with the grid.
/// </summary>
internal static class RadarScopeGeometry
{
    public const float HexSize = 9f;
    public const float Sqrt3 = 1.7320508f;
    public const float HalfSqrt3 = 0.8660254f;

    /// <summary>
    /// Pointy-top axial neighbour offsets ordered so index <c>i</c> lies across
    /// edge <c>i</c> (connecting corner <c>i</c> to corner <c>(i + 1) % 6</c>).
    /// </summary>
    public static readonly (int q, int r)[] NeighbourOffsets =
    {
        (1, 0),   // Edge 0: East
        (0, 1),   // Edge 1: South-East
        (-1, 1),  // Edge 2: South-West
        (-1, 0),  // Edge 3: West
        (0, -1),  // Edge 4: North-West
        (1, -1),  // Edge 5: North-East
    };

    /// <summary>
    /// Contiguous four-hex mountain ridge at axial ring 2 (beyond immediate ring 1,
    /// which is never masked, and strictly inside the identification horizon ring).
    /// </summary>
    public static readonly (int q, int r)[] MountainCluster =
    {
        (0, -2),
        (1, -2),
        (2, -2),
        (2, -1),
    };

    private static readonly Vector2[] SlabNormals =
    {
        new(1f, 0f),
        new(0.5f, HalfSqrt3),
        new(-0.5f, HalfSqrt3),
    };

    /// <summary>
    /// Converts pointy-top axial coordinates <c>(q, r)</c> to 2D pixel coordinates
    /// relative to <paramref name="origin"/>.
    /// </summary>
    public static Vector2 HexToPixel(Vector2 origin, int q, int r, float size = HexSize)
    {
        float x = size * Sqrt3 * (q + (0.5f * r));
        float y = size * 1.5f * r;
        return new Vector2(origin.x + x, origin.y + y);
    }

    /// <summary>
    /// Returns the axial ring distance from <c>(0, 0)</c> to <c>(q, r)</c>.
    /// </summary>
    public static int AxialDistance(int q, int r)
    {
        int s = -q - r;
        return (Mathf.Abs(q) + Mathf.Abs(r) + Mathf.Abs(s)) / 2;
    }

    /// <summary>
    /// Returns corner <paramref name="index"/> (0..5) of a pointy-top hexagon at
    /// <paramref name="hexCentre"/>, where corner 0 is at -30 degrees and indices
    /// advance in 60-degree steps.
    /// </summary>
    public static Vector2 HexCorner(Vector2 hexCentre, int index, float size = HexSize)
    {
        int wrapped = ((index % 6) + 6) % 6;
        return wrapped switch
        {
            0 => new Vector2(hexCentre.x + (size * HalfSqrt3), hexCentre.y - (size * 0.5f)),
            1 => new Vector2(hexCentre.x + (size * HalfSqrt3), hexCentre.y + (size * 0.5f)),
            2 => new Vector2(hexCentre.x, hexCentre.y + size),
            3 => new Vector2(hexCentre.x - (size * HalfSqrt3), hexCentre.y + (size * 0.5f)),
            4 => new Vector2(hexCentre.x - (size * HalfSqrt3), hexCentre.y - (size * 0.5f)),
            _ => new Vector2(hexCentre.x, hexCentre.y - size),
        };
    }

    /// <summary>
    /// Returns true when <c>(q, r)</c> is one of the mountain cluster cells.
    /// </summary>
    public static bool IsInMountainCluster(int q, int r)
    {
        for (int i = 0; i < MountainCluster.Length; i++)
        {
            if (MountainCluster[i].q == q && MountainCluster[i].r == r)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns true when edge <paramref name="edgeIndex"/> (0..5) of mountain hex
    /// <c>(q, r)</c> borders a non-mountain hex and should render a contour ribbon.
    /// </summary>
    public static bool IsEdgeExposed(int q, int r, int edgeIndex)
    {
        (int dq, int dr) = NeighbourOffsets[((edgeIndex % 6) + 6) % 6];
        return !IsInMountainCluster(q + dq, r + dr);
    }

    /// <summary>
    /// Clips segment <c>[a, b]</c> against the regular pointy-top hexagon centred at
    /// <paramref name="hexCentre"/> with radius <paramref name="size"/> using three
    /// symmetric slab tests. Returns false when the segment lies completely outside.
    /// </summary>
    public static bool TryClipSegmentToHex(Vector2 hexCentre, float size, ref Vector2 a, ref Vector2 b)
    {
        float apothem = size * HalfSqrt3;
        Vector2 d = b - a;
        Vector2 relA = a - hexCentre;
        float tMin = 0f;
        float tMax = 1f;

        for (int i = 0; i < SlabNormals.Length; i++)
        {
            Vector2 n = SlabNormals[i];
            float p0 = (n.x * relA.x) + (n.y * relA.y);
            float dir = (n.x * d.x) + (n.y * d.y);

            if (Mathf.Abs(dir) < 1e-6f)
            {
                if (p0 < -apothem || p0 > apothem)
                {
                    return false;
                }
                continue;
            }

            float t1 = (-apothem - p0) / dir;
            float t2 = (apothem - p0) / dir;
            if (t1 > t2)
            {
                (t1, t2) = (t2, t1);
            }

            if (t1 > tMin)
            {
                tMin = t1;
            }
            if (t2 < tMax)
            {
                tMax = t2;
            }
            if (tMin > tMax)
            {
                return false;
            }
        }

        Vector2 origA = a;
        a = origA + (d * tMin);
        b = origA + (d * tMax);
        return true;
    }
}
