using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.World;

public class ChoiceLetter_TacticalEncounter : ChoiceLetter
{
    public Map? battleMap;
    public WorldObject? targetWorldObject;
    public PlanetTile targetTile = PlanetTile.Invalid;
    public bool canReformCaravan;
    public string? battleButtonLabel;
    public string? worldButtonLabel;

    public TaggedString label
    {
        get => Label;
        set
        {
            Label = value;
            title = value.Resolve();
        }
    }

    public TaggedString text
    {
        get => Text;
        set => Text = value;
    }

    public override IEnumerable<DiaOption> Choices
    {
        get
        {
            // Option 1: Enter the Battlescape / Local Map
            if (battleMap != null && battleMap.Index >= 0)
            {
                string label = !string.IsNullOrEmpty(battleButtonLabel) ? battleButtonLabel : "Go to Battlescape";
                DiaOption jumpToMap = new DiaOption(label)
                {
                    action = () =>
                    {
                        Pawn? colonist = battleMap.mapPawns?.FreeColonistsSpawned?.FirstOrDefault();
                        if (colonist != null)
                        {
                            CameraJumper.TryJumpAndSelect(colonist);
                        }
                        else
                        {
                            CameraJumper.TryJump(battleMap.Center, battleMap);
                        }
                    },
                    resolveTree = true
                };
                yield return jumpToMap;
            }

            // Option 2: View on the Geoscape / World Map
            if ((targetWorldObject != null && targetWorldObject.Spawned) || targetTile.Valid)
            {
                string label = !string.IsNullOrEmpty(worldButtonLabel) ? worldButtonLabel : "Go to cell (Geoscape)";
                DiaOption jumpToWorld = new DiaOption(label)
                {
                    action = () =>
                    {
                        if (targetWorldObject != null && targetWorldObject.Spawned)
                        {
                            CameraJumper.TryJumpAndSelect(targetWorldObject);
                        }
                        else if (targetTile.Valid)
                        {
                            CameraJumper.TryJump(targetTile);
                        }
                    },
                    resolveTree = true
                };
                yield return jumpToWorld;
            }

            // Option 3: Reform Caravan (if applicable)
            if (canReformCaravan && battleMap != null && battleMap.Index >= 0)
            {
                DiaOption reformOption = new DiaOption("Reform Caravan Now")
                {
                    action = () =>
                    {
                        Pawn? colonist = battleMap.mapPawns?.FreeColonistsSpawned?.FirstOrDefault();
                        if (colonist != null)
                        {
                            CameraJumper.TryJumpAndSelect(colonist);
                        }
                        IncursionTacticalRetreatUtility.OpenReformCaravanDialog(battleMap);
                    },
                    resolveTree = true
                };
                yield return reformOption;
            }

            // Option 4: Close
            yield return Option_Close;
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref battleMap, "battleMap");
        Scribe_References.Look(ref targetWorldObject, "targetWorldObject");
        Scribe_Values.Look(ref targetTile, "targetTile");
        Scribe_Values.Look(ref canReformCaravan, "canReformCaravan");
        Scribe_Values.Look(ref battleButtonLabel, "battleButtonLabel");
        Scribe_Values.Look(ref worldButtonLabel, "worldButtonLabel");
    }
}
