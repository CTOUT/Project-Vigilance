using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Building component representing a planetary early-warning radar dish or sensor installation.
/// Implements ISensorProvider to interface with SensoryManager, manages interferometric array stacking,
/// and dynamically modulates power consumption based on active threat tracking.
/// </summary>
public class CompRadar : ThingComp, ISensorProvider
{
    private CompPowerTrader? cachedPowerTrader;
    private int cachedTrackedCount;
    private int cachedVantageBonus;
    private int lastTrackedUpdateTick = -1;
    private bool lastSigintActive;
    private int lastOperationalDishes = -1;
    private int lastSigintDishes = -1;

    public CompProperties_Radar Props => (CompProperties_Radar)props;

    public CompPowerTrader? PowerTrader => cachedPowerTrader ??= parent?.GetComp<CompPowerTrader>();

    public bool ReceivesVantageBonus => Props.receivesVantageBonus;

    public int CachedVantageBonus => cachedVantageBonus;

    /// <summary>
    /// Checks whether an operational Signals Intelligence (SIGINT) Decryption Console
    /// is linked to this radar installation via CompAffectedByFacilities.
    /// Requires this dish to be powered and at least one operational dish directly wired to SIGINT.
    /// </summary>
    public bool HasActiveSigintConsole => IsSensorActive && SigintOperationalDishCount > 0;

    /// <summary>
    /// Counts operational, powered phased array dishes on the map that are directly linked
    /// to an operational, powered SIGINT Decryption Console.
    /// Determines the active SIGINT correlator bandwidth and penetration radius.
    /// </summary>
    public int SigintOperationalDishCount
    {
        get
        {
            if (parent?.Map == null)
            {
                return 0;
            }

            int count = 0;
            var buildings = parent.Map.listerBuildings?.AllBuildingsColonistOfDef(parent.def);
            if (buildings != null && buildings.Count > 0)
            {
                for (int i = 0; i < buildings.Count; i++)
                {
                    Building b = buildings[i];
                    if (b != null && b.Spawned && !b.Destroyed && IsDirectlyLinkedToActiveSigint(b))
                    {
                        count++;
                    }
                }
            }
            else if (parent.Map.listerThings != null)
            {
                var things = parent.Map.listerThings.ThingsOfDef(parent.def);
                if (things != null)
                {
                    for (int i = 0; i < things.Count; i++)
                    {
                        Thing t = things[i];
                        if (t != null && t.Spawned && !t.Destroyed && IsDirectlyLinkedToActiveSigint(t))
                        {
                            count++;
                        }
                    }
                }
            }

            return count;
        }
    }

    /// <summary>
    /// Maximum traversal ring distance within which this phased array can penetrate mountain terrain masking
    /// and resolve Tier 4 (Analysed) telemetry. Scales with the number of operational dishes wired to SIGINT.
    /// 1 wired dish = 10 rings (single-dish aperture).
    /// 2 wired dishes = 17 rings (dual-dish baseline).
    /// 3+ wired dishes = 22 rings (tri-dish full compound aperture).
    /// </summary>
    public int SigintPenetrationRadiusInTiles =>
        HasActiveSigintConsole
            ? Props.ResolveIdentificationRadius(SigintOperationalDishCount, cachedVantageBonus)
            : (Props.penetratesTerrainMasking ? IdentificationRadiusInTiles : 0);

    public static bool IsDirectlyLinkedToActiveSigint(Thing building)
    {
        if (building == null || !building.Spawned || building.Destroyed)
        {
            return false;
        }

        // CRITICAL: The radar dish itself MUST be active (powered, not broken down)
        CompRadar? radar = building.TryGetComp<CompRadar>();
        if (radar == null || !radar.IsSensorActive)
        {
            return false;
        }

        var affected = building.TryGetComp<CompAffectedByFacilities>();
        if (affected == null)
        {
            return false;
        }

        var linked = affected.LinkedFacilitiesListForReading;
        if (linked == null || linked.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < linked.Count; i++)
        {
            Thing facility = linked[i];
            if (facility != null && facility.Spawned && !facility.Destroyed &&
                facility.def.defName == "Geoscape_SigintConsole")
            {
                var sigint = facility.TryGetComp<CompSigintConsole>();
                if (sigint != null && sigint.IsOperational)
                {
                    return true;
                }

                if (affected.IsFacilityActive(facility))
                {
                    return true;
                }
            }
        }

        return false;
    }

    #region ISensorProvider Implementation

    public WorldObject? Source => parent?.Map?.Parent;

    public string SensorLabel => parent?.Map?.Parent != null
        ? $"{parent.Map.Parent.LabelCap} {Props.radarLabel}"
        : Props.radarLabel;

    public PlanetTile SensorTile => parent?.Map != null
        ? parent.Map.Tile
        : (parent?.Map?.Parent?.Tile ?? PlanetTile.Invalid);

    public float DetectionRadiusInTiles => Props.detectionRadius + (Props.receivesVantageBonus ? cachedVantageBonus : 0);

    public IntelligenceTier MaxIntelligenceTier =>
        HasActiveSigintConsole ? IntelligenceTier.Analysed : Props.maxIntelligenceTier;

    public bool IsStationary => true;

    public bool IsTechnological => parent?.def?.techLevel >= TechLevel.Industrial;

    public bool PenetratesTerrainMasking =>
        Props.penetratesTerrainMasking || SigintPenetrationRadiusInTiles > 0;

    public bool IsOceanicSonar => Props.isOceanicSonar;

    public bool IsSensorActive
    {
        get
        {
            if (parent == null || !parent.Spawned || parent.Destroyed || parent.Map == null)
            {
                return false;
            }

            if (parent.IsBrokenDown())
            {
                return false;
            }

            if (parent.GetComp<CompStunnable>()?.StunHandler?.Stunned == true)
            {
                return false;
            }

            if (SensoryDisruptionUtility.IsSolarFlareActive(SensorTile))
            {
                return false;
            }

            if (PowerTrader != null && !PowerTrader.PowerOn)
            {
                return false;
            }

            return true;
        }
    }

    private string GetOfflineReason()
    {
        if (parent == null || !parent.Spawned || parent.Destroyed)
        {
            return "Unspawned";
        }

        if (parent.IsBrokenDown())
        {
            return "Faulty / Broken Down";
        }

        if (parent.GetComp<CompStunnable>()?.StunHandler?.Stunned == true)
        {
            return "EMP Disrupted / Stunned";
        }

        if (SensoryDisruptionUtility.IsSolarFlareActive(SensorTile))
        {
            return "Solar Flare Disruption";
        }

        if (PowerTrader != null && !PowerTrader.PowerOn)
        {
            return "Unpowered";
        }

        return "Offline";
    }

    /// <summary>
    /// Counts operational radar dishes sharing this def on the local map.
    /// Multi-dish arrays achieve synthetic aperture interferometry, extending identification range.
    /// </summary>
    public int OperationalDishCount
    {
        get
        {
            if (parent?.Map == null)
            {
                return IsSensorActive ? 1 : 0;
            }

            int count = 0;
            var buildings = parent.Map.listerBuildings?.AllBuildingsColonistOfDef(parent.def);
            if (buildings != null && buildings.Count > 0)
            {
                for (int i = 0; i < buildings.Count; i++)
                {
                    Building building = buildings[i];
                    CompRadar? radar = building.GetComp<CompRadar>();
                    if (radar != null && radar.IsSensorActive)
                    {
                        count++;
                    }
                }
            }
            else if (parent.Map.listerThings != null)
            {
                var things = parent.Map.listerThings.ThingsOfDef(parent.def);
                if (things != null)
                {
                    for (int i = 0; i < things.Count; i++)
                    {
                        CompRadar? radar = things[i].TryGetComp<CompRadar>();
                        if (radar != null && radar.IsSensorActive)
                        {
                            count++;
                        }
                    }
                }
            }

            return count > 0 ? count : (IsSensorActive ? 1 : 0);
        }
    }

    /// <summary>
    /// Resolves the effective optical and electronic identification horizon based on interferometric dish count.
    /// 1 dish = identificationRadiusDish1
    /// 2 dishes = identificationRadiusDish2
    /// 3+ dishes = identificationRadiusDish3
    /// </summary>
    public int IdentificationRadiusInTiles => Props.ResolveIdentificationRadius(OperationalDishCount, cachedVantageBonus);

    public bool TryGetDetectionDistance(IDetectableTarget target, out int distance) =>
        SensorDetectionUtility.TryGetDetectionDistance(this, target, out distance);

    public bool CanDetect(IDetectableTarget target) => TryGetDetectionDistance(target, out _);

    #endregion

    #region Target Tracking & Power Modulation

    /// <summary>
    /// Counts active detectable targets currently within the radar's detection radius.
    /// Updates cachedTrackedCount for high-performance zero-allocation UI telemetry.
    /// </summary>
    public int CountTrackedTargets()
    {
        if (!IsSensorActive || !SensorTile.Valid)
        {
            cachedTrackedCount = 0;
            return 0;
        }

        int currentTick = (Current.Game != null && Find.TickManager != null) ? Find.TickManager.TicksGame : -1;
        if (currentTick >= 0 && lastTrackedUpdateTick == currentTick)
        {
            return cachedTrackedCount;
        }

        SensoryManager? sensory = SensoryManager.Instance;
        if (sensory != null)
        {
            var targets = sensory.ActiveTargets;
            if (targets == null || targets.Count == 0)
            {
                cachedTrackedCount = 0;
                lastTrackedUpdateTick = currentTick;
                return 0;
            }

            int count = 0;
            foreach (IDetectableTarget target in targets)
            {
                if (target is WorldObject wo && (wo.Destroyed || !wo.Spawned))
                {
                    continue;
                }

                if (CanDetect(target))
                {
                    count++;
                }
            }

            cachedTrackedCount = count;
            lastTrackedUpdateTick = currentTick;
            return count;
        }

        // Standalone fallback when SensoryManager WorldComponent is not present
        if (Find.WorldObjects?.AllWorldObjects != null)
        {
            int count = 0;
            foreach (WorldObject wo in Find.WorldObjects.AllWorldObjects)
            {
                if (wo is IDetectableTarget target && !wo.Destroyed && wo.Spawned && CanDetect(target))
                {
                    count++;
                }
            }

            cachedTrackedCount = count;
            lastTrackedUpdateTick = currentTick;
            return count;
        }

        cachedTrackedCount = 0;
        lastTrackedUpdateTick = currentTick;
        return 0;
    }

    /// <summary>
    /// Scales radar power draw based on active tracking load:
    /// Peacetime standby draw when clear, surging to active power draw when tracking targets.
    /// </summary>
    public void UpdatePowerState()
    {
        if (PowerTrader == null)
        {
            return;
        }

        int tracked = CountTrackedTargets();
        float targetDraw = (tracked > 0) ? Props.activePowerDraw : Props.standbyPowerDraw;

        if (Mathf.Abs(PowerTrader.PowerOutput - (-targetDraw)) > 0.01f)
        {
            PowerTrader.PowerOutput = -targetDraw;
        }
    }

    #endregion

    #region Lifecycle Hooks

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        if (Props.receivesVantageBonus && SensorTile.Valid)
        {
            cachedVantageBonus = SensoryTerrainUtility.GetVantageBonus(SensorTile);
        }
        else
        {
            cachedVantageBonus = 0;
        }

        lastSigintActive = HasActiveSigintConsole;
        lastOperationalDishes = OperationalDishCount;
        lastSigintDishes = SigintOperationalDishCount;
        SensoryManager.Instance?.RegisterSensor(this);
        UpdatePowerState();
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        base.PostDeSpawn(map, mode);
        SensoryManager.Instance?.DeregisterSensor(this);
    }

    public override void CompTick()
    {
        base.CompTick();
        if (parent.IsHashIntervalTick(SensoryManager.SweepIntervalTicks))
        {
            UpdatePowerState();

            bool sigintActive = HasActiveSigintConsole;
            int dishCount = OperationalDishCount;
            int sigintDishes = SigintOperationalDishCount;
            if (sigintActive != lastSigintActive || dishCount != lastOperationalDishes || sigintDishes != lastSigintDishes)
            {
                lastSigintActive = sigintActive;
                lastOperationalDishes = dishCount;
                lastSigintDishes = sigintDishes;
                WorldDrawLayer_RadarCoverage.Instance?.SetDirty();
            }
        }
    }

    public override void CompTickRare()
    {
        base.CompTickRare();
        UpdatePowerState();
    }

    #endregion

    #region Telemetry & Gizmos

    public override string? CompInspectStringExtra()
    {
        if (parent?.Map == null)
        {
            return null;
        }

        var sb = new StringBuilder();
        int dishCount = OperationalDishCount;
        int idRadius = IdentificationRadiusInTiles;

        if (!IsSensorActive)
        {
            string reason = GetOfflineReason();
            sb.AppendLine($"Radar Status: Offline ({reason})");
            sb.AppendLine("Power Mode: Offline (0 W)");
        }
        else
        {
            int tracked = cachedTrackedCount;
            string powerMode = tracked > 0 ? "Active Tracking" : "Standby";
            float draw = tracked > 0 ? Props.activePowerDraw : Props.standbyPowerDraw;

            sb.AppendLine($"Radar Status: Operational ({powerMode})");
            sb.AppendLine($"Power Mode: {powerMode} ({draw:F0} W)");
        }

        float detRadius = DetectionRadiusInTiles;
        sb.AppendLine($"Coverage Rings: Detection {detRadius:F0} tiles, Identification {idRadius} tiles ({dishCount} operational {(dishCount == 1 ? "dish" : "dishes")})");

        if (Props.receivesVantageBonus && cachedVantageBonus > 0)
        {
            sb.AppendLine($"Terrain Vantage: +{cachedVantageBonus} rings ({SensorTile.Tile?.hilliness.ToString() ?? "Elevated"})");
        }

        if (HasActiveSigintConsole)
        {
            string linkType = IsDirectlyLinkedToActiveSigint(parent) ? "Directly Linked" : "Array Linked";
            int sigintDishes = SigintOperationalDishCount;
            int sigintRadius = SigintPenetrationRadiusInTiles;
            sb.AppendLine($"SIGINT Decryption: {linkType} ({sigintRadius} tiles bandwidth, {sigintDishes} active {(sigintDishes == 1 ? "dish" : "dishes")})");
        }

        if (IsOceanicSonar)
        {
            sb.AppendLine("Oceanic Sonar: Maritime Acoustic Mode (Water Only)");
        }

        sb.Append($"Tracked Contacts: {(IsSensorActive ? cachedTrackedCount : 0)}");

        return sb.ToString().TrimEnd();
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (Gizmo gizmo in base.CompGetGizmosExtra())
        {
            yield return gizmo;
        }

        int tracked = IsSensorActive ? cachedTrackedCount : 0;
        string powerMode = tracked > 0 ? "Active Tracking" : "Standby";
        float draw = tracked > 0 ? Props.activePowerDraw : Props.standbyPowerDraw;
        int dishCount = OperationalDishCount;
        int idRadius = IdentificationRadiusInTiles;
        float detRadius = DetectionRadiusInTiles;
        string offlineReason = GetOfflineReason();

        var descBuilder = new StringBuilder();
        descBuilder.AppendLine($"Operational Status: {(IsSensorActive ? "Online" : $"Offline ({offlineReason})")}");
        descBuilder.AppendLine($"Power Mode: {(IsSensorActive ? powerMode : "Offline")} ({(IsSensorActive ? draw : 0f):F0} W)");
        descBuilder.AppendLine($"Coverage Rings: Detection {detRadius:F0} tiles, Identification {idRadius} tiles");
        descBuilder.AppendLine($"Interferometry Array: {dishCount} operational {(dishCount == 1 ? "dish" : "dishes")}");

        if (Props.receivesVantageBonus && cachedVantageBonus > 0)
        {
            descBuilder.AppendLine($"Terrain Vantage: +{cachedVantageBonus} rings ({SensorTile.Tile?.hilliness.ToString() ?? "Elevated"})");
        }

        if (HasActiveSigintConsole)
        {
            string linkType = IsDirectlyLinkedToActiveSigint(parent) ? "Directly Linked" : "Array Linked";
            int sigintDishes = SigintOperationalDishCount;
            int sigintRadius = SigintPenetrationRadiusInTiles;
            descBuilder.AppendLine($"SIGINT Decryption: {linkType} ({sigintRadius} tiles bandwidth, {sigintDishes} active {(sigintDishes == 1 ? "dish" : "dishes")})");
        }

        if (IsOceanicSonar)
        {
            descBuilder.AppendLine("Oceanic Sonar: Maritime Acoustic Mode (Water Only)");
        }

        descBuilder.AppendLine($"Tracked Contacts: {tracked}");
        descBuilder.AppendLine();
        descBuilder.Append("Click to view planetary sensory coverage on the Geoscape.");

        var cmd = new Command_Action
        {
            defaultLabel = IsSensorActive
                ? (tracked > 0 ? "Radar: Tracking" : "Radar: Standby")
                : "Radar: Offline",
            defaultDesc = descBuilder.ToString(),
            icon = ContentFinder<Texture2D>.Get("UI/Commands/LaunchReport", false) ?? BaseContent.WhiteTex,
            action = () =>
            {
                if (parent?.Map?.Parent != null)
                {
                    CameraJumper.TryJumpAndSelect(parent.Map.Parent);
                }
                else if (SensorTile.Valid)
                {
                    CameraJumper.TryJump(SensorTile);
                }
            }
        };

        yield return cmd;
    }

    #endregion
}
