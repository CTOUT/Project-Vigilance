using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.World;

public static class IncursionInterceptionUtility
{
    private static readonly HashSet<int> notifiedContactLostCaravans = new();
    private static readonly Dictionary<int, int> contactLostDebounceTicks = new();

    public static void ExecuteInterception(Caravan caravan, WorldObject_Incursion incursion, bool isAmbush = false)
    {
        if (caravan == null || !caravan.Spawned || incursion == null || !incursion.Spawned)
        {
            return;
        }

        bool isHostile = incursion.Faction?.HostileTo(Faction.OfPlayer) ?? true;

        if (isHostile)
        {
            ExecuteHostileInterception(caravan, incursion, isAmbush);
        }
        else
        {
            ExecuteFriendlyInterception(caravan, incursion);
        }
    }

    public static List<Pawn> GetOrGenerateIncursionPawns(WorldObject_Incursion incursion, Map? targetMap = null)
    {
        if (incursion.RetreatingPawns != null && incursion.RetreatingPawns.Count > 0)
        {
            var retreating = incursion.RetreatingPawns.Where(p => p != null && !p.Dead && !p.Destroyed).ToList();
            if (retreating.Count > 0)
            {
                return retreating;
            }
        }

        if (incursion.CommittedPawns != null && incursion.CommittedPawns.Count > 0)
        {
            var committed = incursion.CommittedPawns.Where(p => p != null && !p.Dead && !p.Destroyed).ToList();
            if (committed.Count > 0)
            {
                return committed;
            }
        }

        bool isTrader = incursion.IsUnderlyingTradeCaravan;
        bool isVisitorOrTraveler = incursion.IsUnderlyingVisitorOrTraveler;

        IIncidentTarget incidentTarget = (IIncidentTarget?)targetMap ?? Find.AnyPlayerHomeMap ?? (IIncidentTarget)Find.World;
        IncidentParms baseParms = incursion.IncidentParms != null
            ? incursion.IncidentParms.ShallowCopy()
            : new IncidentParms
            {
                target = incidentTarget,
                faction = incursion.Faction
            };

        if (baseParms.target == null)
        {
            baseParms.target = incidentTarget;
        }
        if (baseParms.faction == null)
        {
            baseParms.faction = incursion.Faction ?? (isTrader || isVisitorOrTraveler
                ? Find.FactionManager?.AllFactions?.FirstOrDefault(f => !f.HostileTo(Faction.OfPlayer) && !f.IsPlayer && !f.def.hidden)
                : Find.FactionManager?.AllFactions?.FirstOrDefault(f => f.HostileTo(Faction.OfPlayer) && !f.def.hidden));
            if (incursion.Faction == null && baseParms.faction != null)
            {
                incursion.SetFaction(baseParms.faction);
            }
        }

        PawnGroupKindDef groupKind = isTrader
            ? (DefDatabase<PawnGroupKindDef>.GetNamedSilentFail("Trader") ?? PawnGroupKindDefOf.Trader ?? PawnGroupKindDefOf.Combat)
            : (isVisitorOrTraveler
                ? (DefDatabase<PawnGroupKindDef>.GetNamedSilentFail("Peaceful") ?? PawnGroupKindDefOf.Combat)
                : PawnGroupKindDefOf.Combat);

        if (isTrader)
        {
            if (baseParms.points <= 0f)
            {
                baseParms.points = Mathf.Max(650f, StorytellerUtility.DefaultThreatPointsNow(incidentTarget));
            }
            if (baseParms.traderKind == null)
            {
                baseParms.traderKind = incursion.TraderKind ?? incursion.Faction?.def?.caravanTraderKinds?.FirstOrDefault();
            }
        }
        else if (isVisitorOrTraveler)
        {
            if (baseParms.points <= 0f)
            {
                baseParms.points = Mathf.Max(250f, StorytellerUtility.DefaultThreatPointsNow(incidentTarget) * 0.5f);
            }
        }
        else
        {
            if (baseParms.points <= 0f)
            {
                baseParms.points = Mathf.Max(350f, StorytellerUtility.DefaultThreatPointsNow(incidentTarget));
            }
        }

        var groupParms = IncidentParmsUtility.GetDefaultPawnGroupMakerParms(groupKind, baseParms, ensureCanGenerateAtLeastOnePawn: true);
        if (isTrader)
        {
            groupParms.generateFightersOnly = false;
            if (groupParms.traderKind == null)
            {
                groupParms.traderKind = baseParms.traderKind;
            }
        }
        else if (!isVisitorOrTraveler && groupKind == PawnGroupKindDefOf.Combat)
        {
            groupParms.generateFightersOnly = true;
        }

        bool hasMatchingGroupMaker = groupParms.faction?.def?.pawnGroupMakers != null &&
                                     groupParms.faction.def.pawnGroupMakers.Any(pgm => pgm.kindDef == groupKind);

        if (!hasMatchingGroupMaker && isTrader && groupParms.faction?.def?.pawnGroupMakers != null &&
            groupParms.faction.def.pawnGroupMakers.Any(pgm => pgm.kindDef == PawnGroupKindDefOf.Peaceful))
        {
            groupParms.groupKind = PawnGroupKindDefOf.Peaceful;
            hasMatchingGroupMaker = true;
        }

        List<Pawn> generated = new();
        if (hasMatchingGroupMaker)
        {
            generated = PawnGroupMakerUtility.GeneratePawns(groupParms, warnOnZeroResults: false).ToList();
        }

        // Fallback for solo visitors / low-point neutral groups or factions without group makers
        // so an overland incursion never generates 0 pawns
        if (generated.Count == 0 && (isVisitorOrTraveler || isTrader) && incursion.Faction != null)
        {
            PawnKindDef kindDef = incursion.Faction.RandomPawnKind() ?? PawnKindDefOf.Villager;
            Pawn fallbackPawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                kindDef,
                incursion.Faction,
                PawnGenerationContext.NonPlayer,
                tile: incursion.Tile));
            if (fallbackPawn != null)
            {
                generated.Add(fallbackPawn);
            }
        }

        if (generated.Count > 0)
        {
            incursion.SetCommittedPawns(generated);
        }

        return generated;
    }

    public static void ExecuteHostileInterception(Caravan caravan, WorldObject_Incursion incursion, bool isAmbush = false)
    {
        Log.Message($"[Project Vigilance] Interception initiated: {caravan.LabelCap} intercepted hostile {incursion.Label} at tile {incursion.Tile}. IsAmbush={isAmbush}");

        // Determine enemy combatants (preserving full caravan/pack animal/visitor composition)
        List<Pawn> enemies = GetOrGenerateIncursionPawns(incursion);

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

        if (incursion.Faction != null && !incursion.Faction.HostileTo(Faction.OfPlayer) && incursion.IsUnderlyingTradeCaravan)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                Pawn p = enemies[i];
                if (p != null)
                {
                    IncursionTrackingHelper.EnsureTraderComponents(p, incursion);
                }
            }
        }

        // Setup the local tactical battle map using RimWorld's caravan incident architecture
        Map battleMap = CaravanIncidentUtility.SetupCaravanAttackMap(caravan, enemies, sendLetterIfRelatedPawns: true);

        // Immediately lock incursion to tactical engagement so world ticks do not trigger duplicate transit
        incursion.MarkCaravanMet(caravan.ID);
        incursion.SetCaravanCooldown(caravan.ID, Find.TickManager.TicksGame + 15000);
        incursion.IsEngagedInTacticalMap = true;
        if (battleMap != null)
        {
            incursion.MarkMapMet(battleMap.Parent?.ID ?? battleMap.uniqueID);
            var ambushComp = battleMap.GetComponent<MapComponent_OverlandAmbush>();
            ambushComp?.RegisterIncursion(incursion, enemies, markAmbushSprung: false);
        }

        // Hostile overland encounters (ambushes, night camp raids, and field interceptions) start concealed
        // so enemies only engage once they detect player pawns within visual range (LOS) or hear gunfire / take damage.
        bool isUnawareAmbush = isAmbush || isNightAmbush || !incursion.IsUnderlyingTradeCaravan;

        if (battleMap != null && enemies.Count > 0)
        {
            // Strip any existing or stale Lord memberships from combatants before creating new encounter Lord
            for (int i = 0; i < enemies.Count; i++)
            {
                Pawn p = enemies[i];
                if (p != null)
                {
                    Lord? currentLord = p.GetLord();
                    if (currentLord != null)
                    {
                        currentLord.RemovePawn(p);
                    }
                }
            }

            Faction? enemyFaction = incursion.Faction ?? enemies[0].Faction;
            if (enemyFaction != null)
            {
                if (isUnawareAmbush)
                {
                    // Draft player colonists in place and set Hold Fire so they remain concealed until ordered or spotted
                    var spawnedColonists = battleMap.mapPawns.FreeColonistsSpawned;
                    IntVec3 playerCenter = battleMap.Center;
                    if (spawnedColonists.Count > 0)
                    {
                        int sumX = 0, sumZ = 0;
                        for (int p = 0; p < spawnedColonists.Count; p++)
                        {
                            Pawn col = spawnedColonists[p];
                            sumX += col.Position.x;
                            sumZ += col.Position.z;
                            if (col.drafter != null)
                            {
                                col.drafter.Drafted = true;
                                col.drafter.FireAtWill = false;
                            }
                        }
                        playerCenter = new IntVec3(sumX / spawnedColonists.Count, 0, sumZ / spawnedColonists.Count);
                    }

                    // Ensure enemy group did not spawn within immediate visual detection range of player colonists
                    EnsureEnemyGroupSeparatedFromPlayer(battleMap, enemies, spawnedColonists, playerCenter);

                    // Enemies are in transit marching across the map along a route that does not walk straight into player's spawn position
                    IntVec3 enemySpawn = enemies[0].Position;
                    IntVec3 exitSpot = FindAmbushTransitExitSpot(battleMap, enemySpawn, playerCenter);

                    LordJob travelJob = new LordJob_TravelAndExit(exitSpot);
                    Lord travelLord = LordMaker.MakeNewLord(enemyFaction, travelJob, battleMap, enemies);

                    incursion.IsEngagedInTacticalMap = true;
                    var ambushComp = battleMap.GetComponent<MapComponent_OverlandAmbush>();
                    ambushComp?.InitializeAmbush(enemyFaction, enemies, travelLord, contactName, factionName, incursion);
                }
                else
                {
                    // Active Skirmish (Field Interception) or Night Camp Ambush: aggressive combat
                    bool canHumanlikeSteal = enemyFaction.def?.humanlikeFaction ?? enemies.Any(p => p != null && p.RaceProps.Humanlike);
                    LordJob lordJob = new LordJob_AssaultColony(
                        enemyFaction,
                        canKidnap: true,
                        canTimeoutOrFlee: true,
                        sappers: false,
                        useAvoidGridSmart: false,
                        canSteal: canHumanlikeSteal);
                    LordMaker.MakeNewLord(enemyFaction, lordJob, battleMap, enemies);
                    Find.TickManager.Notify_GeneratedPotentiallyHostileMap();

                    // Maintain world incursion reference so retreat or casualties persist
                    incursion.IsEngagedInTacticalMap = true;
                    var ambushComp = battleMap.GetComponent<MapComponent_OverlandAmbush>();
                    ambushComp?.InitializeActiveCombat(enemyFaction, enemies, contactName, factionName, incursion);
                }
            }
        }

        // Onboard any other friendly/visitor caravans present on this world tile cleanly as independent cohorts
        if (battleMap != null)
        {
            // Only consider non-player visiting/allied caravans as guest cohorts (never treat player caravans as guests)
            var otherCaravans = Find.WorldObjects?.Caravans?
                .Where(c => c != null && c.Spawned && c.Tile == incursion.Tile && c != caravan && c.Faction != Faction.OfPlayer)
                .ToList();

            if (otherCaravans != null && otherCaravans.Count > 0)
            {
                var ambushComp = battleMap.GetComponent<MapComponent_OverlandAmbush>();
                IntVec3 playerGroupCenter = battleMap.mapPawns.FreeColonistsSpawned.FirstOrDefault()?.Position ?? battleMap.Center;

                for (int oc = 0; oc < otherCaravans.Count; oc++)
                {
                    Caravan other = otherCaravans[oc];
                    string otherLabel = other.LabelCap;
                    Faction otherFaction = other.Faction;

                    // Snapshot pawns to transfer (ignoring any player pawns that might have been carried)
                    var cohortPawns = other.PawnsListForReading.Where(p => p != null && p.Faction != Faction.OfPlayer).ToList();
                    if (cohortPawns.Count == 0)
                    {
                        other.Destroy();
                        continue;
                    }

                    // Remove pawns from world caravan container cleanly before spawning
                    for (int cp = 0; cp < cohortPawns.Count; cp++)
                    {
                        other.RemovePawn(cohortPawns[cp]);
                    }

                    // Destroy/clean empty world caravan so CheckAnyNonWorldPawns doesn't run on it
                    if (other.Spawned)
                    {
                        other.Destroy();
                    }

                    // Spawn cohort pawns separated from player group so they are not caught in crossfire
                    List<Pawn> spawnedCohort = new();
                    CellFinder.TryFindRandomCellNear(playerGroupCenter, battleMap, 15, c => c.Walkable(battleMap) && !c.Fogged(battleMap), out IntVec3 cohortCenter);
                    if (!cohortCenter.IsValid) cohortCenter = playerGroupCenter;

                    for (int cp = 0; cp < cohortPawns.Count; cp++)
                    {
                        Pawn p = cohortPawns[cp];
                        if (p != null && !p.Spawned)
                        {
                            CellFinder.TryFindRandomCellNear(cohortCenter, battleMap, 6, c => c.Walkable(battleMap) && !c.Fogged(battleMap), out IntVec3 spawnSpot);
                            GenSpawn.Spawn(p, spawnSpot.IsValid ? spawnSpot : cohortCenter, battleMap, Rot4.Random);
                            spawnedCohort.Add(p);
                        }
                    }

                    if (spawnedCohort.Count > 0)
                    {
                        StripExistingLords(spawnedCohort);

                        // Assign independent AI Lord: defend themselves / assist in battle
                        LordJob assistJob = new LordJob_DefendBase(otherFaction, cohortCenter, delayBeforeAssault: 0, attackWhenPlayerBecameEnemy: false);
                        LordMaker.MakeNewLord(otherFaction, assistJob, battleMap, spawnedCohort);

                        // Register cohort into overland ambush tracker
                        ambushComp?.RegisterGuestCohort(otherLabel, otherFaction, spawnedCohort);
                        Log.Message($"[Project Vigilance] Onboarded allied/visitor party '{otherLabel}' ({spawnedCohort.Count} pawns) into tactical encounter map as an independent cohort.");
                    }
                }
            }
        }

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

        // Pack any carried trade stock onto pack animals/carriers if this was a trade caravan
        if (incursion.IsUnderlyingTradeCaravan)
        {
            incursion.SyncCargoFromTradeStockToPawns(enemies);

            // Fallback: If no pawns were capable of carrying items (e.g. all dead or no inventory), place on ground
            if (incursion.TradeStock != null && incursion.TradeStock.Count > 0 && battleMap != null)
            {
                IntVec3 enemyPos = enemies.Count > 0 ? enemies[0].Position : battleMap.Center;
                var stockList = incursion.TradeStock.ToList();
                for (int s = 0; s < stockList.Count; s++)
                {
                    Thing item = stockList[s];
                    incursion.TradeStock.Remove(item);
                    GenPlace.TryPlaceThing(item, enemyPos, battleMap, ThingPlaceMode.Near);
                }
            }
        }

        // Night Ambush dynamics: sleeping enemies and night-owl strike team advantages
        bool hasNightOwl = caravan.PawnsListForReading.Any(p => p.story?.traits?.HasTrait(DefDatabase<TraitDef>.GetNamedSilentFail("NightOwl")) == true);

        // Dispatch tactical battle alert letter
        TaggedString letterLabel;
        TaggedString letterText;
        LetterDef letterDef = LetterDefOf.ThreatBig;

        if (isUnawareAmbush)
        {
            string captivesNote = (kidnappedColonists.Count > 0)
                ? $"\n\nRESCUE MISSION: Captured colonist(s) ({string.Join(", ", kidnappedColonists.Select(c => c.LabelShort))}) are in the enemy marching column! You have the element of surprise to free them before they are taken away."
                : string.Empty;

            letterLabel = kidnappedColonists.Count > 0 ? "Rescue Ambush: " + contactName : "Ambush Position: " + contactName;
            letterText = $"{caravan.LabelCap} was stationed in concealment at tile {caravan.Tile} when {contactName} from {factionName} entered your sector!\n\n" +
                         $"TACTICAL ADVANTAGE (SURPRISE):\n" +
                         $"The approaching enemy column is currently unaware of your presence and is marching through the area.\n\n" +
                         $"• Your forces have the element of surprise: choose your firing lines, set up crossfires, and open fire when ready.\n" +
                         $"• Your squad's Fire-at-Will has been set to Hold Fire so you can position in stealth without prematurely alerting enemies.\n" +
                         $"• The ambush will be sprung if an enemy gets clear line of sight to a colonist within 18 cells, or if your forces open fire or inflict damage.\n" +
                         $"• If outmatched, you can hold fire in concealment and allow the enemy column to pass through without engaging." +
                         captivesNote +
                         $"\n\nTip: Once hostile forces are defeated or pass through, switch to the World view to reform your caravan.";
            letterDef = LetterDefOf.PositiveEvent;
        }
        else if (kidnappedColonists.Count > 0)
        {
            string captivesList = string.Join(", ", kidnappedColonists.Select(c => c.LabelShort));
            letterLabel = "Rescue Operation: " + contactName;
            letterText = $"{caravan.LabelCap} has intercepted {contactName} from {factionName}!\n\n" +
                         $"RESCUE MISSION: Captured colonist(s) ({captivesList}) are on the battlefield!\n\n" +
                         $"Defeat the hostile captors and recover your people before they can escape or cause further harm.\n\n" +
                         $"Tip: Once the hostiles are defeated, switch to the World view and select this encounter site to reform your caravan.";
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
            letterText += "\n\nTip: Once hostile forces are defeated, switch to the World view and select this encounter site to reform your caravan.";
        }
        else if (incursion.IsUnderlyingTradeCaravan)
        {
            letterLabel = "Caravan Assault: " + contactName;
            letterText = $"{caravan.LabelCap} has initiated a combat assault against the trade caravan from {factionName}!\n\n" +
                         $"Their traders and guards are defending their pack animals and cargo. Defeat their forces to seize their caravan stock.\n\n" +
                         $"Tip: Once hostile forces are defeated, switch to the World view to reform your caravan and claim any seized cargo.";
        }
        else
        {
            letterLabel = "Field Interception: " + contactName;
            letterText = $"{caravan.LabelCap} has intercepted {contactName} from {factionName} on the overland march!\n\n" +
                         $"Prepare your forces for a field engagement.\n\n" +
                         $"Tip: Once hostile forces are defeated, switch to the World view and select this encounter site to reform your caravan.";
        }

        var playerPawns = battleMap?.mapPawns?.FreeColonistsSpawned?.ToList() ?? new List<Pawn>();
        Pawn? leadPawn = playerPawns.FirstOrDefault();

        // Seamless camera transition into battlescape
        if (leadPawn != null)
        {
            CameraJumper.TryJumpAndSelect(leadPawn);
        }
        else if (battleMap != null)
        {
            CameraJumper.TryJump(battleMap.Center, battleMap);
        }

        LetterDef def = (isUnawareAmbush || letterDef == LetterDefOf.PositiveEvent)
            ? (PV_LetterDefOf.PV_TacticalEncounter_Positive ?? LetterDefOf.PositiveEvent)
            : (PV_LetterDefOf.PV_TacticalEncounter_Threat ?? LetterDefOf.ThreatBig);

        var encounterLetter = (ChoiceLetter_TacticalEncounter)LetterMaker.MakeLetter(def);
        encounterLetter.label = letterLabel;
        encounterLetter.text = letterText;
        encounterLetter.battleMap = battleMap;
        encounterLetter.targetWorldObject = (WorldObject?)battleMap?.Parent ?? incursion;
        encounterLetter.targetTile = battleMap?.Tile ?? incursion.Tile;
        encounterLetter.battleButtonLabel = "Go to Battlescape";
        encounterLetter.worldButtonLabel = "Go to cell (Geoscape)";
        encounterLetter.lookTargets = new LookTargets(playerPawns);

        Find.LetterStack.ReceiveLetter(encounterLetter);
        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
    }

    public static void ExecuteHostileInterception(List<Caravan> caravans, WorldObject_Incursion incursion, bool isAmbush = false)
    {
        if (caravans == null || caravans.Count == 0 || incursion == null || !incursion.Spawned)
        {
            return;
        }

        if (caravans.Count == 1)
        {
            ExecuteHostileInterception(caravans[0], incursion, isAmbush);
            return;
        }

        if (!isAmbush)
        {
            Caravan bestCombatCaravan = caravans
                .OrderByDescending(c => c.PawnsListForReading.Count(p => p.IsColonist && !p.Downed))
                .First();

            for (int i = 0; i < caravans.Count; i++)
            {
                incursion.MarkCaravanMet(caravans[i].ID);
            }

            ExecuteHostileInterception(bestCombatCaravan, incursion, isAmbush: false);
            return;
        }

        string contactName = incursion.LabelCap;
        string factionName = incursion.Faction?.Name ?? "enemy forces";

        DiaNode root = new DiaNode(
            $"TACTICAL AMBUSH OPPORTUNITY\n\n" +
            $"{contactName} from {factionName} is traversing tile {incursion.Tile} unaware of your forces!\n\n" +
            $"Multiple player squads are stationed in this sector. Choose which squad will spring the ambush while other squads remain concealed:");

        for (int i = 0; i < caravans.Count; i++)
        {
            Caravan c = caravans[i];
            int fighters = c.PawnsListForReading.Count(p => p.IsColonist && !p.Downed);
            int animals = c.PawnsListForReading.Count(p => !p.IsColonist);
            string squadDesc = $"{c.LabelCap} ({fighters} combatant{(fighters == 1 ? "" : "s")}" +
                               (animals > 0 ? $", {animals} animal{(animals == 1 ? "" : "s")}" : "") + ")";

            DiaOption ambushOption = new DiaOption($"Deploy {squadDesc}")
            {
                action = () =>
                {
                    for (int j = 0; j < caravans.Count; j++)
                    {
                        incursion.MarkCaravanMet(caravans[j].ID);
                    }
                    ExecuteHostileInterception(c, incursion, isAmbush: true);
                },
                resolveTree = true
            };
            root.options.Add(ambushOption);
        }

        DiaOption holdFireAll = new DiaOption("Hold fire and stay concealed (All squads let them pass)")
        {
            action = () =>
            {
                int cd = Find.TickManager.TicksGame + 15000;
                for (int j = 0; j < caravans.Count; j++)
                {
                    incursion.MarkCaravanMet(caravans[j].ID);
                    incursion.SetCaravanCooldown(caravans[j].ID, cd);
                }
                Messages.Message($"All player squads held fire in concealment as {contactName} passed through sector {incursion.Tile}.", new GlobalTargetInfo(incursion.Tile), MessageTypeDefOf.NeutralEvent);
            },
            resolveTree = true
        };
        root.options.Add(holdFireAll);

        DiaOption examineSectorAmbush = new DiaOption($"Examine sector on Geoscape (Tile {incursion.Tile})")
        {
            action = () =>
            {
                if (incursion.Spawned)
                {
                    CameraJumper.TryJumpAndSelect(incursion);
                }
                else
                {
                    CameraJumper.TryJump(incursion.Tile);
                }
            },
            resolveTree = false
        };
        root.options.Add(examineSectorAmbush);

        Find.WindowStack.Add(new Dialog_NodeTree(root, delayInteractivity: true, radioMode: false, title: "Ambush Opportunity: " + contactName));
    }

    public static void ExecuteFriendlyInterception(List<Caravan> caravans, WorldObject_Incursion incursion)
    {
        if (caravans == null || caravans.Count == 0 || incursion == null || !incursion.Spawned)
        {
            return;
        }

        if (caravans.Count == 1)
        {
            ExecuteFriendlyInterception(caravans[0], incursion);
            return;
        }

        string groupDesc = incursion.GetIdentifiedGroupLabel();
        string factionName = incursion.Faction?.Name ?? "peaceful travellers";
        bool isTrader = incursion.GroupKind == IncursionGroupKind.TradeCaravan;

        DiaNode root = new DiaNode(
            $"The {groupDesc} from {factionName} has crossed paths with your forces at tile {incursion.Tile}.\n\n" +
            $"Multiple player expeditions are currently stationed in this sector. Choose which caravan should respond to their hail:\n\n" +
            (isTrader
                ? "Their trade representative awaits negotiation with your chosen party."
                : "Their scouts offer friendly greetings to your chosen party."));

        for (int i = 0; i < caravans.Count; i++)
        {
            Caravan c = caravans[i];
            Pawn? negotiator = BestCaravanPawnUtility.FindBestNegotiator(c);
            int social = negotiator?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
            int colonistCount = c.PawnsListForReading.Count(p => p.IsColonist);
            int animalCount = c.PawnsListForReading.Count(p => !p.IsColonist);

            string partyDesc = $"{c.LabelCap} ({colonistCount} colonist{(colonistCount == 1 ? "" : "s")}" +
                               (animalCount > 0 ? $", {animalCount} animal{(animalCount == 1 ? "" : "s")}" : "") +
                               $" | Negotiator: {negotiator?.LabelShort ?? "None"} [Social {social}])";

            DiaOption option = new DiaOption($"Meet with {partyDesc}")
            {
                action = () =>
                {
                    int cd = Find.TickManager.TicksGame + 15000;
                    for (int j = 0; j < caravans.Count; j++)
                    {
                        incursion.MarkCaravanMet(caravans[j].ID);
                        incursion.SetCaravanCooldown(caravans[j].ID, cd);
                    }
                    ExecuteFriendlyInterception(c, incursion);
                },
                resolveTree = true
            };
            root.options.Add(option);
        }

        DiaOption partWaysAll = new DiaOption("Part ways peacefully (All caravans continue)")
        {
            action = () =>
            {
                int cd = Find.TickManager.TicksGame + 15000;
                for (int j = 0; j < caravans.Count; j++)
                {
                    if (caravans[j].pather != null && caravans[j].pather.Paused)
                    {
                        caravans[j].pather.Paused = false;
                    }
                    incursion.MarkCaravanMet(caravans[j].ID);
                    incursion.SetCaravanCooldown(caravans[j].ID, cd);
                }
                string meetMsg = $"Your caravans and the {groupDesc} from {factionName} greeted each other and continued on their journey.";
                Messages.Message(meetMsg, new GlobalTargetInfo(incursion.Tile), MessageTypeDefOf.NeutralEvent, historical: true);
            },
            resolveTree = true
        };
        root.options.Add(partWaysAll);

        DiaOption examineSectorFriendlyAll = new DiaOption($"Examine sector on Geoscape (Tile {incursion.Tile})")
        {
            action = () =>
            {
                if (incursion.Spawned)
                {
                    CameraJumper.TryJumpAndSelect(incursion);
                }
                else
                {
                    CameraJumper.TryJump(incursion.Tile);
                }
            },
            resolveTree = false
        };
        root.options.Add(examineSectorFriendlyAll);

        Find.WindowStack.Add(new Dialog_NodeTree(root, delayInteractivity: true, radioMode: false, title: "Encounter: " + groupDesc));
    }

    public static void ExecuteFriendlyInterception(Caravan caravan, WorldObject_Incursion incursion)
    {
        incursion.MarkCaravanMet(caravan.ID);

        // Caravan halts while negotiating if moving
        if (caravan.pather != null && caravan.pather.Moving)
        {
            caravan.pather.Paused = true;
        }

        string groupDesc = incursion.GetIdentifiedGroupLabel();
        string factionName = incursion.Faction?.Name ?? "peaceful travellers";
        bool isTrader = incursion.GroupKind == IncursionGroupKind.TradeCaravan;

        DiaNode root = new DiaNode(
            $"{caravan.LabelCap} has crossed paths with the {groupDesc} from {factionName} at tile {caravan.Tile}.\n\n" +
            (isTrader
                ? "Their pack animals are laden with goods and their trade representative hails your caravan, inviting you to trade or share the road."
                : "They hail your caravan peacefully with friendly greetings and road information."));

        if (isTrader && incursion.CanTradeNow && incursion.TraderKind != null)
        {
            DiaOption tradeOption = new DiaOption("Trade with caravan")
            {
                action = () =>
                {
                    if (caravan.pather != null && caravan.pather.Paused)
                    {
                        caravan.pather.Paused = false;
                    }
                    incursion.SetCaravanCooldown(caravan.ID, Find.TickManager.TicksGame + 15000);
                    Pawn? negotiator = BestCaravanPawnUtility.FindBestNegotiator(caravan);
                    if (negotiator != null)
                    {
                        Find.WindowStack.Add(new Dialog_Trade(negotiator, incursion));
                        string meetMsg = $"{caravan.LabelCap} initiated roadside trading with {groupDesc} from {factionName}.";
                        Messages.Message(meetMsg, new GlobalTargetInfo(caravan.Tile), MessageTypeDefOf.PositiveEvent, historical: true);
                    }
                    else
                    {
                        Messages.Message("No capable negotiator found in caravan.", caravan, MessageTypeDefOf.RejectInput, historical: false);
                    }
                },
                resolveTree = true
            };
            root.options.Add(tradeOption);
        }

        // Hostile action option: Attack Caravan (with confirmation link)
        DiaNode confirmAttackNode = new DiaNode(
            $"Are you sure you want to attack the {groupDesc}? Attacking this peaceful caravan will immediately declare war with {factionName} (-100 goodwill)!");

        DiaOption confirmYes = new DiaOption("Confirm Attack (Declare War)")
        {
            action = () =>
            {
                if (caravan.pather != null && caravan.pather.Paused)
                {
                    caravan.pather.Paused = false;
                }
                if (incursion.Faction != null)
                {
                    Faction.OfPlayer.TryAffectGoodwillWith(
                        incursion.Faction,
                        -100,
                        canSendMessage: true,
                        canSendHostilityLetter: true,
                        HistoryEventDefOf.AttackedCaravan);
                }
                ExecuteHostileInterception(caravan, incursion, isAmbush: false);
            },
            resolveTree = true
        };

        DiaOption confirmNo = new DiaOption("Back")
        {
            link = root
        };

        confirmAttackNode.options.Add(confirmYes);
        confirmAttackNode.options.Add(confirmNo);

        DiaOption attackOption = new DiaOption("Attack caravan (Declare War)")
        {
            link = confirmAttackNode
        };
        root.options.Add(attackOption);

        // Peaceful pass option
        DiaOption moveOnOption = new DiaOption("Move on / Part ways")
        {
            action = () =>
            {
                if (caravan.pather != null && caravan.pather.Paused)
                {
                    caravan.pather.Paused = false;
                }
                incursion.SetCaravanCooldown(caravan.ID, Find.TickManager.TicksGame + 15000);
                string meetMsg = $"{caravan.LabelCap} greeted the {groupDesc} from {factionName} and both continued peacefully.";
                Messages.Message(meetMsg, new GlobalTargetInfo(caravan.Tile), MessageTypeDefOf.NeutralEvent, historical: true);
            },
            resolveTree = true
        };
        root.options.Add(moveOnOption);

        DiaOption examineSectorFriendlySingle = new DiaOption($"Examine sector on Geoscape (Tile {caravan.Tile})")
        {
            action = () =>
            {
                if (incursion.Spawned)
                {
                    CameraJumper.TryJumpAndSelect(incursion);
                }
                else
                {
                    CameraJumper.TryJump(caravan.Tile);
                }
            },
            resolveTree = false
        };
        root.options.Add(examineSectorFriendlySingle);

        var dialog = new Dialog_NodeTree(root, delayInteractivity: true, radioMode: false, title: "Encounter: " + groupDesc)
        {
            closeAction = () =>
            {
                if (caravan.pather != null && caravan.pather.Paused)
                {
                    caravan.pather.Paused = false;
                }
                incursion.SetCaravanCooldown(caravan.ID, Find.TickManager.TicksGame + 15000);
            }
        };

        Find.WindowStack.Add(dialog);
    }

    public static void ExecuteHostileDemand(Caravan caravan, WorldObject_Incursion incursion)
    {
        if (caravan == null || !caravan.Spawned || incursion == null || !incursion.Spawned)
        {
            return;
        }

        incursion.MarkCaravanMet(caravan.ID);

        if (caravan.pather != null && caravan.pather.Moving)
        {
            caravan.pather.Paused = true;
        }

        string incursionName = incursion.LabelCap;
        string factionName = incursion.Faction?.Name ?? "hostile forces";

        // Identify candidate goods from caravan inventory to demand
        List<Thing> allItems = CaravanInventoryUtility.AllInventoryItems(caravan);
        List<(Thing item, int count)> plannedDemands = new();
        float totalDemandValue = 0f;
        float targetValue = Mathf.Clamp(StorytellerUtility.DefaultThreatPointsNow(caravan) * 0.45f, 200f, 600f);

        // 1. Demand silver if available
        for (int i = 0; i < allItems.Count && totalDemandValue < targetValue; i++)
        {
            Thing t = allItems[i];
            if (t.def == ThingDefOf.Silver && t.stackCount > 0)
            {
                int countToTake = Mathf.Min(t.stackCount, (int)(targetValue / Mathf.Max(0.1f, t.MarketValue)));
                if (countToTake > 0)
                {
                    plannedDemands.Add((t, countToTake));
                    totalDemandValue += countToTake * t.MarketValue;
                }
            }
        }

        // 2. If demand value not met, demand packaged meals, medicine, or valuables
        if (totalDemandValue < targetValue)
        {
            for (int i = 0; i < allItems.Count && totalDemandValue < targetValue; i++)
            {
                Thing t = allItems[i];
                if (t != null && !t.Destroyed && t.stackCount > 0 &&
                    t.def != ThingDefOf.Silver &&
                    (t.def.IsNutritionGivingIngestible || t is Medicine || t.MarketValue > 20f))
                {
                    int countToTake = Mathf.Min(t.stackCount, Mathf.CeilToInt((targetValue - totalDemandValue) / Mathf.Max(1f, t.MarketValue)));
                    if (countToTake > 0)
                    {
                        plannedDemands.Add((t, countToTake));
                        totalDemandValue += countToTake * t.MarketValue;
                    }
                }
            }
        }

        // If caravan possesses no tribute, hostiles immediately ambush
        if (plannedDemands.Count == 0)
        {
            if (caravan.pather != null && caravan.pather.Paused)
            {
                caravan.pather.Paused = false;
            }
            Messages.Message($"{caravan.LabelCap} had no valuable supplies to satisfy {incursionName}; the raiders attack!", new GlobalTargetInfo(caravan.Tile), MessageTypeDefOf.ThreatSmall);
            ExecuteHostileInterception(caravan, incursion, isAmbush: true);
            return;
        }

        string demandSummary = string.Join(", ", plannedDemands.Select(d => $"{d.count}x {d.item.LabelNoCount}"));

        DiaNode root = new DiaNode(
            $"CARAVAN EXTORTION DEMAND\n\n" +
            $"{caravan.LabelCap} has been intercepted on the overland route by {incursionName} from {factionName} at sector {caravan.Tile}!\n\n" +
            $"Their commanders surround your caravan and demand tribute in exchange for safe passage:\n\n" +
            $"Demanded tribute: {demandSummary}\n\n" +
            $"Surrendering the tribute will satisfy them; they will take the spoils and depart along their route. If you refuse, they will engage your caravan immediately in tactical battle!");

        DiaOption payDemand = new DiaOption($"Surrender demanded tribute ({demandSummary})")
        {
            action = () =>
            {
                if (caravan.pather != null && caravan.pather.Paused)
                {
                    caravan.pather.Paused = false;
                }

                var stock = incursion.GetOrCreateTradeStock();
                for (int i = 0; i < plannedDemands.Count; i++)
                {
                    var (t, count) = plannedDemands[i];
                    if (t != null && !t.Destroyed && t.stackCount > 0)
                    {
                        int actualCount = Mathf.Min(t.stackCount, count);
                        Thing split = t.SplitOff(actualCount);
                        stock.TryAdd(split, canMergeWithExistingStacks: true);
                    }
                }

                incursion.SetCaravanCooldown(caravan.ID, Find.TickManager.TicksGame + 30000);
                incursion.ReverseCourse("satisfied with extorted caravan tribute");

                Messages.Message(
                    $"{caravan.LabelCap} surrendered {demandSummary} to {incursionName}. The hostiles departed along their route with the extorted plunder.",
                    new GlobalTargetInfo(caravan.Tile),
                    MessageTypeDefOf.NeutralEvent);
            },
            resolveTree = true
        };
        root.options.Add(payDemand);

        DiaOption fightOption = new DiaOption("Refuse demands and fight (Deploy to tactical battle)")
        {
            action = () =>
            {
                if (caravan.pather != null && caravan.pather.Paused)
                {
                    caravan.pather.Paused = false;
                }

                ExecuteHostileInterception(caravan, incursion, isAmbush: false);
            },
            resolveTree = true
        };
        root.options.Add(fightOption);

        DiaOption evadeOption = new DiaOption("Attempt to evade and flee across the sector")
        {
            action = () =>
            {
                if (caravan.pather != null && caravan.pather.Paused)
                {
                    caravan.pather.Paused = false;
                }

                bool success = Rand.Value < 0.40f;
                if (success)
                {
                    incursion.SetCaravanCooldown(caravan.ID, Find.TickManager.TicksGame + 15000);
                    Messages.Message(
                        $"{caravan.LabelCap} successfully slipped past the blockade and outran {incursionName}!",
                        new GlobalTargetInfo(caravan.Tile),
                        MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message(
                        $"{caravan.LabelCap} failed to evade {incursionName}; the hostiles open fire!",
                        new GlobalTargetInfo(caravan.Tile),
                        MessageTypeDefOf.ThreatSmall);
                    ExecuteHostileInterception(caravan, incursion, isAmbush: false);
                }
            },
            resolveTree = true
        };
        root.options.Add(evadeOption);

        var dialog = new Dialog_NodeTree(root, delayInteractivity: true, radioMode: false, title: "Demand: " + incursionName)
        {
            closeAction = () =>
            {
                if (caravan.pather != null && caravan.pather.Paused)
                {
                    caravan.pather.Paused = false;
                }
            }
        };

        Find.WindowStack.Add(dialog);
    }

    public static void WarnAway(WorldObject_Incursion incursion, Caravan? warningCaravan = null)
    {
        if (incursion == null || !incursion.Spawned)
        {
            return;
        }

        if (incursion.HasBeenWarnedAway || incursion.IsTurningBack || !incursion.HasImminentThreatNearby())
        {
            return;
        }

        string groupDesc = incursion.GetIdentifiedGroupLabel();
        string factionName = incursion.Faction?.Name ?? "peaceful travellers";
        Settlement? originSettlement = Find.WorldObjects?.SettlementAt(incursion.OriginTile);
        string originDesc = originSettlement != null
            ? originSettlement.LabelCap
            : (incursion.OriginTile.Valid ? $"their origin at tile {incursion.OriginTile.tileId}" : "their home territory");

        incursion.MarkWarnedAway();
        incursion.ReverseCourse("warned of danger by player");

        // Faction goodwill improvement for saving them from danger (awarded once)
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
                if (contactLostDebounceTicks.TryGetValue(caravanId, out int debounce) && Find.TickManager.TicksGame < debounce)
                {
                    return;
                }

                notifiedContactLostCaravans.Add(caravanId);
                contactLostDebounceTicks[caravanId] = Find.TickManager.TicksGame + 2500;
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
        // Pause the caravan so the player can deliberate if moving
        if (caravan.pather != null && caravan.pather.Moving)
        {
            caravan.pather.Paused = true;
        }

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
                if (caravan.pather != null && caravan.pather.Paused)
                {
                    caravan.pather.Paused = false;
                }
                Messages.Message($"{caravan.LabelCap} is continuing pursuit toward Last Known Position (Tile {lastTile}).", caravan, MessageTypeDefOf.NeutralEvent);
            },
            resolveTree = true
        };

        DiaOption returnOption = new DiaOption("Return Home")
        {
            action = () =>
            {
                if (caravan.pather != null)
                {
                    if (caravan.pather.Paused)
                    {
                        caravan.pather.Paused = false;
                    }
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
                }
            },
            resolveTree = true
        };

        root.options.Add(proceedOption);
        root.options.Add(returnOption);

        DiaOption examineLkp = new DiaOption($"Examine Last Known Position on Geoscape (Tile {lastTile})")
        {
            action = () =>
            {
                CameraJumper.TryJump(lastTile);
            },
            resolveTree = false
        };
        root.options.Add(examineLkp);

        Find.WindowStack.Add(new Dialog_NodeTree(root, true, false, "Contact Lost: " + contactName));
    }

    public static void ExecuteCampOrSettlementAssault(Map map, WorldObject_Incursion incursion)
    {
        if (map == null || incursion == null || !incursion.Spawned || incursion.IsRetreating)
        {
            return;
        }

        Log.Message($"[Project Vigilance] Incursion {incursion.TrackingId} ({incursion.Label}) assaulted player map at tile {incursion.Tile}. MapIsPlayerHome={map.IsPlayerHome}");

        // If it's a permanent home colony and is a native ThreatBig raid without pre-existing tactical pawns, attempt vanilla storyteller arrival first
        bool hasPreExistingPawns = (incursion.CommittedPawns != null && incursion.CommittedPawns.Count > 0) ||
                                   (incursion.RetreatingPawns != null && incursion.RetreatingPawns.Count > 0);
        if (!IncursionArrivalManager.IsExecutingArrival &&
            map.IsPlayerHome &&
            !hasPreExistingPawns &&
            incursion.IncidentDef != null &&
            incursion.IncidentDef.category == IncidentCategoryDefOf.ThreatBig &&
            incursion.IncidentParms != null)
        {
            incursion.DestinationTile = incursion.Tile;
            incursion.IncidentParms.target = map;
            if (IncursionArrivalManager.ExecuteArrival(incursion))
            {
                return;
            }
        }

        // Tactical Camp Assault / Fallback: Spawn hostile combatants directly at the map edge with LordJob_AssaultColony
        List<Pawn> enemies = GetOrGenerateIncursionPawns(incursion, map);

        if (enemies.Count == 0)
        {
            Log.Warning("[Project Vigilance] Camp/Settlement assault generated 0 enemies. Disbanding incursion.");
            incursion.Destroy();
            return;
        }

        // Find entry cell on the edge of the map
        if (!RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 spawnCenter, map, CellFinder.EdgeRoadChance_Hostile))
        {
            if (!CellFinder.TryFindRandomEdgeCellWith(c => c.Walkable(map) && !c.Fogged(map), map, CellFinder.EdgeRoadChance_Hostile, out spawnCenter))
            {
                CellFinder.TryFindRandomCellNear(map.Center, map, Mathf.Min(map.Size.x, map.Size.z) / 2, c => c.Walkable(map) && !c.Fogged(map), out spawnCenter);
            }
        }
        if (!spawnCenter.IsValid)
        {
            spawnCenter = map.Center;
        }

        for (int i = 0; i < enemies.Count; i++)
        {
            Pawn enemy = enemies[i];
            if (enemy == null) continue;
            if (incursion.Faction != null && !incursion.Faction.HostileTo(Faction.OfPlayer) && incursion.IsUnderlyingTradeCaravan)
            {
                IncursionTrackingHelper.EnsureTraderComponents(enemy, incursion);
            }
            if (enemy.Spawned) continue;
            CellFinder.TryFindRandomCellNear(spawnCenter, map, 4, c => c.Walkable(map) && !c.Fogged(map), out IntVec3 pCell);
            GenSpawn.Spawn(enemy, pCell.IsValid ? pCell : spawnCenter, map, Rot4.Random);
        }

        // Assign Assault Colony lord so enemies actively engage
        StripExistingLords(enemies);
        Faction? enemyFaction = incursion.Faction ?? enemies[0].Faction;
        if (enemyFaction != null)
        {
            bool canHumanlikeSteal = enemyFaction.def?.humanlikeFaction ?? enemies.Any(p => p != null && p.RaceProps.Humanlike);
            LordJob lordJob = new LordJob_AssaultColony(
                enemyFaction,
                canKidnap: true,
                canTimeoutOrFlee: true,
                sappers: false,
                useAvoidGridSmart: false,
                canSteal: canHumanlikeSteal);
            LordMaker.MakeNewLord(enemyFaction, lordJob, map, enemies);
        }

        Find.TickManager.Notify_GeneratedPotentiallyHostileMap();

        // Spawn any carried kidnapped colonists
        if (incursion.KidnappedColonists != null && incursion.KidnappedColonists.Count > 0)
        {
            foreach (var captive in incursion.KidnappedColonists)
            {
                if (captive != null && !captive.Spawned)
                {
                    CellFinder.TryFindRandomCellNear(spawnCenter, map, 5, c => c.Walkable(map) && !c.Fogged(map), out IntVec3 cCell);
                    GenSpawn.Spawn(captive, cCell.IsValid ? cCell : spawnCenter, map, Rot4.Random);
                }
            }
        }

        string contactName = incursion.LabelCap;
        string factionName = incursion.Faction?.Name ?? "enemy forces";
        string targetDesc = map.IsPlayerHome ? "settlement" : "camp";

        incursion.IsEngagedInTacticalMap = true;
        incursion.SetCommittedPawns(enemies);

        var ambushComp = map.GetComponent<MapComponent_OverlandAmbush>();
        ambushComp?.RegisterIncursion(incursion, enemies);

        bool isOngoingBattle = map.Parent is CaravansBattlefield || (ambushComp != null && ambushComp.AmbushSprung && ambushComp.Engagements.Count > 1);

        TaggedString letterLabel;
        TaggedString letterText;

        if (isOngoingBattle)
        {
            letterLabel = $"Overland Raid (Reinforcements): {contactName}";
            letterText = $"Hostile reinforcements from {factionName} ({contactName}) have entered the sector at tile {incursion.Tile}!\n\n" +
                         $"Additional hostile raiders have breached your perimeter and are joining the ongoing assault. Prepare your defenders!";
        }
        else
        {
            letterLabel = $"Overland Raid: {contactName}";
            letterText = $"Hostile forces from {factionName} ({contactName}) have marched directly into your {targetDesc} at tile {incursion.Tile}!\n\n" +
                         $"Hostile raiders have breached your perimeter and are launching an immediate assault. Prepare your defenders!";
        }

        // Auto-transition camera to the assaulted map
        CameraJumper.TryJump(spawnCenter, map);

        LetterDef raidDef = PV_LetterDefOf.PV_TacticalEncounter_Threat ?? LetterDefOf.ThreatBig;
        var raidLetter = (ChoiceLetter_TacticalEncounter)LetterMaker.MakeLetter(raidDef);
        raidLetter.label = letterLabel;
        raidLetter.text = letterText;
        raidLetter.battleMap = map;
        raidLetter.targetWorldObject = map.Parent ?? (WorldObject)incursion;
        raidLetter.targetTile = incursion.Tile;
        raidLetter.battleButtonLabel = "Go inside (Defenders)";
        raidLetter.worldButtonLabel = "Go to cell (Geoscape)";
        raidLetter.lookTargets = new LookTargets(enemies);

        Find.LetterStack.ReceiveLetter(raidLetter);
        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
    }

    public static void ExecuteFriendlyCampOrSettlementVisit(Map map, WorldObject_Incursion incursion)
    {
        if (map == null || incursion == null || !incursion.Spawned)
        {
            return;
        }

        if (map.IsPlayerHome && incursion.IncidentDef != null && incursion.IncidentParms != null)
        {
            incursion.DestinationTile = incursion.Tile;
            incursion.IncidentParms.target = map;
            if (IncursionArrivalManager.ExecuteArrival(incursion))
            {
                return;
            }
        }

        string groupDesc = incursion.GetIdentifiedGroupLabel();
        string factionName = incursion.Faction?.Name ?? "peaceful travellers";
        bool isTrader = incursion.GroupKind == IncursionGroupKind.TradeCaravan;
        string targetDesc = map.IsPlayerHome ? "settlement" : "camp";

        DiaNode root = new DiaNode(
            $"The {groupDesc} from {factionName} has arrived at your {targetDesc} at tile {incursion.Tile}.\n\n" +
            (isTrader
                ? "Their pack animals are laden with goods and their trade representative hails your camp, offering to trade goods."
                : "They hail your camp peacefully with friendly greetings and road information."));

        if (isTrader && incursion.CanTradeNow && incursion.TraderKind != null)
        {
            DiaOption tradeOption = new DiaOption("Trade with visitors")
            {
                action = () =>
                {
                    Pawn? negotiator = map.mapPawns?.FreeColonistsSpawned?
                        .Where(p => !p.Dead && !p.Downed && p.health.capacities.CapableOf(PawnCapacityDefOf.Talking))
                        .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0)
                        .FirstOrDefault();

                    if (negotiator != null)
                    {
                        Find.WindowStack.Add(new Dialog_Trade(negotiator, incursion));
                        string meetMsg = $"Initiated camp trading with {groupDesc} from {factionName}.";
                        Messages.Message(meetMsg, new GlobalTargetInfo(incursion.Tile), MessageTypeDefOf.PositiveEvent, historical: true);
                    }
                    else
                    {
                        Messages.Message("No capable negotiator found in camp.", new GlobalTargetInfo(incursion.Tile), MessageTypeDefOf.RejectInput, historical: false);
                    }
                },
                resolveTree = true
            };
            root.options.Add(tradeOption);
        }

        DiaOption leaveOption = new DiaOption("Permit them to pass")
        {
            resolveTree = true
        };
        root.options.Add(leaveOption);

        Find.WindowStack.Add(new Dialog_NodeTree(root, delayInteractivity: true, radioMode: true, title: $"{groupDesc} Visited {targetDesc.CapitalizeFirst()}"));
    }

    public static void ExecuteBattlescapeTransit(Map map, WorldObject_Incursion incursion)
    {
        if (map == null || incursion == null || !incursion.Spawned)
        {
            return;
        }

        Log.Message($"[Project Vigilance] Incursion {incursion.TrackingId} ({incursion.Label}) started Battlescape transit across map at tile {incursion.Tile}.");

        // 1. Determine directional vector from Geoscape
        ResolveTransitDirections(incursion, out Rot4 entryDir, out Rot4 exitDir);

        // 2. Find entry and exit cells on map edges
        if (!CellFinder.TryFindRandomEdgeCellWith(c => c.Walkable(map) && !c.Fogged(map), map, entryDir, CellFinder.EdgeRoadChance_Friendly, out IntVec3 entryCell))
        {
            if (!CellFinder.TryFindRandomEdgeCellWith(c => c.Walkable(map) && !c.Fogged(map), map, CellFinder.EdgeRoadChance_Friendly, out entryCell))
            {
                entryCell = map.Center;
            }
        }

        if (!CellFinder.TryFindRandomEdgeCellWith(c => c.Walkable(map) && !c.Fogged(map), map, exitDir, CellFinder.EdgeRoadChance_Friendly, out IntVec3 exitCell))
        {
            if (!CellFinder.TryFindRandomEdgeCellWith(c => c.Walkable(map) && !c.Fogged(map) && c.DistanceToSquared(entryCell) > 100f, map, CellFinder.EdgeRoadChance_Friendly, out exitCell))
            {
                exitCell = CellFinder.RandomEdgeCell(map);
            }
        }

        // 3. Generate or retrieve pawns
        List<Pawn> pawns = GetOrGenerateIncursionPawns(incursion, map);

        if (pawns.Count == 0)
        {
            Log.Warning("[Project Vigilance] Battlescape transit generated 0 pawns. Destroying incursion.");
            incursion.Destroy();
            return;
        }

        bool isHostile = incursion.Faction?.HostileTo(Faction.OfPlayer) ?? true;

        // 4. Spawn pawns at entry edge
        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn p = pawns[i];
            if (p != null)
            {
                if (!isHostile && incursion.IsUnderlyingTradeCaravan)
                {
                    IncursionTrackingHelper.EnsureTraderComponents(p, incursion);
                }

                if (!p.Spawned)
                {
                    CellFinder.TryFindRandomCellNear(entryCell, map, 4, c => c.Walkable(map) && !c.Fogged(map), out IntVec3 pCell);
                    GenSpawn.Spawn(p, pCell.IsValid ? pCell : entryCell, map, Rot4.Random);
                }
            }
        }

        // Spawn carried kidnapped colonists if any
        if (incursion.KidnappedColonists != null)
        {
            foreach (var captive in incursion.KidnappedColonists)
            {
                if (captive != null && !captive.Spawned)
                {
                    CellFinder.TryFindRandomCellNear(entryCell, map, 5, c => c.Walkable(map) && !c.Fogged(map), out IntVec3 cCell);
                    GenSpawn.Spawn(captive, cCell.IsValid ? cCell : entryCell, map, Rot4.Random);
                }
            }
        }

        // 5. Create Lord and assign LordJob_IncursionTransit
        StripExistingLords(pawns);
        Faction lordFaction = incursion.Faction ?? pawns[0].Faction;

        LordJob_IncursionTransit transitJob = new LordJob_IncursionTransit(incursion, lordFaction, exitCell, isHostile);
        LordMaker.MakeNewLord(lordFaction, transitJob, map, pawns);

        // Pack any carried trade stock onto pack animals/carriers for transiting trade caravans
        if (incursion.IsUnderlyingTradeCaravan)
        {
            incursion.SyncCargoFromTradeStockToPawns(pawns);
        }

        // Enable roadside trading for neutral traders while transiting peaceful sectors
        if (!isHostile && incursion.IsUnderlyingTradeCaravan)
        {
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p != null)
                {
                    IncursionTrackingHelper.EnsureTraderComponents(p, incursion);
                }
            }
        }

        // 6. Register into MapComponent_OverlandAmbush with isTransitOnly = true
        incursion.MarkMapMet(map.Parent?.ID ?? map.uniqueID);
        incursion.IsEngagedInTacticalMap = true;
        var ambushComp = map.GetComponent<MapComponent_OverlandAmbush>();
        if (ambushComp != null)
        {
            ambushComp.RegisterIncursion(incursion, pawns, markAmbushSprung: false);
            var record = ambushComp.Engagements.FirstOrDefault(e => e.incursion == incursion);
            if (record != null)
            {
                record.isTransitOnly = true;
                record.wasEngagedInCombat = false;
            }
        }

        // 7. Tactical Notification / Letter
        string contactName = incursion.LabelCap;
        string factionName = incursion.Faction?.Name ?? "unknown forces";
        string dirName = entryDir.ToString();

        if (isHostile)
        {
            Messages.Message(
                $"Unaware Hostile Patrol: A {contactName} from {factionName} has entered the sector from the {dirName} edge and is transiting the area. If you remain concealed, they may pass without noticing your squad.",
                new TargetInfo(entryCell, map),
                MessageTypeDefOf.ThreatSmall);
        }
        else
        {
            string groupDesc = incursion.GetIdentifiedGroupLabel();
            Messages.Message(
                $"Passing Travelers: {groupDesc} from {factionName} has entered the sector from the {dirName} edge, peacefully traversing the area.",
                new TargetInfo(entryCell, map),
                MessageTypeDefOf.NeutralEvent);
        }
    }

    private static void ResolveTransitDirections(WorldObject_Incursion incursion, out Rot4 entryDir, out Rot4 exitDir)
    {
        PlanetTile currentTile = incursion.Tile;
        PlanetTile destTile = incursion.DestinationTile;

        Vector2 currentLatLong = Find.WorldGrid.LongLatOf(currentTile);
        Vector2 destLatLong = Find.WorldGrid.LongLatOf(destTile);
        Vector2 delta = destLatLong - currentLatLong;

        if (Mathf.Abs(delta.y) >= Mathf.Abs(delta.x))
        {
            if (delta.y >= 0f)
            {
                // Moving North: entered from South, exiting North
                entryDir = Rot4.South;
                exitDir = Rot4.North;
            }
            else
            {
                // Moving South: entered from North, exiting South
                entryDir = Rot4.North;
                exitDir = Rot4.South;
            }
        }
        else
        {
            if (delta.x >= 0f)
            {
                // Moving East: entered from West, exiting East
                entryDir = Rot4.West;
                exitDir = Rot4.East;
            }
            else
            {
                // Moving West: entered from East, exiting West
                entryDir = Rot4.East;
                exitDir = Rot4.West;
            }
        }
    }

    private static void StripExistingLords(IReadOnlyList<Pawn> pawns)
    {
        if (pawns == null)
        {
            return;
        }

        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn p = pawns[i];
            if (p == null)
            {
                continue;
            }

            Lord? currentLord = p.GetLord();
            if (currentLord != null)
            {
                Map? lordMap = currentLord.Map ?? p.Map;
                currentLord.RemovePawn(p);
                if (currentLord.ownedPawns.Count == 0 && lordMap?.lordManager != null && lordMap.lordManager.lords.Contains(currentLord))
                {
                    lordMap.lordManager.RemoveLord(currentLord);
                }
            }
        }
    }

    private static void EnsureEnemyGroupSeparatedFromPlayer(Map map, List<Pawn> enemies, List<Pawn> colonists, IntVec3 playerCenter)
    {
        if (enemies == null || enemies.Count == 0 || colonists == null || colonists.Count == 0)
        {
            return;
        }

        bool tooClose = false;
        for (int i = 0; i < enemies.Count && !tooClose; i++)
        {
            Pawn e = enemies[i];
            if (e == null || !e.Spawned) continue;
            for (int j = 0; j < colonists.Count; j++)
            {
                Pawn c = colonists[j];
                if (c != null && c.Spawned && e.Position.InHorDistOf(c.Position, 35f))
                {
                    tooClose = true;
                    break;
                }
            }
        }

        if (!tooClose)
        {
            return;
        }

        if (CellFinder.TryFindRandomEdgeCellWith(
            c => c.Walkable(map) && !c.Fogged(map) && c.DistanceTo(playerCenter) >= 65f,
            map,
            CellFinder.EdgeRoadChance_Hostile,
            out IntVec3 newGroupCenter))
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                Pawn e = enemies[i];
                if (e != null && e.Spawned)
                {
                    CellFinder.TryFindRandomCellNear(newGroupCenter, map, 6, c => c.Walkable(map) && !c.Fogged(map), out IntVec3 dest);
                    e.Position = dest.IsValid ? dest : newGroupCenter;
                    e.Notify_Teleported(endCurrentJob: true, resetTweenedPos: true);
                }
            }
        }
    }

    private static IntVec3 FindAmbushTransitExitSpot(Map map, IntVec3 enemySpawn, IntVec3 playerCenter)
    {
        float minMarchDist = Mathf.Max(50f, map.Size.x * 0.45f);
        TraverseParms traverseParms = TraverseParms.For(TraverseMode.NoPassClosedDoors);

        bool IsValidExit(IntVec3 c, float minPlayerDist, float minPathClearance)
        {
            if (!c.InBounds(map) || !c.Walkable(map) || c.Fogged(map))
            {
                return false;
            }
            if (c.DistanceTo(enemySpawn) < minMarchDist)
            {
                return false;
            }
            if (c.DistanceTo(playerCenter) < minPlayerDist)
            {
                return false;
            }
            if (DistancePointToSegment(playerCenter, enemySpawn, c) < minPathClearance)
            {
                return false;
            }
            return map.reachability.CanReach(enemySpawn, c, PathEndMode.OnCell, traverseParms);
        }

        if (CellFinder.TryFindRandomEdgeCellWith(c => IsValidExit(c, 45f, 35f), map, CellFinder.EdgeRoadChance_Friendly, out IntVec3 exitSpot))
        {
            return exitSpot;
        }

        if (CellFinder.TryFindRandomEdgeCellWith(c => IsValidExit(c, 35f, 25f), map, CellFinder.EdgeRoadChance_Friendly, out exitSpot))
        {
            return exitSpot;
        }

        if (CellFinder.TryFindRandomEdgeCellWith(
            c => c.Walkable(map) && !c.Fogged(map) && c.DistanceTo(enemySpawn) >= 40f && c.DistanceTo(playerCenter) >= 30f && map.reachability.CanReach(enemySpawn, c, PathEndMode.OnCell, traverseParms),
            map,
            CellFinder.EdgeRoadChance_Friendly,
            out exitSpot))
        {
            return exitSpot;
        }

        if (RCellFinder.TryFindRandomPawnEntryCell(out exitSpot, map, CellFinder.EdgeRoadChance_Friendly))
        {
            return exitSpot;
        }

        return CellFinder.RandomEdgeCell(map);
    }

    private static float DistancePointToSegment(IntVec3 p, IntVec3 a, IntVec3 b)
    {
        Vector2 pv = new Vector2(p.x, p.z);
        Vector2 av = new Vector2(a.x, a.z);
        Vector2 bv = new Vector2(b.x, b.z);
        Vector2 ab = bv - av;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 0.001f)
        {
            return Vector2.Distance(pv, av);
        }
        float t = Mathf.Clamp01(Vector2.Dot(pv - av, ab) / lenSq);
        Vector2 proj = av + t * ab;
        return Vector2.Distance(pv, proj);
    }
}
