using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Utility providing sensory classification, distinctive cyan visual telemetry, and procedural
/// discovery markers for anomalous, man-made, and otherworldly landmarks on the Geoscape.
/// Distinguishes high-interest points of discovery (cyan exclamation mark) from tactical threats (amber question mark).
/// </summary>
public static class LandmarkSensoryUtility
{
    /// <summary>
    /// Tactical cyan discovery colour for anomalous POIs, monoliths, and ancient structures.
    /// Distinct from the amber surveillance colour used for unknown tactical sites and incursions.
    /// </summary>
    public static readonly Color TacticalCyan = new(0.35f, 0.85f, 1.0f, 0.95f);

    private static Texture2D? cachedExclamationTex;
    private static Material? cachedLandmarkWorldMat;

    private static readonly HashSet<string> ManMadeLandmarkDefNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Odyssey DLC structure landmarks
        "Ruins",
        "FrozenRuins",
        "AncientLaunchSite",
        "AncientGarrison",
        "AncientWarehouse",
        "AncientChemfuelRefinery",
        "AncientInfestedSettlement",
        "AbandonedColonyTribal",
        "AbandonedColonyOutlander",
        "AbandonedColonyLandmarkBase",
        "AncientSmokeVent",
        "AncientToxVent",
        "AncientHeatVent",
        "AncientQuarry",
        "Harbor",
        "TerraformingScar"
    };

    /// <summary>
    /// Evaluates whether a WorldObject represents a man-made, anomalous, or otherworldly structure
    /// that should be demarcated with the cyan discovery marker rather than the threat question mark.
    /// </summary>
    public static bool IsAnomalousStructure(WorldObject? wo)
    {
        if (wo == null)
        {
            return false;
        }

        if (wo is EscapeShip)
        {
            return true;
        }

        string? defName = wo.def?.defName;
        if (!string.IsNullOrEmpty(defName))
        {
            if (defName.Equals("FallenMonolith", StringComparison.OrdinalIgnoreCase) ||
                defName.Equals("AncientComplex", StringComparison.OrdinalIgnoreCase) ||
                defName.Equals("AncientRuins", StringComparison.OrdinalIgnoreCase) ||
                defName.Equals("AncientShrine", StringComparison.OrdinalIgnoreCase) ||
                defName.Equals("HermeticCrate", StringComparison.OrdinalIgnoreCase) ||
                defName.Equals("Archonexus", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (defName.IndexOf("Monolith", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defName.IndexOf("Ancient", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defName.IndexOf("Complex", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defName.IndexOf("Shrine", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defName.IndexOf("Archotech", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        if (wo is Site site && site.parts != null)
        {
            for (int i = 0; i < site.parts.Count; i++)
            {
                string? partDefName = site.parts[i].def?.defName;
                if (!string.IsNullOrEmpty(partDefName))
                {
                    if (partDefName.IndexOf("Ancient", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        partDefName.IndexOf("Complex", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        partDefName.IndexOf("Monolith", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        partDefName.IndexOf("Shrine", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        partDefName.IndexOf("Ruins", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Evaluates whether a landmark defName corresponds to a man-made, anomalous, or architectural structure,
    /// excluding natural or mundane geographical features (such as lakes, ponds, cliffs, dunes, and valleys).
    /// </summary>
    public static bool IsManMadeLandmarkDef(string? defName)
    {
        if (string.IsNullOrEmpty(defName))
        {
            return false;
        }

        if (ManMadeLandmarkDefNames.Contains(defName))
        {
            return true;
        }

        if (defName.StartsWith("Ancient", StringComparison.OrdinalIgnoreCase) ||
            defName.StartsWith("Abandoned", StringComparison.OrdinalIgnoreCase) ||
            defName.StartsWith("Ruins", StringComparison.OrdinalIgnoreCase) ||
            defName.StartsWith("FrozenRuins", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Retrieves or dynamically generates a procedural 64x64 anti-aliased exclamation mark texture
    /// for rendering discovery blips on the Geoscape.
    /// </summary>
    public static Texture2D GetUnknownLandmarkExpandingTexture()
    {
        if (cachedExclamationTex != null)
        {
            return cachedExclamationTex;
        }

        try
        {
            cachedExclamationTex = ContentFinder<Texture2D>.Get("UI/Overlays/ExclamationMark", false);
        }
        catch
        {
            cachedExclamationTex = null;
        }

        cachedExclamationTex ??= CreateProceduralExclamationMarkTexture();
        return cachedExclamationTex;
    }

    /// <summary>
    /// Retrieves or initialises the world-space quad material for upright anomalous landmark discovery blips.
    /// </summary>
    public static Material GetUnknownLandmarkWorldMaterial()
    {
        if (cachedLandmarkWorldMat != null)
        {
            return cachedLandmarkWorldMat;
        }

        try
        {
            Texture2D tex = GetUnknownLandmarkExpandingTexture();
            cachedLandmarkWorldMat = MaterialPool.MatFrom(
                tex,
                ShaderDatabase.WorldOverlayTransparentLit,
                TacticalCyan,
                WorldMaterials.WorldObjectRenderQueue);
        }
        catch
        {
            cachedLandmarkWorldMat = BaseContent.BadMat;
        }

        return cachedLandmarkWorldMat ?? BaseContent.BadMat;
    }

    /// <summary>
    /// Procedurally rasterises a clean, anti-aliased white exclamation mark on a transparent background,
    /// suitable for tinting with <see cref="TacticalCyan"/>.
    /// </summary>
    public static Texture2D CreateProceduralExclamationMarkTexture()
    {
        const int width = 64;
        const int height = 64;
        Texture2D tex = new(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }

        // 1. Draw vertical tapering bar: y in [22, 56], centered at x = 31.5
        for (int y = 22; y <= 56; y++)
        {
            float progress = (y - 22f) / (56f - 22f); // 0.0 at bottom, 1.0 at top
            float halfWidth = Mathf.Lerp(2.4f, 4.2f, progress);
            int minX = Mathf.RoundToInt(31.5f - halfWidth - 1f);
            int maxX = Mathf.RoundToInt(31.5f + halfWidth + 1f);

            for (int x = minX; x <= maxX; x++)
            {
                float dx = Mathf.Abs(x - 31.5f);
                if (dx <= halfWidth)
                {
                    pixels[y * width + x] = Color.white;
                }
                else if (dx <= halfWidth + 1f)
                {
                    float alpha = 1f - (dx - halfWidth);
                    pixels[y * width + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
                }
            }
        }

        // 2. Draw circular bottom dot: centered at (31.5, 11) with radius 3.8
        for (int y = 7; y <= 15; y++)
        {
            for (int x = 27; x <= 36; x++)
            {
                float dx = x - 31.5f;
                float dy = y - 11f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist <= 3.8f)
                {
                    pixels[y * width + x] = Color.white;
                }
                else if (dist <= 4.8f)
                {
                    float alpha = 1f - (dist - 3.8f);
                    pixels[y * width + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
