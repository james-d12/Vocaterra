using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Voxel;
using static Vocaterra.Game.Core.Tests.Voxel.VoxelTestSupport;

namespace Vocaterra.Game.Core.Tests.Scripted;

/// <summary>The scripted <c>teleport-to</c> step (B12): the player is put on the highest ground at or below a height, which is how the cave tour reaches a cave under the ground, and the coordinates may be signed.</summary>
public sealed class TeleportToStepTests
{
    private static (VoxelSimulation Sim, List<string> Log) Go(params VoxelStep[] steps)
    {
        // A flat world of stone to y 9 with a chamber hollowed from y 3 to 6 under (20, 20): the floor of the chamber is the block at y 2.
        var sim = FlatSim(grid =>
        {
            for (var y = 3; y <= 6; y++)
            {
                grid.SetBlock(20, y, 20, BlockId.Air);
            }
        });
        var log = new List<string>();
        var script = new VoxelScript(sim, new LookControl(), steps, static _ => null, log.Add);
        var commands = new List<VoxelCommand>();
        for (var tick = 0; tick < 10 && !script.IsDone; tick++)
        {
            script.Fill(commands);
            sim.Step(commands);
        }

        Assert.True(script.IsDone);
        return (sim, log);
    }

    [Fact]
    public void Given_ATeleportToStepBelowTheGround_When_TheScriptRuns_Then_ThePlayerStandsOnTheFloorOfTheCave()
    {
        var (_, log) = Go(VoxelStep.TeleportTo(20, 5, 20));

        Assert.Equal("teleport-to 20,5,20: standing at 20,3,20", Assert.Single(log));
    }

    [Fact]
    public void Given_ATeleportStepOverTheSameColumn_When_TheScriptRuns_Then_ItStandsOnTheSurfaceNotInTheCave()
    {
        var (_, log) = Go(VoxelStep.Teleport(20, 20));

        Assert.Equal("teleport 20,20: standing at 20,10,20", Assert.Single(log)); // the roof of the chamber is the ground seen from the top
    }

    [Fact]
    public void Given_ATeleportToStepWithNoGroundBelowIt_When_TheScriptRuns_Then_ItSaysSo()
    {
        var (_, log) = Go(VoxelStep.TeleportTo(20, 0, 20), VoxelStep.TeleportTo(-5, 3, 20));

        Assert.Equal(["teleport-to 20,0,20: no ground to stand on there", "teleport-to -5,3,20: outside the world"], log);
    }

    [Theory]
    [InlineData("-120,-5,300", -120, -5, 300)]
    [InlineData("0,-64,0", 0, -64, 0)]
    public void Given_SignedCoordinates_When_ABlockIsParsed_Then_TheMinusSignsAreRead(string text, int x, int y, int z)
    {
        Assert.True(VoxelStep.TryParseSignedBlock(text, out var px, out var py, out var pz));
        Assert.Equal((x, y, z), (px, py, pz));
        Assert.True(VoxelStep.TryParseSignedColumn($"{x},{z}", out var cx, out var cz));
        Assert.Equal((x, z), (cx, cz));
        Assert.False(VoxelStep.TryParseSignedBlock("1,2", out _, out _, out _));
        Assert.False(VoxelStep.TryParseSignedColumn("a,b", out _, out _));
        Assert.True(VoxelStep.TryParseColumn("-1,2", out var wx, out var wz)); // B14: the plain readers of the other steps are signed too
        Assert.Equal((-1, 2), (wx, wz));
    }
}
