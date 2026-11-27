using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Evaluates planetary and localized environmental disruptions on electronic radar systems,
/// Automated Listening Posts, and reconnaissance satellites.
/// Signal Beacons use primitive optical lookouts and acoustic bonfires and remain immune.
/// </summary>
public static class SensoryDisruptionUtility
{
    /// <summary>
    /// Testing hook for simulating solar flares in headless test runners.
    /// </summary>
    public static bool? SolarFlareOverride { get; set; }

    /// <summary>
    /// Testing hook for simulating eclipses or prolonged darkness in headless test runners.
    /// </summary>
    public static bool? EclipseOverride { get; set; }

    private static bool cachedConditionDefsResolved;
    private static GameConditionDef? cachedSolarFlareDef;
    private static GameConditionDef? cachedEmiDef;
    private static GameConditionDef? cachedEclipseDef;
    private static GameConditionDef? cachedVolcanicDef;
    private static GameConditionDef? cachedUnnaturalDarknessDef;
    private static GameConditionDef? cachedSunBlockerDef;

    private static void EnsureConditionDefsCached()
    {
        if (cachedConditionDefsResolved)
        {
            return;
        }

        if (DefDatabase<GameConditionDef>.DefCount == 0)
        {
            return;
        }

        cachedSolarFlareDef = DefDatabase<GameConditionDef>.GetNamedSilentFail("SolarFlare");
        cachedEmiDef = GameConditionDefOf.EMIField ?? DefDatabase<GameConditionDef>.GetNamedSilentFail("EMIField");
        cachedEclipseDef = GameConditionDefOf.Eclipse ?? DefDatabase<GameConditionDef>.GetNamedSilentFail("Eclipse");
        cachedVolcanicDef = GameConditionDefOf.VolcanicWinter ?? DefDatabase<GameConditionDef>.GetNamedSilentFail("VolcanicWinter");
        cachedUnnaturalDarknessDef = DefDatabase<GameConditionDef>.GetNamedSilentFail("UnnaturalDarkness");
        cachedSunBlockerDef = DefDatabase<GameConditionDef>.GetNamedSilentFail("SunBlocker");
        cachedConditionDefsResolved = true;
    }

    /// <summary>
    /// Checks whether an intense solar flare, EMI field, or coronal geomagnetic storm is disrupting
    /// electronic telemetry, sensors, or communication grids.
    /// </summary>
    public static bool IsSolarFlareActive(PlanetTile? tile = null)
    {
        bool? solarOverride = SolarFlareOverride;
        if (solarOverride.HasValue)
        {
            return solarOverride.Value;
        }

        try
        {
            EnsureConditionDefsCached();
            var solarFlareDef = cachedSolarFlareDef;
            var emiDef = cachedEmiDef;

            // 1. World-level solar flare / EMI condition
            if (Find.World?.gameConditionManager != null)
            {
                var wm = Find.World.gameConditionManager;
                if (solarFlareDef != null && wm.ConditionIsActive(solarFlareDef)) return true;
                if (emiDef != null && wm.ConditionIsActive(emiDef)) return true;
            }

            // 2. Map-level solar flare / EMI condition
            if (Find.Maps != null)
            {
                for (int i = 0; i < Find.Maps.Count; i++)
                {
                    var mgr = Find.Maps[i]?.gameConditionManager;
                    if (mgr != null)
                    {
                        if (solarFlareDef != null && mgr.ConditionIsActive(solarFlareDef)) return true;
                        if (emiDef != null && mgr.ConditionIsActive(emiDef)) return true;
                    }
                }
            }
        }
        catch
        {
            // Headless unit test harness
        }

        return false;
    }

    /// <summary>
    /// Checks whether a solar eclipse or prolonged darkness condition has occluded sunlight,
    /// disabling photovoltaic arrays on unmanned automated listening posts.
    /// </summary>
    public static bool IsEclipseOrProlongedDarknessActive(PlanetTile? tile = null)
    {
        bool? eclipseOverride = EclipseOverride;
        if (eclipseOverride.HasValue)
        {
            return eclipseOverride.Value;
        }

        try
        {
            EnsureConditionDefsCached();
            var eclipseDef = cachedEclipseDef;
            var volcanicDef = cachedVolcanicDef;
            var unnaturalDef = cachedUnnaturalDarknessDef;
            var sunBlockerDef = cachedSunBlockerDef;

            // 1. World-level eclipse or volcanic darkness
            if (Find.World?.gameConditionManager != null)
            {
                var wm = Find.World.gameConditionManager;
                if (eclipseDef != null && wm.ConditionIsActive(eclipseDef)) return true;
                if (volcanicDef != null && wm.ConditionIsActive(volcanicDef)) return true;
            }

            // 2. Map-level eclipse or prolonged darkness conditions
            if (Find.Maps != null)
            {
                for (int i = 0; i < Find.Maps.Count; i++)
                {
                    var mgr = Find.Maps[i]?.gameConditionManager;
                    if (mgr != null)
                    {
                        if (eclipseDef != null && mgr.ConditionIsActive(eclipseDef)) return true;
                        if (volcanicDef != null && mgr.ConditionIsActive(volcanicDef)) return true;
                        if (unnaturalDef != null && mgr.ConditionIsActive(unnaturalDef)) return true;
                        if (sunBlockerDef != null && mgr.ConditionIsActive(sunBlockerDef)) return true;
                    }
                }
            }
        }
        catch
        {
            // Headless unit test harness
        }

        return false;
    }
}
