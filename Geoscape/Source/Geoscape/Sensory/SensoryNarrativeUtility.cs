using RimWorld;
using RimWorld.Planet;
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
    /// Resolves a readable target name or sector designation for a planet tile,
    /// checking maps, settlements, and world objects before falling back to tile ID coordinates.
    /// </summary>
    public static string ResolveTargetSectorLabel(PlanetTile tile, Map? targetMap = null, string fallback = "colony territory")
    {
        if (targetMap?.Parent != null)
        {
            return targetMap.Parent.LabelCap;
        }

        if (Current.Game != null && tile.Valid)
        {
            Map? map = Current.Game.FindMap(tile);
            if (map?.Parent != null)
            {
                return map.Parent.LabelCap;
            }
        }

        if (Find.World?.worldObjects != null && tile.Valid)
        {
            var settlement = Find.World.worldObjects.SettlementAt(tile);
            if (settlement != null)
            {
                return settlement.LabelCap;
            }

            var mapParent = Find.World.worldObjects.MapParentAt(tile);
            if (mapParent != null)
            {
                return mapParent.LabelCap;
            }
        }

        return tile.Valid ? $"territory at tile {tile.tileId}" : fallback;
    }

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

    /// <summary>
    /// Returns the world map label for a non-player static site or settlement that has been
    /// detected in the outer sensor layer (or explored out of the shroud) but not yet identified.
    /// </summary>
    public static string GetUnknownSiteLabel()
    {
        return IsLowTech ? "unknown site" : "unidentified site";
    }

    /// <summary>
    /// Returns the world map label for an anomalous landmark, ancient ruin, or otherworldly structure
    /// that has been detected but not yet identified.
    /// </summary>
    public static string GetUnknownLandmarkLabel()
    {
        return IsLowTech ? "unknown landmark" : "unidentified landmark";
    }

    /// <summary>
    /// Returns the inspect pane status text for an unidentified static site or settlement.
    /// </summary>
    public static string GetUnknownSiteInspectString()
    {
        if (IsLowTech)
        {
            return "Status: Unidentified Site\nDistant smoke, cleared ground, or structures have been sighted in this sector, but our scouts have not yet approached close enough to determine what kind of camp or settlement lies here.";
        }

        return "Status: Unidentified Site\nLong-range reconnaissance indicates a surface installation, outpost, or settlement in this sector. Bring the site within identification sensor range or approach with a caravan to classify its nature and faction alignment.";
    }

    /// <summary>
    /// Returns the inspect pane status text for an anomalous landmark, monolith, or ancient complex.
    /// </summary>
    public static string GetUnknownLandmarkInspectString()
    {
        if (IsLowTech)
        {
            return "Status: Unidentified Landmark\nOur scouts report ancient architectural formations or otherworldly shapes rising from the wilderness. Move closer with a caravan to inspect.";
        }

        return "Status: Anomalous Landmark\nLong-range sensors register prominent architectural geometry or exotic energy signatures at these coordinates. Dispatch an expedition or expand sensor coverage to classify.";
    }

    /// <summary>
    /// Returns the atmospheric info card / tooltip description for an unidentified static site or settlement.
    /// </summary>
    public static string GetUnknownSiteDescription()
    {
        if (IsLowTech)
        {
            return "Distant signs of habitation or activity spotted across the wilderness. Without closer reconnaissance, our lookouts cannot tell whether this is a permanent settlement, a temporary camp, or a fortified outpost.";
        }

        return "An unidentified surface site detected beyond identification range. Sensor telemetry confirms artificial structures or encampment signatures at these coordinates, but closer reconnaissance is required to determine site classification and faction ownership.";
    }

    /// <summary>
    /// Returns the atmospheric info card description for an anomalous landmark or ancient structure.
    /// </summary>
    public static string GetUnknownLandmarkDescription()
    {
        if (IsLowTech)
        {
            return "Ancient megaliths, monolithic ruins, or otherworldly formations sighted across the frontier. Closer exploration is required to uncover their nature.";
        }

        return "An anomalous surface structure detected beyond identification range. Sensor telemetry confirms pre-collapse architectural remnants or unusual energy resonance at these coordinates.";
    }
}
