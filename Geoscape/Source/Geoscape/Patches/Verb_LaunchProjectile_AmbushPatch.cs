using HarmonyLib;
using ProjectVigilance.Geoscape.World;
using Verse;

namespace ProjectVigilance.Geoscape.Patches;

[HarmonyPatch(typeof(Verb_LaunchProjectile), "TryCastShot")]
public static class Verb_LaunchProjectile_AmbushPatch
{
    [HarmonyPostfix]
    public static void Postfix(Verb_LaunchProjectile __instance, bool __result)
    {
        if (__result && __instance.CasterPawn != null && __instance.CasterPawn.Map != null)
        {
            var ambushComp = __instance.CasterPawn.Map.GetComponent<MapComponent_OverlandAmbush>();
            ambushComp?.Notify_WeaponDischarged(__instance.CasterPawn);
        }
    }
}
