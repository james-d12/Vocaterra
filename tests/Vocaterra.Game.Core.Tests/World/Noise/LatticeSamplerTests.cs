using Vocaterra.Game.Core.World.Noise;

namespace Vocaterra.Game.Core.Tests.World.Noise;

/// <summary>The coarse lattices (B01): the field read only at lattice points, interpolated between, and Fill equal to At.</summary>
public sealed class LatticeSamplerTests
{
    private static int Source2(int x, int z) => (x * 7) - (z * 3) + ((x ^ z) & 255);

    private static int Source3(int x, int y, int z) => (x * 5) + (y * 11) - (z * 2) + ((x ^ y ^ z) & 127);

    [Fact]
    public void Given_ALattice_When_AColumnOnAPointIsSampled_Then_ItIsTheSourceThere()
    {
        var sampler = new LatticeSampler2(4, Source2);

        foreach (var (x, z) in new[] { (0, 0), (-8, 12), (29_000, -28_996), (-29_000, 29_000) })
        {
            Assert.Equal(Source2(x, z), sampler.At(x, z));
        }
    }

    [Fact]
    public void Given_ALatticeOfALinearField_When_AnyColumnIsSampled_Then_ItIsExactlyTheField()
    {
        var sampler = new LatticeSampler2(4, static (x, z) => (x * 16) + (z * 8));

        for (var z = -9; z < 9; z++)
        {
            for (var x = -9; x < 9; x++)
            {
                Assert.Equal((x * 16) + (z * 8), sampler.At(x, z));
            }
        }
    }

    [Fact]
    public void Given_TheSourceIsRecorded_When_ColumnsAreSampled_Then_ItIsReadOnlyAtLatticePoints()
    {
        var read = new List<(int X, int Z)>();
        var sampler = new LatticeSampler2(4, (x, z) =>
        {
            read.Add((x, z));
            return Source2(x, z);
        });

        sampler.At(-7, 13);
        sampler.Fill(-5, -5, 11, 9, new int[99]);

        Assert.NotEmpty(read);
        Assert.All(read, p => Assert.True(p.X % 4 == 0 && p.Z % 4 == 0, $"read at {p}"));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-13, 7)]
    [InlineData(-29_001, 28_999)]
    public void Given_ARectangle_When_ItIsFilled_Then_EveryValueIsWhatAtGives(int x0, int z0)
    {
        var sampler = new LatticeSampler2(4, Source2);
        var into = new int[19 * 11];

        sampler.Fill(x0, z0, 19, 11, into);

        for (var z = 0; z < 11; z++)
        {
            for (var x = 0; x < 19; x++)
            {
                Assert.Equal(sampler.At(x0 + x, z0 + z), into[(z * 19) + x]);
            }
        }
    }

    [Fact]
    public void Given_ALattice3_When_APointIsSampled_Then_ItIsTheSourceOnLatticePointsAndFillEqualsAt()
    {
        var sampler = new LatticeSampler3(8, 4, Source3);
        Assert.Equal(Source3(16, -8, -24), sampler.At(16, -8, -24));
        Assert.Equal(Source3(-29_000, 64, 29_000), sampler.At(-29_000, 64, 29_000));

        var into = new int[9 * 7 * 10];
        sampler.Fill(-11, -6, 3, 9, 7, 10, into);

        for (var y = 0; y < 7; y++)
        {
            for (var z = 0; z < 10; z++)
            {
                for (var x = 0; x < 9; x++)
                {
                    Assert.Equal(sampler.At(-11 + x, -6 + y, 3 + z), into[(((y * 10) + z) * 9) + x]);
                }
            }
        }
    }

    [Fact]
    public void Given_ALatticeOfALinearField3_When_AnyPointIsSampled_Then_ItIsExactlyTheField()
    {
        var sampler = new LatticeSampler3(8, 4, static (x, y, z) => (x * 16) + (y * 32) - (z * 8));

        for (var y = -5; y < 5; y++)
        {
            for (var z = -9; z < 9; z += 2)
            {
                for (var x = -9; x < 9; x += 3)
                {
                    Assert.Equal((x * 16) + (y * 32) - (z * 8), sampler.At(x, y, z));
                }
            }
        }
    }

    [Fact]
    public void Given_AnEmptyOrBadSampler_When_ItIsMade_Then_ItIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LatticeSampler2(0, Source2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LatticeSampler3(4, 0, Source3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LatticeSampler2(4, Source2).Fill(0, 0, 4, 4, new int[3]));
    }
}
