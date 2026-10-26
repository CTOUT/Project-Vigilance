using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.World;

public class IncursionRetreatManager : WorldComponent
{
    public static IncursionRetreatManager? Instance { get; private set; }

    public class RetreatBatch : IExposable
    {
        public PlanetTile originTile;
        public string settlementName = string.Empty;
        public Faction? faction;
        public List<Pawn> fleeingPawns = new();
        public List<Pawn> kidnappedColonists = new();
        public int timeoutTick;

        public void ExposeData()
        {
            int tileId = originTile.tileId;
            Scribe_Values.Look(ref tileId, "originTileId", 0);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                originTile = (PlanetTile)tileId;
            }
            Scribe_Values.Look(ref settlementName, "settlementName", string.Empty);
            Scribe_References.Look(ref faction, "faction");
            Scribe_Collections.Look(ref fleeingPawns, "fleeingPawns", LookMode.Reference);
            Scribe_Collections.Look(ref kidnappedColonists, "kidnappedColonists", LookMode.Reference);
            Scribe_Values.Look(ref timeoutTick, "timeoutTick", 0);
        }
    }

    public class IncarcerationRecord : IExposable
    {
        public Pawn? captive;
        public PlanetTile settlementTile;
        public Faction? faction;
        public int captureGameTick;

        public void ExposeData()
        {
            Scribe_References.Look(ref captive, "captive");
            int tileId = settlementTile.tileId;
            Scribe_Values.Look(ref tileId, "settlementTileId", 0);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                settlementTile = (PlanetTile)tileId;
            }
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref captureGameTick, "captureGameTick", 0);
        }
    }

    private List<RetreatBatch> activeBatches = new();
    private List<IncarcerationRecord> incarceratedColonists = new();

    public static void RecordSettlementIncarceration(Pawn captive, PlanetTile settlementTile, Faction? faction)
    {
        if (Instance == null || captive == null) return;
        Instance.incarceratedColonists.Add(new IncarcerationRecord
        {
            captive = captive,
            settlementTile = settlementTile,
            faction = faction,
            captureGameTick = Find.TickManager.TicksGame
        });
        Log.Message($"[Project Vigilance] Colonist {captive.LabelShort} registered as incarcerated at settlement tile {settlementTile}.");
    }

    public static List<Pawn> GetIncarceratedColonistsAt(PlanetTile tile)
    {
        if (Instance == null) return new List<Pawn>();
        return Instance.incarceratedColonists
            .Where(r => r.settlementTile == tile && r.captive != null && !r.captive.Dead)
            .Select(r => r.captive!)
            .ToList();
    }

    public static void RemoveIncarceratedColonistsAt(PlanetTile tile)
    {
        if (Instance == null) return;
        Instance.incarceratedColonists.RemoveAll(r => r.settlementTile == tile);
    }

    public IncursionRetreatManager(RimWorld.Planet.World world) : base(world)
    {
        Instance = this;
    }

    public override void FinalizeInit(bool fromLoad)
    {
        base.FinalizeInit(fromLoad);
        Instance = this;
    }

    public static void RegisterFleeingPawn(Pawn pawn, Map map, Pawn? captive)
    {
        if (Instance == null || pawn == null || map == null || pawn.Faction == null)
        {
            return;
        }

        Instance.RegisterPawnInternal(pawn, map, captive);
    }

    private void RegisterPawnInternal(Pawn pawn, Map map, Pawn? captive)
    {
        Faction faction = pawn.Faction;
        PlanetTile mapTile = map.Tile;
        string mapLabel = map.Parent?.LabelCap ??
                          Find.WorldObjects?.SettlementAt(mapTile)?.LabelCap ??
                          Find.WorldObjects?.MapParentAt(mapTile)?.LabelCap ??
                          (map.IsPlayerHome ? "your colony" : $"settlement at tile {mapTile.tileId}");

        RetreatBatch? batch = activeBatches.FirstOrDefault(b => b.originTile == mapTile && b.faction == faction);

        if (batch == null)
        {
            // Check if a retreating incursion from this map and faction is already active on the geoscape nearby
            WorldObject_Incursion? existingIncursion = Find.WorldObjects?.AllWorldObjects
                .OfType<WorldObject_Incursion>()
                .FirstOrDefault(inc => inc.Spawned && inc.IsRetreating && inc.Faction == faction && inc.OriginTile == mapTile && inc.CurrentPathIndex <= 1);

            if (existingIncursion != null)
            {
                var fleeingList = existingIncursion.RetreatingPawns ?? new List<Pawn>();
                if (!fleeingList.Contains(pawn))
                {
                    fleeingList.Add(pawn);
                    existingIncursion.SetRetreatingPawns(fleeingList);
                }

                if (captive != null)
                {
                    var captivesList = existingIncursion.KidnappedColonists ?? new List<Pawn>();
                    if (!captivesList.Contains(captive))
                    {
                        captivesList.Add(captive);
                        existingIncursion.SetKidnappedColonists(captivesList);
                        if (faction.kidnapped != null && faction.kidnapped.KidnappedPawnsListForReading.Contains(captive))
                        {
                            faction.kidnapped.RemoveKidnappedPawn(captive);
                        }
                        Messages.Message($"Fleeing {pawn.LabelShort} carrying captive {captive.LabelShort} has joined retreating {existingIncursion.LabelCap}.", existingIncursion, MessageTypeDefOf.NegativeEvent);
                    }
                }
                else
                {
                    Messages.Message($"Fleeing {pawn.LabelShort} has joined retreating {existingIncursion.LabelCap}.", existingIncursion, MessageTypeDefOf.NeutralEvent);
                }
                return;
            }

            batch = new RetreatBatch
            {
                originTile = mapTile,
                settlementName = mapLabel,
                faction = faction,
                timeoutTick = Find.TickManager.TicksGame + 1500 // 25 seconds departure window
            };
            activeBatches.Add(batch);
        }
        else
        {
            // Extend timeout as more pawns exit
            batch.timeoutTick = Find.TickManager.TicksGame + 900;
        }

        if (!batch.fleeingPawns.Contains(pawn))
        {
            batch.fleeingPawns.Add(pawn);
        }

        if (captive != null && !batch.kidnappedColonists.Contains(captive))
        {
            batch.kidnappedColonists.Add(captive);
            if (faction.kidnapped != null && faction.kidnapped.KidnappedPawnsListForReading.Contains(captive))
            {
                faction.kidnapped.RemoveKidnappedPawn(captive);
            }
            Log.Message($"[Project Vigilance] Colonist {captive.LabelShort} abducted by fleeing {pawn.LabelShort} from {faction.Name}. Intercepted vanilla instant kidnap for physical retreat.");
        }
    }

    public override void WorldComponentTick()
    {
        base.WorldComponentTick();

        if (activeBatches.Count == 0)
        {
            return;
        }

        int curTick = Find.TickManager.TicksGame;

        for (int i = activeBatches.Count - 1; i >= 0; i--)
        {
            RetreatBatch batch = activeBatches[i];

            if (curTick >= batch.timeoutTick || !batch.originTile.Valid)
            {
                FinalizeBatch(batch);
                activeBatches.RemoveAt(i);
            }
        }
    }

    private void FinalizeBatch(RetreatBatch batch)
    {
        if (!batch.originTile.Valid || batch.faction == null || batch.fleeingPawns.Count == 0)
        {
            return;
        }

        PlanetTile originTile = batch.originTile;

        // Resolve retreat route back to faction stronghold or wilderness staging area
        if (!IncursionOriginResolver.TryResolveRetreatRoute(originTile, batch.faction, out PlanetTile destinationTile, out var path, out float speed))
        {
            Log.Warning($"[Project Vigilance] Could not resolve retreat destination for {batch.faction.Name}. Pawns disbanded.");
            return;
        }

        WorldObjectDef incursionDef = DefDatabase<WorldObjectDef>.GetNamedSilentFail("Vigilance_Incursion")
                                      ?? DefDatabase<WorldObjectDef>.GetNamed("Caravan");

        var incursion = (WorldObject_Incursion)WorldObjectMaker.MakeWorldObject(incursionDef);

        IncidentParms parms = new IncidentParms
        {
            target = Find.World,
            faction = batch.faction,
            points = batch.fleeingPawns.Count * 80f
        };

        incursion.SetupIncursion(
            IncidentDefOf.RaidEnemy,
            parms,
            originTile,
            destinationTile,
            path,
            speed
        );

        incursion.IsRetreating = true;
        incursion.SetRetreatingPawns(new List<Pawn>(batch.fleeingPawns));

        if (batch.kidnappedColonists.Count > 0)
        {
            incursion.SetKidnappedColonists(new List<Pawn>(batch.kidnappedColonists));
        }

        // Retreating raiders departing the player's colony are immediately identified and tracked
        if (SensoryManager.Instance != null && incursion.TrackingId <= 0)
        {
            incursion.TrackingId = SensoryManager.Instance.AllocateContactTrackingId();
        }
        incursion.CurrentIntelligenceTier = IntelligenceTier.Classified;
        incursion.IsIdentified = true;

        Find.WorldObjects.Add(incursion);

        // Dispatch alert letter to the player
        string contactLabel = incursion.LabelCap;
        string factionName = batch.faction.Name;
        string settlementName = !string.IsNullOrWhiteSpace(batch.settlementName)
            ? batch.settlementName
            : (Find.WorldObjects?.SettlementAt(originTile)?.LabelCap ??
               Find.WorldObjects?.MapParentAt(originTile)?.LabelCap ??
               (Find.Maps != null && Find.Maps.Any(m => m.Tile == originTile && m.IsPlayerHome) ? "your colony" : $"settlement at tile {originTile.tileId}"));

        Settlement? destSettlement = Find.WorldObjects?.SettlementAt(destinationTile);
        string destName = destSettlement != null
            ? destSettlement.LabelCap
            : (Find.WorldObjects?.MapParentAt(destinationTile)?.LabelCap ??
               (destinationTile.Valid ? $"stronghold at tile {destinationTile.tileId}" : "their territory"));

        if (batch.kidnappedColonists.Count > 0)
        {
            string captivesList = string.Join(", ", batch.kidnappedColonists.Select(p => p.LabelShort));
            string title = "Kidnappers in Retreat: " + contactLabel;
            string text = $"Forces from {factionName} have fled {settlementName} carrying captured colonist(s): {captivesList}!\n\n" +
                          $"They are retreating overland towards {destName} as {contactLabel}.\n\n" +
                          $"Dispatch a rapid-response pursuit force immediately to intercept them before they reach {destName}.";

            Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.ThreatBig, incursion);
        }
        else
        {
            string title = "Raiders in Retreat: " + contactLabel;
            string text = $"The surviving raiders from {factionName} have fled {settlementName} and are retreating overland towards {destName}.\n\n" +
                          $"Their force has been designated {contactLabel} on the Geoscape.";

            Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.NeutralEvent, incursion);
        }

        Log.Message($"[Project Vigilance] Spawned retreating incursion {incursion.TrackingId} ({incursion.Label}) with {batch.fleeingPawns.Count} pawns and {batch.kidnappedColonists.Count} captive(s).");
    }

    public override void ExposeData()
    {
        base.ExposeData();

        if (Scribe.mode == LoadSaveMode.Saving)
        {
            incarceratedColonists.RemoveAll(r => r.captive == null || r.captive.Dead || r.captive.Destroyed);
            for (int i = activeBatches.Count - 1; i >= 0; i--)
            {
                activeBatches[i].fleeingPawns.RemoveAll(p => p == null || p.Dead || p.Destroyed);
                activeBatches[i].kidnappedColonists.RemoveAll(p => p == null || p.Dead || p.Destroyed);
                if (activeBatches[i].fleeingPawns.Count == 0)
                {
                    activeBatches.RemoveAt(i);
                }
            }
        }

        Scribe_Collections.Look(ref activeBatches, "activeBatches", LookMode.Deep);
        Scribe_Collections.Look(ref incarceratedColonists, "incarceratedColonists", LookMode.Deep);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            activeBatches ??= new List<RetreatBatch>();
            incarceratedColonists ??= new List<IncarcerationRecord>();
            incarceratedColonists.RemoveAll(r => r.captive == null || r.captive.Dead || r.captive.Destroyed);
            Instance = this;
        }
    }
}
