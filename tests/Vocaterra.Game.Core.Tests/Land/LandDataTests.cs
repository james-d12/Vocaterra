using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Land;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Land;

/// <summary>The land rules file (<c>game/land.json</c>, Y09): its values, its shape's refusals and the validator's refusals.</summary>
public sealed class LandDataTests
{
    private const string Valid = """
        { "schemaVersion": 1,
          "protectedSites": [ { "site": "village", "protection": "village", "margin": 2, "includeBlend": true }, { "site": "camp.guide", "protection": "camp", "margin": 2 } ],
          "protectedBlocks": [ { "blocks": ["path", "bridge"], "protection": "road", "margin": 2 } ],
          "eligible": [ { "site": "meadow.north", "radius": 22 }, { "site": "meadow.south", "radius": 22 } ],
          "plot": { "width": 10, "depth": 10, "price": 10, "maxSlope": 1 } }
        """;

    private static IReadOnlyList<string> LandProblems(LandData land) =>
        GameDataValidator.Validate(TestGame.Data.WithLand(land)).Where(static p => p.StartsWith("game/land.json", StringComparison.Ordinal)).ToList();

    private static LandData Edited(string from, string to) => LandData.Parse(Valid.Replace(from, to, StringComparison.Ordinal));

    [Fact]
    public void Given_TheShippedFile_When_ItIsLoaded_Then_ItHoldsTheMeasuredNumbersOfTheSettlementPlan()
    {
        var land = TestGame.Data.Land;

        Assert.Equal([("village", Protection.Village, 2, true), ("camp.guide", Protection.Camp, 2, false)], land.ProtectedSites.Select(static s => (s.Site, s.Protection, s.Margin, s.IncludeBlend)));
        var roads = Assert.Single(land.ProtectedBlocks);
        Assert.Equal(["path", "bridge"], roads.Blocks);
        Assert.Equal((Protection.Road, 2), (roads.Protection, roads.Margin));
        Assert.Equal([("meadow.north", 22), ("meadow.south", 22)], land.Eligible.Select(static m => (m.Site, m.Radius)));
        Assert.Equal((10, 10, 10, 1), (land.PlotWidth, land.PlotDepth, land.PlotPrice, land.MaxSlope));
    }

    [Fact]
    public void Given_TheShippedFile_When_TheContentIsValidated_Then_TheLandRulesHaveNoProblem() => Assert.Empty(LandProblems(TestGame.Data.Land));

    [Fact]
    public void Given_AValidFile_When_ItIsParsedAndValidated_Then_NoProblemIsFound() => Assert.Empty(LandProblems(LandData.Parse(Valid)));

    [Fact]
    public void Given_AProtectedSiteTheSceneLacks_When_TheContentIsValidated_Then_TheProblemNamesTheSite()
    {
        var problems = LandProblems(Edited("\"site\": \"camp.guide\"", "\"site\": \"camp.nowhere\""));

        var problem = Assert.Single(problems);
        Assert.Contains("camp.nowhere", problem, StringComparison.Ordinal);
        Assert.Contains("scene.json", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_AMeadowTheSceneLacks_When_TheContentIsValidated_Then_TheProblemNamesTheSite()
    {
        var problem = Assert.Single(LandProblems(Edited("meadow.south", "meadow.moon")));

        Assert.Contains("meadow.moon", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_AProtectedSiteWithNoRadius_When_TheContentIsValidated_Then_TheDiscIsRefusedAsEmpty()
    {
        // meadow.north is a point of the scene with no radius and no blend: a margin of 0 leaves a disc of nothing.
        var problem = Assert.Single(LandProblems(Edited("\"site\": \"camp.guide\", \"protection\": \"camp\", \"margin\": 2", "\"site\": \"meadow.north\", \"protection\": \"camp\", \"margin\": 0")));

        Assert.Contains("covers nothing", problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"width\": 10", "\"width\": 0", "width and depth")]
    [InlineData("\"depth\": 10", "\"depth\": -4", "width and depth")]
    [InlineData("\"price\": 10", "\"price\": -1", "price")]
    [InlineData("\"maxSlope\": 1", "\"maxSlope\": -1", "maxSlope")]
    [InlineData("\"radius\": 22 }, { \"site\": \"meadow.south\"", "\"radius\": 0 }, { \"site\": \"meadow.south\"", "radius")]
    [InlineData("\"margin\": 2, \"includeBlend\": true", "\"margin\": -1, \"includeBlend\": true", "margin")]
    [InlineData("\"blocks\": [\"path\", \"bridge\"]", "\"blocks\": [\"path\", \"cobblepath\"]", "cobblepath")]
    public void Given_ANumberOrNameThatIsWrong_When_TheContentIsValidated_Then_OneProblemNamesIt(string from, string to, string mention)
    {
        var problem = Assert.Single(LandProblems(Edited(from, to)));

        Assert.Contains(mention, problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_APriceOfZero_When_TheContentIsValidated_Then_ItIsAllowed() => Assert.Empty(LandProblems(Edited("\"price\": 10", "\"price\": 0")));

    [Fact]
    public void Given_NoEligibleRegion_When_TheContentIsValidated_Then_TheProblemSaysNoPlotCouldBeBought()
    {
        var land = LandData.Parse(Valid) with { Eligible = [] };

        Assert.Contains("eligible", Assert.Single(LandProblems(land)), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"protection\": \"village\", \"margin\"", "\"protection\": \"keep\", \"margin\"")]
    [InlineData("\"plot\": { \"width\": 10, ", "\"plot\": { ")]
    [InlineData("\"schemaVersion\": 1", "\"schemaVersion\": 2")]
    public void Given_AFileWithABadShape_When_ItIsParsed_Then_ItIsRefused(string from, string to) =>
        _ = Assert.Throws<InvalidDataException>(() => LandData.Parse(Valid.Replace(from, to, StringComparison.Ordinal)));

    [Fact]
    public void Given_TextThatIsNotJson_When_ItIsParsed_Then_ItIsRefused() => _ = Assert.Throws<InvalidDataException>(() => LandData.Parse("{ not json"));
}
