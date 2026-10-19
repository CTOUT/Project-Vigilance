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
        var viableSettlements = GetViableFactionSettlements(parms.faction, destinationTile, speed, 4);
        if (viableSettlements.Count > 0)
        {
            // Weighted selection: Closer bases have higher probability, but regional bases can also strike!
            var chosen = viableSettlements.RandomElementByWeight(s => 100f / Math.Max(5f, s.Distance));
            originTile = chosen.Tile;
            path = chosen.Path;
            return true;
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

    private static List<(PlanetTile Tile, List<PlanetTile> Path, float Distance, float TravelHours)> GetViableFactionSettlements(
        Faction? faction,
        PlanetTile destinationTile,
        float speedTilesPerDay,
        int maxSettlements)
    {
        var result = new List<(PlanetTile Tile, List<PlanetTile> Path, float Distance, float TravelHours)>();
        if (faction == null || Find.WorldObjects?.Settlements == null || !destinationTile.Valid)
        {
            return result;
        }

        var factionSettlements = Find.WorldObjects.Settlements
            .Where(s => s.Faction == faction && s.Tile.Valid && s.Tile.Layer == destinationTile.Layer)
            .OrderBy(s => Find.WorldGrid.ApproxDistanceInTiles(s.Tile, destinationTile));

        foreach (var settlement in factionSettlements)
        {
            if (TryBuildPath(settlement.Tile, destinationTile, out var foundPath))
            {
                float dist = Find.WorldGrid.ApproxDistanceInTiles(settlement.Tile, destinationTile);
                float travelHours = (foundPath.Count / Math.Max(0.1f, speedTilesPerDay)) * 24f;
                result.Add((settlement.Tile, foundPath, dist, travelHours));

                if (result.Count >= maxSettlements)
                {
                    break;
                }
            }
        }

        return result;
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

    public const float MaxCoordinationWindowHours = 36f;

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

        // 1. Gather all reachable settlements belonging to this faction
        var viableSettlements = GetViableFactionSettlements(parms.faction, destinationTile, speed, 6);

        // 2. Check for multi-base coordination
        if (viableSettlements.Count > 0)
        {
            var primary = viableSettlements[0];
            results.Add((primary.Tile, primary.Path, speed));

            // Select all secondary bases whose travel times fall within the coordination window (<= 36h delta)
            var compatibleOtherBases = viableSettlements
                .Skip(1)
                .Where(s => Math.Abs(s.TravelHours - primary.TravelHours) <= MaxCoordinationWindowHours)
                .OrderBy(s => Math.Abs(s.TravelHours - primary.TravelHours))
                .ToList();

            foreach (var sec in compatibleOtherBases)
            {
                if (results.Count >= count)
                {
                    break;
                }
                results.Add((sec.Tile, sec.Path, speed));
            }

            // If we still need more vectors (e.g. only 1 or 2 bases available),
            // generate tactical flanking columns originating from the primary base!
            var existingPaths = results.Select(r => r.Path).ToList();
            float[] flankSampleFractions = { 0.30f, 0.60f, 0.45f };

            int flankIdx = 0;
            while (results.Count < count && flankIdx < flankSampleFractions.Length)
            {
                if (TryGenerateFlankingPath(primary.Tile, destinationTile, primary.Path, existingPaths, flankSampleFractions[flankIdx], out var flankPath))
                {
                    results.Add((primary.Tile, flankPath, speed));
                    existingPaths.Add(flankPath);
                }
                flankIdx++;
            }

            // Fallback: staggered columns along primary route
            while (results.Count < count)
            {
                results.Add((primary.Tile, new List<PlanetTile>(primary.Path), speed));
            }

            return results.Count >= 2;
        }

        // 3. Fallback: Search for wilderness staging tile if no faction settlements exist
        if (TryResolveOrigin(parms, destinationTile, out var singleOrigin, out var singlePath, out var singleSpeed))
        {
            results.Add((singleOrigin, singlePath, singleSpeed));
            var fallbackPaths = new List<List<PlanetTile>> { singlePath };

            float[] flankSampleFractions = { 0.30f, 0.60f, 0.45f };
            int fIdx = 0;
            while (results.Count < count && fIdx < flankSampleFractions.Length)
            {
                if (TryGenerateFlankingPath(singleOrigin, destinationTile, singlePath, fallbackPaths, flankSampleFractions[fIdx], out var flankPath))
                {
                    results.Add((singleOrigin, flankPath, singleSpeed));
                    fallbackPaths.Add(flankPath);
                }
                fIdx++;
            }

            while (results.Count < count)
            {
                results.Add((singleOrigin, new List<PlanetTile>(singlePath), singleSpeed));
            }

            return results.Count >= 2;
        }

        return false;
    }

    private static bool TryGenerateFlankingPath(
        PlanetTile start,
        PlanetTile dest,
        List<PlanetTile> directPath,
        List<List<PlanetTile>> existingPaths,
        float sampleFraction,
        out List<PlanetTile> flankingPath)
    {
        flankingPath = new List<PlanetTile>();
        if (directPath == null || directPath.Count < 6 || !start.Valid || !dest.Valid)
        {
            return false;
        }

        var excludeSet = new HashSet<PlanetTile>();
        foreach (var p in existingPaths)
        {
            // Exclude intermediate corridor tiles to encourage distinct flanking routes
            for (int i = 1; i < p.Count - 1; i++)
            {
                excludeSet.Add(p[i]);
            }
        }

        var worldGrid = Find.WorldGrid;
        if (worldGrid == null)
        {
            return false;
        }

        int targetIdx = Math.Clamp((int)(directPath.Count * sampleFraction), 2, directPath.Count - 2);
        PlanetTile sampleTile = directPath[targetIdx];

        // Search for a passable tile 4 to 10 tiles away from sampleTile that is not in any existing route
        PlanetTile flankTile = PlanetTile.Invalid;
        sampleTile.Layer.Filler.FloodFill(
            sampleTile,
            t =>
            {
                if (!t.Valid || t.Layer != sampleTile.Layer) return false;
                Tile tileData = worldGrid[t];
                return tileData != null && !tileData.WaterCovered && tileData.PrimaryBiome != null && !tileData.PrimaryBiome.impassable;
            },
            (t, dist) =>
            {
                if (flankTile.Valid) return;
                if (dist >= 4 && dist <= 10 && !excludeSet.Contains(t))
                {
                    flankTile = t;
                }
            },
            150,
            null);

        if (!flankTile.Valid)
        {
            return false;
        }

        // Build path: start -> flankTile -> dest
        if (TryBuildPath(start, flankTile, out var leg1) && TryBuildPath(flankTile, dest, out var leg2))
        {
            var combined = new List<PlanetTile>(leg1);
            if (leg2.Count > 1)
            {
                // Skip the duplicate flankTile
                combined.AddRange(leg2.Skip(1));
            }
            flankingPath = combined;
            return true;
        }

        return false;
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
