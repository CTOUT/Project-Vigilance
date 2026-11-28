using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.World;

public partial class WorldObject_Incursion
{
    public IEnumerable<Thing> Goods => IsUnderlyingTradeCaravan ? GetOrCreateTradeStock() : Enumerable.Empty<Thing>();

    public int RandomPriceFactorSeed => ID;

    public string TraderName => LabelCap;

    public bool CanTradeNow => IsUnderlyingTradeCaravan &&
                               TraderKind != null &&
                               !isTradeMissionAborted &&
                               Faction != null &&
                               !Faction.HostileTo(Faction.OfPlayer);

    public float TradePriceImprovementOffsetForPlayer => 0f;

    public TradeCurrency TradeCurrency => TraderKind?.tradeCurrency ?? TradeCurrency.Silver;

    public IEnumerable<Thing> ColonyThingsWillingToBuy(Pawn playerNegotiator)
    {
        Caravan? caravan = playerNegotiator?.GetCaravan();
        if (caravan != null)
        {
            foreach (Thing t in CaravanInventoryUtility.AllInventoryItems(caravan))
            {
                yield return t;
            }
        }
    }

    public void GiveSoldThingToTrader(Thing toGive, int countToGive, Pawn playerNegotiator)
    {
        Thing split = toGive.SplitOff(countToGive);
        GetOrCreateTradeStock().TryAdd(split, canMergeWithExistingStacks: true);
    }

    public void GiveSoldThingToPlayer(Thing toGive, int countToGive, Pawn playerNegotiator)
    {
        Caravan? caravan = playerNegotiator?.GetCaravan();
        Thing split = toGive.SplitOff(countToGive);
        if (caravan != null)
        {
            CaravanInventoryUtility.GiveThing(caravan, split);
        }
        else if (playerNegotiator?.inventory != null)
        {
            playerNegotiator.inventory.innerContainer.TryAdd(split);
        }
    }

    public ThingOwner<Thing> GetOrCreateTradeStock()
    {
        if (tradeStock == null)
        {
            tradeStock = new ThingOwner<Thing>(this);
            if (IsUnderlyingTradeCaravan)
            {
                // First check if committed or retreating pawns already carry wares from generation
                bool foundAnyItemsInCarriers = false;
                var sourcePawns = committedPawns ?? retreatingPawns;
                if (sourcePawns != null && sourcePawns.Count > 0)
                {
                    for (int i = 0; i < sourcePawns.Count; i++)
                    {
                        Pawn p = sourcePawns[i];
                        if (p?.inventory?.innerContainer != null && p.inventory.innerContainer.Count > 0)
                        {
                            foundAnyItemsInCarriers = true;
                            var items = p.inventory.innerContainer.ToList();
                            for (int it = 0; it < items.Count; it++)
                            {
                                Thing t = items[it];
                                p.inventory.innerContainer.Remove(t);
                                tradeStock.TryAdd(t, canMergeWithExistingStacks: true);
                            }
                        }
                    }
                }

                if (!foundAnyItemsInCarriers && TraderKind != null)
                {
                    ThingSetMakerParams parms = default;
                    parms.traderDef = TraderKind;
                    parms.makingFaction = Faction;
                    parms.tile = Tile;
                    List<Thing> generated = ThingSetMakerDefOf.TraderStock.root.Generate(parms);
                    for (int i = 0; i < generated.Count; i++)
                    {
                        if (generated[i] is Pawn generatedPawn)
                        {
                            committedPawns ??= new List<Pawn>();
                            if (!committedPawns.Contains(generatedPawn))
                            {
                                committedPawns.Add(generatedPawn);
                            }

                            EnsureInWorldPawnsIfUnspawned(generatedPawn);
                            continue;
                        }

                        tradeStock.TryAdd(generated[i], canMergeWithExistingStacks: false);
                    }
                }
            }
        }
        return tradeStock;
    }

    /// <summary>
    /// Transfers all items held in tradeStock onto carrier pack animals (or humanoids if pack animals are absent)
    /// so the caravan pawns physically transport their trade cargo on tactical battle and transit maps.
    /// </summary>
    public void SyncCargoFromTradeStockToPawns(List<Pawn> pawns)
    {
        if (!IsUnderlyingTradeCaravan || tradeStock == null || tradeStock.Count == 0 || pawns == null || pawns.Count == 0) return;

        var carriers = pawns.Where(p => p != null && !p.Dead && p.inventory != null && p.RaceProps != null && p.RaceProps.packAnimal).ToList();
        if (carriers.Count == 0)
        {
            carriers = pawns.Where(p => p != null && !p.Dead && p.inventory != null).ToList();
        }

        if (carriers.Count > 0)
        {
            int carrierIdx = 0;
            var stockList = tradeStock.ToList();
            for (int s = 0; s < stockList.Count; s++)
            {
                Thing item = stockList[s];
                tradeStock.Remove(item);
                Pawn carrier = carriers[carrierIdx % carriers.Count];
                carrier.inventory.innerContainer.TryAdd(item, canMergeWithExistingStacks: true);
                carrierIdx++;
            }
        }
    }

    /// <summary>
    /// Gathers all items from carrier pawns' inventories back into tradeStock when transitioning from tactical maps
    /// to the Geoscape, ensuring dropped cargo from casualties stays on the battlefield while surviving cargo persists.
    /// </summary>
    public void SyncCargoFromPawnsToTradeStock(List<Pawn> pawns)
    {
        if (!IsUnderlyingTradeCaravan || pawns == null || pawns.Count == 0) return;
        var stock = GetOrCreateTradeStock();
        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn p = pawns[i];
            if (p?.inventory?.innerContainer != null && p.inventory.innerContainer.Count > 0)
            {
                var items = p.inventory.innerContainer.ToList();
                for (int it = 0; it < items.Count; it++)
                {
                    Thing t = items[it];
                    p.inventory.innerContainer.Remove(t);
                    stock.TryAdd(t, canMergeWithExistingStacks: true);
                }
            }
        }
    }

    public void GetChildHolders(List<IThingHolder> outChildren)
    {
        ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
    }

    public ThingOwner GetDirectlyHeldThings() => GetOrCreateTradeStock();

    // =========================================================================
    // Overland Physiological Simulation (Retreating Raiders & Captives)
    // =========================================================================

    private void TickCommittedPawnsPhysiology(int interval)
    {
        if (committedPawns != null && committedPawns.Count > 0)
        {
            SimulatePawnsPhysiology(committedPawns, interval, "Incursion combatant");
        }
    }

    private void TickRetreatingPawnsPhysiology(int interval)
    {
        bool hadLivingCaptors = retreatingPawns != null && retreatingPawns.Count > 0;
        if (hadLivingCaptors)
        {
            SimulatePawnsPhysiology(retreatingPawns!, interval, "Retreating raider");
        }

        if (retreatingPawns == null || retreatingPawns.Count == 0)
        {
            if (hadLivingCaptors && !captorsSuccumbedLetterSent && kidnappedColonists != null && kidnappedColonists.Count > 0)
            {
                captorsSuccumbedLetterSent = true;
                string captivesList = string.Join(", ", kidnappedColonists.Select(c => c.LabelShort));
                LetterDef posDef = PV_LetterDefOf.PV_TacticalEncounter_Positive ?? LetterDefOf.PositiveEvent;
                var letter = (ChoiceLetter_TacticalEncounter)LetterMaker.MakeLetter(posDef);
                letter.label = "Captors Succumbed: " + LabelCap;
                letter.text = $"All captors holding {captivesList} have died of their wounds on the overland trail!\n\n" +
                              $"{captivesList} remains stranded at tile {Tile}. A rescue caravan can now be dispatched to recover them safely.";
                letter.targetWorldObject = this;
                letter.targetTile = Tile;
                letter.worldButtonLabel = "Go to cell (Geoscape)";
                letter.lookTargets = new GlobalTargetInfo(Tile);
                Find.LetterStack.ReceiveLetter(letter);
            }
            return;
        }

        Pawn? doctor = retreatingPawns
            .Where(p => !p.Dead && !p.Downed && p.health?.capacities?.CanBeAwake == true)
            .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0)
            .FirstOrDefault();

        Medicine? carriedMed = null;
        foreach (var p in retreatingPawns)
        {
            if (p.inventory?.innerContainer != null)
            {
                var med = p.inventory.innerContainer.FirstOrDefault(t => t is Medicine) as Medicine;
                if (med != null)
                {
                    carriedMed = med;
                    break;
                }
            }
        }

        if (kidnappedColonists != null && kidnappedColonists.Count > 0)
        {
            for (int i = kidnappedColonists.Count - 1; i >= 0; i--)
            {
                Pawn captive = kidnappedColonists[i];
                if (captive == null)
                {
                    kidnappedColonists.RemoveAt(i);
                    continue;
                }

                if (!captive.Dead)
                {
                    try
                    {
                        captive.health?.HealthTickInterval(interval);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[Project Vigilance] Error ticking health for captive {captive.LabelShort}: {ex.Message}");
                    }

                    if (captive.Dead)
                    {
                        Messages.Message($"Captured colonist {captive.LabelShort} has died of their wounds while in enemy hands.", new GlobalTargetInfo(Tile), MessageTypeDefOf.NegativeEvent);
                    }
                    else
                    {
                        if (doctor != null && captive.health != null && captive.health.HasHediffsNeedingTend())
                        {
                            TendUtility.DoTend(doctor, captive, carriedMed);
                        }

                        if (captive.needs?.food != null)
                        {
                            captive.needs.food.CurLevel -= 0.0005f * (interval / 250f);

                            // Captors feed prisoners from carried provisions
                            if (captive.needs.food.CurLevelPercentage < 0.3f)
                            {
                                foreach (var captor in retreatingPawns)
                                {
                                    Thing? food = captor.inventory?.innerContainer?.FirstOrDefault(t => t.def.IsNutritionGivingIngestible);
                                    if (food != null)
                                    {
                                        float nut = food.def.ingestible?.CachedNutrition ?? 0.5f;
                                        captive.needs.food.CurLevel += nut;
                                        food.SplitOff(1).Destroy();
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private void SimulatePawnsPhysiology(List<Pawn> pawns, int interval, string pawnLabel)
    {
        if (pawns == null || pawns.Count == 0) return;

        bool restingNow = IsRestingAtNight;

        Pawn? doctor = pawns
            .Where(p => !p.Dead && !p.Downed && p.health?.capacities?.CanBeAwake == true)
            .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0)
            .FirstOrDefault();

        Medicine? carriedMed = null;
        foreach (var p in pawns)
        {
            if (p.inventory?.innerContainer != null)
            {
                var med = p.inventory.innerContainer.FirstOrDefault(t => t is Medicine) as Medicine;
                if (med != null)
                {
                    carriedMed = med;
                    break;
                }
            }
        }

        for (int i = pawns.Count - 1; i >= 0; i--)
        {
            Pawn p = pawns[i];
            if (p == null)
            {
                pawns.RemoveAt(i);
                continue;
            }

            if (!p.Dead)
            {
                try
                {
                    p.health?.HealthTickInterval(interval);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[Project Vigilance] Error ticking health for {pawnLabel} {p.LabelShort}: {ex.Message}");
                }

                if (p.Dead)
                {
                    Log.Message($"[Project Vigilance] {pawnLabel} {p.LabelShort} succumbed to wounds on the march.");
                    pawns.RemoveAt(i);
                    continue;
                }

                if (doctor != null && p.health != null && p.health.HasHediffsNeedingTend())
                {
                    if (carriedMed != null && (carriedMed.Destroyed || carriedMed.stackCount <= 0))
                    {
                        carriedMed = null;
                        for (int m = 0; m < pawns.Count; m++)
                        {
                            var med = pawns[m].inventory?.innerContainer?.FirstOrDefault(t => t is Medicine && !t.Destroyed && t.stackCount > 0) as Medicine;
                            if (med != null)
                            {
                                carriedMed = med;
                                break;
                            }
                        }
                    }

                    TendUtility.DoTend(doctor, p, carriedMed);
                }

                if (p.needs != null)
                {
                    if (restingNow && p.needs.rest != null)
                    {
                        p.needs.rest.CurLevel += 0.002f * (interval / 250f);
                    }
                    if (p.needs.food != null)
                    {
                        p.needs.food.CurLevel -= 0.0005f * (interval / 250f);

                        // Caravan Eating Simulation: Consume carried provisions when hungry
                        if (p.needs.food.CurLevelPercentage < 0.35f && p.inventory?.innerContainer != null)
                        {
                            Thing? edible = p.inventory.innerContainer.FirstOrDefault(t => t.def.IsNutritionGivingIngestible);
                            if (edible != null)
                            {
                                float nut = edible.def.ingestible?.CachedNutrition ?? 0.5f;
                                p.needs.food.CurLevel += nut;
                                edible.SplitOff(1).Destroy();
                            }
                        }
                    }
                }
            }
        }
    }

    public override void Destroy()
    {
        string defaultOutcome = (Faction != null && Faction.HostileTo(Faction.OfPlayer))
            ? "Neutralized"
            : "Disbanded";
        ContactRegistry.Instance?.Notify_ContactDestroyed(this, defaultOutcome);

        UnpinUnownedPawns(committedPawns);
        UnpinUnownedPawns(retreatingPawns);
        tradeStock?.ClearAndDestroyContents();
        base.Destroy();
    }

    private void UnpinUnownedPawns(List<Pawn>? pawns)
    {
        if (pawns == null || Find.WorldPawns == null)
        {
            return;
        }

        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn? p = pawns[i];
            if (p != null && !WorldPawnIntegrityUtility.IsTrackedByAnyGeoscapeSystem(p, excludingIncursion: this))
            {
                Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
            }
        }
    }
}

