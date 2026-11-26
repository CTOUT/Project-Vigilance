using System;
using System.Collections.Generic;
using System.Linq;
using ProjectVigilance.Core.Modules;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Core.Migration;

/// <summary>
/// WorldComponent tracking which Project Vigilance modules produced a savegame and managing safe
/// migration or retirement of orphaned module objects when loading pre-split or partial-suite saves.
/// </summary>
public class VigilanceMigrationTracker : WorldComponent
{
    public const int CurrentMigrationSchemaVersion = 1;

    private static readonly object pendingLock = new();
    private static readonly Dictionary<VigilanceModuleId, List<string>> pendingInterceptedEntities = new();

    private List<string> savedActiveModules = new();
    private int migrationSchemaVersion;
    private readonly Dictionary<VigilanceModuleId, List<string>> interceptedEntitiesByModule = new();
    private bool loadedFromSave;

    public IReadOnlyList<string> SavedActiveModules => savedActiveModules;
    public int MigrationSchemaVersion => migrationSchemaVersion;
    public IReadOnlyDictionary<VigilanceModuleId, List<string>> InterceptedEntitiesByModule => interceptedEntitiesByModule;
    public bool RequiresMigrationAdvisory { get; private set; }

    public IReadOnlyList<VigilanceModuleId> MissingModules
    {
        get
        {
            var missing = new List<VigilanceModuleId>();
            foreach (VigilanceModuleId id in interceptedEntitiesByModule.Keys)
            {
                if (!VigilanceModuleManager.IsModuleActive(id) && !missing.Contains(id))
                {
                    missing.Add(id);
                }
            }

            for (int i = 0; i < savedActiveModules.Count; i++)
            {
                var desc = VigilanceModuleManager.FindDescriptorByPackageId(savedActiveModules[i]);
                if (desc != null && !VigilanceModuleManager.IsModuleActive(desc.Id) && !missing.Contains(desc.Id))
                {
                    missing.Add(desc.Id);
                }
            }

            if (IsPreSplitLegacySave && missing.Count == 0)
            {
                if (!VigilanceModuleManager.IsModuleActive(VigilanceModuleId.Core)) missing.Add(VigilanceModuleId.Core);
                if (!VigilanceModuleManager.IsOrbitalActive) missing.Add(VigilanceModuleId.Orbital);
                if (!VigilanceModuleManager.IsAquaticsActive) missing.Add(VigilanceModuleId.Aquatics);
                if (!VigilanceModuleManager.IsThermalActive) missing.Add(VigilanceModuleId.Thermal);
            }
            else if (VigilanceModuleManager.IsCoreEmbeddedFallback &&
                     !VigilanceModuleManager.IsModuleActive(VigilanceModuleId.Core) &&
                     missing.Count > 0 &&
                     !missing.Contains(VigilanceModuleId.Core))
            {
                missing.Insert(0, VigilanceModuleId.Core);
            }

            return missing;
        }
    }

    public bool IsPreSplitLegacySave =>
        loadedFromSave &&
        migrationSchemaVersion == 0 &&
        savedActiveModules.Count == 0 &&
        (VigilanceModuleManager.HasPriorGeoscapeSaveDataHook?.Invoke() == true);

    public VigilanceMigrationTracker(RimWorld.Planet.World world) : base(world)
    {
    }

    public VigilanceMigrationTracker() : base(null!)
    {
    }

    public static void ClearPendingInterceptions()
    {
        lock (pendingLock)
        {
            pendingInterceptedEntities.Clear();
        }
    }

    public static void RecordInterceptedEntity(VigilanceModuleId moduleId, string entityIdentifier)
    {
        if (string.IsNullOrEmpty(entityIdentifier))
        {
            return;
        }

        lock (pendingLock)
        {
            if (!pendingInterceptedEntities.TryGetValue(moduleId, out var list))
            {
                list = new List<string>();
                pendingInterceptedEntities[moduleId] = list;
            }
            list.Add(entityIdentifier);
        }
    }

    public static Dictionary<VigilanceModuleId, List<string>> GetPendingInterceptionsSnapshot()
    {
        lock (pendingLock)
        {
            return pendingInterceptedEntities.ToDictionary(
                kvp => kvp.Key,
                kvp => new List<string>(kvp.Value));
        }
    }

    public override void FinalizeInit(bool fromLoad)
    {
        base.FinalizeInit(fromLoad);
        EvaluateMigrationState(fromLoad);

        if (RequiresMigrationAdvisory &&
            (Current.ProgramState == ProgramState.Playing || Current.ProgramState == ProgramState.MapInitializing))
        {
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                if (Current.ProgramState == ProgramState.Playing && Find.WindowStack != null && RequiresMigrationAdvisory)
                {
                    Find.WindowStack.Add(new Dialog_VigilanceModuleMigration(this));
                }
            });
        }
    }

    /// <summary>
    /// Evaluates absorbed Scribe stubs, previously saved module IDs, and pre-split save state.
    /// Exposed publicly for headless testing without requiring full RimWorld map loading.
    /// </summary>
    public void EvaluateMigrationState(bool fromLoad)
    {
        // Absorb any intercepted entities recorded during Scribe load (or pre-load hooks)
        lock (pendingLock)
        {
            foreach (var kvp in pendingInterceptedEntities)
            {
                if (!interceptedEntitiesByModule.TryGetValue(kvp.Key, out var targetList))
                {
                    targetList = new List<string>();
                    interceptedEntitiesByModule[kvp.Key] = targetList;
                }
                targetList.AddRange(kvp.Value);
            }
            pendingInterceptedEntities.Clear();
        }

        // Also scan active WorldObjects for any loaded MissingModuleWorldObjectStub instances
        if (Find.World?.worldObjects?.AllWorldObjects != null)
        {
            var all = Find.World.worldObjects.AllWorldObjects;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] is MissingModuleWorldObjectStub stub)
                {
                    VigilanceModuleId owner = InferModuleForStub(stub);
                    if (!VigilanceModuleManager.IsModuleActive(owner))
                    {
                        if (!interceptedEntitiesByModule.TryGetValue(owner, out var targetList))
                        {
                            targetList = new List<string>();
                            interceptedEntitiesByModule[owner] = targetList;
                        }
                        string label = stub.Callsign ?? stub.OriginalDefName ?? stub.def?.defName ?? "MissingModuleObject";
                        if (!targetList.Contains(label))
                        {
                            targetList.Add(label);
                        }
                    }
                }
            }
        }

        if (!fromLoad)
        {
            migrationSchemaVersion = CurrentMigrationSchemaVersion;
            savedActiveModules = VigilanceModuleManager.GetActiveModulePackageIds();
            RequiresMigrationAdvisory = false;
            return;
        }

        bool hasIntercepted = interceptedEntitiesByModule.Count > 0;
        bool hasMissingPreviouslySavedModule = false;
        for (int i = 0; i < savedActiveModules.Count; i++)
        {
            var desc = VigilanceModuleManager.FindDescriptorByPackageId(savedActiveModules[i]);
            if (desc != null && !VigilanceModuleManager.IsModuleActive(desc.Id))
            {
                hasMissingPreviouslySavedModule = true;
                break;
            }
        }

        bool legacySplitGap = IsPreSplitLegacySave &&
                              (!VigilanceModuleManager.IsModuleActive(VigilanceModuleId.Core) ||
                               !VigilanceModuleManager.IsOrbitalActive ||
                               !VigilanceModuleManager.IsAquaticsActive ||
                               !VigilanceModuleManager.IsThermalActive);

        RequiresMigrationAdvisory = hasIntercepted || hasMissingPreviouslySavedModule || legacySplitGap;

        if (!RequiresMigrationAdvisory)
        {
            migrationSchemaVersion = CurrentMigrationSchemaVersion;
            savedActiveModules = VigilanceModuleManager.GetActiveModulePackageIds();
        }
    }

    private static VigilanceModuleId InferModuleForStub(MissingModuleWorldObjectStub stub)
    {
        string orig = stub.OriginalDefName ?? stub.OriginalClassName ?? string.Empty;
        if (orig.IndexOf("Sonar", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return VigilanceModuleId.Aquatics;
        }
        return VigilanceModuleId.Orbital;
    }

    public void SetSavedActiveModulesForTesting(IEnumerable<string> packageIds, int schemaVersion = 1, bool markLoaded = true)
    {
        savedActiveModules = packageIds?.ToList() ?? new List<string>();
        migrationSchemaVersion = schemaVersion;
        loadedFromSave = markLoaded;
    }

    /// <summary>
    /// Cleanly destroys and removes all placeholder stubs from the world, resets intercepted state,
    /// and updates the saved module manifest to the currently active set.
    /// </summary>
    public int RetireMissingModuleObjects(IList<WorldObject>? worldObjectsOverride = null)
    {
        int removedCount = 0;
        IList<WorldObject>? all = worldObjectsOverride ?? Find.World?.worldObjects?.AllWorldObjects;

        if (all != null)
        {
            for (int i = all.Count - 1; i >= 0; i--)
            {
                WorldObject wo = all[i];
                bool isStub = wo is MissingModuleWorldObjectStub ||
                              wo?.def?.defName == BackCompatibilityConverter_VigilanceModular.MissingModuleWorldObjectDefName ||
                              (!VigilanceModuleManager.IsAquaticsActive && wo?.def?.defName == "Vigilance_ListeningPost_Sonar");

                if (isStub && wo != null)
                {
                    if (worldObjectsOverride != null)
                    {
                        all.RemoveAt(i);
                    }
                    else
                    {
                        try
                        {
                            if (wo.Spawned)
                            {
                                Find.WorldObjects?.Remove(wo);
                            }
                            if (!wo.Destroyed)
                            {
                                wo.Destroy();
                            }
                        }
                        catch
                        {
                            all.RemoveAt(i);
                        }
                    }
                    removedCount++;
                }
            }
        }

        if (worldObjectsOverride == null)
        {
            Find.World?.components?.RemoveAll(c => c is DeprecatedWorldComponentStub);
            Current.Game?.components?.RemoveAll(c => c is DeprecatedGameComponentStub);
        }

        interceptedEntitiesByModule.Clear();
        ClearPendingInterceptions();
        migrationSchemaVersion = CurrentMigrationSchemaVersion;
        savedActiveModules = VigilanceModuleManager.GetActiveModulePackageIds();
        RequiresMigrationAdvisory = false;

        VigilanceModuleManager.RecordModuleMigrationHook?.Invoke($"Retired {removedCount} missing-module objects; active: {string.Join(", ", savedActiveModules)}");
        VigilanceModuleManager.OnMigrationResolved?.Invoke();

        return removedCount;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref migrationSchemaVersion, "migrationSchemaVersion", 0);
        Scribe_Collections.Look(ref savedActiveModules, "activeVigilanceModules", LookMode.Value);

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            loadedFromSave = true;
            savedActiveModules ??= new List<string>();
        }
    }
}
