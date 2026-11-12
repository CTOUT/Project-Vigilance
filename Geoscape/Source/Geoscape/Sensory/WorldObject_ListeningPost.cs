using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Geoscape.World;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Technological and architectural classification of a lightweight planetary listening post.
/// </summary>
public enum ListeningPostKind
{
    AutomatedRelay, // Industrial/Spacer automated radar relay (solar-powered, 12 rings detection)
    SignalBeacon,   // Neolithic/Medieval unmanned stone/timber signal beacon (optical, 6 rings + vantage)
    AutomatedSonar  // Industrial/Spacer coastal sonar installation (18 rings water-connected directional detection)
}

/// <summary>
/// Lightweight, zero-TPS planetary sensory outpost deployed directly on the WorldGrid.
/// Does NOT inherit from MapParent, avoiding tactical map generation and colonist garrison overhead.
/// Implements ISensorProvider to provide planetary radar/visual surveillance, respects terrain vantage,
/// and is vulnerable to sabotage by crossing hostile incursions.
/// </summary>
[StaticConstructorOnStartup]
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
    public int ConstructionProgressPct => Mathf.Clamp(Mathf.RoundToInt((1f - (float)constructionTicksLeft / Mathf.Max(1, constructionTicksTotal)) * 100f), 0, 99);

    public bool IsDismantling => isDismantling;
    public int DismantleTicksLeft => dismantleTicksLeft;
    public int DismantleTicksTotal => dismantleTicksTotal;

    public int CachedVantageBonus => cachedVantageBonus;

    public const float UprightQuadRotationAngle = -90f;
    public const float UprightPrintRotationAngle = 90f;
    private static MaterialPropertyBlock? sharedPropertyBlock;

    public override Color ExpandingIconColor
    {
        get
        {
            if (isConstructing)
            {
                float pulse = 0.65f + 0.35f * Mathf.Sin(Time.realtimeSinceStartup * 4f);
                return new Color(1f, 0.82f, 0.2f, pulse);
            }
            if (isSabotaged)
            {
                return new Color(1f, 0.3f, 0.3f);
            }
            return Faction != null ? Faction.Color : Color.white;
        }
    }

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

    public override void Draw()
    {
        float averageTileSize = Find.WorldGrid.AverageTileSize;
        float rawTransitionPct = ExpandableWorldObjectsUtility.RawTransitionPct;
        Material? mat = Material;
        if (mat == null)
        {
            return;
        }

        if (def.expandingIcon && rawTransitionPct > 0f)
        {
            Color color = mat.color;
            float num = 1f - rawTransitionPct;
            sharedPropertyBlock ??= new MaterialPropertyBlock();
            sharedPropertyBlock.SetColor(ShaderPropertyIDs.Color, new Color(color.r, color.g, color.b, color.a * num));
            WorldRendererUtility.DrawQuadTangentialToPlanet(
                DrawPos,
                0.7f * averageTileSize,
                DrawAltitude,
                mat,
                UprightQuadRotationAngle,
                counterClockwise: false,
                useSkyboxLayer: false,
                sharedPropertyBlock);
            return;
        }

        WorldRendererUtility.DrawQuadTangentialToPlanet(
            DrawPos,
            0.7f * averageTileSize,
            DrawAltitude,
            mat,
            UprightQuadRotationAngle);
    }

    public override string Label
    {
        get
        {
            if (customName != null) return customName;
            string baseLabel = def != null
                ? base.Label
                : (kind == ListeningPostKind.AutomatedRelay
                    ? "Automated Sensor Relay"
                    : (kind == ListeningPostKind.AutomatedSonar ? "Automated Sonar Station" : "Signal Beacon"));
            if (kind == ListeningPostKind.SignalBeacon && stuffDef != null)
            {
                baseLabel = $"{stuffDef.label} {baseLabel}";
            }

            if (isConstructing)
            {
                int pct = Mathf.Clamp(Mathf.RoundToInt((1f - (float)constructionTicksLeft / Mathf.Max(1, constructionTicksTotal)) * 100f), 0, 99);
                return $"{baseLabel} (Under Construction {pct}%)";
            }
            if (isDismantling)
            {
                return $"{baseLabel} (Dismantling)";
            }
            if (isSabotaged)
            {
                return $"{baseLabel} (Sabotaged)";
            }
            if (IsDisruptedByEnvironment(out string disruptionReason))
            {
                return $"{baseLabel} (Offline: {disruptionReason})";
            }

            return baseLabel;
        }
    }

    #region ISensorProvider Implementation

    public WorldObject? Source => this;

    public string SensorLabel => LabelCap;

    public PlanetTile SensorTile => Tile;

    /// <summary>
    /// Evaluates the detection and identification radii for a listening post kind given vantage bonus and operational status.
    /// </summary>
    public static (float detection, int identification) CalculateRadii(ListeningPostKind kind, int vantageBonus, bool isSabotaged = false)
    {
        if (isSabotaged)
        {
            return (0f, 0);
        }

        if (kind == ListeningPostKind.AutomatedRelay)
        {
            int bonus = vantageBonus > 0 ? Mathf.Min(2, vantageBonus) : 0;
            float det = 12f + bonus;
            int id = 5 + (bonus >= 2 ? 1 : 0);
            return (det, id);
        }
        else if (kind == ListeningPostKind.AutomatedSonar)
        {
            return (18f, 8);
        }
        else
        {
            float det = 6f + vantageBonus;
            int id = 0; // Unmanned signal beacon has 0 identification
            return (det, id);
        }
    }

    public float DetectionRadiusInTiles =>
        !IsSensorActive ? 0f : CalculateRadii(kind, cachedVantageBonus, isSabotaged).detection;

    public int IdentificationRadiusInTiles =>
        !IsSensorActive ? 0 : CalculateRadii(kind, cachedVantageBonus, isSabotaged).identification;

    public IntelligenceTier MaxIntelligenceTier => kind == ListeningPostKind.SignalBeacon
        ? IntelligenceTier.Contact
        : IntelligenceTier.Classified;

    public bool IsDisruptedByEnvironment(out string reason)
    {
        reason = string.Empty;
        if (kind == ListeningPostKind.SignalBeacon)
        {
            return false; // Signal Beacons use primitive mechanical/optical watchtowers and bonfires; immune to EMP, solar flares, and darkness
        }

        if (SensoryDisruptionUtility.IsSolarFlareActive(Tile))
        {
            reason = "Solar Flare";
            return true;
        }

        if (SensoryDisruptionUtility.IsEclipseOrProlongedDarknessActive(Tile))
        {
            reason = "Solar Eclipse / Darkness";
            return true;
        }

        return false;
    }

    public bool IsSensorActive =>
        (Find.World?.worldObjects != null ? Spawned : true) &&
        !Destroyed &&
        !isSabotaged &&
        !isConstructing &&
        !isDismantling &&
        !IsDisruptedByEnvironment(out _) &&
        (kind != ListeningPostKind.AutomatedSonar || !SonarTraversalUtility.CanEvaluateWorldGrid() || SonarTraversalUtility.HasCoastalAccess(Tile));

    public void SetTileRaw(PlanetTile t) => WorldObjectAccessUtility.SetTileRaw(this, t);
    public void SetSpawnedRaw(bool s) => WorldObjectAccessUtility.SetSpawnedRaw(this, s);

    public bool IsStationary => true;

    public bool IsTechnological => kind != ListeningPostKind.SignalBeacon;

    public bool PenetratesTerrainMasking => false;

    public bool IsOceanicSonar => kind == ListeningPostKind.AutomatedSonar;

    public bool TryGetDetectionDistance(IDetectableTarget target, out int distance) =>
        SensorDetectionUtility.TryGetDetectionDistance(this, target, out distance);

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

    private bool wasCaravanPresentDuringConstruction = true;
    private float constructionWorkAccumulator = 0f;
    private float dismantleWorkAccumulator = 0f;

    public float ConstructionWorkAccumulator => constructionWorkAccumulator;
    public float DismantleWorkAccumulator => dismantleWorkAccumulator;

    protected override void Tick()
    {
        base.Tick();

        if (isConstructing)
        {
            if (HasPlayerCaravanPresent(out Caravan? caravan))
            {
                if (!wasCaravanPresentDuringConstruction)
                {
                    wasCaravanPresentDuringConstruction = true;
                    if (Current.ProgramState == ProgramState.Playing)
                    {
                        Messages.Message($"Caravan on-site. Construction of {LabelCap} at Tile {Tile.tileId} resumed.", MessageTypeDefOf.PositiveEvent, false);
                    }
                }

                List<Pawn>? pawns = GetCaravanPawns(caravan);

                var (leadAwake, activeTeamSpeed, awakeCount, totalBuilders) =
                    OutpostAssemblySpeedUtility.CalculateActiveTeamSpeed(pawns, Tile);

                if (totalBuilders > 0 && awakeCount > 0)
                {
                    var (_, fullTeamSpeed) = OutpostAssemblySpeedUtility.CalculateTeamSpeed(pawns);
                    float speedRatio = fullTeamSpeed > 0f ? Mathf.Clamp01(activeTeamSpeed / fullTeamSpeed) : 1f;

                    constructionWorkAccumulator += speedRatio;
                    while (constructionWorkAccumulator >= 1f)
                    {
                        constructionWorkAccumulator -= 1f;
                        constructionTicksLeft--;
                        if (constructionTicksLeft <= 0)
                        {
                            FinishConstruction();
                            break;
                        }
                    }
                }
            }
            else
            {
                if (wasCaravanPresentDuringConstruction)
                {
                    wasCaravanPresentDuringConstruction = false;
                    if (Current.ProgramState == ProgramState.Playing)
                    {
                        Messages.Message($"Construction of {LabelCap} at Tile {Tile.tileId} paused. Caravan must remain on-site to finish assembly.", MessageTypeDefOf.CautionInput, false);
                    }
                }
            }
        }
        else if (isDismantling)
        {
            if (HasPlayerCaravanPresent(out Caravan? caravan))
            {
                List<Pawn>? pawns = GetCaravanPawns(caravan);

                var (leadAwake, activeTeamSpeed, awakeCount, totalBuilders) =
                    OutpostAssemblySpeedUtility.CalculateActiveTeamSpeed(pawns, Tile);

                if (totalBuilders > 0 && awakeCount > 0)
                {
                    var (_, fullTeamSpeed) = OutpostAssemblySpeedUtility.CalculateTeamSpeed(pawns);
                    float speedRatio = fullTeamSpeed > 0f ? Mathf.Clamp01(activeTeamSpeed / fullTeamSpeed) : 1f;

                    dismantleWorkAccumulator += speedRatio;
                    while (dismantleWorkAccumulator >= 1f)
                    {
                        dismantleWorkAccumulator -= 1f;
                        dismantleTicksLeft--;
                        if (dismantleTicksLeft <= 0)
                        {
                            FinishDismantling(caravan);
                            break;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Optional override for testing player caravan presence in test harnesses where Find.World is uninitialized.
    /// </summary>
    public static System.Func<WorldObject_ListeningPost, Caravan?>? PlayerCaravanPresenceOverride { get; set; }

    /// <summary>
    /// Optional hook for overriding the caravan's pawn roster during test runs.
    /// </summary>
    public static System.Func<Caravan, List<Pawn>?>? CaravanPawnsOverride { get; set; }

    /// <summary>
    /// Safely retrieves the colonist roster from a caravan, respecting test overrides.
    /// </summary>
    public static List<Pawn>? GetCaravanPawns(Caravan? caravan)
    {
        if (caravan == null) return null;
        if (CaravanPawnsOverride != null) return CaravanPawnsOverride(caravan);
        try
        {
            return caravan.PawnsListForReading;
        }
        catch
        {
            return null;
        }
    }

    public void TickForTesting() => Tick();

    public bool HasPlayerCaravanPresent() => HasPlayerCaravanPresent(out _);

    private static bool IsCaravanMoving(Caravan c)
    {
        if (c.pather == null) return false;
        try
        {
            return c.pather.Moving;
        }
        catch
        {
            return false;
        }
    }

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
            if (c != null && c.Spawned && c.Tile == Tile && c.Faction?.IsPlayer == true && !IsCaravanMoving(c))
            {
                // Ensure caravan has at least one capable, conscious worker
                bool hasCapableWorker = false;
                List<Pawn>? pawns = GetCaravanPawns(c);

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
            : (kind == ListeningPostKind.AutomatedSonar
                ? "Automated sonar station"
                : (stuffDef != null ? $"{stuffDef.LabelCap} signal beacon" : "Signal beacon"));
        if (Current.ProgramState == ProgramState.Playing)
        {
            Messages.Message($"{label} construction complete and surveillance grid active at Tile {Tile}.", MessageTypeDefOf.PositiveEvent, false);
            Find.LetterStack?.ReceiveLetter(
                "Outpost Operational: " + LabelCap,
                $"{label} construction complete at Tile {Tile}.\n\nPlanetary surveillance grid is now operational across {DetectionRadiusInTiles:F0} tiles.",
                LetterDefOf.PositiveEvent,
                new LookTargets(this));
        }
    }

    public void CancelConstruction(Caravan? caravan)
    {
        if (!isConstructing) return;
        isConstructing = false;
        constructionTicksLeft = 0;

        // Refund 100% of construction materials
        if (caravan != null)
        {
            if (kind == ListeningPostKind.AutomatedRelay)
            {
                OutpostMaterialUtility.GiveMaterialToCaravan(caravan, ThingDefOf.Steel, OutpostMaterialUtility.AutomatedRelaySteelCost);
                OutpostMaterialUtility.GiveMaterialToCaravan(caravan, ThingDefOf.ComponentIndustrial, OutpostMaterialUtility.AutomatedRelayComponentCost);
            }
            else if (kind == ListeningPostKind.AutomatedSonar)
            {
                OutpostMaterialUtility.GiveMaterialToCaravan(caravan, ThingDefOf.Steel, OutpostMaterialUtility.AutomatedSonarSteelCost);
                OutpostMaterialUtility.GiveMaterialToCaravan(caravan, ThingDefOf.ComponentIndustrial, OutpostMaterialUtility.AutomatedSonarComponentCost);
            }
            else
            {
                ThingDef refundDef = stuffDef ?? ThingDefOf.WoodLog;
                OutpostMaterialUtility.GiveMaterialToCaravan(caravan, refundDef, OutpostMaterialUtility.SignalBeaconWoodOrStoneCost);
            }
        }

        if (Current.ProgramState == ProgramState.Playing)
        {
            Messages.Message($"Construction of {LabelCap} cancelled. Materials refunded.", MessageTypeDefOf.CautionInput, false);
        }
        try
        {
            Destroy();
        }
        catch
        {
            // Unit test harnesses without full RimWorld game state
        }
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
        Scribe_Values.Look(ref constructionWorkAccumulator, "constructionWorkAccumulator", 0f);
        Scribe_Values.Look(ref isDismantling, "isDismantling", false);
        Scribe_Values.Look(ref dismantleTicksLeft, "dismantleTicksLeft", 0);
        Scribe_Values.Look(ref dismantleTicksTotal, "dismantleTicksTotal", 1500);
        Scribe_Values.Look(ref dismantleWorkAccumulator, "dismantleWorkAccumulator", 0f);
        Scribe_Values.Look(ref customName, "customName");
        Scribe_Values.Look(ref cachedVantageBonus, "cachedVantageBonus", 0);
        Scribe_Defs.Look(ref stuffDef, "stuffDef");
        Scribe_Values.Look(ref wasCaravanPresentDuringConstruction, "wasCaravanPresentDuringConstruction", true);
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
        if (isConstructing)
        {
            CancelConstruction(caravan);
            return;
        }

        if (caravan != null)
        {
            if (kind == ListeningPostKind.AutomatedRelay)
            {
                OutpostMaterialUtility.GiveMaterialToCaravan(caravan, ThingDefOf.Steel, OutpostMaterialUtility.AutomatedRelaySteelSalvage);
                OutpostMaterialUtility.GiveMaterialToCaravan(caravan, ThingDefOf.ComponentIndustrial, OutpostMaterialUtility.AutomatedRelayComponentSalvage);
            }
            else if (kind == ListeningPostKind.AutomatedSonar)
            {
                OutpostMaterialUtility.GiveMaterialToCaravan(caravan, ThingDefOf.Steel, OutpostMaterialUtility.AutomatedSonarSteelSalvage);
                OutpostMaterialUtility.GiveMaterialToCaravan(caravan, ThingDefOf.ComponentIndustrial, OutpostMaterialUtility.AutomatedSonarComponentSalvage);
            }
            else
            {
                ThingDef refundDef = stuffDef ?? ThingDefOf.WoodLog;
                OutpostMaterialUtility.GiveMaterialToCaravan(caravan, refundDef, OutpostMaterialUtility.SignalBeaconWoodSalvage);
            }
        }

        if (Current.ProgramState == ProgramState.Playing)
        {
            Messages.Message($"{LabelCap} dismantled. Salvaged materials recovered into caravan inventory.", MessageTypeDefOf.TaskCompletion, false);
        }
        try
        {
            Destroy();
        }
        catch
        {
            // Unit test harnesses without full RimWorld game state
        }
    }

    public bool HasRepairMaterials(Caravan caravan, out string missingDescription)
    {
        if (kind == ListeningPostKind.AutomatedRelay)
        {
            int steel = OutpostMaterialUtility.CountItem(caravan, ThingDefOf.Steel);
            int comp = OutpostMaterialUtility.CountItem(caravan, ThingDefOf.ComponentIndustrial);

            if (steel < OutpostMaterialUtility.AutomatedRelaySteelRepairCost || comp < OutpostMaterialUtility.AutomatedRelayComponentRepairCost)
            {
                missingDescription = $"Requires {OutpostMaterialUtility.AutomatedRelaySteelRepairCost} Steel (have {steel}) and {OutpostMaterialUtility.AutomatedRelayComponentRepairCost} Component (have {comp}).";
                return false;
            }

            missingDescription = string.Empty;
            return true;
        }
        else if (kind == ListeningPostKind.AutomatedSonar)
        {
            int steel = OutpostMaterialUtility.CountItem(caravan, ThingDefOf.Steel);
            int comp = OutpostMaterialUtility.CountItem(caravan, ThingDefOf.ComponentIndustrial);

            if (steel < OutpostMaterialUtility.AutomatedSonarSteelRepairCost || comp < OutpostMaterialUtility.AutomatedSonarComponentRepairCost)
            {
                missingDescription = $"Requires {OutpostMaterialUtility.AutomatedSonarSteelRepairCost} Steel (have {steel}) and {OutpostMaterialUtility.AutomatedSonarComponentRepairCost} Component (have {comp}).";
                return false;
            }

            missingDescription = string.Empty;
            return true;
        }
        else
        {
            int woodOrStone = OutpostMaterialUtility.CountWoodOrStone(caravan);
            if (woodOrStone < OutpostMaterialUtility.SignalBeaconWoodOrStoneRepairCost)
            {
                missingDescription = $"Requires {OutpostMaterialUtility.SignalBeaconWoodOrStoneRepairCost} Wood or Stone blocks (have {woodOrStone}).";
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
            OutpostMaterialUtility.TakeItem(caravan, ThingDefOf.Steel, OutpostMaterialUtility.AutomatedRelaySteelRepairCost);
            OutpostMaterialUtility.TakeItem(caravan, ThingDefOf.ComponentIndustrial, OutpostMaterialUtility.AutomatedRelayComponentRepairCost);
        }
        else if (kind == ListeningPostKind.AutomatedSonar)
        {
            OutpostMaterialUtility.TakeItem(caravan, ThingDefOf.Steel, OutpostMaterialUtility.AutomatedSonarSteelRepairCost);
            OutpostMaterialUtility.TakeItem(caravan, ThingDefOf.ComponentIndustrial, OutpostMaterialUtility.AutomatedSonarComponentRepairCost);
        }
        else
        {
            OutpostMaterialUtility.TakeWoodOrStone(caravan, OutpostMaterialUtility.SignalBeaconWoodOrStoneRepairCost);
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
            : (kind == ListeningPostKind.AutomatedSonar
                ? "Automated Coastal Sonar Station"
                : (stuffDef != null ? $"Signal Beacon ({stuffDef.LabelCap})" : "Signal Beacon"));
        sb.AppendLine($"Type: {typeLabel}");

        if (isConstructing)
        {
            int pct = Mathf.Clamp(Mathf.RoundToInt((1f - (float)constructionTicksLeft / Mathf.Max(1, constructionTicksTotal)) * 100f), 0, 99);
            float hoursLeft = constructionTicksLeft / 2500f;

            if (HasPlayerCaravanPresent(out Caravan? caravan))
            {
                List<Pawn>? pawns = GetCaravanPawns(caravan);

                var (leadAwake, activeTeamSpeed, awakeCount, totalBuilders) =
                    OutpostAssemblySpeedUtility.CalculateActiveTeamSpeed(pawns, Tile);

                if (totalBuilders == 0)
                {
                    sb.AppendLine($"Status: Construction Paused ({pct}% complete - no colonist capable of Construction on-site)");
                }
                else if (awakeCount == 0)
                {
                    bool allNightOwls = pawns != null && pawns.Where(OutpostAssemblySpeedUtility.IsCapableBuilder).All(ProjectVigilance.Geoscape.Patches.Caravan_NightResting_Patch.HasNightOwlTrait);
                    string restReason = allNightOwls ? "Resting - Daytime Sleep Period" : "Resting - Night/Sleep Period";
                    sb.AppendLine($"Status: Construction Paused ({pct}% complete - {restReason})");
                }
                else if (awakeCount < totalBuilders)
                {
                    var (_, fullTeamSpeed) = OutpostAssemblySpeedUtility.CalculateTeamSpeed(pawns);
                    float speedRatio = fullTeamSpeed > 0f ? (activeTeamSpeed / fullTeamSpeed) : 1f;
                    float effectiveHoursLeft = hoursLeft / Mathf.Max(0.01f, speedRatio);
                    sb.AppendLine($"Status: Under Construction ({pct}%, ~{effectiveHoursLeft:F1}h remaining - Reduced Shift ({awakeCount}/{totalBuilders} awake))");
                }
                else
                {
                    sb.AppendLine($"Status: Under Construction ({pct}%, ~{hoursLeft:F1}h remaining)");
                }
            }
            else
            {
                sb.AppendLine($"Status: Construction Paused ({pct}% complete - caravan required on-site)");
            }
            sb.AppendLine("Requires visiting caravan at hex to construct.");
        }
        else if (isDismantling)
        {
            int pct = Mathf.Clamp(Mathf.RoundToInt((1f - (float)dismantleTicksLeft / Mathf.Max(1, dismantleTicksTotal)) * 100f), 0, 99);
            float hoursLeft = dismantleTicksLeft / 2500f;

            if (HasPlayerCaravanPresent(out Caravan? caravan))
            {
                List<Pawn>? pawns = GetCaravanPawns(caravan);

                var (leadAwake, activeTeamSpeed, awakeCount, totalBuilders) =
                    OutpostAssemblySpeedUtility.CalculateActiveTeamSpeed(pawns, Tile);

                if (totalBuilders == 0)
                {
                    sb.AppendLine($"Status: Dismantling Paused ({pct}% complete - no colonist capable of Construction on-site)");
                }
                else if (awakeCount == 0)
                {
                    bool allNightOwls = pawns != null && pawns.Where(OutpostAssemblySpeedUtility.IsCapableBuilder).All(ProjectVigilance.Geoscape.Patches.Caravan_NightResting_Patch.HasNightOwlTrait);
                    string restReason = allNightOwls ? "Resting - Daytime Sleep Period" : "Resting - Night/Sleep Period";
                    sb.AppendLine($"Status: Dismantling Paused ({pct}% complete - {restReason})");
                }
                else if (awakeCount < totalBuilders)
                {
                    var (_, fullTeamSpeed) = OutpostAssemblySpeedUtility.CalculateTeamSpeed(pawns);
                    float speedRatio = fullTeamSpeed > 0f ? (activeTeamSpeed / fullTeamSpeed) : 1f;
                    float effectiveHoursLeft = hoursLeft / Mathf.Max(0.01f, speedRatio);
                    sb.AppendLine($"Status: Dismantling ({pct}%, ~{effectiveHoursLeft:F1}h remaining - Reduced Shift ({awakeCount}/{totalBuilders} awake))");
                }
                else
                {
                    sb.AppendLine($"Status: Dismantling ({pct}%, ~{hoursLeft:F1}h remaining)");
                }
            }
            else
            {
                sb.AppendLine($"Status: Dismantling Paused ({pct}% complete - caravan required on-site)");
            }
            sb.AppendLine("Requires visiting caravan at hex to dismantle.");
        }
        else if (isSabotaged)
        {
            sb.AppendLine("Status: Sabotaged / Signal Lost");
            sb.AppendLine("Requires maintenance caravan to repair and restore telemetry.");
        }
        else if (IsDisruptedByEnvironment(out string disruptionReason))
        {
            sb.AppendLine($"Status: Offline ({disruptionReason})");
            if (disruptionReason.Contains("Solar Flare"))
            {
                sb.AppendLine("Intense coronal radiation has knocked out electronic telemetry.");
            }
            else
            {
                sb.AppendLine("Photovoltaic arrays offline due to obscured sunlight.");
            }
        }
        else
        {
            sb.AppendLine("Status: Operational");
            if (kind == ListeningPostKind.SignalBeacon)
            {
                sb.AppendLine($"Coverage Rings: Detection {DetectionRadiusInTiles:F0} tiles (Unmanned: 0 tiles identification)");
            }
            else
            {
                sb.AppendLine($"Coverage Rings: Detection {DetectionRadiusInTiles:F0} tiles, Identification {IdentificationRadiusInTiles} tiles");
            }

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
                if (caravan.pather != null && caravan.pather.Moving)
                {
                    yield return new FloatMenuOption($"Halt & resume construction of {LabelCap} ({ConstructionProgressPct}% complete)", () =>
                    {
                        caravan.pather.StopDead();
                        Messages.Message($"Caravan {caravan.LabelCap} halted on-site to resume construction of {LabelCap}.", caravan, MessageTypeDefOf.TaskCompletion, false);
                    }, MenuOptionPriority.High);
                }

                yield return new FloatMenuOption($"Cancel Construction of {LabelCap} (Refund 100% Materials)", () =>
                {
                    CancelConstruction(caravan);
                }, MenuOptionPriority.High);
                yield break;
            }

            if (isDismantling)
            {
                if (caravan.pather != null && caravan.pather.Moving)
                {
                    yield return new FloatMenuOption($"Halt & resume dismantling of {LabelCap}", () =>
                    {
                        caravan.pather.StopDead();
                        Messages.Message($"Caravan {caravan.LabelCap} halted on-site to resume dismantling of {LabelCap}.", caravan, MessageTypeDefOf.TaskCompletion, false);
                    }, MenuOptionPriority.High);
                }

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
