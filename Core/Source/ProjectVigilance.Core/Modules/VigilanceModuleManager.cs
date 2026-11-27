using System;
using System.Collections.Generic;
using ProjectVigilance.Geoscape.Sensory;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Core.Modules;

/// <summary>
/// Canonical identifiers for the separable Project Vigilance suite modules.
/// </summary>
public enum VigilanceModuleId
{
    Core = 0,
    Geoscape = 1,
    Orbital = 2,
    Aquatics = 3,
    Thermal = 4
}

/// <summary>
/// Metadata describing an individual Project Vigilance module package.
/// </summary>
public sealed class VigilanceModuleDescriptor
{
    public VigilanceModuleId Id { get; init; }
    public string PackageId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string WorkshopUrl { get; init; } = string.Empty;
}

/// <summary>
/// Discovers active Project Vigilance sub-modules at runtime and hosts decoupled cross-module
/// functional delegates so modules remain strictly independent at compile time.
/// </summary>
public static class VigilanceModuleManager
{
    private static readonly object syncLock = new();
    private static readonly HashSet<VigilanceModuleId> registeredModules = new();
    private static readonly Dictionary<VigilanceModuleId, bool> testModuleOverrides = new();
    private static bool coreEmbeddedFallback;
    private static bool? testCoreEmbeddedFallbackOverride;

    private static readonly IReadOnlyList<VigilanceModuleDescriptor> AllDescriptors = new[]
    {
        new VigilanceModuleDescriptor
        {
            Id = VigilanceModuleId.Core,
            PackageId = "ProjectVigilance.Core",
            DisplayName = "Project Vigilance - Core Framework",
            Summary = "Essential foundation contracts, orbital kinematics, unified mod options host, and migration scrubber.",
            WorkshopUrl = "https://steamcommunity.com/app/294100/workshop/"
        },
        new VigilanceModuleDescriptor
        {
            Id = VigilanceModuleId.Geoscape,
            PackageId = "ProjectVigilance.Geoscape",
            DisplayName = "Project Vigilance - Geoscape & Early Warning",
            Summary = "Overland incursions, tactical interceptions, 3-tier Haze of War, ground radar networks, and listening posts.",
            WorkshopUrl = "https://steamcommunity.com/sharedfiles/filedetails/?id=3822378226"
        },
        new VigilanceModuleDescriptor
        {
            Id = VigilanceModuleId.Orbital,
            PackageId = "ProjectVigilance.Orbital",
            DisplayName = "Project Vigilance - Orbital Logistics & Satellites",
            Summary = "Orbital Logistics Hubs and drifting Trade, Recon, Navigation, Communication, and Military satellites.",
            WorkshopUrl = "https://steamcommunity.com/app/294100/workshop/"
        },
        new VigilanceModuleDescriptor
        {
            Id = VigilanceModuleId.Aquatics,
            PackageId = "ProjectVigilance.Aquatics",
            DisplayName = "Project Vigilance - Aquatic Operations & Sonar",
            Summary = "Coastal Sonar Stations, Automated Sonar Outposts, and directional water-connected acoustic detection.",
            WorkshopUrl = "https://steamcommunity.com/app/294100/workshop/"
        },
        new VigilanceModuleDescriptor
        {
            Id = VigilanceModuleId.Thermal,
            PackageId = "ProjectVigilance.Thermal",
            DisplayName = "Project Vigilance - Thermal Integrity & Materials",
            Summary = "Enforces realistic non-combustible flammability across metals, stone, heavy bridges, machinery, and gravship hulls.",
            WorkshopUrl = "https://steamcommunity.com/app/294100/workshop/"
        }
    };

    public static IReadOnlyList<VigilanceModuleDescriptor> Modules => AllDescriptors;

    /// <summary>
    /// Allows headless unit tests to force-enable coastal sonar UI gating without requiring full RimWorld mod initialization.
    /// </summary>
    public static bool AllowSonarInTests { get; set; }

    // =========================================================================
    // Cross-Module Soft Hooks (Zero Hard Compile-Time Dependencies)
    // =========================================================================

    public static Func<WorldObject, bool>? IsOrbitalEntityDetectedHook { get; set; }
    public static Func<PlanetTile, PlanetTile, int, int>? DistanceCalculationOverrideHook { get; set; }
    public static Func<PlanetTile, bool>? HasNavigationCoverageHook { get; set; }
    public static Func<VisibilityMode>? GetVisibilityModeHook { get; set; }
    public static Func<Map, PlanetTile, bool>? HasGroundRadarCoveringTileHook { get; set; }
    public static Func<int>? GetSensorySweepRevisionHook { get; set; }
    public static Func<Map, float>? GetMaxGroundRadarRangeHook { get; set; }
    public static Action<ISensorProvider>? RegisterDynamicSensorHook { get; set; }
    public static Action<ISensorProvider>? DeregisterDynamicSensorHook { get; set; }
    public static Action? PerformSensorySweepHook { get; set; }
    public static Action<PlanetTile>? DeploySonarOutpostHook { get; set; }
    public static Func<bool>? IsThermalAuditEnabledHook { get; set; }
    public static Action<Rect>? DoUnifiedSettingsWindowContentsHook { get; set; }
    public static Func<bool>? HasPriorGeoscapeSaveDataHook { get; set; }
    public static Action<string>? RecordModuleMigrationHook { get; set; }

    public static bool IsAquaticsActive => IsModuleActive(VigilanceModuleId.Aquatics);
    public static bool IsOrbitalActive => IsModuleActive(VigilanceModuleId.Orbital);
    public static bool IsGeoscapeActive => IsModuleActive(VigilanceModuleId.Geoscape);
    public static bool IsThermalActive => IsModuleActive(VigilanceModuleId.Thermal);

    public static bool IsCoreEmbeddedFallback
    {
        get
        {
            lock (syncLock)
            {
                return testCoreEmbeddedFallbackOverride ?? coreEmbeddedFallback;
            }
        }
    }

    public static void SetCoreEmbeddedFallback(bool isEmbedded)
    {
        lock (syncLock)
        {
            coreEmbeddedFallback = isEmbedded;
        }
    }

    public static void SetCoreEmbeddedFallbackOverride(bool? isEmbedded)
    {
        lock (syncLock)
        {
            testCoreEmbeddedFallbackOverride = isEmbedded;
        }
    }

    public static void RegisterActiveModule(VigilanceModuleId id)
    {
        lock (syncLock)
        {
            registeredModules.Add(id);
        }
    }

    public static void SetModuleActiveOverride(VigilanceModuleId? id, bool? active)
    {
        lock (syncLock)
        {
            if (id == null)
            {
                testModuleOverrides.Clear();
                return;
            }

            if (active.HasValue)
            {
                testModuleOverrides[id.Value] = active.Value;
            }
            else
            {
                testModuleOverrides.Remove(id.Value);
            }
        }
    }

    public static void ResetForTesting()
    {
        lock (syncLock)
        {
            testModuleOverrides.Clear();
            coreEmbeddedFallback = false;
            testCoreEmbeddedFallbackOverride = null;
        }
        AllowSonarInTests = false;
        DistanceCalculationOverrideHook = null;
        GetSensorySweepRevisionHook = null;
        GetMaxGroundRadarRangeHook = null;
        HasPriorGeoscapeSaveDataHook = null;
        RecordModuleMigrationHook = null;
        ProjectVigilance.Geoscape.Sensory.SonarGatingUtility.DevModeOverrideForTesting = null;
    }

    public static bool IsModuleActive(VigilanceModuleId id)
    {
        lock (syncLock)
        {
            if (testModuleOverrides.TryGetValue(id, out bool overrideVal))
            {
                return overrideVal;
            }

            if (registeredModules.Contains(id))
            {
                return true;
            }
        }

        VigilanceModuleDescriptor descriptor = GetDescriptor(id);
        try
        {
            return ModLister.GetActiveModWithIdentifier(descriptor.PackageId, ignorePostfix: true) != null;
        }
        catch
        {
            return false;
        }
    }

    public static List<string> GetActiveModulePackageIds()
    {
        var result = new List<string>(AllDescriptors.Count);
        for (int i = 0; i < AllDescriptors.Count; i++)
        {
            if (IsModuleActive(AllDescriptors[i].Id))
            {
                result.Add(AllDescriptors[i].PackageId);
            }
        }
        return result;
    }

    public static VigilanceModuleDescriptor GetDescriptor(VigilanceModuleId id)
    {
        for (int i = 0; i < AllDescriptors.Count; i++)
        {
            if (AllDescriptors[i].Id == id)
            {
                return AllDescriptors[i];
            }
        }
        return AllDescriptors[0];
    }

    public static VigilanceModuleDescriptor? FindDescriptorByPackageId(string packageId)
    {
        for (int i = 0; i < AllDescriptors.Count; i++)
        {
            if (string.Equals(AllDescriptors[i].PackageId, packageId, StringComparison.OrdinalIgnoreCase))
            {
                return AllDescriptors[i];
            }
        }
        return null;
    }

    // =========================================================================
    // Soft Hook Invocation Helpers
    // =========================================================================

    public static bool IsOrbitalEntityDetected(WorldObject wo)
    {
        if (IsOrbitalEntityDetectedHook != null)
        {
            return IsOrbitalEntityDetectedHook(wo);
        }

        // Default conservative fallback when Orbital hooks are absent
        if (wo?.Faction?.IsPlayer == true)
        {
            return true;
        }
        return GetEffectiveVisibilityMode() == VisibilityMode.Omniscient;
    }

    public static bool HasNavigationCoverage(PlanetTile tile) =>
        HasNavigationCoverageHook != null && HasNavigationCoverageHook(tile);

    public static VisibilityMode GetEffectiveVisibilityMode() =>
        GetVisibilityModeHook?.Invoke() ?? VisibilityMode.Omniscient;

    public static bool HasGroundRadarCoveringTile(Map map, PlanetTile targetTile) =>
        HasGroundRadarCoveringTileHook != null && HasGroundRadarCoveringTileHook(map, targetTile);

    public static int GetSensorySweepRevision() =>
        GetSensorySweepRevisionHook?.Invoke() ?? 0;

    public static float GetMaxGroundRadarRange(Map map) =>
        GetMaxGroundRadarRangeHook?.Invoke(map) ?? 0f;

    public static void RegisterDynamicSensor(ISensorProvider sensor) =>
        RegisterDynamicSensorHook?.Invoke(sensor);

    public static void DeregisterDynamicSensor(ISensorProvider sensor) =>
        DeregisterDynamicSensorHook?.Invoke(sensor);

    public static void PerformSensorySweep() =>
        PerformSensorySweepHook?.Invoke();

    public static void DeploySonarOutpost(PlanetTile tile) =>
        DeploySonarOutpostHook?.Invoke(tile);

    public static bool IsThermalAuditEnabled() =>
        IsThermalAuditEnabledHook?.Invoke() ?? true;
}
