using ProjectVigilance.Geoscape.Sensory;
using RimWorld.Planet;
using Verse;

namespace ProjectVigilance.Geoscape.Rules;

/// <summary>
/// Executes operational side-effects (cache flushes, sweeps, caravan repathing, mesh rebuilds)
/// when a campaign rule transition occurs.
/// </summary>
public static class RuleTransitionExecutor
{
    public static void Execute(RuleTransitionEffects effects)
    {
        if (effects == RuleTransitionEffects.None)
        {
            return;
        }

        if (Current.ProgramState != ProgramState.Playing || Find.World == null)
        {
            return;
        }

        // 1. Release concealed dynamic sites before rescanning vision
        if (effects.HasFlag(RuleTransitionEffects.ReleaseConcealedSites))
        {
            HazeReconnaissanceManager.Instance?.ReleaseAllConcealedSites();
        }

        // 2. Invalidate sensory caches and trigger an immediate sweep
        if (effects.HasFlag(RuleTransitionEffects.RescanSensors))
        {
            SensoryManager.Instance?.Notify_SensorRadiusChanged();
            SensoryManager.Instance?.PerformSensorySweep();
        }

        // 3. Re-evaluate paths for active player caravans
        if (effects.HasFlag(RuleTransitionEffects.RepathPlayerCaravans))
        {
            OptimisticPathingUtility.RepathMovingPlayerCaravans();
        }

        // 4. Invalidate 3D GPU mesh layers and feature labels
        if (effects.HasFlag(RuleTransitionEffects.RebuildWorldLayers))
        {
            WorldDrawLayer_HazeOfWar.Instance?.SetDirty();
            WorldDrawLayer_RadarCoverage.Instance?.SetDirty();

            if (Find.WorldGrid?.Surface != null)
            {
                Find.WorldGrid.Surface.SetAllLayersDirty();
            }

            Find.WorldFeatures?.UpdateFeatures();
        }
    }
}
