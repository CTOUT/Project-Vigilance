using System;
using System.Collections.Generic;
using RimWorld.Planet;
using Verse;
using ProjectVigilance.Geoscape.Patches;
using ProjectVigilance.Geoscape.World;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Enforces bidirectional tile exclusivity between sensory outposts (<see cref="WorldObject_ListeningPost"/>)
/// and permanent or temporary settlements, camps, quest sites, and ruins on the Geoscape.
/// </summary>
public static class OutpostTileOccupancyUtility
{
    /// <summary>
    /// Optional hook for resolving a blocking settlement or site at a tile during headless unit tests.
    /// </summary>
    public static Func<PlanetTile, WorldObject?>? BlockingObjectAtOverride { get; set; }

    /// <summary>
    /// Optional hook for resolving a listening post at a tile during headless unit tests.
    /// </summary>
    public static Func<PlanetTile, WorldObject_ListeningPost?>? ListeningPostAtOverride { get; set; }

    /// <summary>
    /// Attempts to find a non-destroyed <see cref="WorldObject_ListeningPost"/> occupying <paramref name="tile"/>.
    /// </summary>
    public static bool TryGetListeningPostAt(
        PlanetTile tile,
        out WorldObject_ListeningPost? post,
        bool includeUnderConstruction = true)
    {
        post = null;
        if (!tile.Valid)
        {
            return false;
        }

        if (ListeningPostAtOverride != null)
        {
            WorldObject_ListeningPost? overridden = ListeningPostAtOverride(tile);
            if (overridden != null && !overridden.Destroyed && (includeUnderConstruction || !overridden.IsConstructing))
            {
                post = overridden;
                return true;
            }
            return false;
        }

        var allObjects = Current.Game?.World?.worldObjects?.AllWorldObjects;
        if (allObjects == null)
        {
            return false;
        }

        for (int i = 0; i < allObjects.Count; i++)
        {
            if (allObjects[i] is WorldObject_ListeningPost candidate &&
                !candidate.Destroyed &&
                candidate.Tile == tile &&
                (includeUnderConstruction || !candidate.IsConstructing))
            {
                post = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns any settlement, player camp, map parent, quest site, or abandoned settlement occupying
    /// <paramref name="tile"/> that must prevent overlapping outpost construction.
    /// Transient mobile groups (<see cref="Caravan"/>, <see cref="WorldObject_Incursion"/>) and
    /// <see cref="WorldObject_ListeningPost"/> instances are excluded.
    /// </summary>
    public static WorldObject? GetBlockingFacilityOrSiteAt(
        PlanetTile tile,
        IEnumerable<Settlement>? knownSettlements = null)
    {
        if (!tile.Valid)
        {
            return null;
        }

        if (BlockingObjectAtOverride != null)
        {
            WorldObject? overridden = BlockingObjectAtOverride(tile);
            if (overridden != null && !overridden.Destroyed)
            {
                return overridden;
            }
        }

        Settlement? testOrWorldSettlement = FloatMenuMakerWorld_ChoicesAtFor_Patch.GetSettlementAt(tile);
        if (testOrWorldSettlement != null && !testOrWorldSettlement.Destroyed)
        {
            return testOrWorldSettlement;
        }

        if (knownSettlements != null)
        {
            foreach (Settlement s in knownSettlements)
            {
                if (s != null && !s.Destroyed && s.Tile == tile)
                {
                    return s;
                }
            }
        }

        var allObjects = Current.Game?.World?.worldObjects?.AllWorldObjects;
        if (allObjects == null)
        {
            return null;
        }

        for (int i = 0; i < allObjects.Count; i++)
        {
            WorldObject w = allObjects[i];
            if (w == null || w.Destroyed || w.Tile != tile)
            {
                continue;
            }

            if (w is WorldObject_ListeningPost || w is Caravan || w is WorldObject_Incursion)
            {
                continue;
            }

            if (w is Settlement || w is MapParent || w is AbandonedSettlement)
            {
                return w;
            }
        }

        return null;
    }

    /// <summary>
    /// Evaluates whether an outpost may be constructed or dispatched onto <paramref name="tile"/>.
    /// Rejects tiles occupied by an existing outpost (unless resuming construction when
    /// <paramref name="allowUnderConstructionPost"/> is true) or by any settlement, camp, site, or ruins.
    /// </summary>
    public static bool CanConstructOutpostAt(
        PlanetTile tile,
        out string blockedReason,
        bool allowUnderConstructionPost = false,
        IEnumerable<Settlement>? knownSettlements = null)
    {
        if (!tile.Valid)
        {
            blockedReason = "Invalid target tile.";
            return false;
        }

        if (TryGetListeningPostAt(tile, out WorldObject_ListeningPost? existingPost, includeUnderConstruction: !allowUnderConstructionPost))
        {
            string label = GetSafeObjectLabel(existingPost, "an outpost");
            blockedReason = $"Tile already occupied by {label}.";
            return false;
        }

        WorldObject? blockingSite = GetBlockingFacilityOrSiteAt(tile, knownSettlements);
        if (blockingSite != null)
        {
            string label = GetSafeObjectLabel(blockingSite, "an existing settlement or site");
            blockedReason = $"Cannot construct outpost on tile occupied by {label}.";
            return false;
        }

        blockedReason = string.Empty;
        return true;
    }

    /// <summary>
    /// Returns the player-facing rejection text shown when attempting to settle or make camp
    /// on a tile already dedicated to an outpost.
    /// </summary>
    public static string GetSettleOrCampBlockedByOutpostMessage(WorldObject_ListeningPost? post)
    {
        string label = GetSafeObjectLabel(post, "outpost");
        return $"Tile occupied by {label} — dismantle outpost first.";
    }

    private static string GetSafeObjectLabel(WorldObject? obj, string fallback)
    {
        if (obj == null)
        {
            return fallback;
        }

        try
        {
            string label = obj.LabelCap.ToString();
            if (!string.IsNullOrEmpty(label))
            {
                return label;
            }
        }
        catch
        {
            // Uninitialized WorldObject mock in unit tests without def/faction
        }

        try
        {
            string rawLabel = obj.Label;
            if (!string.IsNullOrEmpty(rawLabel))
            {
                return rawLabel;
            }
        }
        catch
        {
            // Fallback below
        }

        return fallback;
    }
}
