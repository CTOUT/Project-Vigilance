using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Core.Migration;

/// <summary>
/// Safe placeholder WorldObject absorbing entities (such as satellites, orbital hubs, or sonar stations)
/// from savegames whose owning Project Vigilance module is no longer active.
/// Prevents Scribe cross-reference and rendering errors until the player chooses to reload with the module
/// or cleanly retire the orphaned objects.
/// </summary>
public class MissingModuleWorldObjectStub : WorldObject
{
    private string? originalDefName;
    private string? originalClassName;
    private string? satelliteCallsign;
    private string? stationCallsign;
    private int parentStationId = -1;
    private string? kind;

    public string? OriginalDefName
    {
        get => originalDefName;
        set => originalDefName = value;
    }

    public string? OriginalClassName
    {
        get => originalClassName;
        set => originalClassName = value;
    }

    public string? Callsign => !string.IsNullOrEmpty(satelliteCallsign) ? satelliteCallsign : stationCallsign;
    public int ParentStationId => parentStationId;
    public string? Kind => kind;

    public override bool SelectableNow => false;
    public override bool VisibleInBackground => false;

    public override void Draw()
    {
        // Intentionally invisible while awaiting migration resolution
    }

    public override void Print(LayerSubMesh subMesh)
    {
        // Intentionally omitted from static world mesh batching
    }

    public override void SpawnSetup()
    {
        // Guard against uninitialized engine managers in headless unit tests (Rule 5)
        if (Find.World != null && Find.WorldObjects != null)
        {
            base.SpawnSetup();
        }
    }

    public override void PostRemove()
    {
        if (Find.World != null && Find.WorldObjects != null)
        {
            base.PostRemove();
        }
    }

    public override void ExposeData()
    {
        if (Find.World != null && Find.WorldObjects != null)
        {
            base.ExposeData();
        }
        else
        {
            Scribe_Values.Look(ref ID, "ID", -1);
            PlanetTile t = Tile;
            Scribe_Values.Look(ref t, "tile", PlanetTile.Invalid);
            Tile = t;
            Scribe_Defs.Look(ref def, "def");
        }

        Scribe_Values.Look(ref originalDefName, "originalDefName");
        Scribe_Values.Look(ref originalClassName, "originalClassName");
        Scribe_Values.Look(ref satelliteCallsign, "satelliteCallsign");
        Scribe_Values.Look(ref stationCallsign, "stationCallsign");
        Scribe_Values.Look(ref parentStationId, "parentStationId", -1);
        Scribe_Values.Look(ref kind, "kind");
    }
}
