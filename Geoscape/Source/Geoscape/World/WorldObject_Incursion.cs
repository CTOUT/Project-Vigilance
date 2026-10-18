using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.World;

[StaticConstructorOnStartup]
public class WorldObject_Incursion : WorldObject
{
    private IncidentDef? incidentDef;
    private IncidentParms? incidentParms;
    private PlanetTile originTile;
    private PlanetTile destinationTile;
    private List<PlanetTile> pathTiles = new();
    private int currentPathIndex;
    private float tileProgress;
    private float speedTilesPerDay = 24f;
    private int departureDelayTicks;
    private bool isIdentified = true;

    public IncidentDef? IncidentDef => incidentDef;
    public IncidentParms? IncidentParms => incidentParms;
    public PlanetTile OriginTile => originTile;
    public PlanetTile DestinationTile => destinationTile;
    public IReadOnlyList<PlanetTile> PathTiles => pathTiles;
    public int CurrentPathIndex => currentPathIndex;
    public float TileProgress => tileProgress;
    public float SpeedTilesPerDay => speedTilesPerDay;
    public int DepartureDelayTicks => departureDelayTicks;
    public bool IsMobilizing => departureDelayTicks > 0;
    public bool IsIdentified
    {
        get => isIdentified;
        set => isIdentified = value;
    }

    private static Texture2D? cachedCaravanExpandingIcon;
    private static Texture2D CaravanExpandingIcon =>
        cachedCaravanExpandingIcon ??= ContentFinder<Texture2D>.Get("World/WorldObjects/Expanding/Caravan");

    private static Texture2D? cachedCaravanWorldTexture;
    private static Texture2D CaravanWorldTexture =>
        cachedCaravanWorldTexture ??= ContentFinder<Texture2D>.Get("World/WorldObjects/Caravan");

    public override Texture2D ExpandingIcon
    {
        get
        {
            // For both incursions and caravans, use the marching travellers icon so they are
            // immediately distinguishable from static settlement bases (which use FactionIcon)
            return CaravanExpandingIcon;
        }
    }

    public override Color ExpandingIconColor
    {
        get
        {
            bool isHostile = Faction?.HostileTo(Faction.OfPlayer) ?? true;
            if (isHostile)
            {
                return new Color(1f, 0.25f, 0.25f, 0.95f); // Vivid hostile red
            }

            return new Color(0.25f, 0.85f, 1f, 0.95f); // Tactical friendly cyan
        }
    }

    public override Material Material
    {
        get
        {
            bool isHostile = Faction?.HostileTo(Faction.OfPlayer) ?? true;
            Color incursionColor = isHostile
                ? new Color(1f, 0.25f, 0.25f, 0.95f)
                : new Color(0.25f, 0.85f, 1f, 0.95f);

            return MaterialPool.MatFrom(CaravanWorldTexture, ShaderDatabase.WorldOverlayTransparentLit, incursionColor, WorldMaterials.DynamicObjectRenderQueue);
        }
    }

    public override float DrawAltitude => 0.035f;

    public override void Draw()
    {
        float averageTileSize = Tile.Layer.AverageTileSize;
        float rawTransitionPct = ExpandableWorldObjectsUtility.RawTransitionPct;
        if (Tile.LayerDef.isSpace)
        {
            return;
        }

        float altOffset = Rand.RangeSeeded(0f, 0.01f, ID) + def.drawAltitudeOffset;
        bool isHostile = Faction?.HostileTo(Faction.OfPlayer) ?? true;
        Color statusColor = isHostile
            ? new Color(1f, 0.25f, 0.25f, 0.95f)
            : new Color(0.25f, 0.85f, 1f, 0.95f);

        // Alpha fade matching vanilla expandable objects transition
        float alpha = 1f;
        if (def.expandingIcon && rawTransitionPct > 0f && !ExpandableWorldObjectsUtility.HiddenByRules(this))
        {
            alpha = Mathf.Clamp01(1f - rawTransitionPct);
        }

        // 1. Base Layer: Caravan / marching force silhouette tinted in Red (hostile) or Cyan (friendly)
        Material baseMat = MaterialPool.MatFrom(
            CaravanWorldTexture,
            ShaderDatabase.WorldOverlayTransparentLit,
            new Color(statusColor.r, statusColor.g, statusColor.b, statusColor.a * alpha),
            WorldMaterials.DynamicObjectRenderQueue);

        // In RimWorld's DrawQuadTangentialToPlanet, rotationAngle = -90f aligns quad local Z (top) with planet North
        WorldRendererUtility.DrawQuadTangentialToPlanet(
            DrawPos,
            0.85f * averageTileSize,
            DrawAltitude + altOffset,
            baseMat,
            -90f,
            false,
            false,
            null);

        // 2. Overlay Layer: Faction Emblem centered atop the caravan silhouette (when identified and setting enabled)
        if (isIdentified && Faction?.def?.FactionIcon != null && GeoscapeMod.Settings.showFactionEmblems)
        {
            Color factionTint = Faction.Color;
            Material emblemMat = MaterialPool.MatFrom(
                Faction.def.FactionIcon,
                ShaderDatabase.WorldOverlayTransparentLit,
                new Color(factionTint.r, factionTint.g, factionTint.b, alpha),
                WorldMaterials.DynamicObjectRenderQueue);

            WorldRendererUtility.DrawQuadTangentialToPlanet(
                DrawPos,
                0.52f * averageTileSize,
                DrawAltitude + altOffset + 0.005f,
                emblemMat,
                -90f,
                false,
                false,
                null);
        }
    }

    public void SetupIncursion(
        IncidentDef def,
        IncidentParms parms,
        PlanetTile origin,
        PlanetTile destination,
        List<PlanetTile> path,
        float speed,
        int delayTicks = 0)
    {
        incidentDef = def;
        incidentParms = parms;
        originTile = origin;
        destinationTile = destination;
        pathTiles = path ?? new List<PlanetTile>();
        currentPathIndex = 0;
        tileProgress = 0f;
        speedTilesPerDay = speed > 0f ? speed : 24f;
        departureDelayTicks = Math.Max(0, delayTicks);

        if (parms.faction != null)
        {
            SetFaction(parms.faction);
        }

        if (pathTiles.Count > 0)
        {
            Tile = pathTiles[0];
        }
        else
        {
            Tile = origin;
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();

        // Cleanse target reference if the target Map was destroyed/abandoned to avoid orphaned Scribe cross-reference errors
        if (incidentParms != null && incidentParms.target is Map map && !Find.Maps.Contains(map))
        {
            incidentParms.target = null!;
        }

        Scribe_Defs.Look(ref incidentDef, "incidentDef");
        Scribe_Deep.Look(ref incidentParms, "incidentParms");
        Scribe_Values.Look(ref speedTilesPerDay, "speedTilesPerDay", 24f);
        Scribe_Values.Look(ref currentPathIndex, "currentPathIndex", 0);
        Scribe_Values.Look(ref tileProgress, "tileProgress", 0f);
        Scribe_Values.Look(ref departureDelayTicks, "departureDelayTicks", 0);
        Scribe_Values.Look(ref isIdentified, "isIdentified", true);

        int originId = originTile.tileId;
        Scribe_Values.Look(ref originId, "originTileId", 0);
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            originTile = (PlanetTile)originId;
        }

        int destinationId = destinationTile.tileId;
        Scribe_Values.Look(ref destinationId, "destinationTileId", 0);
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            destinationTile = (PlanetTile)destinationId;
        }

        List<int>? serializedPath = null;
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            serializedPath = pathTiles.Select(t => t.tileId).ToList();
        }

        Scribe_Collections.Look(ref serializedPath, "pathTileIds", LookMode.Value);

        if (Scribe.mode == LoadSaveMode.LoadingVars && serializedPath != null)
        {
            pathTiles = serializedPath.Select(id => (PlanetTile)id).ToList();
        }

        if (Scribe.mode == LoadSaveMode.PostLoadInit && incidentParms != null && incidentParms.target == null && destinationTile.Valid)
        {
            incidentParms.target = Current.Game.FindMap(destinationTile);
        }
    }

    protected override void Tick()
    {
        base.Tick();

        if (pathTiles == null || pathTiles.Count == 0)
        {
            IncursionArrivalManager.ExecuteArrival(this);
            return;
        }

        // If mobilizing at origin base, decrement delay and hold position
        if (departureDelayTicks > 0)
        {
            departureDelayTicks--;
            return;
        }

        float tilesPerTick = speedTilesPerDay / 60000f;
        tileProgress += tilesPerTick;

        while (tileProgress >= 1f)
        {
            tileProgress -= 1f;
            currentPathIndex++;

            if (currentPathIndex < pathTiles.Count)
            {
                Tile = pathTiles[currentPathIndex];
            }
            else
            {
                IncursionArrivalManager.ExecuteArrival(this);
                return;
            }
        }
    }

    public override Vector3 DrawPos
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
                return Find.WorldGrid.GetTileCenter(pathTiles[pathTiles.Count - 1]);
            }

            PlanetTile curTile = pathTiles[currentPathIndex];
            PlanetTile nextTile = pathTiles[currentPathIndex + 1];

            Vector3 curCenter = Find.WorldGrid.GetTileCenter(curTile);
            Vector3 nextCenter = Find.WorldGrid.GetTileCenter(nextTile);

            return Vector3.Slerp(curCenter, nextCenter, Mathf.Clamp01(tileProgress));
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
        base.DrawExtraSelectionOverlays();
        DrawPathOverlay();
    }

    private void DrawPathOverlay()
    {
        if (pathTiles == null || pathTiles.Count < 2)
        {
            return;
        }

        bool isHostile = Faction?.HostileTo(Faction.OfPlayer) ?? true;
        Material activeMat = isHostile ? HostileLineMat : FriendlyLineMat;
        Material pastMat = isHostile ? HostilePastLineMat : FriendlyPastLineMat;

        // If mobilizing at origin base, draw the full planned path as active
        if (departureDelayTicks > 0)
        {
            for (int i = 0; i < pathTiles.Count - 1; i++)
            {
                Vector3 pA = GetElevatedTileCenter(pathTiles[i]);
                Vector3 pB = GetElevatedTileCenter(pathTiles[i + 1]);
                GenDraw.DrawWorldLineBetween(pA, pB, activeMat, 1.2f);
            }
            return;
        }

        // 1. Draw traversed past path (from origin up to currentPathIndex)
        for (int i = 0; i < currentPathIndex; i++)
        {
            Vector3 pA = GetElevatedTileCenter(pathTiles[i]);
            Vector3 pB = GetElevatedTileCenter(pathTiles[i + 1]);
            GenDraw.DrawWorldLineBetween(pA, pB, pastMat, 1.0f);
        }

        // 2. Draw current leg connecting to smooth DrawPos
        Vector3 curDrawPos = DrawPos + DrawPos.normalized * 0.08f;

        if (currentPathIndex < pathTiles.Count)
        {
            Vector3 pCur = GetElevatedTileCenter(pathTiles[currentPathIndex]);
            if ((curDrawPos - pCur).sqrMagnitude > 0.001f)
            {
                GenDraw.DrawWorldLineBetween(pCur, curDrawPos, pastMat, 1.0f);
            }
        }

        if (currentPathIndex + 1 < pathTiles.Count)
        {
            Vector3 pNext = GetElevatedTileCenter(pathTiles[currentPathIndex + 1]);
            if ((pNext - curDrawPos).sqrMagnitude > 0.001f)
            {
                GenDraw.DrawWorldLineBetween(curDrawPos, pNext, activeMat, 1.2f);
            }

            // 3. Draw remaining upcoming path from next tile to destination
            for (int i = currentPathIndex + 1; i < pathTiles.Count - 1; i++)
            {
                Vector3 pA = GetElevatedTileCenter(pathTiles[i]);
                Vector3 pB = GetElevatedTileCenter(pathTiles[i + 1]);
                GenDraw.DrawWorldLineBetween(pA, pB, activeMat, 1.2f);
            }
        }
    }

    public override string GetInspectString()
    {
        float actualRemainingTiles = Math.Max(0f, (pathTiles.Count - currentPathIndex) - tileProgress);
        float remainingDays = actualRemainingTiles / Math.Max(0.1f, speedTilesPerDay);
        float remainingHours = remainingDays * 24f;

        string factionName = isIdentified ? (Faction?.Name ?? "Unknown") : "Unidentified Faction";
        string label = isIdentified
            ? (incidentDef?.label ?? def.label ?? "Approaching Force")
            : "Unidentified Contact";

        if (departureDelayTicks > 0)
        {
            float delayHours = departureDelayTicks / 2500f;
            int totalTiles = Math.Max(0, pathTiles.Count);
            float transitHours = (totalTiles / Math.Max(0.1f, speedTilesPerDay)) * 24f;
            return $"{label.CapitalizeFirst()} ({factionName})\nMobilizing at base (Departing in ~{delayHours:F1} hours)\nEst. Transit: ~{transitHours:F1} hours ({totalTiles} hexes)";
        }

        Map? targetMap = Current.Game.FindMap(destinationTile);
        if (targetMap == null || !Find.Maps.Contains(targetMap))
        {
            return $"{label.CapitalizeFirst()} ({factionName})\nTarget settlement is abandoned\nETA: ~{remainingHours:F1} hours ({Mathf.CeilToInt(actualRemainingTiles)} hexes remaining)";
        }

        return $"{label.CapitalizeFirst()} ({factionName})\nETA: ~{remainingHours:F1} hours ({Mathf.CeilToInt(actualRemainingTiles)} hexes remaining)";
    }
}
