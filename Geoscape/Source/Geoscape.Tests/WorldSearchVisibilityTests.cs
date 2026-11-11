using System;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape;
using ProjectVigilance.Geoscape.Patches;
using ProjectVigilance.Geoscape.Rules;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Geoscape.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WorldSearchVisibilityTests
{
    private VisibilityMode originalMode;

    [TestInitialize]
    public void Init()
    {
        GeoscapeCampaignRules.ResetStaticDataForTesting();
        originalMode = GeoscapeMod.Settings.visibilityMode;
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;
        SensoryManager.Instance = new SensoryManager(null!);
        SensoryManager.IsExploredOverride = null;
        SensoryManager.IsIdentifiedOverride = null;
    }

    [TestCleanup]
    public void Cleanup()
    {
        GeoscapeMod.Settings.visibilityMode = originalMode;
        SensoryManager.IsExploredOverride = null;
        SensoryManager.IsIdentifiedOverride = null;
        SensoryManager.Instance = null;
        GeoscapeCampaignRules.ResetStaticDataForTesting();
    }

    [TestMethod]
    public void Classify_GatesWorldObjectsLandmarksMutatorsAndIncursionsCorrectly()
    {
        // 1. Hidden surface/orbital world object -> Excluded
        Assert.AreEqual(
            WorldSearchElementState.Excluded,
            WorldSearchVisibilityUtility.Classify(
                VisibilityMode.ClassicHazeOfWar,
                hasWorldObject: true,
                isPlayerFaction: false,
                isHiddenWorldObject: true,
                shouldMaskWorldObject: false,
                isIncursion: false,
                incursionTier: IntelligenceTier.Undetected,
                hasLandmark: false,
                isManMadeLandmark: false,
                isTileExplored: false,
                isTileIdentified: false));

        // 2. Explored but unidentified site -> VisibleMasked
        Assert.AreEqual(
            WorldSearchElementState.VisibleMasked,
            WorldSearchVisibilityUtility.Classify(
                VisibilityMode.ClassicHazeOfWar,
                hasWorldObject: true,
                isPlayerFaction: false,
                isHiddenWorldObject: false,
                shouldMaskWorldObject: true,
                isIncursion: false,
                incursionTier: IntelligenceTier.Undetected,
                hasLandmark: false,
                isManMadeLandmark: false,
                isTileExplored: true,
                isTileIdentified: false));

        // 3. Explored but unidentified man-made Odyssey landmark -> VisibleMasked
        Assert.AreEqual(
            WorldSearchElementState.VisibleMasked,
            WorldSearchVisibilityUtility.Classify(
                VisibilityMode.ClassicHazeOfWar,
                hasWorldObject: false,
                isPlayerFaction: false,
                isHiddenWorldObject: false,
                shouldMaskWorldObject: false,
                isIncursion: false,
                incursionTier: IntelligenceTier.Undetected,
                hasLandmark: true,
                isManMadeLandmark: true,
                isTileExplored: true,
                isTileIdentified: false));

        // 4. Explored unidentified natural landmark -> VisibleUnmasked
        Assert.AreEqual(
            WorldSearchElementState.VisibleUnmasked,
            WorldSearchVisibilityUtility.Classify(
                VisibilityMode.ClassicHazeOfWar,
                hasWorldObject: false,
                isPlayerFaction: false,
                isHiddenWorldObject: false,
                shouldMaskWorldObject: false,
                isIncursion: false,
                incursionTier: IntelligenceTier.Undetected,
                hasLandmark: true,
                isManMadeLandmark: false,
                isTileExplored: true,
                isTileIdentified: false));

        // 5. Mutators-only element requires full identification
        Assert.AreEqual(
            WorldSearchElementState.Excluded,
            WorldSearchVisibilityUtility.Classify(
                VisibilityMode.ClassicHazeOfWar,
                hasWorldObject: false,
                isPlayerFaction: false,
                isHiddenWorldObject: false,
                shouldMaskWorldObject: false,
                isIncursion: false,
                incursionTier: IntelligenceTier.Undetected,
                hasLandmark: false,
                isManMadeLandmark: false,
                isTileExplored: true,
                isTileIdentified: false));

        // 6. Radar Contact incursion -> VisibleUnmasked; Undetected incursion -> Excluded
        Assert.AreEqual(
            WorldSearchElementState.Excluded,
            WorldSearchVisibilityUtility.Classify(
                VisibilityMode.ClassicHazeOfWar,
                hasWorldObject: true,
                isPlayerFaction: false,
                isHiddenWorldObject: false,
                shouldMaskWorldObject: false,
                isIncursion: true,
                incursionTier: IntelligenceTier.Undetected,
                hasLandmark: false,
                isManMadeLandmark: false,
                isTileExplored: true,
                isTileIdentified: true));

        Assert.AreEqual(
            WorldSearchElementState.VisibleUnmasked,
            WorldSearchVisibilityUtility.Classify(
                VisibilityMode.ClassicHazeOfWar,
                hasWorldObject: true,
                isPlayerFaction: false,
                isHiddenWorldObject: false,
                shouldMaskWorldObject: false,
                isIncursion: true,
                incursionTier: IntelligenceTier.Contact,
                hasLandmark: false,
                isManMadeLandmark: false,
                isTileExplored: true,
                isTileIdentified: false));

        // 7. Planetary Surveillance and Omniscient bypass fog restrictions
        Assert.AreEqual(
            WorldSearchElementState.VisibleUnmasked,
            WorldSearchVisibilityUtility.Classify(
                VisibilityMode.PlanetarySurveillance,
                hasWorldObject: true,
                isPlayerFaction: false,
                isHiddenWorldObject: true,
                shouldMaskWorldObject: true,
                isIncursion: true,
                incursionTier: IntelligenceTier.Undetected,
                hasLandmark: true,
                isManMadeLandmark: true,
                isTileExplored: false,
                isTileIdentified: false));
    }

    [TestMethod]
    public void CanSearchMutators_RequiresTileIdentificationUnderHazeOfWar()
    {
        PlanetTile tile = new(50);

        SensoryManager.IsExploredOverride = _ => false;
        SensoryManager.IsIdentifiedOverride = _ => false;
        Assert.IsFalse(WorldSearchVisibilityUtility.CanSearchMutators(tile));

        SensoryManager.IsExploredOverride = _ => true;
        SensoryManager.IsIdentifiedOverride = _ => false;
        Assert.IsFalse(WorldSearchVisibilityUtility.CanSearchMutators(tile));

        SensoryManager.IsIdentifiedOverride = _ => true;
        Assert.IsTrue(WorldSearchVisibilityUtility.CanSearchMutators(tile));

        GeoscapeMod.Settings.visibilityMode = VisibilityMode.PlanetarySurveillance;
        SensoryManager.IsExploredOverride = _ => false;
        SensoryManager.IsIdentifiedOverride = _ => false;
        Assert.IsTrue(WorldSearchVisibilityUtility.CanSearchMutators(tile));
    }

    [TestMethod]
    public void ResolveMaskedDisplayLabel_DistinguishesLandmarkFromSite()
    {
        var manMadeElement = new WorldSearchElement
        {
            tile = new PlanetTile(10),
            landmark = new Landmark { def = new LandmarkDef { defName = "AbandonedColonyOutlander" }, name = "Ouror Outpost" }
        };

        var settlementElement = new WorldSearchElement
        {
            tile = new PlanetTile(11),
            worldObject = new Settlement { def = new WorldObjectDef { defName = "Settlement" } }
        };

        Assert.IsTrue(WorldSearchVisibilityUtility.UsesLandmarkMask(manMadeElement));
        Assert.AreEqual(
            SensoryNarrativeUtility.GetUnknownLandmarkLabel().CapitalizeFirst(),
            WorldSearchVisibilityUtility.ResolveMaskedDisplayLabel(manMadeElement));

        Assert.IsFalse(WorldSearchVisibilityUtility.UsesLandmarkMask(settlementElement));
        Assert.AreEqual(
            SensoryNarrativeUtility.GetUnknownSiteLabel().CapitalizeFirst(),
            WorldSearchVisibilityUtility.ResolveMaskedDisplayLabel(settlementElement));
    }

    [TestMethod]
    public void WorldSearchPatches_ResolveAllRimWorldTargetsWithoutError()
    {
        string id = $"Geoscape.Tests.WorldSearch.{Guid.NewGuid():N}";
        Harmony harmony = new(id);
        try
        {
            Type[] patchTypes =
            {
                typeof(WorldSearch_Visibility_Patch.WorldSearchElement_DisplayLabel_Patch),
                typeof(WorldSearch_Visibility_Patch.Dialog_WorldSearch_InitializeSearchSet_Patch),
                typeof(WorldSearch_Visibility_Patch.Dialog_WorldSearch_ShouldSkipElement_Patch),
                typeof(WorldSearch_Visibility_Patch.Dialog_WorldSearch_ElementMatch_Patch),
                typeof(WorldSearch_Visibility_Patch.Dialog_WorldSearch_DoIcon_Patch),
                typeof(WorldSearch_Visibility_Patch.Dialog_WorldSearch_DoLabel_Patch),
                typeof(WorldSearch_Visibility_Patch.Dialog_WorldSearch_WindowUpdate_Patch)
            };

            foreach (Type patchType in patchTypes)
            {
                harmony.CreateClassProcessor(patchType).Patch();
            }
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }
}
