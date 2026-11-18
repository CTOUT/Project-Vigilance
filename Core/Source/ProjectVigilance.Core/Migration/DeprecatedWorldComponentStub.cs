using RimWorld.Planet;

namespace ProjectVigilance.Core.Migration;

/// <summary>
/// Inert WorldComponent stub safely absorbing serialized XML data when a previously active
/// module WorldComponent (such as OrbitalManager) is not loaded in the current mod list.
/// </summary>
public class DeprecatedWorldComponentStub : WorldComponent
{
    public DeprecatedWorldComponentStub(RimWorld.Planet.World world) : base(world)
    {
    }

    public DeprecatedWorldComponentStub() : base(null!)
    {
    }

    public override void ExposeData()
    {
        base.ExposeData();
    }
}
