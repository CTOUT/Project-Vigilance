using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape;
using ProjectVigilance.Geoscape.Patches;
using ProjectVigilance.Geoscape.Rules;
using ProjectVigilance.Geoscape.Sensory;
using ProjectVigilance.Geoscape.World;
using RimWorld;
using UnityEngine;
using Verse;

namespace Geoscape.Tests;

[TestClass]
public class CampaignRulesTests
{
    [TestInitialize]
    public void Setup()
    {
        GeoscapeCampaignRules.ResetStaticDataForTesting();
        UnsafeRuleOverride.ResetForTesting();
        NewCampaignRuleDraft.Clear();
        SensoryManager.Instance = null;
        ContactRegistry.Instance = null;
        HazeReconnaissanceManager.Instance = null;
    }

    [TestCleanup]
    public void Teardown()
    {
        GeoscapeCampaignRules.ResetStaticDataForTesting();
        UnsafeRuleOverride.ResetForTesting();
        NewCampaignRuleDraft.Clear();
        SensoryManager.Instance = null;
        ContactRegistry.Instance = null;
        HazeReconnaissanceManager.Instance = null;
    }

    // =========================================================================
    // 1. Framework Evaluation Tests
    // =========================================================================

    [TestMethod]
    public void Evaluate_MutableRule_OutsideCampaign_ReturnsAllowed()
    {
        GeoscapeRuleSet rules = new();
        RuleEditContext ctx = new(CampaignActive: false, ReviewWindowOpen: false, UnsafeOverrideEnabled: false);

        RuleTransitionVerdict verdict = rules.EarlyWarningLetters.Evaluate(false, ctx);

        Assert.AreEqual(RuleTransitionVerdict.Allowed, verdict);
    }

    [TestMethod]
    public void Evaluate_MutableRule_DuringCampaign_ReturnsAllowed()
    {
        GeoscapeRuleSet rules = new();
        RuleEditContext ctx = new(CampaignActive: true, ReviewWindowOpen: false, UnsafeOverrideEnabled: false);

        RuleTransitionVerdict verdict = rules.EarlyWarningLetters.Evaluate(false, ctx);

        Assert.AreEqual(RuleTransitionVerdict.Allowed, verdict);
    }

    [TestMethod]
    public void Evaluate_ImmutableRule_OutsideCampaign_ReturnsAllowed()
    {
        GeoscapeRuleSet rules = new();
        RuleEditContext ctx = new(CampaignActive: false, ReviewWindowOpen: false, UnsafeOverrideEnabled: false);

        RuleTransitionVerdict verdict = rules.ColonyScoutRadius.Evaluate(8.0f, ctx);

        Assert.AreEqual(RuleTransitionVerdict.Allowed, verdict);
    }

    [TestMethod]
    public void Evaluate_ImmutableRule_DuringCampaign_ReturnsBlocked()
    {
        GeoscapeRuleSet rules = new();
        RuleEditContext ctx = new(CampaignActive: true, ReviewWindowOpen: false, UnsafeOverrideEnabled: false);

        RuleTransitionVerdict verdict = rules.ColonyScoutRadius.Evaluate(8.0f, ctx);

        Assert.AreEqual(RuleTransitionVerdict.Blocked, verdict);
    }

    [TestMethod]
    public void Evaluate_ImmutableRule_ReviewWindow_ReturnsAllowed()
    {
        GeoscapeRuleSet rules = new();
        RuleEditContext ctx = new(CampaignActive: true, ReviewWindowOpen: true, UnsafeOverrideEnabled: false);

        RuleTransitionVerdict verdict = rules.ColonyScoutRadius.Evaluate(8.0f, ctx);

        Assert.AreEqual(RuleTransitionVerdict.Allowed, verdict);
    }

    [TestMethod]
    public void Evaluate_ImmutableRule_WithDevOverride_ReturnsRequiresUnsafeConfirmation()
    {
        GeoscapeRuleSet rules = new();
        RuleEditContext ctx = new(CampaignActive: true, ReviewWindowOpen: false, UnsafeOverrideEnabled: true);

        RuleTransitionVerdict verdict = rules.ColonyScoutRadius.Evaluate(8.0f, ctx);

        Assert.AreEqual(RuleTransitionVerdict.RequiresUnsafeConfirmation, verdict);
    }

    [TestMethod]
    public void Evaluate_EscalatableRule_EscalationRankIncrease_ReturnsRequiresEscalationConfirmation()
    {
        GeoscapeRuleSet rules = new();
        rules.Visibility.Value = VisibilityMode.ClassicHazeOfWar;
        RuleEditContext ctx = new(CampaignActive: true, ReviewWindowOpen: false, UnsafeOverrideEnabled: false);

        RuleTransitionVerdict verdict = rules.Visibility.Evaluate(VisibilityMode.PlanetarySurveillance, ctx);

        Assert.AreEqual(RuleTransitionVerdict.RequiresEscalationConfirmation, verdict);
    }

    [TestMethod]
    public void Evaluate_EscalatableRule_EscalationRankDecrease_ReturnsBlocked()
    {
        GeoscapeRuleSet rules = new();
        rules.Visibility.Value = VisibilityMode.PlanetarySurveillance;
        RuleEditContext ctx = new(CampaignActive: true, ReviewWindowOpen: false, UnsafeOverrideEnabled: false);

        RuleTransitionVerdict verdict = rules.Visibility.Evaluate(VisibilityMode.ClassicHazeOfWar, ctx);

        Assert.AreEqual(RuleTransitionVerdict.Blocked, verdict);
    }

    [TestMethod]
    public void Evaluate_EscalatableRule_EscalationRankDecrease_WithDevOverride_ReturnsRequiresUnsafeConfirmation()
    {
        GeoscapeRuleSet rules = new();
        rules.Visibility.Value = VisibilityMode.PlanetarySurveillance;
        RuleEditContext ctx = new(CampaignActive: true, ReviewWindowOpen: false, UnsafeOverrideEnabled: true);

        RuleTransitionVerdict verdict = rules.Visibility.Evaluate(VisibilityMode.ClassicHazeOfWar, ctx);

        Assert.AreEqual(RuleTransitionVerdict.RequiresUnsafeConfirmation, verdict);
    }

    [TestMethod]
    public void Evaluate_NoChange_ReturnsNoChange()
    {
        GeoscapeRuleSet rules = new();
        rules.Visibility.Value = VisibilityMode.ClassicHazeOfWar;
        RuleEditContext ctx = new(CampaignActive: true, ReviewWindowOpen: false, UnsafeOverrideEnabled: false);

        RuleTransitionVerdict verdict = rules.Visibility.Evaluate(VisibilityMode.ClassicHazeOfWar, ctx);

        Assert.AreEqual(RuleTransitionVerdict.NoChange, verdict);
    }

    // =========================================================================
    // 2. Save & Isolation Tests
    // =========================================================================

    [TestMethod]
    public void RuleSet_CopyFromDefaults_CopiesAllValues()
    {
        GeoscapeSettings settings = new()
        {
            visibilityMode = VisibilityMode.Omniscient,
            colonyScoutRadius = 8.5f,
            caravanScoutRadius = 4.2f,
            enableNighttimeSensoryReduction = false,
            incursionsRestAtNight = false,
            synchronizeMultiVectorRaids = false,
            enableTacticalIngressFlanking = false,
            resetThreatCooldownOnArrival = true,
            enableEarlyWarningLetters = false
        };

        GeoscapeRuleSet rules = new();
        rules.CopyFromDefaults(settings);

        Assert.AreEqual(VisibilityMode.Omniscient, rules.Visibility.Value);
        Assert.AreEqual(8.5f, rules.ColonyScoutRadius.Value, 0.01f);
        Assert.AreEqual(4.2f, rules.CaravanScoutRadius.Value, 0.01f);
        Assert.IsFalse(rules.NighttimeSensoryReduction.Value);
        Assert.IsFalse(rules.IncursionsRestAtNight.Value);
        Assert.IsFalse(rules.SynchronizeMultiVectorRaids.Value);
        Assert.IsFalse(rules.TacticalIngressFlanking.Value);
        Assert.IsTrue(rules.ResetThreatCooldownOnArrival.Value);
        Assert.IsFalse(rules.EarlyWarningLetters.Value);
    }

    [TestMethod]
    public void RuleSet_WriteToDefaults_WritesAllValues()
    {
        GeoscapeRuleSet rules = new();
        rules.Visibility.Value = VisibilityMode.PlanetarySurveillance;
        rules.ColonyScoutRadius.Value = 6.0f;
        rules.CaravanScoutRadius.Value = 3.5f;
        rules.NighttimeSensoryReduction.Value = false;
        rules.IncursionsRestAtNight.Value = true;
        rules.SynchronizeMultiVectorRaids.Value = false;
        rules.TacticalIngressFlanking.Value = true;
        rules.ResetThreatCooldownOnArrival.Value = true;
        rules.EarlyWarningLetters.Value = false;

        GeoscapeSettings settings = new();
        rules.WriteToDefaults(settings);

        Assert.AreEqual(VisibilityMode.PlanetarySurveillance, settings.visibilityMode);
        Assert.AreEqual(6.0f, settings.colonyScoutRadius, 0.01f);
        Assert.AreEqual(3.5f, settings.caravanScoutRadius, 0.01f);
        Assert.IsFalse(settings.enableNighttimeSensoryReduction);
        Assert.IsTrue(settings.incursionsRestAtNight);
        Assert.IsFalse(settings.synchronizeMultiVectorRaids);
        Assert.IsTrue(settings.enableTacticalIngressFlanking);
        Assert.IsTrue(settings.resetThreatCooldownOnArrival);
        Assert.IsFalse(settings.enableEarlyWarningLetters);
    }

    [TestMethod]
    public void RuleSet_Profiles_ApplyCorrectDefaults()
    {
        GeoscapeRuleSet rules = new();

        rules.ApplyProfile(CampaignProfile.ClassicHaze);
        Assert.AreEqual(VisibilityMode.ClassicHazeOfWar, rules.Visibility.Value);
        Assert.AreEqual(5.0f, rules.ColonyScoutRadius.Value, 0.01f);
        Assert.AreEqual(3.0f, rules.CaravanScoutRadius.Value, 0.01f);

        rules.ApplyProfile(CampaignProfile.PlanetarySurveillance);
        Assert.AreEqual(VisibilityMode.PlanetarySurveillance, rules.Visibility.Value);
        Assert.AreEqual(5.0f, rules.ColonyScoutRadius.Value, 0.01f);

        rules.ApplyProfile(CampaignProfile.HardcoreVigilance);
        Assert.AreEqual(VisibilityMode.ClassicHazeOfWar, rules.Visibility.Value);
        Assert.AreEqual(4.0f, rules.ColonyScoutRadius.Value, 0.01f);
        Assert.AreEqual(2.0f, rules.CaravanScoutRadius.Value, 0.01f);
    }

    [TestMethod]
    public void RuleSet_DetectCurrentProfile_MatchesExpectedProfile()
    {
        GeoscapeRuleSet rules = new();

        rules.ApplyProfile(CampaignProfile.ClassicHaze);
        Assert.AreEqual(CampaignProfile.ClassicHaze, rules.DetectCurrentProfile());

        rules.ApplyProfile(CampaignProfile.PlanetarySurveillance);
        Assert.AreEqual(CampaignProfile.PlanetarySurveillance, rules.DetectCurrentProfile());

        rules.ApplyProfile(CampaignProfile.HardcoreVigilance);
        Assert.AreEqual(CampaignProfile.HardcoreVigilance, rules.DetectCurrentProfile());

        // Customise a single value
        rules.ColonyScoutRadius.Value = 9.9f;
        Assert.AreEqual(CampaignProfile.Custom, rules.DetectCurrentProfile());
    }

    // =========================================================================
    // 3. Zero-Allocation Facade (GeoscapeRules) Tests
    // =========================================================================

    [TestMethod]
    public void GeoscapeRules_FallbackToDefaults_WhenNoActiveCampaign()
    {
        GeoscapeCampaignRules.ResetStaticDataForTesting();

        GeoscapeMod.Settings.visibilityMode = VisibilityMode.PlanetarySurveillance;
        GeoscapeMod.Settings.colonyScoutRadius = 7.7f;

        Assert.AreEqual(VisibilityMode.PlanetarySurveillance, GeoscapeRules.VisibilityMode);
        Assert.AreEqual(7.7f, GeoscapeRules.ColonyScoutRadius, 0.01f);
    }

    [TestMethod]
    public void GeoscapeRules_ReadsFromActiveCampaign_WhenActive()
    {
        GeoscapeCampaignRules campaign = new();
        campaign.Rules.Visibility.Value = VisibilityMode.Omniscient;
        campaign.Rules.ColonyScoutRadius.Value = 12.0f;
        GeoscapeCampaignRules.TestInstance = campaign;

        GeoscapeMod.Settings.visibilityMode = VisibilityMode.ClassicHazeOfWar;
        GeoscapeMod.Settings.colonyScoutRadius = 5.0f;

        Assert.AreEqual(VisibilityMode.Omniscient, GeoscapeRules.VisibilityMode);
        Assert.AreEqual(12.0f, GeoscapeRules.ColonyScoutRadius, 0.01f);
    }

    // =========================================================================
    // 4. Snapshot Selection & Draft Consumption Tests
    // =========================================================================

    [TestMethod]
    public void SelectSnapshotSource_ConsumesDraft_OnlyDuringWorldGeneration()
    {
        GeoscapeRuleSet draft = new();
        draft.Visibility.Value = VisibilityMode.ClassicHazeOfWar;
        GeoscapeSettings defaults = new() { visibilityMode = VisibilityMode.PlanetarySurveillance };

        var result = GeoscapeCampaignRules.SelectSnapshotSource(
            isWorldGeneration: true,
            draft: draft,
            defaults: defaults);

        Assert.AreSame(draft, result.Draft);
        Assert.AreEqual("World generation page", result.Label);
    }

    [TestMethod]
    public void SelectSnapshotSource_IgnoresDraft_WhenNotWorldGeneration()
    {
        GeoscapeRuleSet draft = new();
        GeoscapeSettings defaults = new();

        var result = GeoscapeCampaignRules.SelectSnapshotSource(
            isWorldGeneration: false,
            draft: draft,
            defaults: defaults);

        Assert.IsNull(result.Draft);
        Assert.AreEqual("Defaults", result.Label);
    }

    [TestMethod]
    public void NewCampaignRuleDraft_BeginAndClear()
    {
        GeoscapeSettings defaults = new() { colonyScoutRadius = 4.5f };

        NewCampaignRuleDraft.BeginFromDefaults(defaults);
        Assert.IsNotNull(NewCampaignRuleDraft.Current);
        Assert.AreEqual(4.5f, NewCampaignRuleDraft.Current.ColonyScoutRadius.Value, 0.01f);

        NewCampaignRuleDraft.Clear();
        Assert.IsNull(NewCampaignRuleDraft.Current);
    }

    // =========================================================================
    // 5. Audit Ledger Tests
    // =========================================================================

    [TestMethod]
    public void Ledger_RecordsCampaignCreated_AsPinned()
    {
        GeoscapeCampaignRules campaign = new();
        campaign.Record(RuleChangeKind.CampaignCreated, note: "Day-0 snapshot");

        Assert.AreEqual(1, campaign.Ledger.Count);
        RuleChangeRecord record = campaign.Ledger[0];
        Assert.AreEqual(RuleChangeKind.CampaignCreated, record.kind);
        Assert.IsTrue(record.IsPinned);
        Assert.AreEqual("Day-0 snapshot", record.note);
    }

    [TestMethod]
    public void Ledger_RecordsLegacyMigration_AsPinned()
    {
        GeoscapeCampaignRules campaign = new();
        campaign.Record(RuleChangeKind.LegacyMigration, note: "Migrated from defaults");

        Assert.AreEqual(1, campaign.Ledger.Count);
        RuleChangeRecord record = campaign.Ledger[0];
        Assert.AreEqual(RuleChangeKind.LegacyMigration, record.kind);
        Assert.IsTrue(record.IsPinned);
    }

    [TestMethod]
    public void Ledger_EnforcesMaxEntries_PreservingPinned()
    {
        GeoscapeCampaignRules campaign = new();
        GeoscapeCampaignRules.CurrentTickOverride = () => 100;

        // Day-0 pinned record
        campaign.Record(RuleChangeKind.CampaignCreated, note: "Pinned initial creation");

        // Add 120 unpinned edits across increasing ticks
        for (int i = 0; i < 120; i++)
        {
            int tick = 200 + i;
            GeoscapeCampaignRules.CurrentTickOverride = () => tick;
            campaign.Record(RuleChangeKind.Escalation, "ruleKey", "from", "to");
        }

        Assert.AreEqual(GeoscapeCampaignRules.MaxLedgerEntries, campaign.Ledger.Count);
        // Verify Day-0 pinned record was NOT evicted
        Assert.IsTrue(campaign.Ledger[0].IsPinned);
        Assert.AreEqual(RuleChangeKind.CampaignCreated, campaign.Ledger[0].kind);
    }

    [TestMethod]
    public void Ledger_CoalescesSameTickEdits()
    {
        GeoscapeCampaignRules campaign = new();
        GeoscapeCampaignRules.CurrentTickOverride = () => 500;

        campaign.Record(RuleChangeKind.Escalation, "colonyScoutRadius", "5.0", "6.0");
        campaign.Record(RuleChangeKind.Escalation, "colonyScoutRadius", "6.0", "7.0");

        Assert.AreEqual(1, campaign.Ledger.Count);
        Assert.AreEqual("5.0", campaign.Ledger[0].from);
        Assert.AreEqual("7.0", campaign.Ledger[0].to);
    }

    // =========================================================================
    // 6. Dev Override Lifecycle Tests
    // =========================================================================

    [TestMethod]
    public void UnsafeRuleOverride_LifecycleAndLedgerIntegration()
    {
        GeoscapeCampaignRules campaign = new();
        GeoscapeCampaignRules.TestInstance = campaign;

        bool devMode = false;
        UnsafeRuleOverride.DevModeProviderOverride = () => devMode;

        UnsafeRuleOverride.SetRequested(true);
        // Not effective yet because devMode is false
        Assert.IsFalse(UnsafeRuleOverride.Effective);
        Assert.IsTrue(UnsafeRuleOverride.Requested);

        // Turn on DevMode and notify
        devMode = true;
        UnsafeRuleOverride.Notify_DevModeChanged(true);
        Assert.IsTrue(UnsafeRuleOverride.Effective);
        Assert.AreEqual(RuleChangeKind.DevOverrideEnabled, campaign.Ledger[campaign.Ledger.Count - 1].kind);

        // Turn off DevMode and notify
        devMode = false;
        UnsafeRuleOverride.Notify_DevModeChanged(false);
        Assert.IsFalse(UnsafeRuleOverride.Effective);
        Assert.IsFalse(UnsafeRuleOverride.Requested);

        // Check ledger recorded disabled transition
        Assert.IsTrue(campaign.Ledger.Count >= 2);
        Assert.AreEqual(RuleChangeKind.DevOverrideDisabled, campaign.Ledger[campaign.Ledger.Count - 1].kind);
    }

    // =========================================================================
    // 7. Mod Adoption & Lifecycle Entry Path Tests
    // =========================================================================

    [TestMethod]
    public void DetermineEntryPath_ReturnsNewCampaign_WhenNotFromLoad()
    {
        GeoscapeCampaignRules campaign = new();
        CampaignEntryPath path = campaign.DetermineEntryPath(fromLoad: false);
        Assert.AreEqual(CampaignEntryPath.NewCampaign, path);
    }

    [TestMethod]
    public void DetermineEntryPath_ReturnsActiveSave_WhenLoadedFromSaveAndCurrentSchema()
    {
        GeoscapeCampaignRules campaign = new();
        campaign.LoadedFromSave = true;
        // Parameterless constructor sets schemaVersion = CurrentSchemaVersion (1)
        CampaignEntryPath path = campaign.DetermineEntryPath(fromLoad: true);
        Assert.AreEqual(CampaignEntryPath.ActiveSave, path);
    }

    [TestMethod]
    public void DetermineEntryPath_ReturnsLegacyMigration_WhenPriorSensoryDataLoadedFromSave()
    {
        GeoscapeCampaignRules campaign = new();
        campaign.LoadedFromSave = false;

        SensoryManager sensory = new(null!);
        sensory.LoadedFromSave = true;
        SensoryManager.Instance = sensory;

        CampaignEntryPath path = campaign.DetermineEntryPath(fromLoad: true);
        Assert.AreEqual(CampaignEntryPath.LegacyMigration, path);
    }

    [TestMethod]
    public void DetermineEntryPath_ReturnsLegacyMigration_WhenPriorContactRegistryLoadedFromSave()
    {
        GeoscapeCampaignRules campaign = new();
        campaign.LoadedFromSave = false;

        ContactRegistry registry = new(null!);
        registry.LoadedFromSave = true;
        ContactRegistry.Instance = registry;

        CampaignEntryPath path = campaign.DetermineEntryPath(fromLoad: true);
        Assert.AreEqual(CampaignEntryPath.LegacyMigration, path);
    }

    [TestMethod]
    public void DetermineEntryPath_ReturnsModAdoption_WhenNoPriorComponentsDeserialized()
    {
        GeoscapeCampaignRules campaign = new();
        campaign.LoadedFromSave = false;

        // No prior components deserialized
        SensoryManager sensory = new(null!);
        sensory.LoadedFromSave = false;
        SensoryManager.Instance = sensory;

        CampaignEntryPath path = campaign.DetermineEntryPath(fromLoad: true);
        Assert.AreEqual(CampaignEntryPath.ModAdoption, path);
    }

    [TestMethod]
    public void ApplyAdoptionProfile_RecordsModAdoption_AsPinnedRecord()
    {
        GeoscapeCampaignRules campaign = new();
        campaign.ApplyAdoptionProfile(CampaignProfile.PlanetarySurveillance, "Test User");

        Assert.AreEqual(1, campaign.Ledger.Count);
        RuleChangeRecord record = campaign.Ledger[0];
        Assert.AreEqual(RuleChangeKind.ModAdoption, record.kind);
        Assert.IsTrue(record.IsPinned);
        Assert.IsTrue(record.note?.Contains("Planetary Surveillance") == true);
        Assert.AreEqual(VisibilityMode.PlanetarySurveillance, campaign.Rules.Visibility.Value);
        Assert.IsTrue(campaign.HasRecordedAdoption);
    }

    [TestMethod]
    public void ApplyAdoptionProfile_ClassicHaze_SetsClassicHazeOfWar()
    {
        GeoscapeCampaignRules campaign = new();
        campaign.ApplyAdoptionProfile(CampaignProfile.ClassicHaze, "Test User");

        Assert.AreEqual(1, campaign.Ledger.Count);
        Assert.AreEqual(RuleChangeKind.ModAdoption, campaign.Ledger[0].kind);
        Assert.AreEqual(VisibilityMode.ClassicHazeOfWar, campaign.Rules.Visibility.Value);
    }

    [TestMethod]
    public void ApplyAdoptionCustom_RecordsModAdoption_WithCustomProfile()
    {
        GeoscapeCampaignRules campaign = new();
        campaign.ApplyAdoptionCustom("Test Custom");

        Assert.AreEqual(1, campaign.Ledger.Count);
        Assert.AreEqual(RuleChangeKind.ModAdoption, campaign.Ledger[0].kind);
        Assert.IsTrue(campaign.Ledger[0].note?.Contains("Custom") == true);
        Assert.IsTrue(campaign.HasRecordedAdoption);
    }

    [TestMethod]
    public void Page_GeoscapeRules_Properties_And_Draft_Initialization()
    {
        Page_GeoscapeRules page = new();
        Assert.AreEqual("Geoscape Campaign Rules", page.PageTitle);
        Assert.AreEqual(new Vector2(1020f, 764f), page.InitialSize);

        page.PreOpen();
        Assert.IsNotNull(NewCampaignRuleDraft.Current);
    }

    [TestMethod]
    public void PageUtility_StitchedPages_Patch_Inserts_Page_GeoscapeRules_Before_CreateWorldParams()
    {
        Page_SelectStoryteller storyteller = new();
        Page_CreateWorldParams worldParams = new();
        Page_ConfigureStartingPawns pawns = new();

        IEnumerable<Page> pages = new List<Page> { storyteller, worldParams, pawns };

        PageUtility_StitchedPages_Patch.Prefix(ref pages);

        List<Page> list = pages.ToList();
        Assert.AreEqual(4, list.Count);
        Assert.IsInstanceOfType(list[0], typeof(Page_SelectStoryteller));
        Assert.IsInstanceOfType(list[1], typeof(Page_GeoscapeRules));
        Assert.IsInstanceOfType(list[2], typeof(Page_CreateWorldParams));
        Assert.IsInstanceOfType(list[3], typeof(Page_ConfigureStartingPawns));

        // Test idempotency: calling Prefix again must not insert duplicate
        PageUtility_StitchedPages_Patch.Prefix(ref pages);
        Assert.AreEqual(4, pages.Count());
    }

    [TestMethod]
    public void CampaignProfile_FogOfWar_Aliases_And_Omniscient()
    {
        GeoscapeRuleSet rules = new();
        rules.ApplyProfile(CampaignProfile.FogOfWar);
        Assert.AreEqual(VisibilityMode.FogOfWar, rules.Visibility.Value);
        Assert.AreEqual(CampaignProfile.FogOfWar, rules.DetectCurrentProfile());

        rules.ApplyProfile(CampaignProfile.Omniscient);
        Assert.AreEqual(VisibilityMode.Omniscient, rules.Visibility.Value);
        Assert.AreEqual(CampaignProfile.Omniscient, rules.DetectCurrentProfile());
    }

    [TestMethod]
    public void Dialog_GeoscapeCampaignRules_Properties_And_OnClose_Callback()
    {
        GeoscapeRuleSet draft = new();
        bool closedCalled = false;
        Dialog_GeoscapeCampaignRules dialog = new(draft, onClose: () => closedCalled = true);

        Assert.AreEqual(new Vector2(700f, 760f), dialog.InitialSize);
        dialog.PreClose();
        Assert.IsTrue(closedCalled);
    }

    [TestMethod]
    public void Draft_Tuning_Switches_To_Custom_Profile_And_Reapply_Resets()
    {
        GeoscapeRuleSet draft = new();
        draft.ApplyProfile(CampaignProfile.FogOfWar);
        Assert.AreEqual(CampaignProfile.FogOfWar, draft.DetectCurrentProfile());

        // Modify fine-grained rule
        draft.ColonyScoutRadius.Value = 3.5f;
        Assert.AreEqual(CampaignProfile.Custom, draft.DetectCurrentProfile());

        // Re-apply preset restores profile
        draft.ApplyProfile(CampaignProfile.HardcoreVigilance);
        Assert.AreEqual(CampaignProfile.HardcoreVigilance, draft.DetectCurrentProfile());
        Assert.AreEqual(4.0f, draft.ColonyScoutRadius.Value, 0.01f);
    }

    [TestMethod]
    public void Draft_Omniscient_Tuning_Incursions_Switches_To_Custom_Profile()
    {
        GeoscapeRuleSet draft = new();
        draft.ApplyProfile(CampaignProfile.Omniscient);
        Assert.AreEqual(CampaignProfile.Omniscient, draft.DetectCurrentProfile());

        // Alter an incursion rule under Omniscient
        draft.IncursionsRestAtNight.Value = false;
        Assert.AreEqual(CampaignProfile.Custom, draft.DetectCurrentProfile());

        // Restore rule restores Omniscient
        draft.IncursionsRestAtNight.Value = true;
        Assert.AreEqual(CampaignProfile.Omniscient, draft.DetectCurrentProfile());

        // Alter tactical flanking
        draft.TacticalIngressFlanking.Value = false;
        Assert.AreEqual(CampaignProfile.Custom, draft.DetectCurrentProfile());
    }

    [TestMethod]
    public void CampaignProfile_GetCleanName_ReturnsCleanStrings()
    {
        Assert.AreEqual("Fog of War", CampaignProfile.FogOfWar.GetCleanName());
        Assert.AreEqual("Planetary Surveillance", CampaignProfile.PlanetarySurveillance.GetCleanName());
        Assert.AreEqual("Hardcore Vigilance", CampaignProfile.HardcoreVigilance.GetCleanName());
        Assert.AreEqual("Omniscient", CampaignProfile.Omniscient.GetCleanName());
        Assert.AreEqual("Custom", CampaignProfile.Custom.GetCleanName());
    }
}
