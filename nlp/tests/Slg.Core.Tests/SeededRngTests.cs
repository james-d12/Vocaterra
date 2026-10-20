using Slg.Core.Determinism;

namespace Slg.Core.Tests;

/// <summary>Tests for <see cref="SeededRng"/> and <see cref="SplitMix64"/>.</summary>
public class SeededRngTests
{
    // Golden values cross-checked against an independent Python implementation of SplitMix64 + xoshiro256**.
    private static readonly ulong[] Seed12345First10 =
    [
        0xBE6A36374160D49BUL, 0x214AAA0637A688C6UL, 0xF69D16DE9954D388UL, 0x0C60048C4E96E033UL, 0x8E2076AEED51C648UL,
        0x02BBCC1C1FC50F84UL, 0x28E72A4FEC84F699UL, 0x4BB9D7CBB8DDDEBEUL, 0x62CEA6A22CF0BD36UL, 0xE91DF042CCDE955DUL,
    ];

    [Fact]
    public void SplitMix64MatchesStandardVectorForSeedZero()
    {
        var mix = new SplitMix64(0);
        Assert.Equal(0xE220A8397B1DCDAFUL, mix.Next());
        Assert.Equal(0x6E789E6AA1B965F4UL, mix.Next());
        Assert.Equal(0x06C45D188009454FUL, mix.Next());
        Assert.Equal(0xF88BB8A8724C81ECUL, mix.Next());
    }

    [Fact]
    public void FirstTenOutputsForSeed12345AreGolden()
    {
        var rng = new SeededRng(12345);
        foreach (var expected in Seed12345First10)
        {
            Assert.Equal(expected, rng.NextUInt64());
        }
    }

    [Fact]
    public void SameSeedGivesIdenticalSequences()
    {
        var a = new SeededRng(987654321);
        var b = new SeededRng(987654321);
        for (var i = 0; i < 1000; i++)
        {
            Assert.Equal(a.NextUInt64(), b.NextUInt64());
        }
    }

    [Fact]
    public void DifferentSeedsGiveDifferentSequences()
    {
        Assert.NotEqual(new SeededRng(1).NextUInt64(), new SeededRng(2).NextUInt64());
    }

    [Fact]
    public void ForInteractionUsesSeed64OfInvariantParts()
    {
        var expected = new SeededRng(0x36ECCE18DF832932UL);
        var actual = SeededRng.ForInteraction(42, "blacksmith", 7);
        Assert.Equal(0xBA2D10E6356B5B3AUL, actual.NextUInt64());
        Assert.Equal(0xBA2D10E6356B5B3AUL, expected.NextUInt64());
        Assert.Equal(expected.NextUInt64(), actual.NextUInt64());
        Assert.Equal(expected.NextUInt64(), actual.NextUInt64());
    }

    [Fact]
    public void ForInteractionDiffersByCounterNpcAndWorldSeed()
    {
        var baseline = SeededRng.ForInteraction(1, "npc", 0).NextUInt64();
        Assert.NotEqual(baseline, SeededRng.ForInteraction(1, "npc", 1).NextUInt64());
        Assert.NotEqual(baseline, SeededRng.ForInteraction(1, "other", 0).NextUInt64());
        Assert.NotEqual(baseline, SeededRng.ForInteraction(2, "npc", 0).NextUInt64());
        Assert.Equal(baseline, SeededRng.ForInteraction(1, "npc", 0).NextUInt64());
    }

    [Fact]
    public void NextIntStaysInRange()
    {
        var rng = new SeededRng(7);
        var seen = new bool[7];
        for (var i = 0; i < 10_000; i++)
        {
            var value = rng.NextInt(7);
            Assert.InRange(value, 0, 6);
            seen[value] = true;
        }

        Assert.All(seen, Assert.True);
    }

    [Fact]
    public void NextIntMatchesRejectionSamplingReference()
    {
        var rng = new SeededRng(12345);
        int[] expected = [3, 8, 6, 5, 4];
        foreach (var value in expected)
        {
            Assert.Equal(value, rng.NextInt(10));
        }
    }

    [Fact]
    public void NextIntOfOneAlwaysReturnsZero()
    {
        var rng = new SeededRng(3);
        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(0, rng.NextInt(1));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void NextIntRejectsNonPositiveBound(int bound)
    {
        var rng = new SeededRng(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(bound));
    }

    [Fact]
    public void NextDoubleUsesTopFiftyThreeBitsAndStaysBelowOne()
    {
        var rng = new SeededRng(12345);
        Assert.Equal(0.7438081631565894, rng.NextDouble());

        var other = new SeededRng(99);
        for (var i = 0; i < 10_000; i++)
        {
            var value = other.NextDouble();
            Assert.True(value >= 0.0 && value < 1.0);
        }
    }

    [Fact]
    public void PickReturnsElementsDeterministically()
    {
        string[] items = ["a", "b", "c", "d"];
        var first = new SeededRng(5);
        var second = new SeededRng(5);
        for (var i = 0; i < 50; i++)
        {
            var picked = first.Pick(items);
            Assert.Contains(picked, items);
            Assert.Equal(picked, second.Pick(items));
        }

        Assert.Throws<ArgumentException>(() => first.Pick(Array.Empty<string>()));
    }

    [Fact]
    public void ShuffleIsAGoldenPermutation()
    {
        var items = Enumerable.Range(0, 10).ToList();
        new SeededRng(12345).Shuffle(items);
        Assert.Equal([2, 6, 9, 5, 1, 8, 7, 0, 4, 3], items);
        Assert.Equal(Enumerable.Range(0, 10), items.Order());
    }
}
