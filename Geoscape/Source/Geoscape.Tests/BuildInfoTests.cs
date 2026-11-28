using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectVigilance.Geoscape;

namespace Geoscape.Tests;

[TestClass]
public sealed class BuildInfoTests
{
    [TestMethod]
    public void FormatBuildDetails_ReturnsDeterministicBuildNumberAndCommitDate()
    {
        string buildDetails = GeoscapeMod.FormatBuildDetails();

        Assert.IsFalse(string.IsNullOrWhiteSpace(buildDetails));
        StringAssert.Contains(buildDetails, $"v{BuildInfo.Version}.{BuildInfo.BuildNumber}");
        StringAssert.Contains(buildDetails, BuildInfo.Configuration);
        StringAssert.Contains(buildDetails, BuildInfo.CommitDate);
        Assert.IsFalse(buildDetails.Contains("UTC"), "Build provenance must be commit-deterministic, not wall-clock timestamped.");
    }

    [TestMethod]
    public void BuildInfoConstants_ArePopulatedAndDeterministic()
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(BuildInfo.Version));
        Assert.IsTrue(int.TryParse(BuildInfo.BuildNumber, out int buildNumber) && buildNumber >= 0);
        Assert.IsFalse(string.IsNullOrWhiteSpace(BuildInfo.CommitHash));
        Assert.IsTrue(
            BuildInfo.CommitDate == "unknown" || System.Text.RegularExpressions.Regex.IsMatch(BuildInfo.CommitDate, @"^\d{4}-\d{2}-\d{2}$"),
            $"Expected ISO commit date (YYYY-MM-DD) or 'unknown', got '{BuildInfo.CommitDate}'.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(BuildInfo.Configuration));
    }
}
