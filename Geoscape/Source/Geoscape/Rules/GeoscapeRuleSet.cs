using System;
using System.Collections.Generic;
using System.Linq;
using ProjectVigilance.Geoscape.Sensory;
using Verse;

namespace ProjectVigilance.Geoscape.Rules;

/// <summary>
/// Container holding the active campaign rule set.
/// Can back new-campaign defaults, world-gen drafts, or active WorldComponent campaign rules.
/// </summary>
public sealed class GeoscapeRuleSet : IExposable
{
    public readonly CampaignRule<VisibilityMode> Visibility;
    public readonly CampaignRule<float> ColonyScoutRadius;
    public readonly CampaignRule<float> CaravanScoutRadius;
    public readonly CampaignRule<bool> NighttimeSensoryReduction;
    public readonly CampaignRule<bool> IncursionsRestAtNight;
    public readonly CampaignRule<bool> SynchronizeMultiVectorRaids;
    public readonly CampaignRule<bool> TacticalIngressFlanking;
    public readonly CampaignRule<bool> ResetThreatCooldownOnArrival;
    public readonly CampaignRule<bool> EarlyWarningLetters;
    public readonly CampaignRule<StartingSiteSelectionMode> StartingSiteSelection;

    private readonly List<ICampaignRule> allRules;
    public IReadOnlyList<ICampaignRule> All => allRules;

    public GeoscapeRuleSet()
    {
        Visibility = new CampaignRule<VisibilityMode>(
            "visibilityMode",
            "Planetary visibility",
            "Determines planetary fog of war and movement visibility. Fog of War shrouds the unexplored planet; Planetary Surveillance charts world geography; Omniscient reveals all global movement.",
            RuleMutability.Escalatable,
            hardDefault: VisibilityMode.FogOfWar,
            defaultSelector: d => d.visibilityMode,
            defaultWriter: (d, val) => d.visibilityMode = val,
            effects: RuleTransitionEffects.ReleaseConcealedSites |
                     RuleTransitionEffects.RescanSensors |
                     RuleTransitionEffects.RepathPlayerCaravans |
                     RuleTransitionEffects.RebuildWorldLayers,
            escalationRank: VisibilityRank,
            valueFormatter: FormatVisibilityMode);

        ColonyScoutRadius = new CampaignRule<float>(
            "colonyScoutRadius",
            "Colony scout horizon",
            "Natural optical observation range of established player colonies in world hexes.",
            RuleMutability.Immutable,
            hardDefault: 5.0f,
            defaultSelector: d => d.colonyScoutRadius,
            defaultWriter: (d, val) => d.colonyScoutRadius = val,
            effects: RuleTransitionEffects.RescanSensors | RuleTransitionEffects.RebuildWorldLayers,
            valueFormatter: FormatHexCount);

        CaravanScoutRadius = new CampaignRule<float>(
            "caravanScoutRadius",
            "Caravan scout horizon",
            "Natural optical observation range of moving player caravans in world hexes.",
            RuleMutability.Immutable,
            hardDefault: 3.0f,
            defaultSelector: d => d.caravanScoutRadius,
            defaultWriter: (d, val) => d.caravanScoutRadius = val,
            effects: RuleTransitionEffects.RescanSensors | RuleTransitionEffects.RebuildWorldLayers,
            valueFormatter: FormatHexCount);

        NighttimeSensoryReduction = new CampaignRule<bool>(
            "enableNighttimeSensoryReduction",
            "Night-time sensory reduction",
            "Biological lookouts suffer visual penalties at night. Sleeping colonies collapse observation to immediate perimeter unless Night Owl colonists are on watch.",
            RuleMutability.Immutable,
            hardDefault: true,
            defaultSelector: d => d.enableNighttimeSensoryReduction,
            defaultWriter: (d, val) => d.enableNighttimeSensoryReduction = val,
            effects: RuleTransitionEffects.RescanSensors,
            valueFormatter: val => val ? "Enabled" : "Disabled");

        IncursionsRestAtNight = new CampaignRule<bool>(
            "incursionsRestAtNight",
            "Incursions rest at night",
            "Biological warbands and caravans halt between 22:00 and 06:00 solar time. Mechanoids and anomaly entities march relentlessly 24/7.",
            RuleMutability.Immutable,
            hardDefault: true,
            defaultSelector: d => d.incursionsRestAtNight,
            defaultWriter: (d, val) => d.incursionsRestAtNight = val,
            effects: RuleTransitionEffects.None,
            valueFormatter: val => val ? "Enabled" : "Disabled");

        SynchronizeMultiVectorRaids = new CampaignRule<bool>(
            "synchronizeMultiVectorRaids",
            "Synchronize multi-vector raids",
            "When hostile factions launch multi-group raids from separate bases, closer settlements delay departure to converge simultaneously.",
            RuleMutability.Immutable,
            hardDefault: true,
            defaultSelector: d => d.synchronizeMultiVectorRaids,
            defaultWriter: (d, val) => d.synchronizeMultiVectorRaids = val,
            effects: RuleTransitionEffects.None,
            valueFormatter: val => val ? "Enabled" : "Disabled");

        TacticalIngressFlanking = new CampaignRule<bool>(
            "enableTacticalIngressFlanking",
            "Tactical ingress flanking",
            "Raiders and tribal warbands occasionally take off-road flanking routes to avoid predictable highway checkpoints.",
            RuleMutability.Immutable,
            hardDefault: true,
            defaultSelector: d => d.enableTacticalIngressFlanking,
            defaultWriter: (d, val) => d.enableTacticalIngressFlanking = val,
            effects: RuleTransitionEffects.None,
            valueFormatter: val => val ? "Enabled" : "Disabled");

        ResetThreatCooldownOnArrival = new CampaignRule<bool>(
            "resetThreatCooldownOnArrival",
            "Reset threat cooldown on arrival",
            "Storyteller threat cycle cooldown begins when the incursion arrives on the colony map rather than upon planetary departure.",
            RuleMutability.Immutable,
            hardDefault: false,
            defaultSelector: d => d.resetThreatCooldownOnArrival,
            defaultWriter: (d, val) => d.resetThreatCooldownOnArrival = val,
            effects: RuleTransitionEffects.None,
            valueFormatter: val => val ? "Enabled" : "Disabled");

        EarlyWarningLetters = new CampaignRule<bool>(
            "enableEarlyWarningLetters",
            "Early warning letters",
            "Dispatch notification letters when sensors or lookouts detect or classify an approaching incursion.",
            RuleMutability.Mutable,
            hardDefault: true,
            defaultSelector: d => d.enableEarlyWarningLetters,
            defaultWriter: (d, val) => d.enableEarlyWarningLetters = val,
            effects: RuleTransitionEffects.None,
            valueFormatter: val => val ? "Enabled" : "Disabled");

        StartingSiteSelection = new CampaignRule<StartingSiteSelectionMode>(
            "startingSiteSelection",
            "Starting site selection",
            "Controls whether campaign creation automatically picks a viable landing site on uncharted globes or presents manual targeting.",
            RuleMutability.Immutable,
            hardDefault: StartingSiteSelectionMode.AutomatedLandfall,
            defaultSelector: d => d.startingSiteSelection,
            defaultWriter: (d, val) => d.startingSiteSelection = val,
            effects: RuleTransitionEffects.None,
            valueFormatter: FormatStartingSiteSelection);

        allRules = new List<ICampaignRule>(10)
        {
            Visibility,
            ColonyScoutRadius,
            CaravanScoutRadius,
            NighttimeSensoryReduction,
            IncursionsRestAtNight,
            SynchronizeMultiVectorRaids,
            TacticalIngressFlanking,
            ResetThreatCooldownOnArrival,
            EarlyWarningLetters,
            StartingSiteSelection
        };
    }

    /// <summary>
    /// Information rank ordering: Classic Haze (0) &lt; Planetary Surveillance (1) &lt; Omniscient (2).
    /// Independent of enum integer values.
    /// </summary>
    public static int VisibilityRank(VisibilityMode m) => m switch
    {
        VisibilityMode.ClassicHazeOfWar      => 0,
        VisibilityMode.PlanetarySurveillance => 1,
        VisibilityMode.Omniscient            => 2,
        _ => 1
    };

    private static string FormatVisibilityMode(VisibilityMode m) => m switch
    {
        VisibilityMode.FogOfWar              => "Fog of War",
        VisibilityMode.PlanetarySurveillance => "Planetary Surveillance",
        VisibilityMode.Omniscient            => "Omniscient",
        _ => m.ToString()
    };

    private static string FormatStartingSiteSelection(StartingSiteSelectionMode m) => m switch
    {
        StartingSiteSelectionMode.AutomatedLandfall => "Automated Landfall",
        StartingSiteSelectionMode.ManualTargeting   => "Manual Targeting",
        _ => m.ToString()
    };

    private static string FormatHexCount(float val)
    {
        int hexes = UnityEngine.Mathf.RoundToInt(val);
        return hexes == 1 ? "1 hex" : $"{hexes} hexes";
    }

    public void CopyFromDefaults(GeoscapeSettings defaults)
    {
        if (defaults == null) return;
        for (int i = 0; i < allRules.Count; i++)
        {
            allRules[i].CopyFromDefaults(defaults);
        }
    }

    public void WriteToDefaults(GeoscapeSettings defaults)
    {
        if (defaults == null) return;
        for (int i = 0; i < allRules.Count; i++)
        {
            allRules[i].WriteToDefaults(defaults);
        }
    }

    public void CopyFrom(GeoscapeRuleSet other)
    {
        if (other == null) return;
        Visibility.Value = other.Visibility.Value;
        ColonyScoutRadius.Value = other.ColonyScoutRadius.Value;
        CaravanScoutRadius.Value = other.CaravanScoutRadius.Value;
        NighttimeSensoryReduction.Value = other.NighttimeSensoryReduction.Value;
        IncursionsRestAtNight.Value = other.IncursionsRestAtNight.Value;
        SynchronizeMultiVectorRaids.Value = other.SynchronizeMultiVectorRaids.Value;
        TacticalIngressFlanking.Value = other.TacticalIngressFlanking.Value;
        ResetThreatCooldownOnArrival.Value = other.ResetThreatCooldownOnArrival.Value;
        EarlyWarningLetters.Value = other.EarlyWarningLetters.Value;
        StartingSiteSelection.Value = other.StartingSiteSelection.Value;
    }

    public void ApplyProfile(CampaignProfile profile)
    {
        switch (profile)
        {
            case CampaignProfile.ClassicHaze:
                Visibility.Value = VisibilityMode.ClassicHazeOfWar;
                ColonyScoutRadius.Value = 5.0f;
                CaravanScoutRadius.Value = 3.0f;
                NighttimeSensoryReduction.Value = true;
                IncursionsRestAtNight.Value = true;
                SynchronizeMultiVectorRaids.Value = true;
                TacticalIngressFlanking.Value = true;
                ResetThreatCooldownOnArrival.Value = false;
                EarlyWarningLetters.Value = true;
                StartingSiteSelection.Value = StartingSiteSelectionMode.AutomatedLandfall;
                break;

            case CampaignProfile.PlanetarySurveillance:
                Visibility.Value = VisibilityMode.PlanetarySurveillance;
                ColonyScoutRadius.Value = 5.0f;
                CaravanScoutRadius.Value = 3.0f;
                NighttimeSensoryReduction.Value = true;
                IncursionsRestAtNight.Value = true;
                SynchronizeMultiVectorRaids.Value = true;
                TacticalIngressFlanking.Value = true;
                ResetThreatCooldownOnArrival.Value = false;
                EarlyWarningLetters.Value = true;
                StartingSiteSelection.Value = StartingSiteSelectionMode.ManualTargeting;
                break;

            case CampaignProfile.HardcoreVigilance:
                Visibility.Value = VisibilityMode.FogOfWar;
                ColonyScoutRadius.Value = 4.0f;
                CaravanScoutRadius.Value = 2.0f;
                NighttimeSensoryReduction.Value = true;
                IncursionsRestAtNight.Value = true;
                SynchronizeMultiVectorRaids.Value = true;
                TacticalIngressFlanking.Value = true;
                ResetThreatCooldownOnArrival.Value = false;
                EarlyWarningLetters.Value = true;
                StartingSiteSelection.Value = StartingSiteSelectionMode.AutomatedLandfall;
                break;

            case CampaignProfile.Omniscient:
                Visibility.Value = VisibilityMode.Omniscient;
                ColonyScoutRadius.Value = 5.0f;
                CaravanScoutRadius.Value = 3.0f;
                NighttimeSensoryReduction.Value = false;
                IncursionsRestAtNight.Value = true;
                SynchronizeMultiVectorRaids.Value = true;
                TacticalIngressFlanking.Value = true;
                ResetThreatCooldownOnArrival.Value = false;
                EarlyWarningLetters.Value = true;
                StartingSiteSelection.Value = StartingSiteSelectionMode.ManualTargeting;
                break;

            case CampaignProfile.Custom:
                // Retains current values
                break;
        }
    }

    public CampaignProfile DetectCurrentProfile()
    {
        if (Visibility.Value == VisibilityMode.Omniscient &&
            IncursionsRestAtNight.Value &&
            SynchronizeMultiVectorRaids.Value &&
            TacticalIngressFlanking.Value &&
            !ResetThreatCooldownOnArrival.Value &&
            StartingSiteSelection.Value == StartingSiteSelectionMode.ManualTargeting)
        {
            return CampaignProfile.Omniscient;
        }

        if (Visibility.Value == VisibilityMode.FogOfWar &&
            Math.Abs(ColonyScoutRadius.Value - 5.0f) < 0.05f &&
            Math.Abs(CaravanScoutRadius.Value - 3.0f) < 0.05f &&
            NighttimeSensoryReduction.Value &&
            IncursionsRestAtNight.Value &&
            SynchronizeMultiVectorRaids.Value &&
            TacticalIngressFlanking.Value &&
            !ResetThreatCooldownOnArrival.Value &&
            StartingSiteSelection.Value == StartingSiteSelectionMode.AutomatedLandfall)
        {
            return CampaignProfile.FogOfWar;
        }

        if (Visibility.Value == VisibilityMode.PlanetarySurveillance &&
            Math.Abs(ColonyScoutRadius.Value - 5.0f) < 0.05f &&
            Math.Abs(CaravanScoutRadius.Value - 3.0f) < 0.05f &&
            NighttimeSensoryReduction.Value &&
            IncursionsRestAtNight.Value &&
            SynchronizeMultiVectorRaids.Value &&
            TacticalIngressFlanking.Value &&
            !ResetThreatCooldownOnArrival.Value &&
            StartingSiteSelection.Value == StartingSiteSelectionMode.ManualTargeting)
        {
            return CampaignProfile.PlanetarySurveillance;
        }

        if (Visibility.Value == VisibilityMode.FogOfWar &&
            Math.Abs(ColonyScoutRadius.Value - 4.0f) < 0.05f &&
            Math.Abs(CaravanScoutRadius.Value - 2.0f) < 0.05f &&
            NighttimeSensoryReduction.Value &&
            IncursionsRestAtNight.Value &&
            SynchronizeMultiVectorRaids.Value &&
            TacticalIngressFlanking.Value &&
            !ResetThreatCooldownOnArrival.Value &&
            StartingSiteSelection.Value == StartingSiteSelectionMode.AutomatedLandfall)
        {
            return CampaignProfile.HardcoreVigilance;
        }

        return CampaignProfile.Custom;
    }

    public string CreateSnapshotString()
    {
        return string.Join("; ", allRules.Select(r => $"{r.Key}={r.CurrentValueLabel}"));
    }

    public void ExposeData()
    {
        for (int i = 0; i < allRules.Count; i++)
        {
            allRules[i].ExposeValue();
        }
    }
}
