using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Hooks caravan map departure to bind pending expedition directives into
/// concrete CaravanArrivalAction_ConstructOutpost instances.
/// </summary>
public static class CaravanExitMapUtility_Patch
{
    [HarmonyPatch(typeof(CaravanExitMapUtility), nameof(CaravanExitMapUtility.ExitMapAndCreateCaravan),
        new[] { typeof(IEnumerable<Pawn>), typeof(Faction), typeof(PlanetTile), typeof(PlanetTile), typeof(PlanetTile), typeof(bool) })]
    public static class ExitMapAndCreateCaravan_TilePatch
    {
        public static void Postfix(Caravan __result, PlanetTile destinationTile)
        {
            ApplyPendingDirectiveIfPresent(__result, destinationTile);
        }
    }

    [HarmonyPatch(typeof(CaravanExitMapUtility), nameof(CaravanExitMapUtility.ExitMapAndCreateCaravan),
        new[] { typeof(IEnumerable<Pawn>), typeof(Faction), typeof(PlanetTile), typeof(Direction8Way), typeof(PlanetTile), typeof(bool) })]
    public static class ExitMapAndCreateCaravan_DirectionPatch
    {
        public static void Postfix(Caravan __result, PlanetTile destinationTile)
        {
            ApplyPendingDirectiveIfPresent(__result, destinationTile);
        }
    }

    private static void ApplyPendingDirectiveIfPresent(Caravan caravan, PlanetTile destinationTile)
    {
        if (caravan == null || !caravan.Spawned || !destinationTile.Valid) return;

        if (CaravanExpeditionUtility.TryConsumePendingDirective(destinationTile, out var directive) && directive != null)
        {
            string outpostName = directive.Kind == ListeningPostKind.AutomatedRelay
                ? "automated listening post"
                : (directive.StuffDef != null ? $"signal beacon ({directive.StuffDef.LabelCap})" : "signal beacon");

            if (directive.DirectiveKind == OutpostDirectiveKind.Dismantle)
            {
                WorldObject_ListeningPost? post = directive.TargetOutpost ??
                    Find.WorldObjects?.AllWorldObjects?.OfType<WorldObject_ListeningPost>().FirstOrDefault(p => p.Tile == destinationTile);

                if (post != null)
                {
                    caravan.pather.StartPath(
                        destinationTile,
                        new CaravanArrivalAction_DismantleOutpost(post, destinationTile),
                        repathImmediately: true,
                        resetPauseStatus: true);

                    Messages.Message($"Caravan {caravan.LabelCap} departed on expedition to dismantle {post.LabelCap} at Tile {destinationTile}.", MessageTypeDefOf.PositiveEvent, false);
                    return;
                }
            }

            caravan.pather.StartPath(
                destinationTile,
                new CaravanArrivalAction_ConstructOutpost(destinationTile, directive.Kind, directive.StuffDef),
                repathImmediately: true,
                resetPauseStatus: true);

            Messages.Message($"Caravan {caravan.LabelCap} departed on construction expedition to establish {outpostName} at Tile {destinationTile}.", MessageTypeDefOf.PositiveEvent, false);
        }
    }
}
