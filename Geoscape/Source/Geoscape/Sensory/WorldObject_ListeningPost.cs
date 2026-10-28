using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Technological and architectural classification of a lightweight planetary listening post.
/// </summary>
public enum ListeningPostKind
{
    AutomatedRelay, // Industrial/Spacer automated radar relay (solar-powered, 12 rings detection)
    SignalBeacon    // Neolithic/Medieval unmanned stone/timber signal beacon (optical, 6 rings + vantage)
}

/// <summary>
/// Lightweight, zero-TPS planetary sensory outpost deployed directly on the WorldGrid.
/// Does NOT inherit from MapParent, avoiding tactical map generation and colonist garrison overhead.
/// Implements ISensorProvider to provide planetary radar/visual surveillance, respects terrain vantage,
/// and is vulnerable to sabotage by crossing hostile incursions.
/// </summary>
public class WorldObject_ListeningPost : WorldObject, ISensorProvider
{
    private ListeningPostKind kind = ListeningPostKind.AutomatedRelay;
    private bool isSabotaged = false;
    private int cachedVantageBonus = 0;
    private string? customName = null;

    public ListeningPostKind Kind
    {
        get => kind;
        set => kind = value;
    }

    public bool IsSabotaged
    {
        get => isSabotaged;
        set
        {
            if (isSabotaged != value)
            {
                isSabotaged = value;
                WorldDrawLayer_RadarCoverage.Instance?.SetDirty();
            }
        }
    }

    public int CachedVantageBonus => cachedVantageBonus;

    public override string Label
    {
        get => customName ?? base.Label;
    }

    #region ISensorProvider Implementation

    public WorldObject? Source => this;

    public string SensorLabel => LabelCap;

    public PlanetTile SensorTile => Tile;

    public float DetectionRadiusInTiles
    {
        get
        {
            if (!IsSensorActive)
            {
                return 0f;
            }

            if (kind == ListeningPostKind.AutomatedRelay)
            {
                // Baseline 12 traversal rings; elevated terrain grants +1 to +2 rings
                int bonus = cachedVantageBonus > 0 ? Mathf.Min(2, cachedVantageBonus) : 0;
                return 12f + bonus;
            }

            // Signal Beacon: Baseline 6 rings + full optical vantage bonus (+1 to +3 rings)
            return 6f + cachedVantageBonus;
        }
    }

    public int IdentificationRadiusInTiles
    {
        get
        {
            if (!IsSensorActive)
            {
                return 0;
            }

            if (kind == ListeningPostKind.AutomatedRelay)
            {
                // Automated Relay: 5 rings baseline, +1 ring on elevated ground (Large Hills / Mountains)
                return 5 + (cachedVantageBonus >= 2 ? 1 : 0);
            }

            // Signal Beacon: 3 rings baseline + optical vantage bonus (+1 for hills, +2 for mountains)
            return 3 + SensoryTerrainUtility.GetIdentificationVantageBonus(cachedVantageBonus);
        }
    }

    public IntelligenceTier MaxIntelligenceTier => IntelligenceTier.Contact;

    public bool IsSensorActive => (Find.World?.worldObjects != null ? Spawned : true) && !Destroyed && !isSabotaged;

    public void SetTileRaw(PlanetTile t) => HarmonyLib.Traverse.Create(this).Field("tile").SetValue(t);
    public void SetSpawnedRaw(bool s) => HarmonyLib.Traverse.Create(this).Field("spawned").SetValue(s);

    public bool IsStationary => true;

    public bool IsTechnological => kind == ListeningPostKind.AutomatedRelay;

    public bool PenetratesTerrainMasking => false;

    public bool IsOceanicSonar => false;

    public bool CanDetect(IDetectableTarget target)
    {
        if (!IsSensorActive || !SensorTile.Valid || !target.CurrentTile.Valid)
        {
            return false;
        }

        if (Find.World?.grid != null && SensorTile.Layer != target.CurrentTile.Layer)
        {
            return false;
        }

        int maxSteps = Mathf.Max(1, Mathf.RoundToInt(DetectionRadiusInTiles));
        if (SensoryManager.DistanceCalculationOverride != null)
        {
            return SensoryManager.DistanceCalculationOverride(SensorTile, target.CurrentTile, maxSteps) <= maxSteps;
        }

        if (Find.World?.grid != null)
        {
            return Find.World.grid.TraversalDistanceBetween(SensorTile, target.CurrentTile, passImpassable: true, maxDist: maxSteps) <= maxSteps;
        }

        return Math.Abs(SensorTile.tileId - target.CurrentTile.tileId) <= maxSteps;
    }

    #endregion

    #region Lifecycle & Serialization

    public override void SpawnSetup()
    {
        base.SpawnSetup();
        cachedVantageBonus = SensoryTerrainUtility.GetVantageBonus(Tile);
        SensoryManager.Instance?.RegisterSensor(this);
        WorldDrawLayer_RadarCoverage.Instance?.SetDirty();
    }

    public override void PostRemove()
    {
        SensoryManager.Instance?.DeregisterSensor(this);
        WorldDrawLayer_RadarCoverage.Instance?.SetDirty();
        base.PostRemove();
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref kind, "kind", ListeningPostKind.AutomatedRelay);
        Scribe_Values.Look(ref isSabotaged, "isSabotaged", false);
        Scribe_Values.Look(ref customName, "customName");
        Scribe_Values.Look(ref cachedVantageBonus, "cachedVantageBonus", 0);
    }

    #endregion

    #region Sabotage, Repair & Dismantle

    /// <summary>
    /// Sabotages and disables the listening post when occupied or raided by a hostile incursion.
    /// Dispatches an early warning notification to the player.
    /// </summary>
    public void Sabotage(Faction? hostileFaction = null)
    {
        if (isSabotaged || !Spawned || Destroyed)
        {
            return;
        }

        isSabotaged = true;
        WorldDrawLayer_RadarCoverage.Instance?.SetDirty();

        string factionStr = hostileFaction != null ? $"by {hostileFaction.Name}" : "by hostile forces";
        Find.LetterStack.ReceiveLetter(
            "Contact Lost: " + LabelCap,
            $"Telemetry signal from {LabelCap} at hex {Tile} has ceased. Telemetry indicates the station was compromised and sabotaged {factionStr}.\n\nA caravan can be dispatched to repair and reactivate the installation.",
            LetterDefOf.ThreatSmall,
            new LookTargets(this));
    }

    /// <summary>
    /// Repairs a sabotaged listening post, reactivating its planetary surveillance grid.
    /// Consumes 25 Steel & 1 Component for Automated Relays, or 30 Wood/Stone for Signal Beacons.
    /// </summary>
    public bool TryRepair(Caravan caravan)
    {
        if (!isSabotaged)
        {
            return false;
        }

        if (kind == ListeningPostKind.AutomatedRelay)
        {
            if (!HasRepairMaterials(caravan, out string missingDesc))
            {
                Messages.Message($"Cannot repair {LabelCap}: {missingDesc}", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            ConsumeRepairMaterials(caravan);
        }
        else
        {
            if (!HasRepairMaterials(caravan, out string missingDesc))
            {
                Messages.Message($"Cannot repair {LabelCap}: {missingDesc}", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            ConsumeRepairMaterials(caravan);
        }

        isSabotaged = false;
        WorldDrawLayer_RadarCoverage.Instance?.SetDirty();
        Messages.Message($"{LabelCap} repaired and operational telemetry restored.", MessageTypeDefOf.PositiveEvent, false);
        return true;
    }

    /// <summary>
    /// Dismantles the listening post, recovering 75% of initial construction materials
    /// into the visiting caravan's inventory.
    /// </summary>
    public void Dismantle(Caravan caravan)
    {
        if (kind == ListeningPostKind.AutomatedRelay)
        {
            // Initial: 100 Steel, 4 Components -> 75% salvage: 75 Steel, 3 Components
            Thing steel = ThingMaker.MakeThing(ThingDefOf.Steel);
            steel.stackCount = 75;
            CaravanInventoryUtility.GiveThing(caravan, steel);

            Thing comp = ThingMaker.MakeThing(ThingDefOf.ComponentIndustrial);
            comp.stackCount = 3;
            CaravanInventoryUtility.GiveThing(caravan, comp);
        }
        else
        {
            // Initial: 150 Wood/Stone -> 75% salvage: 112 Wood
            Thing wood = ThingMaker.MakeThing(ThingDefOf.WoodLog);
            wood.stackCount = 112;
            CaravanInventoryUtility.GiveThing(caravan, wood);
        }

        Messages.Message($"{LabelCap} dismantled. Salvaged materials recovered into caravan inventory.", MessageTypeDefOf.TaskCompletion, false);
        Destroy();
    }

    public bool HasRepairMaterials(Caravan caravan, out string missingDescription)
    {
        if (kind == ListeningPostKind.AutomatedRelay)
        {
            int steel = OutpostMaterialUtility.CountItem(caravan, ThingDefOf.Steel);
            int comp = OutpostMaterialUtility.CountItem(caravan, ThingDefOf.ComponentIndustrial);

            if (steel < 25 || comp < 1)
            {
                missingDescription = $"Requires 25 Steel (have {steel}) and 1 Component (have {comp}).";
                return false;
            }

            missingDescription = string.Empty;
            return true;
        }
        else
        {
            int woodOrStone = OutpostMaterialUtility.CountWoodOrStone(caravan);
            if (woodOrStone < 30)
            {
                missingDescription = $"Requires 30 Wood or Stone blocks (have {woodOrStone}).";
                return false;
            }

            missingDescription = string.Empty;
            return true;
        }
    }

    public void ConsumeRepairMaterials(Caravan caravan)
    {
        if (kind == ListeningPostKind.AutomatedRelay)
        {
            OutpostMaterialUtility.TakeItem(caravan, ThingDefOf.Steel, 25);
            OutpostMaterialUtility.TakeItem(caravan, ThingDefOf.ComponentIndustrial, 1);
        }
        else
        {
            OutpostMaterialUtility.TakeWoodOrStone(caravan, 30);
        }
    }

    #endregion

    #region Telemetry & UX

    public override string GetInspectString()
    {
        var sb = new StringBuilder();
        string baseStr = base.GetInspectString();
        if (!string.IsNullOrEmpty(baseStr))
        {
            sb.AppendLine(baseStr);
        }

        string typeLabel = kind == ListeningPostKind.AutomatedRelay ? "Automated Sensor Relay" : "Signal Beacon";
        sb.AppendLine($"Type: {typeLabel}");

        if (isSabotaged)
        {
            sb.AppendLine("Status: Sabotaged / Signal Lost");
            sb.AppendLine("Requires maintenance caravan to repair and restore telemetry.");
        }
        else
        {
            sb.AppendLine("Status: Operational");
            sb.AppendLine($"Coverage Rings: Detection {DetectionRadiusInTiles:F0} tiles, Identification {IdentificationRadiusInTiles} tiles");

            if (cachedVantageBonus > 0)
            {
                sb.AppendLine($"Terrain Vantage: +{cachedVantageBonus} rings ({Tile.Tile?.hilliness.ToString() ?? "Elevated"})");
            }
        }

        return sb.ToString().TrimEnd();
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        foreach (Gizmo gizmo in base.GetGizmos())
        {
            yield return gizmo;
        }

        // Camera Jump
        yield return new Command_Action
        {
            defaultLabel = "Focus Camera",
            defaultDesc = "Centers the geoscape camera on this listening post.",
            icon = ContentFinder<Texture2D>.Get("UI/Commands/LaunchReport", false) ?? BaseContent.WhiteTex,
            action = () => CameraJumper.TryJump(Tile)
        };
    }

    public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan)
    {
        foreach (FloatMenuOption opt in base.GetFloatMenuOptions(caravan))
        {
            yield return opt;
        }

        if (caravan == null || !caravan.IsPlayerControlled)
        {
            yield break;
        }

        if (caravan.Tile == Tile)
        {
            if (isSabotaged)
            {
                bool canRepair = HasRepairMaterials(caravan, out string missingDesc);
                string repairLabel = canRepair
                    ? $"Repair {LabelCap}"
                    : $"Repair {LabelCap} ({missingDesc})";

                yield return new FloatMenuOption(repairLabel, () =>
                {
                    TryRepair(caravan);
                }, MenuOptionPriority.High)
                {
                    Disabled = !canRepair
                };
            }

            yield return new FloatMenuOption($"Dismantle {LabelCap} (Recover 75% Materials)", () =>
            {
                Dismantle(caravan);
            }, MenuOptionPriority.Low);
        }
    }

    #endregion
}
