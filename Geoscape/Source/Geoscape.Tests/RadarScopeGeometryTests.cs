using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape.Sensory;
using UnityEngine;

namespace Geoscape.Tests;

[TestClass]
public sealed class RadarScopeGeometryTests
{
    [TestMethod]
    public void should_place_each_neighbour_across_matching_edge()
    {
        Vector2 origin = new(90f, 90f);
        Vector2 hexCentre = RadarScopeGeometry.HexToPixel(origin, 0, 0);

        for (int edge = 0; edge < 6; edge++)
        {
            Vector2 c0 = RadarScopeGeometry.HexCorner(hexCentre, edge);
            Vector2 c1 = RadarScopeGeometry.HexCorner(hexCentre, edge + 1);
            Vector2 edgeMidpoint = (c0 + c1) * 0.5f;

            (int dq, int dr) = RadarScopeGeometry.NeighbourOffsets[edge];
            Vector2 neighbourCentre = RadarScopeGeometry.HexToPixel(origin, dq, dr);
            Vector2 centreMidpoint = (hexCentre + neighbourCentre) * 0.5f;

            Assert.AreEqual(centreMidpoint.x, edgeMidpoint.x, 0.01f, $"Edge {edge} X midpoint must lie halfway to neighbour ({dq}, {dr}).");
            Assert.AreEqual(centreMidpoint.y, edgeMidpoint.y, 0.01f, $"Edge {edge} Y midpoint must lie halfway to neighbour ({dq}, {dr}).");
        }
    }

    [TestMethod]
    public void should_keep_mountain_cluster_inside_identification_ring_and_beyond_ring_one()
    {
        Vector2 centre = Vector2.zero;
        float maxR = (180f * 0.5f) - 10f;
        float outerR = maxR * RadarScopePreview.OuterRadiusRatio;
        float idR = outerR * RadarScopePreview.IdentificationRadiusRatio;

        foreach ((int q, int r) in RadarScopeGeometry.MountainCluster)
        {
            int ringDist = RadarScopeGeometry.AxialDistance(q, r);
            Assert.IsGreaterThan(1, ringDist, $"Mountain cell ({q}, {r}) must lie beyond immediate ring 1, which is never masked.");

            Vector2 hexCentre = RadarScopeGeometry.HexToPixel(centre, q, r);
            for (int c = 0; c < 6; c++)
            {
                Vector2 corner = RadarScopeGeometry.HexCorner(hexCentre, c);
                Assert.IsLessThan(idR, corner.magnitude, $"Corner {c} of mountain cell ({q}, {r}) must fit inside the identification horizon ring.");
            }
        }
    }

    [TestMethod]
    public void should_mark_only_outer_edges_as_exposed()
    {
        int exposedCount = 0;
        int internalCount = 0;

        foreach ((int q, int r) in RadarScopeGeometry.MountainCluster)
        {
            for (int edge = 0; edge < 6; edge++)
            {
                if (RadarScopeGeometry.IsEdgeExposed(q, r, edge))
                {
                    exposedCount++;
                }
                else
                {
                    internalCount++;
                }
            }
        }

        // Four contiguous hexes in a chain share 3 pairs of internal edges (6 half-edges) and expose 18 boundary edges
        Assert.AreEqual(6, internalCount, "Adjacent mountain cluster cells must suppress 6 shared internal half-edges.");
        Assert.AreEqual(18, exposedCount, "Contiguous 4-hex ridge must expose 18 outer contour edges.");
    }

    [TestMethod]
    public void should_clip_segment_to_hex_bounds()
    {
        Vector2 hexCentre = new(50f, 50f);
        float size = RadarScopeGeometry.HexSize;
        float apothem = size * RadarScopeGeometry.HalfSqrt3;

        // 1. Horizontal line spanning past both vertical edges of the pointy-top hex
        Vector2 a = new(hexCentre.x - 30f, hexCentre.y);
        Vector2 b = new(hexCentre.x + 30f, hexCentre.y);
        bool hit = RadarScopeGeometry.TryClipSegmentToHex(hexCentre, size, ref a, ref b);

        Assert.IsTrue(hit, "Horizontal line through hex centre must intersect the hexagon.");
        Assert.AreEqual(hexCentre.x - apothem, a.x, 0.01f);
        Assert.AreEqual(hexCentre.x + apothem, b.x, 0.01f);

        // 2. Segment completely outside the hex
        Vector2 outA = new(hexCentre.x - 30f, hexCentre.y + 30f);
        Vector2 outB = new(hexCentre.x + 30f, hexCentre.y + 30f);
        bool missed = RadarScopeGeometry.TryClipSegmentToHex(hexCentre, size, ref outA, ref outB);

        Assert.IsFalse(missed, "Segment passing far below the hex must be rejected.");
    }
}
