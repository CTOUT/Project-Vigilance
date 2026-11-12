using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// World map arrival action that orders a player caravan to travel to a designated planetary tile
/// and immediately establish an unmanned listening post or signal beacon upon arrival.
/// </summary>
public class CaravanArrivalAction_ConstructOutpost : CaravanArrivalAction
{
    private PlanetTile destinationTile;
    private ListeningPostKind postKind;
    private ThingDef? stuffDef;

    public CaravanArrivalAction_ConstructOutpost()
    {
    }

    public CaravanArrivalAction_ConstructOutpost(PlanetTile tile, ListeningPostKind kind, ThingDef? stuff = null)
    {
        destinationTile = tile;
        postKind = kind;
        stuffDef = stuff;
    }

    public PlanetTile TargetTile => destinationTile;
    public ListeningPostKind Kind => postKind;
    public ThingDef? StuffDef => stuffDef;

    public override string Label => postKind == ListeningPostKind.AutomatedRelay
        ? "Establish Automated Listening Post"
        : (postKind == ListeningPostKind.AutomatedSonar
            ? "Establish Automated Sonar Station"
            : (stuffDef != null ? $"Construct Signal Beacon ({stuffDef.LabelCap})" : "Construct Signal Beacon"));

    public override string ReportString => postKind == ListeningPostKind.AutomatedRelay
        ? "travelling to establish automated listening post"
        : (postKind == ListeningPostKind.AutomatedSonar
            ? "travelling to establish automated sonar station"
            : (stuffDef != null ? $"travelling to construct signal beacon ({stuffDef.LabelCap})" : "travelling to construct signal beacon"));

    public override FloatMenuAcceptanceReport StillValid(Caravan caravan, PlanetTile destinationTile)
    {
        if (caravan == null)
        {
            return false;
        }

        try
        {
            if (!caravan.Spawned)
            {
                return false;
            }
        }
        catch
        {
            // Headless unit test mock without initialized Find.WorldObjects
        }

        PlanetTile target = destinationTile.Valid ? destinationTile : (this.destinationTile.Valid ? this.destinationTile : caravan.Tile);
        if (target.Valid)
        {
            if (postKind == ListeningPostKind.AutomatedSonar &&
                SonarTraversalUtility.CanEvaluateWorldGrid() &&
                !SonarTraversalUtility.HasCoastalAccess(target))
            {
                return FloatMenuAcceptanceReport.WithFailMessage("Automated sonar stations require a coastal tile adjacent to water.");
            }

            var worldObjects = Current.Game?.World?.worldObjects;
            bool hasCompletedPost = worldObjects?.AllWorldObjects != null &&
                           worldObjects.AllWorldObjects.OfType<WorldObject_ListeningPost>().Any(p => p.Tile == target && !p.IsConstructing);
            if (hasCompletedPost)
            {
                return FloatMenuAcceptanceReport.WithFailMessage("An active outpost already exists at this location.");
            }
        }

        // Note: Do not evict arrival action mid-transit on missing materials.
        // PatherTickInterval runs StillValid periodically while the caravan is walking across the globe.
        // Failing here causes Caravan_PathFollower.StopDead() to nullify caravan.arrivalAction mid-transit.
        // Full material verification and consumption occur deterministically upon arrival in Arrived(caravan).
        return true;
    }

    public override void Arrived(Caravan caravan)
    {
        if (caravan == null)
        {
            return;
        }

        try
        {
            if (!caravan.Spawned)
            {
                return;
            }
        }
        catch
        {
            // Headless unit test mock without initialized Find.WorldObjects
        }

        PlanetTile targetTile = caravan.Tile.Valid ? caravan.Tile : (destinationTile.Valid ? destinationTile : caravan.Tile);

        List<Pawn>? pawns = null;
        try
        {
            pawns = caravan.PawnsListForReading;
        }
        catch
        {
            // Headless unit test mock
        }

        int durationTicks = OutpostAssemblySpeedUtility.CalculateAssemblyDurationTicks(pawns, postKind);
        float durationHours = durationTicks / 2500f;

        if (postKind == ListeningPostKind.AutomatedRelay)
        {
            if (OutpostMaterialUtility.CanDeployAutomatedRelay(caravan, out string missing))
            {
                OutpostMaterialUtility.ConsumeAutomatedRelayDeployment(caravan);
                var def = DefDatabase<WorldObjectDef>.GetNamed("Vigilance_ListeningPost_Automated");
                var post = (WorldObject_ListeningPost)WorldObjectMaker.MakeWorldObject(def);
                post.Tile = targetTile;
                post.SetFaction(Faction.OfPlayer);
                post.Kind = ListeningPostKind.AutomatedRelay;
                post.StartConstruction(durationTicks);
                Find.WorldObjects?.Add(post);
                Messages.Message($"Caravan {caravan.LabelCap} arrived at Tile {targetTile}. Construction of automated listening post commenced (~{durationHours:F1} in-game hours).", MessageTypeDefOf.PositiveEvent, false);
                Find.LetterStack?.ReceiveLetter(
                    "Outpost Deployment: " + post.LabelCap,
                    $"Caravan {caravan.LabelCap} arrived at Tile {targetTile} and commenced construction of {post.LabelCap}.\n\nColonists are assembling the station (~{durationHours:F1} in-game hours). Keep the caravan on-site until assembly completes.",
                    LetterDefOf.PositiveEvent,
                    new LookTargets(post));
            }
            else
            {
                Messages.Message($"Caravan {caravan.LabelCap} arrived at Tile {targetTile} but lacks materials to deploy automated listening post ({missing}).", MessageTypeDefOf.RejectInput, false);
            }
        }
        else if (postKind == ListeningPostKind.AutomatedSonar)
        {
            if (SonarTraversalUtility.CanEvaluateWorldGrid() && !SonarTraversalUtility.HasCoastalAccess(targetTile))
            {
                Messages.Message($"Caravan {caravan.LabelCap} arrived at Tile {targetTile}, which lacks adjacent water required for an automated sonar station.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (OutpostMaterialUtility.CanDeployAutomatedSonar(caravan, out string missing))
            {
                OutpostMaterialUtility.ConsumeAutomatedSonarDeployment(caravan);
                var def = DefDatabase<WorldObjectDef>.GetNamedSilentFail("Vigilance_ListeningPost_Sonar")
                          ?? DefDatabase<WorldObjectDef>.GetNamed("Vigilance_ListeningPost_Automated");
                var post = (WorldObject_ListeningPost)WorldObjectMaker.MakeWorldObject(def);
                post.Tile = targetTile;
                post.SetFaction(Faction.OfPlayer);
                post.Kind = ListeningPostKind.AutomatedSonar;
                post.StartConstruction(durationTicks);
                Find.WorldObjects?.Add(post);
                Messages.Message($"Caravan {caravan.LabelCap} arrived at Tile {targetTile}. Construction of automated sonar station commenced (~{durationHours:F1} in-game hours).", MessageTypeDefOf.PositiveEvent, false);
                Find.LetterStack?.ReceiveLetter(
                    "Outpost Deployment: " + post.LabelCap,
                    $"Caravan {caravan.LabelCap} arrived at Tile {targetTile} and commenced construction of {post.LabelCap}.\n\nColonists are assembling the coastal sonar array (~{durationHours:F1} in-game hours). Keep the caravan on-site until assembly completes.",
                    LetterDefOf.PositiveEvent,
                    new LookTargets(post));
            }
            else
            {
                Messages.Message($"Caravan {caravan.LabelCap} arrived at Tile {targetTile} but lacks materials to deploy automated sonar station ({missing}).", MessageTypeDefOf.RejectInput, false);
            }
        }
        else
        {
            if (OutpostMaterialUtility.CanDeploySignalBeacon(caravan, out string missing))
            {
                ThingDef? consumed = OutpostMaterialUtility.ConsumeSignalBeaconDeployment(caravan, stuffDef);
                var def = DefDatabase<WorldObjectDef>.GetNamed("Vigilance_ListeningPost_Beacon");
                var post = (WorldObject_ListeningPost)WorldObjectMaker.MakeWorldObject(def);
                post.Tile = targetTile;
                post.SetFaction(Faction.OfPlayer);
                post.Kind = ListeningPostKind.SignalBeacon;
                post.stuffDef = consumed ?? stuffDef ?? ThingDefOf.WoodLog;
                post.StartConstruction(durationTicks);
                Find.WorldObjects?.Add(post);
                Messages.Message($"Caravan {caravan.LabelCap} arrived at Tile {targetTile}. Construction of signal beacon ({post.stuffDef.LabelCap}) commenced (~{durationHours:F1} in-game hours).", MessageTypeDefOf.PositiveEvent, false);
                Find.LetterStack?.ReceiveLetter(
                    "Outpost Deployment: " + post.LabelCap,
                    $"Caravan {caravan.LabelCap} arrived at Tile {targetTile} and commenced construction of {post.LabelCap}.\n\nColonists are assembling the beacon (~{durationHours:F1} in-game hours). Keep the caravan on-site until assembly completes.",
                    LetterDefOf.PositiveEvent,
                    new LookTargets(post));
            }
            else
            {
                Messages.Message($"Caravan {caravan.LabelCap} arrived at Tile {targetTile} but lacks materials to construct signal beacon ({missing}).", MessageTypeDefOf.RejectInput, false);
            }
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref destinationTile, "PV_destinationTile");
        Scribe_Values.Look(ref postKind, "PV_postKind");
        Scribe_Defs.Look(ref stuffDef, "PV_stuffDef");
    }
}
