using ProjectVigilance.Core.Modules;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Centralized gating condition for offering coastal sonar deployment actions.
/// Restricts sonar deployment to Developer Mode in Milestone 1.3–1.5, requiring both Aquatics module activation and coastal water adjacency.
/// </summary>
public static class SonarGatingUtility
{
    internal static System.Func<bool>? DevModeOverrideForTesting { get; set; }

    /// <summary>
    /// Evaluates whether the specified planet tile meets all criteria for offering sonar outpost deployment.
    /// </summary>
    public static bool ShouldOfferSonarDeployment(PlanetTile tile)
    {
        bool devMode = DevModeOverrideForTesting?.Invoke() ?? Prefs.DevMode;
        return devMode
            && (VigilanceModuleManager.IsAquaticsActive || VigilanceModuleManager.AllowSonarInTests)
            && SonarTraversalUtility.CanEvaluateWorldGrid()
            && SonarTraversalUtility.HasCoastalAccess(tile);
    }
}
