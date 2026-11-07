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
/// Replaces vanilla cycling and blind selection with an X-COM style contact disambiguation menu.
/// </summary>
[HarmonyPatch(typeof(WorldSelector), "SelectUnderMouse", typeof(bool))]
public static class WorldSelector_SelectUnderMouse_Patch
{
    public static bool Prefix(WorldSelector __instance, bool canSelectTile)
    {
        if (GeoscapeMod.Settings != null && !GeoscapeMod.Settings.enableOverlappingEntitySelectionMenu)
        {
            return true;
        }

        if (__instance == null)
        {
            return true;
        }

        List<WorldObject>? objects = null;
        try
        {
            objects = __instance.SelectableObjectsUnderMouse()
                .Where(WorldSelector_SelectFirstOrNextFrom_Patch.IsSelectableNowSafe)
                .Distinct()
                .ToList();
        }
        catch
        {
            // Headless test harness
        }

        if (objects != null && objects.Count > 1)
        {
            PlanetTile tile = WorldSelector_HandleWorldClicks_Patch.MouseTileOverride ?? GenWorld.MouseTile(false);
            bool shiftHeld = WorldSelector_SelectFirstOrNextFrom_Patch.IsShiftHeld();
            WorldSelector_SelectFirstOrNextFrom_Patch.OpenDisambiguationMenu(__instance, objects, tile, shiftHeld);
            return false;
        }

        return true;
    }
}

/// <summary>
/// Harmony patch fallback on SelectFirstOrNextFrom when invoked directly or by other callers.
/// </summary>
[HarmonyPatch(typeof(WorldSelector), "SelectFirstOrNextFrom", typeof(List<WorldObject>), typeof(PlanetTile))]
public static class WorldSelector_SelectFirstOrNextFrom_Patch
{
    [System.ThreadStatic]
    public static bool? ShiftHeldOverride;

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
            bool shiftHeld = IsShiftHeld();
            OpenDisambiguationMenu(__instance, distinct, tile, shiftHeld);
            return false;
        }

        return true;
    }

    public static bool IsSelectableNowSafe(WorldObject? obj)
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

    public static bool IsShiftHeld()
    {
        if (ShiftHeldOverride.HasValue)
        {
            return ShiftHeldOverride.Value;
        }

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

    public static void OpenDisambiguationMenu(WorldSelector? selector, List<WorldObject> objects, PlanetTile tile, bool shiftHeldOnOpen = false)
    {
        var options = BuildEntitySelectionOptions(objects, tile, shiftHeldOnOpen);
        if (options != null && options.Count > 0 && Find.WindowStack != null)
        {
            Find.WindowStack.Add(new FloatMenu(options, $"Contacts Detected (Tile {tile.tileId})"));
        }
    }

    public static List<FloatMenuOption> BuildEntitySelectionOptions(List<WorldObject> objects, PlanetTile tile, bool shiftHeldOnOpen = false)
    {
        var options = new List<FloatMenuOption>();
        if (objects == null || objects.Count == 0)
        {
            return options;
        }

        var distinct = objects.Where(IsSelectableNowSafe).Distinct().ToList();

        foreach (var obj in distinct)
        {
            bool isMasked = SensoryManager.ShouldMaskWorldObjectAsUnknown(obj);
            string label = ResolveEntityLabel(obj);

            Texture2D? icon = null;
            Color iconColor = Color.white;
            try
            {
                if (isMasked)
                {
                    icon = WorldObject_Visibility_Patch.GetUnknownSiteExpandingTexture();
                    iconColor = WorldObject_Visibility_Patch.UnknownSiteIconColor;
                }
                else
                {
                    icon = obj.ExpandingIcon;
                    if (obj.Faction != null)
                    {
                        iconColor = obj.Faction.Color;
                    }
                }
            }
            catch
            {
                // Headless test harness resilience
            }

            Action action = () =>
            {
                try
                {
                    var sel = Find.WorldSelector;
                    if (sel != null)
                    {
                        bool shift = shiftHeldOnOpen || IsShiftHeld();
                        if (shift)
                        {
                            if (sel.IsSelected(obj))
                            {
                                sel.Deselect(obj);
                            }
                            else
                            {
                                sel.Select(obj, playSound: true);
                            }
                        }
                        else
                        {
                            sel.ClearSelection();
                            sel.Select(obj, playSound: true);
                        }
                    }
                }
                catch
                {
                    // Headless test harness
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
            opt.iconColor = iconColor;

            options.Add(opt);
        }

        // Sector tile selection option
        if (tile.Valid)
        {
            string tileLabel = $"Select Tile {tile.tileId}";
            options.Add(new FloatMenuOption(tileLabel, () =>
            {
                try
                {
                    var sel = Find.WorldSelector;
                    if (sel != null)
                    {
                        bool shift = shiftHeldOnOpen || IsShiftHeld();
                        if (!shift)
                        {
                            sel.ClearSelection();
                        }
                        sel.SelectedTile = tile;
                    }
                }
                catch
                {
                    // Headless test harness
                }
            }, MenuOptionPriority.Low));
        }

        return options;
    }

    private static Map? GetSafeMap(Settlement? settlement)
    {
        if (settlement == null) return null;
        try
        {
            if (settlement.HasMap)
            {
                return settlement.Map;
            }
        }
        catch
        {
        }
        return null;
    }

    private static int GetCaravanPawnCount(Caravan caravan)
    {
        if (caravan == null) return 0;
        try
        {
            if (caravan.pawns != null)
            {
                return caravan.pawns.Count;
            }
        }
        catch
        {
        }
        try
        {
            return caravan.PawnsListForReading?.Count ?? 0;
        }
        catch
        {
        }
        return 0;
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
                int pawnCount = GetCaravanPawnCount(caravan);
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
                    Map? map = GetSafeMap(settlement);
                    if (map != null)
                    {
                        int colonistCount = map.mapPawns?.FreeColonistsCount ?? 0;
                        string colonistStr = colonistCount == 1 ? "1 colonist" : $"{colonistCount} colonists";
                        return $"{label} (Player Colony — {colonistStr})";
                    }
                    return $"{label} (Player Colony)";
                }
                return $"{label} ({settlement.Faction?.Name ?? "Settlement"})";
            }

            if (obj is WorldObject_ListeningPost post)
            {
                string statusDesc = post.IsSabotaged ? "Sabotaged" : (post.IsConstructing ? "Under Construction" : "Active");
                return $"{SafeLabelCap(post)} ({post.Kind} — {statusDesc})";
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
