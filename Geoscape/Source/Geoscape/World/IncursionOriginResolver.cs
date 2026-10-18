using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.World;

public static class IncursionOriginResolver
{
    public static bool TryResolveOrigin(
        IncidentParms parms,
        PlanetTile destinationTile,
        out PlanetTile originTile,
        out List<PlanetTile> path,
        out float speed)
    {
        originTile = PlanetTile.Invalid;
        path = new List<PlanetTile>();
        speed = GetSpeedForFaction(parms.faction);

        if (!destinationTile.Valid)
        {
            return false;
        }

        // 1. Try finding settlements belonging to the faction (weighted by distance)
        if (parms.faction != null && Find.WorldObjects?.Settlements != null)
        {
            var factionSettlements = Find.WorldObjects.Settlements
                .Where(s => s.Faction == parms.faction && s.Tile.Valid && s.Tile.Layer == destinationTile.Layer)
                .OrderBy(s => Find.WorldGrid.ApproxDistanceInTiles(s.Tile, destinationTile))
                .ToList();

            var reachableSettlements = new List<(PlanetTile Tile, List<PlanetTile> Path, float Distance)>();

            foreach (var settlement in factionSettlements)
            {
                if (TryBuildPath(settlement.Tile, destinationTile, out var foundPath))
                {
                    float dist = Find.WorldGrid.ApproxDistanceInTiles(settlement.Tile, destinationTile);
                    reachableSettlements.Add((settlement.Tile, foundPath, dist));

                    if (reachableSettlements.Count >= 4)
                    {
                        break;
                    }
                }
            }

            if (reachableSettlements.Count > 0)
            {
                // Weighted selection: Closer bases have higher probability, but regional bases can also strike!
                var chosen = reachableSettlements.RandomElementByWeight(s => 100f / Math.Max(5f, s.Distance));
                originTile = chosen.Tile;
                path = chosen.Path;
                return true;
            }
        }

        // 2. Fallback: Search for a passable wilderness tile ~15 to 30 hexes away
        if (TryFindWildernessStagingTile(destinationTile, out var wildernessTile))
        {
            if (TryBuildPath(wildernessTile, destinationTile, out var foundPath))
            {
                originTile = wildernessTile;
                path = foundPath;
                return true;
            }
        }

        return false;
    }

    private static bool TryBuildPath(PlanetTile start, PlanetTile dest, out List<PlanetTile> resultPath)
    {
        resultPath = new List<PlanetTile>();

        if (start == dest || !start.Valid || !dest.Valid)
        {
            return false;
        }

        WorldPath path = dest.Layer.Pather.FindPath(start, dest, null);
        try
        {
            if (path != null && path.Found && path.NodesLeftCount > 0)
            {
                resultPath = new List<PlanetTile>(path.NodesReversed);
                resultPath.Reverse();
                return true;
            }
        }
        finally
        {
            path?.ReleaseToPool();
        }

        return false;
    }

    private static bool TryFindWildernessStagingTile(PlanetTile center, out PlanetTile stagingTile)
    {
        stagingTile = PlanetTile.Invalid;
        var worldGrid = Find.WorldGrid;
        var floodFiller = center.Layer.Filler;
        if (worldGrid == null || floodFiller == null || !center.Valid)
        {
            return false;
        }

        var candidates = new List<PlanetTile>();
        var allConnectedLand = new List<PlanetTile>();

        floodFiller.FloodFill(
            center,
            tile =>
            {
                if (!tile.Valid || tile.Layer != center.Layer)
                {
                    return false;
                }
                Tile t = worldGrid[tile];
                return t != null && !t.WaterCovered && t.PrimaryBiome != null && !t.PrimaryBiome.impassable;
            },
            (tile, dist) =>
            {
                if (tile != center)
                {
                    allConnectedLand.Add(tile);
                    if (dist >= 12 && dist <= 35)
                    {
                        candidates.Add(tile);
                    }
                }
            },
            500,
            null);

        if (candidates.Count > 0)
        {
            stagingTile = candidates.RandomElement();
            return true;
        }

        // On smaller islands where max overland distance is < 12 hexes, pick the furthest connected tile (beachhead)
        if (allConnectedLand.Count > 0)
        {
            stagingTile = allConnectedLand
                .OrderByDescending(t => worldGrid.ApproxDistanceInTiles(t, center))
                .FirstOrDefault();
            return stagingTile.Valid;
        }

        return false;
    }

    public static bool TryResolveMultipleOrigins(
        IncidentParms parms,
        PlanetTile destinationTile,
        int count,
        out List<(PlanetTile Origin, List<PlanetTile> Path, float Speed)> results)
    {
        results = new List<(PlanetTile Origin, List<PlanetTile> Path, float Speed)>();
        float speed = GetSpeedForFaction(parms.faction);

        if (!destinationTile.Valid)
        {
            return false;
        }

        if (parms.faction != null && Find.WorldObjects?.Settlements != null)
        {
            var factionSettlements = Find.WorldObjects.Settlements
                .Where(s => s.Faction == parms.faction && s.Tile.Valid && s.Tile.Layer == destinationTile.Layer)
                .OrderBy(s => Find.WorldGrid.ApproxDistanceInTiles(s.Tile, destinationTile))
                .ToList();

            foreach (var settlement in factionSettlements)
            {
                if (TryBuildPath(settlement.Tile, destinationTile, out var foundPath))
                {
                    results.Add((settlement.Tile, foundPath, speed));
                    if (results.Count >= count)
                    {
                        return true;
                    }
                }
            }
        }

        // If not enough settlement origins found, fallback to single origin
        if (results.Count == 0 && TryResolveOrigin(parms, destinationTile, out var singleOrigin, out var singlePath, out var singleSpeed))
        {
            results.Add((singleOrigin, singlePath, singleSpeed));
            return true;
        }

        return results.Count > 0;
    }

    public static float GetSpeedForFaction(Faction? faction)
    {
        if (faction == null)
        {
            return 24f;
        }

        return faction.def.techLevel switch
        {
            TechLevel.Neolithic => 16f,
            TechLevel.Medieval => 20f,
            TechLevel.Industrial => 28f,
            TechLevel.Spacer => 36f,
            TechLevel.Ultra or TechLevel.Archotech => 45f,
            _ => 24f
        };
    }
}
