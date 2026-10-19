using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.World;

public enum FlankQuadrant
{
    Any,
    Left,
    Right,
    Rear
}

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

            // Apply tactical ingress diversification if doctrine dictates flanking
            if (TryApplyIngressFlank(parms.faction, originTile, destinationTile, path, FlankQuadrant.Any, out var diversifiedPath))
            {
                path = diversifiedPath;
            }

            return true;
        }

        // 2. Fallback: Search for a passable wilderness tile ~15 to 30 hexes away
        if (TryFindWildernessStagingTile(destinationTile, out var wildernessTile))
        {
            if (TryBuildPath(wildernessTile, destinationTile, out var foundPath))
            {
                originTile = wildernessTile;
                path = foundPath;

                // Apply tactical ingress diversification if doctrine dictates flanking
                if (TryApplyIngressFlank(parms.faction, originTile, destinationTile, path, FlankQuadrant.Any, out var diversifiedPath))
                {
                    path = diversifiedPath;
                }

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
            FlankQuadrant[] pincerQuadrants = { FlankQuadrant.Left, FlankQuadrant.Right, FlankQuadrant.Rear };

            var primary = viableSettlements[0];
            List<PlanetTile> primaryPath = primary.Path;
            if (TryApplyIngressFlank(parms.faction, primary.Tile, destinationTile, primaryPath, FlankQuadrant.Any, out var primaryDiversified))
            {
                primaryPath = primaryDiversified;
            }
            results.Add((primary.Tile, primaryPath, speed));

            // Select all secondary bases whose travel times fall within the coordination window (<= 36h delta)
            var compatibleOtherBases = viableSettlements
                .Skip(1)
                .Where(s => Math.Abs(s.TravelHours - primary.TravelHours) <= MaxCoordinationWindowHours)
                .OrderBy(s => Math.Abs(s.TravelHours - primary.TravelHours))
                .ToList();

            int secQuadrantIdx = 0;
            foreach (var sec in compatibleOtherBases)
            {
                if (results.Count >= count)
                {
                    break;
                }

                List<PlanetTile> secPath = sec.Path;
                FlankQuadrant q = pincerQuadrants[secQuadrantIdx % pincerQuadrants.Length];
                secQuadrantIdx++;

                if (TryApplyIngressFlank(parms.faction, sec.Tile, destinationTile, secPath, q, out var secDiversified))
                {
                    secPath = secDiversified;
                }

                results.Add((sec.Tile, secPath, speed));
            }

            // If we still need more vectors (e.g. only 1 or 2 bases available),
            // generate tactical flanking columns originating from the primary base!
            int flankIdx = 0;
            while (results.Count < count && flankIdx < pincerQuadrants.Length)
            {
                FlankQuadrant q = pincerQuadrants[flankIdx];
                if (TryApplyIngressFlank(parms.faction, primary.Tile, destinationTile, primary.Path, q, out var flankPath))
                {
                    results.Add((primary.Tile, flankPath, speed));
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

            FlankQuadrant[] pincerQuadrants = { FlankQuadrant.Left, FlankQuadrant.Right, FlankQuadrant.Rear };
            int fIdx = 0;
            while (results.Count < count && fIdx < pincerQuadrants.Length)
            {
                FlankQuadrant q = pincerQuadrants[fIdx];
                if (TryApplyIngressFlank(parms.faction, singleOrigin, destinationTile, singlePath, q, out var flankPath))
                {
                    results.Add((singleOrigin, flankPath, singleSpeed));
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

    /// <summary>
    /// Calculates faction movement doctrine probability for off-road tactical flanking.
    /// Friendly caravans stick to highways (0%), Mechanoids march in direct lines (0%),
    /// Pirates/Outlanders occasionally flank (50%), and Tribals prefer wilderness cover (80%).
    /// </summary>
    public static float GetFlankProbability(Faction? faction)
    {
        if (faction == null)
        {
            return 0.5f;
        }

        // Friendly trade caravans and neutral visitors always stick to trade roads and highways
        if (!faction.HostileTo(Faction.OfPlayer))
        {
            return 0f;
        }

        // Mechanoids march in cold, uncompromising geodesic direct lines
        if (faction.def == FactionDefOf.Mechanoid || faction.def.defName.Contains("Mechanoid"))
        {
            return 0f;
        }

        // Tribal warbands prioritize wilderness cover, ridge lines, and stealth approaches
        if (faction.def.techLevel <= TechLevel.Neolithic)
        {
            return 0.80f;
        }

        // Pirates, Outlanders, and mercenaries occasionally flank or bypass road checkpoints
        return 0.50f;
    }

    /// <summary>
    /// Finds a passable, non-water land tile ~4 to 7 tiles away from the destination at an angular offset
    /// relative to the incoming approach vector, matching the requested tactical quadrant.
    /// </summary>
    public static bool TryFindFlankingWaypoint(
        PlanetTile destination,
        List<PlanetTile> directPath,
        int minRadius,
        int maxRadius,
        FlankQuadrant quadrant,
        out PlanetTile waypoint)
    {
        waypoint = PlanetTile.Invalid;
        var worldGrid = Find.WorldGrid;
        if (worldGrid == null || directPath == null || directPath.Count < 6 || !destination.Valid)
        {
            return false;
        }

        Vector3 destPos = worldGrid.GetTileCenter(destination);
        int sampleIdx = Math.Max(0, directPath.Count - 6);
        Vector3 approachPos = worldGrid.GetTileCenter(directPath[sampleIdx]);
        Vector3 incomingDir = (approachPos - destPos).normalized;

        if (incomingDir == Vector3.zero)
        {
            return false;
        }

        Vector3 normal = destPos.normalized;
        var candidates = new List<PlanetTile>();

        destination.Layer.Filler.FloodFill(
            destination,
            t =>
            {
                if (!t.Valid || t.Layer != destination.Layer) return false;
                Tile tileData = worldGrid[t];
                return tileData != null && !tileData.WaterCovered && tileData.PrimaryBiome != null && !tileData.PrimaryBiome.impassable;
            },
            (t, dist) =>
            {
                if (dist >= minRadius && dist <= maxRadius)
                {
                    Vector3 candPos = worldGrid.GetTileCenter(t);
                    Vector3 candDir = (candPos - destPos).normalized;
                    float angle = Vector3.Angle(incomingDir, candDir);

                    // Cross product with sphere surface normal gives signed direction (positive = left, negative = right)
                    float signedDir = Vector3.Dot(normal, Vector3.Cross(incomingDir, candDir));

                    bool matchesQuadrant = quadrant switch
                    {
                        FlankQuadrant.Left => signedDir > 0.05f && angle >= 35f && angle <= 125f,
                        FlankQuadrant.Right => signedDir < -0.05f && angle >= 35f && angle <= 125f,
                        FlankQuadrant.Rear => angle >= 125f,
                        _ => angle >= 40f && angle <= 135f
                    };

                    if (matchesQuadrant)
                    {
                        candidates.Add(t);
                    }
                }
            },
            maxRadius + 1,
            null);

        if (candidates.Count > 0)
        {
            waypoint = candidates.RandomElement();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Evaluates faction tactical doctrine and, if applicable, reroutes an incursion through an off-road
    /// approach waypoint ~4 to 7 hexes outside the destination colony to prevent predictable funnelling down roads.
    /// </summary>
    public static bool TryApplyIngressFlank(
        Faction? faction,
        PlanetTile origin,
        PlanetTile destination,
        List<PlanetTile> directPath,
        FlankQuadrant quadrant,
        out List<PlanetTile> diversifiedPath)
    {
        diversifiedPath = directPath;

        if (!GeoscapeMod.Settings.enableTacticalIngressFlanking)
        {
            return false;
        }

        if (directPath == null || directPath.Count < 8 || !origin.Valid || !destination.Valid)
        {
            return false;
        }

        float flankProbability = GetFlankProbability(faction);
        if (quadrant == FlankQuadrant.Any && Rand.Value >= flankProbability)
        {
            return false; // Direct approach chosen by dice roll
        }

        if (TryFindFlankingWaypoint(destination, directPath, 4, 7, quadrant, out PlanetTile waypoint))
        {
            if (TryBuildPath(origin, waypoint, out var leg1) && TryBuildPath(waypoint, destination, out var leg2))
            {
                // Verify the tactical detour is reasonable (at most 60% longer than direct path)
                if (leg1.Count + leg2.Count <= directPath.Count * 1.60f)
                {
                    var combined = new List<PlanetTile>(leg1.Count + leg2.Count);
                    combined.AddRange(leg1);
                    if (leg2.Count > 1)
                    {
                        for (int i = 1; i < leg2.Count; i++)
                        {
                            combined.Add(leg2[i]);
                        }
                    }
                    diversifiedPath = combined;
                    Log.Message($"[Project Vigilance] Tactical Ingress Diversification: Rerouted incursion ({faction?.Name ?? "Hostile"}, {quadrant}) through flanking waypoint {waypoint} ({combined.Count} hexes vs direct {directPath.Count} hexes).");
                    return true;
                }
            }
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
