using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using ProjectVigilance.Geoscape.World;

namespace ProjectVigilance.Geoscape.Patches;

[HarmonyPatch(typeof(CaravansBattlefield), "CheckWonBattle")]
public static class CaravansBattlefield_CheckWonBattle_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(CaravansBattlefield __instance, ref bool ___wonBattle)
    {
        if (___wonBattle)
        {
            return false;
        }

        Map map = __instance.Map;
        if (map == null)
        {
            return false;
        }

        // Check if there are still active hostiles on the battlefield
        if (GenHostility.AnyHostileActiveThreatToPlayer(map, countDormantPawnsAsHostile: false))
        {
            return false;
        }

        // Threat cleared from the map
        ___wonBattle = true;

        // Notify timed raids silently so the countdown begins/shows on the inspect pane without triggering a redundant warning letter
        TimedDetectionRaids? timedDetection = __instance.GetComponent<TimedDetectionRaids>();
        timedDetection?.SetNotifiedSilently();
        string detectionTimeLeft = (timedDetection != null && timedDetection.NextRaidCountdownActiveAndVisible)
            ? $"\n\nEnemy reinforcements will detect this position in {timedDetection.DetectionCountdownTimeLeftString}. Reform your caravan and depart before hostile reinforcements arrive."
            : string.Empty;

        // Assess the player's casualties and combatants on this map
        var allColonists = map.mapPawns.AllPawns
            .Where(p => p.Faction == Faction.OfPlayer && p.IsColonist)
            .ToList();

        int mobileColonists = allColonists.Count(p => !p.Dead && !p.Downed && p.Spawned);
        int downedColonists = allColonists.Count(p => p.Downed && p.Spawned);
        int deadColonists = (map.listerThings?.ThingsInGroup(ThingRequestGroup.Corpse)?
            .OfType<Corpse>()
            .Count(c => c.InnerPawn != null && c.InnerPawn.Faction == Faction.OfPlayer && c.InnerPawn.IsColonist) ?? 0)
            + map.mapPawns.AllPawns.Count(p => p.Dead && p.Faction == Faction.OfPlayer && p.IsColonist);

        var ambushComp = map.GetComponent<MapComponent_OverlandAmbush>();
        int kidnappedCount = ambushComp?.CapturedColonists?.Count ?? ambushComp?.SourceIncursion?.KidnappedColonists?.Count ?? 0;
        if (kidnappedCount == 0 && IncursionRetreatManager.Instance != null)
        {
            kidnappedCount = IncursionRetreatManager.GetIncarceratedColonistsAt(map.Tile).Count;
        }

        WorldObject_Incursion? retreatingIncursion = ambushComp?.SourceIncursion;
        bool hasPursuableKidnappers = kidnappedCount > 0 && retreatingIncursion != null && retreatingIncursion.Spawned;
        WorldObject letterWorldObject = hasPursuableKidnappers ? (WorldObject)retreatingIncursion! : (WorldObject)__instance;
        string worldButtonLabel = hasPursuableKidnappers ? "Track Kidnappers on Geoscape" : "Go to cell (Geoscape)";

        string contactName = ambushComp?.ContactName ?? "Hostile Forces";
        string factionName = ambushComp?.FactionName ?? "enemy forces";

        // Record vanilla tale
        if (allColonists.Count > 0)
        {
            TaleRecorder.RecordTale(TaleDefOf.CaravanAmbushDefeated, allColonists.FirstOrDefault(p => !p.Dead) ?? allColonists[0]);
        }

        // Case 1: TOTAL DEFEAT / SQUAD WIPED (Zero mobile colonists remain)
        if (mobileColonists == 0)
        {
            TaggedString label = "Caravan Defeated: " + contactName;
            TaggedString text = $"Your expedition at tile {map.Tile} has been overwhelmed and defeated by {contactName} from {factionName}!\n\n" +
                                $"COMBAT REPORT:\n" +
                                (deadColonists > 0 ? $"• KIA: {deadColonists} colonist(s) killed in action.\n" : "") +
                                (kidnappedCount > 0 ? $"• ABDUCTED: {kidnappedCount} colonist(s) captured and carried away by raiders!\n" : "") +
                                (downedColonists > 0 ? $"• INCAPACITATED: {downedColonists} survivor(s) remain incapacitated on the field.\n" : "") +
                                $"\nCRITICAL SITUATION:\n" +
                                (downedColonists > 0
                                    ? "There are no conscious combatants left to tend wounds or reform the caravan. Surviving colonists will bleed out without immediate rescue from an external caravan or colony reinforcement!"
                                    : "All combatants on this expedition have been lost.");

            LetterDef threatDef = PV_LetterDefOf.PV_TacticalEncounter_Threat ?? LetterDefOf.ThreatBig;
            var defeatLetter = (ChoiceLetter_TacticalEncounter)LetterMaker.MakeLetter(threatDef);
            defeatLetter.label = label;
            defeatLetter.text = text;
            defeatLetter.battleMap = map;
            defeatLetter.targetWorldObject = letterWorldObject;
            defeatLetter.targetTile = map.Tile;
            defeatLetter.battleButtonLabel = "Go to Fallen Squad";
            defeatLetter.worldButtonLabel = worldButtonLabel;
            defeatLetter.canReformCaravan = false; // Cannot reform caravan when all are downed/dead
            defeatLetter.lookTargets = new LookTargets(allColonists.Where(p => p.Spawned));

            Find.LetterStack.ReceiveLetter(defeatLetter);
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            return false;
        }

        // Case 2: COSTLY / PYRRHIC VICTORY (Colonists died, kidnapped, or downed)
        if (deadColonists > 0 || kidnappedCount > 0 || downedColonists > 0)
        {
            TaggedString label = "Costly Victory: " + contactName;
            TaggedString text = $"The engagement against {contactName} from {factionName} has ended, but your expedition suffered severe casualties.\n\n" +
                                $"CASUALTY REPORT:\n" +
                                $"• Conscious: {mobileColonists} colonist(s) standing.\n" +
                                (downedColonists > 0 ? $"• Incapacitated: {downedColonists} wounded colonist(s) needing medical treatment.\n" : "") +
                                (deadColonists > 0 ? $"• KIA: {deadColonists} colonist(s) killed in action.\n" : "") +
                                (kidnappedCount > 0 ? $"• ABDUCTED: {kidnappedCount} colonist(s) captured by retreating raiders! They can be pursued on the Geoscape.\n" : "") +
                                $"\nAdvice: Stabilize wounded survivors, retrieve fallen gear, or prepare a rescue pursuit before reforming the caravan." +
                                detectionTimeLeft;

            LetterDef neutralDef = PV_LetterDefOf.PV_TacticalEncounter_Neutral ?? LetterDefOf.NeutralEvent;
            var costlyLetter = (ChoiceLetter_TacticalEncounter)LetterMaker.MakeLetter(neutralDef);
            costlyLetter.label = label;
            costlyLetter.text = text;
            costlyLetter.battleMap = map;
            costlyLetter.targetWorldObject = letterWorldObject;
            costlyLetter.targetTile = map.Tile;
            costlyLetter.canReformCaravan = true;
            costlyLetter.battleButtonLabel = "Go to Battlescape";
            costlyLetter.worldButtonLabel = worldButtonLabel;
            costlyLetter.lookTargets = new LookTargets(allColonists.Where(p => p.Spawned));

            Find.LetterStack.ReceiveLetter(costlyLetter);
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            return false;
        }

        // Case 3: DECISIVE VICTORY (Flawless / Clean Victory)
        {
            TaggedString label = "Caravan Battle Won: " + contactName;
            TaggedString text = $"Your forces have decisively defeated {contactName} from {factionName} with zero friendly casualties!\n\n" +
                                $"The sector is completely secure. You can now scavenge battlefield equipment, tend to any minor injuries, and reform your caravan when ready." +
                                detectionTimeLeft;

            LetterDef posDef = PV_LetterDefOf.PV_TacticalEncounter_Positive ?? LetterDefOf.PositiveEvent;
            var victoryLetter = (ChoiceLetter_TacticalEncounter)LetterMaker.MakeLetter(posDef);
            victoryLetter.label = label;
            victoryLetter.text = text;
            victoryLetter.battleMap = map;
            victoryLetter.targetWorldObject = __instance;
            victoryLetter.targetTile = map.Tile;
            victoryLetter.canReformCaravan = true;
            victoryLetter.battleButtonLabel = "Go to Battlescape";
            victoryLetter.worldButtonLabel = "Go to cell (Geoscape)";
            victoryLetter.lookTargets = new LookTargets(allColonists.Where(p => p.Spawned));

            Find.LetterStack.ReceiveLetter(victoryLetter);
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            return false;
        }
    }
}
