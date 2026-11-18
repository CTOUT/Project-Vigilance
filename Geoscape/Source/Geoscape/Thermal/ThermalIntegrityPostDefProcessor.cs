using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ProjectVigilance.Geoscape.Thermal;

/// <summary>
/// Programmatic post-def thermal integrity processor.
/// Executes once at game startup (<see cref="StaticConstructorOnStartup"/>) after all core, DLC, and mod XML defs are loaded.
/// Automatically applies realistic non-combustible thermal properties to metals, stone, and all-metal machinery/infrastructure.
/// Eliminates the need for dozens of fragile manual XML patches.
/// </summary>
[StaticConstructorOnStartup]
public static class ThermalIntegrityPostDefProcessor
{
    private static readonly HashSet<string> StructuralAndPreciousMetals = new(StringComparer.OrdinalIgnoreCase)
    {
        "Steel", "Plasteel", "Uranium", "Gold", "Silver"
    };

    private static readonly HashSet<string> CombustibleMaterialDefNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "WoodLog", "Chemfuel", "Cloth", "Hay", "Bioferrite", "Meat_Twisted", "Meat_Human", "Pemmican", "StrawMatting"
    };

    private static readonly HashSet<string> CombustibleStuffCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Woody", "Fabric", "Leathery", "Bioferrite"
    };

    private static readonly HashSet<string> ExplicitNonCombustibleItems = new(StringComparer.OrdinalIgnoreCase)
    {
        "ReinforcedBarrel", "SignalChip", "PowerfocusChip", "NanostructuringChip", "Gravcore", "GravlitePanel", "ShuttleEngine"
    };

    private static StatDef? cachedFlammabilityStat;
    private static ThingDef? cachedChemfuelDef;
    private static bool resolvedChemfuelDef;

    /// <summary>
    /// Resolves the Flammability StatDef safely across live game and headless test contexts.
    /// </summary>
    public static StatDef ResolveFlammabilityStat()
    {
        if (cachedFlammabilityStat != null) return cachedFlammabilityStat;

        try
        {
            cachedFlammabilityStat = StatDefOf.Flammability ?? DefDatabase<StatDef>.GetNamedSilentFail("Flammability");
        }
        catch
        {
            // Fallback for isolated headless test runner
        }

        if (cachedFlammabilityStat == null)
        {
            cachedFlammabilityStat = new StatDef { defName = "Flammability" };
        }

        return cachedFlammabilityStat;
    }

    static ThermalIntegrityPostDefProcessor()
    {
        ApplyThermalIntegrityAudit();
    }

    /// <summary>
    /// Executes the full thermal integrity audit pass across all loaded defs.
    /// Safe to call in headless test environments.
    /// </summary>
    public static void ApplyThermalIntegrityAudit()
    {
        if (GeoscapeMod.Settings != null && !GeoscapeMod.Settings.enableThermalIntegrityAudit)
        {
            return;
        }

        AuditTerrainBridges();
        AuditThingDefs();
    }

    /// <summary>
    /// Audits structural and precious metals, setting both raw item flammability and Stuff building factor to 0%.
    /// Kept for backwards compatibility and targeted unit tests.
    /// </summary>
    public static void AuditStructuralAndPreciousMetals()
    {
        var allThings = DefDatabase<ThingDef>.AllDefsListForReading;
        if (allThings == null || allThings.Count == 0) return;

        StatDef flam = ResolveFlammabilityStat();

        for (int i = 0; i < allThings.Count; i++)
        {
            var def = allThings[i];
            if (def != null && StructuralAndPreciousMetals.Contains(def.defName))
            {
                def.SetStatBaseValueSafe(flam, 0f);
                if (def.stuffProps != null)
                {
                    def.stuffProps.SetStatFactorSafe(flam, 0f);
                }
            }
        }
    }

    /// <summary>
    /// Audits foundation and bridge terrains: Core wooden bridge to 100%, heavy/ancient metal bridges and substructure to 0%.
    /// </summary>
    public static void AuditTerrainBridges()
    {
        var allTerrains = DefDatabase<TerrainDef>.AllDefsListForReading;
        if (allTerrains == null || allTerrains.Count == 0) return;

        StatDef flam = ResolveFlammabilityStat();

        for (int i = 0; i < allTerrains.Count; i++)
        {
            var def = allTerrains[i];
            if (def == null) continue;

            if (def.defName.Equals("Bridge", StringComparison.OrdinalIgnoreCase))
            {
                def.SetStatBaseValueSafe(flam, 1.0f);
            }
            else if (def.defName.Equals("HeavyBridge", StringComparison.OrdinalIgnoreCase) ||
                     def.defName.Equals("AncientHeavyBridge", StringComparison.OrdinalIgnoreCase) ||
                     def.defName.Equals("Substructure", StringComparison.OrdinalIgnoreCase))
            {
                def.SetStatBaseValueSafe(flam, 0f);
            }
        }
    }

    /// <summary>
    /// Audits all ThingDefs in a single high-performance linear pass: metals, explicit non-combustibles,
    /// archotech, bionics, gravship hull, and pure-metal buildings.
    /// </summary>
    public static void AuditThingDefs()
    {
        var allThings = DefDatabase<ThingDef>.AllDefsListForReading;
        if (allThings == null || allThings.Count == 0) return;

        for (int i = 0; i < allThings.Count; i++)
        {
            var def = allThings[i];
            if (def == null) continue;
            AuditSingleThingDef(def);
        }
    }

    /// <summary>
    /// Evaluates and audits a single ThingDef against thermal integrity rules.
    /// </summary>
    public static void AuditSingleThingDef(ThingDef def)
    {
        if (def == null) return;
        StatDef flam = ResolveFlammabilityStat();

        // 1. Structural and precious metals (raw resource + Stuff factor)
        if (StructuralAndPreciousMetals.Contains(def.defName))
        {
            def.SetStatBaseValueSafe(flam, 0f);
            if (def.stuffProps != null)
            {
                def.stuffProps.SetStatFactorSafe(flam, 0f);
            }
            return;
        }

        // 2. Explicit non-combustibles (artillery barrels, boss chips, spacer parts)
        if (ExplicitNonCombustibleItems.Contains(def.defName))
        {
            def.SetStatBaseValueSafe(flam, 0f);
            return;
        }

        // 3. Archotech artifacts and body parts
        if (IsArchotech(def))
        {
            def.SetStatBaseValueSafe(flam, 0f);
            return;
        }

        // 4. Bionics (spacer tech / plasteel limbs with no organic or combustible materials)
        if (IsPureBionic(def))
        {
            def.SetStatBaseValueSafe(flam, 0f);
            return;
        }

        // 5. Gravship Hull: Base flammability 1.0 so Stuff material (Bioferrite 75%, Steel 0%) governs
        if (def.defName.Equals("GravshipHull", StringComparison.OrdinalIgnoreCase))
        {
            def.SetStatBaseValueSafe(flam, 1.0f);
            return;
        }

        // 6. Fixed non-stuffable buildings made exclusively of metal/stone with no combustible fuel
        if (IsAllMetalOrStoneBuilding(def))
        {
            def.SetStatBaseValueSafe(flam, 0f);
        }
    }

    /// <summary>
    /// Determines whether a ThingDef is an Archotech device or body part.
    /// </summary>
    public static bool IsArchotech(ThingDef def)
    {
        if (def.techLevel == TechLevel.Archotech) return true;
        if (def.defName.StartsWith("Archotech", StringComparison.OrdinalIgnoreCase)) return true;
        if (def.thingCategories != null)
        {
            for (int i = 0; i < def.thingCategories.Count; i++)
            {
                if (def.thingCategories[i]?.defName.Equals("BodyPartsArchotech", StringComparison.OrdinalIgnoreCase) == true)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Determines whether a ThingDef is a bionic limb/implant made exclusively of non-combustible materials.
    /// </summary>
    public static bool IsPureBionic(ThingDef def)
    {
        if (def.thingCategories == null) return false;
        bool isBionic = false;
        for (int i = 0; i < def.thingCategories.Count; i++)
        {
            if (def.thingCategories[i]?.defName.Equals("BodyPartsBionic", StringComparison.OrdinalIgnoreCase) == true)
            {
                isBionic = true;
                break;
            }
        }
        if (!isBionic) return false;

        // If costList exists, verify no combustible components
        if (def.costList != null && def.costList.Count > 0)
        {
            return !HasCombustibleCost(def.costList);
        }

        return true;
    }

    /// <summary>
    /// Determines whether a building has a fixed recipe made exclusively of non-combustible materials (metals, stone, components)
    /// and does not store or consume volatile fuel (chemfuel, wood).
    /// </summary>
    public static bool IsAllMetalOrStoneBuilding(ThingDef def)
    {
        // Must be a building
        if (def.category != ThingCategory.Building && def.building == null) return false;

        // Do not alter stuffable buildings here (their material flammability factor already governs)
        if (def.costStuffCount > 0 || (def.stuffCategories != null && def.stuffCategories.Count > 0)) return false;

        // Must have defined construction costs
        if (def.costList == null || def.costList.Count == 0) return false;

        // Preserve Battery flammability (electrochemical thermal runaway)
        if (def.defName.Equals("Battery", StringComparison.OrdinalIgnoreCase)) return false;

        // Check if any cost ingredient is combustible
        if (HasCombustibleCost(def.costList)) return false;

        // Check if building stores or consumes volatile fuel
        if (StoresOrConsumesCombustibleFuel(def)) return false;

        return true;
    }

    /// <summary>
    /// Checks whether any ingredient in the cost list is a known combustible material.
    /// </summary>
    public static bool HasCombustibleCost(List<ThingDefCountClass> costList)
    {
        if (costList == null) return false;

        for (int i = 0; i < costList.Count; i++)
        {
            var cost = costList[i];
            if (cost?.thingDef == null) continue;
            string name = cost.thingDef.defName;

            if (CombustibleMaterialDefNames.Contains(name)) return true;
            if (name.StartsWith("Leather_", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.StartsWith("Wool", StringComparison.OrdinalIgnoreCase)) return true;

            // Check Stuff categories if the ingredient is itself stuff
            var categories = cost.thingDef.stuffProps?.categories;
            if (categories != null)
            {
                for (int c = 0; c < categories.Count; c++)
                {
                    var cat = categories[c];
                    if (cat != null && CombustibleStuffCategories.Contains(cat.defName))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether a building refuels with or stores combustible fuels (Chemfuel or Wood).
    /// </summary>
    public static bool StoresOrConsumesCombustibleFuel(ThingDef def)
    {
        if (def.comps == null) return false;

        if (!resolvedChemfuelDef)
        {
            resolvedChemfuelDef = true;
            try
            {
                cachedChemfuelDef = DefDatabase<ThingDef>.GetNamedSilentFail("Chemfuel");
            }
            catch
            {
            }
        }

        for (int i = 0; i < def.comps.Count; i++)
        {
            var comp = def.comps[i];
            if (comp is CompProperties_Refuelable refuelable && refuelable.fuelFilter != null)
            {
                if (refuelable.fuelFilter.Allows(ThingDefOf.WoodLog) ||
                    (cachedChemfuelDef != null && refuelable.fuelFilter.Allows(cachedChemfuelDef)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Sets or updates a stat base value on a BuildableDef safely with zero delegate allocation.
    /// </summary>
    public static void SetStatBaseValueSafe(this BuildableDef def, StatDef? stat, float value)
    {
        if (def == null) return;
        stat ??= ResolveFlammabilityStat();
        if (def.statBases == null)
        {
            def.statBases = new List<StatModifier>(4);
        }

        StatModifier? existing = null;
        for (int i = 0; i < def.statBases.Count; i++)
        {
            var m = def.statBases[i];
            if (m.stat == stat || (m.stat != null && m.stat.defName.Equals(stat.defName, StringComparison.OrdinalIgnoreCase)) || (m.stat == null && stat.defName.Equals("Flammability", StringComparison.OrdinalIgnoreCase)))
            {
                existing = m;
                break;
            }
        }

        if (existing != null)
        {
            existing.stat = stat;
            existing.value = value;
        }
        else
        {
            def.statBases.Add(new StatModifier { stat = stat, value = value });
        }
    }

    /// <summary>
    /// Sets or updates a stat factor on StuffProperties safely with zero delegate allocation.
    /// </summary>
    public static void SetStatFactorSafe(this StuffProperties stuffProps, StatDef? stat, float value)
    {
        if (stuffProps == null) return;
        stat ??= ResolveFlammabilityStat();
        if (stuffProps.statFactors == null)
        {
            stuffProps.statFactors = new List<StatModifier>(4);
        }

        StatModifier? existing = null;
        for (int i = 0; i < stuffProps.statFactors.Count; i++)
        {
            var m = stuffProps.statFactors[i];
            if (m.stat == stat || (m.stat != null && m.stat.defName.Equals(stat.defName, StringComparison.OrdinalIgnoreCase)) || (m.stat == null && stat.defName.Equals("Flammability", StringComparison.OrdinalIgnoreCase)))
            {
                existing = m;
                break;
            }
        }

        if (existing != null)
        {
            existing.stat = stat;
            existing.value = value;
        }
        else
        {
            stuffProps.statFactors.Add(new StatModifier { stat = stat, value = value });
        }
    }
}
