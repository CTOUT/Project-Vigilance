using System;
using System.Collections.Generic;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Configuration properties for planetary early-warning radar installations.
/// Defines planetary hex-tile detection and interferometric identification radii, power scaling tiers,
/// and sensory penetration capabilities.
/// </summary>
public class CompProperties_Radar : CompProperties
{
    public int detectionRadius = 24;
    public int identificationRadiusDish1 = 10;
    public int identificationRadiusDish2 = 17;
    public int identificationRadiusDish3 = 22;
    public IntelligenceTier maxIntelligenceTier = IntelligenceTier.Contact;
    public float standbyPowerDraw = 350f;
    public float activePowerDraw = 750f;
    public bool receivesVantageBonus = false;
    public bool penetratesTerrainMasking = false;
    public bool isOceanicSonar = false;
    public string radarLabel = "Early-Warning Radar";

    public CompProperties_Radar()
    {
        compClass = typeof(CompRadar);
    }

    public CompProperties_Radar(Type compClass) : base(compClass)
    {
    }

    public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
    {
        foreach (string error in base.ConfigErrors(parentDef))
        {
            yield return error;
        }

        if (detectionRadius <= 0)
        {
            yield return "CompProperties_Radar: detectionRadius must be greater than 0.";
        }

        if (identificationRadiusDish1 <= 0)
        {
            yield return "CompProperties_Radar: identificationRadiusDish1 must be greater than 0.";
        }

        if (identificationRadiusDish2 <= 0)
        {
            yield return "CompProperties_Radar: identificationRadiusDish2 must be greater than 0.";
        }

        if (identificationRadiusDish3 <= 0)
        {
            yield return "CompProperties_Radar: identificationRadiusDish3 must be greater than 0.";
        }

        if (identificationRadiusDish1 > detectionRadius)
        {
            yield return "CompProperties_Radar: identificationRadiusDish1 cannot exceed detectionRadius.";
        }

        if (identificationRadiusDish2 > detectionRadius)
        {
            yield return "CompProperties_Radar: identificationRadiusDish2 cannot exceed detectionRadius.";
        }

        if (identificationRadiusDish3 > detectionRadius)
        {
            yield return "CompProperties_Radar: identificationRadiusDish3 cannot exceed detectionRadius.";
        }

        if (identificationRadiusDish1 > identificationRadiusDish2 || identificationRadiusDish2 > identificationRadiusDish3)
        {
            yield return "CompProperties_Radar: identification radii must be monotonically non-decreasing (dish1 <= dish2 <= dish3).";
        }

        if (standbyPowerDraw < 0f)
        {
            yield return "CompProperties_Radar: standbyPowerDraw cannot be negative.";
        }

        if (activePowerDraw < 0f)
        {
            yield return "CompProperties_Radar: activePowerDraw cannot be negative.";
        }

        if (activePowerDraw < standbyPowerDraw)
        {
            yield return "CompProperties_Radar: activePowerDraw must be greater than or equal to standbyPowerDraw.";
        }

        if (isOceanicSonar && receivesVantageBonus)
        {
            yield return "CompProperties_Radar: isOceanicSonar is incompatible with receivesVantageBonus.";
        }

        if (penetratesTerrainMasking && maxIntelligenceTier < IntelligenceTier.Classified)
        {
            yield return "CompProperties_Radar: penetratesTerrainMasking requires maxIntelligenceTier to be at least Classified.";
        }
    }
}
