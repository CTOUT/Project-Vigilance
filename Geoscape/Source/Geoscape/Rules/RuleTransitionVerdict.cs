namespace ProjectVigilance.Geoscape.Rules;

/// <summary>
/// Evaluation result of a proposed campaign rule change.
/// </summary>
public enum RuleTransitionVerdict
{
    /// <summary>Proposed value equals the current value; no action taken.</summary>
    NoChange,

    /// <summary>Change is permitted immediately without warning.</summary>
    Allowed,

    /// <summary>Forward escalation: requires user confirmation explaining permanent reveal.</summary>
    RequiresEscalationConfirmation,

    /// <summary>Reverse or immutable change under dev override: requires destructive warning.</summary>
    RequiresUnsafeConfirmation,

    /// <summary>Change is strictly forbidden.</summary>
    Blocked
}

/// <summary>
/// Pure data context passed into rule transition evaluations.
/// </summary>
public readonly record struct RuleEditContext(
    bool CampaignActive,
    bool ReviewWindowOpen,
    bool UnsafeOverrideEnabled);
