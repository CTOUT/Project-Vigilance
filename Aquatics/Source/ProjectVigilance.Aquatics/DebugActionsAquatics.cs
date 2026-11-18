using System.Linq;
using LudeonTK;
using ProjectVigilance.Core.Modules;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Developer Mode debug actions for deploying and validating coastal aquatic sonar infrastructure.
/// </summary>
public static class DebugActionsAquatics
{
    private static PlanetTile ResolveTargetTile()
    {
        if (WorldRendererUtility.WorldRendered)
        {
            bool isMouseObscured = Find.WindowStack != null && Find.WindowStack.MouseObscuredNow;
            if (!isMouseObscured)
            {
                PlanetTile mouseTile = GenWorld.MouseTile();
                if (mouseTile.Valid) return mouseTile;
            }

            if (Find.WorldSelector != null && Find.WorldSelector.SelectedTile.Valid)
            {
                return Find.WorldSelector.SelectedTile;
            }

            if (Find.WorldSelector?.SingleSelectedObject != null && Find.WorldSelector.SingleSelectedObject.Tile.Valid)
            {
                return Find.WorldSelector.SingleSelectedObject.Tile;
            }
        }

        if (!WorldRendererUtility.WorldRendered && Find.CurrentMap != null && Find.CurrentMap.Tile.Valid)
        {
            return Find.CurrentMap.Tile;
        }

        PlanetTile fallbackMouse = GenWorld.MouseTile();
        if (fallbackMouse.Valid) return fallbackMouse;

        return PlanetTile.Invalid;
    }

    [DebugAction("Project Vigilance", "Outpost: Deploy Sonar Station", actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
    private static void DeploySonarStation()
    {
        PlanetTile targetTile = ResolveTargetTile();
        if (!targetTile.Valid)
        {
            Messages.Message("Select a valid globe tile or enter game world.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        if (!SonarTraversalUtility.HasCoastalAccess(targetTile))
        {
            Messages.Message($"Note: Tile {targetTile} lacks coastal access (no adjacent water tiles). Sonar array will have zero acoustic propagation until water is adjacent.", MessageTypeDefOf.CautionInput, false);
        }

        if (VigilanceModuleManager.DeploySonarOutpostHook != null)
        {
            VigilanceModuleManager.DeploySonarOutpost(targetTile);
            return;
        }

        WorldObjectDef? def = DefDatabase<WorldObjectDef>.GetNamedSilentFail("Vigilance_ListeningPost_Sonar");
        if (def == null)
        {
            Messages.Message("Sonar outpost Def not loaded.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        WorldObject wo = WorldObjectMaker.MakeWorldObject(def);
        wo.Tile = targetTile;
        wo.SetFaction(Faction.OfPlayer);
        Find.WorldObjects.Add(wo);
        Messages.Message($"Deployed standalone Sonar Station marker at tile {targetTile}.", MessageTypeDefOf.PositiveEvent, false);
    }
}
