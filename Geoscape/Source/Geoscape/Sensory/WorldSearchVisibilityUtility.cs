using ProjectVigilance.Geoscape.Orbital;
using ProjectVigilance.Geoscape.Patches;
using ProjectVigilance.Geoscape.World;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Visibility classification for an entry in RimWorld's world search dialog.
/// </summary>
public enum WorldSearchElementState
{
    Excluded = 0,
    VisibleUnmasked = 1,
    VisibleMasked = 2
}

/// <summary>
/// Evaluates fog-of-war gating, masked labels, and mutator search permissions for <see cref="Dialog_WorldSearch"/>.
/// </summary>
public static class WorldSearchVisibilityUtility
{
    /// <summary>
    /// Evaluates search visibility from explicit domain flags for headless unit testing.
    /// </summary>
    public static WorldSearchElementState Classify(
        VisibilityMode mode,
        bool hasWorldObject,
        bool isPlayerFaction,
        bool isHiddenWorldObject,
        bool shouldMaskWorldObject,
        bool isIncursion,
        IntelligenceTier incursionTier,
        bool hasLandmark,
        bool isManMadeLandmark,
        bool isTileExplored,
        bool isTileIdentified)
    {
        if (mode != VisibilityMode.ClassicHazeOfWar)
        {
            return WorldSearchElementState.VisibleUnmasked;
        }

        if (hasWorldObject)
        {
            if (isPlayerFaction)
            {
                return WorldSearchElementState.VisibleUnmasked;
            }

            if (isHiddenWorldObject)
            {
                return WorldSearchElementState.Excluded;
            }

            if (isIncursion)
            {
                return incursionTier >= IntelligenceTier.Contact
                    ? WorldSearchElementState.VisibleUnmasked
                    : WorldSearchElementState.Excluded;
            }

            return shouldMaskWorldObject
                ? WorldSearchElementState.VisibleMasked
                : WorldSearchElementState.VisibleUnmasked;
        }

        if (!isTileExplored)
        {
            return WorldSearchElementState.Excluded;
        }

        if (hasLandmark)
        {
            if (isManMadeLandmark && !isTileIdentified)
            {
                return WorldSearchElementState.VisibleMasked;
            }

            return WorldSearchElementState.VisibleUnmasked;
        }

        return isTileIdentified
            ? WorldSearchElementState.VisibleUnmasked
            : WorldSearchElementState.Excluded;
    }

    /// <summary>
    /// Classifies a live <see cref="WorldSearchElement"/> under the current campaign rules and sensory state.
    /// </summary>
    public static WorldSearchElementState Classify(WorldSearchElement? element)
    {
        if (element == null)
        {
            return WorldSearchElementState.Excluded;
        }

        if (GeoscapeRules.VisibilityMode != VisibilityMode.ClassicHazeOfWar)
        {
            return WorldSearchElementState.VisibleUnmasked;
        }

        WorldObject? wo = element.worldObject;
        if (wo != null)
        {
            bool isPlayer = wo.Faction?.IsPlayer == true;
            bool isHidden = WorldObject_Visibility_Patch.ShouldHideWorldObject(wo);
            bool shouldMask = SensoryManager.ShouldMaskWorldObjectAsUnknown(wo);
            bool isIncursion = wo is WorldObject_Incursion;
            IntelligenceTier tier = wo is WorldObject_Incursion incursion
                ? incursion.CurrentIntelligenceTier
                : IntelligenceTier.Undetected;

            return Classify(
                GeoscapeRules.VisibilityMode,
                hasWorldObject: true,
                isPlayerFaction: isPlayer,
                isHiddenWorldObject: isHidden,
                shouldMaskWorldObject: shouldMask,
                isIncursion: isIncursion,
                incursionTier: tier,
                hasLandmark: element.landmark != null,
                isManMadeLandmark: false,
                isTileExplored: true,
                isTileIdentified: true);
        }

        PlanetTile tile = element.tile;
        if (!tile.Valid)
        {
            return WorldSearchElementState.Excluded;
        }

        bool explored = StartingSiteFeedbackUtility.IsTileExplored(tile);
        bool identified = TerrainTelemetryUtility.GetTileIntelLevel(tile) == TileIntelLevel.Identified;

        bool hasLandmark = element.landmark != null;
        bool isManMade = hasLandmark &&
            element.landmark!.def != null &&
            LandmarkSensoryUtility.IsManMadeLandmarkDef(element.landmark.def.defName);

        return Classify(
            GeoscapeRules.VisibilityMode,
            hasWorldObject: false,
            isPlayerFaction: false,
            isHiddenWorldObject: false,
            shouldMaskWorldObject: false,
            isIncursion: false,
            incursionTier: IntelligenceTier.Undetected,
            hasLandmark: hasLandmark,
            isManMadeLandmark: isManMade,
            isTileExplored: explored,
            isTileIdentified: identified);
    }

    /// <summary>
    /// Returns true if tile mutators ("Features:") on <paramref name="tile"/> may be searched in <see cref="Dialog_WorldSearch"/>.
    /// </summary>
    public static bool CanSearchMutators(PlanetTile tile) =>
        TerrainTelemetryUtility.GetTileIntelLevel(tile) == TileIntelLevel.Identified;

    /// <summary>
    /// Returns true if an element is an explored-but-unidentified man-made landmark or anomalous site
    /// whose masked indicator is the tactical cyan '!'.
    /// </summary>
    public static bool UsesLandmarkMask(WorldSearchElement? element)
    {
        if (element == null)
        {
            return false;
        }

        if (element.worldObject != null)
        {
            return LandmarkSensoryUtility.IsAnomalousStructure(element.worldObject);
        }

        return element.landmark?.def != null &&
            LandmarkSensoryUtility.IsManMadeLandmarkDef(element.landmark.def.defName);
    }

    /// <summary>
    /// Resolves the safe, non-spoiler display label for a search element in <see cref="WorldSearchElementState.VisibleMasked"/>.
    /// </summary>
    public static string ResolveMaskedDisplayLabel(WorldSearchElement? element)
    {
        if (element == null)
        {
            return string.Empty;
        }

        if (UsesLandmarkMask(element))
        {
            return SensoryNarrativeUtility.GetUnknownLandmarkLabel().CapitalizeFirst();
        }

        return SensoryNarrativeUtility.GetUnknownSiteLabel().CapitalizeFirst();
    }
}
