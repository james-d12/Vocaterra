using Vocaterra.Game.Core.Tests.Voxel;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water.Flow;

namespace Vocaterra.Game.Core.Tests.Water.Flow;

/// <summary>H07: how a waterfall must be built to be at rest under the flow rules (one source on top, falling water below, its bottom cell in the pool's surface layer).</summary>
public sealed class WaterfallAtRestTests
{
    // pool surface layer offset p above Ground (0 = column bottom cell at Ground, pool surface at Ground)
    private static int ChangedAfterWakingAll(int bottomOffset, int poolSurfaceOffset, int depthPool = 2)
    {
        var w = new FlowWorld();
        var g = FlowWorld.Ground;
        // cliff wall at z=10, x 5..17, y up to Ground+14
        for (var x = 5; x <= 17; x++) for (var y = g; y <= g + 14; y++) w.Grid.SetBlock(x, y, 10, BlockId.Stone);
        // pool basin: rim solid at x=5,17 ; z=22 ; for y up to pool surface; interior sources
        var top = g + poolSurfaceOffset;
        for (var y = g - depthPool + 1; y <= top; y++)
        {
            for (var x = 5; x <= 17; x++) for (var z = 10; z <= 22; z++)
            {
                var rim = x == 5 || x == 17 || z == 22 || z == 10;
                w.Grid.SetBlock(x, y, z, rim ? BlockId.Stone : BlockId.Water);
            }
        }
        // (the floor is only at Floor; dig-in below Ground is not possible, so the basin sits on the floor)
        // sheet: x 10..12 at z=11, from y=Ground+bottomOffset to Ground+12: falling, source at top
        for (var x = 10; x <= 12; x++)
        {
            for (var y = g + bottomOffset; y <= g + 12; y++)
            {
                w.Grid.SetBlock(x, y, 11, BlockId.Water);
                w.Grid.SetWater(x, y, 11, y == g + 12 ? WaterLevel.Source : WaterLevel.Flowing(1, falling: true));
            }
        }
        w.Flow.Queue.Clear();
        var log = new WaterAtRestTests.ChangeLog();
        w.Grid.AddListener(log);
        var due = FlowSimulation.DueAfter(0);
        foreach (var p in w.Grid.BlocksOfKind(BlockId.Water))
        {
            _ = w.Flow.Queue.Schedule(p, due);
            foreach (var (dx, dy, dz) in new[] { (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1) })
                _ = w.Flow.Queue.Schedule(new BlockPos(p.X + dx, p.Y + dy, p.Z + dz), due);
        }
        var steps = w.Settle();
        var net = log.Net();
        Assert.True(steps < 200);
        return net.Count;
    }

    [Fact]
    public void Given_ASheetOfFallingWaterWhoseBottomCellIsInThePoolSurfaceLayer_When_EverythingIsWoken_Then_NothingChanges() =>
        Assert.Equal(0, ChangedAfterWakingAll(bottomOffset: 1, poolSurfaceOffset: 1));

    [Fact]
    public void Given_ASheetOfFallingWaterThatLandsOnePastThePoolSurface_When_EverythingIsWoken_Then_ItSpreadsOverThePool() =>
        Assert.True(ChangedAfterWakingAll(bottomOffset: 1, poolSurfaceOffset: 0) > 0);
}
