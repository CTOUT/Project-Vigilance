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

    /// <summary>
    /// Checks whether an intense solar flare, EMI field, or coronal geomagnetic storm is disrupting
    /// electronic telemetry, sensors, or communication grids.
    /// </summary>
    public static bool IsSolarFlareActive(PlanetTile? tile = null)
    {
        if (SolarFlareOverride.HasValue)
        {
            return SolarFlareOverride.Value;
        }

        try
        {
            var solarFlareDef = DefDatabase<GameConditionDef>.GetNamedSilentFail("SolarFlare");
            var emiDef = GameConditionDefOf.EMIField ?? DefDatabase<GameConditionDef>.GetNamedSilentFail("EMIField");

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
        if (EclipseOverride.HasValue)
        {
            return EclipseOverride.Value;
        }

        try
        {
            var eclipseDef = GameConditionDefOf.Eclipse ?? DefDatabase<GameConditionDef>.GetNamedSilentFail("Eclipse");
            var volcanicDef = GameConditionDefOf.VolcanicWinter ?? DefDatabase<GameConditionDef>.GetNamedSilentFail("VolcanicWinter");

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
                        if (IsConditionActiveNamed(mgr, "UnnaturalDarkness") ||
                            IsConditionActiveNamed(mgr, "SunBlocker"))
                        {
                            return true;
                        }
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

    private static bool IsConditionActiveNamed(GameConditionManager mgr, string defName)
    {
        try
        {
            var def = DefDatabase<GameConditionDef>.GetNamedSilentFail(defName);
            return def != null && mgr.ConditionIsActive(def);
        }
        catch
        {
            return false;
        }
    }
}
