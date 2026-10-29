using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Tests.Scripted;

/// <summary>
/// The scripted run's talk steps (S6): walk up to an NPC and stop at the stand-off distance, open the panel on it, say the lines and wait for the
/// replies, close the panel when the script walks on, take a screenshot in the middle. The script drives the real session, panel and worker, with
/// a fixed number of ticks and no window.
/// </summary>
public sealed class ScenarioScriptTests
{
    private static readonly Lazy<ContentBundle> Bundle = new(() => GameBundle.Load());
    private static readonly Lazy<NounCatalog> Nouns = new(() => NounCatalog.Load(Bundle.Value));

    /// <summary>The game's frame loop without the engine: the script's commands, the simulation, the turn pump, the panel and the hooks.</summary>
    private sealed class Harness : IDisposable
    {
        private readonly List<VoxelCommand> _commands = [];
        private readonly TurnPump _pump;

        public Harness(IReadOnlyList<ScriptStep> steps)
        {
            Session = RouteScene.NewSession(Bundle.Value, Nouns.Value, CreditRules.FullTyped);
            Session.StartWorker(loadSpeech: false).WaitUntilReady(30000);
            Look = new LookControl();
            Look.Set(Session.Scene.Spawn.Yaw, 0);
            Dialogue = new DialogueModel(new InputFocus(), Session.OpeningLine, Session.Data.Words);
            _pump = new TurnPump(Session, Dialogue, () => Session.AimedCell);
            var hooks = new ScenarioHooks(OpenTalk, CloseTalk, path => Shots.Add($"{path}@{Session.Simulation.Tick}"), () => false);
            Script = new ScenarioScript(Session, Look, Dialogue, steps, name => name == "antorcha" ? ItemId.Torch : name == "madera" ? ItemId.Wood : null, hooks, Log.Add);
        }

        public VoxelSession Session { get; }

        public LookControl Look { get; }

        public DialogueModel Dialogue { get; }

        public ScenarioScript Script { get; }

        public List<string> Log { get; } = [];

        public List<string> Shots { get; } = [];

        public List<NpcOpening> Openings { get; } = [];

        public int Closes { get; private set; }

        public void Go(int limit = 20000)
        {
            for (var tick = 0; tick < limit && !Script.IsDone; tick++)
            {
                _commands.Clear();
                Script.Fill(_commands);
                _commands.Insert(0, VoxelCommand.Look(Look.Yaw, Look.Pitch));
                Session.Step(_commands);
                _pump.Submit();
                var deadline = Environment.TickCount64 + 30000;
                while (Session.PendingTurns > 0 && Environment.TickCount64 < deadline)
                {
                    _ = _pump.Apply();
                    Thread.Sleep(1);
                }

                _ = _pump.Apply();
            }

            Assert.True(Script.IsDone, "the script did not finish");
        }

        public void Dispose() => Session.StopWorker();

        private void OpenTalk(string npc)
        {
            var opening = Session.OpenTalk(npc);
            Openings.Add(opening);
            Dialogue.Open(opening, NpcNames.LabelOf(npc), Session.Review?.Question);
        }

        private void CloseTalk()
        {
            Dialogue.Close();
            Session.EndTalk();
            Closes++;
        }
    }

    private static ScriptStep Talk(string npc) => new(ScriptStepKind.Talk, Value: npc);

    private static ScriptStep Say(string line) => new(ScriptStepKind.Say, Value: line);

    private static ScriptStep Move(VoxelStep step) => new(ScriptStepKind.Move, step);

    [Fact]
    public void ATalkWalksToTheNpcStopsAtTheStandOffAndOpensThePanelOnIt()
    {
        using var run = new Harness([Talk("shopkeeper")]);
        var feet = run.Session.Scene.Npc("shopkeeper").Feet;

        run.Go();

        Assert.True(run.Dialogue.IsOpen);
        Assert.Equal("shopkeeper", run.Dialogue.Npc);
        Assert.Equal("Tendero", run.Dialogue.NpcName);
        Assert.Equal("shopkeeper", run.Session.CurrentNpc);
        var body = run.Session.Simulation.Player;
        const double U = VoxelPhysics.UnitsPerBlock;
        var dx = (body.CenterX / U) - (feet.X + 0.5);
        var dz = (body.CenterZ / U) - (feet.Z + 0.5);
        var distance = Math.Sqrt((dx * dx) + (dz * dz));
        Assert.InRange(distance, 1.6, 3.2); // about two blocks: the camera is not inside the figure
        Assert.Equal("¿Qué necesitas?", Assert.Single(run.Dialogue.Lines).Text);
        Assert.Equal("shopkeeper", Assert.Single(run.Openings).Npc);
    }

    [Fact]
    public void TheStallExchangeIsTypedAndAnsweredAndTheBottleArrives()
    {
        using var run = new Harness([Talk("shopkeeper"), Say("quiero agua, por favor"), Say("sí, por favor")]);

        run.Go();

        var inventory = run.Session.Simulation.Inventory;
        Assert.Equal(1, inventory.Count(ItemId.Bottle));
        Assert.Equal(1, inventory.Count(ItemId.Bread));
        Assert.Contains(run.Dialogue.Lines, static l => l.Speaker == DialogueSpeaker.Guide && l.Text.Contains("botella", StringComparison.Ordinal));
        Assert.Equal(2, run.Dialogue.TurnCount);
        Assert.True(run.Dialogue.IsOpen); // the panel stays up at the end, for the screenshot
        Assert.Equal(0, run.Session.PendingTurns);
    }

    [Fact]
    public void ASayWithNobodyToTalkToGoesToTheGuideAsInS3()
    {
        using var run = new Harness([Say("¿dónde está el río?")]);

        run.Go();

        Assert.Equal("guide", run.Dialogue.Npc);
        Assert.Equal("Guía", run.Dialogue.NpcName);
        Assert.Equal("guide", Assert.Single(run.Openings).Npc);
        Assert.Equal("río", run.Session.Pointer!.Place);
        Assert.Equal("pointing", run.Session.Conversation.GuideState.StateId);
    }

    [Fact]
    public void WalkingOnClosesThePanelAndTheNextNpcStartsACleanPage()
    {
        var steps = new[]
        {
            Talk("shopkeeper"),
            Say("quiero agua, por favor"),
            Move(VoxelStep.WalkTo(60, 52)),
            Talk("villager"),
            new ScriptStep(ScriptStepKind.Shot, Value: "villager.png"),
        };
        using var run = new Harness(steps);

        run.Go();

        Assert.Equal(1, run.Closes); // the walk closed the shopkeeper's panel, as Esc would; the second talk found it closed
        Assert.Equal("villager", run.Dialogue.Npc);
        Assert.Equal("Aldeano", run.Dialogue.NpcName);
        Assert.Equal("Hola.", Assert.Single(run.Dialogue.Lines).Text); // the shopkeeper's page is gone
        Assert.Single(run.Shots);
        Assert.StartsWith("villager.png@", run.Shots[0], StringComparison.Ordinal);
        Assert.Equal(1, run.Session.Simulation.Inventory.Count(ItemId.Bottle)); // what the shopkeeper gave stays
        Assert.Contains(run.Session.Events.OfType<WorldEvent>(), static e => e is { Name: WorldEvents.TalkClosed, Npc: "shopkeeper" });
    }

    [Fact]
    public void TalkingStraightToAnotherNpcClosesTheOpenPanelFirst()
    {
        using var run = new Harness([Talk("shopkeeper"), Talk("villager")]);

        run.Go();

        Assert.Equal(1, run.Closes);
        Assert.Equal("villager", run.Dialogue.Npc);
        Assert.Equal(2, run.Openings.Count);
    }

    [Fact]
    public void ALeaveStepClosesThePanelAndTheEndOfTheScriptIsDetected()
    {
        using var run = new Harness([Talk("guide"), new ScriptStep(ScriptStepKind.Leave)]);

        run.Go();

        Assert.False(run.Dialogue.IsOpen);
        Assert.Equal("guide", run.Session.CurrentNpc);
        Assert.Contains(run.Session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.TalkClosed);
    }

    [Fact]
    public void TheBodyStepsKeepTheirOrderWithTheTalkSteps()
    {
        var steps = new[]
        {
            Move(VoxelStep.Look(90, 0)),
            Move(VoxelStep.WalkTo(176, 148)), // a point of the trail, on the way down from the player's camp
            Talk("guide"),
            Say("hola"),
        };
        using var run = new Harness(steps);

        run.Go();

        Assert.Contains(run.Log, static l => l.StartsWith("look:", StringComparison.Ordinal));
        Assert.Contains(run.Log, static l => l.StartsWith("walk-to 176,148: arrived", StringComparison.Ordinal));
        Assert.True(run.Log.Count(static l => l.StartsWith("walk-to ", StringComparison.Ordinal)) >= 2, "the walk to the guide came after the walk to 176,148");
        Assert.Equal("greet", run.Dialogue.LastTurn!.Intent);
    }

    [Fact]
    public void TwoRunsOfTheSameScriptGiveTheSameWorld()
    {
        string Digest()
        {
            using var run = new Harness([Talk("villager"), Say("¿dónde está el río?")]);
            run.Go();
            return run.Session.Simulation.ComputeStateDigest() + "|" + string.Join('|', run.Session.Events.Select(static e => e.Text)) + "|" + run.Session.Conversation.Context.HostStateHash;
        }

        Assert.Equal(Digest(), Digest());
    }
}
