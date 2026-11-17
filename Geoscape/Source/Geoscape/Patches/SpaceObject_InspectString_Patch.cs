using System.Text;
using HarmonyLib;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Geoscape.Orbital;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Appends orbital telemetry and classification to Odyssey space stations and asteroids in the inspect pane,
/// mirroring the telemetry displayed on Vigilance satellites and stations.
/// </summary>
[HarmonyPatch(typeof(WorldObject), nameof(WorldObject.GetInspectString))]
public static class SpaceObject_InspectString_Patch
{
    public static System.Func<WorldObject, bool>? IsSpaceObjectOverride { get; set; }

    public static void Postfix(WorldObject __instance, ref string __result)
    {
        if (__instance == null || __instance is IOrbitalEntity || __instance is WorldObject_ListeningPost || __instance is MapParent || __instance is Caravan || __instance is Site)
        {
            return;
        }

        bool isSpace = false;
        if (IsSpaceObjectOverride != null)
        {
            isSpace = IsSpaceObjectOverride(__instance);
        }
        else if (Find.World != null && __instance.Tile.Valid)
        {
            try
            {
                var layer = __instance.Tile.Layer;
                isSpace = layer?.Def?.isSpace == true || layer?.Def?.defName == "Orbit";
            }
            catch
            {
                isSpace = false;
            }
        }
        else if (__instance.def?.defName?.Contains("Odyssey") == true || __instance.def?.defName?.Contains("Asteroid") == true)
        {
            isSpace = true;
        }

        if (!isSpace)
        {
            return;
        }

        float radius = 125f;
        try
        {
            if (Current.Game?.tickManager != null && __instance.Tile.Valid)
            {
                radius = __instance.DrawPos.magnitude;
            }
        }
        catch
        {
            radius = 125f;
        }

        float visualAlt = Mathf.Max(0f, radius - 100f);
        float approxKm = OrbitalFlightModel.VisualAltitudeToAltitudeKm(visualAlt);

        StringBuilder sb = new();
        if (!string.IsNullOrEmpty(__result))
        {
            sb.AppendLine(__result);
        }

        sb.Append($"Orbital Altitude: ~{Mathf.RoundToInt(approxKm):N0} km");

        string classification = GetSpaceObjectClassification(__instance);
        if (!string.IsNullOrEmpty(classification))
        {
            sb.AppendLine();
            sb.Append($"Classification: {classification}");
        }

        __result = sb.ToString();
    }

    public static string GetSpaceObjectClassification(WorldObject wo)
    {
        string defName = wo.def?.defName?.ToLowerInvariant() ?? "";
        string label = (wo.def?.label ?? defName).ToLowerInvariant();

        if (defName.Contains("asteroid") || label.Contains("asteroid"))
        {
            return "Deep Space Asteroid";
        }
        if (defName.Contains("station") || defName.Contains("ring") || label.Contains("station") || label.Contains("ring"))
        {
            return "Orbital Habitat / Station";
        }
        if (defName.Contains("wreck") || defName.Contains("derelict") || label.Contains("wreck") || label.Contains("derelict"))
        {
            return "Derelict Debris";
        }
        return "Orbital Spacecraft / Facility";
    }
}
