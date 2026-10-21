using Vocaterra.MonoGame.Frontend;
using Vocaterra.Simulation;

namespace Vocaterra.MonoGame.Tests;

public sealed class InputTests
{
    [Fact]
    public void NothingHeldSendsNoCommands()
    {
        var commands = new List<PlayerCommand> { PlayerCommand.Jump };
        CommandMapper.Fill(InputActions.None, commands);
        Assert.Empty(commands);
    }

    [Theory]
    [InlineData(InputActions.Left, PlayerCommandKind.MoveLeft)]
    [InlineData(InputActions.Right, PlayerCommandKind.MoveRight)]
    [InlineData(InputActions.Jump, PlayerCommandKind.Jump)]
    public void EachHeldActionBecomesItsCommand(InputActions actions, PlayerCommandKind kind)
    {
        var commands = new List<PlayerCommand>();
        CommandMapper.Fill(actions, commands);
        Assert.Equal([new PlayerCommand(kind)], commands);
    }

    [Fact]
    public void SeveralHeldActionsKeepAFixedOrderAndTheListIsReused()
    {
        var commands = new List<PlayerCommand>();
        CommandMapper.Fill(InputActions.Jump | InputActions.Left | InputActions.Right, commands);
        Assert.Equal([PlayerCommand.MoveLeft, PlayerCommand.MoveRight, PlayerCommand.Jump], commands);
        CommandMapper.Fill(InputActions.Right, commands);
        Assert.Equal([PlayerCommand.MoveRight], commands);
    }

    [Fact]
    public void TheScriptHoldsRightOnlyForItsFirstTicks()
    {
        var script = new ScriptedInput(3);
        Assert.Equal([InputActions.Right, InputActions.Right, InputActions.Right, InputActions.None, InputActions.None], Enumerable.Range(0, 5).Select(t => script.Poll(t)));
        Assert.Equal(InputActions.None, new ScriptedInput(0).Poll(0));
    }

    [Fact]
    public void ScriptedWalkingMovesTheSimulatedPlayerRight()
    {
        var sim = GameSimulation.Create(42);
        var start = sim.Player.X;
        var script = new ScriptedInput(60);
        var commands = new List<PlayerCommand>();
        for (var i = 0; i < 90; i++)
        {
            CommandMapper.Fill(script.Poll(sim.Tick), commands);
            sim.Step(commands);
        }

        Assert.True(sim.Player.X > start + (4 * 4096), "Sixty ticks of 'right' should move the player several tiles.");
        Assert.Equal(1, sim.Player.Facing);
    }

    [Fact]
    public void AHeldJumpLeavesTheGround()
    {
        var sim = GameSimulation.Create(42);
        var commands = new List<PlayerCommand>();
        var jumped = false;
        for (var i = 0; i < 20; i++)
        {
            CommandMapper.Fill(InputActions.Jump, commands);
            sim.Step(commands);
            jumped |= sim.Events.Any(e => e.Kind == SimulationEventKind.PlayerJumped);
        }

        Assert.True(jumped);
    }

    [Fact]
    public void TheSameScriptGivesTheSameStateDigestOnTwoRuns()
    {
        static string Run()
        {
            var sim = GameSimulation.Create(42);
            var script = new ScriptedInput(60);
            var commands = new List<PlayerCommand>();
            var accumulator = new TickAccumulator(GameSimulation.TicksPerSecond);
            for (var frame = 0; frame < 200; frame++)
            {
                var steps = accumulator.Advance(frame % 3 == 0 ? 1.0 / 30 : 1.0 / 90); // uneven frames
                for (var i = 0; i < steps; i++)
                {
                    CommandMapper.Fill(script.Poll(sim.Tick), commands);
                    sim.Step(commands);
                }
            }

            return $"{sim.Tick}:{sim.ComputeStateDigest()}";
        }

        Assert.Equal(Run(), Run());
    }
}
