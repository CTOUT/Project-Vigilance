using ProjectVigilance.Geoscape.Rules;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape;

/// <summary>
/// High-performance, zero-allocation static facade providing access to effective campaign rules.
/// Reads from the authoritative GeoscapeCampaignRules WorldComponent when a campaign is active,
/// safely falling back to GeoscapeMod.Settings defaults at the main menu or in headless unit tests.
/// </summary>
public static class GeoscapeRules
{
    public static VisibilityMode VisibilityMode =>
        GeoscapeCampaignRules.Active?.Rules.Visibility.Value ?? GeoscapeMod.Settings.visibilityMode;

    public static float ColonyScoutRadius =>
        GeoscapeCampaignRules.Active?.Rules.ColonyScoutRadius.Value ?? GeoscapeMod.Settings.colonyScoutRadius;

    public static float CaravanScoutRadius =>
        GeoscapeCampaignRules.Active?.Rules.CaravanScoutRadius.Value ?? GeoscapeMod.Settings.caravanScoutRadius;

    public static bool NighttimeSensoryReduction =>
        GeoscapeCampaignRules.Active?.Rules.NighttimeSensoryReduction.Value ?? GeoscapeMod.Settings.enableNighttimeSensoryReduction;

    public static bool IncursionsRestAtNight =>
        GeoscapeCampaignRules.Active?.Rules.IncursionsRestAtNight.Value ?? GeoscapeMod.Settings.incursionsRestAtNight;

    public static bool SynchronizeMultiVectorRaids =>
        GeoscapeCampaignRules.Active?.Rules.SynchronizeMultiVectorRaids.Value ?? GeoscapeMod.Settings.synchronizeMultiVectorRaids;

    public static bool TacticalIngressFlanking =>
        GeoscapeCampaignRules.Active?.Rules.TacticalIngressFlanking.Value ?? GeoscapeMod.Settings.enableTacticalIngressFlanking;

    public static bool ResetThreatCooldownOnArrival =>
        GeoscapeCampaignRules.Active?.Rules.ResetThreatCooldownOnArrival.Value ?? GeoscapeMod.Settings.resetThreatCooldownOnArrival;

    public static bool EarlyWarningLetters =>
        GeoscapeCampaignRules.Active?.Rules.EarlyWarningLetters.Value ?? GeoscapeMod.Settings.enableEarlyWarningLetters;
}
