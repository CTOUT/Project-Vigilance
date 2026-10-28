using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.World;

/// <summary>
/// World map site representing the aftermath of an automated overland background skirmish
/// between hostile or rival planetary factions. Contains salvageable trade cargo, discarded supplies,
/// and wounded survivors that can be rescued, captured, or scavenged by player caravans.
/// </summary>
public class WorldObject_EncounterAftermath : MapParent, IThingHolder
{
    private ThingOwner<Thing> salvageLoot;
    private List<Pawn> woundedSurvivors = new();
    private Faction? victorFaction;
    private Faction? defeatedFaction;
    private int expiryTick;

    public ThingOwner<Thing> SalvageLoot => salvageLoot;
    public List<Pawn> WoundedSurvivors => woundedSurvivors;
    public Faction? VictorFaction { get => victorFaction; set => victorFaction = value; }
    public Faction? DefeatedFaction { get => defeatedFaction; set => defeatedFaction = value; }
    public int ExpiryTick { get => expiryTick; set => expiryTick = value; }

    public WorldObject_EncounterAftermath()
    {
        salvageLoot = new ThingOwner<Thing>(this);
    }

    public void SetupAftermath(
        Faction? victor,
        Faction? defeated,
        SkirmishResolution res,
        WorldObject_Incursion? incA = null,
        WorldObject_Incursion? incB = null,
        List<Pawn>? casualties = null)
    {
        victorFaction = victor;
        defeatedFaction = defeated;
        expiryTick = (Current.Game?.tickManager?.TicksGame ?? 0) + 180000; // 3 in-game days

        // 1. Gather salvageable cargo from participant trade stocks if present
        CollectTradeStockLoot(incA);
        CollectTradeStockLoot(incB);

        // 2. Generate procedural trade and survival salvage based on skirmish scale
        GenerateProceduralSalvage(res);

        // 3. Harvest or generate wounded survivors
        HarvestOrGenerateSurvivors(defeated ?? victor, res.WoundedSurvivorCount, incA, incB, casualties);
    }

    private void CollectTradeStockLoot(WorldObject_Incursion? incursion)
    {
        if (incursion?.TradeStock == null || incursion.TradeStock.Count == 0) return;

        var stockItems = incursion.TradeStock.ToList();
        int itemsToTake = Math.Max(1, stockItems.Count / 3);

        for (int i = 0; i < itemsToTake && i < stockItems.Count; i++)
        {
            Thing item = stockItems[i];
            incursion.TradeStock.Remove(item);
            salvageLoot.TryAdd(item, canMergeWithExistingStacks: true);
        }
    }

    private void GenerateProceduralSalvage(SkirmishResolution res)
    {
        // Add currency salvage
        int silverCount = Mathf.Clamp((int)(res.SalvageLootValue * 0.75f), 75, 1200);
        if (ThingDefOf.Silver != null)
        {
            Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = silverCount;
            salvageLoot.TryAdd(silver, canMergeWithExistingStacks: true);
        }

        // Add provisions
        if (ThingDefOf.MealSurvivalPack != null)
        {
            Thing meals = ThingMaker.MakeThing(ThingDefOf.MealSurvivalPack);
            meals.stackCount = Rand.RangeInclusive(4, 16);
            salvageLoot.TryAdd(meals, canMergeWithExistingStacks: true);
        }

        // Add medicine
        ThingDef medDef = (victorFaction?.def?.techLevel ?? TechLevel.Industrial) >= TechLevel.Industrial
            ? (ThingDefOf.MedicineIndustrial ?? ThingDefOf.MedicineHerbal)
            : (ThingDefOf.MedicineHerbal ?? ThingDefOf.MedicineIndustrial);

        if (medDef != null)
        {
            Thing medicine = ThingMaker.MakeThing(medDef);
            medicine.stackCount = Rand.RangeInclusive(3, 10);
            salvageLoot.TryAdd(medicine, canMergeWithExistingStacks: true);
        }

        // Add industrial components if spacer / industrial
        if ((victorFaction?.def?.techLevel ?? TechLevel.Industrial) >= TechLevel.Industrial && ThingDefOf.ComponentIndustrial != null)
        {
            Thing components = ThingMaker.MakeThing(ThingDefOf.ComponentIndustrial);
            components.stackCount = Rand.RangeInclusive(2, 6);
            salvageLoot.TryAdd(components, canMergeWithExistingStacks: true);
        }
    }

    private void HarvestOrGenerateSurvivors(
        Faction? faction,
        int count,
        WorldObject_Incursion? incA,
        WorldObject_Incursion? incB,
        List<Pawn>? casualties = null)
    {
        if (count <= 0) return;

        // 1. First prioritize actual casualties harvested from the battle
        if (casualties != null && casualties.Count > 0)
        {
            for (int i = 0; i < casualties.Count && woundedSurvivors.Count < count; i++)
            {
                Pawn p = casualties[i];
                if (p != null && !p.Dead && !p.Destroyed)
                {
                    HealthUtility.DamageUntilDowned(p, allowBleedingWounds: true);
                    EnsureSurvivorInWorldPawns(p);
                    woundedSurvivors.Add(p);
                }
            }
        }

        // 2. If more are needed, check incursion rosters
        if (woundedSurvivors.Count < count)
        {
            ExtractCasualtyPawn(incB);
        }
        if (woundedSurvivors.Count < count)
        {
            ExtractCasualtyPawn(incA);
        }

        // 3. Fallback to procedural generation if still needed
        while (woundedSurvivors.Count < count && faction != null)
        {
            PawnKindDef kindDef = faction.RandomPawnKind() ??
                                  DefDatabase<PawnKindDef>.GetNamedSilentFail("Colonist") ??
                                  PawnKindDefOf.Colonist;

            if (kindDef == null) break;

            try
            {
                var request = new PawnGenerationRequest(
                    kindDef,
                    faction,
                    PawnGenerationContext.NonPlayer,
                    tile: Tile,
                    forceGenerateNewPawn: true,
                    allowDead: false,
                    allowDowned: true);

                Pawn survivor = PawnGenerator.GeneratePawn(request);
                if (survivor != null)
                {
                    HealthUtility.DamageUntilDowned(survivor, allowBleedingWounds: true);
                    EnsureSurvivorInWorldPawns(survivor);
                    woundedSurvivors.Add(survivor);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[Project Vigilance] Procedural survivor generation skipped: {ex.Message}");
                break;
            }
        }
    }

    private void ExtractCasualtyPawn(WorldObject_Incursion? incursion)
    {
        if (incursion?.CommittedPawns != null && incursion.CommittedPawns.Count > 0)
        {
            Pawn casualty = incursion.CommittedPawns.Last();
            incursion.CommittedPawns.Remove(casualty);
            if (!casualty.Dead && !casualty.Destroyed)
            {
                HealthUtility.DamageUntilDowned(casualty, allowBleedingWounds: true);
                EnsureSurvivorInWorldPawns(casualty);
                woundedSurvivors.Add(casualty);
            }
        }
    }

    private void EnsureSurvivorInWorldPawns(Pawn survivor)
    {
        if (survivor == null || survivor.Dead || survivor.Destroyed || survivor.Spawned) return;
        if (Find.WorldPawns != null)
        {
            if (!Find.WorldPawns.Contains(survivor))
            {
                Find.WorldPawns.PassToWorld(survivor, PawnDiscardDecideMode.KeepForever);
            }
            Find.WorldPawns.ForcefullyKeptPawns.Add(survivor);
        }
    }

    protected override void Tick()
    {
        base.Tick();

        if (expiryTick > 0 && Find.TickManager != null && Find.TickManager.TicksGame >= expiryTick && !HasMap)
        {
            Destroy();
        }
    }

    public override MapGeneratorDef MapGeneratorDef =>
        base.MapGeneratorDef ?? DefDatabase<MapGeneratorDef>.GetNamedSilentFail("CaravanIncident") ?? MapGeneratorDefOf.Encounter;

    public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan)
    {
        foreach (var opt in base.GetFloatMenuOptions(caravan))
        {
            yield return opt;
        }

        if (caravan == null || !caravan.Spawned || !caravan.IsPlayerControlled)
        {
            yield break;
        }

        if (caravan.Tile == Tile)
        {
            yield return new FloatMenuOption("Scavenge aftermath (Salvage loot & tend survivors)", () =>
            {
                ScavengeByCaravan(caravan);
            }, MenuOptionPriority.High);
        }
        else
        {
            yield return new FloatMenuOption("Travel to encounter aftermath", () =>
            {
                caravan.pather.StartPath(Tile, new CaravanArrivalAction_ScavengeAftermath(this), true);
            }, MenuOptionPriority.Default);
        }
    }

    public void ScavengeByCaravan(Caravan caravan)
    {
        if (caravan == null || !caravan.Spawned) return;

        int salvagedCount = salvageLoot.Count;
        int survivorCount = 0;

        // 1. Transfer all salvageable cargo to player caravan
        var itemsToTransfer = salvageLoot.ToList();
        for (int i = 0; i < itemsToTransfer.Count; i++)
        {
            Thing item = itemsToTransfer[i];
            salvageLoot.Remove(item);
            CaravanInventoryUtility.GiveThing(caravan, item);
        }

        // 2. Incorporate wounded survivors into caravan
        for (int i = 0; i < woundedSurvivors.Count; i++)
        {
            Pawn survivor = woundedSurvivors[i];
            if (survivor != null && !survivor.Dead && !survivor.Destroyed)
            {
                if (survivor.Faction != null && survivor.Faction.HostileTo(Faction.OfPlayer))
                {
                    survivor.guest?.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner);
                }
                else
                {
                    survivor.guest?.SetGuestStatus(Faction.OfPlayer, GuestStatus.Guest);
                }

                caravan.AddPawn(survivor, addCarriedPawnToWorldPawnsIfAny: false);
                if (Find.WorldPawns != null)
                {
                    Find.WorldPawns.ForcefullyKeptPawns.Remove(survivor);
                }
                survivorCount++;
            }
        }
        woundedSurvivors.Clear();

        string vicName = victorFaction?.Name ?? "rival forces";
        string defName = defeatedFaction?.Name ?? "enemy forces";
        Messages.Message(
            $"{caravan.LabelCap} investigated the encounter aftermath ({vicName} vs {defName}), recovering {salvagedCount} cargo caches and tending to {survivorCount} wounded survivor{(survivorCount == 1 ? "" : "s")}.",
            new GlobalTargetInfo(Tile),
            MessageTypeDefOf.PositiveEvent);

        Destroy();
    }

    public override void PostMapGenerate()
    {
        base.PostMapGenerate();

        if (Map == null) return;

        IntVec3 center = Map.Center;

        // Scatter salvage loot
        var items = salvageLoot.ToList();
        for (int i = 0; i < items.Count; i++)
        {
            Thing item = items[i];
            salvageLoot.Remove(item);
            if (CellFinder.TryFindRandomCellNear(center, Map, 12, c => c.Standable(Map), out IntVec3 dropCell))
            {
                GenPlace.TryPlaceThing(item, dropCell, Map, ThingPlaceMode.Near);
            }
            else
            {
                GenPlace.TryPlaceThing(item, center, Map, ThingPlaceMode.Near);
            }
        }

        // Scatter wounded survivors
        for (int i = 0; i < woundedSurvivors.Count; i++)
        {
            Pawn survivor = woundedSurvivors[i];
            if (survivor != null && !survivor.Dead && !survivor.Spawned)
            {
                if (CellFinder.TryFindRandomCellNear(center, Map, 10, c => c.Standable(Map), out IntVec3 survivorCell))
                {
                    GenSpawn.Spawn(survivor, survivorCell, Map);
                }
                else
                {
                    GenSpawn.Spawn(survivor, center, Map);
                }
            }
        }
        woundedSurvivors.Clear();
    }

    public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject)
    {
        alsoRemoveWorldObject = true;
        return !Map.mapPawns.AnyPawnBlockingMapRemoval;
    }

    public override string GetInspectString()
    {
        var sb = new StringBuilder();
        try
        {
            string baseStr = base.GetInspectString();
            if (!string.IsNullOrEmpty(baseStr))
            {
                sb.AppendLine(baseStr);
            }
        }
        catch
        {
            // Headless unit tests or uninitialized Verse file paths
        }

        string victor = victorFaction?.Name ?? "Unknown Victor";
        string defeated = defeatedFaction?.Name ?? "Unknown Defeated";
        sb.AppendLine($"Skirmish: {victor} vs {defeated}");

        int lootCount = salvageLoot?.Count ?? 0;
        int survivorCount = woundedSurvivors?.Count(p => !p.Dead) ?? 0;
        sb.AppendLine($"Aftermath: {lootCount} cargo cache{(lootCount == 1 ? "" : "s")}, {survivorCount} wounded survivor{(survivorCount == 1 ? "" : "s")}");

        try
        {
            if (expiryTick > 0 && Current.Game?.tickManager is { } tm)
            {
                int remaining = Math.Max(0, expiryTick - tm.TicksGame);
                sb.Append($"Wilderness decay in: {remaining.ToStringTicksToPeriod()}");
            }
        }
        catch
        {
            // Headless unit tests or uninitialized Game
        }

        return sb.ToString().TrimEnd();
    }

    public override void GetChildHolders(List<IThingHolder> outChildren)
    {
        ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
    }

    public new ThingOwner GetDirectlyHeldThings() => salvageLoot;

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Deep.Look(ref salvageLoot, "salvageLoot", this);
        salvageLoot ??= new ThingOwner<Thing>(this);

        if (Scribe.mode == LoadSaveMode.Saving && woundedSurvivors != null)
        {
            for (int i = 0; i < woundedSurvivors.Count; i++)
            {
                Pawn p = woundedSurvivors[i];
                if (p != null && !p.Destroyed && !p.Spawned && Find.WorldPawns != null && !Find.WorldPawns.Contains(p))
                {
                    Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
                }
            }
        }

        Scribe_Collections.Look(ref woundedSurvivors, "woundedSurvivors", LookMode.Reference);
        woundedSurvivors ??= new List<Pawn>();

        Scribe_References.Look(ref victorFaction, "victorFaction");
        Scribe_References.Look(ref defeatedFaction, "defeatedFaction");
        Scribe_Values.Look(ref expiryTick, "expiryTick", 0);
    }

    public override void Destroy()
    {
        if (woundedSurvivors != null)
        {
            for (int i = 0; i < woundedSurvivors.Count; i++)
            {
                Pawn p = woundedSurvivors[i];
                if (p != null)
                {
                    if (Find.WorldPawns != null)
                    {
                        Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
                        if (Find.WorldPawns.Contains(p) && !p.Dead)
                        {
                            Find.WorldPawns.RemoveAndDiscardPawnViaGC(p);
                        }
                    }
                    else if (!p.Destroyed)
                    {
                        p.Destroy();
                    }
                }
            }
            woundedSurvivors.Clear();
        }
        salvageLoot?.ClearAndDestroyContents();
        base.Destroy();
    }
}
