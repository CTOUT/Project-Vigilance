using System;
using Verse;

namespace ProjectVigilance.Geoscape.Rules;

/// <summary>
/// Session-only, DevMode-gated developer override allowing unsafe campaign rule changes.
/// Resets to disabled upon game exit or when Prefs.DevMode is switched off.
/// </summary>
public static class UnsafeRuleOverride
{
    [ThreadStatic]
    private static bool requested;

    [ThreadStatic]
    private static Func<bool>? devModeProviderOverride;

    public static Func<bool>? DevModeProviderOverride
    {
        get => devModeProviderOverride;
        set => devModeProviderOverride = value;
    }

    private static bool DevMode => devModeProviderOverride?.Invoke() ?? Prefs.DevMode;

    /// <summary>
    /// The override is only effective when explicitly requested AND Prefs.DevMode is active.
    /// </summary>
    public static bool Effective => requested && DevMode;

    public static bool Requested => requested;

    public static void SetRequested(bool value, string reason = "")
    {
        bool before = Effective;
        requested = value;
        bool after = Effective;

        if (before != after)
        {
            RecordTransition(after, string.IsNullOrEmpty(reason) ? (after ? "Toggled on" : "Toggled off") : reason);
        }
    }

    public static void Notify_DevModeChanged(bool devModeNow)
    {
        if (devModeNow && requested)
        {
            RecordTransition(true, "Developer mode switched on while override requested");
        }
        else if (!devModeNow && requested)
        {
            requested = false;
            RecordTransition(false, "Developer mode switched off");
        }
    }

    public static void Notify_CampaignActivated(GeoscapeCampaignRules rules, bool fromLoad)
    {
        if (rules != null && Effective)
        {
            rules.Record(
                RuleChangeKind.DevOverrideEnabled,
                note: fromLoad
                    ? "Override active when campaign was loaded"
                    : "Override active when campaign was created");
        }
    }

    private static void RecordTransition(bool enabled, string note)
    {
        GeoscapeCampaignRules? active = GeoscapeCampaignRules.Active;
        if (active != null)
        {
            active.Record(
                enabled ? RuleChangeKind.DevOverrideEnabled : RuleChangeKind.DevOverrideDisabled,
                note: note);
        }
    }

    public static void ResetForTesting()
    {
        requested = false;
        devModeProviderOverride = null;
    }
}
