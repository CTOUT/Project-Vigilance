using HarmonyLib;
using ProjectVigilance.Geoscape.Rules;
using Verse;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Monitors changes to Prefs.DevMode to automatically clear the unsafe rule override
/// and record a DevOverrideDisabled event in the active campaign's audit ledger.
/// </summary>
[HarmonyPatch(typeof(Prefs), nameof(Prefs.DevMode), MethodType.Setter)]
public static class Prefs_DevMode_Patch
{
    public static void Postfix(bool value)
    {
        UnsafeRuleOverride.Notify_DevModeChanged(value);
    }
}
