using System;

namespace ProjectVigilance.Geoscape.Rules;

/// <summary>
/// Bitmask declaring automated engine routines executed when a permitted rule change occurs.
/// </summary>
[Flags]
public enum RuleTransitionEffects
{
    None                  = 0,
    RebuildWorldLayers    = 1 << 0, // Haze and radar meshes, surface layers, and feature labels
    RescanSensors         = 1 << 1, // SensoryManager.Notify_SensorRadiusChanged() + PerformSensorySweep()
    ReleaseConcealedSites = 1 << 2, // HazeReconnaissanceManager.ReleaseAllConcealedSites()
    RepathPlayerCaravans  = 1 << 3  // OptimisticPathingUtility.RepathMovingPlayerCaravans()
}
