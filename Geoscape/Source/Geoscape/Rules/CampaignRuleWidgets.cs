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
    // WCAG AA compliant contrast ratio (>4.5:1) on dark backgrounds
    private static readonly Color DisabledTextColor = new(1f, 1f, 1f, 0.55f);

    public static void DrawBool(
        Listing_Standard listing,
        CampaignRule<bool> rule,
        GeoscapeCampaignRules? campaign,
        RuleEditContext ctx,
        bool showLockPreview = false,
        bool enabled = true)
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

        bool disabled = !enabled || (verdict == RuleTransitionVerdict.Blocked && !ctx.UnsafeOverrideEnabled);
        bool valRef = current;

        if (disabled)
        {
            GUI.color = DisabledTextColor;
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
        bool showLockPreview = false,
        bool enabled = true,
        bool roundToInt = true,
        float? effectiveMax = null,
        string? effectiveMaxTooltip = null)
    {
        float current = roundToInt ? Mathf.Round(rule.Value) : rule.Value;
        float capMax = effectiveMax.HasValue ? Mathf.Clamp(effectiveMax.Value, min, max) : max;
        RuleTransitionVerdict testVerdict = rule.Evaluate(current + 1.0f, ctx);
        bool isLocked = !enabled || (capMax <= min) || (testVerdict == RuleTransitionVerdict.Blocked && !ctx.UnsafeOverrideEnabled);

        string header = $"{rule.Label}: {rule.CurrentValueLabel}";
        if (showLockPreview && rule.Mutability == RuleMutability.Immutable)
        {
            header += " [Locked after creation]";
        }
        else if (testVerdict == RuleTransitionVerdict.Blocked && !ctx.UnsafeOverrideEnabled)
        {
            header += " [Locked]";
        }
        else if (testVerdict == RuleTransitionVerdict.RequiresUnsafeConfirmation)
        {
            header += " [Unsafe Override]";
        }

        if (isLocked)
        {
            GUI.color = DisabledTextColor;
            listing.Label(header, tooltip: rule.Description);
            Rect lockedRect = listing.GetRect(22f);
            Widgets.HorizontalSlider(lockedRect, current, min, max, middleAlignment: true);
            DrawSliderCapOverlay(lockedRect, min, max, capMax, effectiveMaxTooltip);
            GUI.color = Color.white;
            return;
        }

        listing.Label(header, tooltip: rule.Description);
        Rect sliderRect = listing.GetRect(22f);
        float rawSlider = Widgets.HorizontalSlider(sliderRect, current, min, max, middleAlignment: true);
        DrawSliderCapOverlay(sliderRect, min, max, capMax, effectiveMaxTooltip);

        float newRadius = roundToInt ? Mathf.Round(rawSlider) : rawSlider;
        newRadius = Mathf.Min(newRadius, capMax);
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

    public static void DrawRuleOptionRadio<T>(
        Listing_Standard listing,
        T option,
        string label,
        string tooltip,
        CampaignRule<T> rule,
        GeoscapeCampaignRules? campaign,
        RuleEditContext ctx,
        string? recommendationSuffix = null,
        string blockedSuffix = " (Locked)") where T : notnull
    {
        bool isSelected = EqualityComparer<T>.Default.Equals(rule.Value, option);
        RuleTransitionVerdict verdict = isSelected ? RuleTransitionVerdict.NoChange : rule.Evaluate(option, ctx);

        string displayLabel = label;
        if (!string.IsNullOrEmpty(recommendationSuffix) && !displayLabel.Contains(recommendationSuffix))
        {
            displayLabel += recommendationSuffix;
        }

        if (verdict == RuleTransitionVerdict.Blocked)
        {
            displayLabel += blockedSuffix;
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
            GUI.color = DisabledTextColor;
        }

        if (listing.RadioButton(displayLabel, isSelected, tabIn: 0f, tooltip: tooltip, tooltipDelay: null, disabled: disabled))
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
        string? rec = (showLockPreview && (option == VisibilityMode.FogOfWar || option == VisibilityMode.ClassicHazeOfWar)) ? " (Recommended)" : null;
        DrawRuleOptionRadio(listing, option, label, tooltip, rule, campaign, ctx, rec, " (Locked - Cannot reverse)");
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
        DrawRuleOptionRadio(listing, option, label, tooltip, rule, campaign, ctx);
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
                Dialog_MessageBox escBox = new(
                    escText,
                    "Escalate Rule",
                    () => campaign.ForceApplyEscalation(rule, proposed),
                    "Keep Current",
                    null,
                    "Confirm Campaign Rule Escalation");
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

    private static void DrawSliderCapOverlay(
        Rect sliderRect,
        float min,
        float max,
        float capMax,
        string? tooltip)
    {
        if (capMax >= max - 0.001f)
        {
            return;
        }

        float capFraction = Mathf.Clamp01(Mathf.InverseLerp(min, max, capMax));
        float capX = sliderRect.x + sliderRect.width * capFraction;
        Rect blockedRect = new Rect(
            capX,
            sliderRect.y + 5f,
            Mathf.Max(0f, sliderRect.xMax - capX),
            sliderRect.height - 10f);

        Widgets.DrawBoxSolid(blockedRect, new Color(0.10f, 0.11f, 0.13f, 0.80f));
        Widgets.DrawBoxSolid(
            new Rect(capX - 1f, sliderRect.y + 3f, 2f, sliderRect.height - 6f),
            new Color(0.60f, 0.64f, 0.68f, 0.75f));

        if (!string.IsNullOrEmpty(tooltip))
        {
            TooltipHandler.TipRegion(blockedRect, tooltip);
        }
    }
}
