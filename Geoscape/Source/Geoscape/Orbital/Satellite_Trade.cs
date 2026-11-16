using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

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
    private List<int> activeOverpassMapIds = new();

    public new IThingHolder? ParentHolder => base.ParentHolder;
    public IReadOnlyList<int> ActiveOverpassMapIds => activeOverpassMapIds;

    public Satellite_Trade()
    {
        goods = new ThingOwner<Thing>(this);
        Role = SatelliteRole.Trade;
    }

    public TraderKindDef TraderKind => traderKind ?? (traderKind = DefDatabase<TraderKindDef>.GetRandom());

    public IEnumerable<Thing> Goods
    {
        get
        {
            EnsureGoodsGenerated();
            return goods;
        }
    }

    public int RandomPriceFactorSeed => randomPriceFactorSeed;

    public string TraderName => OrbitalCallsign;

    public bool CanTradeNow
    {
        get
        {
            if (Faction == null || Find.World == null || Find.FactionManager == null) return false;
            Faction? playerFaction = Faction.OfPlayerSilentFail;
            return playerFaction != null && !Faction.HostileTo(playerFaction);
        }
    }

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

        if (goods.Count == 0 && Current.ProgramState == ProgramState.Playing && Find.World != null && Find.FactionManager != null && Faction.OfPlayerSilentFail != null)
        {
            GenerateInitialGoods();
        }
    }

    private void EnsureGoodsGenerated()
    {
        if (goods.Count == 0 && Current.ProgramState == ProgramState.Playing && Find.World != null && Find.FactionManager != null && Faction.OfPlayerSilentFail != null)
        {
            GenerateInitialGoods();
        }
    }

    public void GenerateInitialGoods()
    {
        if (Current.ProgramState != ProgramState.Playing || Find.World == null || Find.FactionManager == null || Faction.OfPlayerSilentFail == null)
        {
            return;
        }

        goods.ClearAndDestroyContents();
        if (TraderKind == null) return;

        ThingSetMakerParams parms = default;
        parms.traderDef = TraderKind;
        parms.tile = Tile.Valid ? Tile : (PlanetTile)0;
        parms.makingFaction = Faction;

        List<Thing> generated = ThingSetMakerDefOf.TraderStock.root.Generate(parms);
        goods.TryAddRangeOrTransfer(generated);
    }

    public override void OnEnterServiceArea(WorldObject station)
    {
        base.OnEnterServiceArea(station);
        if (Current.ProgramState == ProgramState.Playing && Find.World != null && Find.FactionManager != null && Faction.OfPlayerSilentFail != null)
        {
            // Restock inventory while in station service corridor
            GenerateInitialGoods();
        }
    }

    public override void PostRemove()
    {
        base.PostRemove();
        activeOverpassMapIds.Clear();
    }

    protected override void Tick()
    {
        base.Tick();

        int currentTick = Current.Game?.tickManager?.TicksGame ?? 0;
        if (currentTick % 250 == 0)
        {
            CheckColonyOverpass();
        }
    }

    public void CheckColonyOverpass()
    {
        if (Current.ProgramState != ProgramState.Playing || Find.Maps == null || Find.LetterStack == null)
        {
            return;
        }

        List<Map> maps = Find.Maps;
        for (int i = 0; i < maps.Count; i++)
        {
            Map map = maps[i];
            if (map == null || !map.IsPlayerHome || !map.Tile.Valid)
            {
                continue;
            }

            Building_CommsConsole? console = OrbitalVisibilityUtility.FindPoweredCommsConsole(map);
            bool isOverhead = CoversTile(map.Tile);
            bool wasActive = activeOverpassMapIds.Contains(map.Index);

            if (isOverhead)
            {
                if (!wasActive && console != null && CanTradeNow)
                {
                    activeOverpassMapIds.Add(map.Index);
                    NotifyEnteredOverpass(map, console);
                }
            }
            else if (wasActive)
            {
                activeOverpassMapIds.Remove(map.Index);
                NotifyDepartedOverpass(map);
            }
        }

        for (int i = activeOverpassMapIds.Count - 1; i >= 0; i--)
        {
            int mapId = activeOverpassMapIds[i];
            bool mapExists = false;
            for (int j = 0; j < maps.Count; j++)
            {
                if (maps[j].Index == mapId)
                {
                    mapExists = true;
                    break;
                }
            }

            if (!mapExists)
            {
                activeOverpassMapIds.RemoveAt(i);
            }
        }
    }

    private void NotifyEnteredOverpass(Map map, Building_CommsConsole console)
    {
        string traderLabel = TraderKind?.label.CapitalizeFirst() ?? "Orbital Trader";
        string title = $"Trade Satellite in Range: {OrbitalCallsign}";
        string colonyName = map.Parent?.LabelCap ?? "Colony";

        string text = $"An orbital trade satellite, {OrbitalCallsign} ({traderLabel}), has entered communication range of {colonyName}.\n\n" +
                      $"Faction: {Faction?.Name ?? "Independent"}\n" +
                      $"Communication Window: ~3.8 in-game hours\n\n" +
                      $"Use a Comms Console to open trade communications before the satellite drifts out of orbital range.";

        Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.PositiveEvent, new LookTargets(console));
    }

    private void NotifyDepartedOverpass(Map map)
    {
        string colonyName = map.Parent?.LabelCap ?? "colony";
        string text = $"Trade satellite {OrbitalCallsign} has moved out of orbital communication range of {colonyName}.";
        Messages.Message(text, MessageTypeDefOf.NeutralEvent, false);
    }

    #region ICommunicable Implementation

    public string GetCallLabel() => $"{OrbitalCallsign} ({Faction?.Name ?? "Independent"})";

    public string GetInfoText() => $"{OrbitalCallsign}\n{TraderKind?.label.CapitalizeFirst() ?? "Orbital Trader"}\nFaction: {Faction?.Name ?? "Independent"}";

    public Faction GetFaction() => Faction;

    public void TryOpenComms(Pawn myPawn)
    {
        EnsureGoodsGenerated();
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

    public override IEnumerable<Gizmo> GetGizmos()
    {
        foreach (Gizmo g in base.GetGizmos())
        {
            yield return g;
        }

        Map? homeMap = Find.Maps?.FirstOrDefault(m => m.IsPlayerHome && CoversTile(m.Tile));
        if (homeMap != null)
        {
            Building_CommsConsole? console = OrbitalVisibilityUtility.FindPoweredCommsConsole(homeMap);
            var cmd = new Command_Action
            {
                defaultLabel = $"Open Comms ({homeMap.Parent?.LabelCap ?? "Colony"})",
                defaultDesc = console != null
                    ? $"Establish direct orbital trade link via the powered Comms Console at {homeMap.Parent?.LabelCap ?? "the colony"}."
                    : $"The satellite is within range of {homeMap.Parent?.LabelCap ?? "the colony"}, but requires an operational powered Comms Console to establish communication.",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/Trade", false) ?? BaseContent.BadTex,
                action = () =>
                {
                    Pawn? negotiator = homeMap.mapPawns.FreeColonistsSpawned
                        .Where(p => !p.Downed && !p.InMentalState && (console == null || p.CanReach(console, PathEndMode.InteractionCell, Danger.Some)))
                        .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0)
                        .FirstOrDefault();

                    if (negotiator != null)
                    {
                        if (console != null)
                        {
                            console.GiveUseCommsJob(negotiator, this);
                        }
                        else
                        {
                            TryOpenComms(negotiator);
                        }
                    }
                    else
                    {
                        Messages.Message("No available colonist to operate the Comms Console.", MessageTypeDefOf.RejectInput, false);
                    }
                }
            };

            if (console == null)
            {
                cmd.Disable("No operational powered Comms Console.");
            }
            else if (!CanTradeNow)
            {
                cmd.Disable("Trade unavailable.");
            }

            yield return cmd;
        }
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

    public ThingOwner GetDirectlyHeldThings()
    {
        EnsureGoodsGenerated();
        return goods;
    }

    #endregion

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Deep.Look(ref goods, "goods", this);
        Scribe_Defs.Look(ref traderKind, "traderKind");
        Scribe_Values.Look(ref randomPriceFactorSeed, "randomPriceFactorSeed", 0);
        Scribe_Collections.Look(ref activeOverpassMapIds, "activeOverpassMapIds", LookMode.Value);

        goods ??= new ThingOwner<Thing>(this);
        activeOverpassMapIds ??= new();
    }
}
