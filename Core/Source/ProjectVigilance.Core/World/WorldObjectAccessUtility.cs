using System.Reflection;
using HarmonyLib;
using RimWorld.Planet;

namespace ProjectVigilance.Geoscape.World;

/// <summary>
/// Provides cached reflection access to internal WorldObject fields (tile, spawned).
/// Eliminates repeated HarmonyLib.Traverse allocations during test setups and headless simulation.
/// </summary>
public static class WorldObjectAccessUtility
{
    private static readonly FieldInfo? TileField = AccessTools.Field(typeof(WorldObject), "tile");
    private static readonly FieldInfo? SpawnedField = AccessTools.Field(typeof(WorldObject), "spawned");

    public static void SetTileRaw(WorldObject wo, PlanetTile tile)
    {
        TileField?.SetValue(wo, tile);
    }

    public static void SetSpawnedRaw(WorldObject wo, bool spawned)
    {
        SpawnedField?.SetValue(wo, spawned);
    }

    public static bool GetSpawnedRaw(WorldObject wo)
    {
        return SpawnedField?.GetValue(wo) as bool? ?? true;
    }
}
