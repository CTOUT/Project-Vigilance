using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Hooks right-click context menus on the Geoscape globe to allow player caravans
/// to deploy lightweight unmanned listening posts and signal beacons directly from carried inventory.
/// </summary>
[HarmonyPatch(typeof(FloatMenuMakerWorld), nameof(FloatMenuMakerWorld.ChoicesAtFor), typeof(Vector2), typeof(Caravan))]
public static class FloatMenuMakerWorld_ChoicesAtFor_Patch
{
    public static void Postfix(Vector2 mousePos, Caravan caravan, ref List<FloatMenuOption> __result)
    {
        if (caravan == null || !caravan.IsPlayerControlled || !caravan.Spawned)
        {
            return;
        }

        PlanetTile clickedTile = GenWorld.MouseTile(false);
        if (!clickedTile.Valid)
        {
            return;
        }

        // If the player caravan is standing on the clicked hex
        if (caravan.Tile == clickedTile)
        {
            bool hasOutpost = Find.WorldObjects?.AllWorldObjects != null &&
                              Find.WorldObjects.AllWorldObjects.OfType<WorldObject_ListeningPost>().Any(p => p.Tile == clickedTile);

            if (!hasOutpost)
            {
                __result ??= new List<FloatMenuOption>();

                // 1. Deploy Automated Listening Post (100 Steel, 4 Components)
                bool canAffordRelay = OutpostMaterialUtility.CanDeployAutomatedRelay(caravan, out string missingRelay);
                string relayLabel = canAffordRelay
                    ? "Deploy Automated Listening Post (100 Steel, 4 Components)"
                    : $"Deploy Automated Listening Post ({missingRelay})";

                __result.Add(new FloatMenuOption(relayLabel, () =>
                {
                    if (OutpostMaterialUtility.CanDeployAutomatedRelay(caravan, out _))
                    {
                        OutpostMaterialUtility.ConsumeAutomatedRelayDeployment(caravan);
                        var def = DefDatabase<WorldObjectDef>.GetNamed("Vigilance_ListeningPost_Automated");
                        var post = (WorldObject_ListeningPost)WorldObjectMaker.MakeWorldObject(def);
                        post.Tile = clickedTile;
                        post.SetFaction(Faction.OfPlayer);
                        post.Kind = ListeningPostKind.AutomatedRelay;
                        Find.WorldObjects?.Add(post);
                        Messages.Message("Automated listening post deployed and active on geoscape.", MessageTypeDefOf.PositiveEvent, false);
                    }
                }, MenuOptionPriority.Default)
                {
                    Disabled = !canAffordRelay
                });

                // 2. Construct Signal Beacon (150 Wood/Stone)
                bool canAffordBeacon = OutpostMaterialUtility.CanDeploySignalBeacon(caravan, out string missingBeacon);
                string beaconLabel = canAffordBeacon
                    ? "Construct Signal Beacon (150 Wood/Stone)"
                    : $"Construct Signal Beacon ({missingBeacon})";

                __result.Add(new FloatMenuOption(beaconLabel, () =>
                {
                    if (OutpostMaterialUtility.CanDeploySignalBeacon(caravan, out _))
                    {
                        OutpostMaterialUtility.ConsumeSignalBeaconDeployment(caravan);
                        var def = DefDatabase<WorldObjectDef>.GetNamed("Vigilance_ListeningPost_Beacon");
                        var post = (WorldObject_ListeningPost)WorldObjectMaker.MakeWorldObject(def);
                        post.Tile = clickedTile;
                        post.SetFaction(Faction.OfPlayer);
                        post.Kind = ListeningPostKind.SignalBeacon;
                        Find.WorldObjects?.Add(post);
                        Messages.Message("Signal beacon constructed and scanning the sector.", MessageTypeDefOf.PositiveEvent, false);
                    }
                }, MenuOptionPriority.Default)
                {
                    Disabled = !canAffordBeacon
                });
            }
        }
    }
}
