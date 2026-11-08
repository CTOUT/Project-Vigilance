using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape.Rules;

namespace Geoscape.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SavegameIntegrityTests
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

    [TestMethod]
    public void ReconcileLedgerOnLoad_MatchingBaseline_SetsGovernedStatus()
    {
        GeoscapeCampaignRules campaign = new();
        GeoscapeCampaignRules.TestInstance = campaign;

        // Construct standard pinned Day-0 snapshot string from current rules
        string snapshotStr = campaign.Rules.CreateSnapshotString();
        campaign.Record(
            RuleChangeKind.CampaignCreated,
            note: $"Profile: Standard | {snapshotStr}");

        // Simulate ending review window so it's governed
        campaign.ConfirmAndLock();

        campaign.ReconcileLedgerOnLoad();

        Assert.IsFalse(campaign.HasExternalModifications);
        Assert.AreEqual(CampaignIntegrityStatus.Governed, campaign.IntegrityStatus);
        Assert.AreEqual("Governed & Verified", campaign.IntegrityStatusLabel);
    }

    [TestMethod]
    public void ReconcileLedgerOnLoad_ExternalDirectSaveEdit_DetectsDiscrepancyAndRecordsAudit()
    {
        GeoscapeCampaignRules campaign = new();
        GeoscapeCampaignRules.TestInstance = campaign;

        // Baseline pinned record with default rules (ColonyScoutRadius = 5.0)
        string snapshotStr = campaign.Rules.CreateSnapshotString();
        campaign.Record(
            RuleChangeKind.CampaignCreated,
            note: $"Profile: Standard | {snapshotStr}");

        campaign.ConfirmAndLock();

        // Simulate an external XML modification directly modifying ColonyScoutRadius to 15.0 without a ledger entry
        campaign.Rules.ColonyScoutRadius.Value = 15.0f;

        campaign.ReconcileLedgerOnLoad();

        Assert.IsTrue(campaign.HasExternalModifications);
        Assert.AreEqual(CampaignIntegrityStatus.ExternallyModified, campaign.IntegrityStatus);
        Assert.AreEqual("External Modification Detected", campaign.IntegrityStatusLabel);

        RuleChangeRecord? modRecord = campaign.Ledger.FirstOrDefault(r => r.kind == RuleChangeKind.ExternalModification);
        Assert.IsNotNull(modRecord);
        StringAssert.Contains(modRecord.note, "colonyScoutRadius");
        StringAssert.Contains(modRecord.note, "Direct save modification detected");
    }

    [TestMethod]
    public void ReconcileLedgerOnLoad_LegitimateEscalation_ReplaysWithoutFlaggingModification()
    {
        GeoscapeCampaignRules campaign = new();
        GeoscapeCampaignRules.TestInstance = campaign;

        string snapshotStr = campaign.Rules.CreateSnapshotString();
        campaign.Record(
            RuleChangeKind.CampaignCreated,
            note: $"Profile: Standard | {snapshotStr}");

        campaign.ConfirmAndLock();

        // Simulate legitimate in-game escalation recorded in ledger via official API
        campaign.ForceApplyEscalation(campaign.Rules.ColonyScoutRadius, 4.0f);

        campaign.ReconcileLedgerOnLoad();

        Assert.IsFalse(campaign.HasExternalModifications);
        Assert.AreEqual(CampaignIntegrityStatus.Governed, campaign.IntegrityStatus);
    }

    [TestMethod]
    public void ReconcileLedgerOnLoad_Idempotency_DoesNotDuplicateAuditRecord()
    {
        GeoscapeCampaignRules campaign = new();
        GeoscapeCampaignRules.TestInstance = campaign;

        string snapshotStr = campaign.Rules.CreateSnapshotString();
        campaign.Record(
            RuleChangeKind.CampaignCreated,
            note: $"Profile: Standard | {snapshotStr}");

        campaign.ConfirmAndLock();

        // Mutate rule
        campaign.Rules.ColonyScoutRadius.Value = 20.0f;

        campaign.ReconcileLedgerOnLoad();
        int countFirst = campaign.Ledger.Count(r => r.kind == RuleChangeKind.ExternalModification);

        // Run reconciliation a second time
        campaign.ReconcileLedgerOnLoad();
        int countSecond = campaign.Ledger.Count(r => r.kind == RuleChangeKind.ExternalModification);

        Assert.AreEqual(1, countFirst);
        Assert.AreEqual(1, countSecond);
    }
}
