using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Operational directive assigned to a departing caravan expedition.
/// </summary>
public enum OutpostDirectiveKind
{
    Construct,
    Dismantle
}

/// <summary>
/// Data payload defining an active construction or dismantling directive dispatched from a player colony.
/// </summary>
public sealed class OutpostConstructionDirective
{
    public OutpostDirectiveKind DirectiveKind { get; init; } = OutpostDirectiveKind.Construct;
    public PlanetTile TargetTile { get; init; }
    public ListeningPostKind Kind { get; init; }
    public ThingDef? StuffDef { get; init; }
    public WorldObject_ListeningPost? TargetOutpost { get; init; }
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

    public static bool TryConsumePendingDirectiveForCaravan(Caravan caravan, PlanetTile exitFromTile, PlanetTile destinationTile, out OutpostConstructionDirective? directive)
    {
        directive = null;
        if (caravan == null) return false;

        lock (syncLock)
        {
            // 1. Match by explicit destinationTile
            if (destinationTile.Valid && pendingDirectives.TryGetValue(destinationTile.tileId, out directive))
            {
                pendingDirectives.Remove(destinationTile.tileId);
                return true;
            }

            // 2. Match by caravan.pather.Destination
            if (caravan.pather != null && caravan.pather.Destination.Valid && pendingDirectives.TryGetValue(caravan.pather.Destination.tileId, out directive))
            {
                pendingDirectives.Remove(caravan.pather.Destination.tileId);
                return true;
            }

            // Guard against hijacking unrelated caravans:
            // If the departing caravan already has an explicit destination that is not the origin exit tile
            // and does not match any pending directive, fallbacks 3 and 4 must NOT hijack it.
            bool hasExplicitDestination = destinationTile.Valid && (!exitFromTile.Valid || destinationTile != exitFromTile);
            if (caravan.pather != null && caravan.pather.Destination.Valid && (!exitFromTile.Valid || caravan.pather.Destination != exitFromTile))
            {
                hasExplicitDestination = true;
            }

            // 3. Fallback: match by origin map/tile if directive was created recently (within 120,000 ticks)
            int currentTick = Current.Game?.tickManager?.TicksGame ?? 0;
            int originMapId = -1;
            var worldObjects = Current.Game?.World?.worldObjects;
            if (worldObjects != null)
            {
                if (exitFromTile.Valid && worldObjects.SettlementAt(exitFromTile)?.Map != null)
                {
                    originMapId = worldObjects.SettlementAt(exitFromTile).Map.uniqueID;
                }
                else if (caravan.Tile.Valid && worldObjects.SettlementAt(caravan.Tile)?.Map != null)
                {
                    originMapId = worldObjects.SettlementAt(caravan.Tile).Map.uniqueID;
                }
            }

            foreach (var kvp in pendingDirectives.ToList())
            {
                var candidate = kvp.Value;
                if (currentTick > 0 && candidate.CreatedTick > 0 && (currentTick - candidate.CreatedTick) > 120000)
                {
                    pendingDirectives.Remove(kvp.Key);
                    continue;
                }

                // If caravan is specifically headed to another tile, do not bind to this candidate
                if (hasExplicitDestination && destinationTile.Valid && destinationTile != candidate.TargetTile)
                {
                    continue;
                }

                // Verify caravan has required materials for this directive (or is dismantle)
                if (!CaravanMatchesDirectiveRequirements(caravan, candidate))
                {
                    continue;
                }

                if (originMapId != -1 && candidate.OriginMapId == originMapId)
                {
                    pendingDirectives.Remove(kvp.Key);
                    directive = candidate;
                    return true;
                }
            }

            // 4. Fallback: if exactly 1 pending directive exists and has not expired, bind to this caravan
            if (pendingDirectives.Count == 1)
            {
                var single = pendingDirectives.Values.First();
                if ((currentTick == 0 || single.CreatedTick == 0 || (currentTick - single.CreatedTick) <= 120000) &&
                    (!hasExplicitDestination || !destinationTile.Valid || destinationTile == single.TargetTile) &&
                    CaravanMatchesDirectiveRequirements(caravan, single))
                {
                    pendingDirectives.Clear();
                    directive = single;
                    return true;
                }
            }
        }

        directive = null;
        return false;
    }

    private static bool CaravanMatchesDirectiveRequirements(Caravan caravan, OutpostConstructionDirective directive)
    {
        if (caravan == null || directive == null) return false;

        // Safely check if pawns list can be read without throwing on uninitialized test objects
        List<Pawn>? pawns = null;
        try
        {
            pawns = caravan.PawnsListForReading;
        }
        catch
        {
            // Uninitialized test mock
        }

        if (pawns == null || pawns.Count == 0)
        {
            return true;
        }

        if (directive.DirectiveKind == OutpostDirectiveKind.Dismantle)
        {
            return true;
        }

        if (directive.Kind == ListeningPostKind.AutomatedRelay)
        {
            return OutpostMaterialUtility.CanDeployAutomatedRelay(caravan, out _);
        }

        if (directive.Kind == ListeningPostKind.SignalBeacon)
        {
            return OutpostMaterialUtility.CanDeploySignalBeacon(caravan, out _);
        }

        return true;
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
        if (Find.WindowStack != null)
        {
            Find.WindowStack.Add(dialog);
        }
        dialog.Notify_ChoseRoute(targetTile);
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
            CreatedTick = Current.Game?.tickManager?.TicksGame ?? 0
        });

        // 2. Open standard Form Caravan dialog configured for route
        var dialog = new Dialog_FormCaravan(settlement.Map, reform: false, onClosed: () =>
        {
            bool isForming = settlement.Map?.lordManager?.lords != null &&
                             settlement.Map.lordManager.lords.Any(l =>
                             {
                                 if (l.LordJob is LordJob_FormAndSendCaravan f)
                                 {
                                     PlanetTile dest = Traverse.Create(f).Field("destinationTile").GetValue<PlanetTile>();
                                     return dest == targetTile || !dest.Valid;
                                 }
                                 return false;
                             });
            if (!isForming)
            {
                ClearPendingDirective(targetTile);
            }
        });

        if (Find.WindowStack != null)
        {
            Find.WindowStack.Add(dialog);
        }
        dialog.Notify_ChoseRoute(targetTile);

        // 3. Pre-select required construction materials in transferables list
        PreallocateConstructionMaterials(dialog, kind, stuffDef);

        string materialSummary = kind == ListeningPostKind.AutomatedRelay
            ? $"{OutpostMaterialUtility.AutomatedRelaySteelCost} Steel, {OutpostMaterialUtility.AutomatedRelayComponentCost} Components"
            : (stuffDef != null ? $"{OutpostMaterialUtility.SignalBeaconWoodOrStoneCost} {stuffDef.LabelCap}" : $"{OutpostMaterialUtility.SignalBeaconWoodOrStoneCost} Wood/Stone");

        Messages.Message($"[Project Vigilance] Pre-selected {materialSummary} for outpost expedition to Tile {targetTile}. Select colonist(s) and provisions to depart.", MessageTypeDefOf.CautionInput, false);
    }

    public static void StartDismantleExpedition(Settlement settlement, WorldObject_ListeningPost post)
    {
        if (settlement?.Map == null || post == null || !post.Spawned)
        {
            return;
        }

        PlanetTile targetTile = post.Tile;

        // 1. Register pending dismantle directive
        RegisterPendingDirective(targetTile, new OutpostConstructionDirective
        {
            TargetTile = targetTile,
            Kind = post.Kind,
            StuffDef = post.StuffDef,
            DirectiveKind = OutpostDirectiveKind.Dismantle,
            TargetOutpost = post,
            OriginMapId = settlement.Map.uniqueID,
            CreatedTick = Current.Game?.tickManager?.TicksGame ?? 0
        });

        // 2. Open standard Form Caravan dialog configured for route
        var dialog = new Dialog_FormCaravan(settlement.Map, reform: false, onClosed: () =>
        {
            bool isForming = settlement.Map?.lordManager?.lords != null &&
                             settlement.Map.lordManager.lords.Any(l =>
                             {
                                 if (l.LordJob is LordJob_FormAndSendCaravan f)
                                 {
                                     PlanetTile dest = Traverse.Create(f).Field("destinationTile").GetValue<PlanetTile>();
                                     return dest == targetTile || !dest.Valid;
                                 }
                                 return false;
                             });
            if (!isForming)
            {
                ClearPendingDirective(targetTile);
            }
        });

        if (Find.WindowStack != null)
        {
            Find.WindowStack.Add(dialog);
        }
        dialog.Notify_ChoseRoute(targetTile);

        Messages.Message($"[Project Vigilance] Dismantle expedition route set to {post.LabelCap} at Tile {targetTile}. Select colonist(s) and provisions to depart.", MessageTypeDefOf.CautionInput, false);
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
