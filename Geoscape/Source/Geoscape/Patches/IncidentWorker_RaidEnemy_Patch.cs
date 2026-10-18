using HarmonyLib;
using ProjectVigilance.Geoscape.World;
using RimWorld;
using RimWorld.Planet;
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

        if (isMultiGroup && IncursionOriginResolver.TryResolveMultipleOrigins(parms, destinationTile, 2, out var multiOrigins) && multiOrigins.Count >= 2)
        {
            var incursionDef = DefDatabase<WorldObjectDef>.GetNamedSilentFail("Vigilance_Incursion");
            if (incursionDef == null)
            {
                return true;
            }

            string fName = parms.faction?.Name ?? "Unknown Hostile";
            float splitPoints = parms.points / multiOrigins.Count;

            for (int i = 0; i < multiOrigins.Count; i++)
            {
                var route = multiOrigins[i];
                var groupParms = parms.ShallowCopy();
                groupParms.points = splitPoints;
                groupParms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;

                var inc = (WorldObject_Incursion)WorldObjectMaker.MakeWorldObject(incursionDef);
                inc.SetupIncursion(__instance.def, groupParms, route.Origin, destinationTile, route.Path, route.Speed);
                Find.WorldObjects.Add(inc);

                int hCount = route.Path.Count;
                float eta = (hCount / route.Speed) * 24f;
                Log.Message($"[Project Vigilance] Intercepted multi-vector raid from {fName} (Vector {i + 1}/{multiOrigins.Count}). Spawned incursion at tile {route.Origin} heading to tile {destinationTile} ({hCount} hexes, ETA: ~{eta:F1} hours).");
            }

            __result = true;
            return false;
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
