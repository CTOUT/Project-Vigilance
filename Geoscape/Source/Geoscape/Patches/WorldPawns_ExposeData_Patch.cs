using HarmonyLib;
using ProjectVigilance.Geoscape.World;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Prepares Geoscape pawns and scrubs dangling social relations across <see cref="WorldPawns"/>
/// before RimWorld writes <c>&lt;worldPawns&gt;</c> into the save file.
/// </summary>
[HarmonyPatch(typeof(WorldPawns), nameof(WorldPawns.ExposeData))]
public static class WorldPawns_ExposeData_Patch
{
    public static void Prefix(WorldPawns __instance)
    {
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            WorldPawnIntegrityUtility.PrepareWorldPawnsForSave(__instance);
        }
    }

    public static void Postfix(WorldPawns __instance)
    {
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            WorldPawnIntegrityUtility.PrepareWorldPawnsForSave(__instance);
        }
    }
}
