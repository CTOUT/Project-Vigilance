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
        : (stuffDef != null ? $"Construct Signal Beacon ({stuffDef.LabelCap})" : "Construct Signal Beacon");

    public override string ReportString => postKind == ListeningPostKind.AutomatedRelay
        ? "travelling to establish automated listening post"
        : (stuffDef != null ? $"travelling to construct signal beacon ({stuffDef.LabelCap})" : "travelling to construct signal beacon");

    public override FloatMenuAcceptanceReport StillValid(Caravan caravan, PlanetTile destinationTile)
    {
        if (caravan == null || !caravan.Spawned)
        {
            return false;
        }

        PlanetTile target = destinationTile.Valid ? destinationTile : (this.destinationTile.Valid ? this.destinationTile : caravan.Tile);
        if (target.Valid)
        {
            var worldObjects = Current.Game?.World?.worldObjects;
            bool hasPost = worldObjects?.AllWorldObjects != null &&
                           worldObjects.AllWorldObjects.OfType<WorldObject_ListeningPost>().Any(p => p.Tile == target);
            if (hasPost)
            {
                return FloatMenuAcceptanceReport.WithFailMessage("An outpost already exists at this location.");
            }
        }

        if (postKind == ListeningPostKind.AutomatedRelay)
        {
            if (!OutpostMaterialUtility.CanDeployAutomatedRelay(caravan, out string missing))
            {
                return FloatMenuAcceptanceReport.WithFailMessage(missing);
            }
        }
        else
        {
            if (!OutpostMaterialUtility.CanDeploySignalBeacon(caravan, out string missing))
            {
                return FloatMenuAcceptanceReport.WithFailMessage(missing);
            }
        }

        return true;
    }

    public override void Arrived(Caravan caravan)
    {
        if (caravan == null || !caravan.Spawned)
        {
            return;
        }

        PlanetTile targetTile = caravan.Tile.Valid ? caravan.Tile : (destinationTile.Valid ? destinationTile : caravan.Tile);

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
                post.StartConstruction(2500);
                Find.WorldObjects?.Add(post);
                Messages.Message($"Caravan {caravan.LabelCap} arrived at Tile {targetTile}. Construction of automated listening post commenced.", MessageTypeDefOf.PositiveEvent, false);
            }
            else
            {
                Messages.Message($"Caravan {caravan.LabelCap} arrived at Tile {targetTile} but lacks materials to deploy automated listening post ({missing}).", MessageTypeDefOf.RejectInput, false);
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
                post.StartConstruction(2500);
                Find.WorldObjects?.Add(post);
                Messages.Message($"Caravan {caravan.LabelCap} arrived at Tile {targetTile}. Construction of signal beacon ({post.stuffDef.LabelCap}) commenced.", MessageTypeDefOf.PositiveEvent, false);
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
