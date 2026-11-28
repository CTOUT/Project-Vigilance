using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Core.Localization;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.World;

public partial class WorldObject_Incursion
{
    private static Texture2D? cachedCaravanExpandingIcon;
    private static Texture2D CaravanExpandingIcon =>
        cachedCaravanExpandingIcon ??= ContentFinder<Texture2D>.Get("World/WorldObjects/Expanding/Caravan");

    private static Texture2D? cachedCaravanWorldTexture;
    private static Texture2D CaravanWorldTexture =>
        cachedCaravanWorldTexture ??= ContentFinder<Texture2D>.Get("World/WorldObjects/Caravan");

    public override bool SelectableNow =>
        GeoscapeRules.VisibilityMode == VisibilityMode.Omniscient || intelligenceTier >= IntelligenceTier.Contact;

    public override bool NeverMultiSelect =>
        GeoscapeRules.VisibilityMode != VisibilityMode.Omniscient && intelligenceTier < IntelligenceTier.Contact;

    public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan)
    {
        foreach (var opt in base.GetFloatMenuOptions(caravan))
        {
            yield return opt;
        }

        if (caravan == null || !caravan.Spawned || caravan.Faction != Faction.OfPlayer)
        {
            yield break;
        }

        if (GeoscapeRules.VisibilityMode != VisibilityMode.Omniscient && intelligenceTier < IntelligenceTier.Contact)
        {
            yield break;
        }

        bool isClassified = GeoscapeRules.VisibilityMode == VisibilityMode.Omniscient ||
                            (isIdentified && intelligenceTier >= IntelligenceTier.Classified);

        if (!isClassified)
        {
            // Unidentified contact: do not leak whether they are hostile or trade!
            string label = $"Investigate {LabelCap}";
            yield return new FloatMenuOption(label, () =>
            {
                ResetCaravanCooldown(caravan.ID);
                PlanetTile targetTile = GetInterceptTargetTile(caravan);
                caravan.pather.StartPath(targetTile, new CaravanArrivalAction_InterceptIncursion(this), true);
            });
            yield break;
        }

        bool isHostile = Faction?.HostileTo(Faction.OfPlayer) ?? true;
        if (isHostile)
        {
            string interceptLabel = VigilanceLoc.Tr("PV_Menu_InterceptIncursion", $"Intercept {LabelCap}", LabelCap);
            yield return new FloatMenuOption(interceptLabel, () =>
            {
                ResetCaravanCooldown(caravan.ID);
                PlanetTile targetTile = GetInterceptTargetTile(caravan);
                caravan.pather.StartPath(targetTile, new CaravanArrivalAction_InterceptIncursion(this), true);
            });
        }
        else
        {
            if (GroupKind == IncursionGroupKind.TradeCaravan)
            {
                string tradeLabel = VigilanceLoc.Tr("PV_Menu_HailAndTrade", $"Hail & Trade with {LabelCap}", LabelCap);
                yield return new FloatMenuOption(tradeLabel, () =>
                {
                    ResetCaravanCooldown(caravan.ID);
                    PlanetTile targetTile = GetInterceptTargetTile(caravan);
                    caravan.pather.StartPath(targetTile, new CaravanArrivalAction_InterceptIncursion(this), true);
                });
            }

            if (hasBeenWarnedAway || isTurningBack)
            {
                yield return new FloatMenuOption($"Warn away {LabelCap} (Already retreating)", null);
            }
            else if (!HasImminentThreatNearby())
            {
                yield return new FloatMenuOption($"Warn away {LabelCap} (No imminent threat nearby)", null);
            }
            else
            {
                string warnLabel = VigilanceLoc.Tr("PV_Menu_WarnAway", $"Warn away / Turn back {LabelCap}", LabelCap);
                yield return new FloatMenuOption(warnLabel, () =>
                {
                    IncursionInterceptionUtility.WarnAway(this, caravan);
                });
            }
        }
    }

    public bool HasImminentThreatNearby()
    {
        PlanetTile dest = destinationTile.Valid ? destinationTile : Tile;

        // 1. Check if the destination map has active hostile threats
        Map? destMap = Current.Game?.FindMap(dest);
        if (destMap != null && GenHostility.AnyHostileActiveThreatToPlayer(destMap, countDormantPawnsAsHostile: false, canBeFogged: true))
        {
            return true;
        }

        // 2. Check if any hostile incursion is within 4 tiles of our current tile or our destination tile
        List<WorldObject>? allWorldObjects = Find.WorldObjects?.AllWorldObjects;
        if (allWorldObjects != null)
        {
            for (int i = 0; i < allWorldObjects.Count; i++)
            {
                if (allWorldObjects[i] is WorldObject_Incursion other && other != this && other.Spawned)
                {
                    bool otherHostile = other.Faction?.HostileTo(Faction) ?? other.Faction?.HostileTo(Faction.OfPlayer) ?? true;
                    if (otherHostile)
                    {
                        float distCurrent = Find.WorldGrid.ApproxDistanceInTiles(Tile, other.Tile);
                        float distDest = Find.WorldGrid.ApproxDistanceInTiles(dest, other.Tile);
                        if (distCurrent <= 4f || distDest <= 4f)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    public override string LabelShort => Label;

    public override string Label
    {
        get
        {
            bool isClassified = GeoscapeRules.VisibilityMode == VisibilityMode.Omniscient ||
                                (isIdentified && intelligenceTier >= IntelligenceTier.Classified);

            if (!isClassified)
            {
                return SensoryNarrativeUtility.GetContactLabel(trackingId: trackingId, domainFlags: GetActiveDetectingDomains());
            }

            if (incidentDef != null || Faction != null)
            {
                string idTag = trackingId > 0
                    ? (SensoryNarrativeUtility.IsLowTech ? $" [#{trackingId}]" : $" [Contact-{trackingId}]")
                    : string.Empty;

                if (isRetreating)
                {
                    string retreatTag = (kidnappedColonists != null && kidnappedColonists.Count > 0)
                        ? " [Kidnappers]"
                        : " [Retreating]";
                    return $"{GetIdentifiedGroupLabel()}{idTag}{retreatTag}";
                }

                if (pursuitTargetCaravan != null && pursuitTargetCaravan.Spawned)
                {
                    return $"{GetIdentifiedGroupLabel()}{idTag} [Pursuit]";
                }

                return $"{GetIdentifiedGroupLabel()}{idTag}";
            }

            return base.Label;
        }
    }

    /// <summary>
    /// True if this incursion was spawned as a merchant trade caravan (even if its faction subsequently turned hostile).
    /// </summary>
    public bool IsUnderlyingTradeCaravan
    {
        get
        {
            if (incidentParms?.traderKind != null ||
                (incidentParms?.pawnGroupKind != null && incidentParms.pawnGroupKind == PawnGroupKindDefOf.Trader))
            {
                return true;
            }

            string defName = incidentDef?.defName ?? string.Empty;
            string defLabel = incidentDef?.label ?? string.Empty;

            return defName.Equals("TraderCaravanArrival", StringComparison.OrdinalIgnoreCase) ||
                   defName.IndexOf("Trader", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   defLabel.IndexOf("trader", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   defLabel.IndexOf("caravan", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   defLabel.IndexOf("merchant", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    /// <summary>
    /// True if this incursion was spawned as a visitor, traveller, pilgrim, or beggar group.
    /// </summary>
    public bool IsUnderlyingVisitorOrTraveler
    {
        get
        {
            if (IsUnderlyingTradeCaravan)
            {
                return false;
            }

            string defName = incidentDef?.defName ?? string.Empty;
            string defLabel = incidentDef?.label ?? string.Empty;

            return defName.Equals("VisitorGroup", StringComparison.OrdinalIgnoreCase) ||
                   defName.IndexOf("Visitor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   defLabel.IndexOf("visitor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   defLabel.IndexOf("guest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   defName.Equals("TravelerGroup", StringComparison.OrdinalIgnoreCase) ||
                   defName.IndexOf("Traveler", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   defLabel.IndexOf("traveler", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   defLabel.IndexOf("traveller", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   defLabel.IndexOf("passer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   defName.IndexOf("Pilgrim", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   defName.IndexOf("Beggar", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    private IncursionGroupKind? explicitGroupKind;
    public void SetExplicitGroupKind(IncursionGroupKind kind) => explicitGroupKind = kind;

    /// <summary>
    /// Resolves the specific tactical kind of this incursion based on incident parameters and defs.
    /// Distinguishes between trade caravans, social visitors, passing travellers, tribute collectors, and hostile warbands.
    /// </summary>
    public IncursionGroupKind GroupKind
    {
        get
        {
            if (explicitGroupKind.HasValue)
            {
                return explicitGroupKind.Value;
            }

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
                if (isTradeMissionAborted)
                {
                    return "returning caravan (aborted mission)";
                }
                return explicitTraderKind != null
                    ? $"trade caravan ({explicitTraderKind.label})"
                    : incidentParms?.traderKind != null
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
            if (GeoscapeRules.VisibilityMode != VisibilityMode.Omniscient && intelligenceTier < IntelligenceTier.Contact)
            {
                return BaseContent.ClearTex;
            }
            return CaravanExpandingIcon;
        }
    }

    private static MaterialPropertyBlock? sharedDrawPropertyBlock;

    /// <summary>
    /// Calculates a calm, rhythmic tactical amber pulse (breathing beacon) for unidentified contacts.
    /// Uses real-world time with a smooth sinusoidal curve (~2.5s cycle) easing between 0.18 and 1.0 alpha.
    /// Returns a steady luminance value when reduced motion is enabled.
    /// </summary>
    public static Color TacticalAmberPulseColor(float alphaMultiplier = 1f)
    {
        if (GeoscapeMod.Settings?.reduceShroudMotion == true)
        {
            return new Color(1f, 0.78f, 0.15f, 0.92f * alphaMultiplier);
        }

        // Smooth breathing cycle (~2.5s) easing between 0.18 (deep translucency) and 1.0 (vivid beacon)
        float sinVal = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 2.5f);
        float pulse = 0.18f + 0.82f * sinVal;
        return new Color(1f, 0.78f, 0.15f, pulse * alphaMultiplier);
    }

    public override Color ExpandingIconColor
    {
        get
        {
            if (GeoscapeRules.VisibilityMode != VisibilityMode.Omniscient && intelligenceTier < IntelligenceTier.Classified)
            {
                return TacticalAmberPulseColor(0.95f);
            }

            if (Faction != null)
            {
                return Faction.Color;
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
            if (GeoscapeRules.VisibilityMode != VisibilityMode.Omniscient && intelligenceTier < IntelligenceTier.Classified)
            {
                return MaterialPool.MatFrom(
                    CaravanWorldTexture,
                    ShaderDatabase.WorldOverlayTransparentLit,
                    new Color(1f, 0.78f, 0.15f, 0.95f),
                    WorldMaterials.DynamicObjectRenderQueue);
            }

            Color incursionColor = Faction != null ? Faction.Color : ((Faction?.HostileTo(Faction.OfPlayer) ?? true)
                ? new Color(1f, 0.25f, 0.25f, 0.95f)
                : new Color(0.25f, 0.85f, 1f, 0.95f));

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
        if (GeoscapeRules.VisibilityMode != VisibilityMode.Omniscient && intelligenceTier < IntelligenceTier.Contact)
        {
            return;
        }

        float averageTileSize = Tile.Layer.AverageTileSize;
        float rawTransitionPct = ExpandableWorldObjectsUtility.RawTransitionPct;
        float altOffset = Rand.RangeSeeded(0f, 0.01f, ID) + def.drawAltitudeOffset;

        bool isHostile = Faction?.HostileTo(Faction.OfPlayer) ?? true;
        Color statusColor;

        if (GeoscapeRules.VisibilityMode != VisibilityMode.Omniscient && intelligenceTier < IntelligenceTier.Classified)
        {
            // Calm tactical amber breathing blip for unidentified contacts
            statusColor = TacticalAmberPulseColor(1f);
        }
        else
        {
            statusColor = Faction != null ? Faction.Color : (isHostile
                ? new Color(1f, 0.25f, 0.25f, 0.95f)
                : new Color(0.25f, 0.85f, 1f, 0.95f));
        }

        // Alpha fade matching vanilla expandable objects transition
        float alpha = 1f;
        if (def.expandingIcon && rawTransitionPct > 0f && !ExpandableWorldObjectsUtility.HiddenByRules(this))
        {
            alpha = Mathf.Clamp01(1f - rawTransitionPct);
        }

        // 1. Base Layer: Caravan / marching force silhouette (fixed white base material + MaterialPropertyBlock tint to avoid MaterialPool leaks)
        Material baseMat = MaterialPool.MatFrom(
            CaravanWorldTexture,
            ShaderDatabase.WorldOverlayTransparentLit,
            Color.white,
            WorldMaterials.DynamicObjectRenderQueue);

        sharedDrawPropertyBlock ??= new MaterialPropertyBlock();
        sharedDrawPropertyBlock.SetColor(ShaderPropertyIDs.Color, new Color(statusColor.r, statusColor.g, statusColor.b, statusColor.a * alpha));

        WorldRendererUtility.DrawQuadTangentialToPlanet(
            DrawPos,
            0.85f * averageTileSize,
            DrawAltitude + altOffset,
            baseMat,
            WorldObject_ListeningPost.UprightQuadRotationAngle,
            false,
            false,
            sharedDrawPropertyBlock);

        // 2. Overlay Layer: Faction Emblem (ONLY when Classified or Analysed)
        bool canShowEmblem = GeoscapeRules.VisibilityMode == VisibilityMode.Omniscient || intelligenceTier >= IntelligenceTier.Classified;
        if (canShowEmblem && isIdentified && Faction?.def?.FactionIcon != null && GeoscapeMod.Settings.showFactionEmblems)
        {
            Color factionTint = Faction.Color;
            Material emblemMat = MaterialPool.MatFrom(
                Faction.def.FactionIcon,
                ShaderDatabase.WorldOverlayTransparentLit,
                Color.white,
                WorldMaterials.DynamicObjectRenderQueue);

            sharedDrawPropertyBlock.SetColor(ShaderPropertyIDs.Color, new Color(factionTint.r, factionTint.g, factionTint.b, alpha));

            WorldRendererUtility.DrawQuadTangentialToPlanet(
                DrawPos,
                0.52f * averageTileSize,
                DrawAltitude + altOffset + 0.005f,
                emblemMat,
                WorldObject_ListeningPost.UprightQuadRotationAngle,
                false,
                false,
                sharedDrawPropertyBlock);
        }
    }

}
