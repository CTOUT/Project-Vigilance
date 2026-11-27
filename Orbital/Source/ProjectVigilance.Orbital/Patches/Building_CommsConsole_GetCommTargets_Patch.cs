using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using ProjectVigilance.Geoscape.Orbital;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Appends active trade satellites passing overhead the colony's planetary tile to the Comms Console's
/// communication targets list, creating dynamic, time-limited orbital trade windows.
/// </summary>
[HarmonyPatch(typeof(Building_CommsConsole), nameof(Building_CommsConsole.GetCommTargets))]
public static class Building_CommsConsole_GetCommTargets_Patch
{
    public static IEnumerable<ICommunicable> Postfix(IEnumerable<ICommunicable> __result, Building_CommsConsole __instance, Pawn myPawn)
    {
        HashSet<ICommunicable> yielded = new();

        if (__result != null)
        {
            foreach (ICommunicable communicable in __result)
            {
                if (communicable != null && yielded.Add(communicable))
                {
                    yield return communicable;
                }
            }
        }

        if (__instance?.Map == null || !__instance.Map.Tile.Valid)
        {
            yield break;
        }

        PlanetTile mapTile = __instance.Map.Tile;
        List<Satellite_Trade>? activeTradeSats = OrbitalManager.Instance?.GetActiveTradeSatellitesOverTile(mapTile);

        if (activeTradeSats != null)
        {
            for (int i = 0; i < activeTradeSats.Count; i++)
            {
                Satellite_Trade sat = activeTradeSats[i];
                if (sat != null && sat.CanTradeNow && yielded.Add(sat))
                {
                    yield return sat;
                }
            }
        }
    }
}

/// <summary>
/// Augments the Comms Console building gizmos with direct contact actions for active trade satellites
/// passing overhead the colony, providing instant visual feedback and one-click negotiator assignment.
/// </summary>
[HarmonyPatch(typeof(Building), nameof(Building.GetGizmos))]
public static class Building_CommsConsole_GetGizmos_Patch
{
    public static void Postfix(ref IEnumerable<Gizmo> __result, Building __instance)
    {
        if (__instance is not Building_CommsConsole console || console.Map == null || !console.Map.Tile.Valid)
        {
            return;
        }

        CompPowerTrader? power = console.GetComp<CompPowerTrader>();
        if (power != null && !power.PowerOn)
        {
            return;
        }

        List<Satellite_Trade>? activeTradeSats = OrbitalManager.Instance?.GetActiveTradeSatellitesOverTile(console.Map.Tile);
        if (activeTradeSats == null || activeTradeSats.Count == 0)
        {
            return;
        }

        __result = AppendTradeSatelliteGizmos(__result, console, activeTradeSats);
    }

    private static IEnumerable<Gizmo> AppendTradeSatelliteGizmos(
        IEnumerable<Gizmo>? existing,
        Building_CommsConsole console,
        List<Satellite_Trade> activeTradeSats)
    {
        HashSet<string> existingLabels = new();
        if (existing != null)
        {
            foreach (Gizmo g in existing)
            {
                if (g is Command cmd && !string.IsNullOrEmpty(cmd.Label))
                {
                    existingLabels.Add(cmd.Label);
                }
                yield return g;
            }
        }

        for (int i = 0; i < activeTradeSats.Count; i++)
        {
            Satellite_Trade sat = activeTradeSats[i];
            if (sat == null || !sat.CanTradeNow)
            {
                continue;
            }

            string label = $"Contact {sat.OrbitalCallsign}";
            if (!existingLabels.Add(label))
            {
                continue;
            }

            string traderTitle = sat.TraderKind?.label.CapitalizeFirst() ?? "Orbital Trader";
            yield return new Command_Action
            {
                defaultLabel = label,
                defaultDesc = $"Open trade communications with {sat.OrbitalCallsign} ({traderTitle}).\nFaction: {sat.Faction?.Name ?? "Independent"}\nFootprint: {sat.FootprintRadiusInTiles:F0} tiles.",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/Trade", false) ?? BaseContent.BadTex,
                action = () =>
                {
                    Pawn? negotiator = FindBestNegotiator(console.Map, console);
                    if (negotiator != null)
                    {
                        console.GiveUseCommsJob(negotiator, sat);
                    }
                    else
                    {
                        var freeColonists = console.Map.mapPawns.FreeColonistsSpawned;
                        Pawn? fallback = freeColonists.Count > 0 ? freeColonists[0] : null;
                        if (fallback != null)
                        {
                            sat.TryOpenComms(fallback);
                        }
                    }
                }
            };
        }
    }

    private static Pawn? FindBestNegotiator(Map map, Building_CommsConsole console)
    {
        Pawn? best = null;
        int bestSkill = -1;
        List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
        for (int i = 0; i < colonists.Count; i++)
        {
            Pawn p = colonists[i];
            if (!p.Downed && !p.InMentalState && p.CanReach(console, PathEndMode.InteractionCell, Danger.Some))
            {
                int skill = p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
                if (skill > bestSkill)
                {
                    bestSkill = skill;
                    best = p;
                }
            }
        }
        return best;
    }
}
