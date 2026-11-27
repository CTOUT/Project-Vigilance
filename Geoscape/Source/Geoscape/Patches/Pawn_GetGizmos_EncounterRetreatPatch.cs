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
    public static void Postfix(ref IEnumerable<Gizmo> __result, Pawn __instance)
    {
        if (__instance == null || !__instance.IsColonistPlayerControlled || __instance.Map == null || __instance.Map.IsPlayerHome)
        {
            return;
        }

        Map map = __instance.Map;
        var ambushComp = map.GetComponent<MapComponent_OverlandAmbush>();
        bool isOverlandEncounter = IncursionTacticalRetreatUtility.IsOverlandEncounterMap(map, ambushComp);
        if (!IncursionTacticalRetreatUtility.ShouldShowEncounterRetreatGizmo(map.IsPlayerHome, isOverlandEncounter))
        {
            return;
        }

        bool hasActiveThreats = GenHostility.AnyHostileActiveThreatToPlayer(map, countDormantPawnsAsHostile: false);
        Command_Action? extra = null;

        if (!hasActiveThreats)
        {
            bool hasCombatOrAmbushState = ambushComp != null && ambushComp.HasCombatOrAmbushState;
            if (IncursionTacticalRetreatUtility.ShouldShowReformCaravanUi(
                    isPlayerHome: map.IsPlayerHome,
                    isPocketMap: map.IsPocketMap,
                    isSettlementParent: map.Parent is Settlement,
                    isTemporaryBattlefieldParent: map.Parent is CaravansBattlefield,
                    hasActiveThreats: hasActiveThreats,
                    hasCombatOrAmbushState: hasCombatOrAmbushState))
            {
                extra = new Command_Action
                {
                    defaultLabel = "Reform Caravan",
                    defaultDesc = "Reform your caravan and return to the overland geoscape.",
                    icon = CaravanIcon,
                    alsoClickIfOtherInGroupClicked = false,
                    action = () => IncursionTacticalRetreatUtility.OpenReformCaravanDialog(map)
                };
            }
        }
        else if (ambushComp != null && ambushComp.IsActive && !ambushComp.AmbushSprung)
        {
            extra = new Command_Action
            {
                defaultLabel = "Quietly Disengage",
                defaultDesc = "Quietly withdraw your scouting party without alerting the enemy column. Your squad will reform into a caravan on the geoscape while the enemy force continues past.",
                icon = CaravanIcon,
                alsoClickIfOtherInGroupClicked = false,
                action = () => IncursionTacticalRetreatUtility.PromptStealthDisengage(map, ambushComp)
            };
        }
        else
        {
            extra = new Command_Action
            {
                defaultLabel = "Attempt Retreat",
                defaultDesc = "Order your squad to disengage and fall back to the closest map edge to escape.",
                icon = RetreatIcon,
                alsoClickIfOtherInGroupClicked = false,
                action = () => IncursionTacticalRetreatUtility.PromptCombatRetreat(map, ambushComp)
            };
        }

        if (extra != null)
        {
            __result = AppendWithDeduplication(__result, extra);
        }
    }

    private static IEnumerable<Gizmo> AppendWithDeduplication(IEnumerable<Gizmo>? existing, Command_Action added)
    {
        bool alreadyPresent = false;
        if (existing != null)
        {
            foreach (var gizmo in existing)
            {
                if (!alreadyPresent && gizmo is Command cmd && cmd.Label == added.Label)
                {
                    alreadyPresent = true;
                }
                yield return gizmo;
            }
        }

        if (!alreadyPresent)
        {
            yield return added;
        }
    }
}
