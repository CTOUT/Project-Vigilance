using HarmonyLib;
using Verse;
using ProjectVigilance.Geoscape.Sensory;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Harmony patch intercepting instant global announcement letters for dynamic world sites
/// spawned in unmonitored Haze tiles, deferring notification until uncovered by radar or reconnaissance.
/// </summary>
[HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ReceiveLetter), typeof(Letter), typeof(string), typeof(int), typeof(bool))]
public static class LetterStack_ReceiveLetter_Patch
{
    public static bool Prefix(Letter let)
    {
        if (let == null) return true;

        if (HazeReconnaissanceManager.Instance != null &&
            HazeReconnaissanceManager.Instance.TrySuppressLetter(let))
        {
            return false;
        }

        return true;
    }
}
