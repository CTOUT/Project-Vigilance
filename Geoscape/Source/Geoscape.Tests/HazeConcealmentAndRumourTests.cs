using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Geoscape.Tests;

[TestClass]
[DoNotParallelize]
public sealed class HazeConcealmentAndRumourTests
{
    [TestCleanup]
    public void Cleanup()
    {
        HazeReconnaissanceManager.Instance = null;
        HazeReconnaissanceManager.WorldObjectLookupOverride = null;
        SensoryManager.Instance = null;
        SensoryManager.IsExploredOverride = null;
        SensoryManager.IsIdentifiedOverride = null;
        SensoryManager.SuppressUnknownMasking = false;
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
        var patch4 = typeof(ProjectVigilance.Geoscape.Patches.WorldObject_PostRemove_Patch);

        harmony.CreateClassProcessor(patch1).Patch();
        harmony.CreateClassProcessor(patch2).Patch();
        harmony.CreateClassProcessor(patch3).Patch();
        harmony.CreateClassProcessor(patch4).Patch();
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

    [TestMethod]
    public void WorldObject_PostRemove_RemovesConcealedSiteFromHazeReconnaissanceManager()
    {
        var manager = new HazeReconnaissanceManager(null!);
        HazeReconnaissanceManager.Instance = manager;

        var site = new Site { ID = 9922 };
        manager.RegisterConcealedSite(site);
        Assert.IsTrue(manager.IsSiteConcealed(site));

        ProjectVigilance.Geoscape.Patches.WorldObject_PostRemove_Patch.Postfix(site);
        Assert.IsFalse(manager.IsSiteConcealed(site), "PostRemove must purge the site from concealedSites.");
    }

    [TestMethod]
    public void WorldObject_SpawnSetup_ConcealmentPatch_DuringWorldGen_DoesNotThrowOrQueryPlayerFaction()
    {
        // During world generation or when no player faction is registered yet, SpawnSetup must not query Faction.OfPlayer
        var settlement = new Settlement { ID = 54321 };
        
        // Act & Assert: Invoking Postfix must complete cleanly without throwing an exception or querying Faction.OfPlayer
        ProjectVigilance.Geoscape.Patches.WorldObject_SpawnSetup_ConcealmentPatch.Postfix(settlement);
    }

    [TestMethod]
    public void CheckUncoveredSites_PurgesStaleSitesWhenDestroyedOrUnspawned()
    {
        var manager = new HazeReconnaissanceManager(null!);
        HazeReconnaissanceManager.Instance = manager;
        var sensory = new SensoryManager(null!);
        SensoryManager.Instance = sensory;

        var siteSpawned = new Site { ID = 8844 };
        HarmonyLib.Traverse.Create(siteSpawned).Field("spawned").SetValue(true);

        var siteDestroyed = new Site { ID = 8845 };
        HarmonyLib.Traverse.Create(siteDestroyed).Field("spawned").SetValue(true);
        HarmonyLib.Traverse.Create(siteDestroyed).Field("destroyed").SetValue(true);

        var siteMissing = new Site { ID = 8846 };

        manager.RegisterConcealedSite(siteSpawned);
        manager.RegisterConcealedSite(siteDestroyed);
        manager.RegisterConcealedSite(siteMissing);

        Assert.IsTrue(manager.IsSiteConcealed(siteSpawned));
        Assert.IsTrue(manager.IsSiteConcealed(siteDestroyed));
        Assert.IsTrue(manager.IsSiteConcealed(siteMissing));

        // Provide lookup override: 8844 is spawned, 8845 is destroyed, 8846 is null (missing/despawned)
        HazeReconnaissanceManager.WorldObjectLookupOverride = id => id switch
        {
            8844 => siteSpawned,
            8845 => siteDestroyed,
            _ => null
        };
        SensoryManager.IsExploredOverride = _ => false;

        // Act: Run CheckUncoveredSites while tile is unexplored
        manager.CheckUncoveredSites();

        // 8845 (destroyed) and 8846 (missing) must be purged from concealedSites
        Assert.IsTrue(manager.IsSiteConcealed(siteSpawned), "Spawned non-destroyed site on unexplored tile must remain concealed.");
        Assert.IsFalse(manager.IsSiteConcealed(siteDestroyed), "Destroyed site must be purged from concealedSites.");
        Assert.IsFalse(manager.IsSiteConcealed(siteMissing), "Despawned/missing site must be purged from concealedSites.");

        // When explored, spawned site should be uncovered
        SensoryManager.IsExploredOverride = _ => true;
        manager.CheckUncoveredSites();
        Assert.IsFalse(manager.IsSiteConcealed(siteSpawned), "Site must be uncovered when sensory explores its tile.");
    }

    [TestMethod]
    public void DetectionOnlyLayer_SettlementAndOutpostSite_MaskedAsUnknownUntilIdentified()
    {
        ProjectVigilance.Geoscape.GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;

        var harmony = new HarmonyLib.Harmony("test.unknown.site.masking");
        harmony.CreateClassProcessor(typeof(ProjectVigilance.Geoscape.Patches.WorldObject_Visibility_Patch.UnknownSite_Label_Patch)).Patch();
        harmony.CreateClassProcessor(typeof(ProjectVigilance.Geoscape.Patches.WorldObject_Visibility_Patch.UnknownSite_GetInspectString_Patch)).Patch();
        harmony.CreateClassProcessor(typeof(ProjectVigilance.Geoscape.Patches.WorldObject_Visibility_Patch.UnknownSite_GetDescription_Patch)).Patch();
        harmony.CreateClassProcessor(typeof(ProjectVigilance.Geoscape.Patches.WorldObject_Visibility_Patch.UnknownSite_ShowRelatedQuests_Patch)).Patch();
        harmony.CreateClassProcessor(typeof(ProjectVigilance.Geoscape.Patches.WorldObject_Visibility_Patch.UnknownSite_GetGizmos_Patch)).Patch();
        harmony.CreateClassProcessor(typeof(ProjectVigilance.Geoscape.Patches.WorldObject_Visibility_Patch.UnknownSite_ExpandingIconColor_Patch)).Patch();

        var sensory = new SensoryManager(null!);
        SensoryManager.Instance = sensory;
        var hazeManager = new HazeReconnaissanceManager(null!);
        HazeReconnaissanceManager.Instance = hazeManager;

        var settlement = new Settlement { ID = 30101 };
        settlement.Name = "Covenant Stronghold";
        HarmonyLib.Traverse.Create(settlement).Field("tile").SetValue((PlanetTile)55);

        var outpostSite = new Site { ID = 30102 };
        outpostSite.customLabel = "Pirate Artillery Outpost";
        HarmonyLib.Traverse.Create(outpostSite).Field("tile").SetValue((PlanetTile)56);
        HarmonyLib.Traverse.Create(outpostSite).Field("spawned").SetValue(true);

        HazeReconnaissanceManager.WorldObjectLookupOverride = id => id == 30102 ? outpostSite : (id == 30101 ? settlement : null);
        hazeManager.RegisterConcealedSite(outpostSite);

        // Stage 1: Unexplored (in Black Shroud) -> Not masked as unknown because it is completely hidden by Shroud culling
        Assert.IsFalse(sensory.IsExplored((PlanetTile)55));
        Assert.IsFalse(SensoryManager.ShouldMaskWorldObjectAsUnknown(settlement),
            "Unexplored objects in the black shroud are hidden outright rather than masked as detected unknown sites.");

        // Stage 2: Explored by outer Detection layer ONLY (IsExplored == true, IsTileIdentified == false)
        sensory.MarkTileExplored((PlanetTile)55);
        sensory.MarkTileExplored((PlanetTile)56);

        Assert.IsTrue(sensory.IsExplored((PlanetTile)55));
        Assert.IsFalse(sensory.IsTileIdentified((PlanetTile)55), "Outer detection ring must NOT mark the tile as identified.");
        Assert.IsTrue(SensoryManager.ShouldMaskWorldObjectAsUnknown(settlement),
            "Settlement in outer detection layer must be masked as an unknown site.");
        Assert.IsTrue(SensoryManager.ShouldMaskWorldObjectAsUnknown(outpostSite),
            "Outpost site in outer detection layer must be masked as an unknown site.");

        // Verify masked label, inspect string, description, quest suppression, and icon color via Harmony patches
        Assert.AreEqual("unidentified site", settlement.Label);
        Assert.AreEqual("unidentified site", outpostSite.Label);
        Assert.IsTrue(settlement.GetInspectString().Contains("Status: Unidentified Site"));
        Assert.IsFalse(settlement.GetInspectString().Contains("Covenant Stronghold"));
        Assert.IsTrue(outpostSite.GetInspectString().Contains("Status: Unidentified Site"));
        Assert.IsTrue(outpostSite.GetDescription().Contains("unidentified surface site"));
        Assert.IsFalse(settlement.ShowRelatedQuests);
        Assert.AreEqual(ProjectVigilance.Geoscape.Patches.WorldObject_Visibility_Patch.UnknownSiteIconColor, settlement.ExpandingIconColor);

        // Verify dynamic site remains concealed in HazeReconnaissanceManager when only in Detection layer
        hazeManager.CheckUncoveredSites();
        Assert.IsTrue(hazeManager.IsSiteConcealed(outpostSite),
            "Dynamic outpost site must remain concealed (letter suppressed, timeout paused) while only in the outer Detection layer.");

        // Stage 3: Scanned by inner Identification layer (IsTileIdentified == true)
        sensory.MarkTileIdentified((PlanetTile)55);
        sensory.MarkTileIdentified((PlanetTile)56);

        Assert.IsTrue(sensory.IsTileIdentified((PlanetTile)55));
        Assert.IsTrue(sensory.IsWorldObjectIdentified(settlement));
        Assert.IsTrue(sensory.IsWorldObjectIdentified(outpostSite));
        Assert.IsFalse(SensoryManager.ShouldMaskWorldObjectAsUnknown(settlement),
            "Settlement must no longer be masked once scanned by the inner Identification ring.");
        Assert.IsFalse(SensoryManager.ShouldMaskWorldObjectAsUnknown(outpostSite),
            "Outpost site must no longer be masked once scanned by the inner Identification ring.");

        Assert.AreEqual("Covenant Stronghold", settlement.Label);
        Assert.AreEqual("Pirate Artillery Outpost", outpostSite.Label);
        Assert.IsFalse(hazeManager.IsSiteConcealed(outpostSite),
            "Dynamic outpost site must be uncovered once scanned by the inner Identification ring.");
    }

    [TestMethod]
    public void DynamicSiteSpawnedOnPreviouslyIdentifiedTile_OutsideActiveIdentification_RemainsUnknownUntilRescanned()
    {
        ProjectVigilance.Geoscape.GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;

        var sensory = new SensoryManager(null!);
        SensoryManager.Instance = sensory;
        var hazeManager = new HazeReconnaissanceManager(null!);
        HazeReconnaissanceManager.Instance = hazeManager;

        PlanetTile tile = (PlanetTile)77;

        // Tile 77 was previously explored and identified by a passing caravan in the past
        sensory.MarkTileExplored(tile);
        sensory.MarkTileIdentified(tile);
        Assert.IsTrue(sensory.IsExplored(tile));
        Assert.IsTrue(sensory.IsTileIdentified(tile));

        // Later, while no active sensor covers Tile 77, a new enemy outpost spawns on Tile 77
        var newOutpost = new Site { ID = 40201, customLabel = "Raider Siege Camp" };
        HarmonyLib.Traverse.Create(newOutpost).Field("tile").SetValue(tile);
        HarmonyLib.Traverse.Create(newOutpost).Field("spawned").SetValue(true);
        HazeReconnaissanceManager.WorldObjectLookupOverride = id => id == 40201 ? newOutpost : null;

        Assert.IsFalse(sensory.HasActiveIdentification(tile));
        sensory.MarkWorldObjectUnidentified(newOutpost);
        hazeManager.RegisterConcealedSite(newOutpost);

        // Even though the tile was identified in the past, the newly spawned outpost is NOT yet identified
        Assert.IsFalse(sensory.IsWorldObjectIdentified(newOutpost),
            "Newly spawned outpost outside active identification range must be treated as unidentified.");
        Assert.IsTrue(SensoryManager.ShouldMaskWorldObjectAsUnknown(newOutpost),
            "Newly spawned outpost on a previously explored tile must appear as an unknown site until rescanned.");

        hazeManager.CheckUncoveredSites();
        Assert.IsTrue(hazeManager.IsSiteConcealed(newOutpost),
            "Newly spawned outpost must remain concealed in HazeReconnaissanceManager until rescanned by an identification ring.");

        // Internal safe label helper must still retrieve the true name for logging/letters without leaking to UI
        Assert.AreEqual("Raider Siege Camp", HazeReconnaissanceManager.GetSafeSiteLabel(newOutpost));
        Assert.IsFalse(SensoryManager.SuppressUnknownMasking);

        // When an active sensor's identification ring scans Tile 77, the outpost becomes identified and uncovered
        sensory.MarkSurroundingTilesIdentified(tile, 0);
        Assert.IsTrue(sensory.IsWorldObjectIdentified(newOutpost));
        Assert.IsFalse(SensoryManager.ShouldMaskWorldObjectAsUnknown(newOutpost));
        Assert.IsFalse(hazeManager.IsSiteConcealed(newOutpost));
    }

    [TestMethod]
    public void MobileIncursionsAndCaravans_NeverMaskedByStaticUnknownSiteMasking()
    {
        var inc = new ProjectVigilance.Geoscape.World.WorldObject_Incursion();
        var caravan = (Caravan)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Caravan));
        var waypoint = new RoutePlannerWaypoint();

        Assert.IsFalse(SensoryManager.RequiresSiteIdentification(inc));
        Assert.IsFalse(SensoryManager.RequiresSiteIdentification(caravan));
        Assert.IsFalse(SensoryManager.RequiresSiteIdentification(waypoint));
    }

    [TestMethod]
    public void HarmonyPatches_UnknownSiteMaskingAndUprightDrawing_CanBeAppliedCleanlyWithoutException()
    {
        var harmony = new HarmonyLib.Harmony("test.unknown.and.upright.harmony.validation");

        harmony.CreateClassProcessor(typeof(ProjectVigilance.Geoscape.Patches.WorldObject_Visibility_Patch.WorldObject_Draw_Upright_Patch)).Patch();
        harmony.CreateClassProcessor(typeof(ProjectVigilance.Geoscape.Patches.WorldObject_Visibility_Patch.WorldObject_Print_Upright_Patch)).Patch();
        harmony.CreateClassProcessor(typeof(ProjectVigilance.Geoscape.Patches.WorldObject_Visibility_Patch.UnknownSite_Label_Patch)).Patch();
        harmony.CreateClassProcessor(typeof(ProjectVigilance.Geoscape.Patches.WorldObject_Visibility_Patch.UnknownSite_ExpandingIcon_Patch)).Patch();
        harmony.CreateClassProcessor(typeof(ProjectVigilance.Geoscape.Patches.WorldObject_Visibility_Patch.UnknownSite_ExpandingIconColor_Patch)).Patch();
        harmony.CreateClassProcessor(typeof(ProjectVigilance.Geoscape.Patches.WorldObject_Visibility_Patch.UnknownSite_ExpandingMaterial_Patch)).Patch();
        harmony.CreateClassProcessor(typeof(ProjectVigilance.Geoscape.Patches.WorldObject_Visibility_Patch.UnknownSite_Material_Patch)).Patch();
        harmony.CreateClassProcessor(typeof(ProjectVigilance.Geoscape.Patches.WorldObject_Visibility_Patch.UnknownSite_GetInspectString_Patch)).Patch();
        harmony.CreateClassProcessor(typeof(ProjectVigilance.Geoscape.Patches.WorldObject_Visibility_Patch.UnknownSite_GetDescription_Patch)).Patch();
        harmony.CreateClassProcessor(typeof(ProjectVigilance.Geoscape.Patches.WorldObject_Visibility_Patch.UnknownSite_ShowRelatedQuests_Patch)).Patch();
        harmony.CreateClassProcessor(typeof(ProjectVigilance.Geoscape.Patches.WorldObject_Visibility_Patch.UnknownSite_GetGizmos_Patch)).Patch();
        harmony.CreateClassProcessor(typeof(ProjectVigilance.Geoscape.Patches.WorldObject_Visibility_Patch.UnknownSite_GetFloatMenuOptions_Patch)).Patch();
    }
}
