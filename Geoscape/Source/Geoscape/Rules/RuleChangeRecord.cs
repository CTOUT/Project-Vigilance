using Verse;

namespace ProjectVigilance.Geoscape.Rules;

/// <summary>
/// Categories of campaign rule ledger events.
/// </summary>
public enum RuleChangeKind
{
    /// <summary>Pinned: Initial rules and snapshot established at world creation.</summary>
    CampaignCreated,

    /// <summary>Pinned: Initial rules adopted from ModSettings on legacy-save migration.</summary>
    LegacyMigration,

    /// <summary>Pinned: Initial rules adopted mid-campaign onto an existing non-Vigilance save.</summary>
    ModAdoption,

    /// <summary>Rule changed during the legacy-save review window.</summary>
    ReviewWindowEdit,

    /// <summary>Legacy-save review window locked (by player or timer expiry).</summary>
    ReviewWindowLocked,

    /// <summary>Forward monotonic escalation applied.</summary>
    Escalation,

    /// <summary>Reverse or immutable change applied under developer override.</summary>
    UnsafeChange,

    /// <summary>Developer override became effective.</summary>
    DevOverrideEnabled,

    /// <summary>Developer override disabled.</summary>
    DevOverrideDisabled
}

/// <summary>
/// Persistent audit entry in the campaign rule change ledger.
/// </summary>
public sealed class RuleChangeRecord : IExposable
{
    public int tick;
    public RuleChangeKind kind;
    public string? ruleKey;
    public string? from;
    public string? to;
    public string? note;

    public bool IsPinned => kind is RuleChangeKind.CampaignCreated
                               or RuleChangeKind.LegacyMigration
                               or RuleChangeKind.ModAdoption;

    public RuleChangeRecord()
    {
    }

    public RuleChangeRecord(int tick, RuleChangeKind kind, string? ruleKey = null, string? from = null, string? to = null, string? note = null)
    {
        this.tick = tick;
        this.kind = kind;
        this.ruleKey = ruleKey;
        this.from = from;
        this.to = to;
        this.note = note;
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref tick, "tick", 0);
        Scribe_Values.Look(ref kind, "kind", RuleChangeKind.CampaignCreated);
        Scribe_Values.Look(ref ruleKey, "ruleKey");
        Scribe_Values.Look(ref from, "from");
        Scribe_Values.Look(ref to, "to");
        Scribe_Values.Look(ref note, "note");
    }

    public override string ToString()
    {
        string header = $"[Tick {tick}] {kind}";
        if (!string.IsNullOrEmpty(ruleKey))
        {
            header += $" on '{ruleKey}': {from} -> {to}";
        }
        if (!string.IsNullOrEmpty(note))
        {
            header += $" ({note})";
        }
        return header;
    }
}
