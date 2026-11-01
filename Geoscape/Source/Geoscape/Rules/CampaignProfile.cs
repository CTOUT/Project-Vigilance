namespace ProjectVigilance.Geoscape.Rules;

/// <summary>
/// Pre-configured campaign rule profiles for one-click setup.
/// </summary>
public enum CampaignProfile
{
    /// <summary>Classic Haze of War, night rest, multi-vector synchronisation, and tactical flanking.</summary>
    ClassicHaze,

    /// <summary>Classic X-COM: Planetary geography known, dynamic incursions hidden outside radar envelopes.</summary>
    PlanetarySurveillance,

    /// <summary>Uncharted planet, reduced scout horizons, nocturnal vigilance penalties, and aggressive tactical flanking.</summary>
    HardcoreVigilance,

    /// <summary>Manual customisation of individual campaign rules.</summary>
    Custom
}

public static class CampaignProfileExtensions
{
    public static string GetDisplayName(this CampaignProfile profile) => profile switch
    {
        CampaignProfile.ClassicHaze           => "Classic Haze of War (Recommended)",
        CampaignProfile.PlanetarySurveillance => "Planetary Surveillance",
        CampaignProfile.HardcoreVigilance     => "Hardcore Vigilance",
        CampaignProfile.Custom                => "Custom",
        _                                     => profile.ToString()
    };
}
