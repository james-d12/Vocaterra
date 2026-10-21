using Vocaterra.Game;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Nlp.Content;
using Vocaterra.Simulation;

namespace Vocaterra.MonoGame.Tests;

/// <summary>
/// The <c>--say</c> script driven headless the way the game drives it: poll once per tick, step the session, and send a requested line
/// between ticks. It proves the walk, the focus switch, the typing and the turn without a window.
/// </summary>
public sealed class ScriptedDialogueTests
{
    private static readonly Lazy<ContentBundle> Bundle = new(() => GameBundle.Load());
    private static readonly Lazy<NounCatalog> Nouns = new(() => NounCatalog.Load(Bundle.Value));

    private static (GameSession Session, DialogueModel Dialogue, ScriptedDialogue Script, int Ticks) Run(IReadOnlyList<string> lines, int maxTicks = 600)
    {
        var session = new GameSession(Bundle.Value, 42, GameSession.DefaultStartedAt, Nouns.Value);
        var focus = new InputFocus();
        var dialogue = new DialogueModel(focus, session.OpeningLine);
        var script = new ScriptedDialogue(session, dialogue, lines);
        var input = new FocusGatedInput(script, focus);
        var commands = new List<PlayerCommand>();
        var ticks = 0;
        while (!script.IsDone && ticks < maxTicks)
        {
            CommandMapper.Fill(input.Poll(session.Simulation.Tick), commands);
            session.Step(commands);
            ticks++;
            if (dialogue.SubmitRequested)
            {
                dialogue.Submit(line => session.Say(line));
            }
        }

        return (session, dialogue, script, ticks);
    }

    [Fact]
    public void TheScriptWalksToTheGuideOpensThePanelAndSendsTheLine()
    {
        var (session, dialogue, script, ticks) = Run(["¿dónde está el árbol?"]);

        Assert.True(script.IsDone, $"The script did not finish in {ticks} ticks.");
        Assert.True(dialogue.IsOpen);
        Assert.True(session.IsNearGuide);
        Assert.Equal(1, dialogue.TurnCount);
        Assert.Equal("ask_directions", dialogue.LastTurn!.Intent);
        Assert.Equal("pointing", dialogue.LastTurn.AgentState);
        Assert.NotNull(session.Pointer);
        Assert.Contains(dialogue.Lines, l => l.Speaker == DialogueSpeaker.World && l.Text.StartsWith("El árbol está", StringComparison.Ordinal));
    }

    [Fact]
    public void SeveralLinesAreSentInOrderAndTheConversationCarriesOn()
    {
        var (session, dialogue, script, _) = Run(["hola", "blah blah", "blah blah"]);

        Assert.True(script.IsDone);
        Assert.Equal(3, dialogue.TurnCount);
        var player = dialogue.Lines.Where(l => l.Speaker == DialogueSpeaker.Player).Select(l => l.Text).ToList();
        Assert.Equal(["hola", "blah blah", "blah blah"], player);
        Assert.Equal(2, session.Conversation.Context.RepeatCountsByTag["not_communicated"]);
    }

    [Fact]
    public void ThePlayerDoesNotWalkWhileTheScriptTypes()
    {
        var session = new GameSession(Bundle.Value, 42, GameSession.DefaultStartedAt, Nouns.Value);
        var focus = new InputFocus();
        var dialogue = new DialogueModel(focus, session.OpeningLine);
        var script = new ScriptedDialogue(session, dialogue, ["hola"]);
        var input = new FocusGatedInput(script, focus);
        var commands = new List<PlayerCommand>();
        var openedAt = -1;
        for (var tick = 0; tick < 200 && openedAt < 0; tick++)
        {
            CommandMapper.Fill(input.Poll(session.Simulation.Tick), commands);
            session.Step(commands);
            if (dialogue.IsOpen)
            {
                openedAt = tick;
            }
        }

        Assert.True(openedAt > 0, "The panel should open after the walk.");
        var x = session.Simulation.Player.X;
        for (var tick = 0; tick < 30; tick++)
        {
            CommandMapper.Fill(input.Poll(session.Simulation.Tick), commands);
            session.Step(commands);
            Assert.Empty(commands);
        }

        Assert.Equal(x, session.Simulation.Player.X);
    }

    [Fact]
    public void ARunIsDeterministicTickForTick()
    {
        var first = Run(["¿dónde está el árbol?", "gracias"]);
        var second = Run(["¿dónde está el árbol?", "gracias"]);

        Assert.Equal(first.Ticks, second.Ticks);
        Assert.Equal(first.Session.Simulation.ComputeStateDigest(), second.Session.Simulation.ComputeStateDigest());
        Assert.Equal(first.Dialogue.Lines.Select(l => l.Text.Split(", ")[0]), second.Dialogue.Lines.Select(l => l.Text.Split(", ")[0]));
        Assert.Equal(first.Session.Conversation.GuideState, second.Session.Conversation.GuideState);
    }

    [Fact]
    public void AnEmptyScriptStillWalksAndOpensThePanel()
    {
        var (_, dialogue, script, _) = Run([]);

        Assert.True(script.IsDone);
        Assert.True(dialogue.IsOpen);
        Assert.Equal(0, dialogue.TurnCount);
    }
}
