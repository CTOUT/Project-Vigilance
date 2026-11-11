using System;
using System.Security.Cryptography;
using System.Text;

namespace ProjectVigilance.Geoscape.Rules;

/// <summary>
/// Cryptographic hashing service for campaign rule change ledger records.
/// Secures the transaction log against unrecorded direct XML tampering or baseline alterations.
/// </summary>
public static class RuleLedgerSigner
{
    public const string GenesisHash = "GENESIS-00000000";

    // Secret salt embedded in assembly to authenticate ledger continuity
    private static readonly byte[] ModSecret = Encoding.UTF8.GetBytes(
        "PV-Geoscape-Ledger-Sig-v2#9a7f3e1b-4d6c-4b5a-8c9e-2f1d0a3b5c7e"
    );

    /// <summary>
    /// Computes an HMAC-SHA256 signature for a ledger record chained to the previous record's hash.
    /// </summary>
    public static string ComputeRecordHash(
        string? prevHash,
        int tick,
        RuleChangeKind kind,
        string? ruleKey,
        string? from,
        string? to,
        string? note,
        string? campaignId)
    {
        string pHash = string.IsNullOrEmpty(prevHash) ? GenesisHash : prevHash;
        string rKey = ruleKey ?? string.Empty;
        string rFrom = from ?? string.Empty;
        string rTo = to ?? string.Empty;
        string rNote = note ?? string.Empty;
        string cId = string.IsNullOrEmpty(campaignId) ? "DEFAULT-CAMPAIGN" : campaignId;

        // Canonical payload representation
        string payload = $"{pHash}|{tick}|{(int)kind}|{rKey}|{rFrom}|{rTo}|{rNote}|{cId}";

        using var hmac = new HMACSHA256(ModSecret);
        byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Verifies the cryptographic signature of a single ledger record against expected predecessor and campaign state.
    /// </summary>
    public static bool VerifyRecordHash(
        RuleChangeRecord record,
        string? prevHash,
        string? campaignId)
    {
        if (record == null || string.IsNullOrEmpty(record.hash))
        {
            return false;
        }

        string expected = ComputeRecordHash(
            prevHash,
            record.tick,
            record.kind,
            record.ruleKey,
            record.from,
            record.to,
            record.note,
            campaignId);

        return string.Equals(record.hash, expected, StringComparison.OrdinalIgnoreCase);
    }
}
