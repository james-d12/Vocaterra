using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Scripted;

/// <summary>
/// B07b: a streamed world holds the simulation after a teleport until the world round the new place is made, and the steps after the teleport (a shot, a look, a walk) wait with it, so a shot is never
/// taken of the view from before the jump. The game's gate is a loop that does not fill or step while the world is held; here the hold is a number of frames.
/// </summary>
public sealed class TeleportHoldTests
{
    private const int HeldFrames = 5;

    private sealed class Run
    {
        private readonly List<VoxelCommand> _commands = [];
        private readonly (int X, int Z) _start;
        private readonly bool _gated;
        private int _held;

        public Run(IReadOnlyList<ScriptStep> steps, bool gated)
        {
            _gated = gated;
            Session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns, CreditRules.FullTyped);
            Look = new LookControl();
            Look.Set(Session.Scene.Spawn.Yaw, 0);
            var dialogue = new DialogueModel(new InputFocus(), Session.OpeningLine, Session.Data);
            _start = (Session.Simulation.Player.FeetBlock.X, Session.Simulation.Player.FeetBlock.Z);
            var hooks = new ScenarioHooks(static _ => { }, static () => { }, _ => ShotsAt.Add((_held, Moved)), () => false, WorldReady: gated ? WorldReady : null);
            Script = new ScenarioScript(Session, Look, dialogue, steps, static _ => null, hooks, Log.Add);
        }

        public Vocaterra.Game.Core.Session.VoxelSession Session { get; }

        public LookControl Look { get; }

        public ScenarioScript Script { get; }

        public List<string> Log { get; } = [];

        /// <summary>For each shot taken: the frames the game had been held when it was taken, and whether the body had moved from the start.</summary>
        public List<(int Held, bool Moved)> ShotsAt { get; } = [];

        public int TotalHeld => _held;

        private bool Moved => (Session.Simulation.Player.FeetBlock.X, Session.Simulation.Player.FeetBlock.Z) != _start;

        // The world round a place is made after HeldFrames frames of holding; a body that has not moved needs nothing made.
        private bool WorldReady() => !Moved || _held >= HeldFrames;

        public void Go()
        {
            for (var tick = 0; tick < 2000 && !Script.IsDone; tick++)
            {
                if (_gated && !WorldReady())
                {
                    _held++; // the game's gate: no step, no script, while the world is made (the frames go on)
                    continue;
                }

                _commands.Clear();
                Script.Fill(_commands);
                _commands.Insert(0, VoxelCommand.Look(Look.Yaw, Look.Pitch));
                Session.Step(_commands);
            }

            Assert.True(Script.IsDone, "the script did not finish");
        }
    }

    private static ScriptStep Tele(int x, int z) => new(ScriptStepKind.Move, VoxelStep.Teleport(x, z));

    [Fact]
    public void Given_AHeldWorld_When_AShotFollowsATeleport_Then_ItIsTakenOnlyAfterTheHold()
    {
        var run = new Run([Tele(60, 52), new ScriptStep(ScriptStepKind.Shot, Value: "after.png")], gated: true);

        run.Go();

        var shot = Assert.Single(run.ShotsAt);
        Assert.True(shot.Moved, "the shot is of the new place");
        Assert.Equal(HeldFrames, shot.Held); // the whole hold went by before the shot was asked for
        Assert.Equal(HeldFrames, run.TotalHeld);
    }

    [Fact]
    public void Given_AHeldWorld_When_ALookFollowsATeleport_Then_ItIsDoneAfterTheHold()
    {
        var run = new Run([Tele(60, 52), new ScriptStep(ScriptStepKind.Move, VoxelStep.Look(90, -5)), new ScriptStep(ScriptStepKind.Shot, Value: "a.png")], gated: true);

        run.Go();

        Assert.Equal(HeldFrames, Assert.Single(run.ShotsAt).Held);
        Assert.Equal(1024, run.Look.Yaw); // 90 degrees in the look's steps
        Assert.Contains(run.Log, static line => line.StartsWith("look:", StringComparison.Ordinal));
    }

    [Fact]
    public void Given_ABoundedWorldWithNoHold_When_AShotFollowsATeleport_Then_ItIsTakenAtOnceAsBefore()
    {
        var run = new Run([Tele(60, 52), new ScriptStep(ScriptStepKind.Shot, Value: "after.png")], gated: false);

        run.Go();

        Assert.Equal((0, true), Assert.Single(run.ShotsAt));
        Assert.Equal(0, run.TotalHeld);
    }

    [Fact]
    public void Given_ATeleportWithinTheSameWorld_When_TheWorldIsReadyAtOnce_Then_NoTickIsHeld()
    {
        var run = new Run([new ScriptStep(ScriptStepKind.Shot, Value: "first.png"), Tele(60, 52)], gated: true);

        run.Go();

        Assert.Equal((0, false), Assert.Single(run.ShotsAt)); // a shot before any move is not held
    }
}
