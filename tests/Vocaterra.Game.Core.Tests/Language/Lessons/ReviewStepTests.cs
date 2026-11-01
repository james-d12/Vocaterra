using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Language.Lessons;

/// <summary>
/// The <c>review</c> lesson step (X06, R07): it names phrases an earlier lesson taught and the NPC's line that asks each; at run time it asks the most overdue of
/// them that is due, follows the phrase's own rung of help, and is skipped when none is due. It teaches nothing, and the file is checked like any lesson's.
/// </summary>
public sealed class ReviewStepTests
{
    // The sample lesson teaches hola and gracias; "again" asks one of them back, between two explain steps.
    private static string Book(string candidates) => """
        { "schemaVersion": 1, "lessons": [
          { "id": "sample", "npc": "guide", "level": "a1", "title": "A first hello", "english": "E", "autoStart": false, "steps": [
            { "id": "say-hola", "type": "repeat", "phrase": "hola", "prompt": "guide-open", "english": "Say hello." },
            { "id": "say-gracias", "type": "answer", "phrase": "gracias", "prompt": "guide-open", "line": "guide_offer_yes.1", "translation": "Of course! Here you are.", "english": "Thank her." }
          ] },
          { "id": "again", "npc": "guide", "level": "a1", "title": "Again", "english": "E", "autoStart": false, "steps": [
            { "id": "a-intro", "type": "explain", "english": "A reminder." },
            { "id": "a-review", "type": "review", "candidates": [ CANDIDATES ], "english": "She greets you as before. Answer her." },
            { "id": "a-end", "type": "explain", "english": "Well done." }
          ] }
        ] }
        """.Replace("CANDIDATES", candidates, StringComparison.Ordinal);

    private const string Hola = "{ \"phrase\": \"hola\", \"line\": \"guide.lesson.hello.1\" }";

    private static VoxelSession AtGuide(string candidates = Hola)
    {
        var session = TestGame.NewSession(lessons: TestGame.Lessons(Book(candidates)));
        Stage.TeleportToNpc(session, "guide");
        _ = session.OpenTalk("guide");
        return session;
    }

    [Fact]
    public void AReviewStepTeachesNothingAndTakesItsMeaningFromTheLine()
    {
        var book = TestGame.Lessons(Book(Hola));

        var step = book.Find("again")!.Steps[1];

        Assert.Equal(LessonStepType.Review, step.Type);
        Assert.True(step.WaitsOnTurn);
        Assert.Equal(("hola", "villager-open", "¡Hola!"), (step.Phrase, step.Prompt, step.Line!.Text));
        Assert.Equal(TestGame.Locale.Line("guide.lesson.hello.1"), step.Translation);
        Assert.Empty(book.Find("again")!.TaughtPhrases);
    }

    [Theory]
    [InlineData("", "non-empty \"candidates\"")]
    [InlineData("{ \"phrase\": \"pedir-agua\", \"line\": \"guide.lesson.hello.1\" }", "no earlier lesson of the file teaches")]
    [InlineData("{ \"phrase\": \"hola\", \"line\": \"villager_greet.1\" }", "is spoken by villager")]
    [InlineData("{ \"phrase\": \"hola\", \"line\": \"guide.lesson.hello.1\" }, { \"phrase\": \"hola\", \"line\": \"guide.lesson.hello.1\" }", "twice")]
    public void AReviewStepThatDoesNotHoldTogetherIsALoadError(string candidates, string problem)
    {
        var ex = Assert.Throws<InvalidDataException>(() => TestGame.Lessons(Book(candidates)));

        Assert.Contains(problem, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WithNothingDueTheStepIsSkipped()
    {
        var session = AtGuide();

        Assert.True(session.Lessons.Start("again"));
        Assert.True(session.Lessons.Continue());

        Assert.Equal("a-end", session.Lessons.Current!.Id); // hola was never credited: nothing to review
        Assert.Contains(("a-review", StepOutcome.Skipped), session.Lessons.OutcomesOf("again"));
    }

    [Fact]
    public void ADuePhraseIsAskedWithItsLineAndAnsweringItIsLoggedAsReviewed()
    {
        var session = AtGuide();
        Assert.True(session.Lessons.Start("sample"));
        _ = session.Say("hola");
        _ = session.Say("gracias");
        session.Day = 2; // a new play session: hola is due
        session.Sitting = 2;

        Assert.True(session.Lessons.Start("again"));
        Assert.True(session.Lessons.Continue());

        var view = session.Lessons.View!;
        Assert.Equal((LessonStepType.Review, "Remember it", "¡Hola!"), (view.Type, view.Heading, view.Spanish));
        Assert.Equal("villager-open", session.Lessons.ActivePrompt);
        Assert.Equal(new LessonFocus("hola", false, true), session.Lessons.Focus); // the help follows the phrase's own rung

        Assert.True(session.Say("hola").Communicated);

        Assert.Equal("a-end", session.Lessons.Current!.Id);
        Assert.Contains(("a-review", StepOutcome.Done), session.Lessons.OutcomesOf("again"));
        Assert.Equal("hola, lesson", session.Events.OfType<WorldEvent>().Single(static e => e.Name == WorldEvents.ReviewAnswered).Detail);
    }

    [Fact]
    public void OfSeveralDueCandidatesTheMostOverdueIsAsked()
    {
        const string Both = "{ \"phrase\": \"gracias\", \"line\": \"guide.lesson.have.1\" }, { \"phrase\": \"hola\", \"line\": \"guide.lesson.hello.1\" }";
        var session = AtGuide(Both);
        Assert.True(session.Lessons.Start("sample"));
        _ = session.Say("hola");
        session.SkipTime(60);
        _ = session.Say("gracias"); // credited a minute after hola
        session.Day = 2;
        session.Sitting = 2;

        _ = session.Lessons.Start("again");
        _ = session.Lessons.Continue();

        Assert.Equal("hola", session.Lessons.Current!.Phrase); // the older credit, though gracias is listed first
    }
}
