using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using ProjectVigilance.Geoscape.World;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Facilitates grounding abstract storyteller caravan incidents (CaravanMeeting, CaravanDemand)
/// into physical, persistent WorldObject_Incursion entities with realistic routes, inventory, and telemetry.
/// </summary>
public static class GroundedCaravanMeetingHelper
{
    public static bool TryInterceptMeeting(IncidentWorker __instance, IncidentParms parms, ref bool result)
    {
        if (parms?.target is not Caravan caravan || !caravan.Spawned || caravan.Faction != Faction.OfPlayer)
        {
            return true;
        }

        PlanetTile caravanTile = caravan.Tile;
        if (!caravanTile.Valid)
        {
            return true;
        }

        // 1. Look for an existing friendly/neutral incursion on or adjacent to this tile
        WorldObject_Incursion? incursion = FindNearbyMeetingIncursion(caravanTile, isHostile: false);

        // 2. If no incursion is present, instantiate a persistent incursion traversing this sector
        if (incursion == null)
        {
            incursion = CreateGroundedIncursion(
                caravanTile,
                parms,
                groupKind: IncursionGroupKind.TradeCaravan,
                isHostile: false);
        }

        if (incursion == null)
        {
            return true;
        }

        // 3. Execute grounded friendly meeting
        IncursionInterceptionUtility.ExecuteFriendlyInterception(caravan, incursion);

        result = true;
        return false;
    }

    public static bool TryInterceptDemand(IncidentWorker __instance, IncidentParms parms, ref bool result)
    {
        if (parms?.target is not Caravan caravan || !caravan.Spawned || caravan.Faction != Faction.OfPlayer)
        {
            return true;
        }

        PlanetTile caravanTile = caravan.Tile;
        if (!caravanTile.Valid)
        {
            return true;
        }

        // 1. Look for an existing hostile incursion on or adjacent to this tile
        WorldObject_Incursion? incursion = FindNearbyMeetingIncursion(caravanTile, isHostile: true);

        // 2. If no incursion is present, instantiate a persistent hostile incursion
        if (incursion == null)
        {
            incursion = CreateGroundedIncursion(
                caravanTile,
                parms,
                groupKind: IncursionGroupKind.HostileWarband,
                isHostile: true);
        }

        if (incursion == null)
        {
            return true;
        }

        // 3. Execute grounded hostile extortion demand
        IncursionInterceptionUtility.ExecuteHostileDemand(caravan, incursion);

        result = true;
        return false;
    }

    public static WorldObject_Incursion? FindNearbyMeetingIncursion(PlanetTile tile, bool isHostile)
    {
        var worldObjects = Find.WorldObjects?.AllWorldObjects;
        if (worldObjects == null) return null;

        for (int i = 0; i < worldObjects.Count; i++)
        {
            if (worldObjects[i] is WorldObject_Incursion inc &&
                inc.Spawned &&
                !inc.Destroyed &&
                !inc.IsEngagedInTacticalMap)
            {
                bool matchesHostility = isHostile
                    ? (inc.Faction != null && inc.Faction.HostileTo(Faction.OfPlayer))
                    : (inc.Faction == null || !inc.Faction.HostileTo(Faction.OfPlayer));

                if (matchesHostility && inc.Tile == tile)
                {
                    return inc;
                }
            }
        }

        return null;
    }

    public static WorldObject_Incursion? CreateGroundedIncursion(
        PlanetTile currentTile,
        IncidentParms parms,
        IncursionGroupKind groupKind,
        bool isHostile)
    {
        Faction? faction = parms.faction;
        if (faction == null)
        {
            if (isHostile)
            {
                faction = Find.FactionManager?.AllFactions?
                    .Where(f => f.HostileTo(Faction.OfPlayer) && !f.def.hidden)
                    .RandomElementWithFallback();
            }
            else
            {
                faction = Find.FactionManager?.AllFactions?
                    .Where(f => !f.HostileTo(Faction.OfPlayer) && !f.IsPlayer && !f.def.hidden)
                    .RandomElementWithFallback();
            }
        }

        if (faction == null) return null;

        string defName = (!isHostile && groupKind == IncursionGroupKind.TradeCaravan)
            ? "Vigilance_Caravan"
            : "Vigilance_Incursion";

        var def = DefDatabase<WorldObjectDef>.GetNamedSilentFail(defName) ??
                  DefDatabase<WorldObjectDef>.GetNamedSilentFail("Vigilance_Incursion");
        if (def == null) return null;

        PlanetTile originTile = currentTile;
        PlanetTile destinationTile = currentTile;
        List<PlanetTile> routePath = new() { currentTile };
        float speed = 24f;

        var factionSettlements = Find.WorldObjects?.Settlements?
            .Where(s => s.Faction == faction && s.Tile != currentTile)
            .ToList();

        if (factionSettlements != null && factionSettlements.Count > 0)
        {
            var homeBase = factionSettlements.RandomElement();
            if (isHostile)
            {
                originTile = homeBase.Tile;
                destinationTile = Find.AnyPlayerHomeMap?.Tile ?? currentTile;
            }
            else
            {
                originTile = homeBase.Tile;
                destinationTile = Find.AnyPlayerHomeMap?.Tile ?? currentTile;
            }

            if (IncursionOriginResolver.TryBuildPath(originTile, destinationTile, out var builtPath))
            {
                routePath = builtPath;
            }
        }

        if (!routePath.Contains(currentTile))
        {
            routePath.Insert(0, currentTile);
        }

        int pathIdx = routePath.IndexOf(currentTile);

        var incursion = (WorldObject_Incursion)WorldObjectMaker.MakeWorldObject(def);
        var incidentDef = isHostile ? IncidentDefOf.RaidEnemy : IncidentDefOf.TraderCaravanArrival;
        incursion.SetupIncursion(incidentDef, parms, originTile, destinationTile, routePath, speed);
        incursion.Tile = currentTile;
        incursion.SetCurrentPathIndex(pathIdx);

        var pawns = IncursionInterceptionUtility.GetOrGenerateIncursionPawns(incursion);
        incursion.SetCommittedPawns(pawns);

        Find.WorldObjects?.Add(incursion);
        ContactRegistry.Instance?.RegisterContact(incursion);

        return incursion;
    }
}
