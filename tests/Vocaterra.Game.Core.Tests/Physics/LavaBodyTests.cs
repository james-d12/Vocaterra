using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Navigation;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Tests.Voxel;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Tests.Physics;

/// <summary>The body in lava (B10b): it sinks into it, moves slowly, swims up and climbs out at an edge, takes no harm, and the walk planner still avoids it.</summary>
public sealed class LavaBodyTests
{
    private const int U = VoxelPhysics.UnitsPerBlock;
    private const int Ground = VoxelTestSupport.GroundTop;
    private const int PoolMin = 28;
    private const int PoolMax = 36;
    private const int CentreX = (32 * U) + (U / 2);

    // A pool of the given depth (top cell at y = Ground) of the liquid, in stone that stands bank blocks above the top cell.
    private static VoxelGrid Pool(BlockId liquid, int depth, int bank = 0)
    {
        var grid = VoxelTestSupport.FlatScene().Grid;
        for (var z = 0; z < 64; z++)
        {
            for (var x = 0; x < 64; x++)
            {
                var inPool = x is >= PoolMin and <= PoolMax && z is >= PoolMin and <= PoolMax;
                for (var y = 0; y <= Ground + bank; y++)
                {
                    grid.SetBlock(x, y, z, !inPool || y <= Ground - depth ? BlockId.Stone : y <= Ground ? liquid : BlockId.Air);
                }
            }
        }

        return grid;
    }

    private static PlayerBody BodyAt(VoxelGrid grid, int centerX, int feetY, int centerZ = CentreX) => new(grid, centerX, feetY, centerZ, 0);

    private static void Run(PlayerBody body, VoxelGrid grid, int ticks, int intentX = 0, bool jump = false)
    {
        for (var i = 0; i < ticks; i++)
        {
            body.Step(grid, intentX, 0, jump);
        }
    }

    [Fact]
    public void Given_ABodyDroppedOntoAPool_When_Time_Passes_Then_ItSinksIntoTheLavaAndDoesNotRestOnTop()
    {
        var grid = Pool(BlockId.Lava, 6);
        var body = BodyAt(grid, CentreX, (Ground + 3) * U);

        Run(body, grid, 60);
        var after = body.MinY;

        Assert.True(after < (Ground + 1) * U, "the feet are below the lava's top");
        Run(body, grid, 60);
        Assert.True(body.MinY < after, "it keeps sinking");
        Assert.InRange(-body.VelocityY, 1, LavaMotion.SinkSpeed);
        Assert.False(body.Grounded);
    }

    [Fact]
    public void Given_ASwimmerInLava_When_ItSinks_Then_ItSinksSlowerThanInWater()
    {
        var lava = Pool(BlockId.Lava, 8);
        var water = Pool(BlockId.Water, 8);
        var inLava = BodyAt(lava, CentreX, Ground * U);
        var inWater = BodyAt(water, CentreX, Ground * U);

        Run(inLava, lava, 40);
        Run(inWater, water, 40);

        Assert.True(inLava.MinY > inWater.MinY);
        Assert.True(LavaMotion.SinkSpeed < SwimMotion.SinkSpeed);
    }

    [Fact]
    public void Given_ABodyInLava_When_ItMoves_Then_ItIsSlowerThanInWater()
    {
        var lava = Pool(BlockId.Lava, 8);
        var water = Pool(BlockId.Water, 8);
        var inLava = BodyAt(lava, CentreX, (Ground - 2) * U);
        var inWater = BodyAt(water, CentreX, (Ground - 2) * U);

        inLava.Step(lava, Angles.One, 0, false);
        inWater.Step(water, Angles.One, 0, false);

        Assert.True(inLava.VelocityX > 0);
        Assert.True(inLava.VelocityX * 2 < inWater.VelocityX, "well below the speed in water");
    }

    [Fact]
    public void Given_ABodyInLava_When_JumpIsHeld_Then_ItSwimsUpSlowly()
    {
        var grid = Pool(BlockId.Lava, 8);
        var body = BodyAt(grid, CentreX, (Ground - 4) * U);
        var start = body.MinY;

        Run(body, grid, 30, jump: true);

        Assert.True(body.MinY > start);
        Assert.True(body.VelocityY <= LavaMotion.RiseSpeed + 18);
    }

    [Fact]
    public void Given_ALavaPoolWithABankOneBlockAboveTheSurface_When_ASwimmerPushesAtItWithJumpHeld_Then_ItClimbsOut()
    {
        var grid = Pool(BlockId.Lava, 6, bank: 1);
        var body = BodyAt(grid, CentreX, (Ground - 1) * U);

        Run(body, grid, 400, intentX: Angles.One, jump: true);

        Assert.True(body.MinX > (PoolMax + 1) * U, "past the pool's edge");
        Assert.True(body.MinY >= (Ground + 2) * U, "on top of the bank");
    }

    [Fact]
    public void Given_ABodyInLava_When_Time_Passes_Then_ItTakesNoHarm()
    {
        var grid = Pool(BlockId.Lava, 6);
        var body = BodyAt(grid, CentreX, (Ground - 2) * U);
        Run(body, grid, 200);
        Assert.Equal(BlockId.Lava, grid.GetBlock(32, Ground, 32)); // the body changes nothing in the lava; the game has no damage system yet
        Assert.False(BlockDefinition.Get(BlockId.Lava).Solid);
        Assert.True(LavaVolume.Contains(grid, CentreX, Ground * U, CentreX));
        Assert.False(LavaVolume.Contains(grid, CentreX, (Ground + 1) * U, CentreX));
    }

    [Fact]
    public void Given_LavaOnTheWay_When_TheWalkIsPlanned_Then_TheBodyIsRoutedAround()
    {
        var grid = VoxelTestSupport.FlatScene().Grid;
        for (var z = 28; z <= 36; z++)
        {
            grid.SetBlock(34, Ground + 1, z, BlockId.Lava);
        }

        Assert.False(WalkPlanner.Standable(grid, 34, Ground + 1, 32));
        var plan = WalkPlanner.Plan(grid, new BlockPos(32, Ground + 1, 32), 36, 32);
        Assert.NotNull(plan);
        Assert.DoesNotContain(plan!, c => c.X == 34 && c.Z is >= 28 and <= 36);
    }
}
