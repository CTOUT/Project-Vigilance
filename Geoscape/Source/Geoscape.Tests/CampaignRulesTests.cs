using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape;
using ProjectVigilance.Geoscape.Rules;
using ProjectVigilance.Geoscape.Sensory;
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
    }

    [TestCleanup]
    public void Teardown()
    {
        GeoscapeCampaignRules.ResetStaticDataForTesting();
        UnsafeRuleOverride.ResetForTesting();
        NewCampaignRuleDraft.Clear();
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
}
