using HarmonyLib;
using Verse;
using ProjectVigilance.Geoscape.World;

namespace ProjectVigilance.Geoscape.Patches;

[HarmonyPatch(typeof(MapDeiniter), nameof(MapDeiniter.Deinit))]
public static class MapDeiniter_Deinit_Patch
{
    [HarmonyPrefix]
    public static void Prefix(Map map)
    {
        if (map == null)
        {
            return;
        }

        var ambushComp = map.GetComponent<MapComponent_OverlandAmbush>();
        ambushComp?.Notify_MapAboutToBeDeinited();
    }
}
