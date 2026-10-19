using System;
using System.Linq;
using HarmonyLib;
using ProjectVigilance.Geoscape.World;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Patches;

[HarmonyPatch(typeof(IncidentWorker_RaidEnemy), "TryExecuteWorker")]
public static class IncidentWorker_RaidEnemy_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(IncidentWorker_RaidEnemy __instance, IncidentParms parms, ref bool __result)
    {
        // 1. If currently executing an arrival, allow normal execution
        if (IncursionArrivalManager.IsExecutingArrival)
        {
            return true;
        }

        // 2. Only intercept raids targeting a player-controlled home map
        if (parms.target is not Map map || !map.IsPlayerHome)
        {
            return true;
        }

        // 3. If faction, strategy, or arrival mode are not yet resolved (e.g. from debug actions), resolve them now
        if (parms.faction == null)
        {
            bool resolvedFaction = Traverse.Create(__instance).Method("TryResolveRaidFaction", parms).GetValue<bool>();
            if (!resolvedFaction)
            {
                return true;
            }
        }

        if (parms.raidStrategy == null)
        {
            __instance.ResolveRaidStrategy(parms, PawnGroupKindDefOf.Combat);
        }

        if (parms.raidArrivalMode == null)
        {
            __instance.ResolveRaidArriveMode(parms);
        }

        // 4. Drop pod raids bypass overland transit in this milestone
        if (parms.raidArrivalMode == PawnsArrivalModeDefOf.CenterDrop ||
            parms.raidArrivalMode == PawnsArrivalModeDefOf.EdgeDrop ||
            parms.raidArrivalMode == PawnsArrivalModeDefOf.RandomDrop)
        {
            return true;
        }

        PlanetTile destinationTile = map.Tile;

        // 5. Check if multi-group arrival is requested (pincer attack across multiple vectors)
        bool isMultiGroup = parms.raidArrivalMode == PawnsArrivalModeDefOf.EdgeWalkInGroups ||
                            parms.raidArrivalMode == PawnsArrivalModeDefOf.EdgeWalkInDistributedGroups;

        if (isMultiGroup)
        {
            // Determine desired vector count dynamically based on raid points:
            // < 800 pts: 2 vectors (tactical pincer)
            // 800 - 2000 pts: 2 to 3 vectors
            // > 2000 pts: 2 to 4 vectors
            int desiredVectors = parms.points switch
            {
                < 800f => 2,
                < 2000f => Rand.RangeInclusive(2, 3),
                _ => Rand.RangeInclusive(2, 4)
            };

            if (IncursionOriginResolver.TryResolveMultipleOrigins(parms, destinationTile, desiredVectors, out var multiOrigins) && multiOrigins.Count >= 2)
            {
                var incursionDef = DefDatabase<WorldObjectDef>.GetNamedSilentFail("Vigilance_Incursion");
                if (incursionDef == null)
                {
                    return true;
                }

                string fName = parms.faction?.Name ?? "Unknown Hostile";
                float splitPoints = parms.points / multiOrigins.Count;

                // Calculate max transit ETA across routes to coordinate synchronized arrival
                float maxETA = multiOrigins.Max(r => (r.Path.Count / r.Speed) * 24f);

                for (int i = 0; i < multiOrigins.Count; i++)
                {
                    var route = multiOrigins[i];
                    var groupParms = parms.ShallowCopy();
                    groupParms.points = splitPoints;
                    groupParms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;

                    float travelHours = (route.Path.Count / route.Speed) * 24f;
                    float deltaHours = maxETA - travelHours;
                    int departureDelayTicks = 0;

                    // Synchronize arrival times: closer bases mobilize at base so forces converge simultaneously
                    if (GeoscapeMod.Settings.synchronizeMultiVectorRaids)
                    {
                        // Check if this column shares its final approach corridor with an earlier column
                        bool sharesCorridor = false;
                        for (int j = 0; j < i; j++)
                        {
                            var prevPath = multiOrigins[j].Path;
                            int checkCount = Math.Min(4, Math.Min(route.Path.Count, prevPath.Count));
                            int matchCount = 0;
                            for (int k = 1; k <= checkCount; k++)
                            {
                                if (route.Path[route.Path.Count - k] == prevPath[prevPath.Count - k])
                                {
                                    matchCount++;
                                }
                            }
                            if (matchCount >= 3)
                            {
                                sharesCorridor = true;
                                break;
                            }
                        }

                        // If flanking from separate compass quadrants, converge in simultaneous pincer assault (+/- 0.35h jitter)
                        // If sharing the same approach road corridor, stagger in waves (1.2h - 2.0h) to avoid conga-lining
                        float corridorStagger = sharesCorridor ? Rand.Range(1.2f, 2.0f) : 0f;
                        float pincerJitter = Rand.Range(-0.35f, 0.35f);

                        if (deltaHours > 0.05f && deltaHours <= IncursionOriginResolver.MaxCoordinationWindowHours)
                        {
                            float scheduledDelayHours = Mathf.Max(0f, deltaHours + pincerJitter + corridorStagger);
                            departureDelayTicks = (int)(scheduledDelayHours * 2500f);
                        }
                        else if (sharesCorridor)
                        {
                            departureDelayTicks = (int)(corridorStagger * 2500f);
                        }
                        else if (pincerJitter > 0.05f)
                        {
                            departureDelayTicks = (int)(pincerJitter * 2500f);
                        }
                    }

                    var inc = (WorldObject_Incursion)WorldObjectMaker.MakeWorldObject(incursionDef);
                    inc.SetupIncursion(__instance.def, groupParms, route.Origin, destinationTile, route.Path, route.Speed, departureDelayTicks);
                    Find.WorldObjects.Add(inc);

                    int hCount = route.Path.Count;
                    if (departureDelayTicks > 0)
                    {
                        float delayHours = departureDelayTicks / 2500f;
                        Log.Message($"[Project Vigilance] Intercepted multi-vector raid from {fName} (Vector {i + 1}/{multiOrigins.Count}). Mobilizing at tile {route.Origin} for {delayHours:F1}h before march to tile {destinationTile} ({hCount} hexes, Net ETA: ~{(travelHours + delayHours):F1}h).");
                    }
                    else
                    {
                        Log.Message($"[Project Vigilance] Intercepted multi-vector raid from {fName} (Vector {i + 1}/{multiOrigins.Count}). Marching immediately from tile {route.Origin} to tile {destinationTile} ({hCount} hexes, ETA: ~{travelHours:F1}h).");
                    }
                }

                __result = true;
                return false;
            }
        }

        // 6. Standard single-vector incursion (weighted settlement origin)
        if (!IncursionOriginResolver.TryResolveOrigin(parms, destinationTile, out var originTile, out var path, out var speed))
        {
            // Graceful fallback to vanilla execution if route cannot be resolved
            return true;
        }

        var def = DefDatabase<WorldObjectDef>.GetNamedSilentFail("Vigilance_Incursion");
        if (def == null)
        {
            Log.Warning("[Project Vigilance] Could not find WorldObjectDef 'Vigilance_Incursion'. Falling back to vanilla raid.");
            return true;
        }

        var incursion = (WorldObject_Incursion)WorldObjectMaker.MakeWorldObject(def);
        incursion.SetupIncursion(__instance.def, parms, originTile, destinationTile, path, speed);

        Find.WorldObjects.Add(incursion);

        int hexCount = path.Count;
        float etaHours = (hexCount / speed) * 24f;
        string factionName = parms.faction?.Name ?? "Unknown Hostile";

        Log.Message($"[Project Vigilance] Intercepted walk-in raid from {factionName}. Spawned incursion at tile {originTile} heading to tile {destinationTile} ({hexCount} hexes, ETA: ~{etaHours:F1} hours).");

        // Set incident as handled for the Storyteller
        __result = true;
        return false;
    }
}
