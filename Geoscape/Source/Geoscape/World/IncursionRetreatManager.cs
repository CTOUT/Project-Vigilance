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
        public Map? map;
        public Faction? faction;
        public List<Pawn> fleeingPawns = new();
        public List<Pawn> kidnappedColonists = new();
        public int timeoutTick;

        public void ExposeData()
        {
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Collections.Look(ref fleeingPawns, "fleeingPawns", LookMode.Reference);
            Scribe_Collections.Look(ref kidnappedColonists, "kidnappedColonists", LookMode.Deep);
            Scribe_Values.Look(ref timeoutTick, "timeoutTick", 0);
        }
    }

    private List<RetreatBatch> activeBatches = new();

    public IncursionRetreatManager(RimWorld.Planet.World world) : base(world)
    {
        Instance = this;
    }

    public override void FinalizeInit(bool fromLoad)
    {
        base.FinalizeInit(fromLoad);
        Instance = this;
    }

    public static void RegisterFleeingPawn(Pawn pawn, Pawn? captive)
    {
        if (Instance == null || pawn == null || pawn.Map == null || pawn.Faction == null)
        {
            return;
        }

        Instance.RegisterPawnInternal(pawn, captive);
    }

    private void RegisterPawnInternal(Pawn pawn, Pawn? captive)
    {
        Map map = pawn.Map;
        Faction faction = pawn.Faction;

        RetreatBatch? batch = activeBatches.FirstOrDefault(b => b.map == map && b.faction == faction);

        if (batch == null)
        {
            batch = new RetreatBatch
            {
                map = map,
                faction = faction,
                timeoutTick = Find.TickManager.TicksGame + 600 // 10 seconds departure window
            };
            activeBatches.Add(batch);
        }
        else
        {
            // Extend timeout as more pawns exit
            batch.timeoutTick = Find.TickManager.TicksGame + 450;
        }

        if (!batch.fleeingPawns.Contains(pawn))
        {
            batch.fleeingPawns.Add(pawn);
        }

        if (captive != null && !batch.kidnappedColonists.Contains(captive))
        {
            batch.kidnappedColonists.Add(captive);
            faction.kidnapped?.RemoveKidnappedPawn(captive);
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

            if (curTick >= batch.timeoutTick || batch.map == null || !Find.Maps.Contains(batch.map))
            {
                FinalizeBatch(batch);
                activeBatches.RemoveAt(i);
            }
        }
    }

    private void FinalizeBatch(RetreatBatch batch)
    {
        if (batch.map == null || batch.faction == null || batch.fleeingPawns.Count == 0)
        {
            return;
        }

        PlanetTile originTile = batch.map.Tile;

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

        Find.WorldObjects.Add(incursion);

        // Dispatch alert letter to the player
        string contactLabel = incursion.LabelCap;
        string factionName = batch.faction.Name;
        string settlementName = batch.map.Parent?.LabelCap ?? "the colony";
        string destName = destinationTile.Valid && destinationTile.Layer != null
            ? "their stronghold"
            : "their territory";

        if (batch.kidnappedColonists.Count > 0)
        {
            string captivesList = string.Join(", ", batch.kidnappedColonists.Select(p => p.LabelShort));
            string title = "Kidnappers in Retreat: " + contactLabel;
            string text = $"Forces from {factionName} have fled {settlementName} carrying captured colonist(s): {captivesList}!\n\n" +
                          $"They are retreating overland towards {destName} as {contactLabel}.\n\n" +
                          $"Dispatch a rapid-response pursuit force immediately to intercept them before they reach the enemy stronghold.";

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
        Scribe_Collections.Look(ref activeBatches, "activeBatches", LookMode.Deep);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            activeBatches ??= new List<RetreatBatch>();
            Instance = this;
        }
    }
}
