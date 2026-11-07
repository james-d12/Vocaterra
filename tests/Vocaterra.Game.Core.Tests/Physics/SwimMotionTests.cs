using Vocaterra.Game.Core.Physics;

namespace Vocaterra.Game.Core.Tests.Physics;

/// <summary>The swim rules as pure functions: the speeds, the drag, the swim-up push, the cut of a fall and the hop.</summary>
public sealed class SwimMotionTests
{
    private const double UnitsPerSecond = VoxelPhysics.TicksPerSecond / (double)VoxelPhysics.UnitsPerBlock;

    private static int Settle(int velocity, bool jump)
    {
        for (var i = 0; i < 600; i++)
        {
            velocity = SwimMotion.NextVerticalSpeed(velocity, jump);
        }

        return velocity;
    }

    [Fact]
    public void Given_Wading_When_TheSpeedIsAsked_Then_ItIsEightTenthsOfTheWalk()
    {
        Assert.Equal(VoxelPhysics.WalkSpeed * 8 / 10, SwimMotion.HorizontalSpeed(false));
    }

    [Fact]
    public void Given_Swimming_When_TheSpeedIsAsked_Then_ItIsSixTenthsOfTheWalk()
    {
        Assert.Equal(VoxelPhysics.WalkSpeed * 6 / 10, SwimMotion.HorizontalSpeed(true));
    }

    [Fact]
    public void Given_ARestingSwimmer_When_JumpIsNotHeld_Then_TheBodySinksAndSettlesNearOneAndAHalfBlocksASecond()
    {
        var settled = Settle(0, false);
        Assert.InRange(-settled * UnitsPerSecond, 1.3, 1.6);
        Assert.True(SwimMotion.NextVerticalSpeed(0, false) < 0);
    }

    [Fact]
    public void Given_JumpHeld_When_TheBodyIsSwimming_Then_ItRisesAndSettlesNearTwoAndAHalfBlocksASecond()
    {
        var settled = Settle(0, true);
        Assert.InRange(settled * UnitsPerSecond, 2.2, 2.7);
    }

    [Fact]
    public void Given_AFastFall_When_TheFirstWetTickRuns_Then_ItsSpeedIsCutToTheSinkSpeed()
    {
        Assert.Equal(-SwimMotion.SinkSpeed, SwimMotion.NextVerticalSpeed(-1500, false));
        Assert.Equal(-SwimMotion.SinkSpeed, SwimMotion.NextVerticalSpeed(-1500, true));
    }

    [Fact]
    public void Given_AHopFasterThanTheSwimUp_When_TheSpeedIsTaken_Then_TheDragDoesNotCutItToTheSwimUpSpeed()
    {
        var next = SwimMotion.NextVerticalSpeed(SwimMotion.HopVelocity, true);
        Assert.True(next > SwimMotion.RiseSpeed * 2);
    }

    [Fact]
    public void Given_AHop_When_TheSpeedIsTaken_Then_ItIsTheHopSpeedUnlessTheBodyAlreadyRisesFaster()
    {
        Assert.Equal(SwimMotion.HopVelocity, SwimMotion.HopSpeed(100));
        Assert.Equal(SwimMotion.HopVelocity, SwimMotion.HopSpeed(-80));
        Assert.Equal(SwimMotion.HopVelocity + 100, SwimMotion.HopSpeed(SwimMotion.HopVelocity + 100));
    }

    [Fact]
    public void Given_Ledges_When_TheReachIsAsked_Then_TwoBlocksAreReachableAndThreeAreNot()
    {
        Assert.True(SwimMotion.InHopReach(2 * VoxelPhysics.UnitsPerBlock));
        Assert.False(SwimMotion.InHopReach(3 * VoxelPhysics.UnitsPerBlock));
        Assert.False(SwimMotion.InHopReach(0));
    }
}
