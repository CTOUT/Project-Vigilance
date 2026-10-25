using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.World;

public static class IncursionTrackingHelper
{
    public static bool IsTrackedByAnyIncursion(Pawn pawn)
    {
        if (pawn == null)
        {
            return false;
        }

        // 1. Check active WorldObject_Incursion
        var worldObjects = Find.WorldObjects?.AllWorldObjects;
        if (worldObjects != null)
        {
            for (int i = 0; i < worldObjects.Count; i++)
            {
                if (worldObjects[i] is WorldObject_Incursion incursion && incursion.Spawned)
                {
                    if (incursion.CommittedPawns != null && incursion.CommittedPawns.Contains(pawn)) return true;
                    if (incursion.RetreatingPawns != null && incursion.RetreatingPawns.Contains(pawn)) return true;
                    if (incursion.KidnappedColonists != null && incursion.KidnappedColonists.Contains(pawn)) return true;
                }
            }
        }

        // 2. Check active maps with MapComponent_OverlandAmbush
        var maps = Find.Maps;
        if (maps != null)
        {
            for (int m = 0; m < maps.Count; m++)
            {
                var ambushComp = maps[m].GetComponent<MapComponent_OverlandAmbush>();
                if (ambushComp != null && ambushComp.HasTrackedIncursionFor(pawn)) return true;
            }
        }

        // 3. Check currently executing incursion arrival
        if (IncursionArrivalManager.IsExecutingArrival && IncursionArrivalManager.CurrentExecutingIncursion != null)
        {
            var cur = IncursionArrivalManager.CurrentExecutingIncursion;
            if (cur.CommittedPawns != null && cur.CommittedPawns.Contains(pawn)) return true;
        }

        return false;
    }

    public static bool TryGetTraderIncursionFor(Pawn pawn, out WorldObject_Incursion? incursion)
    {
        incursion = null;
        if (pawn == null || pawn.Dead || pawn.Destroyed)
        {
            return false;
        }

        // 1. Check active WorldObject_Incursion
        var worldObjects = Find.WorldObjects?.AllWorldObjects;
        if (worldObjects != null)
        {
            for (int i = 0; i < worldObjects.Count; i++)
            {
                if (worldObjects[i] is WorldObject_Incursion inc && inc.Spawned && inc.IsUnderlyingTradeCaravan)
                {
                    if (inc.Faction != null && inc.Faction.HostileTo(Faction.OfPlayer))
                    {
                        continue;
                    }

                    if (inc.TraderPawn == pawn ||
                        (inc.CommittedPawns != null && inc.CommittedPawns.Contains(pawn) && (pawn.kindDef?.trader == true || pawn.trader != null)))
                    {
                        incursion = inc;
                        return true;
                    }
                }
            }
        }

        // 2. Check tactical maps with MapComponent_OverlandAmbush
        var maps = Find.Maps;
        if (maps != null)
        {
            for (int m = 0; m < maps.Count; m++)
            {
                var ambushComp = maps[m].GetComponent<MapComponent_OverlandAmbush>();
                if (ambushComp?.Engagements != null)
                {
                    for (int e = 0; e < ambushComp.Engagements.Count; e++)
                    {
                        var rec = ambushComp.Engagements[e];
                        if (rec.incursion != null && rec.incursion.Spawned && rec.incursion.IsUnderlyingTradeCaravan)
                        {
                            if (rec.incursion.Faction != null && rec.incursion.Faction.HostileTo(Faction.OfPlayer))
                            {
                                continue;
                            }

                            if (rec.incursion.TraderPawn == pawn ||
                                (rec.trackedPawns != null && rec.trackedPawns.Contains(pawn) && (pawn.kindDef?.trader == true || pawn.trader != null)))
                            {
                                incursion = rec.incursion;
                                return true;
                            }
                        }
                    }
                }
            }
        }

        // 3. Fallback: check if pawn is in a non-hostile trader incursion committed pawns as a humanlike candidate
        if (worldObjects != null)
        {
            for (int i = 0; i < worldObjects.Count; i++)
            {
                if (worldObjects[i] is WorldObject_Incursion inc && inc.Spawned && inc.IsUnderlyingTradeCaravan)
                {
                    if (inc.Faction != null && inc.Faction.HostileTo(Faction.OfPlayer))
                    {
                        continue;
                    }

                    if (inc.CommittedPawns != null && inc.CommittedPawns.Contains(pawn) && pawn.RaceProps != null && pawn.RaceProps.Humanlike)
                    {
                        if (inc.TraderPawn == null || inc.TraderPawn.Dead || inc.TraderPawn.Destroyed)
                        {
                            inc.TraderPawn = pawn;
                        }
                        incursion = inc;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public static void EnsureTraderComponents(Pawn pawn, WorldObject_Incursion incursion)
    {
        if (pawn == null || incursion == null || !incursion.IsUnderlyingTradeCaravan) return;
        if (incursion.Faction != null && incursion.Faction.HostileTo(Faction.OfPlayer)) return;

        bool isTrader = (incursion.TraderPawn == pawn) ||
                        (pawn.kindDef != null && pawn.kindDef.trader) ||
                        (pawn.trader != null) ||
                        (incursion.TraderPawn == null && pawn.RaceProps != null && pawn.RaceProps.Humanlike);

        if (isTrader)
        {
            incursion.TraderPawn = pawn;
            pawn.mindState.wantsToTradeWithColony = true;
            if (pawn.trader == null)
            {
                pawn.trader = new Pawn_TraderTracker(pawn);
            }
            if (pawn.trader.traderKind == null && incursion.TraderKind != null)
            {
                pawn.trader.traderKind = incursion.TraderKind;
            }
        }
    }
}
