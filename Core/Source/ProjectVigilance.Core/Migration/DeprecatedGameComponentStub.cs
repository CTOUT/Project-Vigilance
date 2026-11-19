using Verse;

namespace ProjectVigilance.Core.Migration;

/// <summary>
/// Inert GameComponent stub safely absorbing serialised XML data when a previously active
/// module GameComponent (such as GameComponent_OrbitalLifecycle) is not loaded in the current mod list.
/// </summary>
public class DeprecatedGameComponentStub : GameComponent
{
    public DeprecatedGameComponentStub(Game game)
    {
    }

    public DeprecatedGameComponentStub()
    {
    }

    public override void ExposeData()
    {
        base.ExposeData();
    }
}
