namespace ProjectVigilance.Geoscape.Rules;

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
}
