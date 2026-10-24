using Vocaterra.Game.Core.Language.Learner;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Game.Core.Tests;

/// <summary>
/// The session summary card (X04, R01): a fixed record gives a fixed text (the lines are checked as written), the card has the same six lines whatever the session
/// did, and it names things done, never failures, scores or levels (D-game-10).
/// </summary>
public sealed class SessionSummaryTests
{
    private static readonly TextSpan Span = new(0, 1);

    private static GuideWord Word(string text, string lemma, string pos, string? person = null, string? number = null, string? tense = null) =>
        new(text, Span, lemma, pos, WordRole.Word, person, number, tense);

    private static InputEntry Line(long id, int session, params GuideWord[] words) =>
        new(id, session, 0, InputSource.Typed, "line", "shopkeeper", "stall-need", InputPlace.Dialogue, null, "request_item", true, false, [], words);

    private static LessonRecordRow Lesson(string id, string title) =>
        new(id, title, string.Empty, LessonStatus.InProgress, 3, [], null, 0, 0, 0, 0, []);

    private static LessonEvent LessonStep(string lesson, LessonEventKind kind) =>
        new(10, 1, kind, lesson, 1, "u1", "explain", StepOutcome.Done);

    // The fixed record: session 1 gave "agua"; session 2 used "quiero" and "quieres", "agua" again and "pan" for the first time.
    private static SummaryFacts FixedFacts(PhraseSheet phrases)
    {
        var history = new List<InputEntry>
        {
            Line(1, 1, Word("agua", "agua", "noun")),
            Line(2, 2, Word("quiero", "querer", "verb", "1", "s", "pres"), Word("agua", "agua", "noun")),
            Line(3, 2, Word("quieres", "querer", "verb", "2", "s", "pres"), Word("pan", "pan", "noun"), new GuideWord("Luna", Span, "Luna", "name", WordRole.Name)),
        };
        var events = new List<GameEvent>
        {
            LessonStep("first-words", LessonEventKind.StepDone),
            LessonStep("first-words", LessonEventKind.StepDone),
            LessonStep("first-words", LessonEventKind.LessonCompleted),
            LessonStep("personas", LessonEventKind.StepDone),
            new PhraseUse(20, 1, "pedir-agua", PhraseUseKind.Unaided, "shopkeeper", "stall-need", "Quiero agua, por favor.", null, InputKind.Typed, true, SupportLevel.CardAndAudio),
            new PhraseUse(21, 1, PhraseIds.DontKnow, PhraseUseKind.DontKnow, "villager", "villager-open", "no sé", null, InputKind.Button, true, SupportLevel.CardAndAudio, ["donde-esta-el-rio"]),
            new PhraseUse(22, 1, "si-por-favor", PhraseUseKind.Supported, "guide", "guide-offer", "Sí, por favor.", null, InputKind.Typed, false, SupportLevel.CardAndAudio),
        };
        var levels = new List<PhraseRecord>
        {
            new("pedir-agua", SupportLevel.None),
            new("si-por-favor", SupportLevel.CardAndAudio),
            new("donde-esta-el-rio", SupportLevel.EnglishAndCard),
        };
        return new SummaryFacts(2, history, events, [Lesson("first-words", "First words with Luna"), Lesson("personas", "Personas: he, she, we, they")], phrases, levels);
    }

    [Fact]
    public void AFixedRecordGivesAFixedSummary()
    {
        var session = TestGame.NewSession();

        var lines = SessionSummary.Lines(FixedFacts(session.Phrases), session.English);

        Assert.Equal(
        [
            "Lessons: First words with Luna done; Personas: he, she, we, they begun (1 steps done)",
            "New words: pan",
            "Said without help: Quiero agua, por favor",
            "Verb forms used: quieres (present, 2nd singular), quiero (present, 1st singular)",
            "Next time, practise: ¿Dónde está el río? (you said I don't know); Sí, por favor (a slip to fix)",
            "Replay a lesson: press J for the book, choose a finished lesson, press Practise again, then talk to its guide.",
        ], lines);
        Assert.Equal(lines, SessionSummary.Lines(FixedFacts(session.Phrases), session.English)); // the same record, the same text
    }

    [Fact]
    public void AnEmptySessionStillHasTheSameSixLinesAndNamesNoFailure()
    {
        var session = TestGame.NewSession();
        var facts = new SummaryFacts(1, [], [], [Lesson("first-words", "First words with Luna")], session.Phrases, []);

        var lines = SessionSummary.Lines(facts, session.English);

        Assert.Equal(
        [
            "Lessons: none this session",
            "New words: none this session",
            "Said without help: none this session",
            "Verb forms used: none this session",
            "Next time, practise: nothing is flagged from this session",
            "Replay a lesson: press J for the book, choose a finished lesson, press Practise again, then talk to its guide.",
        ], lines);
    }

    [Fact]
    public void ANoSeThatWasTheAnswerOfALessonIsNotFlaggedForPractice()
    {
        var session = TestGame.NewSession();
        var facts = new SummaryFacts(
            1,
            [],
            [new PhraseUse(5, 1, PhraseIds.DontKnow, PhraseUseKind.DontKnow, "villager", "villager-open", "no sé", null, InputKind.Button, true, SupportLevel.None, [PhraseIds.DontKnow])],
            [],
            session.Phrases,
            []);

        var practise = SessionSummary.Lines(facts, session.English).Single(static l => l.StartsWith("Next time, practise:", StringComparison.Ordinal));

        Assert.Equal("Next time, practise: nothing is flagged from this session", practise);
    }

    [Fact]
    public void TheCardNamesThingsDoneAndNoFailureCountsScoresOrLevels()
    {
        var session = TestGame.NewSession();

        var text = string.Join("\n", SessionSummary.Lines(FixedFacts(session.Phrases), session.English));

        foreach (var banned in new[] { "dont_know", "help uses", "score", "CEFR", "A1", "%", "wrong", "failed", "mistake" })
        {
            Assert.DoesNotContain(banned, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ThePractiseLineNamesAtMostTwoPhrasesAndNoSeComesFirst()
    {
        var session = TestGame.NewSession();
        var lines = SessionSummary.Lines(FixedFacts(session.Phrases), session.English);
        var practise = lines.Single(static l => l.StartsWith("Next time, practise:", StringComparison.Ordinal));

        Assert.Equal(2, practise.Split("; ").Length);
        Assert.Contains("you said I don't know", practise, StringComparison.Ordinal);
        Assert.DoesNotContain("Quiero agua", practise, StringComparison.Ordinal); // said without help, and its support is off: nothing to practise
    }
}
