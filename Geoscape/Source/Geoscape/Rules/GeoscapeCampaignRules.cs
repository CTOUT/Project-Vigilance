using System;
using System.Collections.Generic;
using ProjectVigilance.Geoscape.Sensory;
using ProjectVigilance.Geoscape.World;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.Rules;

/// <summary>
/// Identifies the initialisation origin and provenance of a campaign.
/// </summary>
public enum CampaignEntryPath
{
    /// <summary>Started fresh under Project Vigilance 1.3+ on a new world.</summary>
    NewCampaign,

    /// <summary>Active governed save restored from disk (schemaVersion >= 1).</summary>
    ActiveSave,

    /// <summary>Pre-1.3 Project Vigilance save upgraded to campaign rules framework.</summary>
    LegacyMigration,

    /// <summary>Established non-Vigilance save adopting Geoscape systems for the first time.</summary>
    ModAdoption
}

/// <summary>
/// Authoritative governance and integrity status of an active campaign save.
/// </summary>
public enum CampaignIntegrityStatus
{
    /// <summary>Active rules match replayed audit ledger history perfectly.</summary>
    Governed,

    /// <summary>Campaign is within the initial 1-day review grace window.</summary>
    ReviewActive,

    /// <summary>Developer Mode unsafe override has been activated or used.</summary>
    DeveloperOverride,

    /// <summary>Active rules diverge from audit ledger without recorded authorization.</summary>
    ExternallyModified
}

/// <summary>
/// WorldComponent holding authoritative campaign rules, audit ledger, and legacy migration lifecycle for the active save.
/// </summary>
public sealed class GeoscapeCampaignRules : WorldComponent
{
    public const int CurrentSchemaVersion = 1;
    public const int ReviewWindowTicks = GenDate.TicksPerDay; // 60,000 ticks (1 in-game day)
    public const int MaxLedgerEntries = 100;

    private GeoscapeRuleSet rules = new();
    private List<RuleChangeRecord> ledger = new();
    private int schemaVersion;
    private int reviewWindowEndsAtTick = -1;

    // Transient lifecycle flags
    private bool loadedFromSave;
    private bool initialised;
    private string? pendingCreationSource;

    public bool LoadedFromSave
    {
        get => loadedFromSave;
        internal set => loadedFromSave = value;
    }

    public GeoscapeRuleSet Rules => rules;
    public IReadOnlyList<RuleChangeRecord> Ledger => ledger;
    public int SchemaVersion => schemaVersion;
    public int ReviewWindowEndsAtTick => reviewWindowEndsAtTick;

    public bool HasExternalModifications => ledger.Any(r => r.kind == RuleChangeKind.ExternalModification);

    public CampaignIntegrityStatus IntegrityStatus
    {
        get
        {
            if (HasExternalModifications)
            {
                return CampaignIntegrityStatus.ExternallyModified;
            }

            if (ledger.Any(r => r.kind == RuleChangeKind.UnsafeChange) || UnsafeRuleOverride.Effective)
            {
                return CampaignIntegrityStatus.DeveloperOverride;
            }

            if (ReviewWindowOpen)
            {
                return CampaignIntegrityStatus.ReviewActive;
            }

            return CampaignIntegrityStatus.Governed;
        }
    }

    public string IntegrityStatusLabel => IntegrityStatus switch
    {
        CampaignIntegrityStatus.Governed => "Governed & Verified",
        CampaignIntegrityStatus.ReviewActive => "Review Period Active",
        CampaignIntegrityStatus.DeveloperOverride => "Developer Override Used",
        CampaignIntegrityStatus.ExternallyModified => "External Modification Detected",
        _ => "Unknown"
    };

    [ThreadStatic]
    private static GeoscapeCampaignRules? testInstance;

    public static GeoscapeCampaignRules? TestInstance
    {
        get => testInstance;
        set => testInstance = value;
    }

    [ThreadStatic]
    private static Func<int>? currentTickOverride;

    public static Func<int>? CurrentTickOverride
    {
        get => currentTickOverride;
        set => currentTickOverride = value;
    }

    private int CurrentTick
    {
        get
        {
            if (currentTickOverride != null)
            {
                return currentTickOverride();
            }

            if (Current.Game != null && Find.TickManager != null)
            {
                return Find.TickManager.TicksGame;
            }

            return 0;
        }
    }

    private static GeoscapeCampaignRules? cached;

    public static GeoscapeCampaignRules? Active
    {
        get
        {
            if (testInstance != null)
            {
                testInstance.EnsureInitialised();
                return testInstance;
            }

            RimWorld.Planet.World? world = Find.World;
            if (world == null)
            {
                return null;
            }

            if (cached == null || cached.world != world)
            {
                cached = world.GetComponent<GeoscapeCampaignRules>();
            }

            cached?.EnsureInitialised();
            return cached;
        }
    }

    public GeoscapeCampaignRules(RimWorld.Planet.World world) : base(world)
    {
    }

    /// <summary>
    /// Parameterless constructor for headless unit test harnesses and reflection deserialisation.
    /// </summary>
    public GeoscapeCampaignRules() : base(null!)
    {
        schemaVersion = CurrentSchemaVersion;
        initialised = true;
    }

    /// <summary>
    /// Pure helper determining snapshot source: draft is consumed only during world generation.
    /// </summary>
    public static (GeoscapeRuleSet? Draft, GeoscapeSettings Defaults, string Label) SelectSnapshotSource(
        bool isWorldGeneration,
        GeoscapeRuleSet? draft,
        GeoscapeSettings defaults)
    {
        if (isWorldGeneration && draft != null)
        {
            return (draft, defaults, "World generation page");
        }

        return (null, defaults, "Defaults");
    }

    private void EnsureInitialised()
    {
        if (initialised)
        {
            return;
        }

        initialised = true;

        if (schemaVersion == 0)
        {
            bool isWorldGen = Current.CreatingWorld != null;
            var source = SelectSnapshotSource(isWorldGen, NewCampaignRuleDraft.Current, GeoscapeMod.Settings);

            if (source.Draft != null)
            {
                rules.CopyFrom(source.Draft);
            }
            else
            {
                rules.CopyFromDefaults(source.Defaults);
            }

            schemaVersion = CurrentSchemaVersion;
            pendingCreationSource = source.Label;
        }
    }

    public CampaignEntryPath DetermineEntryPath(bool fromLoad)
    {
        if (!fromLoad)
        {
            return CampaignEntryPath.NewCampaign;
        }

        if (loadedFromSave && schemaVersion >= CurrentSchemaVersion)
        {
            return CampaignEntryPath.ActiveSave;
        }

        bool hasPriorGeoscapeData = (SensoryManager.Instance?.LoadedFromSave == true) ||
                                    (ContactRegistry.Instance?.LoadedFromSave == true) ||
                                    (HazeReconnaissanceManager.Instance?.LoadedFromSave == true);
        return hasPriorGeoscapeData ? CampaignEntryPath.LegacyMigration : CampaignEntryPath.ModAdoption;
    }

    public override void FinalizeInit(bool fromLoad)
    {
        base.FinalizeInit(fromLoad);

        EnsureInitialised();

        CampaignEntryPath path = DetermineEntryPath(fromLoad);
        switch (path)
        {
            case CampaignEntryPath.NewCampaign:
                string source = pendingCreationSource ?? "Defaults";
                string snapshot = rules.CreateSnapshotString();
                Record(RuleChangeKind.CampaignCreated, note: $"{source} | {snapshot}");
                NewCampaignRuleDraft.Clear();
                break;

            case CampaignEntryPath.LegacyMigration:
                BeginLegacyMigration();
                break;

            case CampaignEntryPath.ModAdoption:
                BeginModAdoption();
                break;

            case CampaignEntryPath.ActiveSave:
                ReconcileLedgerOnLoad();
                break;
        }

        UnsafeRuleOverride.Notify_CampaignActivated(this, fromLoad);
    }

    /// <summary>
    /// Replays historical ledger transitions against the pinned Day-0 baseline to verify save file integrity.
    /// If direct external modifications to the XML are detected, logs an audit record detailing the discrepancy.
    /// </summary>
    public void ReconcileLedgerOnLoad()
    {
        RuleChangeRecord? pinned = ledger.FirstOrDefault(r => r.IsPinned);
        if (pinned == null || string.IsNullOrEmpty(pinned.note))
        {
            return;
        }

        int separatorIdx = pinned.note.IndexOf(" | ", StringComparison.Ordinal);
        if (separatorIdx < 0)
        {
            return;
        }

        string snapshotStr = pinned.note.Substring(separatorIdx + 3);
        var expectedValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Parse key=value tokens
        string[] tokens = snapshotStr.Split(new[] { "; " }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string token in tokens)
        {
            int eqIdx = token.IndexOf('=');
            if (eqIdx > 0 && eqIdx < token.Length - 1)
            {
                string k = token.Substring(0, eqIdx).Trim();
                string v = token.Substring(eqIdx + 1).Trim();
                expectedValues[k] = v;
            }
        }

        // Replay subsequent legitimate transitions
        for (int i = 0; i < ledger.Count; i++)
        {
            RuleChangeRecord r = ledger[i];
            if (r.IsPinned)
            {
                continue;
            }

            if (r.kind is RuleChangeKind.ReviewWindowEdit or RuleChangeKind.Escalation or RuleChangeKind.UnsafeChange)
            {
                if (!string.IsNullOrEmpty(r.ruleKey) && r.to != null)
                {
                    expectedValues[r.ruleKey] = r.to;
                }
            }
        }

        // Compare expected vs live rules
        var diffs = new List<string>();
        foreach (ICampaignRule rule in rules.All)
        {
            if (expectedValues.TryGetValue(rule.Key, out string? expectedVal))
            {
                if (!string.Equals(rule.CurrentValueLabel, expectedVal, StringComparison.OrdinalIgnoreCase))
                {
                    diffs.Add($"{rule.Key} (expected '{expectedVal}', active '{rule.CurrentValueLabel}')");
                }
            }
        }

        if (diffs.Count > 0)
        {
            string diffNote = $"Direct save modification detected: {string.Join(", ", diffs)}";
            RuleChangeRecord? lastMod = ledger.LastOrDefault(r => r.kind == RuleChangeKind.ExternalModification);
            if (lastMod == null || lastMod.note != diffNote)
            {
                Record(RuleChangeKind.ExternalModification, note: diffNote);
                Log.Warning($"[Project Vigilance] Savegame integrity discrepancy: {diffNote}");
            }
        }
    }

    private void BeginModAdoption()
    {
        reviewWindowEndsAtTick = CurrentTick + ReviewWindowTicks;
        schemaVersion = CurrentSchemaVersion;

        LongEventHandler.ExecuteWhenFinished(() =>
        {
            if (Current.ProgramState != ProgramState.Playing || Find.WindowStack == null)
            {
                return;
            }

            Find.WindowStack.Add(new Dialog_GeoscapeModAdoption(this));
        });
    }

    public void ApplyAdoptionProfile(CampaignProfile profile, string source)
    {
        rules.ApplyProfile(profile);

        RuleTransitionEffects effects = RuleTransitionEffects.RebuildWorldLayers | RuleTransitionEffects.RescanSensors;
        if (rules.Visibility.Value == VisibilityMode.ClassicHazeOfWar)
        {
            effects |= RuleTransitionEffects.RepathPlayerCaravans;
        }
        else
        {
            effects |= RuleTransitionEffects.ReleaseConcealedSites;
        }

        RuleTransitionExecutor.Execute(effects);

        string snapshot = rules.CreateSnapshotString();
        Record(RuleChangeKind.ModAdoption, note: $"Profile: {profile.GetDisplayName()} ({source}) | {snapshot}");
    }

    public void ApplyAdoptionCustom(string source)
    {
        RuleTransitionEffects effects = RuleTransitionEffects.RebuildWorldLayers | RuleTransitionEffects.RescanSensors;
        if (rules.Visibility.Value == VisibilityMode.ClassicHazeOfWar)
        {
            effects |= RuleTransitionEffects.RepathPlayerCaravans;
        }

        RuleTransitionExecutor.Execute(effects);

        string snapshot = rules.CreateSnapshotString();
        Record(RuleChangeKind.ModAdoption, note: $"Profile: Custom ({source}) | {snapshot}");
    }

    public bool HasRecordedAdoption => ledger.Any(r => r.kind == RuleChangeKind.ModAdoption);

    private void BeginLegacyMigration()
    {
        reviewWindowEndsAtTick = CurrentTick + ReviewWindowTicks;
        string snapshot = rules.CreateSnapshotString();
        Record(RuleChangeKind.LegacyMigration, note: $"Adopted ModSettings defaults | {snapshot}");

        LongEventHandler.ExecuteWhenFinished(() =>
        {
            if (Current.ProgramState != ProgramState.Playing || Find.LetterStack == null)
            {
                return;
            }

            bool hasHazeData = SensoryManager.Instance?.HasAnyExploredTiles == true;
            bool notHaze = rules.Visibility.Value != VisibilityMode.ClassicHazeOfWar;

            string title = "Project Vigilance: Campaign Rules Bound to Save";
            string text = "Project Vigilance now stores gameplay-affecting campaign rules directly within your save file to prevent cross-campaign contamination.\n\n" +
                          "Your current Mod Settings defaults have been adopted for this campaign.\n\n" +
                          "A 1-day review window is currently open, allowing you to freely adjust and lock in your desired rules under Mod Settings.\n\n" +
                          $"Initial rules: {rules.CreateSnapshotString()}";

            if (hasHazeData && notHaze)
            {
                text += "\n\nNotice: This save contains Classic Haze of War exploration data and may have been created under Classic Haze of War. You may wish to re-enable Classic Haze of War before locking rules.";
            }

            Find.LetterStack.ReceiveLetter(title, text, LetterDefOf.NeutralEvent);
        });
    }

    public bool ReviewWindowOpen
    {
        get
        {
            if (reviewWindowEndsAtTick > 0)
            {
                if (CurrentTick < reviewWindowEndsAtTick)
                {
                    return true;
                }

                reviewWindowEndsAtTick = -1;
                Record(RuleChangeKind.ReviewWindowLocked, note: "Review window expired");
                return false;
            }

            return false;
        }
    }

    public void ConfirmAndLock()
    {
        if (reviewWindowEndsAtTick > 0)
        {
            reviewWindowEndsAtTick = -1;
            Record(RuleChangeKind.ReviewWindowLocked, note: "Confirmed by player");
        }
    }

    public RuleEditContext CreateEditContext()
    {
        return new RuleEditContext(
            CampaignActive: Current.ProgramState == ProgramState.Playing,
            ReviewWindowOpen: ReviewWindowOpen,
            UnsafeOverrideEnabled: UnsafeRuleOverride.Effective);
    }

    public bool TryApply<T>(CampaignRule<T> rule, T proposed, out RuleTransitionVerdict verdict) where T : notnull
    {
        RuleEditContext ctx = CreateEditContext();
        verdict = rule.Evaluate(proposed, ctx);

        if (verdict == RuleTransitionVerdict.Allowed)
        {
            string fromLabel = rule.CurrentValueLabel;
            rule.SetUnchecked(proposed);
            string toLabel = rule.CurrentValueLabel;

            if (ReviewWindowOpen)
            {
                Record(RuleChangeKind.ReviewWindowEdit, rule.Key, fromLabel, toLabel);
            }
            else if (rule.Mutability != RuleMutability.Mutable)
            {
                Record(RuleChangeKind.Escalation, rule.Key, fromLabel, toLabel);
            }

            RuleTransitionExecutor.Execute(rule.Effects);
            return true;
        }

        return false;
    }

    public void ForceApplyEscalation<T>(CampaignRule<T> rule, T proposed) where T : notnull
    {
        string fromLabel = rule.CurrentValueLabel;
        rule.SetUnchecked(proposed);
        string toLabel = rule.CurrentValueLabel;

        Record(RuleChangeKind.Escalation, rule.Key, fromLabel, toLabel);
        RuleTransitionExecutor.Execute(rule.Effects);
    }

    public void ForceApplyUnsafe<T>(CampaignRule<T> rule, T proposed) where T : notnull
    {
        string fromLabel = rule.CurrentValueLabel;
        rule.SetUnchecked(proposed);
        string toLabel = rule.CurrentValueLabel;

        Record(RuleChangeKind.UnsafeChange, rule.Key, fromLabel, toLabel, note: "Developer override");
        RuleTransitionExecutor.Execute(rule.Effects);
    }

    public void Record(RuleChangeKind kind, string? ruleKey = null, string? from = null, string? to = null, string? note = null)
    {
        int now = CurrentTick;

        // Merge repeated edits to the same key within the same tick (e.g. dev-mode slider drags)
        if (ledger.Count > 0 && !string.IsNullOrEmpty(ruleKey))
        {
            RuleChangeRecord last = ledger[ledger.Count - 1];
            if (last.tick == now && last.kind == kind && last.ruleKey == ruleKey && !last.IsPinned)
            {
                last.to = to;
                if (!string.IsNullOrEmpty(note))
                {
                    last.note = note;
                }
                return;
            }
        }

        ledger.Add(new RuleChangeRecord(now, kind, ruleKey, from, to, note));

        // Evict oldest non-pinned record if capacity exceeded
        if (ledger.Count > MaxLedgerEntries)
        {
            for (int i = 0; i < ledger.Count; i++)
            {
                if (!ledger[i].IsPinned)
                {
                    ledger.RemoveAt(i);
                    break;
                }
            }
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0, forceSave: true);
        Scribe_Deep.Look(ref rules, "rules");
        Scribe_Values.Look(ref reviewWindowEndsAtTick, "reviewWindowEndsAtTick", -1);
        Scribe_Collections.Look(ref ledger, "ruleChangeLedger", LookMode.Deep);

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            loadedFromSave = true;
            rules ??= new GeoscapeRuleSet();
            ledger ??= new List<RuleChangeRecord>();
        }
    }

    public static void ResetStaticDataForTesting()
    {
        testInstance = null;
        cached = null;
        currentTickOverride = null;
    }
}
