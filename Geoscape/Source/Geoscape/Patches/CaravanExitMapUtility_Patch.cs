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
        public static void Postfix(Caravan __result, PlanetTile exitFromTile, PlanetTile destinationTile)
        {
            ApplyPendingDirectiveIfPresent(__result, exitFromTile, destinationTile);
        }
    }

    [HarmonyPatch(typeof(CaravanExitMapUtility), nameof(CaravanExitMapUtility.ExitMapAndCreateCaravan),
        new[] { typeof(IEnumerable<Pawn>), typeof(Faction), typeof(PlanetTile), typeof(Direction8Way), typeof(PlanetTile), typeof(bool) })]
    public static class ExitMapAndCreateCaravan_DirectionPatch
    {
        public static void Postfix(Caravan __result, PlanetTile exitFromTile, PlanetTile destinationTile)
        {
            ApplyPendingDirectiveIfPresent(__result, exitFromTile, destinationTile);
        }
    }

    private static void ApplyPendingDirectiveIfPresent(Caravan caravan, PlanetTile exitFromTile, PlanetTile destinationTile)
    {
        if (caravan == null || !caravan.Spawned) return;

        if (CaravanExpeditionUtility.TryConsumePendingDirectiveForCaravan(caravan, exitFromTile, destinationTile, out var directive) && directive != null)
        {
            PlanetTile finalTarget = directive.TargetTile.Valid ? directive.TargetTile : (destinationTile.Valid ? destinationTile : caravan.Tile);
            string outpostName = directive.Kind == ListeningPostKind.AutomatedRelay
                ? "automated listening post"
                : (directive.StuffDef != null ? $"signal beacon ({directive.StuffDef.LabelCap})" : "signal beacon");

            if (directive.DirectiveKind == OutpostDirectiveKind.Dismantle)
            {
                WorldObject_ListeningPost? post = directive.TargetOutpost ??
                    Current.Game?.World?.worldObjects?.AllWorldObjects?.OfType<WorldObject_ListeningPost>().FirstOrDefault(p => p.Tile == finalTarget);

                if (post != null)
                {
                    caravan.pather.StartPath(
                        finalTarget,
                        new CaravanArrivalAction_DismantleOutpost(post, finalTarget),
                        repathImmediately: true,
                        resetPauseStatus: true);

                    Messages.Message($"Caravan {caravan.LabelCap} departed on expedition to dismantle {post.LabelCap} at Tile {finalTarget}.", MessageTypeDefOf.PositiveEvent, false);
                    return;
                }
            }

            caravan.pather.StartPath(
                finalTarget,
                new CaravanArrivalAction_ConstructOutpost(finalTarget, directive.Kind, directive.StuffDef),
                repathImmediately: true,
                resetPauseStatus: true);

            Messages.Message($"Caravan {caravan.LabelCap} departed on construction expedition to establish {outpostName} at Tile {finalTarget}.", MessageTypeDefOf.PositiveEvent, false);
        }
    }
}
