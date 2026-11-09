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

    private CompPowerTrader? PowerTrader => powerTrader ??= parent.GetComp<CompPowerTrader>();
    private CompFlickable? Flickable => flickable ??= parent.GetComp<CompFlickable>();
    private CompBreakdownable? Breakdownable => breakdownable ??= parent.GetComp<CompBreakdownable>();
    private CompFacility? Facility => facility ??= parent.GetComp<CompFacility>();

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
    /// Counts operational phased array radar dishes on the local map benefiting from this SIGINT mainframe.
    /// Checks both direct facility connections and compound-wide phased arrays.
    /// </summary>
    public int LinkedDishesCount
    {
        get
        {
            if (parent?.Map == null)
            {
                return 0;
            }

            // 1. Direct facility connections
            var linked = Facility?.LinkedBuildings;
            if (linked != null && linked.Count > 0)
            {
                int facilityDishes = 0;
                for (int i = 0; i < linked.Count; i++)
                {
                    Thing b = linked[i];
                    if (b != null && b.Spawned && !b.Destroyed)
                    {
                        CompRadar? radar = b.TryGetComp<CompRadar>();
                        if (radar != null && radar.IsSensorActive)
                        {
                            facilityDishes++;
                        }
                    }
                }

                if (facilityDishes > 0)
                {
                    return facilityDishes;
                }
            }

            // 2. Compound-wide fallback across the local map
            ThingDef? phasedArrayDef = DefDatabase<ThingDef>.GetNamedSilentFail("Geoscape_RadarLarge");
            if (phasedArrayDef == null)
            {
                return 0;
            }

            int count = 0;
            var buildings = parent.Map.listerBuildings?.AllBuildingsColonistOfDef(phasedArrayDef);
            if (buildings != null && buildings.Count > 0)
            {
                for (int i = 0; i < buildings.Count; i++)
                {
                    Building b = buildings[i];
                    CompRadar? radar = b.GetComp<CompRadar>();
                    if (radar != null && radar.IsSensorActive)
                    {
                        count++;
                    }
                }
            }
            else if (parent.Map.listerThings != null)
            {
                // God-mode / dev-spawned building fallback
                var things = parent.Map.listerThings.ThingsOfDef(phasedArrayDef);
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

            return count;
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
            int linkedDishes = LinkedDishesCount;
            sb.AppendLine("SIGINT Status: Operational (Active Decryption)");
            if (linkedDishes > 0)
            {
                sb.AppendLine($"Linked Radar Array: {linkedDishes} {(linkedDishes == 1 ? "dish" : "dishes")}");
                sb.AppendLine("Capabilities: Tier 4 (Analysed) Intel & Terrain Penetration");
            }
            else
            {
                sb.AppendLine("Linked Radar Array: None (Awaiting Phased Radar Array)");
            }
        }

        return sb.ToString().TrimEnd();
    }
}
