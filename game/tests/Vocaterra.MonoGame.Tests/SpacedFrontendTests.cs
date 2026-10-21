using Vocaterra.Game;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Nlp.Content;
using Vocaterra.Simulation;

namespace Vocaterra.MonoGame.Tests;

/// <summary>The P4b front-end pieces that need no GPU: the journal's due text, the script's skip and talk steps, the dialogue's review line and hint.</summary>
public sealed class SpacedFrontendTests
{
    private static readonly Lazy<ContentBundle> Bundle = new(() => GameBundle.Load());
    private static readonly Lazy<NounCatalog> Nouns = new(() => NounCatalog.Load(Bundle.Value));

    private static GameSession NewSession() => new(Bundle.Value, 42, GameSession.DefaultStartedAt, Nouns.Value, credit: CreditRules.FullTyped);

    private static (GameSession Session, DialogueModel Dialogue, ScriptedDialogue Script, int Ticks) Run(IReadOnlyList<ScriptStep> steps, int maxTicks = 900)
    {
        var session = NewSession();
        var focus = new InputFocus();
        var dialogue = new DialogueModel(focus, session.OpeningLine);
        var script = new ScriptedDialogue(session, dialogue, steps);
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
                var tree = session.Facts.NearestOfNoun(session.PlayerTile, "árbol")!.Tile;
                dialogue.Submit(line => session.Say(line, tree));
            }
        }

        return (session, dialogue, script, ticks);
    }

    [Theory]
    [InlineData(0, "lista para repasar")]
    [InlineData(-5, "lista para repasar")]
    [InlineData(40, "otra vez en 40 s")]
    [InlineData(60, "otra vez en 1 min")]
    [InlineData(61, "otra vez en 2 min")]
    [InlineData(480, "otra vez en 8 min")]
    [InlineData(3599, "otra vez en 60 min")]
    [InlineData(3600, "otra vez en 1 h")]
    public void TheDueTextRoundsUpToTheUnitShown(long seconds, string expected)
    {
        Assert.Equal(expected, JournalRows.DueText(seconds));
    }

    [Fact]
    public void TheJournalShowsWhenAStartedWordIsNextDue()
    {
        var session = NewSession();
        var tree = session.Facts.NearestOfNoun(session.PlayerTile, "árbol")!.Tile;
        session.Say("es un árbol", tree);

        session.SkipTime(2 * 60);
        var row = JournalRows.Build(session.Words, session.StoredTime.UnixSeconds).Single(static r => r.Lemma == "árbol");
        Assert.Equal("otra vez en 8 min", row.DueText);
        Assert.False(row.IsDue);

        session.SkipTime(8 * 60);
        row = JournalRows.Build(session.Words, session.StoredTime.UnixSeconds).Single(static r => r.Lemma == "árbol");
        Assert.Equal("lista para repasar", row.DueText);
        Assert.True(row.IsDue);

        // No time given, an unstarted word and a known word show nothing.
        Assert.All(JournalRows.Build(session.Words), static r => Assert.Null(r.DueText));
        Assert.All(
            JournalRows.Build(session.Words, session.StoredTime.UnixSeconds).Where(static r => r.Lemma != "árbol"),
            static r => Assert.Null(r.DueText));
    }

    [Fact]
    public void SkipAndTalkAreStepsInTheOrderGiven()
    {
        Assert.True(LaunchOptions.TryParse(
            ["--screenshot", "a.png", "--say", "es un árbol", "--skip", "11", "--talk", "--say", "es un árbol"], out var options, out var error));
        Assert.Empty(error);

        Assert.Equal(
            [ScriptStep.Say("es un árbol"), ScriptStep.Skip(11), ScriptStep.Talk(), ScriptStep.Say("es un árbol")],
            options.Steps);
        Assert.Equal(["es un árbol", "es un árbol"], options.Says);
        Assert.Equal(LaunchOptions.DefaultSayFrames, options.Frames);
    }

    [Theory]
    [InlineData("--skip", "0")]
    [InlineData("--skip", "-3")]
    [InlineData("--skip", "soon")]
    public void ASkipNeedsAPositiveNumberOfMinutes(string flag, string value)
    {
        Assert.False(LaunchOptions.TryParse(["--screenshot", "a.png", flag, value], out _, out var error));
        Assert.Contains("--skip", error);
    }

    [Fact]
    public void SkipAndTalkNeedAScreenshot()
    {
        Assert.False(LaunchOptions.TryParse(["--skip", "5"], out _, out var error));
        Assert.Contains("--screenshot", error);
        Assert.False(LaunchOptions.TryParse(["--talk"], out _, out error));
        Assert.Contains("--screenshot", error);
        Assert.Contains("--skip", LaunchOptions.Usage);
        Assert.Contains("--talk", LaunchOptions.Usage);
    }

    [Fact]
    public void TheDialogueAddsTheReviewQuestionEveryTimeItOpensWithOneAndAHintOnDemand()
    {
        var dialogue = new DialogueModel(new InputFocus(), "Hola.");

        dialogue.Open();
        dialogue.Close();
        dialogue.Open("¿Qué es esto?");
        dialogue.AddHint("Bien. Repasa más tarde.");

        Assert.Equal(
            [(DialogueSpeaker.Guide, "Hola."), (DialogueSpeaker.Guide, "¿Qué es esto?"), (DialogueSpeaker.Hint, "Bien. Repasa más tarde.")],
            dialogue.Lines.Select(static l => (l.Speaker, l.Text)));
    }

    [Fact]
    public void AScriptedSkipAndTalkMakeTheGuideAskAndTheAnswerEarnsTheSecondThird()
    {
        var (session, dialogue, script, ticks) = Run(
            [ScriptStep.Say("es un árbol"), ScriptStep.Skip(11), ScriptStep.Talk(), ScriptStep.Say("es un árbol")]);

        Assert.True(script.IsDone, $"The script did not finish in {ticks} ticks.");
        Assert.Equal(GameSession.DefaultStartedAt + (11 * 60) + (session.Simulation.Tick / 60), session.StoredTime.UnixSeconds);
        Assert.Contains(dialogue.Lines, static l => l.Speaker == DialogueSpeaker.Guide && l.Text.Contains("qué es esto?", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2 * WordProgress.UnitsPerUse, session.Words.RecordOf("árbol").CreditUnits);
        Assert.Null(session.Review);
    }

    [Fact]
    public void AScriptedSkipShorterThanTheGapStillEndsWithAnEarlyUseAndNoQuestion()
    {
        var (session, dialogue, script, _) = Run(
            [ScriptStep.Say("es un árbol"), ScriptStep.Skip(5), ScriptStep.Talk(), ScriptStep.Say("es un árbol")]);

        Assert.True(script.IsDone);
        Assert.DoesNotContain(dialogue.Lines, static l => l.Text.Contains("qué es esto?", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(WordProgress.UnitsPerUse, session.Words.RecordOf("árbol").CreditUnits);
        Assert.Single(session.LastWordTurn!.TooEarly);
    }

    [Fact]
    public void AScriptedRunWithSkipsIsDeterministic()
    {
        ScriptStep[] steps = [ScriptStep.Say("es un árbol"), ScriptStep.Skip(11), ScriptStep.Talk(), ScriptStep.Say("es un árbol"), ScriptStep.Skip(61), ScriptStep.Say("es un árbol")];

        var first = Run(steps);
        var second = Run(steps);

        Assert.Equal(first.Ticks, second.Ticks);
        Assert.Equal(first.Session.StoredTime, second.Session.StoredTime);
        Assert.Equal(first.Session.Simulation.ComputeStateDigest(), second.Session.Simulation.ComputeStateDigest());
        Assert.Equal(first.Dialogue.Lines, second.Dialogue.Lines.Select(static l => l).ToList());
        Assert.Equal(first.Session.Words.Records.Values.ToList(), second.Session.Words.Records.Values.ToList());
        Assert.True(first.Session.Words.RecordOf("árbol").Known);
    }
}
