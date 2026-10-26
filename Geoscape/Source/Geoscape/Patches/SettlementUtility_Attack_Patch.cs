using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using ProjectVigilance.Geoscape.World;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// When a player caravan assaults an enemy settlement, spawns any colonists who were previously
/// kidnapped and incarcerated at this specific settlement stronghold, enabling direct rescue operations.
/// </summary>
[HarmonyPatch(typeof(SettlementUtility), "AttackNow")]
public static class SettlementUtility_Attack_Patch
{
    [HarmonyPostfix]
    public static void Postfix(Caravan caravan, Settlement settlement)
    {
        if (settlement == null)
        {
            return;
        }

        Map? map = settlement.Map ?? Current.Game?.FindMap(settlement.Tile);
        if (map == null)
        {
            return;
        }

        List<Pawn> captives = IncursionRetreatManager.GetIncarceratedColonistsAt(settlement.Tile);
        if (captives == null || captives.Count == 0)
        {
            return;
        }

        IntVec3 baseCenter = map.Center;

        foreach (Pawn captive in captives)
        {
            if (captive != null && !captive.Spawned && !captive.Dead && !captive.Destroyed)
            {
                CellFinder.TryFindRandomCellNear(baseCenter, map, 12, c => c.Walkable(map) && !c.Fogged(map), out IntVec3 spawnCell);
                GenSpawn.Spawn(captive, spawnCell.IsValid ? spawnCell : baseCenter, map, Rot4.Random);

                if (captive.guest != null && settlement.Faction != null)
                {
                    captive.guest.SetGuestStatus(settlement.Faction, GuestStatus.Prisoner);
                }
            }
        }

        var livingCaptives = captives.Where(c => c != null && !c.Dead && !c.Destroyed).ToList();
        if (livingCaptives.Count > 0)
        {
            string names = string.Join(", ", livingCaptives.Select(c => c.LabelShort));
            Messages.Message($"RESCUE MISSION: Captured colonist(s) ({names}) are incarcerated inside {settlement.LabelCap}! Secure them during the assault.", settlement, MessageTypeDefOf.PositiveEvent);
        }
        IncursionRetreatManager.RemoveIncarceratedColonistsAt(settlement.Tile);
    }
}
