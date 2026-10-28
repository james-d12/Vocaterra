using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Game.Core.Tests;

/// <summary>
/// Spaced credit and the guide's review (D-game-6 as amended): the first correct use earns a third at any time, the second only after 10 minutes
/// of stored time (or in a new play session), the third only an hour after the second; early uses are logged without colour; a wrong name earns
/// nothing; the guide asks about a due word and grades the answer against the tile she pointed at.
/// </summary>
public sealed class SpacedCreditTests
{
    private static long At(int minutes, int seconds = 0) => GuideSession.DefaultStartedAt + (minutes * 60L) + seconds;

    private static SlotMap Object(string lemma) => SlotMap.From(new Dictionary<string, string> { ["object"] = lemma });

    private static GuideTurn Name(string lemma, NameCheck naming = NameCheck.Right, string? target = null) => new(
        "es un " + lemma,
        "name_object",
        true,
        1.0,
        [],
        null,
        [],
        null,
        "talking",
        [],
        null,
        null,
        null,
        "digest",
        Object(lemma),
        naming,
        target);

    private static WordTurnResult Use(WordProgress words, string lemma, long now, NameCheck naming = NameCheck.Right)
        => words.RecordTurn(Name(lemma, naming), InputKind.Typed, CreditRules.FullTyped, now);

    [Fact]
    public void TheGapsAreOneListInTheCreditRules()
    {
        var rules = CreditRules.FullTyped;

        Assert.Equal([600L, 3600L], rules.Gaps);
        Assert.Equal(0, rules.GapAfter(0));
        Assert.Equal(600, rules.GapAfter(1));
        Assert.Equal(3600, rules.GapAfter(2));
        Assert.Equal(3600, rules.GapAfter(5)); // the last gap repeats (half credit takes more uses)
        Assert.Equal(30, (rules with { GapSeconds = [30L] }).GapAfter(2));
    }

    [Fact]
    public void ThreeUsesWithinSecondsGiveOneThirdAndTwoEarlyOutcomes()
    {
        var session = TestGame.NewSession();
        var tree = TestGame.NearestOf(session, "árbol");

        var first = session.Say("es un árbol", tree);
        Assert.Equal([CreditOutcome.Credited], session.LastWordTurn!.Outcomes.Select(static o => o.Outcome));
        session.Say("es un árbol", tree);
        Assert.Equal([CreditOutcome.TooEarly], session.LastWordTurn!.Outcomes.Select(static o => o.Outcome));
        Assert.Equal(600, session.LastWordTurn.TooEarly.Single().SecondsRemaining);
        session.Say("es un árbol", tree);

        var record = session.Words.RecordOf("árbol");
        Assert.True(first.Communicated);
        Assert.Equal(WordProgress.UnitsPerUse, record.CreditUnits);
        Assert.Equal(1.0 / 3, session.Words.SaturationOf("árbol"), 9);
        Assert.False(record.Known);
        // The early uses still count in the journal: said and correct.
        Assert.Equal(3, record.Spoken);
        Assert.Equal(3, record.SpokenCorrect);
        Assert.Equal(1, record.CorrectTurns);
    }

    [Fact]
    public void TenMinutesThenOneHourMakeTheWordKnown()
    {
        var words = new WordProgress(TestGame.Nouns);

        Assert.Equal(CreditOutcome.Credited, Use(words, "árbol", At(0)).Outcomes.Single().Outcome);
        Assert.Equal(CreditOutcome.Credited, Use(words, "árbol", At(10)).Outcomes.Single().Outcome);
        var third = Use(words, "árbol", At(70));

        Assert.Equal(["árbol"], third.NewlyKnown);
        Assert.True(words.RecordOf("árbol").Known);
        Assert.Equal(1.0, words.SaturationOf("árbol"));
    }

    [Fact]
    public void NineMinutesFiftyNineSecondsIsTooEarlyAndTenMinutesIsNot()
    {
        var words = new WordProgress(TestGame.Nouns);
        Use(words, "árbol", At(0));

        var early = Use(words, "árbol", At(9, 59)).Outcomes.Single();
        Assert.Equal(CreditOutcome.TooEarly, early.Outcome);
        Assert.Equal(1, early.SecondsRemaining);
        Assert.Equal(WordProgress.UnitsPerUse, words.RecordOf("árbol").CreditUnits);

        // An early use does not move the clock: the gap still runs from the credited use.
        Assert.Equal(CreditOutcome.Credited, Use(words, "árbol", At(10)).Outcomes.Single().Outcome);
        Assert.Equal(2 * WordProgress.UnitsPerUse, words.RecordOf("árbol").CreditUnits);
    }

    [Fact]
    public void TheThirdUseNeedsAnHourAfterTheSecondNotAfterTheFirst()
    {
        var words = new WordProgress(TestGame.Nouns);
        Use(words, "árbol", At(0));
        Use(words, "árbol", At(10));

        var early = Use(words, "árbol", At(69, 59)).Outcomes.Single();

        Assert.Equal(CreditOutcome.TooEarly, early.Outcome);
        Assert.Equal(1, early.SecondsRemaining);
        Assert.False(words.RecordOf("árbol").Known);
        Assert.Equal(CreditOutcome.Credited, Use(words, "árbol", At(70)).Outcomes.Single().Outcome);
        Assert.True(words.RecordOf("árbol").Known);
    }

    [Fact]
    public void ADifferentPlaySessionBypassesTheGap()
    {
        var words = new WordProgress(TestGame.Nouns, session: 1);
        Use(words, "árbol", At(0));
        Assert.Equal(CreditOutcome.TooEarly, Use(words, "árbol", At(1)).Outcomes.Single().Outcome);

        words.Session = 2; // a saved game loaded again: the progress keeps the session of each credited use
        Assert.Equal(CreditOutcome.Credited, Use(words, "árbol", At(1)).Outcomes.Single().Outcome);
        Assert.Equal(2, words.RecordOf("árbol").LastCreditedSession);

        // The gap after the second credited use now runs in session 2: an immediate third use is early, a session 3 use is not.
        Assert.Equal(CreditOutcome.TooEarly, Use(words, "árbol", At(2)).Outcomes.Single().Outcome);
        words.Session = 3;
        Assert.Equal(CreditOutcome.Credited, Use(words, "árbol", At(2)).Outcomes.Single().Outcome);
        Assert.True(words.RecordOf("árbol").Known);
    }

    [Fact]
    public void AGameSessionStartedInAnEarlierSessionCreditsTheWordsOfALaterOne()
    {
        var session = TestGame.NewSession(TestGame.Seed, CreditRules.FullTyped);
        var tree = TestGame.NearestOf(session, "árbol");
        session.Say("es un árbol", tree);
        session.Words.Session = 2;

        session.Say("es un árbol", tree);

        Assert.Equal(2 * WordProgress.UnitsPerUse, session.Words.RecordOf("árbol").CreditUnits);
    }

    [Fact]
    public void AWrongNameEarnsNoColourForTheNounNamed()
    {
        var words = new WordProgress(TestGame.Nouns);

        var wrong = Use(words, "piedra", At(0), NameCheck.Wrong);

        Assert.Equal(CreditOutcome.NamedWrong, wrong.Outcomes.Single().Outcome);
        Assert.Empty(wrong.Credited);
        Assert.Equal(0, words.RecordOf("piedra").CreditUnits);
        Assert.Equal(1, words.RecordOf("piedra").Spoken);
        Assert.Equal(0, words.RecordOf("piedra").SpokenCorrect);

        // It does not start the clock either: the next right use is the first credited one and earns at once.
        Assert.Equal(CreditOutcome.Credited, Use(words, "piedra", At(0, 5)).Outcomes.Single().Outcome);
    }

    [Fact]
    public void AWrongNameOrANameWithNoTargetEarnsNoColourInTheCore()
    {
        var session = TestGame.NewSession();
        var tree = TestGame.NearestOf(session, "árbol");

        var wrong = session.Say("es una piedra", tree);
        Assert.Equal(NameCheck.Wrong, wrong.Naming);
        Assert.Equal(0.0, session.Words.SaturationOf("piedra"));

        var noCursor = session.Say("es una piedra");
        Assert.Equal(NameCheck.NoTarget, noCursor.Naming);
        Assert.Equal(0.0, session.Words.SaturationOf("piedra"));
        Assert.Equal(CreditOutcome.NoTarget, session.LastWordTurn!.Outcomes.Single().Outcome);
        Assert.Equal(2, session.Words.RecordOf("piedra").Spoken); // still logged as said
    }

    [Fact]
    public void ANameWithNoTargetEarnsNothingButAQuestionAboutTheNounStillDoes()
    {
        var session = TestGame.NewSession();

        session.Say("es un árbol");
        Assert.Equal(0, session.Words.RecordOf("árbol").CreditUnits);
        Assert.Equal(1, session.Words.RecordOf("árbol").Spoken);

        var asked = session.Say("¿dónde está el árbol?");
        Assert.Equal("ask_directions", asked.Intent);
        Assert.Equal(CreditOutcome.Credited, session.LastWordTurn!.Outcomes.Single().Outcome);
        Assert.Equal(WordProgress.UnitsPerUse, session.Words.RecordOf("árbol").CreditUnits);
    }

    [Fact]
    public void CopyingTheGuidesCorrectionAfterAWrongReviewAnswerIsTooEarly()
    {
        var session = TestGame.NewSession();
        var tree = TestGame.NearestOf(session, "árbol");
        session.Say("es un árbol", tree);
        session.SkipTime(11 * 60);
        session.BeginTalk();

        session.Say("es una piedra"); // wrong: she says "No, es un árbol."
        Assert.Equal(1, session.Words.RecordOf("árbol").Lapses);
        Assert.Equal(WordProgress.UnitsPerUse, session.Words.RecordOf("árbol").CreditUnits); // nothing is taken away

        session.Say("es un árbol", tree);

        Assert.Equal(CreditOutcome.TooEarly, session.LastWordTurn!.Outcomes.Single().Outcome);
        Assert.Equal(WordProgress.UnitsPerUse, session.Words.RecordOf("árbol").CreditUnits);
        session.SkipTime(10 * 60);
        session.Say("es un árbol", tree);
        Assert.Equal(2 * WordProgress.UnitsPerUse, session.Words.RecordOf("árbol").CreditUnits);
    }

    [Fact]
    public void CopyingTheGuidesCorrectionAfterAWrongNamingOutsideAReviewIsTooEarly()
    {
        var words = new WordProgress(TestGame.Nouns);
        Use(words, "árbol", At(0));

        words.RecordTurn(Name("piedra", NameCheck.Wrong, target: "árbol"), InputKind.Typed, CreditRules.FullTyped, At(12));
        var copy = Use(words, "árbol", At(12, 5));

        Assert.Equal(CreditOutcome.TooEarly, copy.Outcomes.Single().Outcome);
        Assert.Equal(595, copy.Outcomes.Single().SecondsRemaining);
        Assert.Equal(1, words.RecordOf("árbol").Lapses);
        Assert.Equal(CreditOutcome.Credited, Use(words, "árbol", At(22)).Outcomes.Single().Outcome);
    }

    [Fact]
    public void ALapseOnAWordWithNoCreditedUseChangesNothing()
    {
        var words = new WordProgress(TestGame.Nouns);

        words.RecordTurn(Name("piedra", NameCheck.Wrong, target: "árbol"), InputKind.Typed, CreditRules.FullTyped, At(0));

        Assert.False(words.Records.ContainsKey("árbol") && words.RecordOf("árbol").Lapses > 0);
        Assert.Equal(0, words.RecordOf("árbol").Lapses);
        Assert.Equal(0, words.RecordOf("árbol").LastCreditedAt);
        Assert.Equal(CreditOutcome.Credited, Use(words, "árbol", At(0, 1)).Outcomes.Single().Outcome); // its first use may come at any time
    }

    [Fact]
    public void ALapseNeverTouchesAKnownWord()
    {
        var words = new WordProgress(TestGame.Nouns);
        Use(words, "árbol", At(0));
        Use(words, "árbol", At(10));
        Use(words, "árbol", At(70));
        Assert.True(words.RecordOf("árbol").Known);

        words.RecordTurn(Name("piedra", NameCheck.Wrong, target: "árbol"), InputKind.Typed, CreditRules.FullTyped, At(80));

        Assert.Equal(0, words.RecordOf("árbol").Lapses);
        Assert.True(words.RecordOf("árbol").Known);
    }

    [Fact]
    public void DueWordsAreStartedNotKnownAndPastTheirGapMostOverdueFirstThenOrdinal()
    {
        var words = new WordProgress(TestGame.Nouns);
        Use(words, "piedra", At(0));
        Use(words, "hierba", At(0, 60));
        Use(words, "árbol", At(0));
        Use(words, "tierra", At(5)); // not due yet at 11 minutes' mark below? due at minute 15
        Assert.Empty(words.DueWords(At(0, 599), CreditRules.FullTyped));
        Assert.Null(words.SecondsUntilNextCredit("lámpara", At(0), CreditRules.FullTyped)); // never credited: nothing to wait for

        var due = words.DueWords(At(11), CreditRules.FullTyped);

        // piedra and árbol are 60 s overdue; hierba is 0 s overdue; ties by ordinal lemma (p before á).
        Assert.Equal(["piedra", "árbol", "hierba"], due.Select(static d => d.Lemma));
        Assert.Equal(60, due[0].OverdueSeconds);
        Assert.Equal(0, due[2].OverdueSeconds);
        Assert.Equal(240, words.SecondsUntilNextCredit("tierra", At(11), CreditRules.FullTyped));
        Assert.Equal(0, words.SecondsUntilNextCredit("piedra", At(11), CreditRules.FullTyped));

        // A known word is never due.
        Use(words, "piedra", At(11));
        Use(words, "piedra", At(72));
        Assert.True(words.RecordOf("piedra").Known);
        Assert.DoesNotContain(words.DueWords(At(500), CreditRules.FullTyped), static d => d.Lemma == "piedra");
        Assert.Null(words.SecondsUntilNextCredit("piedra", At(500), CreditRules.FullTyped));
    }

    [Fact]
    public void ANewPlaySessionMakesStartedWordsDueAtOnce()
    {
        var words = new WordProgress(TestGame.Nouns);
        Use(words, "árbol", At(0));
        Assert.Empty(words.DueWords(At(1), CreditRules.FullTyped));

        words.Session = 2;

        Assert.Equal(["árbol"], words.DueWords(At(1), CreditRules.FullTyped).Select(static d => d.Lemma));
    }

    [Fact]
    public void TheGuideAsksAboutTheNearestExampleOfADueWordAndGradesAgainstThatTile()
    {
        var session = TestGame.NewSession();
        var tree = TestGame.NearestOf(session, "árbol");
        session.Say("es un árbol", tree);
        session.SkipTime(11 * 60);

        var review = session.BeginTalk();

        Assert.NotNull(review);
        Assert.Equal("árbol", review.Lemma);
        Assert.Equal(tree, review.Target); // the nearest example, from where the player stands
        Assert.Contains("qué es esto?", review.Question, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new PointerMarker("árbol", tree, Quiz: true), session.Pointer);

        // The mouse is on a stone tile, but the answer is graded against the pointed tile.
        var stone = TestGame.NearestOf(session, "piedra");
        var answer = session.Say("es un árbol", stone);

        Assert.Equal(NameCheck.Right, answer.Naming);
        Assert.Equal(CreditOutcome.Credited, session.LastWordTurn!.Outcomes.Single().Outcome);
        Assert.Equal(2 * WordProgress.UnitsPerUse, session.Words.RecordOf("árbol").CreditUnits);
        Assert.Null(session.Review); // answered: the question and its arrow are gone
        Assert.Null(session.Pointer);
    }

    [Fact]
    public void AWrongAnswerToTheReviewIsCorrectedAndEarnsNothing()
    {
        var session = TestGame.NewSession();
        var tree = TestGame.NearestOf(session, "árbol");
        session.Say("es un árbol", tree);
        session.SkipTime(11 * 60);
        session.BeginTalk();

        var answer = session.Say("es una piedra");

        Assert.Equal(NameCheck.Wrong, answer.Naming);
        Assert.Contains("un árbol", answer.WorldLine);
        Assert.Equal(0, session.Words.RecordOf("piedra").CreditUnits);
        Assert.Equal(WordProgress.UnitsPerUse, session.Words.RecordOf("árbol").CreditUnits);
    }

    [Fact]
    public void ALineTheGuideDoesNotUnderstandLeavesTheReviewOpen()
    {
        var session = TestGame.NewSession();
        var tree = TestGame.NearestOf(session, "árbol");
        session.Say("es un árbol", tree);
        session.SkipTime(11 * 60);
        var review = session.BeginTalk();

        var garbled = session.Say("blah blah");

        Assert.False(garbled.Communicated);
        Assert.Equal(review, session.Review);
        Assert.NotNull(session.Pointer);
    }

    [Fact]
    public void NoQuestionIsAskedWhenNothingIsDue()
    {
        var session = TestGame.NewSession();
        Assert.Null(session.BeginTalk()); // nothing started

        var tree = TestGame.NearestOf(session, "árbol");
        session.Say("es un árbol", tree);
        session.SkipTime((10 * 60) - 1);
        Assert.Null(session.BeginTalk()); // started, gap not passed
        Assert.Null(session.Review);
        Assert.Null(session.Pointer);

        session.SkipTime(1);
        Assert.NotNull(session.BeginTalk()); // exactly the gap
    }

    [Fact]
    public void TheReviewWorksForAnyCatalogueNoun()
    {
        var session = TestGame.NewSession();
        var stone = TestGame.NearestOf(session, "piedra");
        session.Say("es una piedra", stone);
        session.SkipTime(11 * 60);

        var review = session.BeginTalk();

        Assert.Equal("piedra", review!.Lemma);
        Assert.Equal(stone, review.Target);
    }

    [Fact]
    public void SkippedTimeMovesTheStoredTimeWithoutStepping()
    {
        var session = TestGame.NewSession();
        var tick = session.Simulation.Tick;

        session.SkipTime(11 * 60);

        Assert.Equal(tick, session.Simulation.Tick);
        Assert.Equal(GuideSession.DefaultStartedAt + (11 * 60), session.StoredTime.UnixSeconds);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.SkipTime(-1));
    }

    [Fact]
    public void ASpacedConversationGivesTheSameCountsAndLinesEachTime()
    {
        static (List<string> Lines, List<WordRecord> Records) Play()
        {
            var session = TestGame.NewSession();
            var tree = TestGame.NearestOf(session, "árbol");
            var lines = new List<string>();
            lines.AddRange(session.Say("es un árbol", tree).GuideLines);
            session.SkipTime(11 * 60);
            lines.Add(session.BeginTalk()!.Question);
            lines.AddRange(session.Say("es un árbol").GuideLines);
            session.SkipTime(61 * 60);
            lines.AddRange(session.Say("es un árbol", tree).GuideLines);
            return (lines, session.Words.Records.Values.ToList());
        }

        var first = Play();
        var second = Play();

        Assert.Equal(first.Lines, second.Lines);
        Assert.Equal(first.Records, second.Records);
        Assert.True(first.Records.Single(static r => r.Lemma == "árbol").Known);
    }

    [Fact]
    public void TheBundleHasTheReviewPromptAndPool()
    {
        var bundle = TestGame.Bundle;

        Assert.Equal("¿Qué es esto?", bundle.Prompts[NpcService.GuideReviewPrompt].Text);
        Assert.Equal(bundle.Prompts[NpcService.GuidePrompt].AllowedIntentIds, bundle.Prompts[NpcService.GuideReviewPrompt].AllowedIntentIds);
        Assert.True(bundle.Dialogue.TryGetPool("guide.review.ask", out var pool));
        Assert.All(pool.Variants, static v => Assert.Contains("¿", v.Text));
        Assert.All(pool.Variants, static v => Assert.Contains("qué es esto?", v.Text, StringComparison.OrdinalIgnoreCase));
    }
}
