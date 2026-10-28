using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests;

/// <summary>Input focus, the dialogue model's turn flow, key repeat and text wrapping: the panel's logic without a window.</summary>
public sealed class DialogueTests
{
    private static GuideTurn Turn(string line = "hola", string? feedback = null, string? world = null, string? repair = null, bool communicated = true) => new(
        line,
        communicated ? "greet" : "unknown",
        communicated,
        communicated ? 1.0 : 0.2,
        [],
        feedback,
        ["¡Hola!"],
        repair,
        "talking",
        [],
        world,
        null,
        null,
        "digest",
        Vocaterra.Nlp.Core.Verdicts.SlotMap.Empty);

    private static DialogueModel Model(out InputFocus focus)
    {
        focus = new InputFocus();
        return new DialogueModel(focus, "¡Hola! Soy la guía.");
    }

    [Fact]
    public void OpeningThePanelTakesTheKeyboardAndClosingGivesItBack()
    {
        var dialogue = Model(out var focus);
        Assert.False(focus.TextEntryActive);

        dialogue.Open();
        Assert.True(dialogue.IsOpen);
        Assert.True(focus.TextEntryActive);

        dialogue.Close();
        Assert.False(dialogue.IsOpen);
        Assert.False(focus.TextEntryActive);
    }

    [Fact]
    public void TheGuidesOpeningLineIsShownTheFirstTimeOnly()
    {
        var dialogue = Model(out _);

        dialogue.Open();
        dialogue.Close();
        dialogue.Open();

        Assert.Equal([new DialogueLine(DialogueSpeaker.Guide, "¡Hola! Soy la guía.")], dialogue.Lines);
    }

    [Fact]
    public void SubmittingShowsThePlayersLineTheVerdictNoteAndTheGuidesLinesInOrder()
    {
        var dialogue = Model(out _);
        dialogue.Open();
        dialogue.Entry.Insert("  ¿dónde está la árbol? ");
        dialogue.RequestSubmit();
        Assert.True(dialogue.SubmitRequested);

        var turn = dialogue.Submit(
            line =>
            {
                Assert.Equal("¿dónde está la árbol?", line);
                return Turn(line, feedback: "¡Eso es! Dónde está el árbol.", world: "El árbol está a la derecha, a doce bloques.");
            },
            () => 3.5);

        Assert.NotNull(turn);
        Assert.False(dialogue.SubmitRequested);
        Assert.Equal(string.Empty, dialogue.Entry.Text);
        Assert.Equal(3.5, dialogue.LastTurnMilliseconds);
        Assert.Equal(1, dialogue.TurnCount);
        Assert.Same(turn, dialogue.LastTurn);
        Assert.Equal(
            [
                new DialogueLine(DialogueSpeaker.Guide, "¡Hola! Soy la guía."),
                new DialogueLine(DialogueSpeaker.Player, "¿dónde está la árbol?"),
                new DialogueLine(DialogueSpeaker.Note, "greet, understood, 100 %, 3.5 ms"),
                new DialogueLine(DialogueSpeaker.Correction, "¡Eso es! Dónde está el árbol."),
                new DialogueLine(DialogueSpeaker.Guide, "¡Hola!"),
                new DialogueLine(DialogueSpeaker.World, "El árbol está a la derecha, a doce bloques."),
            ],
            dialogue.Lines);
    }

    [Fact]
    public void APlaytestHidesTheVerdictNoteButKeepsTheTurnsOtherLines()
    {
        var focus = new InputFocus();
        var playtest = new DialogueModel(focus, "¡Hola! Soy la guía.", showVerdictNotes: false);
        playtest.Open();
        playtest.Entry.Insert("hola");
        playtest.RequestSubmit();
        playtest.Submit(line => Turn(line, feedback: "¡Eso es!", world: "El camino está allí."), () => 2.2);

        Assert.DoesNotContain(playtest.Lines, l => l.Speaker == DialogueSpeaker.Note);
        Assert.Contains(playtest.Lines, l => l.Speaker == DialogueSpeaker.Correction && l.Text == "¡Eso es!");
        Assert.Contains(playtest.Lines, l => l.Speaker == DialogueSpeaker.World && l.Text == "El camino está allí.");
        Assert.Equal(2.2, playtest.LastTurnMilliseconds);

        // The default (scripted and developer runs) keeps the verdict line, with its confidence and milliseconds.
        var developer = new DialogueModel(new InputFocus(), "¡Hola! Soy la guía.");
        developer.Open();
        developer.Entry.Insert("hola");
        developer.RequestSubmit();
        developer.Submit(line => Turn(line), () => 2.2);
        Assert.Contains(developer.Lines, l => l.Speaker == DialogueSpeaker.Note && l.Text == "greet, understood, 100 %, 2.2 ms");
    }

    [Fact]
    public void ARepairLineIsShownWhenTheGuideDidNotUnderstand()
    {
        var dialogue = Model(out _);
        dialogue.Open();
        dialogue.Entry.Insert("blah");

        dialogue.Submit(line => Turn(line, repair: "¿Cómo?", communicated: false));

        Assert.Contains(new DialogueLine(DialogueSpeaker.Guide, "¿Cómo?"), dialogue.Lines);
        Assert.Contains(dialogue.Lines, l => l.Speaker == DialogueSpeaker.Note && l.Text.StartsWith("unknown, not understood", StringComparison.Ordinal));
    }

    [Fact]
    public void ABlankLineOrAClosedPanelSendsNothing()
    {
        var dialogue = Model(out _);
        var calls = 0;
        GuideTurn Say(string line)
        {
            calls++;
            return Turn(line);
        }

        dialogue.Entry.Insert("hola");
        dialogue.RequestSubmit(); // closed: no request
        Assert.False(dialogue.SubmitRequested);
        Assert.Null(dialogue.Submit(Say));

        dialogue.Open();
        dialogue.Entry.Clear();
        dialogue.Entry.Insert("   ");
        dialogue.RequestSubmit();
        Assert.False(dialogue.SubmitRequested);
        Assert.Null(dialogue.Submit(Say));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void ClosingDropsAPendingSendButKeepsTheHalfTypedLine()
    {
        var dialogue = Model(out _);
        dialogue.Open();
        dialogue.Entry.Insert("dónde está");
        dialogue.RequestSubmit();

        dialogue.Close();

        Assert.False(dialogue.SubmitRequested);
        Assert.Equal("dónde está", dialogue.Entry.Text);
    }

    [Fact]
    public void OldLinesAreDroppedPastTheLimit()
    {
        var dialogue = Model(out _);
        dialogue.Open();
        for (var i = 0; i < DialogueModel.MaxLines; i++)
        {
            dialogue.Entry.Insert("x");
            dialogue.Submit(line => Turn(line));
        }

        Assert.Equal(DialogueModel.MaxLines, dialogue.Lines.Count);
        Assert.DoesNotContain(dialogue.Lines, line => line.Text == "¡Hola! Soy la guía.");
    }

    [Fact]
    public void AHeldEditingKeyActsAtOnceThenRepeatsAfterTheDelay()
    {
        var repeat = new KeyRepeater();
        var acted = new List<int>();
        for (var frame = 0; frame < 40; frame++)
        {
            if (repeat.Step(down: true))
            {
                acted.Add(frame);
            }
        }

        Assert.Equal(0, acted[0]);
        Assert.Equal(KeyRepeater.DelayFrames, acted[1]);
        Assert.Equal(KeyRepeater.DelayFrames + KeyRepeater.IntervalFrames, acted[2]);
        Assert.Equal(KeyRepeater.IntervalFrames, acted[3] - acted[2]);
        Assert.DoesNotContain(acted, f => f is > 0 and < KeyRepeater.DelayFrames);
    }

    [Fact]
    public void ReleasingAKeyResetsItsRepeat()
    {
        var repeat = new KeyRepeater();
        Assert.True(repeat.Step(true));
        Assert.False(repeat.Step(true));
        Assert.False(repeat.Step(false));
        Assert.True(repeat.Step(true));
    }

    [Fact]
    public void TextWrapBreaksAtSpacesAndNeverExceedsTheWidth()
    {
        static float Measure(string text) => text.Length * 8;

        var lines = TextWrap.Wrap("El árbol está a la derecha, a doce bloques.", 12 * 8, Measure);

        Assert.Equal(["El árbol", "está a la", "derecha, a", "doce", "bloques."], lines);
        Assert.All(lines, line => Assert.True(Measure(line) <= 12 * 8));
        Assert.Equal([string.Empty], TextWrap.Wrap(string.Empty, 100, Measure));
        Assert.Equal(["corta"], TextWrap.Wrap("corta", 100, Measure));
    }

    [Fact]
    public void TextWrapCutsAWordThatIsWiderThanTheLine()
    {
        static float Measure(string text) => text.Length * 8;

        var lines = TextWrap.Wrap("abcdefghijkl x", 5 * 8, Measure);

        Assert.All(lines, line => Assert.True(Measure(line) <= 5 * 8));
        Assert.Equal(["abcde", "fghij", "kl x"], lines);
    }

    [Fact]
    public void TheTextBoxWindowKeepsTheCaretInView()
    {
        static float Measure(string text) => text.Length * 8;

        var (visible, caret) = TextWrap.Window("0123456789", 10, 5 * 8, Measure);
        Assert.Equal("56789", visible);
        Assert.Equal(5, caret);

        var (start, startCaret) = TextWrap.Window("0123456789", 0, 5 * 8, Measure);
        Assert.Equal("01234", start);
        Assert.Equal(0, startCaret);

        var (middle, middleCaret) = TextWrap.Window("0123456789", 7, 5 * 8, Measure);
        Assert.Equal("23456", middle);
        Assert.Equal(5, middleCaret);

        Assert.Equal(("abc", 3), TextWrap.Window("abc", 3, 100, Measure));
    }


}
