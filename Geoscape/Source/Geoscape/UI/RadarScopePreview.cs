using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Renders the 180x180 live tactical radar HUD scope preview inside the Mod Settings
/// Radar tab. Mirrors the layering and gating rules of <see cref="WorldDrawLayer_RadarCoverage"/>.
/// </summary>
internal static class RadarScopePreview
{
    public const float OuterRadiusRatio = 0.90f;
    public const float IdentificationRadiusRatio = 0.64f;

    private static readonly Color BackdropColor = new(0.03f, 0.05f, 0.08f, 0.95f);
    private static readonly Color CornerAccentColor = new(0f, 0.85f, 1f, 0.75f);
    private static readonly Color CrtRangeRingColor = new(0.12f, 0.28f, 0.35f, 0.35f);
    private static readonly Color CrosshairColor = new(0.15f, 0.35f, 0.42f, 0.40f);
    private static readonly Color MountainFillColor = new(0.36f, 0.27f, 0.18f, 0.55f);
    private static readonly Color MountainOutlineMutedColor = new(0.50f, 0.38f, 0.25f, 0.55f);
    private static readonly Color ShadowHatchColor = new(1f, 0.68f, 0.16f, 0.55f);
    private static readonly Color ShadowContourColor = new(1f, 0.78f, 0.22f, 0.92f);
    private static readonly Color ColonyBlipColor = new(0f, 0.95f, 1f, 0.95f);
    private static readonly Color IdentifiedBlipColor = new(0.25f, 1f, 0.5f, 0.92f);
    private static readonly Color UnknownBlipColor = new(1f, 0.80f, 0.20f, 0.95f);
    private static readonly Color ScopeLabelColor = new(0f, 0.85f, 1f, 0.85f);
    private static readonly Color IdentifiedLabelColor = new(0.4f, 1f, 0.6f, 0.88f);
    private static readonly Color UnknownLabelColor = new(1f, 0.85f, 0.2f, 0.92f);
    private static readonly Color ModeTagColor = new(0.55f, 0.70f, 0.80f, 0.68f);

    private const string ScopeLegendTooltip =
        "Live Tactical Radar Scope Preview\n\n" +
        "• Cyan Core: Player colony radar origin\n" +
        "• Outer Ring: Maximum detection perimeter\n" +
        "• Inner Ring: Identification horizon (contacts classified into faction/force)\n" +
        "• Brown Hex Ridge: Mountainous terrain cluster inside identification range\n" +
        "• Amber Hatching / Dashed Outline: Terrain masking indicators on mountain hexes\n" +
        "• Green TR-1: Identified track on open ground inside identification horizon\n" +
        "• Amber ?: Unidentified contact (either beyond identification horizon, or concealed inside a mountain radar shadow)";

    public static void Draw(Rect rect, GeoscapeSettings settings)
    {
        // 1. CRT Scope Backdrop
        Widgets.DrawBoxSolid(rect, BackdropColor);

        Vector2 centre = rect.center;
        float maxR = (rect.width * 0.5f) - 10f;
        float outerR = maxR * OuterRadiusRatio;
        float idR = outerR * IdentificationRadiusRatio;

        bool drawOutlines = settings.radarOutlineStyle != RadarOutlineStyle.None;

        // 2. Surveillance Background Dimming (applied outside the detection perimeter only when radar overlay is active)
        if (drawOutlines && settings.surveillanceDimming > 0.005f)
        {
            DrawExteriorDimming(rect, centre, outerR, settings.surveillanceDimming);
        }

        // 3. Faint CRT Range Rings & Cardinal Crosshairs
        DrawCircleOutline(centre, maxR * 0.35f, CrtRangeRingColor, 1f, 24);
        DrawCircleOutline(centre, maxR * 0.70f, CrtRangeRingColor, 1f, 32);
        DrawCircleOutline(centre, maxR, CrtRangeRingColor, 1f, 36);

        Widgets.DrawLine(new Vector2(rect.x + 6f, centre.y), new Vector2(centre.x - 8f, centre.y), CrosshairColor, 1f);
        Widgets.DrawLine(new Vector2(centre.x + 8f, centre.y), new Vector2(rect.xMax - 6f, centre.y), CrosshairColor, 1f);
        Widgets.DrawLine(new Vector2(centre.x, rect.y + 6f), new Vector2(centre.x, centre.y - 8f), CrosshairColor, 1f);
        Widgets.DrawLine(new Vector2(centre.x, centre.y + 8f), new Vector2(centre.x, rect.yMax - 6f), CrosshairColor, 1f);

        // 4. Honeycomb Hex Grid (aligned with the axial lattice used by the mountain cluster)
        if (settings.radarOutlineStyle == RadarOutlineStyle.BoundaryAndGrid)
        {
            Color hexCol = settings.EffectiveRadarColor;
            hexCol.a *= Mathf.Clamp01(settings.honeycombGridOpacity * 0.52f);
            DrawAxialHoneycomb(centre, outerR - 3f, hexCol);
        }

        // 5. Mountain Cluster Backdrop (always visible so 'None' clearly shows undecorated mountain hexes)
        DrawMountainClusterTerrain(centre);

        // 6. Terrain Masking Overlays (gated by showIdentificationHorizon, matching WorldDrawLayer_RadarCoverage)
        if (drawOutlines && settings.showIdentificationHorizon && settings.radarTerrainShadowStyle != RadarTerrainShadowStyle.None)
        {
            bool drawHatching = settings.radarTerrainShadowStyle == RadarTerrainShadowStyle.CellHatching ||
                                settings.radarTerrainShadowStyle == RadarTerrainShadowStyle.Both;
            bool drawContours = settings.radarTerrainShadowStyle == RadarTerrainShadowStyle.DashedContours ||
                                settings.radarTerrainShadowStyle == RadarTerrainShadowStyle.Both;

            if (drawHatching)
            {
                DrawMountainClusterHatching(centre);
            }

            if (drawContours)
            {
                DrawMountainClusterContours(centre);
            }
        }

        // 7. Inner Identification Horizon Ring
        if (drawOutlines && settings.showIdentificationHorizon)
        {
            Color idCol = settings.EffectiveIdentificationColor;
            idCol.a *= Mathf.Clamp01(settings.identificationRingOpacity);
            DrawCircleOutline(centre, idR, idCol, 2f, 36);
        }

        // 8. Outer Detection Perimeter Ring
        if (drawOutlines)
        {
            Color outerCol = settings.EffectiveRadarColor;
            outerCol.a *= Mathf.Clamp01(settings.radarOutlineOpacity);
            DrawCircleOutline(centre, outerR, outerCol, 2f, 40);
        }

        // 9. Tactical Bezel & Corner Brackets
        DrawCornerBrackets(rect);
        Widgets.DrawBox(rect, 1, BaseContent.GreyTex);

        // 10. Sample Telemetry Blips
        // Colony Core
        Widgets.DrawBoxSolid(new Rect(centre.x - 3f, centre.y - 3f, 6f, 6f), ColonyBlipColor);

        // Classified track on open ground inside identification horizon (TR-1)
        Vector2 idContactPos = centre + new Vector2(-idR * 0.55f, idR * 0.42f);
        Widgets.DrawBoxSolid(new Rect(idContactPos.x - 2.5f, idContactPos.y - 2.5f, 5f, 5f), IdentifiedBlipColor);

        // Unclassified contact in outer detection zone beyond identification horizon (?)
        Vector2 outerUnknownPos = centre + new Vector2(outerR * 0.68f, outerR * 0.36f);
        Widgets.DrawBoxSolid(new Rect(outerUnknownPos.x - 2.5f, outerUnknownPos.y - 2.5f, 5f, 5f), UnknownBlipColor);

        // Masked contact inside the mountain cluster (within identification range, but concealed by terrain shadow)
        Vector2 maskedPos = RadarScopeGeometry.HexToPixel(centre, 1, -2);
        Widgets.DrawBoxSolid(new Rect(maskedPos.x - 2.5f, maskedPos.y - 2.5f, 5f, 5f), UnknownBlipColor);

        // 11. Scope Labels & Legend Tooltip
        GameFont prevFont = Text.Font;
        TextAnchor prevAnchor = Text.Anchor;
        Color prevColor = GUI.color;

        Text.Font = GameFont.Tiny;
        GUI.color = ScopeLabelColor;
        Widgets.Label(new Rect(rect.x + 5f, rect.y + 3f, 95f, 16f), "HUD SCOPE");

        GUI.color = IdentifiedLabelColor;
        Widgets.Label(new Rect(idContactPos.x + 4f, idContactPos.y - 7f, 28f, 14f), "TR-1");

        GUI.color = UnknownLabelColor;
        Widgets.Label(new Rect(outerUnknownPos.x + 4f, outerUnknownPos.y - 7f, 14f, 14f), "?");
        Widgets.Label(new Rect(maskedPos.x + 3f, maskedPos.y - 7f, 14f, 14f), "?");

        GUI.color = ModeTagColor;
        Text.Anchor = TextAnchor.LowerRight;
        string modeTag = settings.radarOutlineStyle switch
        {
            RadarOutlineStyle.BoundaryAndGrid => "GRID",
            RadarOutlineStyle.BoundaryOnly => "PERIM",
            _ => "CLEAN",
        };
        Widgets.Label(new Rect(rect.xMax - 54f, rect.yMax - 18f, 50f, 16f), modeTag);

        Text.Anchor = prevAnchor;
        Text.Font = prevFont;
        GUI.color = prevColor;

        TooltipHandler.TipRegion(rect, ScopeLegendTooltip);
    }

    private static void DrawExteriorDimming(Rect rect, Vector2 centre, float outerR, float dimming)
    {
        Color dimCol = new(0f, 0f, 0f, Mathf.Clamp01(dimming) * 0.65f);
        const float stripHeight = 2f;
        float minX = rect.x + 1f;
        float maxX = rect.xMax - 1f;
        float outerRSq = outerR * outerR;

        for (float y = rect.y + 1f; y < rect.yMax - 1f; y += stripHeight)
        {
            float h = Mathf.Min(stripHeight, rect.yMax - 1f - y);
            float sampleY = y + (h * 0.5f);
            float dy = sampleY - centre.y;
            float dySq = dy * dy;

            if (dySq >= outerRSq)
            {
                Widgets.DrawBoxSolid(new Rect(minX, y, maxX - minX, h), dimCol);
            }
            else
            {
                float halfChord = Mathf.Sqrt(outerRSq - dySq);
                float leftEnd = centre.x - halfChord;
                float rightStart = centre.x + halfChord;

                if (leftEnd > minX)
                {
                    Widgets.DrawBoxSolid(new Rect(minX, y, leftEnd - minX, h), dimCol);
                }
                if (maxX > rightStart)
                {
                    Widgets.DrawBoxSolid(new Rect(rightStart, y, maxX - rightStart, h), dimCol);
                }
            }
        }
    }

    private static void DrawAxialHoneycomb(Vector2 centre, float maxRadius, Color color)
    {
        if (color.a <= 0.005f)
        {
            return;
        }

        const int range = 6;
        float maxRadSq = maxRadius * maxRadius;

        for (int q = -range; q <= range; q++)
        {
            for (int r = -range; r <= range; r++)
            {
                Vector2 hexCentre = RadarScopeGeometry.HexToPixel(centre, q, r);
                if ((hexCentre - centre).sqrMagnitude > maxRadSq)
                {
                    continue;
                }

                for (int e = 0; e < 6; e++)
                {
                    // Draw edges 0..2 unconditionally; draw 3..5 only when the neighbour across that edge is outside maxRadius
                    if (e >= 3)
                    {
                        (int dq, int dr) = RadarScopeGeometry.NeighbourOffsets[e];
                        Vector2 nbCentre = RadarScopeGeometry.HexToPixel(centre, q + dq, r + dr);
                        if ((nbCentre - centre).sqrMagnitude <= maxRadSq)
                        {
                            continue;
                        }
                    }

                    Vector2 c0 = RadarScopeGeometry.HexCorner(hexCentre, e);
                    Vector2 c1 = RadarScopeGeometry.HexCorner(hexCentre, e + 1);
                    Widgets.DrawLine(c0, c1, color, 1f);
                }
            }
        }
    }

    private static void DrawMountainClusterTerrain(Vector2 centre)
    {
        float size = RadarScopeGeometry.HexSize;
        for (int i = 0; i < RadarScopeGeometry.MountainCluster.Length; i++)
        {
            (int q, int r) = RadarScopeGeometry.MountainCluster[i];
            Vector2 hexCentre = RadarScopeGeometry.HexToPixel(centre, q, r, size);

            // Fill hexagon using horizontal scanlines clipped to the hex slab bounds
            for (float dy = -size + 1f; dy <= size - 1f; dy += 2f)
            {
                Vector2 a = new(hexCentre.x - size, hexCentre.y + dy);
                Vector2 b = new(hexCentre.x + size, hexCentre.y + dy);
                if (RadarScopeGeometry.TryClipSegmentToHex(hexCentre, size - 0.5f, ref a, ref b))
                {
                    Widgets.DrawLine(a, b, MountainFillColor, 2f);
                }
            }

            // Subtle outer ridge outline when contours are not overriding it
            for (int e = 0; e < 6; e++)
            {
                if (!RadarScopeGeometry.IsEdgeExposed(q, r, e))
                {
                    continue;
                }
                Vector2 c0 = RadarScopeGeometry.HexCorner(hexCentre, e, size);
                Vector2 c1 = RadarScopeGeometry.HexCorner(hexCentre, e + 1, size);
                Widgets.DrawLine(c0, c1, MountainOutlineMutedColor, 1f);
            }
        }
    }

    private static void DrawMountainClusterHatching(Vector2 centre)
    {
        float size = RadarScopeGeometry.HexSize;
        const float spacing = 4.2f;
        float span = size * 2f;

        for (int i = 0; i < RadarScopeGeometry.MountainCluster.Length; i++)
        {
            (int q, int r) = RadarScopeGeometry.MountainCluster[i];
            Vector2 hexCentre = RadarScopeGeometry.HexToPixel(centre, q, r, size);

            for (float offset = -span; offset <= span; offset += spacing)
            {
                // -45 degree diagonal scanline across the hex
                Vector2 a = new(hexCentre.x + offset - size, hexCentre.y + size);
                Vector2 b = new(hexCentre.x + offset + size, hexCentre.y - size);
                if (RadarScopeGeometry.TryClipSegmentToHex(hexCentre, size - 0.8f, ref a, ref b))
                {
                    Widgets.DrawLine(a, b, ShadowHatchColor, 1.25f);
                }
            }
        }
    }

    private static void DrawMountainClusterContours(Vector2 centre)
    {
        float size = RadarScopeGeometry.HexSize;
        for (int i = 0; i < RadarScopeGeometry.MountainCluster.Length; i++)
        {
            (int q, int r) = RadarScopeGeometry.MountainCluster[i];
            Vector2 hexCentre = RadarScopeGeometry.HexToPixel(centre, q, r, size);

            for (int e = 0; e < 6; e++)
            {
                if (!RadarScopeGeometry.IsEdgeExposed(q, r, e))
                {
                    continue;
                }

                Vector2 c0 = RadarScopeGeometry.HexCorner(hexCentre, e, size);
                Vector2 c1 = RadarScopeGeometry.HexCorner(hexCentre, e + 1, size);

                // Two crisp amber dashes along each exposed outer hex edge
                Vector2 d0Start = Vector2.Lerp(c0, c1, 0.06f);
                Vector2 d0End = Vector2.Lerp(c0, c1, 0.42f);
                Vector2 d1Start = Vector2.Lerp(c0, c1, 0.58f);
                Vector2 d1End = Vector2.Lerp(c0, c1, 0.94f);

                Widgets.DrawLine(d0Start, d0End, ShadowContourColor, 1.75f);
                Widgets.DrawLine(d1Start, d1End, ShadowContourColor, 1.75f);
            }
        }
    }

    private static void DrawCornerBrackets(Rect rect)
    {
        const float cornerLen = 8f;
        Widgets.DrawLine(new Vector2(rect.x, rect.y), new Vector2(rect.x + cornerLen, rect.y), CornerAccentColor, 1.5f);
        Widgets.DrawLine(new Vector2(rect.x, rect.y), new Vector2(rect.x, rect.y + cornerLen), CornerAccentColor, 1.5f);
        Widgets.DrawLine(new Vector2(rect.xMax, rect.y), new Vector2(rect.xMax - cornerLen, rect.y), CornerAccentColor, 1.5f);
        Widgets.DrawLine(new Vector2(rect.xMax, rect.y), new Vector2(rect.xMax, rect.y + cornerLen), CornerAccentColor, 1.5f);
        Widgets.DrawLine(new Vector2(rect.x, rect.yMax), new Vector2(rect.x + cornerLen, rect.yMax), CornerAccentColor, 1.5f);
        Widgets.DrawLine(new Vector2(rect.x, rect.yMax), new Vector2(rect.x, rect.yMax - cornerLen), CornerAccentColor, 1.5f);
        Widgets.DrawLine(new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMax - cornerLen, rect.yMax), CornerAccentColor, 1.5f);
        Widgets.DrawLine(new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMax, rect.yMax - cornerLen), CornerAccentColor, 1.5f);
    }

    private static void DrawCircleOutline(Vector2 centre, float radius, Color color, float width, int segments)
    {
        if (radius <= 0f || color.a <= 0.005f)
        {
            return;
        }

        float step = Mathf.PI * 2f / segments;
        Vector2 prev = centre + (new Vector2(Mathf.Cos(0f), Mathf.Sin(0f)) * radius);

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * step;
            Vector2 curr = centre + (new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            Widgets.DrawLine(prev, curr, color, width);
            prev = curr;
        }
    }
}
