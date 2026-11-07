using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape.Sensory;
using ProjectVigilance.Geoscape.World;
using RimWorld.Planet;

namespace Geoscape.Tests;

[TestClass]
public sealed class IncursionPhysiologyRegressionTests
{
    [TestMethod]
    public void Incursion_TransitionToOverlandRetreat_ConfiguresRetreatProperties()
    {
        var incursion = new WorldObject_Incursion();
        incursion.SetSpawnedRaw(true);
        incursion.SetTileRaw((PlanetTile)100);

        incursion.TransitionToOverlandRetreat(new List<Verse.Pawn>(), null, null);

        Assert.IsTrue(incursion.IsRetreating, "Incursion must be marked as retreating.");
        Assert.IsTrue(incursion.IsIdentified, "Incursion must be marked as identified upon retreat.");
        Assert.AreEqual(IntelligenceTier.Classified, incursion.CurrentIntelligenceTier, "Incursion must have Classified tier upon retreat.");
        Assert.IsFalse(incursion.IsEngagedInTacticalMap, "Incursion must disengage from tactical map.");
    }
}
