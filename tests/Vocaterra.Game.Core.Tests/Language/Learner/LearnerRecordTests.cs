using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Checks;
using Vocaterra.Game.Core.Language.Learner;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Game.Core.Tests.Language.Learner;

/// <summary>
/// The learner's record (S19): the history of every line the game took, the word record folded from it, the lesson record written by the lesson runner,
/// the save of both (version 2) and the loading of a version 1 save. The lines are the real evaluator's (the typed path needs no model; the spoken test
/// is gated on the models like the other spoken tests).
/// </summary>
public sealed class LearnerRecordTests
{
    // The worker's outcomes, in order, as the game applies them.
    private static List<AppliedTurn> Drain(VoxelSession session, int count)
    {
        var all = new List<AppliedTurn>();
        var deadline = Environment.TickCount64 + 30000;
        while (all.Count < count)
        {
            all.AddRange(session.Collect());
            if (all.Count >= count)
            {
                break;
            }

            Assert.True(Environment.TickCount64 < deadline, $"Only {all.Count} of {count} turns came back.");
            Thread.Sleep(2);
        }

        return all;
    }

    // A learner's line as the record gets it: the entries are plain values, so this is an exact picture of the record.
    private static string Show(object value) => CanonicalJson.Serialize(value);

    // A history entry of the test's own (for the bound and the fold): a typed line with one noun or verb form.
    private static InputEntry Entry(long id, string text, IReadOnlyList<GuideWord> words, bool communicated = true, bool held = false, InputSource source = InputSource.Typed, IReadOnlyList<GuideError>? errors = null) =>
        new(id, 1, GuideSession.DefaultStartedAt + id, source, text, NpcService.GuideAgent, "guide-open", InputPlace.Dialogue, null, "ask_directions", communicated, held, errors ?? [], words);

    private static GuideWord Noun(string text, string lemma, int start, int length, string? gender = "f", string? number = "sg") =>
        new(text, new TextSpan(start, length), lemma, "noun", WordRole.Word, null, number, null, null, gender);

    private static GuideWord Verb(string text, string lemma, int start, int length) =>
        new(text, new TextSpan(start, length), lemma, "verb", WordRole.Word, "1", "sg", "pres", "ind", null);

    // A check answer (S11) as the record gets it: a line of the check battery, said to no NPC (S19b: in the history, not in the word record).
    private static InputEntry CheckEntry(long id, string text, IReadOnlyList<GuideWord> words) =>
        Entry(id, text, words) with { Place = InputPlace.Check, Unit = "say.pedir-agua", Npc = null };

    [Fact]
    public void ATypedTurnLandsInTheHistoryWithItsSourceSessionAndStoredTime()
    {
        using var session = TestGame.NewSession(credit: CreditRules.Default);
        session.OpenTalk("shopkeeper");
        var worker = session.StartWorker(loadSpeech: false);
        Assert.True(worker.WaitUntilReady(30000), "The worker did not load.");

        var request = session.SubmitTyped("quiero dos aguas");
        _ = Drain(session, 1);

        var entry = Assert.Single(session.Learner.History);
        Assert.Equal(1, entry.Id);
        Assert.Equal(InputSource.Typed, entry.Source);
        Assert.Equal("quiero dos aguas", entry.Text);
        Assert.Equal("shopkeeper", entry.Npc);
        Assert.Equal("stall-need", entry.Prompt);
        Assert.Equal(InputPlace.Dialogue, entry.Place);
        Assert.Null(entry.Unit);
        Assert.True(entry.Communicated);
        Assert.False(entry.Held);
        Assert.Equal(1, entry.Session);
        Assert.Equal(request.Now.UnixSeconds, entry.StoredTime);
        Assert.Equal(2, session.Learner.NextId);
    }

    [ModelFact(needsKokoro: true)]
    public void ASpokenTurnLandsInTheHistoryAsVoice()
    {
        using var session = TestGame.NewSession(credit: CreditRules.Default);
        session.OpenTalk("shopkeeper");
        Assert.True(session.StartWorker().WaitUntilReady(60000));

        _ = session.SubmitSpeak("quiero dos aguas");
        var applied = Drain(session, 1).Single();

        var entry = Assert.Single(session.Learner.History);
        Assert.Equal(InputSource.Voice, entry.Source);
        Assert.Equal(applied.Turn!.Line, entry.Text);
        Assert.Equal("shopkeeper", entry.Npc);
    }

    [Fact]
    public void ASynchronousTurnRecordsItsLineLikeTheWorkerDoes()
    {
        var session = TestGame.NewSession(credit: CreditRules.Default);
        session.OpenTalk("shopkeeper");

        _ = session.Say("quiero dos aguas");

        var entry = Assert.Single(session.Learner.History);
        Assert.Equal("quiero dos aguas", entry.Text);
        Assert.Equal(InputSource.Typed, entry.Source);
    }

    [Fact]
    public void QuieroDosAguasGivesTheNounAguaAndTheVerbQuerer()
    {
        var session = TestGame.NewSession(credit: CreditRules.Default);
        session.OpenTalk("shopkeeper");

        _ = session.Say("quiero dos aguas");

        var noun = Assert.Single(session.Learner.Nouns);
        Assert.Equal("agua", noun.Lemma);
        Assert.Equal("f", noun.Gender);
        Assert.Equal(1, noun.Times);
        Assert.Equal(1, noun.TimesCorrect);
        var aguas = Assert.Single(noun.Forms);
        Assert.Equal("aguas", aguas.Form);
        Assert.Equal("pl", aguas.Number); // the core reads the form as plural
        Assert.Equal("quiero dos aguas", noun.LastSentence);

        var verb = Assert.Single(session.Learner.Verbs);
        Assert.Equal("querer", verb.Lemma);
        Assert.Equal(1, verb.Times);
        var quiero = Assert.Single(verb.Forms);
        Assert.Equal("quiero", quiero.Form);
        Assert.Equal("1", quiero.Person);
        Assert.Equal("sg", quiero.Number);
        Assert.Equal("pres", quiero.Tense);
        Assert.Equal("quiero dos aguas", verb.LastSentence);
    }

    [Fact]
    public void AnErrorSpanMarksAWordUsedButNotCorrect()
    {
        var session = TestGame.NewSession(credit: CreditRules.Default);
        session.OpenTalk("shopkeeper");

        _ = session.Say("quiero dos aguas"); // understood at the stall's need: "quiero" and "aguas" are right
        var turn = session.Say("quiero agua, por favor"); // at the offer, "quiero" is wrong (vocab.wrong over its span) and the line is not understood

        Assert.Contains(turn.Errors, static e => e.Span.Start == 0 && e.Span.Length == 6);
        var verb = Assert.Single(session.Learner.Verbs);
        Assert.Equal(2, verb.Times);
        Assert.Equal(1, verb.TimesCorrect);
        var quiero = Assert.Single(verb.Forms);
        Assert.Equal(2, quiero.Times);
        Assert.Equal(1, quiero.TimesCorrect);
        var noun = Assert.Single(session.Learner.Nouns, static row => row.Lemma == "agua");
        Assert.Equal(2, noun.Times);
        Assert.Equal(1, noun.TimesCorrect); // "aguas" in the understood line; "agua" in the line the core did not understand is used, not correct
    }

    [Fact]
    public void UnknownWordsStayOutOfTheRecordButStayInTheHistory()
    {
        var session = TestGame.NewSession(credit: CreditRules.Default);

        _ = session.Say("xyzzy");

        var entry = Assert.Single(session.Learner.History);
        Assert.Equal("xyzzy", entry.Text);
        Assert.Contains(entry.Words, static w => w.Role == WordRole.Unknown && w.Lemma is null);
        Assert.Empty(session.Learner.Rows);
    }

    [Fact]
    public void AHeldBackOrButtonLineIsInTheHistoryButNotInTheWordRecord()
    {
        var words = new[] { Noun("aguas", "agua", 0, 5) };
        var record = new LearnerRecord();

        record.Append(Entry(1, "aguas", words, held: true));
        record.Append(Entry(2, "aguas", words, source: InputSource.Button));

        Assert.Equal(2, record.History.Count);
        Assert.Empty(record.Rows);
        record.Append(Entry(3, "aguas", words));
        Assert.Equal(1, Assert.Single(record.Nouns).Times);
    }

    [Fact]
    public void AnErrorOverTheTokenMakesThatUseIncorrectAndOnlyThatUse()
    {
        var words = new[] { Noun("aguas", "agua", 9, 5) };
        var record = new LearnerRecord();

        record.Append(Entry(1, "quiero dos aguas", words, errors: [new GuideError("number.agree", "agua", "aguas", new TextSpan(0, 6))]));
        record.Append(Entry(2, "quiero dos aguas", words, errors: [new GuideError("number.agree", "agua", "aguas", new TextSpan(9, 5))]));

        var noun = Assert.Single(record.Nouns);
        Assert.Equal(2, noun.Times);
        Assert.Equal(1, noun.TimesCorrect); // the error over the token of the second line makes that use incorrect; the first line's error is elsewhere
    }

    [Fact]
    public void TheHistoryIsBoundedAndTheSaveKeepsTheNewest500AndTheWholeWordRecord()
    {
        var session = TestGame.NewSession(credit: CreditRules.Default);
        for (var id = 1; id <= 510; id++)
        {
            session.Learner.Append(Entry(session.Learner.NextId, $"agua {id}", [Noun("agua", "agua", 0, 4)]));
        }

        var text = SaveGame.Save(session, TestGame.Route());
        var document = SaveGame.Parse(text);
        Assert.Equal(SaveGame.CurrentVersion, document.Version);
        Assert.NotNull(document.Learner);
        Assert.Equal(500, document.Learner!.History.Count);
        Assert.Equal(11, document.Learner.History[0].Id);
        Assert.Equal(510, document.Learner.History[^1].Id);
        Assert.Equal(511, document.Learner.NextId);
        Assert.Equal(510, Assert.Single(document.Learner.Words, static w => w.Lemma == "agua").Times);

        var loaded = SaveGame.Load(text, TestGame.Data, TestGame.Route, credit: CreditRules.Default, bumpSession: false);
        Assert.Equal(500, loaded.Learner.History.Count);
        Assert.Equal(511, loaded.Learner.NextId);
        Assert.Equal(510, Assert.Single(loaded.Learner.Nouns).Times);
        loaded.Learner.Append(Entry(loaded.Learner.NextId, "agua", [Noun("agua", "agua", 0, 4)]));
        Assert.Equal(511, loaded.Learner.History[^1].Id);
        Assert.Equal(511, Assert.Single(loaded.Learner.Nouns).Times);
    }

    [Fact]
    public void ReplayingTheHistoryGivesTheSameWordRecord()
    {
        var session = TestGame.NewSession(credit: CreditRules.Default);
        session.OpenTalk("shopkeeper");
        foreach (var line in new[] { "quiero dos aguas", "quiero agua, por favor", "quiero dos aguas, por favor" })
        {
            _ = session.Say(line);
        }

        session.EndTalk();
        session.OpenTalk("guide");
        _ = session.Say("¿dónde está el río?");
        _ = session.Say("el árbol es grande");

        var replayed = LearnerWords.Fold(session.Learner.History).Rows;
        Assert.Equal(Show(session.Learner.Rows), Show(replayed));
        Assert.NotEmpty(replayed);

        // Saved and loaded, the record is the saved one; folding the loaded history (the whole of it, since this one is short) gives it too.
        var loaded = SaveGame.Load(SaveGame.Save(session, TestGame.Route()), TestGame.Data, TestGame.Route, credit: CreditRules.Default, bumpSession: false);
        Assert.Equal(Show(session.Learner.Rows), Show(loaded.Learner.Rows));
        Assert.Equal(Show(session.Learner.Rows), Show(LearnerWords.Fold(loaded.Learner.History).Rows));
    }

    [Fact]
    public void TheReadApiListsNounsAndVerbsByLemmaAndTheRecentHistoryNewestFirst()
    {
        var record = new LearnerRecord();
        record.Append(Entry(1, "casa", [Noun("casa", "casa", 0, 4, "f")]));
        record.Append(Entry(2, "agua", [Noun("agua", "agua", 0, 4)]));
        record.Append(Entry(3, "pan", [Noun("pan", "pan", 0, 3, "m")]));
        record.Append(Entry(4, "quiero", [new GuideWord("quiero", new TextSpan(0, 6), "querer", "verb", WordRole.Word, "1", "sg", "pres", "ind", null)]));

        Assert.Equal(["agua", "casa", "pan"], record.Nouns.Select(static r => r.Lemma));
        Assert.Equal(["querer"], record.Verbs.Select(static r => r.Lemma));
        Assert.Equal([4L, 3L, 2L], record.Recent(3).Select(static e => e.Id));
        Assert.Equal(4, record.Recent(99).Count);
        Assert.Throws<ArgumentException>(() => record.Append(Entry(9, "x", [])));
    }

    [Fact]
    public void ACheckAnswerIsALineTooButNotAWordOfTheRecord()
    {
        using var session = TestGame.NewSession(credit: CreditRules.Default);
        session.OpenTalk("shopkeeper");
        Assert.True(session.StartWorker(loadSpeech: false).WaitUntilReady(30000), "The worker did not load.");
        var item = CheckBank.Load(TestGame.Bundle).Single(static i => i.Id == "say.pedir-agua");

        _ = session.SubmitCheck(item, TurnSource.Typed, "Quiero agua, por favor.", null, null);
        var applied = Drain(session, 1).Single();

        Assert.NotNull(applied.Outcome.Grade);
        var entry = Assert.Single(session.Learner.History);
        Assert.Equal(InputPlace.Check, entry.Place);
        Assert.Equal("say.pedir-agua", entry.Unit);
        Assert.Equal(InputSource.Typed, entry.Source);
        Assert.Equal(item.Prompt, entry.Prompt);
        Assert.Empty(session.Learner.Rows); // S19b: the check's "querer" and "agua" are not counted
        Assert.Empty(session.Learner.Nouns);
        Assert.Empty(session.Learner.Verbs);
    }

    [Fact]
    public void ACheckAnswerLeavesNounsAndVerbsAsTheyWereAndStaysInWhatISaid()
    {
        var record = new LearnerRecord();
        record.Append(Entry(1, "quiero dos aguas", [Noun("aguas", "agua", 9, 5), Verb("quiero", "querer", 0, 6)]));
        var before = Show(record.Rows);

        record.Append(CheckEntry(2, "quiero agua", [Noun("agua", "agua", 7, 4), Verb("quiero", "querer", 0, 6)]));

        Assert.Equal(before, Show(record.Rows));
        Assert.Equal(1, Assert.Single(record.Nouns).Times);
        Assert.Equal(1, Assert.Single(record.Verbs).Times);
        var said = record.Recent(1).Single();
        Assert.Equal(2, said.Id);
        Assert.Equal(InputPlace.Check, said.Place);
        Assert.Equal("quiero agua", said.Text);
    }

    [Fact]
    public void AVersionTwoSaveThatCountedTheCheckAnswersLoadsWithoutThemAndKeepsThemInTheHistory()
    {
        var session = TestGame.NewSession(credit: CreditRules.Default);
        session.Learner.Append(Entry(1, "quiero dos aguas", [Noun("aguas", "agua", 9, 5), Verb("quiero", "querer", 0, 6)]));
        session.Learner.Append(CheckEntry(2, "quiero agua", [Noun("agua", "agua", 7, 4), Verb("quiero", "querer", 0, 6)]));
        session.Learner.Append(CheckEntry(3, "agua", [Noun("agua", "agua", 0, 4)]));
        var before = Show(session.Learner.Rows);

        // The save as version 2 wrote it: the rows counted every line, the check answers too.
        var current = SaveGame.Parse(SaveGame.Save(session, TestGame.Route()));
        var old = current with
        {
            Version = 2,
            Learner = current.Learner! with { Words = LearnerWords.Fold(session.Learner.History.Select(static e => e with { Place = InputPlace.Dialogue })).Rows },
        };
        Assert.Equal(3, Assert.Single(old.Learner!.Words, static w => w.Lemma == "agua").Times);

        var loaded = SaveGame.Load(SaveGame.Serialize(old), TestGame.Data, TestGame.Route, credit: CreditRules.Default, bumpSession: false);

        Assert.Equal(before, Show(loaded.Learner.Rows));
        Assert.Equal(1, Assert.Single(loaded.Learner.Nouns).Times);
        Assert.Equal(3, loaded.Learner.History.Count);
        Assert.Equal(InputPlace.Check, loaded.Learner.History[^1].Place);

        // Saved again (version 3) and loaded again, the record is the same: the check answers are not taken out twice.
        var resaved = SaveGame.Save(loaded, TestGame.Route());
        Assert.Equal(SaveGame.CurrentVersion, SaveGame.Parse(resaved).Version);
        var again = SaveGame.Load(resaved, TestGame.Data, TestGame.Route, credit: CreditRules.Default, bumpSession: false);
        Assert.Equal(before, Show(again.Learner.Rows));
    }

    [Fact]
    public void AVersionTwoSaveWhoseHistoryWasTrimmedTakesOutOnlyTheChecksItStillHolds()
    {
        var session = TestGame.NewSession(credit: CreditRules.Default);
        for (var id = 1; id <= 510; id++)
        {
            // Entry 10 is a check answer the bound trims away from the save; entry 11 is one the saved history still holds.
            var entry = Entry(session.Learner.NextId, $"agua {id}", [Noun("agua", "agua", 0, 4)]);
            session.Learner.Append(id is 10 or 11 ? entry with { Place = InputPlace.Check, Unit = "say.pedir-agua", Npc = null } : entry);
        }

        var current = SaveGame.Parse(SaveGame.Save(session, TestGame.Route()));
        var old = current with
        {
            Version = 2,
            Learner = current.Learner! with { Words = LearnerWords.Fold(session.Learner.History.Select(static e => e with { Place = InputPlace.Dialogue })).Rows },
        };
        Assert.Equal(510, Assert.Single(old.Learner!.Words, static w => w.Lemma == "agua").Times);
        Assert.Equal(11, old.Learner.History[0].Id);

        var loaded = SaveGame.Load(SaveGame.Serialize(old), TestGame.Data, TestGame.Route, credit: CreditRules.Default, bumpSession: false);

        // The history holds 500 entries of 510: it can take out entry 11 only. Entry 10 was counted, is not held, and stays counted.
        Assert.Equal(509, Assert.Single(loaded.Learner.Nouns).Times);
        Assert.Equal(511, loaded.Learner.NextId);
    }

    [Fact]
    public void ReplayingAHistoryWithCheckAnswersGivesTheSameRecordAfterASaveAndALoad()
    {
        var session = TestGame.NewSession(credit: CreditRules.Default);
        session.Learner.Append(Entry(1, "quiero dos aguas", [Noun("aguas", "agua", 9, 5), Verb("quiero", "querer", 0, 6)]));
        session.Learner.Append(CheckEntry(2, "agua", [Noun("agua", "agua", 0, 4)]));

        var replayed = LearnerWords.Fold(session.Learner.History).Rows;
        Assert.Equal(Show(session.Learner.Rows), Show(replayed));

        var loaded = SaveGame.Load(SaveGame.Save(session, TestGame.Route()), TestGame.Data, TestGame.Route, credit: CreditRules.Default, bumpSession: false);
        Assert.Equal(Show(replayed), Show(loaded.Learner.Rows));
        Assert.Equal(Show(replayed), Show(LearnerWords.Fold(loaded.Learner.History).Rows));
    }

    [Fact]
    public void AVersionOneSaveLoadsWithAnEmptyHistoryAndTheLessonRecordAsFarAsTheSaveAllows()
    {
        var text = File.ReadAllText(FixturePath("save-v1.json"));
        var document = SaveGame.Parse(text);
        Assert.Equal(1, document.Version);
        Assert.Null(document.Learner);

        var loaded = SaveGame.Load(text, TestGame.Data, TestGame.Route, credit: CreditRules.FullTyped);

        Assert.Empty(loaded.Learner.History);
        Assert.Empty(loaded.Learner.Rows);
        Assert.Equal(1, loaded.Learner.NextId);
        Assert.Equal(2, loaded.PlaySession);
        var lesson = Assert.Single(loaded.Lessons.Record, static r => r.LessonId == "first-words");
        Assert.Equal(LessonStatus.Completed, lesson.Status);
        Assert.Equal(34, lesson.Units.Count); // the lesson as the version 1 save recorded it (before the verb unit of S26)
        Assert.All(lesson.Units, static u => Assert.Equal(StepOutcome.Done, u.Outcome));
        Assert.Null(lesson.CompletedSession); // the version 1 save does not say the session it was completed in
        Assert.Equal(0, lesson.Turns);
        Assert.Equal(0, lesson.HelpUses);
        Assert.Equal(0, lesson.DontKnowUses);

        // Saved again it is the current version with the (empty) learner's record.
        var again = SaveGame.Parse(SaveGame.Save(loaded, TestGame.Route()));
        Assert.Equal(SaveGame.CurrentVersion, again.Version);
        Assert.NotNull(again.Learner);
        Assert.Empty(again.Learner!.History);
    }

    [Fact]
    public void ALessonUnitLeavesItsStateInTheRecordAndAResumedSaveKeepsIt()
    {
        Assert.Equal(LessonStatus.NotStarted, TestGame.NewSessionWithShippedLessons().Lessons.Record.Single(static r => r.LessonId == "first-words").Status);
        var session = AtGuide();

        // One unit (u1): a help use on the first step, then every step of the unit done with typed answers.
        session.Lessons.Help();
        var unit = new List<LessonDriver.Done>();
        while (session.Lessons.Current is { } step && step.Id.StartsWith("u1", StringComparison.Ordinal))
        {
            unit.Add(LessonDriver.DoStep(session));
        }

        var inUnit = session.Lessons.Record.Single(static r => r.LessonId == "first-words");
        Assert.Equal(LessonStatus.InProgress, inUnit.Status);
        Assert.Equal(unit.Count, inUnit.Units.Count);
        Assert.All(inUnit.Units, static u => Assert.Equal(StepOutcome.Done, u.Outcome));
        Assert.Equal(unit.Count(static s => s.Sent is not null), inUnit.Turns);
        Assert.Equal(1, inUnit.HelpUses);
        Assert.Null(inUnit.CompletedSession);

        // The save is taken mid-lesson: the load carries the counts and the lesson goes on where it stopped.
        var text = SaveGame.Save(session, TestGame.Route());
        var resumed = SaveGame.Load(text, TestGame.Data, TestGame.Route, credit: CreditRules.FullTyped);
        Assert.Equal(Show(session.Lessons.Record), Show(resumed.Lessons.Record));
        Stage.TeleportToNpc(resumed, "guide");
        _ = resumed.OpenTalk("guide");
        var rest = LessonDriver.CompleteAll(resumed);

        var whole = resumed.Lessons.Record.Single(static r => r.LessonId == "first-words");
        Assert.Equal(LessonStatus.Completed, whole.Status);
        Assert.Equal(2, whole.CompletedSession);
        Assert.Equal(37, whole.Units.Count);
        Assert.Equal(unit.Count(static s => s.Sent is not null) + rest.Count(static s => s.Sent is not null), whole.Turns);
        Assert.Equal(1, whole.HelpUses);
    }

    [Fact]
    public void AWholeLessonRecordsEveryUnitTurnsAndTheSessionItWasCompletedIn()
    {
        var session = AtGuide();

        var steps = LessonDriver.CompleteAll(session);

        var row = session.Lessons.Record.Single(static r => r.LessonId == "first-words");
        Assert.Equal(LessonStatus.Completed, row.Status);
        Assert.Equal(1, row.CompletedSession);
        Assert.Equal(steps.Count, row.Units.Count);
        Assert.Equal(6, steps.Select(static s => s.Id[..2]).Distinct().Count());
        Assert.Equal(steps.Count(static s => s.Sent is not null), row.Turns);
        Assert.Equal(0, row.HelpUses); // the lesson never needed its help path
        Assert.Equal(1, row.DontKnowUses); // "No sé" is the answer of u5-quiz
        Assert.Equal(2, row.RepeatUses); // "Otra vez, por favor" and "Más despacio, por favor" (u5-again, u5-slow)
        Assert.Equal(row.Units.Count, row.Units.Count(static u => u.Outcome == StepOutcome.Done));
    }

    [Fact]
    public void ANoSeInALessonCountsAsHelpAndAsNoSe()
    {
        var session = AtGuide();
        _ = session.Lessons.Start("first-words");

        Assert.True(session.Lessons.DontKnow());

        var row = session.Lessons.Record.Single(static r => r.LessonId == "first-words");
        Assert.Equal(1, row.DontKnowUses);
        Assert.Equal(1, row.HelpUses);
        Assert.Equal(0, row.Turns); // a button in the lesson is not a line the game took
        Assert.Empty(session.Learner.History);
    }

    [Fact]
    public void TheLessonRecordIsInTheBundlesOrderWithItsTitleAndPhrases()
    {
        var session = TestGame.NewSessionWithShippedLessons();

        Assert.Equal(session.Lessons.Book.Lessons.Select(static l => l.Id), session.Lessons.Record.Select(static r => r.LessonId));
        var row = session.Lessons.Record.Single(static r => r.LessonId == "first-words");
        Assert.Equal(LessonStatus.NotStarted, row.Status);
        Assert.Equal(37, row.StepCount);
        Assert.Empty(row.Units);
        Assert.Equal(session.Lessons.Book.Lessons.Single(static l => l.Id == "first-words").TaughtPhrases, row.Phrases);
    }

    private static VoxelSession AtGuide()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        Stage.TeleportToNpc(session, "guide");
        _ = session.OpenTalk("guide");
        return session;
    }

    // The version 1 save written by the S18 code (tests/Vocaterra.Game.Core.Tests/Fixtures/save-v1.json): a camp scene with the guide's whole lesson done.
    private static string FixturePath(string name)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "Vocaterra.Game.Core.Tests", "Fixtures", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"The fixture {name} was not found above the test output folder.");
    }
}
