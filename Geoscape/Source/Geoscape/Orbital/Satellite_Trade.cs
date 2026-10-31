using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Orbital;

/// <summary>
/// Specialized commercial satellite platform representing a roving orbital trade outpost.
/// Integrates with Building_CommsConsole, enabling orbital trade when passing overhead player colonies.
/// Restocks inventory when rendezvousing with its parent orbital station.
/// </summary>
public class Satellite_Trade : WorldObject_Satellite, ICommunicable, ITrader, IThingHolder
{
    private ThingOwner<Thing> goods;
    private TraderKindDef? traderKind;
    private int randomPriceFactorSeed;
    private bool wasAnnounced;

    public new IThingHolder? ParentHolder => base.ParentHolder;

    public Satellite_Trade()
    {
        goods = new ThingOwner<Thing>(this);
        Role = SatelliteRole.Trade;
    }

    public TraderKindDef TraderKind => traderKind ?? (traderKind = DefDatabase<TraderKindDef>.GetRandom());

    public IEnumerable<Thing> Goods => goods;

    public int RandomPriceFactorSeed => randomPriceFactorSeed;

    public string TraderName => OrbitalCallsign;

    public bool CanTradeNow => Faction != null && !Faction.HostileTo(Faction.OfPlayer);

    public float TradePriceImprovementOffsetForPlayer => 0f;

    public TradeCurrency TradeCurrency => TradeCurrency.Silver;

    public override void SpawnSetup()
    {
        base.SpawnSetup();
        Role = SatelliteRole.Trade;

        if (randomPriceFactorSeed == 0)
        {
            randomPriceFactorSeed = Rand.Range(1, 1000000);
        }

        if (goods.Count == 0)
        {
            GenerateInitialGoods();
        }
    }

    public void GenerateInitialGoods()
    {
        goods.ClearAndDestroyContents();
        if (TraderKind == null) return;

        ThingSetMakerParams parms = default;
        parms.traderDef = TraderKind;
        parms.tile = Tile.Valid ? Tile : (PlanetTile)0;
        parms.makingFaction = Faction;

        List<Thing> generated = ThingSetMakerDefOf.TraderStock.root.Generate(parms);
        goods.TryAddRangeOrTransfer(generated);
    }

    public override void OnEnterServiceArea(WorldObject_OrbitalStation station)
    {
        base.OnEnterServiceArea(station);
        // Restock inventory while in station service corridor
        GenerateInitialGoods();
    }

    #region ICommunicable Implementation

    public string GetCallLabel() => $"{OrbitalCallsign} ({Faction?.Name ?? "Independent"})";

    public string GetInfoText() => $"{OrbitalCallsign}\n{TraderKind?.label.CapitalizeFirst() ?? "Orbital Trader"}\nFaction: {Faction?.Name ?? "Independent"}";

    public Faction GetFaction() => Faction;

    public void TryOpenComms(Pawn myPawn)
    {
        if (!CanTradeNow) return;
        Find.WindowStack.Add(new Dialog_Trade(myPawn, this));
    }

    public FloatMenuOption CommFloatMenuOption(Building_CommsConsole console, Pawn myPawn)
    {
        string text = "CallOnRadio".Translate(GetCallLabel());
        return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(text, () =>
        {
            console.GiveUseCommsJob(myPawn, this);
        }, MenuOptionPriority.InitiateSocial), myPawn, console);
    }

    #endregion

    #region ITrader Implementation

    public IEnumerable<Thing> ColonyThingsWillingToBuy(Pawn playerNegotiator)
    {
        if (playerNegotiator?.Map == null) yield break;
        foreach (Thing t in TradeUtility.AllLaunchableThingsForTrade(playerNegotiator.Map, this))
        {
            yield return t;
        }
    }

    public void GiveSoldThingToTrader(Thing toGive, int countToGive, Pawn playerNegotiator)
    {
        Thing thing = toGive.SplitOff(countToGive);
        thing.PreTraded(TradeAction.PlayerSells, playerNegotiator, this);
        goods.TryAdd(thing, canMergeWithExistingStacks: false);
    }

    public void GiveSoldThingToPlayer(Thing toGive, int countToGive, Pawn playerNegotiator)
    {
        Thing thing = toGive.SplitOff(countToGive);
        thing.PreTraded(TradeAction.PlayerBuys, playerNegotiator, this);

        if (playerNegotiator?.Map != null)
        {
            IntVec3 dropSpot = DropCellFinder.TradeDropSpot(playerNegotiator.Map);
            TradeUtility.SpawnDropPod(dropSpot, playerNegotiator.Map, thing);
        }
        else
        {
            thing.Destroy(DestroyMode.Vanish);
        }
    }

    #endregion

    #region IThingHolder Implementation

    public void GetChildHolders(List<IThingHolder> outChildren)
    {
        ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
    }

    public ThingOwner GetDirectlyHeldThings() => goods;

    #endregion

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Deep.Look(ref goods, "goods", this);
        Scribe_Defs.Look(ref traderKind, "traderKind");
        Scribe_Values.Look(ref randomPriceFactorSeed, "randomPriceFactorSeed", 0);
        Scribe_Values.Look(ref wasAnnounced, "wasAnnounced", false);

        goods ??= new ThingOwner<Thing>(this);
    }
}
