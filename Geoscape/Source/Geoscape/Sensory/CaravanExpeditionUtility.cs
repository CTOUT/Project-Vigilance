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
    private static readonly System.Reflection.FieldInfo? DestinationTileField =
        AccessTools.Field(typeof(LordJob_FormAndSendCaravan), "destinationTile");

    private static readonly System.Reflection.FieldInfo? DialogDestinationTileField =
        AccessTools.Field(typeof(Dialog_FormCaravan), "destinationTile");

    private static readonly System.Reflection.FieldInfo? DialogMapField =
        AccessTools.Field(typeof(Dialog_FormCaravan), "map");

    private static readonly System.Reflection.MethodInfo? CountToTransferChangedMethod =
        AccessTools.Method(typeof(Dialog_FormCaravan), "CountToTransferChanged");

    internal static readonly System.Reflection.FieldInfo? TransferablesField =
        AccessTools.Field(typeof(Dialog_FormCaravan), "transferables");

    private static readonly System.Reflection.PropertyInfo? DaysWorthOfFoodProp =
        AccessTools.Property(typeof(Dialog_FormCaravan), "DaysWorthOfFood");

    private static readonly System.Reflection.PropertyInfo? TicksToArriveProp =
        AccessTools.Property(typeof(Dialog_FormCaravan), "TicksToArrive");

    private static readonly System.Reflection.MethodInfo? TryFormAndSendCaravanMethod =
        AccessTools.Method(typeof(Dialog_FormCaravan), "TryFormAndSendCaravan");

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

    public static bool TryGetPendingDirective(PlanetTile tile, out OutpostConstructionDirective? directive)
    {
        if (tile.Valid)
        {
            lock (syncLock)
            {
                return pendingDirectives.TryGetValue(tile.tileId, out directive);
            }
        }
        directive = null;
        return false;
    }

    public static bool TryGetPendingDirectiveForDialog(Dialog_FormCaravan dialog, out OutpostConstructionDirective? directive)
    {
        directive = null;
        if (dialog == null) return false;

        PlanetTile dest = DialogDestinationTileField != null && DialogDestinationTileField.GetValue(dialog) is PlanetTile pt
            ? pt
            : PlanetTile.Invalid;

        if (dest.Valid && TryGetPendingDirective(dest, out directive))
        {
            return true;
        }

        Map? map = DialogMapField?.GetValue(dialog) as Map;
        if (map != null)
        {
            lock (syncLock)
            {
                directive = pendingDirectives.Values.FirstOrDefault(d => d.OriginMapId == map.uniqueID);
                return directive != null;
            }
        }

        return false;
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

        if (directive.Kind == ListeningPostKind.AutomatedSonar)
        {
            return OutpostMaterialUtility.CanDeployAutomatedSonar(caravan, out _);
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

    public static bool CanColonyDeployAutomatedSonar(Map map, out int currentSteel, out int currentComponents)
    {
        currentSteel = 0;
        currentComponents = 0;

        if (map?.resourceCounter == null)
        {
            return false;
        }

        currentSteel = map.resourceCounter.GetCount(ThingDefOf.Steel);
        currentComponents = map.resourceCounter.GetCount(ThingDefOf.ComponentIndustrial);

        return currentSteel >= OutpostMaterialUtility.AutomatedSonarSteelCost &&
               currentComponents >= OutpostMaterialUtility.AutomatedSonarComponentCost;
    }

    /// <summary>
    /// Optional hook for overriding available beacon materials during headless unit tests.
    /// </summary>
    public static System.Func<Map, List<ThingDef>>? BeaconMaterialsOverride { get; set; }

    public static List<ThingDef> GetAvailableColonyBeaconMaterials(Map map)
    {
        if (BeaconMaterialsOverride != null)
        {
            return BeaconMaterialsOverride(map);
        }

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
        if (map == null) return;
        var dialog = new Dialog_FormCaravan(map);
        if (Find.WindowStack != null)
        {
            Find.WindowStack.Add(dialog);
        }
        if (targetTile.Valid)
        {
            AttachTargetRouteToDialog(dialog, targetTile);
        }
    }

    private static void AttachTargetRouteToDialog(Dialog_FormCaravan dialog, PlanetTile targetTile)
    {
        if (dialog == null || !targetTile.Valid) return;

        // If WorldRoutePlanner was started in PostOpen, add the destination waypoint and stop route selection
        if (Find.WorldRoutePlanner != null && Find.WorldRoutePlanner.Active)
        {
            Find.WorldRoutePlanner.TryAddWaypoint(targetTile, playSound: false);
            Find.WorldRoutePlanner.Stop();
        }

        // Ensure Dialog_FormCaravan remains open on the window stack
        if (Find.WindowStack != null && !Find.WindowStack.IsOpen(dialog))
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
        var dialog = new Dialog_FormCaravan(settlement.Map, reform: false);

        if (Find.WindowStack != null)
        {
            Find.WindowStack.Add(dialog);
        }
        AttachTargetRouteToDialog(dialog, targetTile);

        // 3. Pre-select required construction materials in transferables list (if already populated)
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
        var dialog = new Dialog_FormCaravan(settlement.Map, reform: false);

        if (Find.WindowStack != null)
        {
            Find.WindowStack.Add(dialog);
        }
        AttachTargetRouteToDialog(dialog, targetTile);

        Messages.Message($"[Project Vigilance] Dismantle expedition route set to {post.LabelCap} at Tile {targetTile}. Select colonist(s) and provisions to depart.", MessageTypeDefOf.CautionInput, false);
    }

    public static void PreallocateConstructionMaterials(Dialog_FormCaravan dialog, ListeningPostKind kind, ThingDef? stuffDef)
    {
        if (dialog == null) return;

        List<TransferableOneWay>? transferables = TransferablesField?.GetValue(dialog) as List<TransferableOneWay>;
        if (transferables == null || transferables.Count == 0) return;

        if (kind == ListeningPostKind.AutomatedRelay)
        {
            AllocateRequirement(transferables, ThingDefOf.Steel, OutpostMaterialUtility.AutomatedRelaySteelCost);
            AllocateRequirement(transferables, ThingDefOf.ComponentIndustrial, OutpostMaterialUtility.AutomatedRelayComponentCost);
        }
        else if (kind == ListeningPostKind.AutomatedSonar)
        {
            AllocateRequirement(transferables, ThingDefOf.Steel, OutpostMaterialUtility.AutomatedSonarSteelCost);
            AllocateRequirement(transferables, ThingDefOf.ComponentIndustrial, OutpostMaterialUtility.AutomatedSonarComponentCost);
        }
        else if (kind == ListeningPostKind.SignalBeacon)
        {
            ThingDef targetMaterial = stuffDef ?? ThingDefOf.WoodLog;
            AllocateRequirement(transferables, targetMaterial, OutpostMaterialUtility.SignalBeaconWoodOrStoneCost);
        }

        CountToTransferChangedMethod?.Invoke(dialog, null);
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

    public static float GetDaysWorthOfFood(Dialog_FormCaravan dialog)
    {
        if (dialog == null || DaysWorthOfFoodProp == null) return 0f;
        object? val = DaysWorthOfFoodProp.GetValue(dialog);
        if (val is (float days, float _))
        {
            return days;
        }
        return 0f;
    }

    public static int GetTicksToArrive(Dialog_FormCaravan dialog)
    {
        if (dialog == null || TicksToArriveProp == null) return 0;
        object? val = TicksToArriveProp.GetValue(dialog);
        return val is int ticks ? ticks : 0;
    }

    public static int GetEstimatedTransitTicks(Dialog_FormCaravan dialog, PlanetTile targetTile)
    {
        int ticks = GetTicksToArrive(dialog);
        if (ticks > 0) return ticks;

        try
        {
            var world = Current.Game?.World;
            if (targetTile.Valid && world?.grid != null)
            {
                Map? map = DialogMapField?.GetValue(dialog) as Map;
                PlanetTile originTile = map != null ? (PlanetTile)map.Tile : PlanetTile.Invalid;
                if (originTile.Valid)
                {
                    float dist = world.grid.ApproxDistanceInTiles(originTile, targetTile);
                    float days = UnityEngine.Mathf.Max(0.25f, dist / 18f);
                    return UnityEngine.Mathf.RoundToInt(days * 60000f);
                }
            }
        }
        catch
        {
            // Headless unit test mocks
        }

        return 15000; // 0.25 days fallback
    }

    public static bool TryFormAndSendCaravan(Dialog_FormCaravan dialog)
    {
        if (dialog == null || TryFormAndSendCaravanMethod == null) return false;
        return TryFormAndSendCaravanMethod.Invoke(dialog, null) is true;
    }

    public static void PreallocateExpeditionFood(Dialog_FormCaravan dialog, OutpostConstructionDirective directive)
    {
        if (dialog == null || directive == null) return;

        List<TransferableOneWay>? transferables = TransferablesField?.GetValue(dialog) as List<TransferableOneWay>;
        if (transferables == null || transferables.Count == 0) return;

        int transitTicks = GetEstimatedTransitTicks(dialog, directive.TargetTile);
        float transitDays = transitTicks > 0 ? (transitTicks / 60000f) : 0.25f;
        float onSiteDays = directive.DirectiveKind == OutpostDirectiveKind.Dismantle
            ? (1500f / 60000f)
            : ((directive.Kind == ListeningPostKind.AutomatedRelay ? 15000 : 7500) / 60000f);

        // Estimate round-trip plus on-site time plus 1.5d safety buffer
        float targetFoodDays = UnityEngine.Mathf.Clamp((transitDays * 2f) + onSiteDays + 1.5f, 2.5f, 10f);

        int colonistCount = GetSelectedColonistCount(transferables);
        if (colonistCount <= 0)
        {
            colonistCount = 3;
        }

        int mealsNeeded = UnityEngine.Mathf.CeilToInt(targetFoodDays * colonistCount * 2f);
        int remainingMeals = mealsNeeded;

        // 1. Packaged Survival Meals (non-perishable)
        if (remainingMeals > 0 && ThingDefOf.MealSurvivalPack != null)
        {
            int allocated = AllocateFoodUnits(transferables, ThingDefOf.MealSurvivalPack, remainingMeals);
            remainingMeals -= allocated;
        }

        // 2. Pemmican (16 units per meal equivalent)
        if (remainingMeals > 0 && ThingDefOf.Pemmican != null)
        {
            int allocatedPemmican = AllocateFoodUnits(transferables, ThingDefOf.Pemmican, remainingMeals * 16);
            remainingMeals -= (allocatedPemmican / 16);
        }

        // 3. Simple Meals
        if (remainingMeals > 0 && ThingDefOf.MealSimple != null)
        {
            int allocated = AllocateFoodUnits(transferables, ThingDefOf.MealSimple, remainingMeals);
            remainingMeals -= allocated;
        }

        // 4. Fine Meals
        if (remainingMeals > 0 && ThingDefOf.MealFine != null)
        {
            int allocated = AllocateFoodUnits(transferables, ThingDefOf.MealFine, remainingMeals);
            remainingMeals -= allocated;
        }

        CountToTransferChangedMethod?.Invoke(dialog, null);
    }

    private static int GetSelectedColonistCount(List<TransferableOneWay> transferables)
    {
        int count = 0;
        for (int i = 0; i < transferables.Count; i++)
        {
            TransferableOneWay t = transferables[i];
            if (t.ThingDef != null && t.ThingDef.category == ThingCategory.Pawn && t.CountToTransfer > 0 && t.things != null)
            {
                count += t.things.OfType<Pawn>().Take(t.CountToTransfer).Count(p => p.IsFreeColonist);
            }
        }
        return count;
    }

    private static int AllocateFoodUnits(List<TransferableOneWay> transferables, ThingDef foodDef, int maxUnits)
    {
        int totalAllocated = 0;
        int remaining = maxUnits;
        for (int i = 0; i < transferables.Count; i++)
        {
            TransferableOneWay t = transferables[i];
            if (t.ThingDef == foodDef && remaining > 0)
            {
                int toTake = Math.Min(t.MaxCount, remaining);
                t.AdjustTo(toTake);
                remaining -= toTake;
                totalAllocated += toTake;
            }
        }
        return totalAllocated;
    }

    private static System.Reflection.PropertyInfo? massCapacityProp;
    private static System.Reflection.PropertyInfo? massUsageProp;

    public static float GetCaravanMassCapacity(Dialog_FormCaravan dialog)
    {
        if (dialog == null) return 0f;
        massCapacityProp ??= typeof(Dialog_FormCaravan).GetProperty("MassCapacity", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (massCapacityProp != null)
        {
            object? val = massCapacityProp.GetValue(dialog);
            if (val is float f) return f;
        }
        return 0f;
    }

    public static float GetCaravanMassUsage(Dialog_FormCaravan dialog)
    {
        if (dialog == null) return 0f;
        massUsageProp ??= typeof(Dialog_FormCaravan).GetProperty("MassUsage", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (massUsageProp != null)
        {
            object? val = massUsageProp.GetValue(dialog);
            if (val is float f) return f;
        }
        return 0f;
    }

    public static (float mass, string summary) GetExpectedDismantleYield(WorldObject_ListeningPost? post, ListeningPostKind kind, ThingDef? stuffDef)
    {
        bool isConstructing = post?.IsConstructing == true;
        ListeningPostKind effectiveKind = post?.Kind ?? kind;
        ThingDef effectiveStuff = post?.StuffDef ?? stuffDef ?? ThingDefOf.WoodLog;

        if (effectiveKind == ListeningPostKind.AutomatedRelay)
        {
            int steel = isConstructing
                ? OutpostMaterialUtility.AutomatedRelaySteelCost
                : OutpostMaterialUtility.AutomatedRelaySteelSalvage;
            int comp = isConstructing
                ? OutpostMaterialUtility.AutomatedRelayComponentCost
                : OutpostMaterialUtility.AutomatedRelayComponentSalvage;

            float steelMass = steel * GetThingBaseMass(ThingDefOf.Steel, 0.5f);
            float compMass = comp * GetThingBaseMass(ThingDefOf.ComponentIndustrial, 0.6f);
            float totalMass = steelMass + compMass;
            string pctStr = isConstructing ? "100% refund" : "75% salvage";
            return (totalMass, $"{steel} Steel, {comp} Components ({pctStr})");
        }
        else if (effectiveKind == ListeningPostKind.AutomatedSonar)
        {
            int steel = isConstructing
                ? OutpostMaterialUtility.AutomatedSonarSteelCost
                : OutpostMaterialUtility.AutomatedSonarSteelSalvage;
            int comp = isConstructing
                ? OutpostMaterialUtility.AutomatedSonarComponentCost
                : OutpostMaterialUtility.AutomatedSonarComponentSalvage;

            float steelMass = steel * GetThingBaseMass(ThingDefOf.Steel, 0.5f);
            float compMass = comp * GetThingBaseMass(ThingDefOf.ComponentIndustrial, 0.6f);
            float totalMass = steelMass + compMass;
            string pctStr = isConstructing ? "100% refund" : "75% salvage";
            return (totalMass, $"{steel} Steel, {comp} Components ({pctStr})");
        }
        else
        {
            int count = isConstructing
                ? OutpostMaterialUtility.SignalBeaconWoodOrStoneCost
                : OutpostMaterialUtility.SignalBeaconWoodSalvage;

            float fallbackMass = (effectiveStuff?.defName?.Contains("Wood") == true) ? 0.4f : 1.0f;
            float unitMass = GetThingBaseMass(effectiveStuff, fallbackMass);
            float totalMass = count * unitMass;
            string pctStr = isConstructing ? "100% refund" : "75% salvage";
            return (totalMass, $"{count} {effectiveStuff?.LabelCap ?? "Wood"} ({pctStr})");
        }
    }

    private static float GetThingBaseMass(ThingDef? def, float fallback)
    {
        if (def == null) return fallback;
        try
        {
            if (StatDefOf.Mass != null && def.statBases != null && def.statBases.Any(s => s.stat == StatDefOf.Mass))
            {
                return def.BaseMass;
            }
        }
        catch
        {
        }
        return fallback;
    }
}
