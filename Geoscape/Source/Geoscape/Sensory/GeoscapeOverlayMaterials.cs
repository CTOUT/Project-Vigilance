using UnityEngine;
using Verse;

namespace ProjectVigilance.Geoscape.Sensory;

/// <summary>
/// Pre-caches Geoscape vertex-colour world overlay materials on the main thread during startup
/// (<see cref="StaticConstructorOnStartup"/>) so separate render-queue variants reside in
/// <see cref="MatLoader"/>'s cache before world generation or save loading runs on background threads.
/// </summary>
[StaticConstructorOnStartup]
internal static class GeoscapeOverlayMaterials
{
    internal const string VertexColorTransparentPath = "World/WorldVertexColorTransparent";

    // Retain array reference so RimWorld tracks startup asset initialisation against this [StaticConstructorOnStartup] type.
    private static readonly Material[] PrewarmedMaterials;

    static GeoscapeOverlayMaterials()
    {
        int[] queues = WorldDrawLayer_HazeOfWar.OverlayRenderQueues;
        PrewarmedMaterials = new Material[queues.Length];
        for (int i = 0; i < queues.Length; i++)
        {
            PrewarmedMaterials[i] = MatLoader.LoadMat(VertexColorTransparentPath, queues[i]);
        }
    }
}
