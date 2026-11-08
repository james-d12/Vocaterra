using Vocaterra.Game.Core.Navigation;
using Vocaterra.Game.Core.Voxel;
using static Vocaterra.Game.Core.Tests.Voxel.VoxelTestSupport;

namespace Vocaterra.Game.Core.Tests.Navigation;

/// <summary>A planned walk agrees with the body about fences (issue #33): nothing stands on a fence and no step goes up onto one.</summary>
public sealed class WalkPlannerFenceTests
{
    [Fact]
    public void AFenceTopIsNotStandableAndIsNotSteppedOnto()
    {
        var grid = FlatScene(g => g.SetBlock(33, GroundTop + 1, 32, BlockId.Fence)).Grid;

        Assert.False(WalkPlanner.Standable(grid, 33, GroundTop + 2, 32));
        Assert.Equal(-1, WalkPlanner.StepTo(grid, 32, GroundTop + 1, 32, 33, 32));
        Assert.True(WalkPlanner.Standable(grid, 34, GroundTop + 1, 32)); // the ground beyond is as it was
    }
}
