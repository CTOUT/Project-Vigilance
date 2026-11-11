using System;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape;
using ProjectVigilance.Geoscape.Patches;
using ProjectVigilance.Geoscape.Rules;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld.Planet;

namespace Geoscape.Tests;

[TestClass]
[DoNotParallelize]
public sealed class TileHighlightVisibilityTests
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
        TileHighlightVisibilityUtility.IsPlayingOverride = null;
    }

    [TestCleanup]
    public void Cleanup()
    {
        GeoscapeMod.Settings.visibilityMode = originalMode;
        SensoryManager.IsExploredOverride = null;
        SensoryManager.IsIdentifiedOverride = null;
        SensoryManager.Instance = null;
        TileHighlightVisibilityUtility.IsPlayingOverride = null;
        GeoscapeCampaignRules.ResetStaticDataForTesting();
    }

    [TestMethod]
    public void ShouldRenderSingleTileHighlight_PureTruthTable_GatesOnlyUnexploredGameplayUnderFogOfWar()
    {
        PlanetTile validTile = new(123);

        // Invalid tiles pass through regardless of mode or exploration.
        Assert.IsTrue(
            TileHighlightVisibilityUtility.ShouldRenderSingleTileHighlight(
                PlanetTile.Invalid,
                VisibilityMode.ClassicHazeOfWar,
                isPlaying: true,
                isExplored: false));

        // Non-fog visibility modes keep highlights visible everywhere.
        Assert.IsTrue(
            TileHighlightVisibilityUtility.ShouldRenderSingleTileHighlight(
                validTile,
                VisibilityMode.PlanetarySurveillance,
                isPlaying: true,
                isExplored: false));
        Assert.IsTrue(
            TileHighlightVisibilityUtility.ShouldRenderSingleTileHighlight(
                validTile,
                VisibilityMode.Omniscient,
                isPlaying: true,
                isExplored: false));

        // Starting-site selection (ProgramState.Entry) retains highlights even over uncharted tiles.
        Assert.IsTrue(
            TileHighlightVisibilityUtility.ShouldRenderSingleTileHighlight(
                validTile,
                VisibilityMode.ClassicHazeOfWar,
                isPlaying: false,
                isExplored: false));

        // Option A: Explored tiles (including DetectedOnly haze) retain crisp highlights during play.
        Assert.IsTrue(
            TileHighlightVisibilityUtility.ShouldRenderSingleTileHighlight(
                validTile,
                VisibilityMode.ClassicHazeOfWar,
                isPlaying: true,
                isExplored: true));

        // Option A: Unexplored shroud tiles suppress single-tile highlights during active play.
        Assert.IsFalse(
            TileHighlightVisibilityUtility.ShouldRenderSingleTileHighlight(
                validTile,
                VisibilityMode.ClassicHazeOfWar,
                isPlaying: true,
                isExplored: false));
    }

    [TestMethod]
    public void PatchPostfixes_FilterOnlyUnexploredHighlightsDuringGameplay()
    {
        PlanetTile exploredTile = new(101);
        PlanetTile unexploredTile = new(202);
        SensoryManager.IsExploredOverride = tileId => tileId == exploredTile.tileId;
        TileHighlightVisibilityUtility.IsPlayingOverride = true;

        // Explored tile remains visible for both hovered and selected highlights.
        PlanetTile mouseExplored = exploredTile;
        PlanetTile selectedExplored = exploredTile;
        WorldDrawLayer_MouseTile_Tile_Patch.Postfix(ref mouseExplored);
        WorldDrawLayer_SelectedTile_Tile_Patch.Postfix(ref selectedExplored);
        Assert.AreEqual(exploredTile, mouseExplored);
        Assert.AreEqual(exploredTile, selectedExplored);

        // Unexplored shroud tile collapses to PlanetTile.Invalid during gameplay.
        PlanetTile mouseUnexplored = unexploredTile;
        PlanetTile selectedUnexplored = unexploredTile;
        WorldDrawLayer_MouseTile_Tile_Patch.Postfix(ref mouseUnexplored);
        WorldDrawLayer_SelectedTile_Tile_Patch.Postfix(ref selectedUnexplored);
        Assert.AreEqual(PlanetTile.Invalid, mouseUnexplored);
        Assert.AreEqual(PlanetTile.Invalid, selectedUnexplored);

        // Already-invalid tile remains invalid.
        PlanetTile invalidTile = PlanetTile.Invalid;
        WorldDrawLayer_MouseTile_Tile_Patch.Postfix(ref invalidTile);
        Assert.AreEqual(PlanetTile.Invalid, invalidTile);

        // Starting-site screen (Entry state) preserves highlights over unexplored tiles.
        TileHighlightVisibilityUtility.IsPlayingOverride = false;
        PlanetTile entryUnexplored = unexploredTile;
        WorldDrawLayer_SelectedTile_Tile_Patch.Postfix(ref entryUnexplored);
        Assert.AreEqual(unexploredTile, entryUnexplored);

        // Planetary Surveillance preserves highlights during gameplay regardless of shroud array.
        TileHighlightVisibilityUtility.IsPlayingOverride = true;
        GeoscapeMod.Settings.visibilityMode = VisibilityMode.PlanetarySurveillance;
        PlanetTile surveillanceUnexplored = unexploredTile;
        WorldDrawLayer_MouseTile_Tile_Patch.Postfix(ref surveillanceUnexplored);
        Assert.AreEqual(unexploredTile, surveillanceUnexplored);
    }

    [TestMethod]
    public void SingleTileHighlightPatches_ResolveAllRimWorldTargetsWithoutError()
    {
        string id = $"Geoscape.Tests.SingleTile.{Guid.NewGuid():N}";
        Harmony harmony = new(id);
        try
        {
            Type[] patchTypes =
            {
                typeof(WorldDrawLayer_MouseTile_Tile_Patch),
                typeof(WorldDrawLayer_SelectedTile_Tile_Patch)
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
