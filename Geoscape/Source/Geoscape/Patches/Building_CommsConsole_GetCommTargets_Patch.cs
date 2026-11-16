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
    public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Building __instance)
    {
        if (__result != null)
        {
            foreach (Gizmo g in __result)
            {
                yield return g;
            }
        }

        if (__instance is Building_CommsConsole console && console.Map != null && console.Map.Tile.Valid)
        {
            CompPowerTrader? power = console.GetComp<CompPowerTrader>();
            if (power != null && !power.PowerOn)
            {
                yield break;
            }

            List<Satellite_Trade>? activeTradeSats = OrbitalManager.Instance?.GetActiveTradeSatellitesOverTile(console.Map.Tile);
            if (activeTradeSats != null && activeTradeSats.Count > 0)
            {
                for (int i = 0; i < activeTradeSats.Count; i++)
                {
                    Satellite_Trade sat = activeTradeSats[i];
                    if (sat == null || !sat.CanTradeNow) continue;
                    string traderTitle = sat.TraderKind?.label.CapitalizeFirst() ?? "Orbital Trader";
                    yield return new Command_Action
                    {
                        defaultLabel = $"Contact {sat.OrbitalCallsign}",
                        defaultDesc = $"Open trade communications with {sat.OrbitalCallsign} ({traderTitle}).\nFaction: {sat.Faction?.Name ?? "Independent"}\nFootprint: {sat.FootprintRadiusInTiles:F0} tiles.",
                        icon = ContentFinder<Texture2D>.Get("UI/Commands/Trade", false) ?? BaseContent.BadTex,
                        action = () =>
                        {
                            Pawn? negotiator = console.Map.mapPawns.FreeColonistsSpawned
                                .Where(p => !p.Downed && !p.InMentalState && p.CanReach(console, PathEndMode.InteractionCell, Danger.Some))
                                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0)
                                .FirstOrDefault();

                            if (negotiator != null)
                            {
                                console.GiveUseCommsJob(negotiator, sat);
                            }
                            else
                            {
                                sat.TryOpenComms(console.Map.mapPawns.FreeColonistsSpawned.FirstOrDefault()!);
                            }
                        }
                    };
                }
            }
        }
    }
}
