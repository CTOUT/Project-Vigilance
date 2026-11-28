using UnityEngine;

namespace ProjectVigilance.Geoscape.Sensory
{
    /// <summary>
    /// Evaluates continuous 3D seamless deep-nebula drift and frontier edge glow across both
    /// Tier S shell vertices and Tier F shroud hex tiles, tuned for WCAG AA contrast and theme harmony.
    /// </summary>
    internal static class ShroudAtmosphere
    {
        public static Color32 EvaluateShellVertexColor(
            Vector3 unitDir,
            ShroudEffectLevel level,
            SensoryTheme theme,
            float timeSeconds,
            bool reduceMotion)
        {
            if (level == ShroudEffectLevel.Off)
            {
                return WorldDrawLayer_HazeOfWar.ShroudColor;
            }

            return EvaluateShroudSurfaceColor(unitDir, isFrontierRing: false, level, theme, timeSeconds, reduceMotion);
        }

        public static Color32 EvaluateFineTileColor(
            Vector3 unitDir,
            byte tileState,
            bool isFrontierRing,
            ShroudEffectLevel level,
            SensoryTheme theme,
            float timeSeconds,
            bool reduceMotion)
        {
            if (tileState == ShroudHierarchy.StateClear)
            {
                return WorldDrawLayer_HazeOfWar.ClearColor;
            }

            if (tileState == ShroudHierarchy.StateHaze)
            {
                return WorldDrawLayer_HazeOfWar.HazeColor;
            }

            if (level == ShroudEffectLevel.Off)
            {
                return WorldDrawLayer_HazeOfWar.ShroudColor;
            }

            return EvaluateShroudSurfaceColor(unitDir, isFrontierRing, level, theme, timeSeconds, reduceMotion);
        }

        /// <summary>
        /// Computes WCAG 2.x relative luminance in [0, 1] for sRGB byte colour values.
        /// </summary>
        public static float ComputeRelativeLuminance(Color32 c)
        {
            float r = SrgbToLinear(c.r / 255f);
            float g = SrgbToLinear(c.g / 255f);
            float b = SrgbToLinear(c.b / 255f);
            return (0.2126f * r) + (0.7152f * g) + (0.0722f * b);
        }

        /// <summary>
        /// Computes WCAG 2.x contrast ratio between two colours (1:1 to 21:1).
        /// </summary>
        public static float ComputeContrastRatio(Color32 a, Color32 b)
        {
            float la = ComputeRelativeLuminance(a);
            float lb = ComputeRelativeLuminance(b);
            float lighter = la > lb ? la : lb;
            float darker = la > lb ? lb : la;
            return (lighter + 0.05f) / (darker + 0.05f);
        }

        private static Color32 EvaluateShroudSurfaceColor(
            Vector3 unitDir,
            bool isFrontierRing,
            ShroudEffectLevel level,
            SensoryTheme theme,
            float timeSeconds,
            bool reduceMotion)
        {
            Vector3 dir = unitDir.sqrMagnitude > 1e-12f ? unitDir.normalized : Vector3.up;
            float t = reduceMotion ? 0f : timeSeconds;

            bool atmospheric = level == ShroudEffectLevel.Atmospheric;
            float driftScale = atmospheric ? 1.0f : 0.62f;

            Vector3 samplePos = new Vector3(
                (dir.x * 3.1f) + (t * 0.072f * driftScale),
                (dir.y * 3.1f) - (t * 0.046f * driftScale),
                (dir.z * 3.1f) + (t * 0.032f * driftScale));

            float nebula = SampleFbm3D(samplePos);
            float secondary = SampleFbm3D(new Vector3(
                (dir.x * 6.4f) - (t * 0.040f * driftScale),
                (dir.y * 6.4f) + (t * 0.052f * driftScale),
                (dir.z * 6.4f) + 7.3f));

            float blendedNebula = Mathf.Clamp01((nebula * 0.70f) + (secondary * 0.30f));
            // Emphasize subtle organic filaments while keeping vast stretches dark obsidian
            float wisp = Mathf.Pow(blendedNebula, 1.35f);

            GetThemePalette(theme, out Color baseObsidian, out Color nebulaTint, out Color frontierGlow);

            float nebulaStrength = atmospheric ? 1.0f : 0.55f;
            float r = Mathf.Lerp(baseObsidian.r, nebulaTint.r, wisp * nebulaStrength);
            float g = Mathf.Lerp(baseObsidian.g, nebulaTint.g, wisp * nebulaStrength);
            float b = Mathf.Lerp(baseObsidian.b, nebulaTint.b, wisp * nebulaStrength);

            float maxByte = atmospheric ? 38f : 34f;
            if (isFrontierRing)
            {
                float pulseSpeed = atmospheric ? 1.45f : 1.15f;
                float pulseAmp = atmospheric ? 0.15f : 0.08f;
                float pulse = reduceMotion ? 1.0f : ((1.0f - pulseAmp) + (pulseAmp * Mathf.Sin(t * pulseSpeed)));
                float frontierStrength = (atmospheric ? 0.88f : 0.45f) * pulse;
                r = Mathf.Lerp(r, frontierGlow.r, frontierStrength);
                g = Mathf.Lerp(g, frontierGlow.g, frontierStrength);
                b = Mathf.Lerp(b, frontierGlow.b, frontierStrength);
                maxByte = atmospheric ? 74f : 50f;
            }

            byte br = (byte)Mathf.Clamp(Mathf.RoundToInt(r * 255f), 4, (int)maxByte);
            byte bg = (byte)Mathf.Clamp(Mathf.RoundToInt(g * 255f), 4, (int)maxByte);
            byte bb = (byte)Mathf.Clamp(Mathf.RoundToInt(b * 255f), 4, (int)maxByte);
            return new Color32(br, bg, bb, 255);
        }

        /// <summary>
        /// Returns theme-matched base obsidian, vivid GPU nebula tint, and sun-lit atmospheric rim
        /// colours for the native Waylith 1.7 <c>PV_ShroudShell</c> GLSL shader.
        /// </summary>
        internal static void GetGpuThemePalette(
            SensoryTheme theme,
            out Vector4 baseObsidian,
            out Vector4 nebulaTint,
            out Vector4 rimColor)
        {
            switch (theme)
            {
                case SensoryTheme.AmberCRT:
                    baseObsidian = new Vector4(6f / 255f, 6f / 255f, 9f / 255f, 1f);
                    nebulaTint = new Vector4(56f / 255f, 38f / 255f, 18f / 255f, 1f);
                    rimColor = new Vector4(110f / 255f, 88f / 255f, 62f / 255f, 1f);
                    break;

                case SensoryTheme.EmeraldPhosphor:
                    baseObsidian = new Vector4(5f / 255f, 8f / 255f, 9f / 255f, 1f);
                    nebulaTint = new Vector4(18f / 255f, 54f / 255f, 36f / 255f, 1f);
                    rimColor = new Vector4(64f / 255f, 118f / 255f, 102f / 255f, 1f);
                    break;

                case SensoryTheme.CrimsonAlert:
                    baseObsidian = new Vector4(7f / 255f, 6f / 255f, 9f / 255f, 1f);
                    nebulaTint = new Vector4(58f / 255f, 22f / 255f, 28f / 255f, 1f);
                    rimColor = new Vector4(112f / 255f, 70f / 255f, 82f / 255f, 1f);
                    break;

                case SensoryTheme.GhostWhite:
                case SensoryTheme.HighContrast:
                    baseObsidian = new Vector4(5f / 255f, 6f / 255f, 8f / 255f, 1f);
                    nebulaTint = new Vector4(36f / 255f, 42f / 255f, 54f / 255f, 1f);
                    rimColor = new Vector4(96f / 255f, 112f / 255f, 134f / 255f, 1f);
                    break;

                case SensoryTheme.ProtanopiaDeuteranopia:
                    baseObsidian = new Vector4(5f / 255f, 7f / 255f, 12f / 255f, 1f);
                    nebulaTint = new Vector4(22f / 255f, 44f / 255f, 78f / 255f, 1f);
                    rimColor = new Vector4(76f / 255f, 114f / 255f, 154f / 255f, 1f);
                    break;

                case SensoryTheme.Tritanopia:
                    baseObsidian = new Vector4(7f / 255f, 6f / 255f, 11f / 255f, 1f);
                    nebulaTint = new Vector4(26f / 255f, 46f / 255f, 54f / 255f, 1f);
                    rimColor = new Vector4(86f / 255f, 108f / 255f, 130f / 255f, 1f);
                    break;

                case SensoryTheme.TacticalCyan:
                default:
                    baseObsidian = new Vector4(6f / 255f, 8f / 255f, 12f / 255f, 1f);
                    nebulaTint = new Vector4(24f / 255f, 46f / 255f, 68f / 255f, 1f);
                    rimColor = new Vector4(74f / 255f, 116f / 255f, 148f / 255f, 1f);
                    break;
            }
        }

        private static void GetThemePalette(
            SensoryTheme theme,
            out Color baseObsidian,
            out Color nebulaTint,
            out Color frontierGlow)
        {
            switch (theme)
            {
                case SensoryTheme.AmberCRT:
                    baseObsidian = new Color(6f / 255f, 6f / 255f, 9f / 255f);
                    nebulaTint = new Color(21f / 255f, 16f / 255f, 11f / 255f);
                    frontierGlow = new Color(46f / 255f, 34f / 255f, 15f / 255f);
                    break;

                case SensoryTheme.EmeraldPhosphor:
                    baseObsidian = new Color(5f / 255f, 8f / 255f, 9f / 255f);
                    nebulaTint = new Color(11f / 255f, 22f / 255f, 16f / 255f);
                    frontierGlow = new Color(17f / 255f, 44f / 255f, 26f / 255f);
                    break;

                case SensoryTheme.CrimsonAlert:
                    baseObsidian = new Color(7f / 255f, 6f / 255f, 9f / 255f);
                    nebulaTint = new Color(22f / 255f, 11f / 255f, 13f / 255f);
                    frontierGlow = new Color(46f / 255f, 18f / 255f, 19f / 255f);
                    break;

                case SensoryTheme.GhostWhite:
                case SensoryTheme.HighContrast:
                    baseObsidian = new Color(5f / 255f, 6f / 255f, 8f / 255f);
                    nebulaTint = new Color(17f / 255f, 19f / 255f, 24f / 255f);
                    frontierGlow = new Color(36f / 255f, 40f / 255f, 48f / 255f);
                    break;

                case SensoryTheme.ProtanopiaDeuteranopia:
                    baseObsidian = new Color(5f / 255f, 7f / 255f, 12f / 255f);
                    nebulaTint = new Color(12f / 255f, 19f / 255f, 32f / 255f);
                    frontierGlow = new Color(20f / 255f, 38f / 255f, 58f / 255f);
                    break;

                case SensoryTheme.Tritanopia:
                    baseObsidian = new Color(7f / 255f, 6f / 255f, 11f / 255f);
                    nebulaTint = new Color(12f / 255f, 20f / 255f, 23f / 255f);
                    frontierGlow = new Color(34f / 255f, 22f / 255f, 42f / 255f);
                    break;

                case SensoryTheme.TacticalCyan:
                default:
                    baseObsidian = new Color(6f / 255f, 8f / 255f, 12f / 255f);
                    nebulaTint = new Color(13f / 255f, 21f / 255f, 30f / 255f);
                    frontierGlow = new Color(18f / 255f, 40f / 255f, 52f / 255f);
                    break;
            }
        }

        private static float SampleFbm3D(Vector3 p)
        {
            float n1 = SampleValueNoise3D(p);
            float n2 = SampleValueNoise3D((p * 2.07f) + new Vector3(13.7f, -9.2f, 5.1f));
            return (n1 * 0.667f) + (n2 * 0.333f);
        }

        private static float SampleValueNoise3D(Vector3 p)
        {
            int xi = Mathf.FloorToInt(p.x);
            int yi = Mathf.FloorToInt(p.y);
            int zi = Mathf.FloorToInt(p.z);

            float fx = p.x - xi;
            float fy = p.y - yi;
            float fz = p.z - zi;

            float ux = QuinticFade(fx);
            float uy = QuinticFade(fy);
            float uz = QuinticFade(fz);

            float c000 = HashToFloat(xi, yi, zi);
            float c100 = HashToFloat(xi + 1, yi, zi);
            float c010 = HashToFloat(xi, yi + 1, zi);
            float c110 = HashToFloat(xi + 1, yi + 1, zi);
            float c001 = HashToFloat(xi, yi, zi + 1);
            float c101 = HashToFloat(xi + 1, yi, zi + 1);
            float c011 = HashToFloat(xi, yi + 1, zi + 1);
            float c111 = HashToFloat(xi + 1, yi + 1, zi + 1);

            float x00 = Mathf.Lerp(c000, c100, ux);
            float x10 = Mathf.Lerp(c010, c110, ux);
            float x01 = Mathf.Lerp(c001, c101, ux);
            float x11 = Mathf.Lerp(c011, c111, ux);

            float y0 = Mathf.Lerp(x00, x10, uy);
            float y1 = Mathf.Lerp(x01, x11, uy);
            return Mathf.Lerp(y0, y1, uz);
        }

        private static float QuinticFade(float t)
        {
            return t * t * t * ((t * ((t * 6f) - 15f)) + 10f);
        }

        private static float HashToFloat(int x, int y, int z)
        {
            unchecked
            {
                uint h = ((uint)x * 374761393u)
                       + ((uint)y * 668265263u)
                       + ((uint)z * 2147483647u);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0x00FFFFFFu) / 16777215f;
            }
        }

        private static float SrgbToLinear(float c)
        {
            return c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
        }
    }
}
