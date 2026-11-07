using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using ProjectVigilance.Geoscape.Sensory;
using ProjectVigilance.Geoscape.World;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Intercepts entity selection on the Geoscape when multiple selectable entities overlap
/// (e.g. a caravan stationed at a settlement, or multiple caravans at the same coordinate).
/// Replaces invisible cycling with an X-COM style contact disambiguation menu.
/// </summary>
[HarmonyPatch(typeof(WorldSelector), "SelectFirstOrNextFrom", typeof(List<WorldObject>), typeof(PlanetTile))]
public static class WorldSelector_SelectFirstOrNextFrom_Patch
{
    public static bool Prefix(WorldSelector __instance, List<WorldObject> objects, PlanetTile tile)
    {
        if (GeoscapeMod.Settings != null && !GeoscapeMod.Settings.enableOverlappingEntitySelectionMenu)
        {
            return true;
        }

        if (objects == null || objects.Count == 0)
        {
            return true;
        }

        var distinct = objects.Where(IsSelectableNowSafe).Distinct().ToList();
        if (distinct.Count > 1)
        {
            OpenDisambiguationMenu(__instance, distinct, tile);
            return false;
        }

        return true;
    }

    private static bool IsSelectableNowSafe(WorldObject? obj)
    {
        if (obj == null) return false;
        try
        {
            return obj.SelectableNow;
        }
        catch
        {
            return true;
        }
    }

    private static bool IsShiftHeld()
    {
        try
        {
            if (KeyBindingDefOf.QueueOrder != null && KeyBindingDefOf.QueueOrder.IsDown)
            {
                return true;
            }
        }
        catch
        {
            // Headless test harness
        }

        try
        {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                return true;
            }
        }
        catch
        {
            // Headless test harness
        }

        return false;
    }

    public static void OpenDisambiguationMenu(WorldSelector selector, List<WorldObject> objects, PlanetTile tile)
    {
        var options = BuildEntitySelectionOptions(objects, tile);
        if (options != null && options.Count > 0 && Find.WindowStack != null)
        {
            Find.WindowStack.Add(new FloatMenu(options, $"Contacts Detected (Tile {tile.tileId})"));
        }
    }

    public static List<FloatMenuOption> BuildEntitySelectionOptions(List<WorldObject> objects, PlanetTile tile)
    {
        var options = new List<FloatMenuOption>();
        if (objects == null || objects.Count == 0)
        {
            return options;
        }

        var distinct = objects.Where(IsSelectableNowSafe).Distinct().ToList();

        foreach (var obj in distinct)
        {
            string label = ResolveEntityLabel(obj);

            Texture2D? icon = null;
            Color iconColor = Color.white;
            try
            {
                icon = obj.ExpandingIcon;
                if (obj.Faction != null)
                {
                    iconColor = obj.Faction.Color;
                }
            }
            catch
            {
                // Headless test harness resilience
            }

            Action action = () =>
            {
                if (Find.WorldSelector != null)
                {
                    if (IsShiftHeld())
                    {
                        if (Find.WorldSelector.IsSelected(obj))
                        {
                            Find.WorldSelector.Deselect(obj);
                        }
                        else
                        {
                            Find.WorldSelector.Select(obj, playSound: true);
                        }
                    }
                    else
                    {
                        Find.WorldSelector.ClearSelection();
                        Find.WorldSelector.Select(obj, playSound: true);
                    }
                }
            };

            FloatMenuOption opt;
            if (icon != null)
            {
                opt = new FloatMenuOption(label, action, icon, iconColor, MenuOptionPriority.Default);
            }
            else
            {
                opt = new FloatMenuOption(label, action, MenuOptionPriority.Default);
            }

            options.Add(opt);
        }

        // Sector tile selection option
        if (tile.Valid)
        {
            string tileLabel = $"Select Tile {tile.tileId}";
            options.Add(new FloatMenuOption(tileLabel, () =>
            {
                if (Find.WorldSelector != null)
                {
                    Find.WorldSelector.ClearSelection();
                    Find.WorldSelector.SelectedTile = tile;
                }
            }, MenuOptionPriority.Low));
        }

        return options;
    }

    public static string ResolveEntityLabel(WorldObject obj)
    {
        if (obj == null) return "Unknown";

        if (SensoryManager.ShouldMaskWorldObjectAsUnknown(obj))
        {
            return "Unidentified Contact";
        }

        try
        {
            if (obj is Caravan caravan)
            {
                int pawnCount = caravan.pawns?.Count ?? 0;
                string memberStr = pawnCount == 1 ? "1 member" : $"{pawnCount} members";
                string label = SafeLabelCap(caravan);
                if (caravan.Faction?.IsPlayer == true)
                {
                    string state = caravan.pather != null && caravan.pather.Moving ? "Moving" : "Stationed";
                    return $"{label} (Caravan — {memberStr}, {state})";
                }
                return $"{label} ({caravan.Faction?.Name ?? "Neutral"} Caravan — {memberStr})";
            }

            if (obj is Settlement settlement)
            {
                string label = SafeLabelCap(settlement);
                if (settlement.Faction?.IsPlayer == true)
                {
                    return $"{label} (Player Colony)";
                }
                return $"{label} ({settlement.Faction?.Name ?? "Settlement"})";
            }

            if (obj is WorldObject_ListeningPost post)
            {
                return $"{SafeLabelCap(post)} ({post.Kind})";
            }

            if (obj is WorldObject_Incursion incursion)
            {
                return SafeLabelCap(incursion);
            }

            if (obj.def != null)
            {
                return $"{SafeLabelCap(obj)} ({obj.def.label})";
            }

            return SafeLabelCap(obj);
        }
        catch
        {
            return obj.def?.label?.CapitalizeFirst() ?? "Contact";
        }
    }

    private static string SafeLabelCap(WorldObject obj)
    {
        try
        {
            var tag = obj.LabelCap;
            if (!tag.NullOrEmpty()) return tag.ToString();
        }
        catch
        {
        }

        try
        {
            if (!string.IsNullOrEmpty(obj.Label)) return obj.Label.CapitalizeFirst();
        }
        catch
        {
        }

        return obj.def?.label?.CapitalizeFirst() ?? "Contact";
    }
}
