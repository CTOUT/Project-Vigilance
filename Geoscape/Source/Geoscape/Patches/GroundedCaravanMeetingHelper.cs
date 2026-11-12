using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using ProjectVigilance.Geoscape.Sensory;
using ProjectVigilance.Geoscape.World;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Facilitates grounding abstract storyteller caravan incidents (CaravanMeeting, CaravanDemand)
/// into physical, persistent WorldObject_Incursion entities with realistic routes, inventory, and telemetry.
/// Employs Shroud Staging (spawning in unmonitored Fog of War) or Physical Anchor Dispatch (settlements/camps)
/// so entities never appear out of thin air on actively monitored tiles.
/// </summary>
public static class GroundedCaravanMeetingHelper
{
    public static bool TryInterceptMeeting(IncidentWorker __instance, IncidentParms parms, ref bool result)
    {
        if (parms?.target is not Caravan caravan || !caravan.Spawned || caravan.Faction?.IsPlayer != true)
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
        if (incursion != null)
        {
            // Already physically intersecting or adjacent: execute friendly meeting immediately
            IncursionInterceptionUtility.ExecuteFriendlyInterception(caravan, incursion);
            result = true;
            return false;
        }

        // 2. If no incursion is present, instantiate a persistent incursion traversing this sector,
        // staged in the Fog of War / shroud or at its origin settlement
        incursion = CreateGroundedIncursion(
            caravanTile,
            parms,
            groupKind: IncursionGroupKind.TradeCaravan,
            isHostile: false,
            out bool stagedAtDistance);

        if (incursion == null)
        {
            return true;
        }

        // 3. If staged at a distance, notify the player of approaching contact rather than modal interrupting!
        if (stagedAtDistance)
        {
            NotifyApproachingContact(caravan, incursion, isHostile: false);
        }
        else
        {
            IncursionInterceptionUtility.ExecuteFriendlyInterception(caravan, incursion);
        }

        result = true;
        return false;
    }

    public static bool TryInterceptDemand(IncidentWorker __instance, IncidentParms parms, ref bool result)
    {
        if (parms?.target is not Caravan caravan || !caravan.Spawned || caravan.Faction?.IsPlayer != true)
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
        if (incursion != null)
        {
            IncursionInterceptionUtility.ExecuteHostileDemand(caravan, incursion);
            result = true;
            return false;
        }

        // 2. Instantiate persistent hostile incursion staged in shroud or origin
        incursion = CreateGroundedIncursion(
            caravanTile,
            parms,
            groupKind: IncursionGroupKind.HostileWarband,
            isHostile: true,
            out bool stagedAtDistance);

        if (incursion == null)
        {
            return true;
        }

        if (stagedAtDistance)
        {
            NotifyApproachingContact(caravan, incursion, isHostile: true);
        }
        else
        {
            IncursionInterceptionUtility.ExecuteHostileDemand(caravan, incursion);
        }

        result = true;
        return false;
    }

    public static WorldObject_Incursion? FindNearbyMeetingIncursion(PlanetTile tile, bool isHostile)
    {
        if (Current.Game == null || Find.World == null) return null;

        var worldObjects = Find.WorldObjects?.AllWorldObjects;
        if (worldObjects == null) return null;

        Faction? playerFaction = Faction.OfPlayerSilentFail;
        if (playerFaction == null) return null;

        for (int i = 0; i < worldObjects.Count; i++)
        {
            if (worldObjects[i] is WorldObject_Incursion inc &&
                inc.Spawned &&
                !inc.Destroyed &&
                !inc.IsEngagedInTacticalMap)
            {
                bool matchesHostility = isHostile
                    ? (inc.Faction != null && inc.Faction.HostileTo(playerFaction))
                    : (inc.Faction != null && !inc.Faction.HostileTo(playerFaction));

                if (matchesHostility)
                {
                    if (inc.Tile == tile)
                    {
                        return inc;
                    }

                    if (tile.Valid && inc.Tile.Valid && Find.WorldGrid != null &&
                        Find.WorldGrid.IsNeighbor(inc.Tile, tile))
                    {
                        inc.Tile = tile;
                        return inc;
                    }
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
        return CreateGroundedIncursion(currentTile, parms, groupKind, isHostile, out _);
    }

    public static WorldObject_Incursion? CreateGroundedIncursion(
        PlanetTile currentTile,
        IncidentParms parms,
        IncursionGroupKind groupKind,
        bool isHostile,
        out bool stagedAtDistance)
    {
        stagedAtDistance = false;
        if (parms == null || Current.Game == null || Find.World == null) return null;

        Faction? faction = parms.faction;
        Faction? playerFaction = Faction.OfPlayerSilentFail;
        if (faction == null)
        {
            if (isHostile)
            {
                faction = Find.FactionManager?.AllFactions?
                    .Where(f => (playerFaction != null ? f.HostileTo(playerFaction) : f.def.permanentEnemy) && !f.def.hidden && !f.temporary &&
                                f.def.pawnGroupMakers != null && f.def.pawnGroupMakers.Any(pgm => pgm.kindDef == PawnGroupKindDefOf.Combat))
                    .RandomElementWithFallback()
                    ?? Find.FactionManager?.AllFactions?
                    .Where(f => (playerFaction != null ? f.HostileTo(playerFaction) : f.def.permanentEnemy) && !f.def.hidden && !f.temporary)
                    .RandomElementWithFallback();
            }
            else if (groupKind == IncursionGroupKind.TradeCaravan)
            {
                // Ensure the faction has overland trade capability (not an orbital-only faction like TradersGuild)
                faction = Find.FactionManager?.AllFactions?
                    .Where(f => (playerFaction == null || !f.HostileTo(playerFaction)) && !f.IsPlayer && !f.def.hidden && !f.temporary &&
                                f.def.humanlikeFaction &&
                                f.def.caravanTraderKinds != null && f.def.caravanTraderKinds.Count > 0 &&
                                f.def.pawnGroupMakers != null && f.def.pawnGroupMakers.Any(pgm => pgm.kindDef == PawnGroupKindDefOf.Trader))
                    .RandomElementWithFallback();
            }
            else
            {
                faction = Find.FactionManager?.AllFactions?
                    .Where(f => (playerFaction == null || !f.HostileTo(playerFaction)) && !f.IsPlayer && !f.def.hidden && !f.temporary &&
                                f.def.humanlikeFaction &&
                                f.def.pawnGroupMakers != null && f.def.pawnGroupMakers.Any(pgm => pgm.kindDef == PawnGroupKindDefOf.Peaceful))
                    .RandomElementWithFallback();
            }
        }

        // If an orbital or non-caravaning faction was supplied for a trade caravan encounter,
        // substitute an eligible terrestrial trading faction or downgrade to a visitor group
        if (!isHostile && groupKind == IncursionGroupKind.TradeCaravan && faction != null)
        {
            bool hasTradeCapability = faction.def.caravanTraderKinds != null && faction.def.caravanTraderKinds.Count > 0 &&
                                     faction.def.pawnGroupMakers != null && faction.def.pawnGroupMakers.Any(pgm => pgm.kindDef == PawnGroupKindDefOf.Trader);
            if (!hasTradeCapability)
            {
                Faction? validTradeFaction = Find.FactionManager?.AllFactions?
                    .Where(f => (playerFaction == null || !f.HostileTo(playerFaction)) && !f.IsPlayer && !f.def.hidden && !f.temporary &&
                                f.def.humanlikeFaction &&
                                f.def.caravanTraderKinds != null && f.def.caravanTraderKinds.Count > 0 &&
                                f.def.pawnGroupMakers != null && f.def.pawnGroupMakers.Any(pgm => pgm.kindDef == PawnGroupKindDefOf.Trader))
                    .RandomElementWithFallback();

                if (validTradeFaction != null)
                {
                    faction = validTradeFaction;
                }
                else
                {
                    groupKind = IncursionGroupKind.Visitors;
                }
            }
        }

        if (faction == null) return null;
        parms.faction = faction;

        if (groupKind == IncursionGroupKind.TradeCaravan)
        {
            if (parms.traderKind == null && faction.def?.caravanTraderKinds != null && faction.def.caravanTraderKinds.Count > 0)
            {
                parms.traderKind = faction.def.caravanTraderKinds.RandomElement();
            }
            if (parms.traderKind == null)
            {
                parms.traderKind = DefDatabase<TraderKindDef>.GetNamedSilentFail("Caravan_Outlander_BulkGoods")
                    ?? DefDatabase<TraderKindDef>.AllDefsListForReading.FirstOrDefault(tk => !tk.orbital);
            }
            if (parms.points <= 0f)
            {
                parms.points = Math.Max(650f, StorytellerUtility.DefaultThreatPointsNow(Find.World));
            }
        }
        else if (isHostile)
        {
            if (parms.points <= 0f)
            {
                parms.points = Math.Max(400f, StorytellerUtility.DefaultThreatPointsNow(Find.World));
            }
        }

        string defName = (!isHostile && groupKind == IncursionGroupKind.TradeCaravan)
            ? "Vigilance_Caravan"
            : "Vigilance_Incursion";

        var def = DefDatabase<WorldObjectDef>.GetNamedSilentFail(defName) ??
                  DefDatabase<WorldObjectDef>.GetNamedSilentFail("Vigilance_Incursion");
        if (def == null) return null;

        PlanetTile originTile = currentTile;
        PlanetTile destinationTile = currentTile;
        List<PlanetTile> routePath = new() { currentTile };
        float speed = IncursionOriginResolver.GetSpeedForFaction(faction);

        var factionSettlements = Find.WorldObjects?.Settlements?
            .Where(s => s.Faction == faction && s.Tile.Valid && s.Tile.Layer == currentTile.Layer)
            .OrderBy(s => Find.WorldGrid.ApproxDistanceInTiles(s.Tile, currentTile))
            .ToList();

        if (factionSettlements != null && factionSettlements.Count > 0)
        {
            var homeBase = factionSettlements.FirstOrDefault(s => s.Tile != currentTile) ?? factionSettlements[0];
            originTile = homeBase.Tile;
            destinationTile = Find.AnyPlayerHomeMap?.Tile ?? currentTile;

            if (destinationTile == currentTile || destinationTile == originTile)
            {
                var otherBases = factionSettlements.Where(s => s.Tile != originTile && s.Tile != currentTile).ToList();
                if (otherBases.Count > 0)
                {
                    destinationTile = otherBases.RandomElement().Tile;
                }
                else
                {
                    var anyOtherSettlement = Find.WorldObjects?.Settlements?
                        .FirstOrDefault(s => s.Tile != originTile && s.Tile != currentTile && s.Tile.Layer == currentTile.Layer);
                    if (anyOtherSettlement != null)
                    {
                        destinationTile = anyOtherSettlement.Tile;
                    }
                }
            }

            // Build two contiguous legs passing through currentTile
            List<PlanetTile> pathIn = new();
            List<PlanetTile> pathOut = new();
            bool hasPathIn = IncursionOriginResolver.TryBuildPath(originTile, currentTile, out pathIn);
            bool hasPathOut = destinationTile.Valid && destinationTile != currentTile &&
                              IncursionOriginResolver.TryBuildPath(currentTile, destinationTile, out pathOut);

            if (hasPathIn && hasPathOut)
            {
                routePath = new List<PlanetTile>(pathIn.Count + pathOut.Count - 1);
                routePath.AddRange(pathIn);
                for (int i = 1; i < pathOut.Count; i++)
                {
                    routePath.Add(pathOut[i]);
                }
            }
            else if (hasPathOut)
            {
                routePath = pathOut;
                originTile = currentTile;
            }
            else if (hasPathIn)
            {
                routePath = pathIn;
                destinationTile = currentTile;
            }
        }

        if (!routePath.Contains(currentTile))
        {
            routePath.Add(currentTile);
        }

        int pathIdx = routePath.IndexOf(currentTile);
        int spawnIdx = pathIdx;

        // Determine staging node along the inbound leg towards currentTile
        if (pathIdx > 0)
        {
            int preferredDist = 5;
            int bestShroudIdx = -1;
            int bestDistDiff = int.MaxValue;

            var sensory = SensoryManager.Instance;
            bool isOmniscient = GeoscapeRules.VisibilityMode == VisibilityMode.Omniscient;

            for (int i = pathIdx - 1; i >= 0; i--)
            {
                PlanetTile node = routePath[i];
                int dist = pathIdx - i;

                if (!isOmniscient && sensory != null)
                {
                    // Check if node is in the dark shroud (not actively monitored)
                    bool isMonitored = sensory.HasActiveVision(node);
                    if (!isMonitored)
                    {
                        int diff = Math.Abs(dist - preferredDist);
                        if (diff < bestDistDiff)
                        {
                            bestDistDiff = diff;
                            bestShroudIdx = i;
                        }
                    }
                }
            }

            if (bestShroudIdx >= 0)
            {
                // Found an authentic shroud staging node along the highway!
                spawnIdx = bestShroudIdx;
                stagedAtDistance = true;
            }
            else if (isOmniscient || sensory != null)
            {
                // In Omniscient mode or complete radar coverage, no shroud tile exists.
                // Fall back to physical origin anchor: spawn at origin settlement (routePath[0])!
                spawnIdx = 0;
                stagedAtDistance = true;
            }
            else
            {
                // Headless test fallback without active sensory manager
                spawnIdx = Math.Max(0, pathIdx - 4);
                stagedAtDistance = (spawnIdx != pathIdx);
            }
        }

        PlanetTile spawnTile = routePath[spawnIdx];

        var incursion = (WorldObject_Incursion)WorldObjectMaker.MakeWorldObject(def);
        incursion.SetFaction(faction);
        incursion.SetExplicitGroupKind(groupKind);
        var incidentDef = isHostile ? IncidentDefOf.RaidEnemy : IncidentDefOf.TraderCaravanArrival;
        incursion.SetupIncursion(incidentDef, parms, originTile, destinationTile, routePath, speed);
        incursion.Tile = spawnTile;
        incursion.SetCurrentPathIndex(spawnIdx);

        var pawns = IncursionInterceptionUtility.GetOrGenerateIncursionPawns(incursion);
        incursion.SetCommittedPawns(pawns);

        Find.WorldObjects?.Add(incursion);
        ContactRegistry.Instance?.RegisterContact(incursion);

        return incursion;
    }

    private static void NotifyApproachingContact(Caravan caravan, WorldObject_Incursion incursion, bool isHostile)
    {
        int dist = (Find.WorldGrid != null && caravan.Tile.Valid && incursion.Tile.Valid)
            ? UnityEngine.Mathf.RoundToInt(Find.WorldGrid.ApproxDistanceInTiles(caravan.Tile, incursion.Tile))
            : 4;

        string groupDesc = incursion.GetIdentifiedGroupLabel();
        string factionName = incursion.Faction?.Name ?? (isHostile ? "hostile forces" : "peaceful travellers");
        string destLabel = GetTileDestinationLabel(incursion.DestinationTile);

        string messageText = isHostile
            ? $"Warning: {caravan.LabelCap} spotted a {groupDesc} from {factionName} {dist} hexes away advancing along the road!"
            : $"{caravan.LabelCap} spotted an approaching {groupDesc} from {factionName} {dist} hexes away en route to {destLabel}.";

        MessageTypeDef msgType = isHostile ? MessageTypeDefOf.ThreatSmall : MessageTypeDefOf.PositiveEvent;
        Messages.Message(messageText, incursion, msgType);
    }

    private static string GetTileDestinationLabel(PlanetTile destTile)
    {
        if (!destTile.Valid) return "their destination";

        if (GeoscapeRules.VisibilityMode != VisibilityMode.Omniscient &&
            SensoryManager.Instance != null &&
            !SensoryManager.Instance.IsExplored(destTile))
        {
            return "an uncharted destination";
        }

        var settlement = Find.WorldObjects?.SettlementAt(destTile);
        if (settlement != null) return settlement.LabelCap;
        if (Find.AnyPlayerHomeMap?.Tile == destTile) return "your colony";
        return $"tile {destTile.tileId}";
    }
}
