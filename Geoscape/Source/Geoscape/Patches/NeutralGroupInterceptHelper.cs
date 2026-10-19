using HarmonyLib;
using ProjectVigilance.Geoscape.World;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.Patches;

public static class NeutralGroupInterceptHelper
{
    public static bool TryIntercept(IncidentWorker __instance, IncidentParms parms, ref bool __result)
    {
        // 1. If currently executing an arrival, allow normal execution
        if (IncursionArrivalManager.IsExecutingArrival)
        {
            return true;
        }

        // 2. Only intercept arrivals targeting a player-controlled home map
        if (parms.target is not Map map || !map.IsPlayerHome)
        {
            return true;
        }

        // 3. Pre-resolve faction and incident parameters if unset
        if (parms.faction == null)
        {
            bool resolved = Traverse.Create(__instance).Method("TryResolveParms", parms).GetValue<bool>();
            if (!resolved || parms.faction == null)
            {
                // Fall back to picking a candidate non-hostile faction directly if TryResolveParms failed on empty map
                if (__instance is IncidentWorker_NeutralGroup neutralWorker)
                {
                    parms.faction = Find.FactionManager.AllFactions.FirstOrDefault(f => neutralWorker.FactionCanBeGroupSource(f, parms, false));
                }

                if (parms.faction == null)
                {
                    return true;
                }
            }
        }

        if (parms.points <= 0f)
        {
            parms.points = StorytellerUtility.DefaultThreatPointsNow(map);
        }

        if (parms.traderKind == null && __instance is IncidentWorker_TraderCaravanArrival && parms.faction?.def?.caravanTraderKinds != null)
        {
            parms.traderKind = parms.faction.def.caravanTraderKinds.RandomElementWithFallback();
        }

        PlanetTile destinationTile = map.Tile;
        if (!IncursionOriginResolver.TryResolveOrigin(parms, destinationTile, out var originTile, out var path, out var speed))
        {
            // Graceful fallback to vanilla execution if overland route cannot be resolved
            return true;
        }

        // 4. Retrieve WorldObjectDef for friendly/neutral caravan
        var def = DefDatabase<WorldObjectDef>.GetNamedSilentFail("Vigilance_Caravan") ??
                  DefDatabase<WorldObjectDef>.GetNamedSilentFail("Vigilance_Incursion");

        if (def == null)
        {
            Log.Warning("[Project Vigilance] Could not find WorldObjectDef 'Vigilance_Caravan'. Falling back to vanilla arrival.");
            return true;
        }

        var caravan = (WorldObject_Incursion)WorldObjectMaker.MakeWorldObject(def);
        caravan.SetupIncursion(__instance.def, parms, originTile, destinationTile, path, speed);

        Find.WorldObjects.Add(caravan);

        int hexCount = path.Count;
        float etaHours = (hexCount / speed) * 24f;
        string factionName = parms.faction?.Name ?? "Unknown Faction";
        string incidentLabel = __instance.def?.label ?? "neutral group";

        Log.Message($"[Project Vigilance] Intercepted {incidentLabel} from {factionName}. Spawned caravan at tile {originTile} heading to tile {destinationTile} ({hexCount} hexes, ETA: ~{etaHours:F1} hours).");

        __result = true;
        return false;
    }
}
