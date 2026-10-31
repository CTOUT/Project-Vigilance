using System.Text;
using HarmonyLib;
using RimWorld.Planet;
using UnityEngine;
using ProjectVigilance.Geoscape.Orbital;

namespace ProjectVigilance.Geoscape.Patches;

/// <summary>
/// Accelerates surface caravan travel speed by 15% when passing through sectors enveloped by an active
/// navigation satellite positioning beam.
/// </summary>
[HarmonyPatch(typeof(CaravanTicksPerMoveUtility), nameof(CaravanTicksPerMoveUtility.GetTicksPerMove), [typeof(Caravan), typeof(StringBuilder)])]
public static class CaravanTicksPerMoveUtility_GetTicksPerMove_Patch
{
    public const float NavigationSpeedBonusFactor = 0.85f; // 15% faster (fewer ticks per move)

    public static void Postfix(Caravan caravan, StringBuilder explanation, ref int __result)
    {
        if (caravan == null || !caravan.Spawned || !caravan.Tile.Valid)
        {
            return;
        }

        if (OrbitalManager.Instance != null && OrbitalManager.Instance.HasNavigationCoverage(caravan.Tile))
        {
            __result = Mathf.Max(1, Mathf.RoundToInt(__result * NavigationSpeedBonusFactor));
            if (explanation != null)
            {
                explanation.AppendLine();
                explanation.Append("  - Navigation satellite GPS uplink: x0.85");
            }
        }
    }
}
