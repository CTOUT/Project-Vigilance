using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape;

namespace Geoscape.Tests;

[TestClass]
public sealed class BuildInfoTests
{
    [TestMethod]
    public void FormatBuildDetails_ReturnsFormattedVersionAndDate()
    {
        string buildDetails = GeoscapeMod.FormatBuildDetails();

        Assert.IsFalse(string.IsNullOrWhiteSpace(buildDetails));
        StringAssert.Contains(buildDetails, "v0.4.1");
        StringAssert.Contains(buildDetails, "Built ");
        StringAssert.Contains(buildDetails, "UTC");
    }

    [TestMethod]
    public void BuildInfoConstants_ArePopulated()
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(BuildInfo.Version));
        Assert.IsFalse(string.IsNullOrWhiteSpace(BuildInfo.BuildDate));
        Assert.IsFalse(string.IsNullOrWhiteSpace(BuildInfo.Configuration));
    }
}
