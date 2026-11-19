using Verse;

namespace ProjectVigilance.Geoscape.Orbital;

/// <summary>
/// Triggers deferred post-load orbital deduplication and infrastructure reconciliation once
/// ScribeLoader.FinalizeLoading() has resolved all cross-references (WorldObject.Faction,
/// Settlement.Faction), including when the mod is added to an existing save.
/// </summary>
public class GameComponent_OrbitalLifecycle : GameComponent
{
    public GameComponent_OrbitalLifecycle(Game game)
    {
    }

    public override void LoadedGame()
    {
        base.LoadedGame();
        OrbitalManager.Instance?.CompletePostLoadReconciliation();
    }
}
