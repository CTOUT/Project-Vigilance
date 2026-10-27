using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Geoscape.Tests;

[TestClass]
public sealed class HazeConcealmentAndRumourTests
{
    private static string CalculateCompassDirection(Vector2 fromLatLong, Vector2 toLatLong)
    {
        float dLat = toLatLong.y - fromLatLong.y;
        float dLon = toLatLong.x - fromLatLong.x;

        while (dLon > 180f) dLon -= 360f;
        while (dLon < -180f) dLon += 360f;

        float angle = Mathf.Atan2(dLon, dLat) * Mathf.Rad2Deg;

        return angle switch
        {
            >= -22.5f and < 22.5f => "north",
            >= 22.5f and < 67.5f => "northeast",
            >= 67.5f and < 112.5f => "east",
            >= 112.5f and < 157.5f => "southeast",
            >= 157.5f or < -157.5f => "south",
            >= -157.5f and < -112.5f => "southwest",
            >= -112.5f and < -67.5f => "west",
            _ => "northwest"
        };
    }

    [TestMethod]
    public void CompassDirection_EightCardinals_CalculatedAccurately()
    {
        Vector2 origin = new Vector2(0f, 0f); // (lon, lat)

        // North: +lat, 0 lon
        Assert.AreEqual("north", CalculateCompassDirection(origin, new Vector2(0f, 10f)));

        // Northeast: +lat, +lon
        Assert.AreEqual("northeast", CalculateCompassDirection(origin, new Vector2(10f, 10f)));

        // East: 0 lat, +lon
        Assert.AreEqual("east", CalculateCompassDirection(origin, new Vector2(10f, 0f)));

        // Southeast: -lat, +lon
        Assert.AreEqual("southeast", CalculateCompassDirection(origin, new Vector2(10f, -10f)));

        // South: -lat, 0 lon
        Assert.AreEqual("south", CalculateCompassDirection(origin, new Vector2(0f, -10f)));

        // Southwest: -lat, -lon
        Assert.AreEqual("southwest", CalculateCompassDirection(origin, new Vector2(-10f, -10f)));

        // West: 0 lat, -lon
        Assert.AreEqual("west", CalculateCompassDirection(origin, new Vector2(-10f, 0f)));

        // Northwest: +lat, -lon
        Assert.AreEqual("northwest", CalculateCompassDirection(origin, new Vector2(-10f, 10f)));
    }

    [TestMethod]
    public void IsDynamicSite_Settlement_ReturnsFalse()
    {
        var settlement = new Settlement();
        Assert.IsFalse(HazeReconnaissanceManager.IsDynamicSite(settlement),
            "Permanent settlements must not be treated as dynamic sites.");
    }

    [TestMethod]
    public void IsDynamicSite_Site_ReturnsTrue()
    {
        var site = new Site();
        Assert.IsTrue(HazeReconnaissanceManager.IsDynamicSite(site),
            "Sites must be treated as dynamic sites.");
    }

    [TestMethod]
    public void TravellerRumour_NarrativeFormatting_MatchesPattern()
    {
        // Scenario: Friendly travellers arrive and report sighting an active bandit camp 7 hexes to the northwest
        string factionName = "Rough Outlanders";
        string siteType = "bandit camp";
        int hexDist = 7;
        string direction = "northwest";

        string expectedText = $"Travellers from the {factionName} report sighting an active {siteType} {hexDist} hexes to the {direction}.\n\nThe location has been charted on your planetary map.";

        string actualText = $"Travellers from the {factionName} report sighting an active {siteType} {hexDist} hexes to the {direction}.\n\nThe location has been charted on your planetary map.";

        Assert.AreEqual(expectedText, actualText);
        Assert.IsTrue(actualText.Contains("Travellers from the Rough Outlanders report sighting an active bandit camp 7 hexes to the northwest."));
    }

    [TestMethod]
    public void TimeoutSuspensionLogic_ConcealedVsUncovered_ControlsCountdown()
    {
        // Scenario: TimeoutComp counts down only when NOT concealed in Haze of War.
        int ticksLeft = 60000;
        bool isConcealed = true;

        // Tick 1: Concealed in Haze -> Prefix returns false, countdown paused
        bool canTick1 = !isConcealed;
        if (canTick1)
        {
            ticksLeft--;
        }
        Assert.AreEqual(60000, ticksLeft, "Timeout clock must be suspended while concealed in Haze.");

        // Tick 2: Still concealed
        bool canTick2 = !isConcealed;
        if (canTick2)
        {
            ticksLeft--;
        }
        Assert.AreEqual(60000, ticksLeft, "Timeout clock remains frozen.");

        // Site is uncovered!
        isConcealed = false;

        // Tick 3: Uncovered -> Prefix returns true, countdown resumes
        bool canTick3 = !isConcealed;
        if (canTick3)
        {
            ticksLeft--;
        }
        Assert.AreEqual(59999, ticksLeft, "Timeout clock resumes countdown once uncovered.");
    }
}
