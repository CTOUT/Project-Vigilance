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

    [TestMethod]
    public void ReconcileLedgerOnLoad_TamperedLedgerNote_DetectsSignatureMismatch()
    {
        GeoscapeCampaignRules campaign = new();
        GeoscapeCampaignRules.TestInstance = campaign;

        string snapshotStr = campaign.Rules.CreateSnapshotString();
        campaign.Record(
            RuleChangeKind.CampaignCreated,
            note: $"Profile: Standard | {snapshotStr}");

        campaign.ConfirmAndLock();

        // Simulate an attacker editing both the rule AND the ledger baseline note in XML
        // To claim colonyScoutRadius was always 15
        campaign.Rules.ColonyScoutRadius.Value = 15.0f;
        string tamperedSnapshot = snapshotStr.Replace("colonyScoutRadius=5 hexes", "colonyScoutRadius=15 hexes");
        campaign.Ledger[0].note = $"Profile: Standard | {tamperedSnapshot}";

        campaign.ReconcileLedgerOnLoad();

        Assert.IsTrue(campaign.HasExternalModifications);
        Assert.AreEqual(CampaignIntegrityStatus.ExternallyModified, campaign.IntegrityStatus);

        RuleChangeRecord? modRecord = campaign.Ledger.FirstOrDefault(r => r.kind == RuleChangeKind.ExternalModification);
        Assert.IsNotNull(modRecord);
        StringAssert.Contains(modRecord.note, "Audit ledger signature invalid");
        StringAssert.Contains(modRecord.note, "tampering detected");
    }

    [TestMethod]
    public void ReconcileLedgerOnLoad_ForgedRecord_DetectsSignatureMismatch()
    {
        GeoscapeCampaignRules campaign = new();
        GeoscapeCampaignRules.TestInstance = campaign;

        string snapshotStr = campaign.Rules.CreateSnapshotString();
        campaign.Record(
            RuleChangeKind.CampaignCreated,
            note: $"Profile: Standard | {snapshotStr}");

        campaign.ConfirmAndLock();

        // Inject forged escalation record with invalid hash
        RuleChangeRecord forged = new(100, RuleChangeKind.Escalation, "colonyScoutRadius", "5 hexes", "15 hexes", null, "badhash12345");
        // Use reflection to add to internal list
        var ledgerList = (System.Collections.Generic.List<RuleChangeRecord>)campaign.Ledger;
        ledgerList.Add(forged);

        campaign.ReconcileLedgerOnLoad();

        Assert.IsTrue(campaign.HasExternalModifications);
        Assert.AreEqual(CampaignIntegrityStatus.ExternallyModified, campaign.IntegrityStatus);

        RuleChangeRecord? modRecord = campaign.Ledger.FirstOrDefault(r => r.kind == RuleChangeKind.ExternalModification);
        Assert.IsNotNull(modRecord);
        StringAssert.Contains(modRecord.note, "Audit ledger signature invalid");
    }

    [TestMethod]
    public void ReconcileLedgerOnLoad_SchemaDowngradeAndStrippedHash_DetectsTampering()
    {
        GeoscapeCampaignRules campaign = new();
        GeoscapeCampaignRules.TestInstance = campaign;

        string snapshotStr = campaign.Rules.CreateSnapshotString();

        // Emulate an attacker downgrading schemaVersion to 1 and stripping the record hash
        RuleChangeRecord unsealedBaseline = new(0, RuleChangeKind.CampaignCreated, null, null, null, $"Profile: Standard | {snapshotStr}", null);
        var ledgerList = (System.Collections.Generic.List<RuleChangeRecord>)campaign.Ledger;
        ledgerList.Clear();
        ledgerList.Add(unsealedBaseline);

        typeof(GeoscapeCampaignRules)
            .GetField("schemaVersion", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(campaign, 1);

        campaign.ConfirmAndLock();
        campaign.ReconcileLedgerOnLoad();

        Assert.IsTrue(campaign.HasExternalModifications);
        Assert.AreEqual(CampaignIntegrityStatus.ExternallyModified, campaign.IntegrityStatus);
        Assert.IsTrue(campaign.Ledger.Any(r => r.kind == RuleChangeKind.ExternalModification && r.note != null && r.note.Contains("schema downgrade")));
        Assert.IsTrue(campaign.Ledger.Any(r => r.kind == RuleChangeKind.ExternalModification && r.note != null && r.note.Contains("Audit ledger signature invalid")));
    }

    [TestMethod]
    public void ReconcileLedgerOnLoad_MissingPinnedBaseline_DetectsTampering()
    {
        GeoscapeCampaignRules campaign = new();
        GeoscapeCampaignRules.TestInstance = campaign;
        campaign.LoadedFromSave = true;

        // Ledger wiped empty (simulates deleting <ruleChangeLedger> items in XML)
        campaign.ConfirmAndLock();
        campaign.ReconcileLedgerOnLoad();

        Assert.IsTrue(campaign.HasExternalModifications);
        Assert.AreEqual(CampaignIntegrityStatus.ExternallyModified, campaign.IntegrityStatus);
        RuleChangeRecord? modRecord = campaign.Ledger.FirstOrDefault(r => r.kind == RuleChangeKind.ExternalModification);
        Assert.IsNotNull(modRecord);
        StringAssert.Contains(modRecord.note, "missing Day-0 baseline snapshot");
    }

    [TestMethod]
    public void ReconcileLedgerOnLoad_SignatureTamperAndRuleDiff_IdempotentOnReload()
    {
        GeoscapeCampaignRules campaign = new();
        GeoscapeCampaignRules.TestInstance = campaign;

        string snapshotStr = campaign.Rules.CreateSnapshotString();
        campaign.Record(
            RuleChangeKind.CampaignCreated,
            note: $"Profile: Standard | {snapshotStr}");
        campaign.ConfirmAndLock();

        // Corrupt both rule and baseline note signature
        campaign.Rules.ColonyScoutRadius.Value = 15.0f;
        campaign.Ledger[0].note = $"Profile: Standard | {snapshotStr} ";

        campaign.ReconcileLedgerOnLoad();
        int firstCount = campaign.Ledger.Count(r => r.kind == RuleChangeKind.ExternalModification);

        campaign.ReconcileLedgerOnLoad();
        int secondCount = campaign.Ledger.Count(r => r.kind == RuleChangeKind.ExternalModification);

        Assert.AreEqual(2, firstCount);
        Assert.AreEqual(2, secondCount);
    }

    [TestMethod]
    public void RuleLedgerSigner_Deterministic_AcrossInstances()
    {
        string campaignId = "test-campaign-12345";
        string hashA = RuleLedgerSigner.ComputeRecordHash(
            RuleLedgerSigner.GenesisHash,
            100,
            RuleChangeKind.CampaignCreated,
            null,
            null,
            null,
            "Defaults | test",
            campaignId);

        string hashB = RuleLedgerSigner.ComputeRecordHash(
            RuleLedgerSigner.GenesisHash,
            100,
            RuleChangeKind.CampaignCreated,
            null,
            null,
            null,
            "Defaults | test",
            campaignId);

        Assert.AreEqual(hashA, hashB);
        Assert.AreEqual(64, hashA.Length); // 256-bit hex string
    }
}
