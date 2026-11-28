namespace ProjectVigilance.Geoscape.Rules;

/// <summary>
/// Controls whether campaign creation automatically picks a viable landing site on uncharted globes
/// or presents the manual globe selection screen.
/// </summary>
public enum StartingSiteSelectionMode
{
    /// <summary>Automatically selects a valid, habitable land plot and advances directly to Ideology/Pawns.</summary>
    AutomatedLandfall,

    /// <summary>Presents the planetary globe for manual sector/hemisphere targeting.</summary>
    ManualTargeting
}

/// <summary>
/// Pre-configured campaign rule profiles for one-click setup.
/// </summary>
public enum CampaignProfile
{
    /// <summary>Full planetary Fog of War, night rest, multi-vector synchronisation, and tactical flanking.</summary>
    FogOfWar,

    /// <summary>Backwards-compatibility alias for FogOfWar (retained for Scribe savegame deserialisation).</summary>
    [System.Obsolete("Use CampaignProfile.FogOfWar instead. Retained solely for Scribe backwards compatibility.")]
    ClassicHaze = FogOfWar,

    /// <summary>Planetary geography known, dynamic incursions hidden outside radar envelopes.</summary>
    PlanetarySurveillance,

    /// <summary>Uncharted planet, reduced scout horizons, nocturnal vigilance penalties, and aggressive tactical flanking.</summary>
    HardcoreVigilance,

    /// <summary>Vanilla RimWorld global visibility. No fog of war.</summary>
    Omniscient,

    /// <summary>Manual customisation of individual campaign rules.</summary>
    Custom
}

public static class CampaignProfileExtensions
{
    public static string GetDisplayName(this CampaignProfile profile) => profile switch
    {
        CampaignProfile.FogOfWar              => ProjectVigilance.Core.Localization.VigilanceLoc.Tr("PV_Profile_FogOfWar_Title", "Fog of War (Recommended)"),
        CampaignProfile.PlanetarySurveillance => ProjectVigilance.Core.Localization.VigilanceLoc.Tr("PV_Profile_PlanetarySurveillance_Title", "Planetary Surveillance"),
        CampaignProfile.HardcoreVigilance     => ProjectVigilance.Core.Localization.VigilanceLoc.Tr("PV_Profile_HardcoreVigilance_Title", "Hardcore Vigilance"),
        CampaignProfile.Omniscient            => ProjectVigilance.Core.Localization.VigilanceLoc.Tr("PV_Profile_Omniscient_Title", "Omniscient (Vanilla)"),
        CampaignProfile.Custom                => ProjectVigilance.Core.Localization.VigilanceLoc.Tr("PV_Profile_Custom_Title", "Custom"),
        _                                     => profile.ToString()
    };

    public static string GetCleanName(this CampaignProfile profile) => profile switch
    {
        CampaignProfile.FogOfWar              => "Fog of War",
        CampaignProfile.PlanetarySurveillance => "Planetary Surveillance",
        CampaignProfile.HardcoreVigilance     => "Hardcore Vigilance",
        CampaignProfile.Omniscient            => "Omniscient",
        CampaignProfile.Custom                => "Custom",
        _                                     => profile.ToString()
    };

    public static string GetTooltipDescription(this CampaignProfile profile) => profile switch
    {
        CampaignProfile.FogOfWar              => ProjectVigilance.Core.Localization.VigilanceLoc.Tr("PV_Profile_FogOfWar_Desc", "Standard Project Vigilance experience. Unexplored hexes shrouded in darkness; dynamic charting through caravans, outposts, and radar networks."),
        CampaignProfile.PlanetarySurveillance => ProjectVigilance.Core.Localization.VigilanceLoc.Tr("PV_Profile_PlanetarySurveillance_Desc", "World geography and biomes known from orbital scans. Dynamic incursions and outposts concealed outside sensor range."),
        CampaignProfile.HardcoreVigilance     => ProjectVigilance.Core.Localization.VigilanceLoc.Tr("PV_Profile_HardcoreVigilance_Desc", "Strict sensory limits: planetary shroud, tightened lookout horizons, terrain masking, and nocturnal vigilance penalties."),
        CampaignProfile.Omniscient            => ProjectVigilance.Core.Localization.VigilanceLoc.Tr("PV_Profile_Omniscient_Desc", "Vanilla RimWorld global visibility: all settlements, terrain, and landmarks revealed from day one."),
        CampaignProfile.Custom                => ProjectVigilance.Core.Localization.VigilanceLoc.Tr("PV_Profile_Custom_Desc", "Fine-tuned custom ruleset configured through individual simulation toggles."),
        _                                     => string.Empty
    };
}
