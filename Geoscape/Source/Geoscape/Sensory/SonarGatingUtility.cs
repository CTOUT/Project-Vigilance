using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Centralized gating condition for offering coastal sonar deployment actions.
/// Restricts sonar deployment to Developer Mode in Milestone 1.3, requiring active coastal water adjacency.
/// </summary>
public static class SonarGatingUtility
{
    /// <summary>
    /// Evaluates whether the specified planet tile meets all criteria for offering sonar outpost deployment.
    /// </summary>
    public static bool ShouldOfferSonarDeployment(PlanetTile tile)
    {
        return Prefs.DevMode
            && SonarTraversalUtility.CanEvaluateWorldGrid()
            && SonarTraversalUtility.HasCoastalAccess(tile);
    }
}
