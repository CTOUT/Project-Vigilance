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

    /// <summary>Backwards-compatibility alias for FogOfWar.</summary>
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
        CampaignProfile.FogOfWar              => "Fog of War (Recommended)",
        CampaignProfile.PlanetarySurveillance => "Planetary Surveillance",
        CampaignProfile.HardcoreVigilance     => "Hardcore Vigilance",
        CampaignProfile.Omniscient            => "Omniscient (Vanilla)",
        CampaignProfile.Custom                => "Custom",
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
        CampaignProfile.FogOfWar              => "Standard Project Vigilance experience. Unexplored hexes shrouded in darkness; dynamic charting through caravans, outposts, and radar networks.",
        CampaignProfile.PlanetarySurveillance => "World geography and biomes known from orbital scans. Dynamic incursions and outposts concealed outside sensor range.",
        CampaignProfile.HardcoreVigilance     => "Strict sensory limits: planetary shroud, tightened lookout horizons, terrain masking, and nocturnal vigilance penalties.",
        CampaignProfile.Omniscient            => "Vanilla RimWorld global visibility: all settlements, terrain, and landmarks revealed from day one.",
        CampaignProfile.Custom                => "Fine-tuned custom ruleset configured through individual simulation toggles.",
        _                                     => string.Empty
    };
}
