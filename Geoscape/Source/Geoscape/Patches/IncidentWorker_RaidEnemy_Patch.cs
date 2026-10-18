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

        // 3. Drop pod raids bypass overland transit in this milestone
        if (parms.raidArrivalMode == PawnsArrivalModeDefOf.CenterDrop ||
            parms.raidArrivalMode == PawnsArrivalModeDefOf.EdgeDrop ||
            parms.raidArrivalMode == PawnsArrivalModeDefOf.RandomDrop)
        {
            return true;
        }

        PlanetTile destinationTile = map.Tile;
        if (!IncursionOriginResolver.TryResolveOrigin(parms, destinationTile, out var originTile, out var path, out var speed))
        {
            // Graceful fallback to vanilla execution if route cannot be resolved
            return true;
        }

        // 4. Retrieve WorldObjectDef and spawn incursion entity
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
