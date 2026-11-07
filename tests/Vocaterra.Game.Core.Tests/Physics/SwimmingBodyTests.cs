using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Tests.Voxel;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Tests.Physics;

/// <summary>
/// The body in water on hand-built pools (x and z 28 to 36, centre column 32): wading, sinking, swimming up, the bob at the surface,
/// the hop out onto a bank, the fall into water, and the dry walk unchanged.
/// </summary>
public sealed class SwimmingBodyTests
{
    private const int U = VoxelPhysics.UnitsPerBlock;
    private const int Ground = VoxelTestSupport.GroundTop;
    private const int PoolMin = 28;
    private const int PoolMax = 36;
    private const int CentreX = (32 * U) + (U / 2);
    private const int Surface = (Ground * U) + (WaterSurface.SourceSixteenths * U / WaterSurface.Full);

    // A pool of the given depth whose top water cell is at y = Ground, set in ground that stands bank blocks above the water cell.
    private static VoxelGrid Pool(int depth, int bank = 0)
    {
        var grid = VoxelTestSupport.FlatScene().Grid;
        for (var z = 0; z < 64; z++)
        {
            for (var x = 0; x < 64; x++)
            {
                var inPool = x is >= PoolMin and <= PoolMax && z is >= PoolMin and <= PoolMax;
                for (var y = 0; y <= Ground + bank; y++)
                {
                    grid.SetBlock(x, y, z, !inPool || y <= Ground - depth ? BlockId.Stone : y <= Ground ? BlockId.Water : BlockId.Air);
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
    public void Given_DryGround_When_TheBodyWalks_Then_ItMovesAtTheWalkSpeedAndJumpsAsBefore()
    {
        var grid = Pool(4);
        var body = BodyAt(grid, 10 * U, (Ground + 1) * U);

        var (jumped, _) = body.Step(grid, Angles.One, 0, true);

        Assert.True(jumped);
        Assert.Equal(VoxelPhysics.WalkSpeed, body.VelocityX);
        Assert.Equal(VoxelPhysics.JumpVelocity - VoxelPhysics.Gravity, body.VelocityY);
    }

    [Fact]
    public void Given_ShallowWater_When_TheBodyWalks_Then_ItWadesAtEightTenthsAndStillJumps()
    {
        var grid = Pool(1);
        var body = BodyAt(grid, CentreX, Ground * U);
        Assert.True(Immersion.Of(grid, body).Feet);
        Assert.False(Immersion.Of(grid, body).Body);

        var (jumped, _) = body.Step(grid, Angles.One, 0, true);

        Assert.True(jumped);
        Assert.Equal(VoxelPhysics.WalkSpeed * 8 / 10, body.VelocityX);
        Assert.Equal(VoxelPhysics.JumpVelocity - VoxelPhysics.Gravity, body.VelocityY);
    }

    [Fact]
    public void Given_DeepWater_When_TheBodyIsLetGo_Then_ItSinksSlowlyAtAboutOneAndAHalfBlocksASecond()
    {
        var grid = Pool(4);
        var body = BodyAt(grid, CentreX, (Ground - 1) * U);
        Assert.True(Immersion.Of(grid, body).Body);
        var start = body.FeetY;

        Run(body, grid, 60);

        var sunk = (start - body.FeetY) / (double)U;
        Assert.InRange(sunk, 1.0, 1.5);
        Assert.InRange(-body.VelocityY * 60.0 / U, 1.3, 1.6);
    }

    [Fact]
    public void Given_DeepWater_When_JumpIsHeld_Then_TheBodyRisesAtAboutTwoAndAHalfBlocksASecond()
    {
        var grid = Pool(4);
        var body = BodyAt(grid, CentreX, (Ground - 3) * U);
        Run(body, grid, 40, jump: true);
        var before = body.FeetY;

        Run(body, grid, 20, jump: true);

        var speed = (body.FeetY - before) / (double)U * 3.0; // 20 ticks is a third of a second
        Assert.InRange(speed, 2.2, 2.7);
    }

    [Fact]
    public void Given_DeepWater_When_JumpIsHeld_Then_TheHeadBobsJustAboveTheSurfaceWithoutLeavingTheWater()
    {
        var grid = Pool(4);
        var body = BodyAt(grid, CentreX, (Ground - 2) * U);
        Run(body, grid, 120, jump: true);

        int min = int.MaxValue, max = int.MinValue, turns = 0;
        var rising = body.VelocityY > 0;
        for (var i = 0; i < 300; i++)
        {
            body.Step(grid, 0, 0, true);
            min = Math.Min(min, body.EyeY);
            max = Math.Max(max, body.EyeY);
            if ((body.VelocityY > 0) != rising)
            {
                rising = !rising;
                turns++;
            }
        }

        // The waist is the swim threshold, so the eye floats about 0.7 of a block above the surface, rising and falling around it.
        Assert.True(turns >= 20, $"the body keeps bobbing ({turns} turns)");
        Assert.True(max > min, "the eye moves");
        Assert.True(min > Surface, $"the head stays out of the water (min {min}, surface {Surface})");
        Assert.True(max < Surface + U, $"the body does not leave the water (max {max}, surface {Surface})");
    }

    [Fact]
    public void Given_ABobbingBody_When_JumpIsReleased_Then_ItSinksToTheFloor()
    {
        var grid = Pool(4);
        var body = BodyAt(grid, CentreX, (Ground - 2) * U);
        Run(body, grid, 120, jump: true);

        Run(body, grid, 400);

        Assert.True(body.Grounded);
        Assert.Equal((Ground - 3) * U, body.FeetY);
    }

    [Fact]
    public void Given_SwimmingBodyOnTheSurface_When_ItPressesAgainstAOneBlockBank_Then_JumpHeldHopsItOut()
    {
        var grid = Pool(4, bank: 1);
        var body = BodyAt(grid, (PoolMin * U) + (U / 2), (Ground - 2) * U);
        Run(body, grid, 120, jump: true);

        Run(body, grid, 90, -Angles.One, true);

        Assert.True(body.FeetY >= (Ground + 2) * U, $"feet at {body.FeetY / (double)U} blocks, the bank top is {Ground + 2}");
        Assert.True(body.MinX < PoolMin * U);
    }

    [Fact]
    public void Given_ABankTwoBlocksHigh_When_TheSwimmerPressesAgainstIt_Then_TheBodyDoesNotHopOut()
    {
        var grid = Pool(4, bank: 2);
        var body = BodyAt(grid, (PoolMin * U) + (U / 2), (Ground - 2) * U);
        Run(body, grid, 120, jump: true);

        Run(body, grid, 120, -Angles.One, true);

        Assert.True(body.FeetY < (Ground + 1) * U);
        Assert.True(body.MinX >= PoolMin * U);
    }

    [Fact]
    public void Given_ABankButNoJump_When_TheSwimmerPressesAgainstIt_Then_TheBodyDoesNotHopOut()
    {
        var grid = Pool(4, bank: 1);
        var body = BodyAt(grid, (PoolMin * U) + (U / 2), (Ground - 1) * U);

        Run(body, grid, 120, -Angles.One);

        Assert.True(body.MinX >= PoolMin * U);
    }

    [Fact]
    public void Given_AFallFromHeight_When_TheBodyEntersDeepWater_Then_ItsSpeedIsCutAndItNeverLands()
    {
        var grid = Pool(4);
        var body = BodyAt(grid, CentreX, (Ground + 10) * U);
        var wasUnder = false;
        var landedInWater = false;
        for (var i = 0; i < 400; i++)
        {
            var wet = Immersion.Of(grid, body);
            var (_, landed) = body.Step(grid, 0, 0, false);
            if (wet.Body)
            {
                Assert.True(body.VelocityY >= -SwimMotion.SinkSpeed, $"speed {body.VelocityY} after a wet tick");
                wasUnder = true;
                landedInWater |= landed;
            }
        }

        Assert.True(wasUnder);
        Assert.False(landedInWater);
        Assert.True(body.Grounded);
        Assert.Equal((Ground - 3) * U, body.FeetY);
    }

    [Fact]
    public void Given_ASwimmer_When_ItJumpsFromTheFloor_Then_NoJumpIsReported()
    {
        var grid = Pool(4);
        var body = BodyAt(grid, CentreX, (Ground - 3) * U);
        var jumpedEver = false;
        for (var i = 0; i < 60; i++)
        {
            jumpedEver |= body.Step(grid, 0, 0, true).Jumped;
        }

        Assert.False(jumpedEver);
        Assert.True(body.FeetY > (Ground - 3) * U);
    }
}
