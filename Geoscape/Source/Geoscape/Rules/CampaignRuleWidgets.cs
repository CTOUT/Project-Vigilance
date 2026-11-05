using System;
using ProjectVigilance.Geoscape.Sensory;
using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Rules;

/// <summary>
/// Metadata-aware UI drawing helpers enforcing mutability constraints, locked state indicators,
/// escalation confirmations, and unsafe developer override warnings.
/// </summary>
public static class CampaignRuleWidgets
{
    public static void DrawBool(
        Listing_Standard listing,
        CampaignRule<bool> rule,
        GeoscapeCampaignRules? campaign,
        RuleEditContext ctx,
        bool showLockPreview = false)
    {
        bool current = rule.Value;
        bool proposed = !current;
        RuleTransitionVerdict verdict = rule.Evaluate(proposed, ctx);

        string label = rule.Label;
        if (showLockPreview && rule.Mutability == RuleMutability.Immutable)
        {
            label += " (Locked after creation)";
        }
        else if (verdict == RuleTransitionVerdict.Blocked)
        {
            label += " (Locked)";
        }
        else if (verdict == RuleTransitionVerdict.RequiresUnsafeConfirmation)
        {
            label += " (Unsafe)";
        }

        bool disabled = verdict == RuleTransitionVerdict.Blocked && !ctx.UnsafeOverrideEnabled;
        bool valRef = current;

        if (disabled)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.45f);
        }

        listing.CheckboxLabeled(label, ref valRef, rule.Description);

        if (disabled)
        {
            GUI.color = Color.white;
        }

        if (valRef != current && !disabled)
        {
            HandleProposedChange(rule, proposed, verdict, campaign);
        }
    }

    public static void DrawFloat(
        Listing_Standard listing,
        CampaignRule<float> rule,
        float min,
        float max,
        GeoscapeCampaignRules? campaign,
        RuleEditContext ctx,
        bool showLockPreview = false)
    {
        float current = rule.Value;
        RuleTransitionVerdict testVerdict = rule.Evaluate(current + 0.1f, ctx);
        bool isLocked = testVerdict == RuleTransitionVerdict.Blocked && !ctx.UnsafeOverrideEnabled;

        string header = $"{rule.Label}: {rule.CurrentValueLabel}";
        if (showLockPreview && rule.Mutability == RuleMutability.Immutable)
        {
            header += " [Locked after creation]";
        }
        else if (isLocked)
        {
            header += " [Locked]";
        }
        else if (testVerdict == RuleTransitionVerdict.RequiresUnsafeConfirmation)
        {
            header += " [Unsafe Override]";
        }

        listing.Label(header, tooltip: rule.Description);

        if (isLocked)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.45f);
            listing.Slider(current, min, max);
            GUI.color = Color.white;
            return;
        }

        float newRadius = listing.Slider(current, min, max);
        if (Mathf.Abs(newRadius - current) > 0.05f)
        {
            RuleTransitionVerdict verdict = rule.Evaluate(newRadius, ctx);
            if (verdict == RuleTransitionVerdict.Allowed)
            {
                if (campaign != null)
                {
                    campaign.TryApply(rule, newRadius, out _);
                }
                else
                {
                    rule.SetUnchecked(newRadius);
                }
            }
            else if (verdict == RuleTransitionVerdict.RequiresUnsafeConfirmation && campaign != null)
            {
                campaign.ForceApplyUnsafe(rule, newRadius);
            }
        }
    }

    public static void DrawVisibilityRadio(
        Listing_Standard listing,
        VisibilityMode option,
        string label,
        string tooltip,
        CampaignRule<VisibilityMode> rule,
        GeoscapeCampaignRules? campaign,
        RuleEditContext ctx,
        bool showLockPreview = false)
    {
        bool isSelected = rule.Value == option;
        RuleTransitionVerdict verdict = isSelected ? RuleTransitionVerdict.NoChange : rule.Evaluate(option, ctx);

        string displayLabel = label;
        if (showLockPreview && (option == VisibilityMode.FogOfWar || option == VisibilityMode.ClassicHazeOfWar) && !displayLabel.Contains("(Recommended)"))
        {
            displayLabel += " (Recommended)";
        }

        if (verdict == RuleTransitionVerdict.Blocked)
        {
            displayLabel += " (Locked - Cannot reverse)";
        }
        else if (verdict == RuleTransitionVerdict.RequiresEscalationConfirmation)
        {
            displayLabel += " (Permanent escalation)";
        }
        else if (verdict == RuleTransitionVerdict.RequiresUnsafeConfirmation)
        {
            displayLabel += " (Unsafe downgrade)";
        }

        bool disabled = verdict == RuleTransitionVerdict.Blocked && !ctx.UnsafeOverrideEnabled;

        if (disabled)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.45f);
        }

        if (listing.RadioButton(displayLabel, isSelected, tooltip: tooltip))
        {
            if (!isSelected && !disabled)
            {
                HandleProposedChange(rule, option, verdict, campaign);
            }
        }

        if (disabled)
        {
            GUI.color = Color.white;
        }
    }

    public static void DrawStartingSiteSelectionRadio(
        Listing_Standard listing,
        StartingSiteSelectionMode option,
        string label,
        string tooltip,
        CampaignRule<StartingSiteSelectionMode> rule,
        GeoscapeCampaignRules? campaign,
        RuleEditContext ctx,
        bool showLockPreview = false)
    {
        bool isSelected = rule.Value == option;
        RuleTransitionVerdict verdict = isSelected ? RuleTransitionVerdict.NoChange : rule.Evaluate(option, ctx);

        string displayLabel = label;
        if (verdict == RuleTransitionVerdict.Blocked)
        {
            displayLabel += " (Locked)";
        }

        bool disabled = verdict == RuleTransitionVerdict.Blocked && !ctx.UnsafeOverrideEnabled;

        if (disabled)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.45f);
        }

        if (listing.RadioButton(displayLabel, isSelected, tooltip: tooltip))
        {
            if (!isSelected && !disabled)
            {
                HandleProposedChange(rule, option, verdict, campaign);
            }
        }

        if (disabled)
        {
            GUI.color = Color.white;
        }
    }

    private static void HandleProposedChange<T>(
        CampaignRule<T> rule,
        T proposed,
        RuleTransitionVerdict verdict,
        GeoscapeCampaignRules? campaign) where T : notnull
    {
        if (campaign == null)
        {
            // Outside of active campaign (defaults or world-gen draft): apply immediately
            rule.SetUnchecked(proposed);
            return;
        }

        switch (verdict)
        {
            case RuleTransitionVerdict.Allowed:
                campaign.TryApply(rule, proposed, out _);
                break;

            case RuleTransitionVerdict.RequiresEscalationConfirmation:
                string escText = $"You are about to permanently escalate the campaign rule '{rule.Label}':\n\n" +
                                 $"{rule.CurrentValueLabel} -> {proposed}\n\n" +
                                 "This change will reveal previously shrouded strategic information, unpause dynamic camp timers, and re-evaluate caravan routes.\n\n" +
                                 "IMPORTANT: This action is permanent for this save game and cannot be reversed.\n\n" +
                                 "Do you wish to proceed?";
                Dialog_MessageBox escBox = Dialog_MessageBox.CreateConfirmation(
                    escText,
                    () => campaign.ForceApplyEscalation(rule, proposed),
                    destructive: false,
                    title: "Confirm Campaign Rule Escalation");
                Find.WindowStack?.Add(escBox);
                break;

            case RuleTransitionVerdict.RequiresUnsafeConfirmation:
                string unsafeText = $"WARNING: You are performing an unsafe campaign rule change under developer override:\n\n" +
                                    $"{rule.Label}: {rule.CurrentValueLabel} -> {proposed}\n\n" +
                                    "Forcing this transition may cause:\n" +
                                    "• Loss of historical caravan exploration data\n" +
                                    "• Stale incursion intelligence classifications\n" +
                                    "• Desynchronised multi-vector raid staging states\n" +
                                    "• Inconsistent dynamic site concealment\n\n" +
                                    "This change will be recorded in the campaign's permanent audit ledger.\n\n" +
                                    "Are you certain you wish to proceed?";
                Dialog_MessageBox unsafeBox = Dialog_MessageBox.CreateConfirmation(
                    unsafeText,
                    () => campaign.ForceApplyUnsafe(rule, proposed),
                    destructive: true,
                    title: "Unsafe Campaign Rule Override");
                Find.WindowStack?.Add(unsafeBox);
                break;
        }
    }
}
