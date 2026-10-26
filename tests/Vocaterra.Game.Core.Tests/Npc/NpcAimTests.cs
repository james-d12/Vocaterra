using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Npc;

/// <summary>The crosshair on an NPC: the ray from the eye along the look, within reach and the figure's radius.</summary>
public sealed class NpcAimTests
{
    private static readonly (double X, double Y, double Z) Eye = (0.5, 1.0, 0.5); // level with the figures' centres

    [Fact]
    public void ACentreStraightAheadIsHitAndTheLookIsTheSimulationsOwn()
    {
        // Yaw 0 looks north (-z): a figure's body centre five blocks north of the eye is on the crosshair.
        Assert.True(NpcAim.Hits(Eye, Angles.FromDegrees(0), 0, (0.5, 1.0, -4.5)));
        Assert.True(NpcAim.Hits(Eye, Angles.FromDegrees(90), 0, (5.5, 1.0, 0.5))); // east when facing east
        Assert.True(NpcAim.Hits(Eye, Angles.FromDegrees(180), 0, (0.5, 1.0, 5.5))); // south when facing south
        Assert.True(NpcAim.Hits(Eye, Angles.FromDegrees(270), 0, (-4.5, 1.0, 0.5))); // west when facing west
    }

    [Fact]
    public void AFigureToTheSideOrBehindOrBeyondReachIsMissed()
    {
        Assert.False(NpcAim.Hits(Eye, Angles.FromDegrees(0), 0, (3.5, 1.0, -4.5))); // three blocks to the right: out of the radius
        Assert.False(NpcAim.Hits(Eye, Angles.FromDegrees(0), 0, (0.5, 1.0, 5.5))); // behind
        Assert.False(NpcAim.Hits(Eye, Angles.FromDegrees(0), 0, (0.5, 1.0, -12.5))); // beyond the eight blocks of reach
        Assert.True(NpcAim.Hits(Eye, Angles.FromDegrees(0), 0, (0.5, 1.0, -7.0), reach: 8.0));
    }

    [Fact]
    public void TheAimPointIsTheChestOneAndATwoTenthsAboveTheFeet()
    {
        Assert.Equal((20.5, 11.2, 20.5), NpcAim.BodyOf(new BlockPos(20, 10, 20)));
        Assert.Equal(2.0, NpcAim.Distance((18.5, 11.0, 20.5), (20.5, 11.0, 20.5)), 6);
    }
}
