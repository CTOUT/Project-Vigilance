using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.World;

public partial class WorldObject_Incursion
{
    /// <summary>
    /// Edge-to-Edge Within-Hex Interpolation:
    /// Rather than interpolating from Center[i] to Center[i+1] (which visually places the icon inside Hex[i+1]
    /// while Tile is still Hex[i] for tileProgress 0.50..0.99), interpolates across the current hex's physical span:
    ///   - Entry Border (shared midpoint with Hex[i-1]) at tileProgress = 0.0
    ///   - Hex Center (Center[i]) at tileProgress = 0.5
    ///   - Exit Border (shared midpoint with Hex[i+1]) at tileProgress = 1.0
    /// Because ExitBorder[i] == EntryBorder[i+1], motion is 100% C0-continuous while guaranteeing the icon is
    /// ALWAYS visually inside its actual Tile and crosses the hex border at the exact tick Tile updates.
    /// </summary>
    public Vector3 TrueDrawPos
    {
        get
        {
            if (pathTiles == null || pathTiles.Count == 0 || !Spawned)
            {
                return base.DrawPos;
            }

            if (departureDelayTicks > 0)
            {
                return Find.WorldGrid.GetTileCenter(originTile.Valid ? originTile : Tile);
            }

            if (currentPathIndex >= pathTiles.Count - 1)
            {
                int lastIdx = pathTiles.Count - 1;
                Vector3 finalCenter = Find.WorldGrid.GetTileCenter(pathTiles[lastIdx]);
                if (lastIdx > 0)
                {
                    Vector3 prevCenter = Find.WorldGrid.GetTileCenter(pathTiles[lastIdx - 1]);
                    Vector3 entryEdge = Vector3.Slerp(prevCenter, finalCenter, 0.5f);
                    return Vector3.Slerp(entryEdge, finalCenter, Mathf.Clamp01(tileProgress * 2f));
                }
                return finalCenter;
            }

            PlanetTile curTile = pathTiles[currentPathIndex];
            PlanetTile nextTile = pathTiles[currentPathIndex + 1];

            Vector3 curCenter = Find.WorldGrid.GetTileCenter(curTile);
            Vector3 nextCenter = Find.WorldGrid.GetTileCenter(nextTile);
            Vector3 exitEdge = Vector3.Slerp(curCenter, nextCenter, 0.5f);

            float t = Mathf.Clamp01(tileProgress);

            // Origin tile (index 0): starts at origin center and moves to the first hex border at t = 1.0
            if (currentPathIndex == 0)
            {
                return Vector3.Slerp(curCenter, exitEdge, t);
            }

            // Transit tiles (index > 0):
            // t in [0.0, 0.5] -> Entry Edge (border with previous tile) to Current Tile Center
            // t in [0.5, 1.0] -> Current Tile Center to Exit Edge (border with next tile)
            Vector3 prevTileCenter = Find.WorldGrid.GetTileCenter(pathTiles[currentPathIndex - 1]);
            Vector3 entryEdgePos = Vector3.Slerp(prevTileCenter, curCenter, 0.5f);

            if (t <= 0.5f)
            {
                return Vector3.Slerp(entryEdgePos, curCenter, t * 2f);
            }

            return Vector3.Slerp(curCenter, exitEdge, (t - 0.5f) * 2f);
        }
    }

    public override Vector3 DrawPos
    {
        get
        {
            // In Contact Tier, position updates discretely on 250-tick sweeps rather than 60 FPS continuous Slerp
            if (GeoscapeRules.VisibilityMode != VisibilityMode.Omniscient && intelligenceTier == IntelligenceTier.Contact)
            {
                if (lastSampledDrawPos != Vector3.zero)
                {
                    return lastSampledDrawPos;
                }
            }

            return TrueDrawPos;
        }
    }

    private static Material? cachedHostileLineMat;
    private static Material? cachedHostilePastLineMat;
    private static Material? cachedFriendlyLineMat;
    private static Material? cachedFriendlyPastLineMat;

    private static Material HostileLineMat =>
        cachedHostileLineMat ??= MaterialPool.MatFrom(
            GenDraw.LineTexPath,
            ShaderDatabase.WorldOverlayTransparent,
            new Color(1f, 0.25f, 0.25f, 0.95f),
            WorldMaterials.WorldLineRenderQueue);

    private static Material HostilePastLineMat =>
        cachedHostilePastLineMat ??= MaterialPool.MatFrom(
            GenDraw.LineTexPath,
            ShaderDatabase.WorldOverlayTransparent,
            new Color(0.85f, 0.2f, 0.2f, 0.35f),
            WorldMaterials.WorldLineRenderQueue);

    private static Material FriendlyLineMat =>
        cachedFriendlyLineMat ??= MaterialPool.MatFrom(
            GenDraw.LineTexPath,
            ShaderDatabase.WorldOverlayTransparent,
            new Color(0.25f, 0.85f, 1f, 0.95f),
            WorldMaterials.WorldLineRenderQueue);

    private static Material FriendlyPastLineMat =>
        cachedFriendlyPastLineMat ??= MaterialPool.MatFrom(
            GenDraw.LineTexPath,
            ShaderDatabase.WorldOverlayTransparent,
            new Color(0.25f, 0.85f, 1f, 0.35f),
            WorldMaterials.WorldLineRenderQueue);

    private static Vector3 GetElevatedTileCenter(PlanetTile tile)
    {
        Vector3 pos = Find.WorldGrid.GetTileCenter(tile);
        return pos + pos.normalized * 0.08f;
    }

    public override void DrawExtraSelectionOverlays()
    {
        if (GeoscapeRules.VisibilityMode != VisibilityMode.Omniscient && intelligenceTier < IntelligenceTier.Contact)
        {
            return;
        }

        base.DrawExtraSelectionOverlays();
        DrawPathOverlay();
    }

    private static void DrawVisibilityClippedSegment(
        Vector3 pA,
        Vector3 pB,
        PlanetTile tileA,
        PlanetTile tileB,
        Material mat,
        float width)
    {
        var sensory = SensoryManager.Instance;
        bool hasA = sensory == null || sensory.HasActiveVision(tileA);
        bool hasB = sensory == null || sensory.HasActiveVision(tileB);

        if (hasA && hasB)
        {
            GenDraw.DrawWorldLineBetween(pA, pB, mat, width);
        }
        else if (hasA && !hasB)
        {
            Vector3 mid = Vector3.Lerp(pA, pB, 0.5f);
            GenDraw.DrawWorldLineBetween(pA, mid, mat, width);
        }
        else if (!hasA && hasB)
        {
            Vector3 mid = Vector3.Lerp(pA, pB, 0.5f);
            GenDraw.DrawWorldLineBetween(mid, pB, mat, width);
        }
    }

    private void DrawPathOverlay()
    {
        if (!WorldRendererUtility.WorldRendered || pathTiles == null || pathTiles.Count < 2)
        {
            return;
        }

        List<PlanetTile> tiles = pathTiles;

        // In Contact tier, do not reveal the entire projected planetary path to the player
        if (GeoscapeRules.VisibilityMode != VisibilityMode.Omniscient && intelligenceTier < IntelligenceTier.Classified)
        {
            Vector3 curDrawPos = DrawPos + DrawPos.normalized * 0.08f;
            int anchorIdx = (currentPathIndex > 0 && tileProgress < 0.5f) ? currentPathIndex - 1 : currentPathIndex;
            if (anchorIdx < tiles.Count)
            {
                Vector3 pAnchor = GetElevatedTileCenter(tiles[anchorIdx]);
                if ((curDrawPos - pAnchor).sqrMagnitude > 0.001f)
                {
                    DrawVisibilityClippedSegment(pAnchor, curDrawPos, tiles[anchorIdx], Tile, FriendlyPastLineMat, 1.0f);
                }
            }
            return;
        }

        bool isHostile = Faction?.HostileTo(Faction.OfPlayer) ?? true;
        Material activeMat = isHostile ? HostileLineMat : FriendlyLineMat;
        Material pastMat = isHostile ? HostilePastLineMat : FriendlyPastLineMat;

        // If mobilizing at origin base, draw the full planned path as active
        if (departureDelayTicks > 0)
        {
            for (int i = 0; i < tiles.Count - 1; i++)
            {
                Vector3 pA = GetElevatedTileCenter(tiles[i]);
                Vector3 pB = GetElevatedTileCenter(tiles[i + 1]);
                DrawVisibilityClippedSegment(pA, pB, tiles[i], tiles[i + 1], activeMat, 1.2f);
            }
            return;
        }

        Vector3 curDrawPos2 = DrawPos + DrawPos.normalized * 0.08f;
        bool beforeHexCenter = currentPathIndex > 0 && tileProgress < 0.5f;

        if (beforeHexCenter)
        {
            // Icon is between EntryEdge[i] and Center[i] (segment tiles[i-1] -> tiles[i])
            for (int i = 0; i < currentPathIndex - 1; i++)
            {
                Vector3 pA = GetElevatedTileCenter(tiles[i]);
                Vector3 pB = GetElevatedTileCenter(tiles[i + 1]);
                DrawVisibilityClippedSegment(pA, pB, tiles[i], tiles[i + 1], pastMat, 1.0f);
            }

            Vector3 pPrev = GetElevatedTileCenter(tiles[currentPathIndex - 1]);
            if ((curDrawPos2 - pPrev).sqrMagnitude > 0.001f)
            {
                DrawVisibilityClippedSegment(pPrev, curDrawPos2, tiles[currentPathIndex - 1], Tile, pastMat, 1.0f);
            }

            Vector3 pCur = GetElevatedTileCenter(tiles[currentPathIndex]);
            if ((pCur - curDrawPos2).sqrMagnitude > 0.001f)
            {
                DrawVisibilityClippedSegment(curDrawPos2, pCur, Tile, tiles[currentPathIndex], activeMat, 1.2f);
            }

            for (int i = currentPathIndex; i < tiles.Count - 1; i++)
            {
                Vector3 pA = GetElevatedTileCenter(tiles[i]);
                Vector3 pB = GetElevatedTileCenter(tiles[i + 1]);
                DrawVisibilityClippedSegment(pA, pB, tiles[i], tiles[i + 1], activeMat, 1.2f);
            }
        }
        else
        {
            // Icon is between Center[i] and ExitEdge[i] (segment tiles[i] -> tiles[i+1])
            for (int i = 0; i < currentPathIndex; i++)
            {
                Vector3 pA = GetElevatedTileCenter(tiles[i]);
                Vector3 pB = GetElevatedTileCenter(tiles[i + 1]);
                DrawVisibilityClippedSegment(pA, pB, tiles[i], tiles[i + 1], pastMat, 1.0f);
            }

            if (currentPathIndex < tiles.Count)
            {
                Vector3 pCur = GetElevatedTileCenter(tiles[currentPathIndex]);
                if ((curDrawPos2 - pCur).sqrMagnitude > 0.001f)
                {
                    DrawVisibilityClippedSegment(pCur, curDrawPos2, tiles[currentPathIndex], Tile, pastMat, 1.0f);
                }
            }

            if (currentPathIndex + 1 < tiles.Count)
            {
                Vector3 pNext = GetElevatedTileCenter(tiles[currentPathIndex + 1]);
                if ((pNext - curDrawPos2).sqrMagnitude > 0.001f)
                {
                    DrawVisibilityClippedSegment(curDrawPos2, pNext, Tile, tiles[currentPathIndex + 1], activeMat, 1.2f);
                }

                for (int i = currentPathIndex + 1; i < tiles.Count - 1; i++)
                {
                    Vector3 pA = GetElevatedTileCenter(tiles[i]);
                    Vector3 pB = GetElevatedTileCenter(tiles[i + 1]);
                    DrawVisibilityClippedSegment(pA, pB, tiles[i], tiles[i + 1], activeMat, 1.2f);
                }
            }
        }
    }

    private SensorDomainFlags GetActiveDetectingDomains()
    {
        return SensoryManager.Instance?.GetDetectingDomainsForTarget(this) ?? SensorDomainFlags.None;
    }

    public override string GetInspectString()
    {
        if (GeoscapeRules.VisibilityMode != VisibilityMode.Omniscient && intelligenceTier < IntelligenceTier.Contact)
        {
            return string.Empty;
        }

        bool isClassified = GeoscapeRules.VisibilityMode == VisibilityMode.Omniscient ||
                            (isIdentified && intelligenceTier >= IntelligenceTier.Classified);

        string idTag = trackingId > 0
            ? (SensoryNarrativeUtility.IsLowTech ? $" [#{trackingId}]" : $" [Contact-{trackingId}]")
            : string.Empty;
        string factionName = isClassified
            ? (Faction?.Name ?? "Unknown")
            : (SensoryNarrativeUtility.IsLowTech ? "Unknown Faction" : "Unidentified Faction");
        SensorDomainFlags activeDomains = GetActiveDetectingDomains();
        string label = isClassified
            ? $"{GetIdentifiedGroupLabel()}{idTag}"
            : SensoryNarrativeUtility.GetContactLabel(trackingId: trackingId, domainFlags: activeDomains);

        if (isEngagedInTacticalMap)
        {
            int combatantCount = committedPawns?.Count(p => p != null && !p.Dead && !p.Destroyed) ?? 0;
            string countStr = combatantCount > 0 ? $" ({combatantCount} combatants)" : string.Empty;
            if (!isClassified)
            {
                return $"{label.CapitalizeFirst()}\nStatus: Engaged in active combat{countStr}\nLocation: Hex {Tile}";
            }
            return $"{label.CapitalizeFirst()} ({factionName})\nStatus: Engaged in active combat{countStr}\nLocation: Hex {Tile}";
        }

        if (isRetreating)
        {
            int survivorCount = retreatingPawns?.Count(p => p != null && !p.Dead && !p.Destroyed) ?? committedPawns?.Count(p => p != null && !p.Dead && !p.Destroyed) ?? 0;
            int captiveCount = kidnappedColonists?.Count ?? 0;
            string captiveStr = captiveCount > 0 ? $"\nCaptives: {captiveCount} abducted colonist(s)" : string.Empty;
            if (!isClassified)
            {
                return $"{label.CapitalizeFirst()}\nStatus: Retreating ({survivorCount} survivors){captiveStr}\nSpeed: ~{EffectiveSpeedTilesPerDay:F0} hexes/day";
            }
            Settlement? destSettlement = Find.WorldObjects?.SettlementAt(destinationTile);
            string destDesc = destSettlement != null ? $"{destSettlement.LabelCap} (Tile {destinationTile.tileId})" : $"Tile {destinationTile.tileId}";
            return $"{label.CapitalizeFirst()} ({factionName})\nStatus: Retreating ({survivorCount} survivors){captiveStr}\nDestination: {destDesc}";
        }

        bool isMechanoid = Faction != null && (Faction.def == FactionDefOf.Mechanoid || Faction.def.defName.Contains("Mechanoid"));
        float currentStepSpeed = EffectiveSpeedTilesPerDay;
        float effectiveSpeed = (GeoscapeRules.IncursionsRestAtNight && !isMechanoid)
            ? currentStepSpeed * (16f / 24f)
            : currentStepSpeed;
        float actualRemainingTiles = Math.Max(0f, (pathTiles != null ? (pathTiles.Count - currentPathIndex) : 0) - tileProgress);
        float remainingDays = actualRemainingTiles / Math.Max(0.1f, effectiveSpeed);
        float remainingHours = remainingDays * 24f;

        if (pursuitTargetCaravan != null && pursuitTargetCaravan.Spawned)
        {
            if (!isClassified)
            {
                return $"{label.CapitalizeFirst()}\nStatus: In hot pursuit\nSpeed: ~{currentStepSpeed:F0} hexes/day";
            }
            return $"{label.CapitalizeFirst()} ({factionName})\nStatus: In hot pursuit of {pursuitTargetCaravan.LabelCap}\nTarget: Hex {destinationTile}\nETA: ~{remainingHours:F1} hours ({Mathf.CeilToInt(actualRemainingTiles)} hexes remaining)";
        }

        if (isTurningBack)
        {
            if (!isClassified)
            {
                return $"{label.CapitalizeFirst()}\nStatus: Returning home\nSpeed: ~{currentStepSpeed:F0} hexes/day";
            }
            Settlement? destSettlement = Find.WorldObjects?.SettlementAt(destinationTile);
            string destDesc = destSettlement != null ? $"{destSettlement.LabelCap} (Tile {destinationTile.tileId})" : $"Tile {destinationTile.tileId}";
            return $"{label.CapitalizeFirst()} ({factionName})\nStatus: Returning home\nDestination: {destDesc}\nETA: ~{remainingHours:F1} hours ({Mathf.CeilToInt(actualRemainingTiles)} hexes remaining)";
        }

        if (departureDelayTicks > 0)
        {
            float delayHours = departureDelayTicks / 2500f;
            int totalTiles = Math.Max(0, pathTiles?.Count ?? 0);
            float transitHours = (totalTiles / Math.Max(0.1f, effectiveSpeed)) * 24f;
            if (!isClassified)
            {
                return $"{label.CapitalizeFirst()}\nMobilizing (Departing in ~{delayHours:F1} hours)\nEst. Transit: ~{transitHours:F1} hours ({totalTiles} hexes)";
            }
            return $"{label.CapitalizeFirst()} ({factionName})\nMobilizing at base (Departing in ~{delayHours:F1} hours)\nEst. Transit: ~{transitHours:F1} hours ({totalTiles} hexes)";
        }

        if (IsRestingAtNight)
        {
            float restHoursLeft = CaravanNightRestUtility.LeftRestTicksAt(Tile) / 2500f;
            remainingHours += restHoursLeft;

            if (!isClassified)
            {
                if (SensoryNarrativeUtility.IsLowTech)
                {
                    string designation = trackingId > 0 ? $"Approaching Strangers #{trackingId}" : "Approaching Strangers";
                    return $"{designation}\nStatus: Camped for the night\nPace: Stationary (Resuming march at dawn, ~{restHoursLeft:F1}h)\nObservations: Campfires spotted on horizon";
                }
                return $"{label.CapitalizeFirst()}\nStatus: Stationary (Camped at night)\nPace: Zero movement telemetry (~{restHoursLeft:F1}h until dawn)";
            }

            return $"{label.CapitalizeFirst()} ({factionName})\nCamped for the night (Resuming march at dawn, ~{restHoursLeft:F1}h)\nETA: ~{remainingHours:F1} hours ({Mathf.CeilToInt(actualRemainingTiles)} hexes remaining)";
        }

        if (!isClassified)
        {
            string domainTag = activeDomains != SensorDomainFlags.None
                ? $"Telemetry ({SensoryManager.FormatSensorDomainFlags(activeDomains)})"
                : "Telemetry";
            bool isMasked = SensoryTerrainUtility.IsTargetMasked(Tile);
            if (isMasked)
            {
                if (SensoryNarrativeUtility.IsLowTech)
                {
                    string designation = trackingId > 0 ? $"Approaching Strangers #{trackingId}" : "Approaching Strangers";
                    return $"{designation}\nStatus: Masked by rugged terrain\nPace: ~{currentStepSpeed:F0} hexes/day\nObservations: Force is navigating deep mountain valleys and canyons; classification delayed until exiting to open terrain";
                }
                return $"{label.CapitalizeFirst()}\nStatus: Terrain Masked\nSpeed: ~{currentStepSpeed:F0} hexes/day\n{domainTag}: Contact signal degraded by mountainous terrain masking; requires open terrain or SIGINT decryption to classify";
            }

            if (SensoryNarrativeUtility.IsLowTech)
            {
                string designation = trackingId > 0 ? $"Approaching Strangers #{trackingId}" : "Approaching Strangers";
                return $"{designation}\nStatus: Spotted in the distance\nPace: ~{currentStepSpeed:F0} hexes/day\nObservations: Identity and intent unknown from this distance";
            }
            return $"{label.CapitalizeFirst()}\nStatus: Tracking in progress\nSpeed: ~{currentStepSpeed:F0} hexes/day\n{domainTag}: Insufficient sensor data to classify faction";
        }

        Map? targetMap = Current.Game.FindMap(destinationTile);
        if (targetMap == null || !Find.Maps.Contains(targetMap))
        {
            Settlement? targetSettlement = Find.WorldObjects?.SettlementAt(destinationTile);
            if (targetSettlement == null || targetSettlement.Faction == null)
            {
                return $"{label.CapitalizeFirst()} ({factionName})\nTarget settlement is abandoned\nETA: ~{remainingHours:F1} hours ({Mathf.CeilToInt(actualRemainingTiles)} hexes remaining)";
            }
        }

        return $"{label.CapitalizeFirst()} ({factionName})\nETA: ~{remainingHours:F1} hours ({Mathf.CeilToInt(actualRemainingTiles)} hexes remaining)";
    }

    public override string GetDescription()
    {
        bool isClassified = GeoscapeRules.VisibilityMode == VisibilityMode.Omniscient ||
                            (isIdentified && intelligenceTier >= IntelligenceTier.Classified);

        if (!isClassified)
        {
            return SensoryNarrativeUtility.GetContactDescription();
        }

        string targetName = Current.Game?.FindMap(destinationTile)?.Parent?.LabelCap ??
                            Find.WorldObjects?.SettlementAt(destinationTile)?.LabelCap ??
                            Find.WorldObjects?.MapParentAt(destinationTile)?.LabelCap ??
                            (destinationTile.Valid ? $"territory at tile {destinationTile.tileId}" : "colony territory");

        switch (GroupKind)
        {
            case IncursionGroupKind.HostileWarband:
                return $"A hostile warband from {Faction?.Name ?? "an unknown enemy faction"} traversing the planetary surface towards {targetName}.";
            case IncursionGroupKind.TradeCaravan:
                if (isTradeMissionAborted)
                {
                    return $"A surviving merchant group from {Faction?.Name ?? "a friendly faction"} returning to base after losing their head merchant.";
                }
                string traderDetail = TraderKind != null ? $" ({TraderKind.label})" : string.Empty;
                return $"A merchant trade caravan{traderDetail} from {Faction?.Name ?? "a friendly faction"} travelling towards {targetName} with trade goods and pack animals.";
            case IncursionGroupKind.Visitors:
                return $"A party of visitors from {Faction?.Name ?? "a friendly faction"} travelling to socialise and visit {targetName}.";
            case IncursionGroupKind.Travelers:
                return $"A group of travellers from {Faction?.Name ?? "a neutral faction"} passing peacefully through the region.";
            case IncursionGroupKind.TributeCollector:
                return $"An imperial royal tribute collector from {Faction?.Name ?? "the Empire"} seeking tithes of gold or prisoners.";
            case IncursionGroupKind.Pilgrims:
                return $"A group of pilgrims from {Faction?.Name ?? "a friendly faction"} travelling on a spiritual journey.";
            case IncursionGroupKind.Beggars:
                return "A group of impoverished travellers in desperate need of charity and aid.";
            case IncursionGroupKind.AlliedReinforcements:
                return $"An allied military force from {Faction?.Name ?? "an allied faction"} marching to reinforce {targetName}.";
            default:
                return $"A travelling party from {Faction?.Name ?? "an unknown faction"} traversing the planetary surface.";
        }
    }

    // =========================================================================
    // ITrader & IThingHolder Implementations (Roadside Persistent Trading)
    // =========================================================================

    public TraderKindDef? TraderKind
    {
        get
        {
            if (!IsUnderlyingTradeCaravan) return null;

            if (explicitTraderKind != null) return explicitTraderKind;
            if (incidentParms?.traderKind != null) return incidentParms.traderKind;

            TraderKindDef? defKind = Faction?.def?.caravanTraderKinds?.FirstOrDefault()
                                  ?? Faction?.def?.visitorTraderKinds?.FirstOrDefault()
                                  ?? Faction?.def?.baseTraderKinds?.FirstOrDefault();
            if (defKind != null) return defKind;

            return DefDatabase<TraderKindDef>.GetNamedSilentFail("Caravan_Outlander_BulkGoods")
                ?? DefDatabase<TraderKindDef>.AllDefsListForReading.FirstOrDefault(tk => !tk.orbital);
        }
    }

}
