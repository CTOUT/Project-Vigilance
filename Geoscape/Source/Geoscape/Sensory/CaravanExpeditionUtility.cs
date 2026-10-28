using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Data payload defining an active construction directive dispatched from a player colony.
/// </summary>
public sealed class OutpostConstructionDirective
{
    public PlanetTile TargetTile { get; init; }
    public ListeningPostKind Kind { get; init; }
    public ThingDef? StuffDef { get; init; }
    public int OriginMapId { get; init; }
    public int CreatedTick { get; init; }
}

/// <summary>
/// Coordinates 4X-style Geoscape expedition dispatching, colony resource validation,
/// and automated supply pre-allocation for planetary sensor expansion.
/// </summary>
public static class CaravanExpeditionUtility
{
    private static readonly object syncLock = new();
    private static readonly Dictionary<int, OutpostConstructionDirective> pendingDirectives = new();

    public static void RegisterPendingDirective(PlanetTile tile, OutpostConstructionDirective directive)
    {
        if (!tile.Valid || directive == null) return;
        lock (syncLock)
        {
            pendingDirectives[tile.tileId] = directive;
        }
    }

    public static bool TryConsumePendingDirective(PlanetTile tile, out OutpostConstructionDirective? directive)
    {
        if (tile.Valid)
        {
            lock (syncLock)
            {
                if (pendingDirectives.TryGetValue(tile.tileId, out directive))
                {
                    pendingDirectives.Remove(tile.tileId);
                    return true;
                }
            }
        }

        directive = null;
        return false;
    }

    public static void ClearPendingDirective(PlanetTile tile)
    {
        if (tile.Valid)
        {
            lock (syncLock)
            {
                pendingDirectives.Remove(tile.tileId);
            }
        }
    }

    public static void ClearAllPendingDirectives()
    {
        lock (syncLock)
        {
            pendingDirectives.Clear();
        }
    }

    public static int PendingDirectivesCount
    {
        get
        {
            lock (syncLock)
            {
                return pendingDirectives.Count;
            }
        }
    }

    public static bool CanColonyDeployAutomatedRelay(Map map, out int currentSteel, out int currentComponents)
    {
        currentSteel = 0;
        currentComponents = 0;

        if (map?.resourceCounter == null)
        {
            return false;
        }

        currentSteel = map.resourceCounter.GetCount(ThingDefOf.Steel);
        currentComponents = map.resourceCounter.GetCount(ThingDefOf.ComponentIndustrial);

        return currentSteel >= OutpostMaterialUtility.AutomatedRelaySteelCost &&
               currentComponents >= OutpostMaterialUtility.AutomatedRelayComponentCost;
    }

    public static List<ThingDef> GetAvailableColonyBeaconMaterials(Map map)
    {
        var result = new List<ThingDef>();
        if (map?.resourceCounter == null)
        {
            return result;
        }

        int wood = map.resourceCounter.GetCount(ThingDefOf.WoodLog);
        if (wood >= OutpostMaterialUtility.SignalBeaconWoodOrStoneCost)
        {
            result.Add(ThingDefOf.WoodLog);
        }

        if (DefDatabase<ThingDef>.AllDefs != null)
        {
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
            {
                if (def != ThingDefOf.WoodLog && OutpostMaterialUtility.IsWoodOrStone(def))
                {
                    int stone = map.resourceCounter.GetCount(def);
                    if (stone >= OutpostMaterialUtility.SignalBeaconWoodOrStoneCost && !result.Contains(def))
                    {
                        result.Add(def);
                    }
                }
            }
        }

        return result;
    }

    public static void OpenCaravanFormingDialog(Map map, PlanetTile targetTile)
    {
        if (map == null || !targetTile.Valid) return;
        var dialog = new Dialog_FormCaravan(map);
        Find.WindowStack.Add(dialog);
        Find.WorldRoutePlanner.Start(dialog);
        Find.WorldRoutePlanner.TryAddWaypoint(targetTile);
    }

    public static void StartConstructionExpedition(Settlement settlement, PlanetTile targetTile, ListeningPostKind kind, ThingDef? stuffDef)
    {
        if (settlement?.Map == null || !targetTile.Valid)
        {
            return;
        }

        // 1. Register pending directive
        RegisterPendingDirective(targetTile, new OutpostConstructionDirective
        {
            TargetTile = targetTile,
            Kind = kind,
            StuffDef = stuffDef,
            OriginMapId = settlement.Map.uniqueID,
            CreatedTick = Find.TickManager?.TicksGame ?? 0
        });

        // 2. Open standard Form Caravan dialog configured for route
        var dialog = new Dialog_FormCaravan(settlement.Map, reform: false, onClosed: () =>
        {
            ClearPendingDirective(targetTile);
        });

        Find.WindowStack.Add(dialog);
        Find.WorldRoutePlanner.Start(dialog);
        Find.WorldRoutePlanner.TryAddWaypoint(targetTile);

        // 3. Pre-select required construction materials in transferables list
        PreallocateConstructionMaterials(dialog, kind, stuffDef);

        string materialSummary = kind == ListeningPostKind.AutomatedRelay
            ? $"{OutpostMaterialUtility.AutomatedRelaySteelCost} Steel, {OutpostMaterialUtility.AutomatedRelayComponentCost} Components"
            : (stuffDef != null ? $"{OutpostMaterialUtility.SignalBeaconWoodOrStoneCost} {stuffDef.LabelCap}" : $"{OutpostMaterialUtility.SignalBeaconWoodOrStoneCost} Wood/Stone");

        Messages.Message($"[Project Vigilance] Pre-selected {materialSummary} for outpost expedition to Tile {targetTile}. Select colonist(s) and provisions to depart.", MessageTypeDefOf.CautionInput, false);
    }

    public static void PreallocateConstructionMaterials(Dialog_FormCaravan dialog, ListeningPostKind kind, ThingDef? stuffDef)
    {
        if (dialog == null) return;

        List<TransferableOneWay>? transferables = Traverse.Create(dialog).Field("transferables").GetValue<List<TransferableOneWay>>();
        if (transferables == null) return;

        if (kind == ListeningPostKind.AutomatedRelay)
        {
            AllocateRequirement(transferables, ThingDefOf.Steel, OutpostMaterialUtility.AutomatedRelaySteelCost);
            AllocateRequirement(transferables, ThingDefOf.ComponentIndustrial, OutpostMaterialUtility.AutomatedRelayComponentCost);
        }
        else if (kind == ListeningPostKind.SignalBeacon)
        {
            ThingDef targetMaterial = stuffDef ?? ThingDefOf.WoodLog;
            AllocateRequirement(transferables, targetMaterial, OutpostMaterialUtility.SignalBeaconWoodOrStoneCost);
        }
    }

    private static void AllocateRequirement(List<TransferableOneWay> transferables, ThingDef requiredDef, int neededCount)
    {
        int remaining = neededCount;
        for (int i = 0; i < transferables.Count; i++)
        {
            TransferableOneWay t = transferables[i];
            if (t.ThingDef == requiredDef && remaining > 0)
            {
                int toTake = Math.Min(t.MaxCount, remaining);
                t.AdjustTo(toTake);
                remaining -= toTake;
            }
        }
    }
}
