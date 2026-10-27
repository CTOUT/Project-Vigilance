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
    [TestCleanup]
    public void Cleanup()
    {
        HazeReconnaissanceManager.Instance = null;
    }

    [TestMethod]
    public void CompassDirection_EightCardinals_CalculatedAccuratelyViaBreadcrumbRumourUtility()
    {
        Vector2 origin = new Vector2(0f, 0f); // (lon, lat)

        // North: +lat, 0 lon
        Assert.AreEqual("north", BreadcrumbRumourUtility.GetCompassDirection(origin, new Vector2(0f, 10f)));

        // Northeast: +lat, +lon
        Assert.AreEqual("northeast", BreadcrumbRumourUtility.GetCompassDirection(origin, new Vector2(10f, 10f)));

        // East: 0 lat, +lon
        Assert.AreEqual("east", BreadcrumbRumourUtility.GetCompassDirection(origin, new Vector2(10f, 0f)));

        // Southeast: -lat, +lon
        Assert.AreEqual("southeast", BreadcrumbRumourUtility.GetCompassDirection(origin, new Vector2(10f, -10f)));

        // South: -lat, 0 lon
        Assert.AreEqual("south", BreadcrumbRumourUtility.GetCompassDirection(origin, new Vector2(0f, -10f)));

        // Southwest: -lat, -lon
        Assert.AreEqual("southwest", BreadcrumbRumourUtility.GetCompassDirection(origin, new Vector2(-10f, -10f)));

        // West: 0 lat, -lon
        Assert.AreEqual("west", BreadcrumbRumourUtility.GetCompassDirection(origin, new Vector2(-10f, 0f)));

        // Northwest: +lat, -lon
        Assert.AreEqual("northwest", BreadcrumbRumourUtility.GetCompassDirection(origin, new Vector2(-10f, 10f)));
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
    public void IsDynamicSite_ListeningPostAndIncursion_ReturnsFalse()
    {
        var post = new WorldObject_ListeningPost();
        var inc = new ProjectVigilance.Geoscape.World.WorldObject_Incursion();

        Assert.IsFalse(HazeReconnaissanceManager.IsDynamicSite(post), "Listening posts are infrastructure, not dynamic camps.");
        Assert.IsFalse(HazeReconnaissanceManager.IsDynamicSite(inc), "Incursions are mobile forces, not dynamic sites.");
    }

    [TestMethod]
    public void TravellerRumour_NarrativeFormatting_MatchesPattern()
    {
        string factionName = "Rough Outlanders";
        string siteType = "bandit camp";
        int hexDist = 7;
        string direction = "northwest";

        TaggedString actualText = BreadcrumbRumourUtility.FormatRumourBody(factionName, siteType, hexDist, direction);

        string expectedSnippet = "Travellers from the Rough Outlanders report sighting an active bandit camp 7 hexes to the northwest.";
        Assert.IsTrue(actualText.ToString().Contains(expectedSnippet),
            $"Narrative must contain traveller intelligence format. Actual: {actualText}");
    }

    [TestMethod]
    public void HarmonyPatches_Phase3_CanBeAppliedCleanlyWithoutException()
    {
        var harmony = new HarmonyLib.Harmony("test.phase3.harmony.validation");
        var patch1 = typeof(ProjectVigilance.Geoscape.Patches.LetterStack_ReceiveLetter_Patch);
        var patch2 = typeof(ProjectVigilance.Geoscape.Patches.TimeoutComp_CompTick_Patch);
        var patch3 = typeof(ProjectVigilance.Geoscape.Patches.WorldObject_SpawnSetup_ConcealmentPatch);

        harmony.CreateClassProcessor(patch1).Patch();
        harmony.CreateClassProcessor(patch2).Patch();
        harmony.CreateClassProcessor(patch3).Patch();
    }

    [TestMethod]
    public void TimeoutComp_CompTickInterval_SuspendedWhenConcealedAndResumedWhenUncovered()
    {
        var harmony = new HarmonyLib.Harmony("test.timeout.patch.execution");
        var patch = typeof(ProjectVigilance.Geoscape.Patches.TimeoutComp_CompTick_Patch);
        harmony.CreateClassProcessor(patch).Patch();

        var manager = new HazeReconnaissanceManager(null!);
        HazeReconnaissanceManager.Instance = manager;

        var site = new Site();
        site.ID = 14502;
        var comp = new TimeoutComp();
        comp.parent = site;

        // Stage 1: Site is concealed in Haze -> Prefix returns false (countdown paused)
        manager.RegisterConcealedSite(site);
        Assert.IsTrue(manager.IsSiteConcealed(site));

        bool canTickConcealed = ProjectVigilance.Geoscape.Patches.TimeoutComp_CompTick_Patch.Prefix(comp);
        Assert.IsFalse(canTickConcealed, "Timeout countdown must be suspended (Prefix returns false) while concealed in Haze.");

        // Stage 2: Site is uncovered -> Prefix returns true (countdown proceeds)
        manager.UncoverSite(site, suppressDiscoveryLetter: true);
        Assert.IsFalse(manager.IsSiteConcealed(site));

        bool canTickUncovered = ProjectVigilance.Geoscape.Patches.TimeoutComp_CompTick_Patch.Prefix(comp);
        Assert.IsTrue(canTickUncovered, "Timeout countdown must resume (Prefix returns true) once uncovered.");
    }

    [TestMethod]
    public void HazeReconnaissanceManager_UncoverSite_RespectsSuppressDiscoveryLetter()
    {
        var manager = new HazeReconnaissanceManager(null!);
        var site = new Site();
        site.ID = 7711;

        manager.RegisterConcealedSite(site);
        Assert.IsTrue(manager.IsSiteConcealed(site));

        // When uncovered via rumours (suppressDiscoveryLetter: true), generic letter is suppressed
        bool uncovered = manager.UncoverSite(site, suppressDiscoveryLetter: true);
        Assert.IsTrue(uncovered);
        Assert.IsFalse(manager.IsSiteConcealed(site));
    }
}
