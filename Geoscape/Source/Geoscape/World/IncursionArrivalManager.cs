using RimWorld;
using Verse;

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

        isExecutingArrival = true;
        bool success = false;

        try
        {
            Log.Message($"[Project Vigilance] Incursion arrived at destination tile {incursion.Tile}. Executing incident: {incursion.IncidentDef.defName}.");
            success = incursion.IncidentDef.Worker.TryExecute(incursion.IncidentParms);
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
