using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

public class CompProperties_SigintConsole : CompProperties
{
    public CompProperties_SigintConsole()
    {
        compClass = typeof(CompSigintConsole);
    }
}

/// <summary>
/// Controls the Signals Intelligence (SIGINT) Decryption Mainframe.
/// Intercepts enemy tactical telemetry, providing compound-wide signals decryption
/// across all linked phased array dishes and penetrating mountain radar shadows.
/// </summary>
public class CompSigintConsole : ThingComp
{
    private CompPowerTrader? powerTrader;
    private CompFlickable? flickable;
    private CompBreakdownable? breakdownable;
    private CompFacility? facility;

    private bool lastOperationalState;
    private int lastLinkedDishCount = -1;

    private CompPowerTrader? PowerTrader => powerTrader ??= parent?.GetComp<CompPowerTrader>();
    private CompFlickable? Flickable => flickable ??= parent?.GetComp<CompFlickable>();
    private CompBreakdownable? Breakdownable => breakdownable ??= parent?.GetComp<CompBreakdownable>();
    private CompFacility? Facility => facility ??= parent?.GetComp<CompFacility>();

    public bool IsOperational
    {
        get
        {
            if (parent == null || !parent.Spawned || parent.Destroyed)
            {
                return false;
            }

            if (PowerTrader != null && !PowerTrader.PowerOn)
            {
                return false;
            }

            if (Flickable != null && !Flickable.SwitchIsOn)
            {
                return false;
            }

            if (Breakdownable != null && Breakdownable.BrokenDown)
            {
                return false;
            }

            return true;
        }
    }

    public string GetOfflineReason()
    {
        if (parent == null || !parent.Spawned || parent.Destroyed)
        {
            return "Unspawned";
        }

        if (Breakdownable != null && Breakdownable.BrokenDown)
        {
            return "Faulty / Broken Down";
        }

        if (Flickable != null && !Flickable.SwitchIsOn)
        {
            return "Switched Off";
        }

        if (PowerTrader != null && !PowerTrader.PowerOn)
        {
            return "Unpowered";
        }

        return "Offline";
    }

    /// <summary>
    /// Counts all phased radar array dishes physically connected to this SIGINT console via CompFacility,
    /// regardless of power state.
    /// </summary>
    public int TotalLinkedDishesCount
    {
        get
        {
            if (parent == null || !parent.Spawned || parent.Destroyed || parent.Map == null)
            {
                return 0;
            }

            var linked = Facility?.LinkedBuildings;
            if (linked == null || linked.Count == 0)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < linked.Count; i++)
            {
                Thing b = linked[i];
                if (b != null && b.Spawned && !b.Destroyed && b.TryGetComp<CompRadar>() != null)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>
    /// Counts operational, powered phased radar array dishes physically connected to this SIGINT console via CompFacility.
    /// </summary>
    public int ActiveLinkedDishesCount
    {
        get
        {
            if (parent == null || !parent.Spawned || parent.Destroyed || parent.Map == null)
            {
                return 0;
            }

            var linked = Facility?.LinkedBuildings;
            if (linked == null || linked.Count == 0)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < linked.Count; i++)
            {
                Thing b = linked[i];
                if (b != null && b.Spawned && !b.Destroyed)
                {
                    CompRadar? radar = b.TryGetComp<CompRadar>();
                    if (radar != null && radar.IsSensorActive)
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }

    /// <summary>
    /// Backward-compatible alias for ActiveLinkedDishesCount.
    /// </summary>
    public int LinkedDishesCount => ActiveLinkedDishesCount;

    /// <summary>
    /// Resolves the active decryption bandwidth in tiles based on active linked dishes.
    /// 1 active dish = 10 tiles.
    /// 2 active dishes = 17 tiles.
    /// 3+ active dishes = 22 tiles.
    /// </summary>
    public int DecryptionBandwidthInTiles
    {
        get
        {
            if (!IsOperational)
            {
                return 0;
            }

            int activeCount = ActiveLinkedDishesCount;
            if (activeCount <= 0)
            {
                return 0;
            }

            var linked = Facility?.LinkedBuildings;
            if (linked != null)
            {
                for (int i = 0; i < linked.Count; i++)
                {
                    CompRadar? radar = linked[i]?.TryGetComp<CompRadar>();
                    if (radar != null)
                    {
                        return radar.Props.ResolveIdentificationRadius(activeCount, 0);
                    }
                }
            }

            return activeCount switch
            {
                1 => 10,
                2 => 17,
                _ => 22
            };
        }
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        lastOperationalState = IsOperational;
        lastLinkedDishCount = LinkedDishesCount;
        WorldDrawLayer_RadarCoverage.Instance?.SetDirty();
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        base.PostDeSpawn(map, mode);
        WorldDrawLayer_RadarCoverage.Instance?.SetDirty();
    }

    public override void CompTick()
    {
        base.CompTick();
        if (parent.IsHashIntervalTick(60))
        {
            bool operational = IsOperational;
            int dishes = LinkedDishesCount;
            if (operational != lastOperationalState || dishes != lastLinkedDishCount)
            {
                lastOperationalState = operational;
                lastLinkedDishCount = dishes;
                WorldDrawLayer_RadarCoverage.Instance?.SetDirty();
            }
        }
    }

    public override string? CompInspectStringExtra()
    {
        if (parent?.Map == null)
        {
            return null;
        }

        var sb = new StringBuilder();

        if (!IsOperational)
        {
            string reason = GetOfflineReason();
            sb.AppendLine($"SIGINT Status: Offline ({reason})");
        }
        else
        {
            int totalLinked = TotalLinkedDishesCount;
            int activeLinked = ActiveLinkedDishesCount;

            if (totalLinked == 0)
            {
                sb.AppendLine("SIGINT Status: Standby (Awaiting Phased Radar Array Link)");
                sb.AppendLine("Linked Radar Array: None (Within 12.9 tiles)");
            }
            else if (activeLinked == 0)
            {
                sb.AppendLine("SIGINT Status: Standby (Connected Radar Array Unpowered)");
                sb.AppendLine($"Linked Radar Array: {totalLinked} {(totalLinked == 1 ? "dish" : "dishes")} (Offline)");
            }
            else
            {
                int bandwidth = DecryptionBandwidthInTiles;
                sb.AppendLine("SIGINT Status: Operational (Active Decryption)");
                sb.AppendLine($"Linked Radar Array: {activeLinked} operational {(activeLinked == 1 ? "dish" : "dishes")}");
                sb.AppendLine($"Decryption Bandwidth: {bandwidth} tiles (Tier 4 Intel & Mountain Penetration)");
            }
        }

        return sb.ToString().TrimEnd();
    }
}
