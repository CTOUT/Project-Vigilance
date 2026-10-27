using System;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Geoscape.World;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Two-stage early warning dispatcher enforcing non-peeking alert escalation.
/// Stage 1 (Contact Tier): Dispatches LetterDefOf.ThreatSmall ("Early Warning: Unidentified Contact [Contact-X]")
/// when crossing R_detect.
/// Stage 2 (Classified Tier): Gated by R_id and terrain masking (Model B+).
/// Hostiles escalate to LetterDefOf.ThreatBig ("Hostile Incursion: [Faction Name]"),
/// and friendly trade caravans dispatch LetterDefOf.PositiveEvent ("Approaching Caravan: [Faction Name]").
/// </summary>
public static class EarlyWarningDispatcher
{
    public static event Action<WorldObject_Incursion, LetterDef, TaggedString, TaggedString>? OnLetterDispatched;

    public static void ProcessDetection(WorldObject_Incursion incursion, ISensorProvider sensor, IntelligenceTier newTier)
    {
        if (incursion == null) return;

        incursion.CurrentIntelligenceTier = newTier;

        if (newTier >= IntelligenceTier.Contact && incursion.TrackingId <= 0)
        {
            incursion.TrackingId = ContactRegistry.Instance?.AllocateTrackingId() ??
                                   SensoryManager.Instance?.AllocateContactTrackingId() ?? 1;
            ContactRegistry.Instance?.RegisterContact(incursion);
        }

        if (newTier >= IntelligenceTier.Classified)
        {
            incursion.IsIdentified = true;
        }
        else
        {
            incursion.IsIdentified = (GeoscapeMod.Settings.visibilityMode == VisibilityMode.Omniscient);
        }

        incursion.UpdateSampledPosition();

        if (!GeoscapeMod.Settings.enableEarlyWarningLetters)
        {
            return;
        }

        // Stage 2 (Identification Threshold): Classified or Analysed
        if (newTier >= IntelligenceTier.Classified)
        {
            if (!incursion.HasDispatchedClassifiedAlert)
            {
                // If immediately detected at Classified+ (e.g. adjacent spawn or high-spec array),
                // mark both stages as dispatched so vague Stage 1 does not fire later
                incursion.HasDispatchedContactAlert = true;
                incursion.HasDispatchedClassifiedAlert = true;
                DispatchClassifiedLetter(incursion, sensor);
            }
        }
        // Stage 1 (Outer Boundary Alert): Contact Tier
        else if (newTier >= IntelligenceTier.Contact && !incursion.HasDispatchedContactAlert)
        {
            incursion.HasDispatchedContactAlert = true;
            DispatchContactLetter(incursion, sensor);
        }
    }

    public static void DispatchContactLetter(WorldObject_Incursion incursion, ISensorProvider sensor)
    {
        Map? targetMap = (Current.Game != null && incursion.DestinationTile.Valid) ? Current.Game.FindMap(incursion.DestinationTile) : null;
        string targetName = targetMap?.Parent?.LabelCap ??
                            Find.World?.worldObjects?.SettlementAt(incursion.DestinationTile)?.LabelCap ??
                            Find.World?.worldObjects?.MapParentAt(incursion.DestinationTile)?.LabelCap ??
                            (incursion.DestinationTile.Valid ? $"territory at tile {incursion.DestinationTile.tileId}" : "colony territory");

        string title = SensoryNarrativeUtility.GetContactLetterTitle(sensor, incursion.TrackingId);
        string text = SensoryNarrativeUtility.GetContactLetterBody(sensor, targetName, incursion.SpeedTilesPerDay, incursion.TrackingId);

        SendIncursionLetter(incursion, title, text, LetterDefOf.ThreatSmall, targetMap, targetName);
    }

    public static void DispatchClassifiedLetter(WorldObject_Incursion incursion, ISensorProvider sensor)
    {
        Map? targetMap = (Current.Game != null && incursion.DestinationTile.Valid) ? Current.Game.FindMap(incursion.DestinationTile) : null;
        string targetName = targetMap?.Parent?.LabelCap ??
                            Find.World?.worldObjects?.SettlementAt(incursion.DestinationTile)?.LabelCap ??
                            Find.World?.worldObjects?.MapParentAt(incursion.DestinationTile)?.LabelCap ??
                            (incursion.DestinationTile.Valid ? $"territory at tile {incursion.DestinationTile.tileId}" : "colony territory");

        float actualRemainingTiles = Math.Max(0f, (incursion.PathTiles.Count - incursion.CurrentPathIndex) - incursion.TileProgress);
        float remainingHours = (actualRemainingTiles / Math.Max(0.1f, incursion.SpeedTilesPerDay)) * 24f;

        string factionStr = incursion.Faction?.Name ?? "Unknown Faction";
        string attrLine = SensoryNarrativeUtility.GetAttributionLine(sensor);
        string contactName = incursion.TrackingId > 0
            ? (SensoryNarrativeUtility.IsLowTech ? $"strangers #{incursion.TrackingId}" : $"Contact-{incursion.TrackingId}")
            : "the approaching contact";

        bool isHostile = incursion.Faction?.HostileTo(Faction.OfPlayer) ?? true;

        switch (incursion.GroupKind)
        {
            case IncursionGroupKind.HostileWarband:
            {
                string title = SensoryNarrativeUtility.IsLowTech ? $"Hostile Warband: {factionStr}" : $"Hostile Incursion: {factionStr}";
                string closingAdvice = SensoryNarrativeUtility.GetHostileClosingAdvice(sensor);
                string text = $"{sensor.SensorLabel} have confirmed that {contactName} is a hostile warband from {factionStr} marching towards {targetName}.\n\n" +
                              $"Target: {targetName}\n" +
                              $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                              $"{attrLine}\n\n" +
                              $"{closingAdvice}";

                SendIncursionLetter(incursion, title, text, LetterDefOf.ThreatBig, targetMap, targetName);
                break;
            }

            case IncursionGroupKind.TradeCaravan:
            {
                string traderDetail = incursion.IncidentParms?.traderKind != null ? $" ({incursion.IncidentParms.traderKind.label})" : string.Empty;
                string title = SensoryNarrativeUtility.IsLowTech ? $"Trading Caravan: {factionStr}" : $"Approaching Caravan: {factionStr}";
                string text = $"{sensor.SensorLabel} have confirmed that {contactName} is a friendly trade caravan{traderDetail} from {factionStr} heading towards {targetName}.\n\n" +
                              $"Target: {targetName}\n" +
                              $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                              $"{attrLine}\n\n" +
                              "They are bringing goods and silver to trade. Prepare your negotiators, silver stores, and trade stockpiles for their arrival.";

                SendIncursionLetter(incursion, title, text, LetterDefOf.PositiveEvent, targetMap, targetName);
                break;
            }

            case IncursionGroupKind.Visitors:
            {
                string title = $"Approaching Visitors: {factionStr}";
                string text = $"{sensor.SensorLabel} have confirmed that {contactName} is a party of visitors from {factionStr} travelling towards {targetName}.\n\n" +
                              $"Target: {targetName}\n" +
                              $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                              $"{attrLine}\n\n" +
                              "They are visiting to socialise and relax. They are not a merchant caravan, though individual visitors may carry personal items to barter. Ensure your colony has a hospitable social space and spare guest accommodations.";

                SendIncursionLetter(incursion, title, text, LetterDefOf.NeutralEvent, targetMap, targetName);
                break;
            }

            case IncursionGroupKind.Travelers:
            {
                string title = $"Travellers Passing Near: {factionStr}";
                string text = $"{sensor.SensorLabel} have confirmed that {contactName} is a group of travellers from {factionStr} moving through the region near {targetName}.\n\n" +
                              $"Target: {targetName}\n" +
                              $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                              $"{attrLine}\n\n" +
                              "They are simply passing through your territory and do not intend to visit, stay, or trade.";

                SendIncursionLetter(incursion, title, text, LetterDefOf.NeutralEvent, targetMap, targetName);
                break;
            }

            case IncursionGroupKind.TributeCollector:
            {
                string title = $"Royal Tribute Collector: {factionStr}";
                string text = $"{sensor.SensorLabel} have confirmed that {contactName} is a royal tribute collector from {factionStr} heading towards {targetName}.\n\n" +
                              $"Target: {targetName}\n" +
                              $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                              $"{attrLine}\n\n" +
                              "They seek offerings of gold or enslaved prisoners in exchange for royal favour and honour.";

                SendIncursionLetter(incursion, title, text, LetterDefOf.PositiveEvent, targetMap, targetName);
                break;
            }

            case IncursionGroupKind.Pilgrims:
            {
                string title = $"Approaching Pilgrims: {factionStr}";
                string text = $"{sensor.SensorLabel} have confirmed that {contactName} is a group of pilgrims from {factionStr} travelling towards {targetName}.\n\n" +
                              $"Target: {targetName}\n" +
                              $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                              $"{attrLine}\n\n" +
                              "They are travelling to venerate sacred relics or holy sites at your settlement.";

                SendIncursionLetter(incursion, title, text, LetterDefOf.NeutralEvent, targetMap, targetName);
                break;
            }

            case IncursionGroupKind.Beggars:
            {
                string title = $"Approaching Beggars: {factionStr}";
                string beggarsDetail = SensoryNarrativeUtility.GetBeggarsIntentDescription();
                string text = $"{sensor.SensorLabel} have confirmed that {contactName} is an impoverished group travelling towards {targetName}.\n\n" +
                              $"Target: {targetName}\n" +
                              $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                              $"{attrLine}\n\n" +
                              $"{beggarsDetail}";

                SendIncursionLetter(incursion, title, text, LetterDefOf.NeutralEvent, targetMap, targetName);
                break;
            }

            case IncursionGroupKind.AlliedReinforcements:
            {
                string title = $"Allied Reinforcements: {factionStr}";
                string text = $"{sensor.SensorLabel} have confirmed that {contactName} is an allied military contingent from {factionStr} marching towards {targetName} to reinforce your defences.\n\n" +
                              $"Target: {targetName}\n" +
                              $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                              $"{attrLine}\n\n" +
                              "They are armed and ready to assist against planetary threats.";

                SendIncursionLetter(incursion, title, text, LetterDefOf.PositiveEvent, targetMap, targetName);
                break;
            }

            default:
            {
                LetterDef fallbackDef = isHostile ? LetterDefOf.ThreatBig : LetterDefOf.NeutralEvent;
                string title = isHostile
                    ? (SensoryNarrativeUtility.IsLowTech ? $"Hostile Warband: {factionStr}" : $"Hostile Incursion: {factionStr}")
                    : $"Approaching Force: {factionStr}";
                string text = $"{sensor.SensorLabel} have confirmed that {contactName} from {factionStr} is moving towards {targetName}.\n\n" +
                              $"Target: {targetName}\n" +
                              $"Estimated Arrival: ~{remainingHours:F1} hours (~{Mathf.CeilToInt(actualRemainingTiles)} hexes away)\n" +
                              $"{attrLine}";

                SendIncursionLetter(incursion, title, text, fallbackDef, targetMap, targetName);
                break;
            }
        }
    }

    public static void SendIncursionLetter(
        WorldObject_Incursion incursion,
        TaggedString title,
        TaggedString text,
        LetterDef def,
        Map? destinationMap,
        string targetName)
    {
        OnLetterDispatched?.Invoke(incursion, def, title, text);

        LetterDef choiceDef;
        if (def == LetterDefOf.ThreatSmall)
        {
            choiceDef = PV_LetterDefOf.PV_TacticalEncounter_ThreatSmall ?? LetterDefOf.ThreatSmall;
        }
        else if (def == LetterDefOf.ThreatBig)
        {
            choiceDef = PV_LetterDefOf.PV_TacticalEncounter_Threat ?? LetterDefOf.ThreatBig;
        }
        else if (def == LetterDefOf.PositiveEvent)
        {
            choiceDef = PV_LetterDefOf.PV_TacticalEncounter_Positive ?? LetterDefOf.PositiveEvent;
        }
        else
        {
            choiceDef = PV_LetterDefOf.PV_TacticalEncounter_Neutral ?? LetterDefOf.NeutralEvent;
        }

        if (Current.ProgramState != ProgramState.Playing || Find.LetterStack == null)
        {
            return;
        }

        var letter = (ChoiceLetter_TacticalEncounter)LetterMaker.MakeLetter(choiceDef);
        letter.label = title;
        letter.text = text;
        letter.targetWorldObject = incursion;
        letter.targetTile = incursion.Tile;
        letter.worldButtonLabel = "Go to Contact (Geoscape)";
        letter.lookTargets = new GlobalTargetInfo(incursion);

        if (destinationMap != null && destinationMap.Index >= 0)
        {
            letter.battleMap = destinationMap;
            letter.battleButtonLabel = $"Go inside {targetName} (Defenses)";
        }

        Find.LetterStack.ReceiveLetter(letter);
    }
}
