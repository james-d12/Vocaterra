using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Xunit.Abstractions;
using static Vocaterra.Game.Core.Tests.Voxel.VoxelTestSupport;

namespace Vocaterra.Game.Core.Tests.Timing;

/// <summary>K03: the body's collider costs about the same near shaped blocks (slabs, stairs, a fence line) as over plain ground; CPU time, fastest of several attempts, in the timing collection.</summary>
[Collection(TimingCollection.Name)]
[Trait(TestSpeed.Category, TestSpeed.Timing)]
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class ColliderTimingTests(ITestOutputHelper output)
{
    private const int Attempts = 3;
    private const int Probes = 200_000;
    private static readonly double Budget = CpuTime.Budget(300); // 200 000 probes; the Release cost is about 70 to 110 ms before and after K03 (a probe is 200 to 500 ns), so this is about three times that

    private static VoxelGrid Plain() => FlatScene().Grid;

    private static VoxelGrid Shaped()
    {
        var grid = FlatScene().Grid;
        for (var x = 20; x < 44; x++)
        {
            grid.SetBlock(x, GroundTop + 1, 30, BlockId.OakFence);
            grid.SetBlock(x, GroundTop + 1, 34, BlockId.OakSlab, BlockState.Slab.Pack(SlabKind.Bottom));
            grid.SetBlock(x, GroundTop + 1, 36, BlockId.OakStairs, BlockState.Stairs.Pack(Facing.North, false));
        }

        return grid;
    }

    private static double Run(VoxelGrid grid)
    {
        return CpuTime.Fastest(Attempts, () =>
        {
            var hits = 0;
            for (var i = 0; i < Probes; i++)
            {
                var x = (20 * 4096) + ((i * 37) % (24 * 4096));
                var z = (29 * 4096) + ((i * 91) % (8 * 4096));
                var y = (GroundTop + 1) * 4096;
                if (!VoxelCollider.IsFree(grid, x, y, z))
                {
                    hits++;
                }

                var moved = x;
                if (VoxelCollider.MoveX(grid, ref moved, y, z, 294))
                {
                    hits++;
                }
            }

            Assert.True(hits >= 0);
        });
    }

    [Fact]
    public void Given_ShapedBlocksNearTheBody_When_TheColliderIsProbed_Then_TheCpuTimeIsWithinBudget()
    {
        var plain = Plain();
        var shaped = Shaped();
        _ = Run(plain);
        var plainTime = Run(plain);
        var shapedTime = Run(shaped);
        output.WriteLine($"{Probes} IsFree + MoveX probes: plain ground {plainTime:F1} ms, beside a fence line, slabs and stairs {shapedTime:F1} ms of CPU time, fastest of {Attempts} (budget {Budget:F0} ms)");
        Assert.True(plainTime < Budget, $"the probes over plain ground take {plainTime:F0} ms of CPU time (budget {Budget:F0})");
        Assert.True(shapedTime < Budget, $"the probes beside shaped blocks take {shapedTime:F0} ms of CPU time (budget {Budget:F0})");
    }
}
