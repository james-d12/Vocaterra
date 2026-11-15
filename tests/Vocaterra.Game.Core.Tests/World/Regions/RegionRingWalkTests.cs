using Vocaterra.Game.Core.Tests.TestSupport;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Regions;

/// <summary>
/// After the pass opens the player may leave the valley anywhere, so the blend has to be walkable round the whole ring (B06, D-game-34 item 15), not only at the pass: from a column beside the border a walk
/// of steps up of one block and steps down of three, over dry ground, leaves the margin for the free land.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class RegionRingWalkTests(ITestOutputHelper output)
{
    private const int StepUp = 1;
    private const int StepDown = 3;

    [Theory]
    [InlineData(42UL)]
    [InlineData(2026UL)]
    [InlineData(7UL)] // a ridge of 100 blocks rises beside the valley's east side: 94% of the starts there climb it by steps of one, the rest meet a step of two (the margin is data: 96 gives 97%, 128 gives 99.5%)
    public void Given_TheRingOfTheValley_When_AWalkIsSoughtFromEveryEighthColumnBesideTheBorder_Then_TheMarginIsCrossedOnFootFromNearlyAllOfThem(ulong seed)
    {
        var world = RegionTestWorld.Composition(seed);
        var b = RegionTestWorld.Valley(seed).Bounds;
        var margin = TestGame.Data.WorldGen.Blend.Margin;
        var sea = TestGame.Data.WorldGen.Vertical.SeaLevel;
        var depth = margin + 16; // the strip of columns round the footprint that is searched; the walk is done once it is out of the margin, onto the free land
        var (minX, minZ) = (b.MinX - depth, b.MinZ - depth);
        var size = (b.MaxX - b.MinX + 1) + (2 * depth);
        var height = new int[size * size];
        var open = new bool[size * size]; // outside the footprint and dry
        for (var j = 0; j < size; j++)
        {
            for (var i = 0; i < size; i++)
            {
                var (x, z) = (minX + i, minZ + j);
                var shape = RegionTestWorld.Shape(world, x, z);
                height[(j * size) + i] = shape.Height;
                open[(j * size) + i] = b.DistanceTo(x, z) > 0 && shape.Height >= sea;
            }
        }

        // Backwards from the free land: a column can reach it when a step to a neighbour that can is allowed.
        var reaches = new bool[size * size];
        var queue = new Queue<int>();
        for (var j = 0; j < size; j++)
        {
            for (var i = 0; i < size; i++)
            {
                var k = (j * size) + i;
                if (open[k] && b.DistanceTo(minX + i, minZ + j) >= margin)
                {
                    reaches[k] = true;
                    queue.Enqueue(k);
                }
            }
        }

        var steps = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        while (queue.TryDequeue(out var k))
        {
            var (i, j) = (k % size, k / size);
            foreach (var (di, dj) in steps)
            {
                var (ni, nj) = (i + di, j + dj);
                if ((uint)ni >= (uint)size || (uint)nj >= (uint)size)
                {
                    continue;
                }

                var n = (nj * size) + ni;
                var rise = height[k] - height[n]; // a step from n to k
                if (!reaches[n] && open[n] && rise <= StepUp && -rise <= StepDown)
                {
                    reaches[n] = true;
                    queue.Enqueue(n);
                }
            }
        }

        var (starts, ok, wet) = (0, 0, 0);
        var failed = new List<string>();
        for (var d = 1; d <= 1; d++)
        {
            foreach (var (x, z) in Border(b, d, 8))
            {
                var k = ((z - minZ) * size) + (x - minX);
                if (!open[k])
                {
                    wet++;
                    continue;
                }

                starts++;
                if (reaches[k])
                {
                    ok++;
                }
                else
                {
                    failed.Add($"{x},{z}");
                }
            }
        }

        output.WriteLine($"seed {seed}: {ok} of {starts} starts beside the border leave the margin on foot ({wet} start columns are water); not reaching: {string.Join(' ', failed.Take(40))}");
        Assert.True(ok >= starts * 0.9, $"{ok} of {starts}");
    }

    private static IEnumerable<(int X, int Z)> Border(Vocaterra.Game.Core.World.Regions.RegionBounds b, int d, int stride)
    {
        for (var t = 0; t <= b.MaxX - b.MinX; t += stride)
        {
            yield return (b.MinX + t, b.MinZ - d);
            yield return (b.MinX + t, b.MaxZ + d);
        }

        for (var t = 0; t <= b.MaxZ - b.MinZ; t += stride)
        {
            yield return (b.MinX - d, b.MinZ + t);
            yield return (b.MaxX + d, b.MinZ + t);
        }
    }
}
