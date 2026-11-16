using Vocaterra.Game.Core.Tests.TestSupport;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

public sealed class TempGoldenSearchTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(42UL)]
    [InlineData(7UL)]
    [InlineData(2026UL)]
    public void Search(ulong seed)
    {
        foreach (var feature in new[] { Feature.River, Feature.Lake, Feature.Coast, Feature.Forest, Feature.Mountain, Feature.Plain })
        {
            var (x, z) = ProceduralTestWorld.Find(seed, feature, 0);
            output.WriteLine($"FOUND {seed} {feature} {x} {z} {ProceduralGoldenTests.ColumnDigest(seed, x, z)}");
        }
    }
}
