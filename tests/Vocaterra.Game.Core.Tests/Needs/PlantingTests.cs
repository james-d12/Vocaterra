using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Needs;

/// <summary>The berry planted at the camp (N08, design decision 14): the rule in data, its validator's refusals and its optionality.</summary>
public sealed class PlantingTests
{
    private static NeedsData Needs => TestGame.Data.Needs;

    private static IReadOnlyList<string> Problems(PlantingRule rule) => GameDataValidator.Validate(TestGame.Data.WithNeeds(Needs with { Planting = rule }));

    private static PlantingRule Good() => new("berry", "bush", "berry-bush", 8, 6);

    [Fact]
    public void TheShippedPlantingIsTheDesignsAndHoldsTogether()
    {
        Assert.Equal(Good(), Needs.Planting);
        Assert.Empty(GameDataValidator.Validate(TestGame.Data));
    }

    [Fact]
    public void TheValidatorRefusesAnUnknownItemOrBlock()
    {
        Assert.Contains(Problems(Good() with { Item = "moon-cheese" }), static p => p.Contains("'moon-cheese', which is not an item", StringComparison.Ordinal));
        Assert.Contains(Problems(Good() with { Block = "moon-bush" }), static p => p.Contains("'moon-bush', which is not a block", StringComparison.Ordinal));
        Assert.Contains(Problems(Good() with { GrownBlock = "moon-bush" }), static p => p.Contains("'moon-bush', which is not a block", StringComparison.Ordinal));
    }

    [Fact]
    public void TheValidatorRefusesAGrownBlockWithNoRegrowthRuleAndAPlantedBlockItWouldNotRegrowFrom()
    {
        Assert.Contains(Problems(Good() with { GrownBlock = "trunk" }), static p => p.Contains("which has no regrowth rule", StringComparison.Ordinal));
        Assert.Contains(Problems(Good() with { Block = "grass" }), static p => p.Contains("regrows from 'bush'", StringComparison.Ordinal));
    }

    [Fact]
    public void TheValidatorRefusesARadiusOrACountOfZeroOrLess()
    {
        Assert.Contains(Problems(Good() with { RadiusBlocks = 0 }), static p => p.Contains("radius is 0", StringComparison.Ordinal));
        Assert.Contains(Problems(Good() with { RadiusBlocks = -2 }), static p => p.Contains("radius is -2", StringComparison.Ordinal));
        Assert.Contains(Problems(Good() with { MaxBushes = 0 }), static p => p.Contains("allows 0 bushes", StringComparison.Ordinal));
    }

    [Fact]
    public void ThePlantingIsOptionalInTheFile()
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse(GameData.HostText(TestGame.Bundle, NeedsData.RelativePath))!.AsObject();
        _ = json.Remove("planting");

        var parsed = NeedsData.Parse(json.ToJsonString());

        Assert.Null(parsed.Planting);
        Assert.Empty(GameDataValidator.Validate(TestGame.Data.WithNeeds(parsed)));
    }
}
