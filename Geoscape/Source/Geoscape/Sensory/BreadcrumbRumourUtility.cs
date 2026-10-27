using System;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Geoscape.World;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Provides traveller intelligence and breadcrumb rumours when friendly trade caravans
/// or visitors arrive at your settlement.
/// Friendly travellers share intelligence about unmapped dynamic sites they passed:
/// "Travellers from the [Faction] report sighting an active [Site Type] [X] hexes to the [Direction]."
/// Reveals the target hex on the Geoscape and logs a quest lead/letter.
/// </summary>
public static class BreadcrumbRumourUtility
{
    public static event Action<Faction?, WorldObject, int, string>? OnRumourShared;

    /// <summary>
    /// Computes an 8-way compass direction ("north", "northeast", "east", etc.)
    /// from origin tile to destination tile on the spherical planetary grid.
    /// </summary>
    public static string GetCompassDirection(PlanetTile fromTile, PlanetTile toTile)
    {
        if (!fromTile.Valid || !toTile.Valid || Find.World?.grid == null)
        {
            return "nearby";
        }

        Vector2 fromLatLong = Find.WorldGrid.LongLatOf(fromTile);
        Vector2 toLatLong = Find.WorldGrid.LongLatOf(toTile);

        float dLat = toLatLong.y - fromLatLong.y;
        float dLon = toLatLong.x - fromLatLong.x;

        // Normalise longitude delta to [-180, 180]
        while (dLon > 180f) dLon -= 360f;
        while (dLon < -180f) dLon += 360f;

        // Atan2: 0 is North (+dLat, 0 dLon), 90 is East, 180/-180 is South, -90 is West
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

    /// <summary>
    /// Evaluates whether arriving travellers can share intelligence regarding an unmapped
    /// dynamic site they passed along their route or near the settlement.
    /// </summary>
    public static bool TryShareTravellerRumour(Faction? travellerFaction, PlanetTile settlementTile, WorldObject_Incursion? incursion = null)
    {
        if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.ClassicHazeOfWar)
        {
            return false;
        }

        if (Find.World?.grid == null)
        {
            return false;
        }

        WorldObject? targetSite = FindCandidateSite(settlementTile, incursion);
        if (targetSite == null)
        {
            return false;
        }

        int hexDist = Find.WorldGrid.TraversalDistanceBetween(settlementTile, targetSite.Tile, passImpassable: true, maxDist: 60);
        if (hexDist <= 0 || hexDist > 100)
        {
            hexDist = Mathf.RoundToInt(Find.WorldGrid.ApproxDistanceInTiles(settlementTile, targetSite.Tile));
            if (hexDist <= 0) hexDist = 1;
        }

        string direction = GetCompassDirection(settlementTile, targetSite.Tile);
        string siteType = targetSite.Label;
        string factionName = travellerFaction?.Name ?? "peaceful travellers";

        OnRumourShared?.Invoke(travellerFaction, targetSite, hexDist, direction);

        // Reveal the target hex on the Geoscape
        SensoryManager.Instance?.MarkSurroundingTilesExplored(targetSite.Tile, 1f);

        // Uncover the site if it was concealed in HazeReconnaissanceManager
        HazeReconnaissanceManager.Instance?.UncoverSite(targetSite);

        if (Current.ProgramState == ProgramState.Playing && Find.LetterStack != null)
        {
            TaggedString title = $"Traveller Intelligence: {targetSite.LabelCap}";
            TaggedString text = $"Travellers from the {factionName} report sighting an active {siteType} {hexDist} hexes to the {direction}.\n\nThe location has been charted on your planetary map.";
            Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.PositiveEvent, new GlobalTargetInfo(targetSite.Tile));
        }

        return true;
    }

    public static WorldObject? FindCandidateSite(PlanetTile settlementTile, WorldObject_Incursion? incursion)
    {
        SensoryManager? sensory = SensoryManager.Instance;
        if (sensory == null || Find.World?.grid == null) return null;

        var allObjects = Find.World?.worldObjects?.AllWorldObjects;
        if (allObjects == null || allObjects.Count == 0) return null;

        // 1. If incursion has a path, check along their route for unmapped sites
        if (incursion?.PathTiles != null && incursion.PathTiles.Count > 0)
        {
            for (int i = 0; i < allObjects.Count; i++)
            {
                WorldObject wo = allObjects[i];
                if (wo.Spawned && wo.Tile.Valid && wo.Faction != Faction.OfPlayer && !sensory.IsExplored(wo.Tile) && HazeReconnaissanceManager.IsDynamicSite(wo))
                {
                    for (int p = 0; p < incursion.PathTiles.Count; p++)
                    {
                        if (Find.WorldGrid.TraversalDistanceBetween(incursion.PathTiles[p], wo.Tile, passImpassable: true, maxDist: 6) <= 6)
                        {
                            return wo;
                        }
                    }
                }
            }
        }

        // 2. Otherwise find the closest unmapped dynamic site to the settlement
        WorldObject? closest = null;
        int minDistance = int.MaxValue;

        for (int i = 0; i < allObjects.Count; i++)
        {
            WorldObject wo = allObjects[i];
            if (wo.Spawned && wo.Tile.Valid && wo.Faction != Faction.OfPlayer && !sensory.IsExplored(wo.Tile) && HazeReconnaissanceManager.IsDynamicSite(wo))
            {
                int dist = Find.WorldGrid.TraversalDistanceBetween(settlementTile, wo.Tile, passImpassable: true, maxDist: 40);
                if (dist > 0 && dist < minDistance)
                {
                    minDistance = dist;
                    closest = wo;
                }
            }
        }

        return closest;
    }
}
