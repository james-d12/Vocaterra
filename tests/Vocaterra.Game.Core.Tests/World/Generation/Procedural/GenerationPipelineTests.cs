using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Generation;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>The pipeline's passes (B03): a fixed order registered in one place, every name its own and none the valley's.</summary>
public sealed class GenerationPipelineTests
{
    // The keys the valley's builder draws and samples noise by: SceneBuilder's TreePassName and the passes named in its comments and constants.
    private static readonly string[] ValleyNames = ["terrain", "forest", "trees", "treeshape", "undergrowth"];

    [Fact]
    public void Given_TheShippedPipeline_When_ItsPassesAreListed_Then_TheyRunInTheFixedOrderTheyAreRegisteredIn()
    {
        var names = ProceduralTestWorld.Source(42).Pipeline.PassNames;

        Assert.Equal(["gen/relief", "gen/banks", "gen/waterside", "gen/surface", "gen/water", "gen/stone", "gen/strata", "gen/cliffs", "gen/floor", "gen/caves", "gen/entrances", "gen/lava", "gen/ores", "gen/linings", "gen/trees", "gen/undergrowth"], names);
        Assert.Equal(3, ProceduralTestWorld.Source(42).Pipeline.Shapers.Count);
        Assert.Equal(13, ProceduralTestWorld.Source(42).Pipeline.Decorators.Count);
    }

    [Fact]
    public void Given_TheShippedPipeline_When_ItsPassNamesAreCompared_Then_TheyAreUniqueAndDifferFromTheValleys()
    {
        var names = ProceduralTestWorld.Source(42).Pipeline.PassNames;

        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(names.Intersect(ValleyNames, StringComparer.Ordinal));
        Assert.Equal(SceneBuilder.TreePassName, ValleyNames[2]); // the list above is the valley's
    }
}
