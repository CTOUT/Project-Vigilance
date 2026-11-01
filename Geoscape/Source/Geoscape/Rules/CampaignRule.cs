using System;
using System.Collections.Generic;
using Verse;

namespace ProjectVigilance.Geoscape.Rules;

/// <summary>
/// Non-generic contract for campaign rule metadata, serialization, and UI rendering.
/// </summary>
public interface ICampaignRule
{
    string Key { get; }
    string Label { get; }
    string Description { get; }
    RuleMutability Mutability { get; }
    RuleTransitionEffects Effects { get; }
    string CurrentValueLabel { get; }
    void CopyFromDefaults(GeoscapeSettings defaults);
    void WriteToDefaults(GeoscapeSettings defaults);
    void ExposeValue();
}

/// <summary>
/// Strongly-typed campaign rule container holding current value, metadata, transition evaluation, and Scribe bindings.
/// </summary>
/// <typeparam name="T">Type of rule value (e.g. bool, float, enum).</typeparam>
public sealed class CampaignRule<T> : ICampaignRule where T : notnull
{
    private T value;
    private readonly T hardDefault;
    private readonly Func<GeoscapeSettings, T> defaultSelector;
    private readonly Action<GeoscapeSettings, T> defaultWriter;
    private readonly Func<T, int>? escalationRank;
    private readonly Func<T, string> valueFormatter;

    public string Key { get; }
    public string Label { get; }
    public string Description { get; }
    public RuleMutability Mutability { get; }
    public RuleTransitionEffects Effects { get; }

    public T Value
    {
        get => value;
        set => this.value = value;
    }

    public T HardDefault => hardDefault;
    public string CurrentValueLabel => valueFormatter(value);

    public CampaignRule(
        string key,
        string label,
        string description,
        RuleMutability mutability,
        T hardDefault,
        Func<GeoscapeSettings, T> defaultSelector,
        Action<GeoscapeSettings, T> defaultWriter,
        RuleTransitionEffects effects = RuleTransitionEffects.None,
        Func<T, int>? escalationRank = null,
        Func<T, string>? valueFormatter = null)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Label = label ?? throw new ArgumentNullException(nameof(label));
        Description = description ?? string.Empty;
        Mutability = mutability;
        this.hardDefault = hardDefault;
        this.defaultSelector = defaultSelector ?? throw new ArgumentNullException(nameof(defaultSelector));
        this.defaultWriter = defaultWriter ?? throw new ArgumentNullException(nameof(defaultWriter));
        Effects = effects;
        this.escalationRank = escalationRank;
        this.valueFormatter = valueFormatter ?? (val => val.ToString() ?? string.Empty);

        if (mutability == RuleMutability.Escalatable && escalationRank == null)
        {
            throw new ArgumentException($"Rule '{key}' is marked Escalatable but no escalationRank function was provided.", nameof(escalationRank));
        }

        value = hardDefault;
    }

    public RuleTransitionVerdict Evaluate(T proposed, in RuleEditContext ctx)
    {
        if (EqualityComparer<T>.Default.Equals(value, proposed))
        {
            return RuleTransitionVerdict.NoChange;
        }

        // When not in an active campaign (editing new-campaign defaults or world-gen draft),
        // or during the legacy-save review window, or for Mutable rules: allowed immediately.
        if (!ctx.CampaignActive || ctx.ReviewWindowOpen || Mutability == RuleMutability.Mutable)
        {
            return RuleTransitionVerdict.Allowed;
        }

        // In an active campaign, Escalatable rules allow forward monotonic escalation with confirmation.
        if (Mutability == RuleMutability.Escalatable)
        {
            int currentRank = escalationRank!(value);
            int proposedRank = escalationRank!(proposed);
            if (proposedRank > currentRank)
            {
                return RuleTransitionVerdict.RequiresEscalationConfirmation;
            }
        }

        // Reverse transitions or changes to Immutable rules require developer override.
        if (ctx.UnsafeOverrideEnabled)
        {
            return RuleTransitionVerdict.RequiresUnsafeConfirmation;
        }

        return RuleTransitionVerdict.Blocked;
    }

    public void SetUnchecked(T newValue)
    {
        value = newValue;
    }

    public void CopyFromDefaults(GeoscapeSettings defaults)
    {
        value = defaultSelector(defaults);
    }

    public void WriteToDefaults(GeoscapeSettings defaults)
    {
        defaultWriter(defaults, value);
    }

    public void ExposeValue()
    {
        // forceSave: true ensures every rule is explicitly serialised to XML,
        // preventing future shipping default changes from silently altering existing saves.
        Scribe_Values.Look(ref value, Key, hardDefault, forceSave: true);
    }
}
