using LudeonTK;
using RimWorld;
using Verse;

namespace ProjectVigilance.Geoscape.Debug;

public static class DebugActionsVigilance
{
    [DebugAction("Project Vigilance", "Spawn Walk-In Raid (500 pts)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnWalkInRaid500()
    {
        Map map = Find.CurrentMap;
        if (map == null)
        {
            Messages.Message("No active player map found.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
        parms.points = 500f;
        parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
        IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
    }

    [DebugAction("Project Vigilance", "Spawn Walk-In Raid (1500 pts)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnWalkInRaid1500()
    {
        Map map = Find.CurrentMap;
        if (map == null)
        {
            Messages.Message("No active player map found.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
        parms.points = 1500f;
        parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
        IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
    }

    [DebugAction("Project Vigilance", "Spawn Multi-Vector Raid (1500 pts)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnMultiVectorRaid1500()
    {
        Map map = Find.CurrentMap;
        if (map == null)
        {
            Messages.Message("No active player map found.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
        parms.points = 1500f;
        parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkInGroups;
        IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
    }

    [DebugAction("Project Vigilance", "Spawn Multi-Vector Raid (3000 pts)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnMultiVectorRaid3000()
    {
        Map map = Find.CurrentMap;
        if (map == null)
        {
            Messages.Message("No active player map found.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
        parms.points = 3000f;
        parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkInGroups;
        IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
    }

    [DebugAction("Project Vigilance", "Spawn Trade Caravan", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnTradeCaravan()
    {
        Map map = Find.CurrentMap;
        if (map == null)
        {
            Messages.Message("No active player map found.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentDefOf.TraderCaravanArrival.category, map);
        IncidentDefOf.TraderCaravanArrival.Worker.TryExecute(parms);
    }

    [DebugAction("Project Vigilance", "Spawn Visitor Group", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnVisitorGroup()
    {
        Map map = Find.CurrentMap;
        if (map == null)
        {
            Messages.Message("No active player map found.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentDefOf.VisitorGroup.category, map);
        IncidentDefOf.VisitorGroup.Worker.TryExecute(parms);
    }
}
