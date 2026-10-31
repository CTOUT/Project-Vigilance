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
    private bool isConstructing = false;
    private int constructionTicksLeft = 0;
    private int constructionTicksTotal = 2500;
    private bool isDismantling = false;
    private int dismantleTicksLeft = 0;
    private int dismantleTicksTotal = 1500;
    private int cachedVantageBonus = 0;
    private string? customName = null;
    public ThingDef? stuffDef = null;
    private Material? cachedMat;

    public ThingDef? StuffDef
    {
        get => stuffDef;
        set => stuffDef = value;
    }

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

    public bool IsConstructing => isConstructing;
    public int ConstructionTicksLeft => constructionTicksLeft;
    public int ConstructionTicksTotal => constructionTicksTotal;

    public bool IsDismantling => isDismantling;
    public int DismantleTicksLeft => dismantleTicksLeft;
    public int DismantleTicksTotal => dismantleTicksTotal;

    public int CachedVantageBonus => cachedVantageBonus;

    public override Color ExpandingIconColor => Faction != null ? Faction.Color : Color.white;

    public override Material Material
    {
        get
        {
            if (cachedMat == null && def?.texture != null)
            {
                Color color = Faction != null ? Faction.Color : Color.white;
                cachedMat = MaterialPool.MatFrom(
                    def.texture,
                    ShaderDatabase.WorldOverlayTransparentLit,
                    color,
                    WorldMaterials.WorldObjectRenderQueue);
            }
            return cachedMat ?? base.Material;
        }
    }

    public override string Label
    {
        get
        {
            if (customName != null) return customName;
            string baseLabel = def != null ? base.Label : (kind == ListeningPostKind.AutomatedRelay ? "Automated Sensor Relay" : "Signal Beacon");
            if (kind == ListeningPostKind.SignalBeacon && stuffDef != null)
            {
                return $"{stuffDef.label} {baseLabel}";
            }
            return baseLabel;
        }
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

    public IntelligenceTier MaxIntelligenceTier => IntelligenceTier.Classified;

    public bool IsSensorActive => (Find.World?.worldObjects != null ? Spawned : true) && !Destroyed && !isSabotaged && !isConstructing && !isDismantling;

    public void SetTileRaw(PlanetTile t) => HarmonyLib.Traverse.Create(this).Field("tile").SetValue(t);
    public void SetSpawnedRaw(bool s) => HarmonyLib.Traverse.Create(this).Field("spawned").SetValue(s);

    public bool IsStationary => true;

    public bool IsTechnological => kind == ListeningPostKind.AutomatedRelay;

    public bool PenetratesTerrainMasking => false;

    public bool IsOceanicSonar => false;

    public bool TryGetDetectionDistance(IDetectableTarget target, out int distance)
    {
        distance = -1;
        if (!IsSensorActive || !SensorTile.Valid || !target.CurrentTile.Valid)
        {
            return false;
        }

        if (Find.World?.grid != null && SensorTile.Layer != target.CurrentTile.Layer)
        {
            return false;
        }

        int maxSteps = Mathf.Max(1, Mathf.RoundToInt(DetectionRadiusInTiles));
        int d = SensoryManager.DistanceCalculationOverride != null
            ? SensoryManager.DistanceCalculationOverride(SensorTile, target.CurrentTile, maxSteps)
            : (Find.World?.grid != null
                ? Find.World.grid.TraversalDistanceBetween(SensorTile, target.CurrentTile, passImpassable: true, maxDist: maxSteps)
                : Math.Abs(SensorTile.tileId - target.CurrentTile.tileId));

        if (d <= maxSteps)
        {
            distance = d;
            return true;
        }

        return false;
    }

    public bool CanDetect(IDetectableTarget target) => TryGetDetectionDistance(target, out _);

    #endregion

    #region Lifecycle, Ticking & Serialization

    public override void SpawnSetup()
    {
        base.SpawnSetup();
        cachedMat = null;
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

    protected override void Tick()
    {
        base.Tick();

        if (isConstructing)
        {
            if (HasPlayerCaravanPresent())
            {
                constructionTicksLeft--;
                if (constructionTicksLeft <= 0)
                {
                    FinishConstruction();
                }
            }
        }
        else if (isDismantling)
        {
            if (HasPlayerCaravanPresent(out Caravan? caravan))
            {
                dismantleTicksLeft--;
                if (dismantleTicksLeft <= 0)
                {
                    FinishDismantling(caravan);
                }
            }
        }
    }

    /// <summary>
    /// Optional override for testing player caravan presence in test harnesses where Find.World is uninitialized.
    /// </summary>
    public static System.Func<WorldObject_ListeningPost, Caravan?>? PlayerCaravanPresenceOverride { get; set; }

    public void TickForTesting() => Tick();

    public bool HasPlayerCaravanPresent() => HasPlayerCaravanPresent(out _);

    public bool HasPlayerCaravanPresent(out Caravan? caravan)
    {
        caravan = null;

        if (PlayerCaravanPresenceOverride != null)
        {
            caravan = PlayerCaravanPresenceOverride(this);
            return caravan != null;
        }

        var caravans = Find.World?.worldObjects?.Caravans;
        if (caravans == null) return false;
        for (int i = 0; i < caravans.Count; i++)
        {
            Caravan c = caravans[i];
            if (c != null && c.Spawned && c.Tile == Tile && c.Faction?.IsPlayer == true)
            {
                // Ensure caravan has at least one capable, conscious worker
                bool hasCapableWorker = false;
                List<Pawn>? pawns = null;
                try
                {
                    pawns = c.PawnsListForReading;
                }
                catch
                {
                    // Uninitialized mock
                }

                if (pawns != null)
                {
                    for (int p = 0; p < pawns.Count; p++)
                    {
                        Pawn pawn = pawns[p];
                        if (pawn != null && pawn.IsColonist && !pawn.Dead && !pawn.Downed)
                        {
                            hasCapableWorker = true;
                            break;
                        }
                    }
                }
                else
                {
                    hasCapableWorker = true;
                }

                if (hasCapableWorker)
                {
                    caravan = c;
                    return true;
                }
            }
        }
        return false;
    }

    public void StartConstruction(int ticks = 2500)
    {
        isConstructing = true;
        constructionTicksLeft = ticks;
        constructionTicksTotal = ticks;
        WorldDrawLayer_RadarCoverage.Instance?.SetDirty();
    }

    public void FinishConstruction()
    {
        isConstructing = false;
        constructionTicksLeft = 0;
        WorldDrawLayer_RadarCoverage.Instance?.SetDirty();
        string label = kind == ListeningPostKind.AutomatedRelay
            ? "Automated listening post"
            : (stuffDef != null ? $"{stuffDef.LabelCap} signal beacon" : "Signal beacon");
        if (Current.ProgramState == ProgramState.Playing)
        {
            Messages.Message($"{label} construction complete and surveillance grid active at Tile {Tile}.", MessageTypeDefOf.PositiveEvent, false);
        }
    }

    public void CancelConstruction(Caravan? caravan)
    {
        if (!isConstructing) return;

        // Refund 100% of construction materials
        if (kind == ListeningPostKind.AutomatedRelay)
        {
            if (caravan != null)
            {
                Thing steel = ThingMaker.MakeThing(ThingDefOf.Steel);
                steel.stackCount = OutpostMaterialUtility.AutomatedRelaySteelCost;
                CaravanInventoryUtility.GiveThing(caravan, steel);

                Thing comp = ThingMaker.MakeThing(ThingDefOf.ComponentIndustrial);
                comp.stackCount = OutpostMaterialUtility.AutomatedRelayComponentCost;
                CaravanInventoryUtility.GiveThing(caravan, comp);
            }
        }
        else
        {
            if (caravan != null)
            {
                ThingDef refundDef = stuffDef ?? ThingDefOf.WoodLog;
                int remaining = OutpostMaterialUtility.SignalBeaconWoodOrStoneCost;
                int stackLimit = Mathf.Max(1, refundDef.stackLimit);
                while (remaining > 0)
                {
                    Thing salvaged = ThingMaker.MakeThing(refundDef);
                    int countToTake = Mathf.Min(remaining, stackLimit);
                    salvaged.stackCount = countToTake;
                    remaining -= countToTake;
                    CaravanInventoryUtility.GiveThing(caravan, salvaged);
                }
            }
        }

        if (Current.ProgramState == ProgramState.Playing)
        {
            Messages.Message($"Construction of {LabelCap} cancelled. Materials refunded.", MessageTypeDefOf.CautionInput, false);
        }
        Destroy();
    }

    public void StartDismantling(int ticks = 1500)
    {
        if (isDismantling) return;
        isDismantling = true;
        dismantleTicksLeft = ticks;
        dismantleTicksTotal = ticks;
        WorldDrawLayer_RadarCoverage.Instance?.SetDirty();
    }

    public void CancelDismantling()
    {
        if (!isDismantling) return;
        isDismantling = false;
        dismantleTicksLeft = 0;
        WorldDrawLayer_RadarCoverage.Instance?.SetDirty();
        if (Current.ProgramState == ProgramState.Playing)
        {
            Messages.Message($"Dismantling of {LabelCap} cancelled. Installation operational.", MessageTypeDefOf.CautionInput, false);
        }
    }

    public void FinishDismantling(Caravan? workerCaravan)
    {
        isDismantling = false;
        dismantleTicksLeft = 0;
        if (workerCaravan != null)
        {
            Dismantle(workerCaravan);
        }
        else
        {
            Destroy();
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref kind, "kind", ListeningPostKind.AutomatedRelay);
        Scribe_Values.Look(ref isSabotaged, "isSabotaged", false);
        Scribe_Values.Look(ref isConstructing, "isConstructing", false);
        Scribe_Values.Look(ref constructionTicksLeft, "constructionTicksLeft", 0);
        Scribe_Values.Look(ref constructionTicksTotal, "constructionTicksTotal", 2500);
        Scribe_Values.Look(ref isDismantling, "isDismantling", false);
        Scribe_Values.Look(ref dismantleTicksLeft, "dismantleTicksLeft", 0);
        Scribe_Values.Look(ref dismantleTicksTotal, "dismantleTicksTotal", 1500);
        Scribe_Values.Look(ref customName, "customName");
        Scribe_Values.Look(ref cachedVantageBonus, "cachedVantageBonus", 0);
        Scribe_Defs.Look(ref stuffDef, "stuffDef");
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

        if (!HasRepairMaterials(caravan, out string missingDesc))
        {
            Messages.Message($"Cannot repair {LabelCap}: {missingDesc}", MessageTypeDefOf.RejectInput, false);
            return false;
        }

        ConsumeRepairMaterials(caravan);

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
            // Initial: 150 Wood/Stone -> 75% salvage: 112 items
            ThingDef refundDef = stuffDef ?? ThingDefOf.WoodLog;
            int remaining = 112;
            int stackLimit = Mathf.Max(1, refundDef.stackLimit);
            while (remaining > 0)
            {
                Thing salvaged = ThingMaker.MakeThing(refundDef);
                int countToTake = Mathf.Min(remaining, stackLimit);
                salvaged.stackCount = countToTake;
                remaining -= countToTake;
                CaravanInventoryUtility.GiveThing(caravan, salvaged);
            }
        }

        if (Current.ProgramState == ProgramState.Playing)
        {
            Messages.Message($"{LabelCap} dismantled. Salvaged materials recovered into caravan inventory.", MessageTypeDefOf.TaskCompletion, false);
        }
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
        string? baseStr = null;
        try
        {
            baseStr = base.GetInspectString();
        }
        catch
        {
            // Unit test harnesses without Verse.ModsConfig / RimWorld game paths
        }

        if (!string.IsNullOrEmpty(baseStr))
        {
            sb.AppendLine(baseStr);
        }

        string typeLabel = kind == ListeningPostKind.AutomatedRelay
            ? "Automated Sensor Relay"
            : (stuffDef != null ? $"Signal Beacon ({stuffDef.LabelCap})" : "Signal Beacon");
        sb.AppendLine($"Type: {typeLabel}");

        if (isConstructing)
        {
            int pct = Mathf.Clamp(Mathf.RoundToInt((1f - (float)constructionTicksLeft / Mathf.Max(1, constructionTicksTotal)) * 100f), 0, 99);
            float hoursLeft = constructionTicksLeft / 2500f;
            sb.AppendLine(HasPlayerCaravanPresent()
                ? $"Status: Under Construction ({pct}%, ~{hoursLeft:F1}h remaining)"
                : $"Status: Construction Paused ({pct}% complete - caravan required on-site)");
            sb.AppendLine("Requires visiting caravan at hex to construct.");
        }
        else if (isDismantling)
        {
            int pct = Mathf.Clamp(Mathf.RoundToInt((1f - (float)dismantleTicksLeft / Mathf.Max(1, dismantleTicksTotal)) * 100f), 0, 99);
            float hoursLeft = dismantleTicksLeft / 2500f;
            sb.AppendLine(HasPlayerCaravanPresent()
                ? $"Status: Dismantling ({pct}%, ~{hoursLeft:F1}h remaining)"
                : $"Status: Dismantling Paused ({pct}% complete - caravan required on-site)");
            sb.AppendLine("Requires visiting caravan at hex to dismantle.");
        }
        else if (isSabotaged)
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

        if (Faction?.IsPlayer == true)
        {
            if (isConstructing)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Cancel Construction",
                    defaultDesc = "Cancels construction of this outpost and refunds materials if caravan is present.",
                    icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", false) ?? BaseContent.WhiteTex,
                    action = () =>
                    {
                        HasPlayerCaravanPresent(out Caravan? caravan);
                        CancelConstruction(caravan);
                    }
                };
            }
            else if (isDismantling)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Cancel Dismantling",
                    defaultDesc = "Cancels dismantling of this outpost and restores operational telemetry.",
                    icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", false) ?? BaseContent.WhiteTex,
                    action = CancelDismantling
                };
            }
        }
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
            if (isConstructing)
            {
                yield return new FloatMenuOption($"Cancel Construction of {LabelCap} (Refund 100% Materials)", () =>
                {
                    CancelConstruction(caravan);
                }, MenuOptionPriority.High);
                yield break;
            }

            if (isDismantling)
            {
                yield return new FloatMenuOption($"Cancel Dismantling of {LabelCap}", () =>
                {
                    CancelDismantling();
                }, MenuOptionPriority.High);
                yield break;
            }

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

            yield return new FloatMenuOption($"Dismantle {LabelCap} (Takes time, recover 75% materials)", () =>
            {
                StartDismantling(1500);
            }, MenuOptionPriority.Low);
        }
    }

    #endregion
}
