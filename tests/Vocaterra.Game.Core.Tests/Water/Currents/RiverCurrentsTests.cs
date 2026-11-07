using Vocaterra.Game.Core.Tests.Voxel;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water.Currents;
using Vocaterra.Game.Core.World.Layout;

namespace Vocaterra.Game.Core.Tests.Water.Currents;

/// <summary>The river's current (water-plan 4.6) on hand-built rivers: straight and bent, the direction from the surface heights or from the path order.</summary>
public sealed class RiverCurrentsTests
{
    private const int Ground = VoxelTestSupport.GroundTop;

    // A river of source water along the path: one water block per column, at the height the function gives.
    private static VoxelGrid RiverGrid(RiverSpec river, Func<int, int, int> surfaceAt)
    {
        var grid = VoxelTestSupport.FlatScene().Grid;
        for (var z = 0; z < 64; z++)
        {
            for (var x = 0; x < 64; x++)
            {
                if (WithinRiver(river, x, z))
                {
                    grid.SetBlock(x, surfaceAt(x, z), z, BlockId.Water);
                }
            }
        }

        return grid;
    }

    // Straight legs between the waypoints, wide enough to cover every column a test samples.
    private static bool WithinRiver(RiverSpec river, int x, int z)
    {
        for (var i = 0; i + 1 < river.Path.Count; i++)
        {
            var (ax, az) = river.Path[i];
            var (bx, bz) = river.Path[i + 1];
            if (x >= Math.Min(ax, bx) - 2 && x <= Math.Max(ax, bx) + 2 && z >= Math.Min(az, bz) - 2 && z <= Math.Max(az, bz) + 2)
            {
                return true;
            }
        }

        return false;
    }

    private static RiverSpec Straight => new(2, 2, 2, [(32, 10), (32, 50)]);

    [Fact]
    public void Given_NoRiver_When_TheCurrentIsRead_Then_ItIsZeroEverywhere()
    {
        var currents = RiverCurrents.Build(null, VoxelTestSupport.FlatScene().Grid);

        Assert.Equal(FlowVector.Zero, currents.At(32, 32));
        Assert.Equal(FlowVector.Zero, RiverCurrents.None.At(32, 32));
    }

    [Fact]
    public void Given_ALevelStraightRiver_When_TheCurrentIsRead_Then_ItRunsInPathOrder()
    {
        var grid = RiverGrid(Straight, (_, _) => Ground + 1);

        var current = RiverCurrents.Build(Straight, grid).At(32, 30);

        Assert.Equal(0, current.X);
        Assert.InRange(current.Z, FlowVector.One - 3, FlowVector.One);
    }

    [Fact]
    public void Given_ASurfaceFallingAlongThePath_When_TheCurrentIsRead_Then_ItRunsDownThePath()
    {
        var grid = RiverGrid(Straight, (_, z) => z < 30 ? Ground + 2 : Ground + 1);

        Assert.True(RiverCurrents.Build(Straight, grid).At(32, 20).Z > 200);
    }

    [Fact]
    public void Given_ASurfaceRisingAlongThePath_When_TheCurrentIsRead_Then_ItRunsAgainstThePathOrder()
    {
        var grid = RiverGrid(Straight, (_, z) => Ground + 1 + ((z - 10) / 3));

        var currents = RiverCurrents.Build(Straight, grid);

        Assert.True(currents.At(32, 20).Z < -200);
        Assert.True(currents.At(32, 40).Z < -200);
    }

    [Fact]
    public void Given_ABentRiver_When_TheCurrentIsRead_Then_EachLegRunsAlongItsOwnDirection()
    {
        var bent = new RiverSpec(2, 2, 2, [(10, 10), (10, 40), (40, 40)]);
        var grid = RiverGrid(bent, (_, _) => Ground + 1);
        var currents = RiverCurrents.Build(bent, grid);

        var firstLeg = currents.At(10, 22);
        var secondLeg = currents.At(28, 40);

        Assert.True(firstLeg.Z > 200 && Math.Abs(firstLeg.X) < 60, $"south on the first leg ({firstLeg})");
        Assert.True(secondLeg.X > 200 && Math.Abs(secondLeg.Z) < 60, $"east on the second leg ({secondLeg})");
    }

    [Fact]
    public void Given_ColumnsAwayFromTheRiver_When_TheCurrentIsRead_Then_ItIsZero()
    {
        var grid = RiverGrid(Straight, (_, _) => Ground + 1);
        var currents = RiverCurrents.Build(Straight, grid);

        Assert.Equal(FlowVector.Zero, currents.At(50, 30));
        Assert.Equal(FlowVector.Zero, currents.At(-1, 30));
        Assert.Equal(FlowVector.Zero, currents.At(32, 500));
    }

    [Fact]
    public void Given_TheShippedScene_When_TheRiverIsRead_Then_ItRunsDownstreamAndTheLakeAndPoolAreStill()
    {
        var scene = TestSupport.RouteScene.Build();
        var river = scene.Point("river");

        Assert.True(scene.Currents.At(river.X, river.Z).IsHorizontal);
        var lake = scene.Point("lake");
        Assert.Equal(FlowVector.Zero, scene.Currents.At(lake.X, lake.Z));
        var fall = scene.Point("waterfall");
        Assert.Equal(FlowVector.Zero, scene.Currents.At(fall.X, fall.Z - 6));
    }
}
