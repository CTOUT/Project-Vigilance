using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Core.Modules;
using ProjectVigilance.Geoscape.Orbital;
using ProjectVigilance.Geoscape.Sensory;
using ProjectVigilance.Geoscape.World;

namespace ProjectVigilance.Geoscape.Patches;

public static partial class WorldObject_Visibility_Patch
{
    // ---------------------------------------------------------
    // 6. Detection-Only Unknown Site Masking (Settlements, Sites, Outposts, Camps, Landmarks)
    // ---------------------------------------------------------
    [HarmonyPatch]
    public static class UnknownSite_Label_Patch
    {
        public static bool Prepare() => TargetMethods().Any();

        public static IEnumerable<MethodBase> TargetMethods() => ResolveDeclaredMethods(
            "get_Label",
            typeof(WorldObject),
            typeof(Settlement),
            typeof(Site),
            typeof(Caravan),
            typeof(Gravship),
            typeof(RoutePlannerWaypoint),
            typeof(SpaceMapParent),
            typeof(WorldObject_ListeningPost));

        public static bool Prefix(WorldObject __instance, ref string __result)
        {
            if (SensoryManager.ShouldMaskWorldObjectAsUnknown(__instance))
            {
                __result = LandmarkSensoryUtility.IsAnomalousStructure(__instance)
                    ? SensoryNarrativeUtility.GetUnknownLandmarkLabel()
                    : SensoryNarrativeUtility.GetUnknownSiteLabel();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    public static class UnknownSite_ExpandingIcon_Patch
    {
        public static bool Prepare() => TargetMethods().Any();

        public static IEnumerable<MethodBase> TargetMethods() => ResolveDeclaredMethods(
            "get_ExpandingIcon",
            typeof(WorldObject),
            typeof(Settlement),
            typeof(Site),
            typeof(EscapeShip));

        public static bool Prefix(WorldObject __instance, ref Texture2D __result)
        {
            if (SensoryManager.ShouldMaskWorldObjectAsUnknown(__instance))
            {
                __result = LandmarkSensoryUtility.IsAnomalousStructure(__instance)
                    ? LandmarkSensoryUtility.GetUnknownLandmarkExpandingTexture()
                    : GetUnknownSiteExpandingTexture();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    public static class UnknownSite_ExpandingIconColor_Patch
    {
        public static bool Prepare() => TargetMethods().Any();

        public static IEnumerable<MethodBase> TargetMethods() => ResolveDeclaredMethods(
            "get_ExpandingIconColor",
            typeof(WorldObject),
            typeof(EscapeShip),
            typeof(WorldObject_ListeningPost));

        public static bool Prefix(WorldObject __instance, ref Color __result)
        {
            if (SensoryManager.ShouldMaskWorldObjectAsUnknown(__instance))
            {
                __result = LandmarkSensoryUtility.IsAnomalousStructure(__instance)
                    ? LandmarkSensoryUtility.TacticalCyan
                    : UnknownSiteIconColor;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(WorldObject), "get_ExpandingMaterial")]
    public static class UnknownSite_ExpandingMaterial_Patch
    {
        public static bool Prefix(WorldObject __instance, ref Material? __result)
        {
            if (SensoryManager.ShouldMaskWorldObjectAsUnknown(__instance))
            {
                __result = null;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    public static class UnknownSite_Material_Patch
    {
        public static bool Prepare() => TargetMethods().Any();

        public static IEnumerable<MethodBase> TargetMethods() => ResolveDeclaredMethods(
            "get_Material",
            typeof(WorldObject),
            typeof(Settlement),
            typeof(Site),
            typeof(Camp),
            typeof(PeaceTalks),
            typeof(EscapeShip),
            typeof(WorldObject_ListeningPost));

        public static bool Prefix(WorldObject __instance, ref Material __result)
        {
            if (SensoryManager.ShouldMaskWorldObjectAsUnknown(__instance))
            {
                __result = LandmarkSensoryUtility.IsAnomalousStructure(__instance)
                    ? LandmarkSensoryUtility.GetUnknownLandmarkWorldMaterial()
                    : GetUnknownSiteWorldMaterial();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    public static class UnknownSite_GetInspectString_Patch
    {
        public static bool Prepare() => TargetMethods().Any();

        public static IEnumerable<MethodBase> TargetMethods() => ResolveDeclaredMethods(
            nameof(WorldObject.GetInspectString),
            typeof(WorldObject),
            typeof(MapParent),
            typeof(Settlement),
            typeof(Site),
            typeof(AbandonedSettlement),
            typeof(WorldObject_EncounterAftermath),
            typeof(WorldObject_ListeningPost));

        public static bool Prefix(WorldObject __instance, ref string __result)
        {
            if (SensoryManager.ShouldMaskWorldObjectAsUnknown(__instance))
            {
                __result = LandmarkSensoryUtility.IsAnomalousStructure(__instance)
                    ? SensoryNarrativeUtility.GetUnknownLandmarkInspectString()
                    : SensoryNarrativeUtility.GetUnknownSiteInspectString();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    public static class UnknownSite_GetDescription_Patch
    {
        public static bool Prepare() => TargetMethods().Any();

        public static IEnumerable<MethodBase> TargetMethods() => ResolveDeclaredMethods(
            nameof(WorldObject.GetDescription),
            typeof(WorldObject),
            typeof(Site));

        public static bool Prefix(WorldObject __instance, ref string __result)
        {
            if (SensoryManager.ShouldMaskWorldObjectAsUnknown(__instance))
            {
                __result = LandmarkSensoryUtility.IsAnomalousStructure(__instance)
                    ? SensoryNarrativeUtility.GetUnknownLandmarkDescription()
                    : SensoryNarrativeUtility.GetUnknownSiteDescription();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    public static class UnknownSite_ShowRelatedQuests_Patch
    {
        public static bool Prepare() => TargetMethods().Any();

        public static IEnumerable<MethodBase> TargetMethods() => ResolveDeclaredMethods(
            "get_ShowRelatedQuests",
            typeof(WorldObject),
            typeof(Settlement));

        public static bool Prefix(WorldObject __instance, ref bool __result)
        {
            if (SensoryManager.ShouldMaskWorldObjectAsUnknown(__instance))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    public static class UnknownSite_GetGizmos_Patch
    {
        public static bool Prepare() => TargetMethods().Any();

        public static IEnumerable<MethodBase> TargetMethods() => ResolveDeclaredMethods(
            nameof(WorldObject.GetGizmos),
            typeof(WorldObject),
            typeof(MapParent),
            typeof(Settlement),
            typeof(Site),
            typeof(WorldObject_ListeningPost));

        public static bool Prefix(WorldObject __instance, ref IEnumerable<Gizmo> __result)
        {
            if (SensoryManager.ShouldMaskWorldObjectAsUnknown(__instance))
            {
                __result = Enumerable.Empty<Gizmo>();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    public static class UnknownSite_GetFloatMenuOptions_Patch
    {
        public static bool Prepare() => TargetMethods().Any();

        public static IEnumerable<MethodBase> TargetMethods() => ResolveDeclaredMethods(
            nameof(WorldObject.GetFloatMenuOptions),
            typeof(WorldObject),
            typeof(MapParent),
            typeof(Settlement),
            typeof(Site),
            typeof(PeaceTalks),
            typeof(WorldObject_EncounterAftermath),
            typeof(WorldObject_ListeningPost));

        public static bool Prefix(WorldObject __instance, Caravan caravan, ref IEnumerable<FloatMenuOption> __result)
        {
            if (SensoryManager.ShouldMaskWorldObjectAsUnknown(__instance))
            {
                __result = BuildInvestigateFloatMenuOptions(__instance, caravan);
                return false;
            }
            return true;
        }

        private static IEnumerable<FloatMenuOption> BuildInvestigateFloatMenuOptions(WorldObject site, Caravan caravan)
        {
            if (caravan == null || !caravan.IsPlayerControlled || !site.Tile.Valid)
            {
                yield break;
            }

            string label = $"Investigate {SensoryNarrativeUtility.GetUnknownSiteLabel()} (Tile {site.Tile.tileId})";
            yield return new FloatMenuOption(label, () =>
            {
                if (caravan.Spawned && caravan.pather != null)
                {
                    caravan.pather.StartPath(site.Tile, null, repathImmediately: true);
                }
            });
        }
    }

    // ---------------------------------------------------------
    // 7. Tile Terrain & World Inspect Pane Shrouding
    // ---------------------------------------------------------
    [HarmonyPatch(typeof(WorldInspectPane), "GetLabel")]
    public static class WorldInspectPane_GetLabel_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(ref string __result)
        {
            if (GeoscapeRules.VisibilityMode != VisibilityMode.FogOfWar)
            {
                return true;
            }

            if (Find.WorldSelector.NumSelectedObjects == 0 && Find.WorldSelector.SelectedTile.Valid)
            {
                PlanetTile tile = Find.WorldSelector.SelectedTile;
                TileIntelLevel intel = TerrainTelemetryUtility.GetTileIntelLevel(tile);
                if (intel == TileIntelLevel.Unexplored)
                {
                    __result = "Unexplored";
                    return false;
                }

                if (intel == TileIntelLevel.DetectedOnly)
                {
                    BiomeDef? biome = Find.WorldGrid?[tile]?.PrimaryBiome;
                    if (biome != null)
                    {
                        __result = biome.LabelCap;
                        return false;
                    }
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(WorldInspectPane), "TileInspectString", MethodType.Getter)]
    public static class WorldInspectPane_TileInspectString_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(ref string __result)
        {
            if (GeoscapeRules.VisibilityMode != VisibilityMode.FogOfWar)
            {
                return true;
            }

            PlanetTile tile = Find.WorldSelector.SelectedTile;
            if (tile.Valid)
            {
                TileIntelLevel intel = TerrainTelemetryUtility.GetTileIntelLevel(tile);
                if (intel == TileIntelLevel.Unexplored)
                {
                    __result = SensoryNarrativeUtility.IsLowTech
                        ? "Uncharted territory. Send scouts or travellers to explore this region."
                        : "This region is completely unexplored. Dispatch a caravan or establish reconnaissance to chart the terrain.";
                    return false;
                }

                if (intel == TileIntelLevel.DetectedOnly)
                {
                    __result = TerrainTelemetryUtility.BuildDetectedTileInspectString(tile);
                    return false;
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(WorldInspectPane), nameof(WorldInspectPane.DoInspectPaneButtons))]
    public static class WorldInspectPane_DoInspectPaneButtons_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Rect rect, ref float lineEndWidth)
        {
            if (GeoscapeRules.VisibilityMode != VisibilityMode.FogOfWar)
            {
                return true;
            }

            if (Find.WorldSelector.SingleSelectedObject != null &&
                SensoryManager.ShouldMaskWorldObjectAsUnknown(Find.WorldSelector.SingleSelectedObject))
            {
                return false;
            }

            if (Find.WorldSelector.NumSelectedObjects == 0 && Find.WorldSelector.SelectedTile.Valid &&
                !StartingSiteFeedbackUtility.IsTileExplored(Find.WorldSelector.SelectedTile))
            {
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(WorldInspectPane), "CurTabs", MethodType.Getter)]
    public static class WorldInspectPane_CurTabs_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ref IEnumerable<InspectTabBase> __result)
        {
            if (GeoscapeRules.VisibilityMode != VisibilityMode.FogOfWar)
            {
                return;
            }

            if (Find.WorldSelector.SingleSelectedObject != null &&
                SensoryManager.ShouldMaskWorldObjectAsUnknown(Find.WorldSelector.SingleSelectedObject))
            {
                __result = Enumerable.Empty<InspectTabBase>();
                return;
            }

            if (Find.WorldSelector.NumSelectedObjects == 0 && Find.WorldSelector.SelectedTile.Valid &&
                !StartingSiteFeedbackUtility.IsTileExplored(Find.WorldSelector.SelectedTile))
            {
                __result = Enumerable.Empty<InspectTabBase>();
            }
        }
    }

    [HarmonyPatch(typeof(WITab_Terrain), "IsVisible", MethodType.Getter)]
    public static class WITab_Terrain_IsVisible_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ref bool __result)
        {
            if (!__result || GeoscapeRules.VisibilityMode != VisibilityMode.FogOfWar)
            {
                return;
            }

            PlanetTile tile = Find.WorldSelector.SelectedTile;
            if (tile.Valid && Find.WorldSelector.NumSelectedObjects == 0 &&
                TerrainTelemetryUtility.GetTileIntelLevel(tile) != TileIntelLevel.Identified)
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(WITab_Terrain), "FillTab")]
    public static class WITab_Terrain_FillTab_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (GeoscapeRules.VisibilityMode != VisibilityMode.FogOfWar)
            {
                return true;
            }

            PlanetTile tile = Find.WorldSelector.SelectedTile;
            if (tile.Valid && Find.WorldSelector.NumSelectedObjects == 0 &&
                TerrainTelemetryUtility.GetTileIntelLevel(tile) != TileIntelLevel.Identified)
            {
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(WITab_Planet), "IsVisible", MethodType.Getter)]
    public static class WITab_Planet_IsVisible_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ref bool __result)
        {
            if (!__result || GeoscapeRules.VisibilityMode != VisibilityMode.FogOfWar)
            {
                return;
            }

            PlanetTile tile = Find.WorldSelector.SelectedTile;
            if (tile.Valid && Find.WorldSelector.NumSelectedObjects == 0 &&
                !StartingSiteFeedbackUtility.IsTileExplored(tile))
            {
                __result = false;
            }
        }
    }

    // ---------------------------------------------------------
    // 8. Cell Inspector Shrouding on World Map (Left Alt)
    // ---------------------------------------------------------
    [HarmonyPatch(typeof(CellInspectorDrawer), "DrawWorldInspector")]
    public static class CellInspectorDrawer_DrawWorldInspector_Patch
    {
        private static bool delegatesInitialised;
        private static Action<string>? drawHeader;
        private static Action<string, string>? drawRow;
        private static Action? drawDivider;

        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (GeoscapeRules.VisibilityMode != VisibilityMode.FogOfWar)
            {
                return true;
            }

            PlanetTile mouseTile = GenWorld.MouseTile(false);
            if (!mouseTile.Valid)
            {
                return true;
            }

            TileIntelLevel intel = TerrainTelemetryUtility.GetTileIntelLevel(mouseTile);
            if (intel == TileIntelLevel.Unexplored)
            {
                return false;
            }

            if (intel == TileIntelLevel.DetectedOnly)
            {
                return !TryDrawDetectedOnlyInspector(mouseTile);
            }

            return true;
        }

        private static bool TryDrawDetectedOnlyInspector(PlanetTile mouseTile)
        {
            if (!TryEnsureDrawDelegates())
            {
                return false;
            }

            Tile? tile = Find.WorldGrid?[mouseTile];
            if (tile == null)
            {
                return false;
            }

            List<WorldObject>? worldObjects = GenWorldUI.WorldObjectsUnderMouse(UI.MousePositionOnUI);
            if (worldObjects != null)
            {
                for (int i = 0; i < worldObjects.Count; i++)
                {
                    WorldObject wo = worldObjects[i];
                    if (wo == null || ShouldHideWorldObject(wo))
                    {
                        continue;
                    }

                    if (wo is WorldObject_Incursion incursion && incursion.CurrentIntelligenceTier < IntelligenceTier.Contact)
                    {
                        continue;
                    }

                    drawHeader!(wo.LabelCap);
                    if (wo is Caravan caravan)
                    {
                        DrawCaravanInspectorRows(caravan);
                    }
                    else if (wo is Settlement settlement &&
                             !SensoryManager.ShouldMaskWorldObjectAsUnknown(settlement) &&
                             settlement.Faction != null)
                    {
                        DrawSettlementFactionRows(settlement);
                    }

                    drawDivider!();
                }
            }

            drawRow!("Biome_Label".Translate(), tile.PrimaryBiome.LabelCap);
            if (!tile.PrimaryBiome.impassable && tile.hilliness != Hilliness.Undefined)
            {
                drawRow!("Hilliness_Label".Translate(), tile.hilliness.GetLabelCap());
            }

            if (tile is SurfaceTile surfaceTile)
            {
                if (!surfaceTile.Roads.NullOrEmpty())
                {
                    RoadDef? road = surfaceTile.Roads.Select(rl => rl.road).MaxBy(r => r.priority);
                    if (road != null)
                    {
                        drawRow!("Road_Label".Translate(), road.LabelCap);
                    }
                }

                if (!surfaceTile.Rivers.NullOrEmpty() && surfaceTile.Rivers[0].river != null)
                {
                    drawRow!("River_Label".Translate(), surfaceTile.Rivers[0].river.LabelCap);
                }
            }

            drawRow!("AvgTemp".Translate(), TerrainTelemetryUtility.FormatEstimatedTemperature(mouseTile));

            if (TerrainTelemetryUtility.HasUnidentifiedManMadeLandmark(mouseTile))
            {
                drawRow!("Signature", "Unidentified landmark");
            }

            return true;
        }

        private static void DrawCaravanInspectorRows(Caravan caravan)
        {
            drawRow!("CaravanColonists_Label".Translate(), caravan.pawns.Count<Pawn>(p => p.IsColonist).ToString());
            if (caravan.pather.Moving)
            {
                if (!caravan.pather.MovingNow)
                {
                    drawRow!("CaravanStatus_Label".Translate(), CaravanBedUtility.AppendUsingBedsLabel("CaravanResting".Translate(), caravan.beds.GetUsedBedCount()));
                }
                else if (caravan.pather.ArrivalAction != null)
                {
                    drawRow!("CaravanStatus_Label".Translate(), caravan.pather.ArrivalAction.ReportString);
                }
                else
                {
                    drawRow!("CaravanStatus_Label".Translate(), "CaravanTraveling".Translate());
                }

                float days = (float)CaravanArrivalTimeEstimator.EstimatedTicksToArrive(caravan, true) / 60000f;
                drawRow!("CaravanTTD_Label".Translate(), days.ToString("0.#"));
            }
            else
            {
                Settlement? visited = CaravanVisitUtility.SettlementVisitedNow(caravan);
                drawRow!(
                    "CaravanStatus_Label".Translate(),
                    visited != null ? "CaravanVisiting".Translate(visited.Label) : "CaravanWaiting".Translate());
            }
        }

        private static void DrawSettlementFactionRows(Settlement settlement)
        {
            drawRow!("Faction_Label".Translate(), settlement.Faction!.Name);
            if (settlement.Faction != Faction.OfPlayer)
            {
                string relation = settlement.Faction.Hidden
                    ? settlement.Faction.PlayerRelationKind.GetLabelCap()
                    : settlement.Faction.PlayerRelationKind.GetLabelCap() + " (" + settlement.Faction.PlayerGoodwill.ToStringWithSign() + ")";
                drawRow!("Relationship_Label".Translate(), relation);
            }
        }

        private static bool TryEnsureDrawDelegates()
        {
            if (delegatesInitialised)
            {
                return drawHeader != null && drawRow != null && drawDivider != null;
            }

            delegatesInitialised = true;
            MethodInfo? headerMi = AccessTools.Method(typeof(CellInspectorDrawer), "DrawHeader", new[] { typeof(string) });
            MethodInfo? rowMi = AccessTools.Method(typeof(CellInspectorDrawer), "DrawRow", new[] { typeof(string), typeof(string) });
            MethodInfo? dividerMi = AccessTools.Method(typeof(CellInspectorDrawer), "DrawDivider");

            if (headerMi != null)
            {
                drawHeader = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), headerMi);
            }

            if (rowMi != null)
            {
                drawRow = (Action<string, string>)Delegate.CreateDelegate(typeof(Action<string, string>), rowMi);
            }

            if (dividerMi != null)
            {
                drawDivider = (Action)Delegate.CreateDelegate(typeof(Action), dividerMi);
            }

            return drawHeader != null && drawRow != null && drawDivider != null;
        }
    }
}
