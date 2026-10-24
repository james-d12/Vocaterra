namespace Vocaterra.Simulation.Tests;

/// <summary>The SplitMix64 generator, its derivation per pass and chunk, and the integer value noise.</summary>
public sealed class WorldRngTests
{
    [Fact]
    public void SplitMix64MatchesTheReferenceVectorsForSeedZero()
    {
        var rng = new WorldRng(0);
        Assert.Equal(0xE220A8397B1DCDAFUL, rng.NextUInt64());
        Assert.Equal(0x6E789E6AA1B965F4UL, rng.NextUInt64());
        Assert.Equal(0x06C45D188009454FUL, rng.NextUInt64());
    }

    [Fact]
    public void DerivationIsStableAndSeparatesPassesChunksAndSeeds()
    {
        var a = WorldRng.For(42, "forest", 3, 1).NextUInt64();
        Assert.Equal(a, WorldRng.For(42, "forest", 3, 1).NextUInt64());
        Assert.NotEqual(a, WorldRng.For(42, "terrain", 3, 1).NextUInt64());
        Assert.NotEqual(a, WorldRng.For(42, "forest", 1, 3).NextUInt64());
        Assert.NotEqual(a, WorldRng.For(42, "forest", 3, 2).NextUInt64());
        Assert.NotEqual(a, WorldRng.For(43, "forest", 3, 1).NextUInt64());
    }

    [Fact]
    public void BoundedDrawsStayInRangeAndCoverIt()
    {
        var rng = WorldRng.For(7, "test");
        var seen = new bool[6];
        for (var i = 0; i < 600; i++)
        {
            var value = rng.NextInt(2, 8);
            Assert.InRange(value, 2, 7);
            seen[value - 2] = true;
        }

        Assert.All(seen, Assert.True);
    }
}
