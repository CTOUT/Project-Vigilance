using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Geoscape.World;

namespace ProjectVigilance.Geoscape.Patches;

[StaticConstructorOnStartup]
[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
public static class Pawn_GetGizmos_EncounterRetreatPatch
{
    private static Texture2D? cachedCaravanIcon;
    private static Texture2D CaravanIcon => cachedCaravanIcon ??= ContentFinder<Texture2D>.Get("World/WorldObjects/Expanding/Caravan", true);

    private static Texture2D? cachedRetreatIcon;
    private static Texture2D RetreatIcon => cachedRetreatIcon ??= ContentFinder<Texture2D>.Get("UI/Designators/Cancel", true);

    [HarmonyPostfix]
    public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
    {
        foreach (var gizmo in __result)
        {
            yield return gizmo;
        }

        if (__instance == null || !__instance.IsColonistPlayerControlled || __instance.Map == null)
        {
            yield break;
        }

        Map map = __instance.Map;
        var ambushComp = map.GetComponent<MapComponent_OverlandAmbush>();
        bool isBattlefield = map.Parent is CaravansBattlefield || ambushComp != null;

        if (!isBattlefield)
        {
            yield break;
        }

        bool hasActiveThreats = GenHostility.AnyHostileActiveThreatToPlayer(map, countDormantPawnsAsHostile: false);

        // 1. If map has no active threats: offer direct "Reform Caravan"
        if (!hasActiveThreats)
        {
            yield return new Command_Action
            {
                defaultLabel = "Reform Caravan",
                defaultDesc = "Reform your caravan and return to the overland geoscape.",
                icon = CaravanIcon,
                alsoClickIfOtherInGroupClicked = false,
                action = () =>
                {
                    IncursionTacticalRetreatUtility.OpenReformCaravanDialog(map);
                }
            };
            yield break;
        }

        // 2. If in Unaware Ambush Concealment: offer "Quietly Disengage"
        if (ambushComp != null && ambushComp.IsActive && !ambushComp.AmbushSprung)
        {
            yield return new Command_Action
            {
                defaultLabel = "Quietly Disengage",
                defaultDesc = "Quietly withdraw your scouting party without alerting the enemy column. Your squad will reform into a caravan on the geoscape while the enemy force continues past.",
                icon = CaravanIcon,
                alsoClickIfOtherInGroupClicked = false,
                action = () =>
                {
                    IncursionTacticalRetreatUtility.PromptStealthDisengage(map, ambushComp);
                }
            };
            yield break;
        }

        // 3. If in Active Combat: offer "Attempt Retreat"
        yield return new Command_Action
        {
            defaultLabel = "Attempt Retreat",
            defaultDesc = "Order your squad to disengage and fall back to the closest map edge to escape.",
            icon = RetreatIcon,
            alsoClickIfOtherInGroupClicked = false,
            action = () =>
            {
                IncursionTacticalRetreatUtility.PromptCombatRetreat(map, ambushComp);
            }
        };
    }
}
