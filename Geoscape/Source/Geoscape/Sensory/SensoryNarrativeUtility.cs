using RimWorld;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Provides adaptive narrative tone, terminology, and letter formatting
/// respecting the player colony's tech level and sensor capabilities.
/// Differentiates between tribal/medieval spotters, industrial lookouts,
/// and advanced electronic/radar surveillance systems.
/// </summary>
public static class SensoryNarrativeUtility
{
    /// <summary>
    /// Returns true if the player faction has a Neolithic or Medieval tech level.
    /// </summary>
    public static bool IsLowTech
    {
        get
        {
            if (Current.ProgramState == ProgramState.Playing && Faction.OfPlayerSilentFail?.def != null)
            {
                return Faction.OfPlayerSilentFail.def.techLevel <= TechLevel.Medieval;
            }
            return false;
        }
    }

    /// <summary>
    /// Returns the appropriate world map label for an unidentified contact at Tier 2 (Contact).
    /// </summary>
    public static string GetContactLabel(ISensorProvider? sensor = null, int trackingId = 0)
    {
        if (IsLowTech)
        {
            return trackingId > 0 ? $"Approaching Strangers #{trackingId}" : "Approaching Strangers";
        }

        if (sensor != null && !sensor.IsTechnological)
        {
            return trackingId > 0 ? $"Approaching Force #{trackingId}" : "Approaching Force";
        }

        return trackingId > 0 ? $"Contact-{trackingId}" : "Unidentified Contact";
    }

    /// <summary>
    /// Returns the atmospheric inspect/info card description for an unidentified contact at Tier 2 (Contact).
    /// </summary>
    public static string GetContactDescription(ISensorProvider? sensor = null)
    {
        if (IsLowTech)
        {
            return "Distant movement spotted across the land. From this distance, our watchers can only make out dust and indistinct silhouettes on the horizon. It is impossible to tell whether they are peaceful wanderers, a trading party, or an approaching warband.";
        }

        if (sensor != null && sensor.IsTechnological)
        {
            return "An unidentified contact detected traversing the planetary surface. Long-range sensors have registered movement telemetry, but sensor data is currently insufficient to determine whether this contact is a military warband, trade caravan, or travelling party.";
        }

        return "An unidentified group spotted moving across the planetary surface. Long-range lookouts have sighted distant movement, but visual observations are currently insufficient to determine whether they are a hostile warband, a trade caravan, or peaceful travellers.";
    }

    /// <summary>
    /// Returns the notification letter title for an initial contact early warning.
    /// </summary>
    public static string GetContactLetterTitle(ISensorProvider sensor, int trackingId = 0)
    {
        if (IsLowTech)
        {
            return trackingId > 0 ? $"Sighting: Strangers #{trackingId}" : "Sighting: Approaching Strangers";
        }

        if (!sensor.IsTechnological)
        {
            return trackingId > 0 ? $"Early Warning: Force #{trackingId}" : "Early Warning: Approaching Force";
        }

        return trackingId > 0 ? $"Early Warning: Unidentified Contact [Contact-{trackingId}]" : "Early Warning: Unidentified Contact";
    }

    /// <summary>
    /// Returns the notification letter body text for an initial contact early warning.
    /// </summary>
    public static string GetContactLetterBody(ISensorProvider sensor, string targetName, float speedTilesPerDay, int trackingId = 0)
    {
        string designation = trackingId > 0
            ? (IsLowTech ? $"party #{trackingId}" : $"Contact-{trackingId}")
            : (IsLowTech ? "an approaching group" : "an unidentified contact");

        if (IsLowTech)
        {
            return $"{sensor.SensorLabel} have spotted {designation} moving across the wilderness towards {targetName}.\n\n" +
                   $"Direction: Towards {targetName}\n" +
                   $"Estimated Pace: ~{speedTilesPerDay:F0} hexes/day\n" +
                   $"Spotted by: {sensor.SensorLabel}\n\n" +
                   "From this distance, our lookouts cannot tell who they are or what their intentions might be. Prepare our defences or send scouts to investigate.";
        }

        if (sensor.IsTechnological)
        {
            return $"{sensor.SensorLabel} have detected {designation} advancing towards {targetName}.\n\n" +
                   $"Heading: Towards {targetName}\n" +
                   $"Estimated Speed: ~{speedTilesPerDay:F0} hexes/day\n" +
                   $"Sensor: {sensor.SensorLabel}\n\n" +
                   "Telemetry is currently insufficient to determine faction alignment or intentions. Prepare colony defences or scramble reconnaissance to investigate.";
        }

        return $"{sensor.SensorLabel} have spotted {designation} advancing towards {targetName}.\n\n" +
               $"Heading: Towards {targetName}\n" +
               $"Estimated Speed: ~{speedTilesPerDay:F0} hexes/day\n" +
               $"Reported by: {sensor.SensorLabel}\n\n" +
               "Observations are currently insufficient to determine faction alignment or intentions. Prepare colony defences or dispatch scouts to investigate.";
    }

    /// <summary>
    /// Returns the attribution prefix line for classified confirmation letters (e.g. "Sensor: ...", "Reported by: ...", "Spotted by: ...").
    /// </summary>
    public static string GetAttributionLine(ISensorProvider sensor)
    {
        if (sensor.IsTechnological)
        {
            return $"Sensor: {sensor.SensorLabel}";
        }

        if (IsLowTech)
        {
            return $"Spotted by: {sensor.SensorLabel}";
        }

        return $"Reported by: {sensor.SensorLabel}";
    }

    /// <summary>
    /// Returns the tactical recommendation for a confirmed hostile warband.
    /// </summary>
    public static string GetHostileClosingAdvice(ISensorProvider sensor)
    {
        if (IsLowTech)
        {
            return "Prepare our warriors for battle, or send a party to confront them before they reach the settlement.";
        }

        if (sensor.IsTechnological)
        {
            return "Prepare your colony for tactical engagement, or deploy an interception caravan to confront them in the field.";
        }

        return "Prepare your colony for combat, or deploy an interception caravan to confront them in the field.";
    }

    /// <summary>
    /// Returns the description for approaching beggars tailored to tech level.
    /// </summary>
    public static string GetBeggarsIntentDescription()
    {
        if (IsLowTech)
        {
            return "They are unarmed and in desperate need of charity. They may ask for food, herbal medicine, or supplies.";
        }

        return "They are unarmed and in desperate need of charity. They may ask for food, medicine, or supplies.";
    }
}
