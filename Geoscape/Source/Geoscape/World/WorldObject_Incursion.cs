using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.World;

/// <summary>
/// Broad tactical category of planetary incursion, differentiating trade merchants, visitors,
/// passers-by, imperial tribute collectors, pilgrims, and hostile combatants.
/// </summary>
public enum IncursionGroupKind
{
    HostileWarband,
    TradeCaravan,
    Visitors,
    Travelers,
    TributeCollector,
    Pilgrims,
    Beggars,
    AlliedReinforcements,
    GenericNeutral
}

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
    private int trackingId;

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
    public int TrackingId => trackingId;

    public bool IsRestingAtNight
    {
        get
        {
            if (!GeoscapeMod.Settings.incursionsRestAtNight)
            {
                return false;
            }

            // Incursions at their final destination tile push forward to begin arrival/attack
            if (currentPathIndex >= pathTiles.Count - 1)
            {
                return false;
            }

            // Mechanoids, ancient automatons, and supernatural horrors never sleep
            if (Faction != null && (Faction.def == FactionDefOf.Mechanoid || Faction.def.defName.Contains("Mechanoid")))
            {
                return false;
            }

            if (incidentDef != null)
            {
                string defName = incidentDef.defName;
                if (defName.Contains("Mechanoid") || defName.Contains("Entity") || defName.Contains("Chimera") || defName.Contains("Devourer") || defName.Contains("Gorehulk"))
                {
                    return false;
                }
            }

            // Biological mortal factions camp during local solar night
            return Tile.Valid && CaravanNightRestUtility.RestingNowAt(Tile);
        }
    }

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
        set
        {
            intelligenceTier = value;
            if (value < IntelligenceTier.Contact && GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient)
            {
                if (Find.WorldSelector != null && Find.WorldSelector.IsSelected(this))
                {
                    Find.WorldSelector.Deselect(this);
                }
            }
        }
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

    public override bool NeverMultiSelect =>
        GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient && intelligenceTier < IntelligenceTier.Contact;

    public override string LabelShort => Label;

    public override string Label
    {
        get
        {
            if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient && !isIdentified)
            {
                return SensoryNarrativeUtility.GetContactLabel(trackingId: trackingId);
            }

            if (isIdentified && (incidentDef != null || Faction != null))
            {
                string idTag = trackingId > 0
                    ? (SensoryNarrativeUtility.IsLowTech ? $" [#{trackingId}]" : $" [Contact-{trackingId}]")
                    : string.Empty;
                return $"{GetIdentifiedGroupLabel()}{idTag}";
            }

            return base.Label;
        }
    }

    /// <summary>
    /// Resolves the specific tactical kind of this incursion based on incident parameters and defs.
    /// Distinguishes between trade caravans, social visitors, passing travellers, tribute collectors, and hostile warbands.
    /// </summary>
    public IncursionGroupKind GroupKind
    {
        get
        {
            if (IsHostileToPlayer)
            {
                return IncursionGroupKind.HostileWarband;
            }

            if (incidentParms?.traderKind != null ||
                (incidentParms?.pawnGroupKind != null && incidentParms.pawnGroupKind == PawnGroupKindDefOf.Trader))
            {
                return IncursionGroupKind.TradeCaravan;
            }

            string defName = incidentDef?.defName ?? string.Empty;
            string defLabel = incidentDef?.label ?? string.Empty;

            if (defName.Equals("TraderCaravanArrival", StringComparison.OrdinalIgnoreCase) ||
                defName.IndexOf("Trader", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defLabel.IndexOf("trader", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defLabel.IndexOf("caravan", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defLabel.IndexOf("merchant", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return IncursionGroupKind.TradeCaravan;
            }

            if (defName.Equals("VisitorGroup", StringComparison.OrdinalIgnoreCase) ||
                defName.IndexOf("Visitor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defLabel.IndexOf("visitor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defLabel.IndexOf("guest", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return IncursionGroupKind.Visitors;
            }

            if (defName.Equals("TravelerGroup", StringComparison.OrdinalIgnoreCase) ||
                defName.IndexOf("Traveler", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defLabel.IndexOf("traveler", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defLabel.IndexOf("traveller", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defLabel.IndexOf("passer", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return IncursionGroupKind.Travelers;
            }

            if (defName.IndexOf("Tribute", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defLabel.IndexOf("tribute", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return IncursionGroupKind.TributeCollector;
            }

            if (defName.IndexOf("Pilgrim", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defLabel.IndexOf("pilgrim", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return IncursionGroupKind.Pilgrims;
            }

            if (defName.IndexOf("Beggar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defLabel.IndexOf("beggar", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return IncursionGroupKind.Beggars;
            }

            if (defName.IndexOf("RaidFriendly", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defName.IndexOf("MilitaryAid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defLabel.IndexOf("reinforcement", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defLabel.IndexOf("allies", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return IncursionGroupKind.AlliedReinforcements;
            }

            return IncursionGroupKind.GenericNeutral;
        }
    }

    /// <summary>
    /// Returns a human-readable lowercase label distinguishing the identified group.
    /// </summary>
    public string GetIdentifiedGroupLabel()
    {
        switch (GroupKind)
        {
            case IncursionGroupKind.HostileWarband:
                return "hostile warband";
            case IncursionGroupKind.TradeCaravan:
                return incidentParms?.traderKind != null
                    ? $"trade caravan ({incidentParms.traderKind.label})"
                    : "trade caravan";
            case IncursionGroupKind.Visitors:
                return "visitors";
            case IncursionGroupKind.Travelers:
                return "travellers";
            case IncursionGroupKind.TributeCollector:
                return "royal tribute collector";
            case IncursionGroupKind.Pilgrims:
                return "pilgrims";
            case IncursionGroupKind.Beggars:
                return "beggars";
            case IncursionGroupKind.AlliedReinforcements:
                return "allied reinforcements";
            default:
                return incidentDef?.label ?? "peaceful party";
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
    /// Uses real-world time with a smooth sinusoidal curve (~2.5s cycle) easing between 0.18 and 1.0 alpha.
    /// </summary>
    public static Color TacticalAmberPulseColor(float alphaMultiplier = 1f)
    {
        // Smooth breathing cycle (~2.5s) easing between 0.18 (deep translucency) and 1.0 (vivid beacon)
        float sinVal = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 2.5f);
        float pulse = 0.18f + 0.82f * sinVal;
        return new Color(1f, 0.78f, 0.15f, pulse * alphaMultiplier);
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

        if (newTier >= IntelligenceTier.Contact && trackingId <= 0 && SensoryManager.Instance != null)
        {
            trackingId = SensoryManager.Instance.AllocateContactTrackingId();
        }

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

        string title = SensoryNarrativeUtility.GetContactLetterTitle(sensor, trackingId);
        string text = SensoryNarrativeUtility.GetContactLetterBody(sensor, targetName, speedTilesPerDay, trackingId);

        Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.ThreatSmall, new GlobalTargetInfo(this));
    }

    private void DispatchClassifiedLetter(ISensorProvider sensor)
    {
        Map? targetMap = Current.Game.FindMap(destinationTile);
        string targetName = targetMap?.Parent?.LabelCap ?? "Colony territory";

        float actualRemainingTiles = Math.Max(0f, (pathTiles.Count - currentPathIndex) - tileProgress);
        float remainingHours = (actualRemainingTiles / Math.Max(0.1f, speedTilesPerDay)) * 24f;

        string factionStr = Faction?.Name ?? "Unknown Faction";
        string attrLine = SensoryNarrativeUtility.GetAttributionLine(sensor);
        string idPrefix = trackingId > 0
            ? (SensoryNarrativeUtility.IsLowTech ? $"Strangers #{trackingId} Classified: " : $"Contact-{trackingId} Classified: ")
            : string.Empty;
        string contactName = trackingId > 0
            ? (SensoryNarrativeUtility.IsLowTech ? $"strangers #{trackingId}" : $"Contact-{trackingId}")
            : "the approaching contact";

        switch (GroupKind)
        {
            case IncursionGroupKind.HostileWarband:
                {
                    string title = $"{idPrefix}Hostile Incursion ({factionStr})";
                    string closingAdvice = SensoryNarrativeUtility.GetHostileClosingAdvice(sensor);
                    string text = $"{sensor.SensorLabel} have confirmed that {contactName} is a hostile warband from {factionStr} marching towards {targetName}.\n\n" +
                                  $"Target: {targetName}\n" +
                                  $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                                  $"{attrLine}\n\n" +
                                  $"{closingAdvice}";

                    Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.ThreatBig, new GlobalTargetInfo(this));
                    break;
                }

            case IncursionGroupKind.TradeCaravan:
                {
                    string traderDetail = incidentParms?.traderKind != null ? $" ({incidentParms.traderKind.label})" : string.Empty;
                    string title = $"{idPrefix}Approaching Trade Caravan ({factionStr})";
                    string text = $"{sensor.SensorLabel} have confirmed that {contactName} is a friendly trade caravan{traderDetail} from {factionStr} heading towards {targetName}.\n\n" +
                                  $"Target: {targetName}\n" +
                                  $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                                  $"{attrLine}\n\n" +
                                  "They are bringing goods and silver to trade. Prepare your negotiators, silver stores, and trade stockpiles for their arrival.";

                    Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.PositiveEvent, new GlobalTargetInfo(this));
                    break;
                }

            case IncursionGroupKind.Visitors:
                {
                    string title = $"{idPrefix}Approaching Visitors ({factionStr})";
                    string text = $"{sensor.SensorLabel} have confirmed that {contactName} is a party of visitors from {factionStr} travelling towards {targetName}.\n\n" +
                                  $"Target: {targetName}\n" +
                                  $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                                  $"{attrLine}\n\n" +
                                  "They are visiting to socialise and relax. They are not a merchant caravan, though individual visitors may carry personal items to barter. Ensure your colony has a hospitable social space and spare guest accommodations.";

                    Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.NeutralEvent, new GlobalTargetInfo(this));
                    break;
                }

            case IncursionGroupKind.Travelers:
                {
                    string title = $"{idPrefix}Travellers Passing Near ({factionStr})";
                    string text = $"{sensor.SensorLabel} have confirmed that {contactName} is a group of travellers from {factionStr} moving through the region near {targetName}.\n\n" +
                                  $"Target: {targetName}\n" +
                                  $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                                  $"{attrLine}\n\n" +
                                  "They are simply passing through your territory and do not intend to visit, stay, or trade.";

                    Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.NeutralEvent, new GlobalTargetInfo(this));
                    break;
                }

            case IncursionGroupKind.TributeCollector:
                {
                    string title = $"{idPrefix}Royal Tribute Collector ({factionStr})";
                    string text = $"{sensor.SensorLabel} have confirmed that {contactName} is a royal tribute collector from {factionStr} heading towards {targetName}.\n\n" +
                                  $"Target: {targetName}\n" +
                                  $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                                  $"{attrLine}\n\n" +
                                  "They seek offerings of gold or enslaved prisoners in exchange for royal favour and honour.";

                    Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.PositiveEvent, new GlobalTargetInfo(this));
                    break;
                }

            case IncursionGroupKind.Pilgrims:
                {
                    string title = $"{idPrefix}Approaching Pilgrims ({factionStr})";
                    string text = $"{sensor.SensorLabel} have confirmed that {contactName} is a group of pilgrims from {factionStr} travelling towards {targetName}.\n\n" +
                                  $"Target: {targetName}\n" +
                                  $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                                  $"{attrLine}\n\n" +
                                  "They are travelling to venerate sacred relics or holy sites at your settlement.";

                    Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.NeutralEvent, new GlobalTargetInfo(this));
                    break;
                }

            case IncursionGroupKind.Beggars:
                {
                    string title = $"{idPrefix}Approaching Beggars ({factionStr})";
                    string beggarsDetail = SensoryNarrativeUtility.GetBeggarsIntentDescription();
                    string text = $"{sensor.SensorLabel} have confirmed that {contactName} is an impoverished group travelling towards {targetName}.\n\n" +
                                  $"Target: {targetName}\n" +
                                  $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                                  $"{attrLine}\n\n" +
                                  $"{beggarsDetail}";

                    Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.NeutralEvent, new GlobalTargetInfo(this));
                    break;
                }

            case IncursionGroupKind.AlliedReinforcements:
                {
                    string title = $"{idPrefix}Allied Reinforcements ({factionStr})";
                    string text = $"{sensor.SensorLabel} have confirmed that {contactName} is an allied military contingent from {factionStr} marching towards {targetName} to reinforce your defences.\n\n" +
                                  $"Target: {targetName}\n" +
                                  $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                                  $"{attrLine}\n\n" +
                                  "They are armed and ready to assist against planetary threats.";

                    Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.PositiveEvent, new GlobalTargetInfo(this));
                    break;
                }

            default:
                {
                    string groupType = GetIdentifiedGroupLabel();
                    string title = $"{idPrefix}Approaching {groupType.CapitalizeFirst()} ({factionStr})";
                    string text = $"{sensor.SensorLabel} have confirmed that {contactName} ({groupType}) from {factionStr} is heading towards {targetName}.\n\n" +
                                  $"Target: {targetName}\n" +
                                  $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                                  $"{attrLine}\n\n" +
                                  "They are approaching peacefully without hostile intent.";

                    Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.NeutralEvent, new GlobalTargetInfo(this));
                    break;
                }
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
        Scribe_Values.Look(ref trackingId, "trackingId", 0);

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

        if (intelligenceTier < IntelligenceTier.Contact && GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient)
        {
            if (Find.WorldSelector != null && Find.WorldSelector.IsSelected(this))
            {
                Find.WorldSelector.Deselect(this);
            }
        }

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

        // Nighttime Rest: Biological mortal forces halt and camp at night
        if (IsRestingAtNight)
        {
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
        if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient && intelligenceTier < IntelligenceTier.Contact)
        {
            return;
        }

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

        string idTag = trackingId > 0
            ? (SensoryNarrativeUtility.IsLowTech ? $" [#{trackingId}]" : $" [Contact-{trackingId}]")
            : string.Empty;
        string factionName = isIdentified
            ? (Faction?.Name ?? "Unknown")
            : (SensoryNarrativeUtility.IsLowTech ? "Unknown Faction" : "Unidentified Faction");
        string label = isIdentified
            ? $"{GetIdentifiedGroupLabel()}{idTag}"
            : SensoryNarrativeUtility.GetContactLabel(trackingId: trackingId);

        bool isMechanoid = Faction != null && (Faction.def == FactionDefOf.Mechanoid || Faction.def.defName.Contains("Mechanoid"));
        float effectiveSpeed = (GeoscapeMod.Settings.incursionsRestAtNight && !isMechanoid)
            ? speedTilesPerDay * (16f / 24f)
            : speedTilesPerDay;
        float actualRemainingTiles = Math.Max(0f, (pathTiles.Count - currentPathIndex) - tileProgress);
        float remainingDays = actualRemainingTiles / Math.Max(0.1f, effectiveSpeed);
        float remainingHours = remainingDays * 24f;

        if (departureDelayTicks > 0)
        {
            float delayHours = departureDelayTicks / 2500f;
            int totalTiles = Math.Max(0, pathTiles.Count);
            float transitHours = (totalTiles / Math.Max(0.1f, effectiveSpeed)) * 24f;
            return $"{label.CapitalizeFirst()} ({factionName})\nMobilizing at base (Departing in ~{delayHours:F1} hours)\nEst. Transit: ~{transitHours:F1} hours ({totalTiles} hexes)";
        }

        if (IsRestingAtNight)
        {
            float restHoursLeft = CaravanNightRestUtility.LeftRestTicksAt(Tile) / 2500f;
            remainingHours += restHoursLeft;

            if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient && intelligenceTier == IntelligenceTier.Contact)
            {
                if (SensoryNarrativeUtility.IsLowTech)
                {
                    string designation = trackingId > 0 ? $"Approaching Strangers #{trackingId}" : "Approaching Strangers";
                    return $"{designation}\nStatus: Camped for the night\nPace: Stationary (Resuming march at dawn, ~{restHoursLeft:F1}h)\nObservations: Campfires spotted on horizon";
                }
                string highTechDesignation = trackingId > 0 ? $"Contact-{trackingId}" : "Unidentified Contact";
                return $"{highTechDesignation}\nStatus: Stationary (Camped at night)\nPace: Zero movement telemetry (~{restHoursLeft:F1}h until dawn)";
            }

            return $"{label.CapitalizeFirst()} ({factionName})\nCamped for the night (Resuming march at dawn, ~{restHoursLeft:F1}h)\nETA: ~{remainingHours:F1} hours ({Mathf.CeilToInt(actualRemainingTiles)} hexes remaining)";
        }

        if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient && intelligenceTier == IntelligenceTier.Contact)
        {
            if (SensoryNarrativeUtility.IsLowTech)
            {
                string designation = trackingId > 0 ? $"Approaching Strangers #{trackingId}" : "Approaching Strangers";
                return $"{designation}\nStatus: Spotted in the distance\nPace: ~{speedTilesPerDay:F0} hexes/day\nObservations: Identity and intent unknown from this distance";
            }
            string highTechDesignation = trackingId > 0 ? $"Contact-{trackingId}" : "Unidentified Contact";
            return $"{highTechDesignation}\nStatus: Tracking in progress\nSpeed: ~{speedTilesPerDay:F0} hexes/day\nTelemetry: Insufficient sensor data to classify faction";
        }

        Map? targetMap = Current.Game.FindMap(destinationTile);
        if (targetMap == null || !Find.Maps.Contains(targetMap))
        {
            return $"{label.CapitalizeFirst()} ({factionName})\nTarget settlement is abandoned\nETA: ~{remainingHours:F1} hours ({Mathf.CeilToInt(actualRemainingTiles)} hexes remaining)";
        }

        return $"{label.CapitalizeFirst()} ({factionName})\nETA: ~{remainingHours:F1} hours ({Mathf.CeilToInt(actualRemainingTiles)} hexes remaining)";
    }

    public override string GetDescription()
    {
        if (GeoscapeMod.Settings.visibilityMode != VisibilityMode.Omniscient && intelligenceTier == IntelligenceTier.Contact)
        {
            return SensoryNarrativeUtility.GetContactDescription();
        }

        switch (GroupKind)
        {
            case IncursionGroupKind.HostileWarband:
                return $"A hostile warband from {Faction?.Name ?? "an unknown enemy faction"} traversing the planetary surface towards colony territory.";
            case IncursionGroupKind.TradeCaravan:
                string traderDetail = incidentParms?.traderKind != null ? $" ({incidentParms.traderKind.label})" : string.Empty;
                return $"A merchant trade caravan{traderDetail} from {Faction?.Name ?? "a friendly faction"} travelling with trade goods and pack animals.";
            case IncursionGroupKind.Visitors:
                return $"A party of visitors from {Faction?.Name ?? "a friendly faction"} travelling to socialise and visit your settlement.";
            case IncursionGroupKind.Travelers:
                return $"A group of travellers from {Faction?.Name ?? "a neutral faction"} passing peacefully through the region.";
            case IncursionGroupKind.TributeCollector:
                return $"An imperial royal tribute collector from {Faction?.Name ?? "the Empire"} seeking tithes of gold or prisoners.";
            case IncursionGroupKind.Pilgrims:
                return $"A group of pilgrims from {Faction?.Name ?? "a friendly faction"} travelling on a spiritual journey.";
            case IncursionGroupKind.Beggars:
                return "A group of impoverished travellers in desperate need of charity and aid.";
            case IncursionGroupKind.AlliedReinforcements:
                return $"An allied military force from {Faction?.Name ?? "an allied faction"} marching to reinforce your colony.";
            default:
                return $"A travelling party from {Faction?.Name ?? "an unknown faction"} traversing the planetary surface.";
        }
    }
}
