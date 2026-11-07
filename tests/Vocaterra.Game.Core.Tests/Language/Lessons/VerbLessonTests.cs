using System.Globalization;
using System.Text;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Es;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.Language.Lessons;

/// <summary>
/// The verbs in the lessons (S26, D-game-19): the verb unit of the guide's lesson (yo and tú of querer, tener and necesitar), the lesson "Personas" of session 2 with the villager and the shopkeeper
/// (él, ella, usted, nosotros, ustedes and ellos of six verbs), the unlocking of lessons by play session, the replay of a completed lesson, the save of all of it, and the verb forms each lesson
/// declares that it teaches (the conjugation tables of S28 read them).
/// </summary>
public sealed class VerbLessonTests(ITestOutputHelper output)
{
    private static VoxelSession AtGuide()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        session.Lessons.CountAsCompleted("first-words", 1); // the introduction is done; the verb unit is Luna's first class (L01)
        Stage.TeleportToNpc(session, "guide");
        _ = session.OpenTalkForClass("guide");
        Assert.Equal("first-class", session.Lessons.Active?.Id);
        return session;
    }

    private static VoxelSession SessionTwoAtVillager()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        session.Day = 2;
        session.Sitting = 2;
        Stage.TeleportToNpc(session, "villager");
        _ = session.OpenTalkForClass("villager");
        return session;
    }

    private static LessonBook Book => TestGame.NewSessionWithShippedLessons().Lessons.Book;

    // ---- Session 1: the verb unit

    [Fact]
    public void TheVerbUnitComesAfterPleaseAndThankYouAndBeforeGettingUnstuck()
    {
        var steps = Book.Find("first-class")!.Steps;
        var ids = steps.Select(static s => s.Id).ToList();
        var first = ids.IndexOf("u4v-intro");

        Assert.Equal("u4-welcome", ids[first - 1]); // "De nada", the end of please and thank you
        Assert.Equal(
            [("u4v-intro", LessonStepType.Explain), ("u4v-ask-want", LessonStepType.Model), ("u4v-say-want", LessonStepType.Repeat), ("u4v-ask-have", LessonStepType.Answer), ("u4v-ask-need", LessonStepType.Answer)],
            steps.Skip(first).Take(5).Select(static s => (s.Id, s.Type)));
        Assert.Equal(first + 5, ids.Count); // the class ends with the verb unit: getting unstuck stayed in the introduction (L01)
        Assert.DoesNotContain(steps, static s => s.Id == "u3-table"); // cut to keep the lesson under ten minutes
        Assert.Contains("the ending is -o", steps[first].English, StringComparison.Ordinal); // the English names the pattern once...
        Assert.True(steps.Single(static s => s.Id == "u4v-ask-need").English.Length < steps.Single(static s => s.Id == "u4v-say-want").English.Length); // ...and thins
    }

    [Fact]
    public void TheGuideAsksQueQuieresAndTienesMaderaAndTheLearnerAnswersWithTheYoFormsThroughTheCore()
    {
        var session = AtGuide();
        var done = new List<LessonDriver.Done>();
        while (session.Lessons.Current is { } step && step.Id != "u5-intro")
        {
            done.Add(LessonDriver.DoStep(session));
        }

        var unit = done.Where(static d => d.Id.StartsWith("u4v-", StringComparison.Ordinal)).ToList();
        foreach (var d in unit)
        {
            output.WriteLine($"{d.Id,-14} {d.Type,-8} {d.Sent,-20} {d.Turn?.Intent}");
        }

        Assert.Equal(["Quiero madera", "Tengo una fruta", "Necesito agua"], unit.Where(static d => d.Sent is not null).Select(static d => d.Sent));
        Assert.Equal(["state_want", "state_have", "state_want"], unit.Where(static d => d.Turn is not null).Select(static d => d.Turn!.Intent));
        Assert.All(unit.Where(static d => d.Turn is not null), static d =>
        {
            Assert.True(d.Turn!.Communicated, d.Id);
            Assert.Empty(d.Turn.Errors);
            Assert.Empty(d.Turn.HostEvents); // the guide hands nothing over: it is a lesson, not a request
            Assert.Equal(StepOutcome.Done, d.Outcome);
        });
        Assert.Equal(["lesson-want", "lesson-have", "lesson-need"], unit.Where(static d => d.Turn is not null).Select(static d => d.Turn!.Prompt));
    }

    [Fact]
    public void TheTuFormInsteadOfTheYoFormIsAPersonSlipAndTheStepStillEnds()
    {
        var session = AtGuide();
        while (session.Lessons.Current is { } step && step.Id != "u4v-say-want")
        {
            _ = LessonDriver.DoStep(session);
        }

        var turn = session.Say("yo quieres madera");

        Assert.True(turn.Communicated);
        Assert.Contains(turn.Errors, static e => e.Tag == "conj.person");
        Assert.Equal("u4v-ask-have", session.Lessons.Current!.Id); // a slip is the phrase tracking's business: the lesson goes on
        Assert.Equal(StepOutcome.Done, session.Lessons.OutcomesOf("first-class")[^1].Outcome);
    }

    [Fact]
    public void TheRegularizedFormOfTenerIsAnIrregularSlip()
    {
        var session = AtGuide();
        while (session.Lessons.Current is { } step && step.Id != "u4v-ask-have")
        {
            _ = LessonDriver.DoStep(session);
        }

        var turn = session.Say("yo teno una fruta");

        Assert.Contains(turn.Errors, static e => e.Tag == "conj.irregular");
    }

    // ---- Session 2: Personas

    [Fact]
    public void PersonasIsTheVillagersLessonOfSessionTwoWithTheShopkeeperAsItsSecondNpc()
    {
        var personas = Book.Find("personas")!;

        Assert.Equal(("villager", 2, false), (personas.Npc, personas.Session, personas.AutoStart));
        Assert.Contains("Personas", personas.Objective, StringComparison.Ordinal); // the objective names it
        Assert.Equal(["villager", "shopkeeper"], personas.Steps.Select(personas.NpcOf).Distinct());
        Assert.Equal(["quiero-madera", "ella-quiere-fruta", "necesitamos-agua", "necesito-pan", "tengo-botella", "queremos-pan"], personas.TaughtPhrases); // the first is the reminder of session 1
        var walk = Assert.Single(personas.Steps, static s => s.Type == LessonStepType.Do);
        Assert.Equal(("talk_opened", "shopkeeper", "tienda"), (walk.Event, walk.At, walk.Point)); // the walk from the villager to the stall
        Assert.Equal(LessonStepType.Recap, personas.Steps[^1].Type);
        foreach (var unit in new[] { "p1", "p2", "p3", "p4" })
        {
            Assert.Contains(personas.Steps, s => s.Id.StartsWith(unit + "-", StringComparison.Ordinal));
        }

        // every line is said by the NPC the step is said at; the shopkeeper's steps use usted and ustedes, the villager's do not
        foreach (var step in personas.Steps.Where(static s => s.Line is not null))
        {
            Assert.Equal(personas.NpcOf(step), step.Line!.Speaker);
        }

        var shopLines = string.Join(" ", personas.Steps.Where(s => personas.NpcOf(s) == "shopkeeper" && s.Line is not null).Select(static s => s.Line!.Text));
        Assert.Contains("usted", shopLines, StringComparison.Ordinal);
        Assert.Contains("ustedes", shopLines, StringComparison.Ordinal);
        Assert.DoesNotContain("vosotros", JsonOf(personas), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PersonasRunsFromTheVillagerToTheShopkeeperWithTypedAnswersAndEveryStepEndsDone()
    {
        var session = SessionTwoAtVillager();
        Assert.True(session.Lessons.Engaged);
        Assert.Equal("p1-intro", session.Lessons.Current!.Id);
        var done = new List<LessonDriver.Done>();

        while (session.Lessons.Current is { } step && step.Id != "p2-walk")
        {
            done.Add(LessonDriver.DoStep(session));
        }

        // The walk to the stall: the panel closes, the lesson waits (engaged, with its short line), and only the shopkeeper's panel moves it on.
        Assert.Equal("p2-walk", session.Lessons.Current!.Id);
        Assert.Equal("Walk to the stall and talk to the shopkeeper", session.Lessons.HudLine);
        session.EndTalk();
        Assert.True(session.Lessons.Engaged);
        Stage.TeleportToNpc(session, "guide");
        _ = session.OpenTalkForClass("guide");
        Assert.Equal("p2-walk", session.Lessons.Current!.Id); // another NPC's panel does not end the step
        session.EndTalk();
        Stage.TeleportToNpc(session, "shopkeeper");
        var opening = session.OpenTalkForClass("shopkeeper");
        Assert.Empty(opening.Lines); // the shopkeeper's lesson speaks first
        Assert.Equal("p3-intro", session.Lessons.Current!.Id);
        Assert.Equal("shopkeeper", session.Lessons.CurrentNpc);
        Assert.Equal(StepOutcome.Done, session.Lessons.OutcomesOf("personas")[^1].Outcome);

        while (session.Lessons.StatusOf("personas") == LessonStatus.InProgress)
        {
            done.Add(LessonDriver.DoStep(session));
        }

        foreach (var d in done)
        {
            output.WriteLine($"{d.Id,-14} {d.Type,-8} {d.Sent,-22} {d.Turn?.Intent,-12} {d.Outcome}");
        }

        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf("personas"));
        Assert.Equal(2, session.Lessons.Record.Single(static r => r.LessonId == "personas").CompletedSession);
        Assert.All(done, static d => Assert.Equal(StepOutcome.Done, d.Outcome));
        var turns = done.Where(static d => d.Turn is not null).ToList();
        Assert.Equal(["Quiero madera", "Ella quiere fruta", "Necesitamos agua", "Necesito pan", "Tengo una botella", "Queremos pan"], turns.Select(static d => d.Sent));
        Assert.Equal(["state_want", "state_want", "state_want", "state_want", "state_have", "state_want"], turns.Select(static d => d.Turn!.Intent));
        Assert.All(turns, static d =>
        {
            Assert.True(d.Turn!.Communicated, d.Id);
            Assert.Empty(d.Turn.Errors);
            Assert.Empty(d.Turn.HostEvents); // the stall hands nothing over, and the villager gives nothing
            Assert.True(d.Turn.LessonHandled, d.Id);
        });
        Assert.Equal(["villager", "villager", "villager", "shopkeeper", "shopkeeper", "shopkeeper"], turns.Select(static d => d.Turn!.Npc));
        Assert.Equal("ayer-y-manana", session.Lessons.OfferFor("villager")?.LessonId); // personas is done: the villager's next class of the series is on offer (L01)
    }

    [Fact]
    public void TheStallsAndTheVillagersOwnPhraseCardsStayDownWhilePersonasRunsAndOnlyARepeatStepShowsItsCard()
    {
        var session = SessionTwoAtVillager();
        Assert.Empty(session.Support.Cards); // not the villager's "¿Dónde está el río?"
        while (session.Lessons.Current is { } step && step.Id != "p1-say-she")
        {
            Assert.Empty(session.Support.Cards);
            _ = LessonDriver.DoStep(session);
        }

        Assert.Equal(["ella-quiere-fruta"], session.Support.Cards.Select(static c => c.PhraseId)); // a repeat step has its card up
        _ = LessonDriver.DoStep(session);
        while (session.Lessons.Current is { } step && step.Id != "p2-walk")
        {
            Assert.Empty(session.Support.Cards); // an answer step has none, and the explain, model and listen steps ask for no phrase
            _ = LessonDriver.DoStep(session);
        }

        session.EndTalk();
        Stage.TeleportToNpc(session, "shopkeeper");
        _ = session.OpenTalkForClass("shopkeeper");
        Assert.Empty(session.Support.Cards); // not the stall's "Quiero agua, por favor."
        Assert.Equal("p3-intro", session.Lessons.Current!.Id);
    }

    [Fact]
    public void ThePersonSlipsOfPersonasAreGradedByTheCore()
    {
        var session = SessionTwoAtVillager();
        while (session.Lessons.Current is { } step && step.Id != "p1-say-she")
        {
            _ = LessonDriver.DoStep(session);
        }

        var turn = session.Say("ella quiero fruta");

        Assert.Equal("state_want", turn.Intent);
        Assert.Contains(turn.Errors, static e => e.Tag == "conj.person");
        Assert.Equal("p2-intro", session.Lessons.Current!.Id);
    }

    // ---- Sessions

    [Fact]
    public void AClassIsOpenOnceItsTeacherIsMetWhateverTheDay()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        Stage.TeleportToNpc(session, "villager");

        Assert.Equal(1, session.Day);
        Assert.Null(session.Lessons.OfferFor("villager")); // not met: nothing is open (L01)
        Assert.Null(session.Lessons.OpenClass());
        var opening = session.OpenTalk("villager");
        Assert.NotEmpty(opening.Lines); // the villager's own "Hola." (the river errand of session 1)
        Assert.Null(session.Lessons.Active); // a class never starts by itself
        Assert.Equal(LessonStatus.NotStarted, session.Lessons.StatusOf("personas"));
        Assert.Equal("personas", session.Lessons.OfferFor("villager")?.LessonId); // met: the first class of the series is on offer, on day 1 as on any day
        Assert.Equal("guide.lesson.villager.offer.1", session.Support.TakeCues().Last().LineId);
        Assert.Equal(["¿Una clase hoy? ¿Sí o no?"], session.Lessons.TakeLines());
    }

    [Fact]
    public void TheObjectiveNeverNamesAClassAndTheBeaconDoesNotPointAtItsTeacher()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        session.Day = 2;
        session.Sitting = 2;
        _ = session.OpenTalk("villager");
        session.EndTalk();
        session.BeginPlaySession();

        Assert.Equal("personas", session.Lessons.OfferFor("villager")!.LessonId);
        Assert.DoesNotContain("Personas", session.ObjectiveHud()[0].Text, StringComparison.Ordinal);
        Assert.NotEqual("villager", session.Pointer?.Point);
    }

    [Fact]
    public void ALoadIntoTheNextSessionUnlocksItsLessonAndTheSaveHoldsTheSession()
    {
        var session = AtGuide();
        _ = LessonDriver.CompleteAll(session, "first-class");
        session.EndTalk();
        session.Day = 2; // the sleep at the fire (V25: a load keeps the day, only a sleep moves it)
        session.Sitting = 2;
        session.BeginPlaySession();

        var text = SaveGame.Save(session, TestGame.Route());
        var document = SaveGame.Parse(text);
        var loaded = SaveGame.Load(text, TestGame.Data, TestGame.Route, credit: CreditRules.FullTyped);

        Assert.Equal(SaveGame.CurrentVersion, document.Version);
        Assert.Equal(2, document.Session);
        Assert.Equal(2, loaded.Day);
        Assert.False(loaded.Lessons.IsUnlocked(loaded.Lessons.Book.Find("personas")!)); // the villager is not met yet: the day opens nothing
        _ = loaded.OpenTalk("villager");
        Assert.Equal("personas", loaded.Lessons.OfferFor("villager")!.LessonId); // the first class of the series
        Assert.Equal(LessonStatus.Completed, loaded.Lessons.StatusOf("first-class"));
    }

    // ---- Replay

    [Fact]
    public void ACompletedLessonCanBePractisedAgainAndStartsAtItsNpc()
    {
        var session = AtGuide();
        _ = LessonDriver.CompleteAll(session, "first-class");
        session.EndTalk();
        Assert.Null(session.Lessons.OfferFor("guide"));

        Assert.True(session.Lessons.RequestReplay("first-class"));

        Assert.Equal("first-class", session.Lessons.ReplayPending);
        var title = session.Lessons.Book.Find("first-class")!.ShortTitle;
        Assert.Equal($"Practise again: talk to the guide to start “{title}”", session.Lessons.ReplayHud);
        Assert.Equal($"Practise again: talk to the guide to start “{title}”", session.ObjectiveHud()[0].Text);
        Assert.NotNull(session.Pointer); // the beacon points at Luna
        Assert.Equal("first-class", session.Lessons.OfferFor("guide")!.LessonId);
        Assert.Contains(session.Events.OfType<LessonEvent>(), static e => e.Kind == LessonEventKind.ReplayRequested);

        Stage.TeleportToNpc(session, "guide");
        var opening = session.OpenTalkForClass("guide");

        Assert.Empty(opening.Lines); // the lesson speaks first
        Assert.True(session.Lessons.Engaged);
        Assert.Equal("u4-intro", session.Lessons.Current!.Id); // from its first step
        Assert.Null(session.Lessons.ReplayPending);
        Assert.True(session.Lessons.IsDone("first-class")); // still done while it runs again
        Assert.Null(session.Lessons.ReplayHud);

        var again = LessonDriver.CompleteAll(session, "first-class");

        Assert.Equal(12, again.Count);
        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf("first-class"));
    }

    [Fact]
    public void ALessonNeverCompletedCannotBePractisedAgainNorWhileAnotherIsUnderWay()
    {
        var session = AtGuide();

        Assert.False(session.Lessons.RequestReplay("personas")); // another class is under way
        Assert.False(session.Lessons.RequestReplay("no-such-lesson"));
        Assert.Null(session.Lessons.ReplayPending);
        Assert.True(session.Lessons.RequestReplay("first-class")); // the class under way: asking for it again resumes it at its teacher (L01)
        Assert.Equal("first-class", session.Lessons.ReplayPending);
    }

    [Fact]
    public void PersonasCanBePractisedAgainAtTheVillager()
    {
        var session = SessionTwoAtVillager();
        while (session.Lessons.Current is { } step && step.Id != "p2-walk")
        {
            _ = LessonDriver.DoStep(session);
        }

        session.EndTalk();
        Stage.TeleportToNpc(session, "shopkeeper");
        _ = session.OpenTalkForClass("shopkeeper");
        while (session.Lessons.StatusOf("personas") == LessonStatus.InProgress)
        {
            _ = LessonDriver.DoStep(session);
        }

        session.EndTalk();
        Assert.True(session.Lessons.RequestReplay("personas"));
        Stage.TeleportToNpc(session, "villager");
        _ = session.OpenTalkForClass("villager");

        Assert.Equal("p1-intro", session.Lessons.Current!.Id);
        Assert.Equal("villager", session.Lessons.CurrentNpc);
    }

    [Fact]
    public void AReplayUnderWayAndAReplayAskedForSurviveASaveAndALoad()
    {
        var session = AtGuide();
        _ = LessonDriver.CompleteAll(session, "first-class");
        session.EndTalk();
        Assert.True(session.Lessons.RequestReplay("first-class"));
        var asked = SaveGame.Save(session, TestGame.Route());

        var restored = SaveGame.Load(asked, TestGame.Data, TestGame.Route, credit: CreditRules.FullTyped, bumpSession: false);

        Assert.Equal("first-class", restored.Lessons.ReplayPending);
        Assert.True(restored.Lessons.IsDone("first-class"));
        Assert.Equal(asked, SaveGame.Save(restored, TestGame.Route()));

        // and half way through the replay: the lesson is in progress and still done
        Stage.TeleportToNpc(restored, "guide");
        _ = restored.OpenTalkForClass("guide");
        for (var i = 0; i < 5; i++)
        {
            _ = LessonDriver.DoStep(restored);
        }

        restored.EndTalk();
        var half = SaveGame.Save(restored, TestGame.Route());
        var again = SaveGame.Load(half, TestGame.Data, TestGame.Route, credit: CreditRules.FullTyped, bumpSession: false);

        Assert.Equal(LessonStatus.InProgress, again.Lessons.StatusOf("first-class"));
        Assert.True(again.Lessons.IsDone("first-class"));
        Assert.Null(again.Lessons.ReplayPending);
        Assert.Equal(half, SaveGame.Save(again, TestGame.Route()));
        Assert.Equal(5, again.Lessons.OutcomesOf("first-class").Count);
    }

    [Fact]
    public void ASaveBeforeTheReplayWasKeptReadsACompletedLessonAsDone()
    {
        var session = AtGuide();
        _ = LessonDriver.CompleteAll(session, "first-class");
        session.EndTalk();
        var document = SaveGame.Parse(SaveGame.Save(session, TestGame.Route()));
        var old = document with
        {
            Version = 3,
            Lessons = document.Lessons! with
            {
                Replay = null,
                Lessons = document.Lessons.Lessons.Select(static l => l with { EverCompleted = null }).ToArray(),
            },
        };

        var loaded = SaveGame.Load(SaveGame.Serialize(old), TestGame.Data, TestGame.Route, credit: CreditRules.FullTyped, bumpSession: false);

        Assert.True(loaded.Lessons.IsDone("first-class"));
        Assert.False(loaded.Lessons.IsDone("personas"));
    }

    // ---- The verb forms a lesson declares it teaches

    [Fact]
    public void FirstWordsDeclaresYoAndTuOfQuererTenerAndNecesitar()
    {
        var forms = Book.Find("first-class")!.TaughtVerbs;

        Assert.Equal(
            ["querer 1 sg pres", "querer 2 sg pres", "tener 1 sg pres", "tener 2 sg pres", "necesitar 1 sg pres", "necesitar 2 sg pres"],
            forms.Select(Show));
    }

    [Fact]
    public void PersonasDeclaresTheOtherPersonsOfSixVerbsInThePresent()
    {
        var forms = Book.Find("personas")!.TaughtVerbs;

        // only the forms that are heard, said or named: él, ella, usted (3 sg); nosotros (1 pl); ustedes, ellos (3 pl). The conjugation of ir comes in session 3.
        Assert.Equal(
            ["querer 3 sg", "querer 1 pl", "querer 3 pl", "tener 3 sg", "tener 1 pl", "tener 3 pl", "necesitar 3 sg", "necesitar 1 pl", "ser 3 sg", "ser 1 pl", "estar 3 sg", "ir 1 pl"],
            forms.Select(static f => $"{f.Lemma} {f.Person} {f.Number}"));
        Assert.All(forms, static f => Assert.Equal("pres", f.Tense));

        Assert.DoesNotContain(forms, static f => f is { Person: "2", Number: "pl" }); // no vosotros
    }

    [Fact]
    public void EveryDeclaredFormIsAWordTheLearnerMeetsInItsLesson()
    {
        foreach (var lesson in Book.Lessons)
        {
            var text = Plain(LessonText(lesson));
            foreach (var form in lesson.TaughtVerbs)
            {
                var word = form.Text(TestGame.Bundle.SpanishMorphology(), TestGame.Data.Words.NearFuture, TestGame.Data.Words.Obligation); // voy a comer for the near future (ir-a): ir's present, a and the infinitive
                Assert.True(word is not null, $"{lesson.Id}: the core has no form for {Show(form)}.");
                Assert.True(text.Contains(Plain(word), StringComparison.Ordinal), $"{lesson.Id} declares {Show(form)} ({word}) but never shows it.");
            }
        }
    }

    [Fact]
    public void EveryFormTheLessonsDeclareIsDeclaredOnceAndTheirSessionsAreInOrder()
    {
        var book = Book;

        Assert.All(book.Lessons, static l => Assert.Equal(l.TaughtVerbs.Count, l.TaughtVerbs.Distinct().Count()));
        Assert.Equal(book.Lessons.Sum(static l => l.TaughtVerbs.Count), book.Lessons.SelectMany(static l => l.TaughtVerbs).Distinct().Count()); // each form belongs to one lesson
        Assert.Equal([1, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11], book.Lessons.Select(static l => l.Session));
    }

    [Fact]
    public void ALessonFileMustNameItsObjectiveWhenItUnlocksLaterAndKeepItsFormsWellFormed()
    {
        string Lesson(string extra, string step = "{ \"id\": \"a\", \"type\": \"explain\", \"english\": \"E\" }") =>
            "{ \"schemaVersion\": 1, \"lessons\": [ { \"id\": \"t\", \"npc\": \"guide\", \"level\": \"a1\", \"title\": \"T\", \"english\": \"E\", " + extra + " \"steps\": [ " + step + " ] } ] }";

        Assert.Contains("objective", Assert.Throws<InvalidDataException>(() => TestGame.Lessons(Lesson("\"session\": 2,"))).Message, StringComparison.Ordinal);
        Assert.Equal(2, Assert.Single(TestGame.Lessons(Lesson("\"session\": 2, \"objective\": \"Go.\",")).Lessons).Session);
        Assert.Contains("person", Assert.Throws<InvalidDataException>(() => TestGame.Lessons(Lesson("\"verbs\": [ { \"lemma\": \"ir\", \"person\": \"4\", \"number\": \"sg\", \"tense\": \"pres\" } ],"))).Message, StringComparison.Ordinal);
        Assert.Contains("twice", Assert.Throws<InvalidDataException>(() => TestGame.Lessons(Lesson(
            "\"verbs\": [ { \"lemma\": \"ir\", \"person\": \"1\", \"number\": \"sg\", \"tense\": \"pres\" }, { \"lemma\": \"ir\", \"person\": \"1\", \"number\": \"sg\", \"tense\": \"pres\" } ],"))).Message, StringComparison.Ordinal);
        Assert.Contains("NPC", Assert.Throws<InvalidDataException>(() => TestGame.Lessons(Lesson("", "{ \"id\": \"a\", \"type\": \"explain\", \"english\": \"E\", \"npc\": \"miller\" }"))).Message, StringComparison.Ordinal);
        Assert.Contains("spoken by", Assert.Throws<InvalidDataException>(() => TestGame.Lessons(Lesson("", "{ \"id\": \"a\", \"type\": \"model\", \"line\": \"shop_need.1\", \"translation\": \"T\", \"english\": \"E\" }"))).Message, StringComparison.Ordinal);
        // C06: a do step's NPC is its trigger's npc; the old field "at" is refused, so a lesson cannot wait at an NPC by a name the matcher does not read.
        Assert.Contains("'at'", Assert.Throws<InvalidDataException>(() => TestGame.Lessons(Lesson("", "{ \"id\": \"a\", \"type\": \"do\", \"trigger\": { \"event\": \"talk_opened\", \"at\": \"shopkeeper\" }, \"english\": \"E\" }"))).Message, StringComparison.Ordinal);
    }

    // ---- The person of the answer's verb (S26 part 2)

    private static VoxelSession AtStep(string stepId)
    {
        var session = SessionTwoAtVillager();
        while (session.Lessons.Current is { } step && step.Id != stepId)
        {
            if (step.Id == "p2-walk")
            {
                session.EndTalk();
                Stage.TeleportToNpc(session, "shopkeeper");
                _ = session.OpenTalkForClass("shopkeeper");
                continue;
            }

            _ = LessonDriver.DoStep(session);
        }

        return session;
    }

    private static void AssertMissThenDone(VoxelSession session, string wrong, string right, string stepId, string next, string ending)
    {
        var turn = session.Say(wrong);
        Assert.True(turn.Communicated, wrong);
        Assert.DoesNotContain(turn.Errors, static e => e.Tag.StartsWith("conj.", StringComparison.Ordinal)); // no pronoun, so the core flags no person: only the verb's person is wrong
        Assert.Equal(stepId, session.Lessons.Current!.Id); // a miss: the step stays
        Assert.Equal(1, session.Lessons.Attempts);
        Assert.Contains(ending, session.Lessons.View!.Hint, StringComparison.Ordinal); // the retry names the ending
        Assert.Contains(session.Events.OfType<LessonEvent>(), e => e.Kind == LessonEventKind.Retry && e.StepId == stepId && e.Detail!.StartsWith("wrong person", StringComparison.Ordinal)); // and the log says why

        _ = session.Say(right);
        Assert.Equal(next, session.Lessons.Current!.Id);
        Assert.Equal(StepOutcome.Done, session.Lessons.OutcomesOf(session.Lessons.Active!.Id)[^1].Outcome);
    }

    [Fact]
    public void TuForYoIsAMissAtQueQuieresAndTheRetryNamesTheEnding()
    {
        var session = AtGuide();
        while (session.Lessons.Current is { } step && step.Id != "u4v-say-want")
        {
            _ = LessonDriver.DoStep(session);
        }

        AssertMissThenDone(session, "quieres madera", "quiero madera", "u4v-say-want", "u4v-ask-have", "-o");
    }

    [Fact]
    public void YoForElIsAMissAtQueQuiereLuna()
    {
        var session = AtStep("p1-say-she");

        AssertMissThenDone(session, "quiero fruta", "ella quiere fruta", "p1-say-she", "p2-intro", "-e");
    }

    [Fact]
    public void YoForNosotrosIsAMissAtQueNecesitamos()
    {
        var session = AtStep("p2-ask-we");

        AssertMissThenDone(session, "necesito agua", "necesitamos agua", "p2-ask-we", "p2-they", "-amos");
    }

    [Fact]
    public void YoForNosotrosIsAMissAtTheShopkeepersUstedesQuestionToo()
    {
        var session = AtStep("p4-say-we");

        AssertMissThenDone(session, "quiero pan", "queremos pan", "p4-say-we", "p4-they", "-amos");
    }

    [Fact]
    public void AMissIsStillFailForwardAndAStepWithNoDeclaredPersonJudgesNothingOfIt()
    {
        var session = AtGuide();
        while (session.Lessons.Current is { } step && step.Id != "u4v-say-want")
        {
            _ = LessonDriver.DoStep(session);
        }

        _ = session.Say("quieres madera");
        _ = session.Say("quieres fruta");
        Assert.Equal("u4v-ask-have", session.Lessons.Current!.Id); // the second miss moves on (attempts, D-game-9)
        Assert.Equal(StepOutcome.MovedOn, session.Lessons.OutcomesOf("first-class")[^1].Outcome);

        const string Plain = """
            { "schemaVersion": 1, "lessons": [ { "id": "t", "npc": "guide", "level": "a1", "title": "T", "english": "E", "steps": [
              { "id": "say", "type": "repeat", "phrase": "quiero-madera", "prompt": "lesson-want", "english": "Say it." },
              { "id": "end", "type": "explain", "english": "Done." } ] } ] }
            """;
        var other = TestGame.NewSession(lessons: TestGame.Lessons(Plain));
        Stage.TeleportToNpc(other, "guide");
        _ = other.OpenTalkForClass("guide");
        Assert.True(other.Lessons.Start("t"));

        _ = other.Say("quieres madera"); // no person declared: as before, the intent is all that counts

        Assert.Equal("end", other.Lessons.Current!.Id);
        Assert.Equal(StepOutcome.Done, other.Lessons.OutcomesOf("t")[^1].Outcome);
    }

    [Fact]
    public void EveryVerbStepOfBothLessonsDeclaresItsPerson()
    {
        var expected = new Dictionary<string, string>
        {
            ["quiero-madera"] = "1sg",
            ["tengo-fruta"] = "1sg",
            ["necesito-agua"] = "1sg",
            ["ella-quiere-fruta"] = "3sg",
            ["necesitamos-agua"] = "1pl",
            ["necesito-pan"] = "1sg",
            ["tengo-botella"] = "1sg",
            ["queremos-pan"] = "1pl",
        };

        foreach (var step in Book.Lessons.SelectMany(static l => l.Steps).Where(s => s.Type is LessonStepType.Repeat or LessonStepType.Answer && expected.ContainsKey(s.Phrase!))) // a review step judges no person (X06)
        {
            Assert.Equal(expected[step.Phrase!], step.Person);
            Assert.False(string.IsNullOrWhiteSpace(step.PersonRetry), step.Id);
        }

        Assert.All(Book.Lessons.SelectMany(static l => l.Steps).Where(s => s.Type is LessonStepType.Repeat or LessonStepType.Answer && expected.ContainsKey(s.Phrase!)), s => Assert.NotNull(s.Person));
        Assert.Contains("person", Assert.Throws<InvalidDataException>(() => TestGame.Lessons(
            "{ \"schemaVersion\": 1, \"lessons\": [ { \"id\": \"t\", \"npc\": \"guide\", \"level\": \"a1\", \"title\": \"T\", \"english\": \"E\", \"steps\": [ { \"id\": \"a\", \"type\": \"explain\", \"english\": \"E\", \"person\": \"1sg\" } ] } ] }")).Message, StringComparison.Ordinal);
    }

    // ---- The new content

    [Fact]
    public void TheNewRowsAreMarkedPendingAndTheNewLinesAreInTheSpeechList()
    {
        var sheet = RouteScene.DataOf(TestGame.Bundle).Phrases;
        var added = new[]
        {
            "que-quieres", "quiero-madera", "tienes-fruta", "tengo-fruta", "necesito-agua",
            "que-quiere-luna", "ella-quiere-fruta", "necesitamos-agua", "que-necesita-usted", "necesito-pan", "tengo-botella", "que-quieren-ustedes", "queremos-pan",
        };

        Assert.All(added, id => Assert.NotNull(sheet.Find(id)));
        var speech = TestGame.Speech.ToDictionary(static l => l.LineId, StringComparer.Ordinal);
        Assert.Equal("villager", speech["guide.lesson.villager.luna.1"].Speaker);
        Assert.Equal("shopkeeper", speech["guide.lesson.shop.need.1"].Speaker);
        Assert.Equal("guide", speech["guide.lesson.want.1"].Speaker);
        Assert.Equal("shopkeeper", speech["prompt.lesson-usted"].Speaker);
        Assert.All(PendingSpeech.Ids, id => Assert.True(speech.ContainsKey(id), id));
    }

    [Fact]
    public void ThePromptsOfTheVerbLessonsAllowTheNewStatementsAndNotTheRequestAtTheStall()
    {
        var prompts = TestGame.Bundle.Prompts;

        Assert.Contains("state_want", prompts["lesson-usted"].AllowedIntentIds);
        Assert.DoesNotContain("request_item", prompts["lesson-usted"].AllowedIntentIds); // a request at the stall would hand over water
        Assert.Contains("state_have", prompts["lesson-bottle"].AllowedIntentIds);
        Assert.Contains("state_want", prompts["lesson-want"].AllowedIntentIds);
        Assert.DoesNotContain("request_item", prompts["lesson-want"].AllowedIntentIds); // at Luna and at the villager nothing is handed over either
        Assert.Equal("villager", prompts["lesson-she"].AgentId);
        Assert.Equal("shopkeeper", prompts["lesson-ustedes"].AgentId);
    }

    private static string Show(TaughtVerbForm form) => string.Create(CultureInfo.InvariantCulture, $"{form.Lemma} {form.Person} {form.Number} {form.Tense}");

    private static string JsonOf(LessonDefinition lesson) => LessonText(lesson);

    // Everything a lesson shows or says: the Spanish of its lines and phrases, and its English (which quotes the forms), one string of words between spaces.
    private static string LessonText(LessonDefinition lesson)
    {
        var sheet = RouteScene.DataOf(TestGame.Bundle).Phrases;
        var text = new StringBuilder(" ");
        foreach (var step in lesson.Steps)
        {
            text.Append(step.English).Append(' ').Append(step.Line?.Text).Append(' ').Append(step.Translation).Append(' ').Append(step.Help).Append(' ');
            if (step.Phrase is { } phrase)
            {
                text.Append(sheet.Find(phrase)!.Spanish).Append(' ');
            }
        }

        return text.ToString();
    }

    // Lower case, accents off, punctuation turned into spaces: so a form is found as a whole word.
    private static string Plain(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(" ");
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetter(c) ? char.ToLowerInvariant(c) : ' ');
        }

        return builder.Append(' ').ToString();
    }
}
