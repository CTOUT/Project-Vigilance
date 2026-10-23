using HarmonyLib;
using ProjectVigilance.Geoscape.World;
using Verse;

namespace ProjectVigilance.Geoscape.Patches;

[HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage))]
public static class Pawn_PostApplyDamage_AmbushPatch
{
    [HarmonyPostfix]
    public static void Postfix(Pawn __instance, DamageInfo dinfo, float totalDamageDealt)
    {
        if (totalDamageDealt > 0f && __instance.Map != null)
        {
            var ambushComp = __instance.Map.GetComponent<MapComponent_OverlandAmbush>();
            ambushComp?.Notify_DamageTaken(__instance, dinfo);
        }
    }
}
