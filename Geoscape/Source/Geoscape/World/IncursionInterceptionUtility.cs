using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.World;

public static class IncursionInterceptionUtility
{
    private static readonly HashSet<int> notifiedContactLostCaravans = new();

    public static void ExecuteInterception(Caravan caravan, WorldObject_Incursion incursion)
    {
        if (caravan == null || !caravan.Spawned || incursion == null || !incursion.Spawned)
        {
            return;
        }

        bool isHostile = incursion.Faction?.HostileTo(Faction.OfPlayer) ?? true;

        if (isHostile)
        {
            ExecuteHostileInterception(caravan, incursion);
        }
        else
        {
            ExecuteFriendlyInterception(caravan, incursion);
        }
    }

    private static void ExecuteHostileInterception(Caravan caravan, WorldObject_Incursion incursion)
    {
        Log.Message($"[Project Vigilance] Interception initiated: {caravan.LabelCap} intercepted hostile {incursion.Label} at tile {incursion.Tile}.");

        // Determine enemy combatants
        List<Pawn> enemies;
        if (incursion.RetreatingPawns != null && incursion.RetreatingPawns.Count > 0)
        {
            enemies = incursion.RetreatingPawns.Where(p => !p.Dead).ToList();
        }
        else
        {
            IncidentParms parms = incursion.IncidentParms ?? new IncidentParms
            {
                target = Find.World,
                points = 350f,
                faction = incursion.Faction
            };

            var groupParms = IncidentParmsUtility.GetDefaultPawnGroupMakerParms(PawnGroupKindDefOf.Combat, parms);
            groupParms.generateFightersOnly = true;
            enemies = PawnGroupMakerUtility.GeneratePawns(groupParms).ToList();
        }

        if (enemies.Count == 0)
        {
            Log.Warning("[Project Vigilance] Intercepted incursion generated 0 combatants. Disbanding incursion.");
            incursion.Destroy();
            return;
        }

        bool isNightAmbush = incursion.IsRestingAtNight;
        string contactName = incursion.LabelCap;
        string factionName = incursion.Faction?.Name ?? "enemy forces";
        var kidnappedColonists = incursion.KidnappedColonists != null
            ? incursion.KidnappedColonists.Where(p => !p.Dead).ToList()
            : new List<Pawn>();

        // Setup the local tactical battle map using RimWorld's caravan incident architecture
        Map battleMap = CaravanIncidentUtility.SetupCaravanAttackMap(caravan, enemies, sendLetterIfRelatedPawns: true);

        // Spawn any carried kidnapped colonists on the battle map near the enemy force
        if (kidnappedColonists.Count > 0 && battleMap != null)
        {
            IntVec3 enemyCenter = enemies.Count > 0 ? enemies[0].Position : battleMap.Center;
            foreach (var captive in kidnappedColonists)
            {
                if (captive != null && !captive.Spawned)
                {
                    CellFinder.TryFindRandomCellNear(enemyCenter, battleMap, 6, c => c.Walkable(battleMap) && !c.Fogged(battleMap), out IntVec3 spawnCell);
                    GenSpawn.Spawn(captive, spawnCell.IsValid ? spawnCell : enemyCenter, battleMap, Rot4.Random);
                }
            }
        }

        // Night Ambush dynamics: sleeping enemies and night-owl strike team advantages
        bool hasNightOwl = caravan.PawnsListForReading.Any(p => p.story?.traits?.HasTrait(DefDatabase<TraitDef>.GetNamedSilentFail("NightOwl")) == true);

        // Destroy the world incursion object now that physical engagement has transitioned to the local map
        incursion.Destroy();

        // Dispatch tactical battle alert letter
        TaggedString letterLabel;
        TaggedString letterText;
        LetterDef letterDef = LetterDefOf.ThreatBig;

        if (kidnappedColonists.Count > 0)
        {
            string captivesList = string.Join(", ", kidnappedColonists.Select(c => c.LabelShort));
            letterLabel = "Rescue Operation: " + contactName;
            letterText = $"{caravan.LabelCap} has intercepted {contactName} from {factionName}!\n\n" +
                         $"RESCUE MISSION: Captured colonist(s) ({captivesList}) are on the battlefield!\n\n" +
                         $"Defeat the hostile captors and recover your people before they can escape or cause further harm.";
        }
        else if (isNightAmbush)
        {
            letterLabel = "Tactical Ambush: " + contactName;
            letterText = $"{caravan.LabelCap} has intercepted {contactName} from {factionName} while they were camped overnight!\n\n" +
                         $"The enemy forces were caught resting with no sentries prepared. Your combatants have the tactical advantage of surprise.";
            if (hasNightOwl)
            {
                letterText += "\n\nNight-Owl combatants in your strike team lead the dark operation with elevated focus and stealth.";
            }
        }
        else
        {
            letterLabel = "Field Interception: " + contactName;
            letterText = $"{caravan.LabelCap} has intercepted {contactName} from {factionName} on the overland march!\n\n" +
                         $"Prepare your forces for a field engagement.";
        }

        Find.LetterStack.ReceiveLetter(letterLabel, letterText, letterDef, new LookTargets(caravan.PawnsListForReading));
    }

    private static void ExecuteFriendlyInterception(Caravan caravan, WorldObject_Incursion incursion)
    {
        string groupDesc = incursion.GetIdentifiedGroupLabel();
        string factionName = incursion.Faction?.Name ?? "peaceful travellers";

        if (incursion.GroupKind == IncursionGroupKind.TradeCaravan)
        {
            Pawn? negotiator = BestCaravanPawnUtility.FindBestNegotiator(caravan);
            if (negotiator != null && incursion.CanTradeNow)
            {
                Find.WindowStack.Add(new Dialog_Trade(negotiator, incursion));
                string meetMsg = $"{caravan.LabelCap} met the {groupDesc} from {factionName} and initiated roadside trading.";
                Messages.Message(meetMsg, new GlobalTargetInfo(caravan.Tile), MessageTypeDefOf.PositiveEvent, historical: true);
                Log.Message($"[Project Vigilance] Initiated roadside trade between {caravan.LabelCap} and {incursion.Label}.");
            }
            else
            {
                string meetMsg = $"{caravan.LabelCap} met the {groupDesc} from {factionName} on the road.";
                Messages.Message(meetMsg, new GlobalTargetInfo(caravan.Tile), MessageTypeDefOf.NeutralEvent, historical: true);
            }
        }
        else
        {
            string meetMsg = $"{caravan.LabelCap} encountered the {groupDesc} from {factionName} on the road. After exchanging news and greetings, both groups continued their journeys.";
            Messages.Message(meetMsg, new GlobalTargetInfo(caravan.Tile), MessageTypeDefOf.NeutralEvent, historical: true);
            Log.Message($"[Project Vigilance] Roadside meeting between {caravan.LabelCap} and {incursion.Label}.");
        }

        // Caravan halts briefly to avoid continuous re-triggers
        caravan.pather.StopDead();
    }

    public static void WarnAway(WorldObject_Incursion incursion, Caravan? warningCaravan = null)
    {
        if (incursion == null || !incursion.Spawned)
        {
            return;
        }

        string groupDesc = incursion.GetIdentifiedGroupLabel();
        string factionName = incursion.Faction?.Name ?? "peaceful travellers";
        string originDesc = incursion.OriginTile.Valid ? $"their origin at tile {incursion.OriginTile}" : "their home territory";

        incursion.ReverseCourse("warned of danger by player");

        // Faction goodwill improvement for saving them from danger
        if (incursion.Faction != null && !incursion.Faction.HostileTo(Faction.OfPlayer))
        {
            incursion.Faction.TryAffectGoodwillWith(Faction.OfPlayer, 5, canSendMessage: true, canSendHostilityLetter: false);
        }

        string title = "Caravan Warned Away";
        string text = $"You have warned the {groupDesc} from {factionName} of imminent danger in the region.\n\n" +
                      $"Thankful for the advance notice, their leaders have ordered the group to turn back and return towards {originDesc}.";

        Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.PositiveEvent, new GlobalTargetInfo(incursion.Tile));
        Log.Message($"[Project Vigilance] Player warned away {incursion.Label}. Incursion is reversing course.");
    }

    public static void CheckContactLost(Caravan caravan, WorldObject_Incursion incursion)
    {
        if (caravan == null || incursion == null)
        {
            return;
        }

        int caravanId = caravan.ID;
        var sensoryManager = Find.World?.GetComponent<SensoryManager>();
        bool hasVision = sensoryManager != null && sensoryManager.HasActiveVision(incursion.Tile);
        bool isDetected = GeoscapeMod.Settings.visibilityMode == VisibilityMode.Omniscient ||
                          incursion.CurrentIntelligenceTier >= IntelligenceTier.Contact ||
                          hasVision;

        if (!isDetected)
        {
            if (!notifiedContactLostCaravans.Contains(caravanId))
            {
                notifiedContactLostCaravans.Add(caravanId);
                PromptContactLost(caravan, incursion);
            }
        }
        else
        {
            // Reset state if contact is re-acquired
            notifiedContactLostCaravans.Remove(caravanId);
        }
    }

    private static void PromptContactLost(Caravan caravan, WorldObject_Incursion incursion)
    {
        // Pause the caravan so the player can deliberate
        caravan.pather.Paused = true;

        string contactName = incursion.LabelCap;
        PlanetTile lastTile = incursion.Tile;

        // Calculate food / provision safety telemetry
        float daysOfFood = DaysWorthOfFoodCalculator.ApproxDaysWorthOfFood(caravan);

        string foodText = daysOfFood > 0f
            ? $"Current caravan rations: {daysOfFood:F1} days."
            : "Warning: Caravan is low on or out of rations!";

        DiaNode root = new DiaNode(
            $"Sensors have lost contact with {contactName}! The target has slipped into an unmonitored sector.\n\n" +
            $"{foodText}\n\n" +
            $"Should {caravan.LabelCap} proceed to the Last Known Position at tile {lastTile}, or cancel the pursuit and return home?");

        DiaOption proceedOption = new DiaOption("Proceed to Last Known Position")
        {
            action = () =>
            {
                caravan.pather.Paused = false;
                Messages.Message($"{caravan.LabelCap} is continuing pursuit toward Last Known Position (Tile {lastTile}).", caravan, MessageTypeDefOf.NeutralEvent);
            },
            resolveTree = true
        };

        DiaOption returnOption = new DiaOption("Return Home")
        {
            action = () =>
            {
                caravan.pather.Paused = false;
                caravan.pather.StopDead();

                // Find nearest player settlement
                Settlement? nearestHome = Find.WorldObjects.Settlements
                    .Where(s => s.Faction == Faction.OfPlayer)
                    .OrderBy(s => Find.WorldGrid.ApproxDistanceInTiles(caravan.Tile, s.Tile))
                    .FirstOrDefault();

                if (nearestHome != null)
                {
                    caravan.pather.StartPath(nearestHome.Tile, null, true);
                    Messages.Message($"{caravan.LabelCap} is returning to {nearestHome.LabelCap}.", caravan, MessageTypeDefOf.NeutralEvent);
                }
            },
            resolveTree = true
        };

        root.options.Add(proceedOption);
        root.options.Add(returnOption);

        Find.WindowStack.Add(new Dialog_NodeTree(root, true, false, "Contact Lost: " + contactName));
    }
}
