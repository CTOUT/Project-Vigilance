using LudeonTK;
using RimWorld;
using Verse;

namespace ProjectVigilance.Geoscape.Debug;

public static class DebugActionsVigilance
{
    private static bool TryGetDebugIncidentParms(IncidentCategoryDef category, float points, out IncidentParms parms)
    {
        Map map = Find.CurrentMap;
        if (map == null)
        {
            Messages.Message("No active player map found.", MessageTypeDefOf.RejectInput, false);
            parms = null!;
            return false;
        }

        parms = StorytellerUtility.DefaultParmsNow(category, map);
        if (points > 0f)
        {
            parms.points = points;
        }
        return true;
    }

    [DebugAction("Project Vigilance", "Spawn Walk-In Raid (500 pts)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnWalkInRaid500()
    {
        if (!TryGetDebugIncidentParms(IncidentCategoryDefOf.ThreatBig, 500f, out var parms)) return;
        parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
        IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
    }

    [DebugAction("Project Vigilance", "Spawn Walk-In Raid (1500 pts)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnWalkInRaid1500()
    {
        if (!TryGetDebugIncidentParms(IncidentCategoryDefOf.ThreatBig, 1500f, out var parms)) return;
        parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
        IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
    }

    [DebugAction("Project Vigilance", "Spawn Multi-Vector Raid (1500 pts)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnMultiVectorRaid1500()
    {
        if (!TryGetDebugIncidentParms(IncidentCategoryDefOf.ThreatBig, 1500f, out var parms)) return;
        parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkInGroups;
        IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
    }

    [DebugAction("Project Vigilance", "Spawn Multi-Vector Raid (3000 pts)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnMultiVectorRaid3000()
    {
        if (!TryGetDebugIncidentParms(IncidentCategoryDefOf.ThreatBig, 3000f, out var parms)) return;
        parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkInGroups;
        IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
    }

    [DebugAction("Project Vigilance", "Spawn Trade Caravan", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnTradeCaravan()
    {
        if (!TryGetDebugIncidentParms(IncidentDefOf.TraderCaravanArrival.category, 0f, out var parms)) return;
        IncidentDefOf.TraderCaravanArrival.Worker.TryExecute(parms);
    }

    [DebugAction("Project Vigilance", "Spawn Visitor Group", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnVisitorGroup()
    {
        if (!TryGetDebugIncidentParms(IncidentDefOf.VisitorGroup.category, 0f, out var parms)) return;
        IncidentDefOf.VisitorGroup.Worker.TryExecute(parms);
    }
}
