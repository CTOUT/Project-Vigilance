using HarmonyLib;
using RimWorld;
using Verse;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.World;

public static class IncursionArrivalManager
{
    private static bool isExecutingArrival;

    public static bool IsExecutingArrival
    {
        get => isExecutingArrival;
        set => isExecutingArrival = value;
    }

    public static bool ExecuteArrival(WorldObject_Incursion incursion)
    {
        if (incursion.IncidentDef == null || incursion.IncidentParms == null)
        {
            Log.Error("[Project Vigilance] Cannot execute incursion arrival: IncidentDef or IncidentParms is null.");
            incursion.Destroy();
            return false;
        }

        // Validate that destination map is still active (player has not abandoned or destroyed the settlement)
        Map? targetMap = Current.Game.FindMap(incursion.DestinationTile);
        if (targetMap == null || !Find.Maps.Contains(targetMap))
        {
            var sensoryManager = Find.World?.GetComponent<SensoryManager>();
            bool hasVisual = sensoryManager != null && sensoryManager.HasActiveVision(incursion.DestinationTile);
            bool isIdentified = GeoscapeMod.Settings.visibilityMode == VisibilityMode.Omniscient ||
                                incursion.CurrentIntelligenceTier >= IntelligenceTier.Classified ||
                                hasVisual;

            string fName = incursion.Faction?.Name ?? "enemy forces";
            bool isHostile = incursion.Faction?.HostileTo(Faction.OfPlayer) ?? true;
            Log.Message($"[Project Vigilance] Incursion arrived at destination tile {incursion.Tile}, but the target settlement was abandoned. Aborting incident execution.");

            string msg;
            if (isIdentified)
            {
                msg = isHostile
                    ? $"The raid from {fName} arrived at the abandoned settlement, found no defenders or structures of value, and dispersed."
                    : $"The travellers from {fName} arrived at the abandoned settlement, found no one there, and returned home.";
            }
            else
            {
                msg = "An unidentified force arrived at the abandoned settlement, found no defenders, and dispersed.";
            }

            Messages.Message(msg, MessageTypeDefOf.NeutralEvent, false);
            incursion.Destroy();
            return false;
        }

        // Re-bind target to current active map
        incursion.IncidentParms.target = targetMap;

        isExecutingArrival = true;
        bool success = false;

        try
        {
            Log.Message($"[Project Vigilance] Incursion arrived at destination tile {incursion.Tile}. Executing incident: {incursion.IncidentDef.defName}.");
            success = incursion.IncidentDef.Worker.TryExecute(incursion.IncidentParms);

            if (success && GeoscapeMod.Settings.resetThreatCooldownOnArrival &&
                incursion.IncidentDef.category == IncidentCategoryDefOf.ThreatBig &&
                targetMap.storyState != null)
            {
                var lastThreatField = Traverse.Create(targetMap.storyState).Field("lastThreatBigTick");
                if (lastThreatField.FieldExists())
                {
                    lastThreatField.SetValue(Find.TickManager.TicksGame);
                    Log.Message($"[Project Vigilance] Reset Storyteller LastThreatBigTick to current game tick ({Find.TickManager.TicksGame}) upon incursion arrival.");
                }
            }
        }
        catch (System.Exception ex)
        {
            Log.Error($"[Project Vigilance] Exception while executing incursion arrival incident: {ex}");
        }
        finally
        {
            isExecutingArrival = false;
            incursion.Destroy();
        }

        return success;
    }
}
