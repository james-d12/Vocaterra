using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Regions;

namespace Vocaterra.Game.Core.Tests.World.Regions;

/// <summary>The <c>regionBlend</c> section of <c>game/world.json</c> (B06): the shipped numbers, a file without the section, and each validator rule.</summary>
public sealed class RegionBlendDataTests
{
    private static WorldGenData Shipped => TestGame.Data.WorldGen;

    private static IReadOnlyList<string> Problems(WorldGenData world) => WorldGenValidator.Validate(world);

    [Fact]
    public void Given_TheShippedWorldFile_When_TheBlendIsRead_Then_ItIsTheNumbersTheBriefNamesWithThePassCorridorAtTheValleysEastEdge()
    {
        var blend = Shipped.Blend;

        Assert.Equal((64, 3), (blend.Margin, blend.TreeClearance));
        Assert.Equal(new LandBiasSpec(450, 150, 3000, new ErosionBiasSpec(250, -250, 800, 1800, 8000)), blend.LandBias);
        Assert.Equal(new EdgeSmoothingSpec(16, 16), blend.Smoothing);
        Assert.Equal(new HeadwaterSpec(320), blend.Headwater); // a few hundred blocks beyond the margin (B02b)
        var corridor = Assert.Single(blend.ExitCorridors);
        Assert.Equal((383, 208), (corridor.FromX, corridor.FromZ)); // the pass's road ends at x 383, z about 208 (B08a)
        Assert.True(corridor.ToX > corridor.FromX);
        Assert.Empty(Problems(Shipped));
    }

    [Fact]
    public void Given_AWorldFileWithoutTheSection_When_ItIsParsed_Then_TheDefaultBlendWithNoCorridorIsUsed()
    {
        var json = File.ReadAllText(Repo.Path("content", "game-v1", "game", "world.json"));
        var cut = json.IndexOf(",\n  \"regionBlend\"", StringComparison.Ordinal);
        Assert.True(cut > 0);

        var world = WorldGenData.Parse(json[..cut] + "\n}\n");

        Assert.Equal(RegionBlendData.Default, world.Blend);
        Assert.Empty(world.Blend.ExitCorridors);
        Assert.Empty(Problems(world));
    }

    [Fact]
    public void Given_ASectionWithAPartMissing_When_ItIsParsed_Then_ItIsRefused()
    {
        var json = File.ReadAllText(Repo.Path("content", "game-v1", "game", "world.json")).Replace("\"treeClearance\": 3,", string.Empty, StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => WorldGenData.Parse(json));
    }

    [Theory]
    [InlineData(4, 3, 512, "regionBlend.margin")]
    [InlineData(600, 3, 700, "regionBlend.margin")]
    [InlineData(64, 64, 512, "regionBlend.treeClearance")]
    [InlineData(64, -1, 512, "regionBlend.treeClearance")]
    [InlineData(64, 3, 32, "regionBlend.landBias")]
    [InlineData(64, 3, 9000, "regionBlend.landBias")]
    public void Given_ABlendOutOfRange_When_TheWorldIsValidated_Then_ThePartIsNamed(int margin, int clearance, int radius, string part)
    {
        var bad = Shipped with { RegionBlend = Shipped.Blend with { Margin = margin, TreeClearance = clearance, LandBias = Shipped.Blend.LandBias with { Radius = radius } } };

        Assert.Contains(Problems(bad), p => p.Contains(part, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(-1, 16)]
    [InlineData(65, 16)]
    [InlineData(16, 0)]
    [InlineData(16, 65)]
    public void Given_AnEdgeSmoothingOutOfRange_When_TheWorldIsValidated_Then_ItIsRefused(int blurRadius, int ease)
    {
        var bad = Shipped with { RegionBlend = Shipped.Blend with { EdgeSmoothing = new EdgeSmoothingSpec(blurRadius, ease) } };

        Assert.Contains(Problems(bad), static p => p.Contains("edgeSmoothing", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5000)]
    public void Given_AHeadwaterDistanceOutOfRange_When_TheWorldIsValidated_Then_ItIsRefused(int distance)
    {
        var bad = Shipped with { RegionBlend = Shipped.Blend with { HeadwaterSpec = new HeadwaterSpec(distance) } };

        Assert.Contains(Problems(bad), static p => p.Contains("headwater", StringComparison.Ordinal));
    }

    [Fact]
    public void Given_ASectionWithNoHeadwaterPart_When_ItIsParsed_Then_TheDefaultHeadwaterIsUsed()
    {
        var json = File.ReadAllText(Repo.Path("content", "game-v1", "game", "world.json")).Replace("\"headwater\": { \"distance\": 320 },", string.Empty, StringComparison.Ordinal);

        var world = WorldGenData.Parse(json);

        Assert.Equal(HeadwaterSpec.Default, world.Blend.Headwater);
        Assert.Empty(Problems(world));
    }

    [Theory]
    [InlineData(-250, 250, 800, 1800, 8000)] // far above near
    [InlineData(250, -250, 1800, 800, 8000)] // the radii fall
    [InlineData(250, -250, 800, 1800, 20000)] // too far
    [InlineData(2000, -250, 800, 1800, 8000)] // out of range
    public void Given_AnErosionBiasOutOfRange_When_TheWorldIsValidated_Then_ItIsRefused(int near, int far, int from, int to, int fade)
    {
        var bad = Shipped with { RegionBlend = Shipped.Blend with { LandBias = Shipped.Blend.LandBias with { Erosion = new ErosionBiasSpec(near, far, from, to, fade) } } };

        Assert.Contains(Problems(bad), static p => p.Contains("landBias.erosion", StringComparison.Ordinal));
    }

    [Fact]
    public void Given_ALandBiasWithTheShoreAboveTheFloorOrOutOfRange_When_TheWorldIsValidated_Then_ItIsRefused()
    {
        var inverted = Shipped with { RegionBlend = Shipped.Blend with { LandBias = new LandBiasSpec(-300, 100, 512) } };
        var outOfRange = Shipped with { RegionBlend = Shipped.Blend with { LandBias = new LandBiasSpec(2000, -120, 512) } };

        Assert.Contains(Problems(inverted), static p => p.Contains("landBias", StringComparison.Ordinal));
        Assert.Contains(Problems(outOfRange), static p => p.Contains("landBias.floor", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(383, 208, 383, 208, 3, 10, 0)] // no length
    [InlineData(383, 208, 479, 208, 0, 10, 0)] // no width
    [InlineData(383, 208, 479, 208, 3, 0, 0)] // no bank
    [InlineData(383, 208, 479, 208, 3, 10, 200)] // a fade longer than the road
    [InlineData(383, 208, 479, 208, 40, 10, 0)] // too wide
    public void Given_AnExitCorridorOutOfRange_When_TheWorldIsValidated_Then_ItIsRefused(int fromX, int fromZ, int toX, int toZ, int halfWidth, int shoulder, int fade)
    {
        var bad = Shipped with { RegionBlend = Shipped.Blend with { ExitCorridors = [new ExitCorridorSpec("bad", fromX, fromZ, toX, toZ, halfWidth, shoulder, fade)] } };

        Assert.Contains(Problems(bad), static p => p.Contains("exitCorridors", StringComparison.Ordinal));
    }
}
