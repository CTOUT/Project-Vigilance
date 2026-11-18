using System;

namespace ProjectVigilance.Geoscape.World;

/// <summary>
/// Central lifecycle registry and event dispatcher for planetary incursions.
/// Allows external modders and sub-modules to observe incursion spawns and departures.
/// </summary>
public static class IncursionManager
{
    /// <summary>
    /// Invoked whenever a new incursion world object spawns onto the planetary globe.
    /// </summary>
    public static event Action<WorldObject_Incursion>? OnIncursionSpawned;

    /// <summary>
    /// Invoked whenever an incursion departs, disbands, or is despawned.
    /// </summary>
    public static event Action<WorldObject_Incursion>? OnIncursionDeparted;

    internal static void Notify_IncursionSpawned(WorldObject_Incursion incursion)
    {
        if (incursion != null)
        {
            OnIncursionSpawned?.Invoke(incursion);
        }
    }

    internal static void Notify_IncursionDeparted(WorldObject_Incursion incursion)
    {
        if (incursion != null)
        {
            OnIncursionDeparted?.Invoke(incursion);
        }
    }
}
