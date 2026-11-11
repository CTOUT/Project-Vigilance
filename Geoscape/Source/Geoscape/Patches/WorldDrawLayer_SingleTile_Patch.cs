using HarmonyLib;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld.Planet;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Suppresses hovered tile hex outlines (<see cref="WorldDrawLayer_MouseTile"/>) beneath unexplored shroud during gameplay.
/// </summary>
[HarmonyPatch(typeof(WorldDrawLayer_MouseTile), "Tile", MethodType.Getter)]
public static class WorldDrawLayer_MouseTile_Tile_Patch
{
    [HarmonyPostfix]
    public static void Postfix(ref PlanetTile __result)
    {
        __result = TileHighlightVisibilityUtility.FilterHighlightTile(__result);
    }
}

/// <summary>
/// Suppresses selected tile hex outlines (<see cref="WorldDrawLayer_SelectedTile"/>) beneath unexplored shroud during
/// gameplay without altering <see cref="WorldSelector.SelectedTile"/> state.
/// </summary>
[HarmonyPatch(typeof(WorldDrawLayer_SelectedTile), "Tile", MethodType.Getter)]
public static class WorldDrawLayer_SelectedTile_Tile_Patch
{
    [HarmonyPostfix]
    public static void Postfix(ref PlanetTile __result)
    {
        __result = TileHighlightVisibilityUtility.FilterHighlightTile(__result);
    }
}
