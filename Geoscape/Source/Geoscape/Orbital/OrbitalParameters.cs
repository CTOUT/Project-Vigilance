using System;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Orbital;

/// <summary>
/// Encapsulates the Keplerian orbital parameters governing the drifting trajectory of an orbital entity.
/// All calculations are algebraic and closed-form, providing O(1) evaluation without physics integration.
/// </summary>
public class OrbitalParameters : IExposable
{
    private float altitudeKm = 500f;
    private int periodTicks = 3450;
    private float inclinationDeg = 45f;
    private float ascendingNodeDeg = 0f;
    private float precessionRateDegPerDay = -3.2f;
    private float meanAnomalyEpochDeg = 0f;
    private float orbitalDriftDegPerDay = 0.5f;
    private int epochTick = 0;

    /// <summary>
    /// Orbital altitude above the planetary surface in kilometres.
    /// </summary>
    public float AltitudeKm
    {
        get => altitudeKm;
        set => altitudeKm = value;
    }

    /// <summary>
    /// Orbital period in game ticks (60,000 ticks = 1 in-game day).
    /// </summary>
    public int PeriodTicks
    {
        get => periodTicks;
        set => periodTicks = Mathf.Max(100, value);
    }

    /// <summary>
    /// Orbital inclination in degrees relative to the planetary equatorial plane (0 to 180 degrees).
    /// </summary>
    public float InclinationDeg
    {
        get => inclinationDeg;
        set => inclinationDeg = Mathf.Clamp(value, 0f, 180f);
    }

    /// <summary>
    /// Right Ascension of the Ascending Node (RAAN) at epoch in degrees (0 to 360 degrees).
    /// </summary>
    public float AscendingNodeDeg
    {
        get => ascendingNodeDeg;
        set => ascendingNodeDeg = value % 360f;
    }

    /// <summary>
    /// Rate at which the orbital plane precesses around the polar axis in degrees per in-game day (60,000 ticks)
    /// due to planetary oblateness (J2 perturbation).
    /// </summary>
    public float PrecessionRateDegPerDay
    {
        get => precessionRateDegPerDay;
        set => precessionRateDegPerDay = value;
    }

    /// <summary>
    /// Mean anomaly at epoch in degrees (0 to 360 degrees).
    /// </summary>
    public float MeanAnomalyEpochDeg
    {
        get => meanAnomalyEpochDeg;
        set => meanAnomalyEpochDeg = value % 360f;
    }

    /// <summary>
    /// Minor secondary orbital drift rate in degrees per day, ensuring ground tracks never lock into an exact repeating pattern.
    /// </summary>
    public float OrbitalDriftDegPerDay
    {
        get => orbitalDriftDegPerDay;
        set => orbitalDriftDegPerDay = value;
    }

    /// <summary>
    /// The reference game tick at which MeanAnomalyEpochDeg and AscendingNodeDeg were defined.
    /// </summary>
    public int EpochTick
    {
        get => epochTick;
        set => epochTick = value;
    }

    public OrbitalParameters()
    {
    }

    public OrbitalParameters(
        float altitudeKm,
        int periodTicks,
        float inclinationDeg,
        float ascendingNodeDeg,
        float precessionRateDegPerDay,
        float meanAnomalyEpochDeg,
        float orbitalDriftDegPerDay,
        int epochTick)
    {
        this.altitudeKm = altitudeKm;
        this.periodTicks = Mathf.Max(100, periodTicks);
        this.inclinationDeg = Mathf.Clamp(inclinationDeg, 0f, 180f);
        this.ascendingNodeDeg = ascendingNodeDeg % 360f;
        this.precessionRateDegPerDay = precessionRateDegPerDay;
        this.meanAnomalyEpochDeg = meanAnomalyEpochDeg % 360f;
        this.orbitalDriftDegPerDay = orbitalDriftDegPerDay;
        this.epochTick = epochTick;
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref altitudeKm, "altitudeKm", 500f);
        Scribe_Values.Look(ref periodTicks, "periodTicks", 3450);
        Scribe_Values.Look(ref inclinationDeg, "inclinationDeg", 45f);
        Scribe_Values.Look(ref ascendingNodeDeg, "ascendingNodeDeg", 0f);
        Scribe_Values.Look(ref precessionRateDegPerDay, "precessionRateDegPerDay", -3.2f);
        Scribe_Values.Look(ref meanAnomalyEpochDeg, "meanAnomalyEpochDeg", 0f);
        Scribe_Values.Look(ref orbitalDriftDegPerDay, "orbitalDriftDegPerDay", 0.5f);
        Scribe_Values.Look(ref epochTick, "epochTick", 0);
    }
}
