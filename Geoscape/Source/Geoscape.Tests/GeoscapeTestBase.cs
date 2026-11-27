using Microsoft.VisualStudio.TestTools.UnitTesting;
using RimWorld.Planet;

namespace Geoscape.Tests;

/// <summary>
/// Common base test fixture providing standardized lifecycle initialization, mock world seeding,
/// and static state reset across Project Vigilance test suites.
/// </summary>
[DoNotParallelize]
public abstract class GeoscapeTestBase
{
    protected static PlanetTile MakeTile(int id) => GeoscapeTestHarness.MakeTile(id);

    [TestInitialize]
    public virtual void Setup()
    {
        GeoscapeTestHarness.ResetAllStaticState();
        GeoscapeTestHarness.EnsureEnvironmentInitialised();
    }

    [TestCleanup]
    public virtual void Cleanup()
    {
        GeoscapeTestHarness.ResetAllStaticState();
    }
}
