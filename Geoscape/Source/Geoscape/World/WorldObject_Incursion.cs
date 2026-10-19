using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.World;

[StaticConstructorOnStartup]
public class WorldObject_Incursion : WorldObject, IDetectableTarget
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
    private bool isIdentified = false;
    private IntelligenceTier intelligenceTier = IntelligenceTier.Undetected;
    private bool hasDispatchedContactAlert = false;
    private bool hasDispatchedClassifiedAlert = false;
    private Vector3 lastSampledDrawPos = Vector3.zero;

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

    // IDetectableTarget Implementation
    public string TargetLabel => LabelCap;
    public PlanetTile CurrentTile => Tile;
    public Vector3 CurrentDrawPos => TrueDrawPos;
    public Faction? TargetFaction => Faction;
    public bool IsHostileToPlayer => Faction?.HostileTo(Faction.OfPlayer) ?? true;
    public IntelligenceTier CurrentIntelligenceTier
    {
        get => intelligenceTier;
        set => intelligenceTier = value;
    }

    public void UpdateSampledPosition()
    {
        lastSampledDrawPos = TrueDrawPos;
    }

    private static Texture2D? cachedCaravanExpandingIcon;
    private static Texture2D CaravanExpandingIcon =>
        cachedCaravanExpandingIcon ??= ContentFinder<Texture2D>.Get("World/WorldObjects/Expanding/Caravan");

    private static Texture2D? cachedCaravanWorldTexture;
    private static Texture2D CaravanWorldTexture =>
        cachedCaravanWorldTexture ??= ContentFinder<Texture2D>.Get("World/WorldObjects/Caravan");

    public override bool SelectableNow =>
        GeoscapeMod.Settings.visibilityMode == VisibilityMode.Omniscient || intelligenceTier >= IntelligenceTier.Contact;

    public override string Label
    {
        get
        {
            if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient && intelligenceTier == IntelligenceTier.Contact)
            {
                return "Unidentified Contact";
            }
            return base.Label;
        }
    }

    public override Texture2D ExpandingIcon
    {
        get
        {
            if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient && intelligenceTier < IntelligenceTier.Contact)
            {
                return BaseContent.ClearTex;
            }
            return CaravanExpandingIcon;
        }
    }

    /// <summary>
    /// Calculates a calm, rhythmic tactical amber pulse (breathing beacon) for unidentified contacts.
    /// Uses real-world time with a smooth sinusoidal curve (~2.5s cycle) rather than high-frequency strobing.
    /// </summary>
    public static Color TacticalAmberPulseColor(float alphaMultiplier = 1f)
    {
        // 2.5s smooth breathing cycle (~0.4 Hz) easing between 0.55 and 0.95 alpha
        float pulse = 0.55f + 0.40f * (0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 2.5f));
        return new Color(1f, 0.82f, 0.20f, pulse * alphaMultiplier);
    }

    public override Color ExpandingIconColor
    {
        get
        {
            if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient && intelligenceTier == IntelligenceTier.Contact)
            {
                return TacticalAmberPulseColor(0.95f);
            }

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
        if (!WorldRendererUtility.WorldRendered || Tile.LayerDef.isSpace)
        {
            return;
        }

        // Hide entirely if undetected by the sensory net
        if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient && intelligenceTier < IntelligenceTier.Contact)
        {
            return;
        }

        float averageTileSize = Tile.Layer.AverageTileSize;
        float rawTransitionPct = ExpandableWorldObjectsUtility.RawTransitionPct;
        float altOffset = Rand.RangeSeeded(0f, 0.01f, ID) + def.drawAltitudeOffset;

        bool isHostile = Faction?.HostileTo(Faction.OfPlayer) ?? true;
        Color statusColor;

        if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient && intelligenceTier == IntelligenceTier.Contact)
        {
            // Calm tactical amber breathing blip for unidentified contacts
            statusColor = TacticalAmberPulseColor(1f);
        }
        else
        {
            statusColor = isHostile
                ? new Color(1f, 0.25f, 0.25f, 0.95f)
                : new Color(0.25f, 0.85f, 1f, 0.95f);
        }

        // Alpha fade matching vanilla expandable objects transition
        float alpha = 1f;
        if (def.expandingIcon && rawTransitionPct > 0f && !ExpandableWorldObjectsUtility.HiddenByRules(this))
        {
            alpha = Mathf.Clamp01(1f - rawTransitionPct);
        }

        // 1. Base Layer: Caravan / marching force silhouette
        Material baseMat = MaterialPool.MatFrom(
            CaravanWorldTexture,
            ShaderDatabase.WorldOverlayTransparentLit,
            new Color(statusColor.r, statusColor.g, statusColor.b, statusColor.a * alpha),
            WorldMaterials.DynamicObjectRenderQueue);

        WorldRendererUtility.DrawQuadTangentialToPlanet(
            DrawPos,
            0.85f * averageTileSize,
            DrawAltitude + altOffset,
            baseMat,
            -90f,
            false,
            false,
            null);

        // 2. Overlay Layer: Faction Emblem (ONLY when Classified or Analysed)
        bool canShowEmblem = GeoscapeMod.Settings.visibilityMode == VisibilityMode.Omniscient || intelligenceTier >= IntelligenceTier.Classified;
        if (canShowEmblem && isIdentified && Faction?.def?.FactionIcon != null && GeoscapeMod.Settings.showFactionEmblems)
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

        isIdentified = (GeoscapeMod.Settings.visibilityMode == VisibilityMode.Omniscient);
        intelligenceTier = (GeoscapeMod.Settings.visibilityMode == VisibilityMode.Omniscient)
            ? IntelligenceTier.Analysed
            : IntelligenceTier.Undetected;
        hasDispatchedContactAlert = false;
        hasDispatchedClassifiedAlert = false;
        lastSampledDrawPos = TrueDrawPos;

        // Register target with SensoryManager and trigger immediate detection sweep
        SensoryManager.Instance?.RegisterTarget(this);
        SensoryManager.Instance?.PerformSensorySweep();
    }

    public override void SpawnSetup()
    {
        base.SpawnSetup();
        if (lastSampledDrawPos == Vector3.zero)
        {
            lastSampledDrawPos = TrueDrawPos;
        }
        SensoryManager.Instance?.RegisterTarget(this);
    }

    public override void PostRemove()
    {
        SensoryManager.Instance?.DeregisterTarget(this);
        base.PostRemove();
    }

    public void OnDetected(ISensorProvider sensor, IntelligenceTier newTier)
    {
        intelligenceTier = newTier;

        if (newTier >= IntelligenceTier.Classified)
        {
            isIdentified = true;
        }

        UpdateSampledPosition();

        if (GeoscapeMod.Settings.enableEarlyWarningLetters)
        {
            // If immediately detected at Classified+ (e.g. adjacent spawn or high-spec sensor),
            // bypass the vague Contact blip alert and directly issue the detailed identification alert.
            if (newTier >= IntelligenceTier.Classified)
            {
                if (!hasDispatchedClassifiedAlert)
                {
                    hasDispatchedContactAlert = true;
                    hasDispatchedClassifiedAlert = true;
                    DispatchClassifiedLetter(sensor);
                }
            }
            else if (newTier >= IntelligenceTier.Contact && !hasDispatchedContactAlert)
            {
                hasDispatchedContactAlert = true;
                DispatchContactLetter(sensor);
            }
        }
    }

    private void DispatchContactLetter(ISensorProvider sensor)
    {
        Map? targetMap = Current.Game.FindMap(destinationTile);
        string targetName = targetMap?.Parent?.LabelCap ?? "Colony territory";

        string title = "Early Warning: Unidentified Contact";
        string text = $"{sensor.SensorLabel} have detected an unidentified force advancing towards {targetName}.\n\n" +
                      $"Heading: Towards {targetName}\n" +
                      $"Estimated Speed: ~{speedTilesPerDay:F0} hexes/day\n" +
                      $"Sensor: {sensor.SensorLabel}\n\n" +
                      "Telemetry is currently insufficient to determine faction alignment or intentions. Prepare colony defenses or scramble scouts to investigate.";

        Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.ThreatSmall, new GlobalTargetInfo(this));
    }

    private void DispatchClassifiedLetter(ISensorProvider sensor)
    {
        Map? targetMap = Current.Game.FindMap(destinationTile);
        string targetName = targetMap?.Parent?.LabelCap ?? "Colony territory";

        float actualRemainingTiles = Math.Max(0f, (pathTiles.Count - currentPathIndex) - tileProgress);
        float remainingHours = (actualRemainingTiles / Math.Max(0.1f, speedTilesPerDay)) * 24f;

        string factionStr = Faction?.Name ?? "Unknown Faction";

        if (IsHostileToPlayer)
        {
            string title = $"Hostile Incursion: {factionStr}";
            string text = $"{sensor.SensorLabel} have visually confirmed that the approaching contact is a hostile warband from {factionStr} marching towards {targetName}.\n\n" +
                          $"Target: {targetName}\n" +
                          $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                          $"Sensor: {sensor.SensorLabel}\n\n" +
                          "Prepare your colony for tactical engagement, or deploy an interception caravan to confront them in the field.";

            Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.ThreatBig, new GlobalTargetInfo(this));
        }
        else
        {
            string groupType = incidentDef?.label ?? "caravan";
            string title = $"Approaching {groupType.CapitalizeFirst()}: {factionStr}";
            string text = $"{sensor.SensorLabel} have visually confirmed that the approaching contact is a friendly {groupType} from {factionStr} heading towards {targetName}.\n\n" +
                          $"Target: {targetName}\n" +
                          $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                          $"Sensor: {sensor.SensorLabel}\n\n" +
                          "Prepare your negotiators, silver stores, and trade goods for their arrival.";

            Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.PositiveEvent, new GlobalTargetInfo(this));
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
        Scribe_Values.Look(ref intelligenceTier, "intelligenceTier", IntelligenceTier.Undetected);
        Scribe_Values.Look(ref hasDispatchedContactAlert, "hasDispatchedContactAlert", false);
        Scribe_Values.Look(ref hasDispatchedClassifiedAlert, "hasDispatchedClassifiedAlert", false);
        Scribe_Values.Look(ref lastSampledDrawPos, "lastSampledDrawPos", Vector3.zero);

        bool legacyAlert = false;
        Scribe_Values.Look(ref legacyAlert, "hasDispatchedAlert", false);
        if (legacyAlert)
        {
            hasDispatchedContactAlert = true;
            hasDispatchedClassifiedAlert = true;
        }

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
                return Find.WorldGrid.GetTileCenter(pathTiles[pathTiles.Count - 1]);
            }

            PlanetTile curTile = pathTiles[currentPathIndex];
            PlanetTile nextTile = pathTiles[currentPathIndex + 1];

            Vector3 curCenter = Find.WorldGrid.GetTileCenter(curTile);
            Vector3 nextCenter = Find.WorldGrid.GetTileCenter(nextTile);

            return Vector3.Slerp(curCenter, nextCenter, Mathf.Clamp01(tileProgress));
        }
    }

    public override Vector3 DrawPos
    {
        get
        {
            // In Contact Tier, position updates discretely on 250-tick sweeps rather than 60 FPS continuous Slerp
            if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient && intelligenceTier == IntelligenceTier.Contact)
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
        base.DrawExtraSelectionOverlays();
        DrawPathOverlay();
    }

    private void DrawPathOverlay()
    {
        if (!WorldRendererUtility.WorldRendered || pathTiles == null || pathTiles.Count < 2)
        {
            return;
        }

        List<PlanetTile> tiles = pathTiles;

        // In Contact tier, do not reveal the entire projected planetary path to the player
        if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient && intelligenceTier < IntelligenceTier.Classified)
        {
            Vector3 curDrawPos = DrawPos + DrawPos.normalized * 0.08f;
            if (currentPathIndex < tiles.Count)
            {
                Vector3 pCur = GetElevatedTileCenter(tiles[currentPathIndex]);
                if ((curDrawPos - pCur).sqrMagnitude > 0.001f)
                {
                    GenDraw.DrawWorldLineBetween(pCur, curDrawPos, FriendlyPastLineMat, 1.0f);
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
                GenDraw.DrawWorldLineBetween(pA, pB, activeMat, 1.2f);
            }
            return;
        }

        // 1. Draw traversed past path (from origin up to currentPathIndex)
        for (int i = 0; i < currentPathIndex; i++)
        {
            Vector3 pA = GetElevatedTileCenter(tiles[i]);
            Vector3 pB = GetElevatedTileCenter(tiles[i + 1]);
            GenDraw.DrawWorldLineBetween(pA, pB, pastMat, 1.0f);
        }

        // 2. Draw current leg connecting to smooth DrawPos
        Vector3 curDrawPos2 = DrawPos + DrawPos.normalized * 0.08f;

        if (currentPathIndex < tiles.Count)
        {
            Vector3 pCur = GetElevatedTileCenter(tiles[currentPathIndex]);
            if ((curDrawPos2 - pCur).sqrMagnitude > 0.001f)
            {
                GenDraw.DrawWorldLineBetween(pCur, curDrawPos2, pastMat, 1.0f);
            }
        }

        if (currentPathIndex + 1 < tiles.Count)
        {
            Vector3 pNext = GetElevatedTileCenter(tiles[currentPathIndex + 1]);
            if ((pNext - curDrawPos2).sqrMagnitude > 0.001f)
            {
                GenDraw.DrawWorldLineBetween(curDrawPos2, pNext, activeMat, 1.2f);
            }

            // 3. Draw remaining upcoming path from next tile to destination
            for (int i = currentPathIndex + 1; i < tiles.Count - 1; i++)
            {
                Vector3 pA = GetElevatedTileCenter(tiles[i]);
                Vector3 pB = GetElevatedTileCenter(tiles[i + 1]);
                GenDraw.DrawWorldLineBetween(pA, pB, activeMat, 1.2f);
            }
        }
    }

    public override string GetInspectString()
    {
        if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient && intelligenceTier < IntelligenceTier.Contact)
        {
            return string.Empty;
        }

        float actualRemainingTiles = Math.Max(0f, (pathTiles.Count - currentPathIndex) - tileProgress);
        float remainingDays = actualRemainingTiles / Math.Max(0.1f, speedTilesPerDay);
        float remainingHours = remainingDays * 24f;

        if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient && intelligenceTier == IntelligenceTier.Contact)
        {
            return $"Unidentified Contact\nStatus: Tracking in progress\nSpeed: ~{speedTilesPerDay:F0} hexes/day\nTelemetry: Insufficient sensor data to classify faction";
        }

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
