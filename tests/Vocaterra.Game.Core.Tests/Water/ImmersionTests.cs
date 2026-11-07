using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Tests.Voxel;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Tests.Water;

/// <summary>Immersion on small hand-built grids: ground at y 0 to 9, water placed above it in the column (32, 32).</summary>
public sealed class ImmersionTests
{
    private const int U = VoxelPhysics.UnitsPerBlock;
    private const int Ground = VoxelTestSupport.GroundTop;

    private static VoxelGrid Pool(int waterLayers, WaterLevel? topLevel = null)
    {
        var grid = VoxelTestSupport.FlatScene().Grid;
        for (var i = 1; i <= waterLayers; i++)
        {
            grid.SetBlock(32, Ground + i, 32, BlockId.Water);
        }

        if (topLevel is { } level && waterLayers > 0)
        {
            grid.SetWater(32, Ground + waterLayers, 32, level);
        }

        return grid;
    }

    private static PlayerBody Standing(VoxelGrid grid, int extraFeetUnits = 0) =>
        new(grid, (32 * U) + (U / 2), ((Ground + 1) * U) + extraFeetUnits, (32 * U) + (U / 2), 0);

    [Fact]
    public void Given_DryLand_When_Read_Then_NothingIsWet()
    {
        var grid = Pool(0);
        Assert.Equal(Immersion.Dry, Immersion.Of(grid, Standing(grid)));
    }

    [Fact]
    public void Given_AnkleDeepWater_When_Read_Then_OnlyTheFeetAreWet()
    {
        var grid = Pool(1, WaterLevel.Flowing(4)); // surface 7/16 of a block
        var result = Immersion.Of(grid, Standing(grid));

        Assert.Equal(new Immersion(true, false, false), result);
    }

    [Fact]
    public void Given_WaistDeepWater_When_Read_Then_FeetAndBodyAreWetButNotTheEye()
    {
        var grid = Pool(2, WaterLevel.Flowing(5)); // one full layer plus a cell at 5/16 of a block: the surface is 1.3 blocks up
        var result = Immersion.Of(grid, Standing(grid));

        Assert.True(result.Feet);
        Assert.True(result.Body);
        Assert.False(result.Eye);
    }

    [Fact]
    public void Given_WaterJustBelowTheWaist_When_Read_Then_TheBodyIsNotWet()
    {
        var grid = Pool(1); // source surface 14/16 of a block = 3 584 units; the waist is 3 686 units up
        var result = Immersion.Of(grid, Standing(grid));

        Assert.True(result.Feet);
        Assert.False(result.Body);
    }

    [Fact]
    public void Given_FullyUnderWater_When_Read_Then_AllThreeAreWet()
    {
        var grid = Pool(4);
        Assert.Equal(new Immersion(true, true, true), Immersion.Of(grid, Standing(grid)));
    }

    [Fact]
    public void Given_ASourceSurfaceAtTheEye_When_TheEyeIsJustAboveOrBelow_Then_EyeFollowsTheSurface()
    {
        var grid = Pool(2); // the top surface is at (Ground + 1 + 1 + 14/16) * U
        var surface = (Ground + 2) * U + (14 * (U / 16));
        var eyeAbove = surface - VoxelPhysics.EyeHeight + 1;
        var eyeBelow = surface - VoxelPhysics.EyeHeight - 1;
        var feetY = Ground + 1;
        Assert.True(eyeAbove > feetY * U);

        Assert.False(Immersion.Of(grid, Standing(grid, eyeAbove - (feetY * U))).Eye);
        Assert.True(Immersion.Of(grid, Standing(grid, eyeBelow - (feetY * U))).Eye);
    }

    [Fact]
    public void Given_AFlowingCellWithALowSurface_When_TheEyeIsInItAboveTheSurface_Then_TheEyeIsDryButTheBodyIsWet()
    {
        // A full cell under a cell flowing at distance 7 (surface 2/16); the eye, 1.62 blocks above the feet, is in the upper cell above 2/16.
        var grid = Pool(2, WaterLevel.Flowing(7));

        Assert.Equal(new Immersion(true, true, false), Immersion.Of(grid, Standing(grid)));
    }

    [Fact]
    public void Given_FallingWaterInTheTopCell_When_TheEyeIsInIt_Then_ItCountsAsFull()
    {
        var falling = WaterLevel.Flowing(7, falling: true);
        var grid = Pool(2, falling);
        var body = Standing(grid);

        Assert.True(Immersion.Of(grid, body).Body);
        // the eye is in the top cell (1.62 blocks above the feet), which is falling water: full height
        Assert.True(Immersion.Of(grid, Standing(grid, U / 4)).Eye);
    }

    [Fact]
    public void Given_ABodyAtTheGridEdge_When_Read_Then_ItIsDryWithoutThrowing()
    {
        var grid = new VoxelGrid(32, 32, 32);
        var body = new PlayerBody(grid, U / 2, 31 * U, U / 2, 0);

        Assert.Equal(Immersion.Dry, Immersion.Of(grid, body));
        var above = new PlayerBody(grid, U / 2, 40 * U, U / 2, 0);
        Assert.Equal(Immersion.Dry, Immersion.Of(grid, above));
    }
}
