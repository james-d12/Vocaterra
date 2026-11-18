using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Noise;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Regions;

/// <summary>The land round the valley (B02b redo): inland, with no sea within a thousand blocks, calm hills round it and a belt of rugged land beyond where ranges stand.</summary>
public sealed class RegionLandBiasTests(ITestOutputHelper output)
{
    private static readonly int[] Directions = [0, 1, 2, 3];

    [Theory]
    [InlineData(42UL)]
    [InlineData(7UL)]
    [InlineData(2026UL)]
    public void Given_TheValley_When_TheClimateRoundItIsRead_Then_NoSeaIsWithinAThousandBlocksTheRingIsCalmAndTheBeltBeyondIsRugged(ulong seed)
    {
        var world = RegionTestWorld.Composition(seed);
        var b = RegionTestWorld.Valley(seed).Bounds;
        var bias = TestGame.Data.WorldGen.Blend.LandBias;
        var e = bias.Erosion!;
        var seaWithin = 0;
        var (calmWorst, ruggedWorst) = (int.MaxValue, int.MinValue);
        foreach (var d in new[] { 200, 500, 700, 1000, 2200, 3000, 4500 })
        {
            foreach (var side in Directions)
            {
                var (x, z) = side switch
                {
                    0 => (b.MaxX + d, (b.MinZ + b.MaxZ) / 2),
                    1 => (b.MinX - d, (b.MinZ + b.MaxZ) / 2),
                    2 => ((b.MinX + b.MaxX) / 2, b.MaxZ + d),
                    _ => ((b.MinX + b.MaxX) / 2, b.MinZ - d),
                };
                var c = world.Places.ClimateAt(x, z);
                if (d <= 1000)
                {
                    seaWithin += c.Continentalness < 0 ? 1 : 0;
                }

                if (d <= e.FromRadius)
                {
                    calmWorst = Math.Min(calmWorst, c.Erosion);
                }

                if (d == 2200)
                {
                    ruggedWorst = Math.Max(ruggedWorst, c.Erosion - FieldScale.FromThousandths(e.Far));
                }
            }
        }

        output.WriteLine($"seed {seed}: sea columns within 1 000 blocks {seaWithin}; the least erosion within {e.FromRadius} blocks {calmWorst * 1000 / IntMath.One} thousandths (the ring's floor {e.Near}); the belt's erosion beyond the far bound {ruggedWorst * 1000 / IntMath.One} at most");
        Assert.Equal(0, seaWithin);
        Assert.True(calmWorst >= FieldScale.FromThousandths(e.Near), "the ring round the valley is calm hills: the erosion is never below the ring's floor");
        Assert.True(ruggedWorst <= FieldScale.FromThousandths(30), $"the belt is as rugged as the far bound or more: {ruggedWorst} above it"); // the free erosion may be lower still; the belt is full a little beyond its start, so it is never far above the bound
    }
}
