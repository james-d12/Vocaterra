using System.Globalization;
using System.Text;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests;

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
        Stage.TeleportToNpc(session, "guide");
        _ = session.OpenTalk("guide");
        return session;
    }

    private static VoxelSession SessionTwoAtVillager()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        session.PlaySession = 2;
        Stage.TeleportToNpc(session, "villager");
        _ = session.OpenTalk("villager");
        return session;
    }

    private static LessonBook Book => TestGame.NewSessionWithShippedLessons().Lessons.Book;

    // ---- Session 1: the verb unit

    [Fact]
    public void TheVerbUnitComesAfterPleaseAndThankYouAndBeforeGettingUnstuck()
    {
        var steps = Book.Find("first-words")!.Steps;
        var ids = steps.Select(static s => s.Id).ToList();
        var first = ids.IndexOf("u4v-intro");

        Assert.Equal("u4-welcome", ids[first - 1]); // "De nada", the end of please and thank you
        Assert.Equal(
            [("u4v-intro", LessonStepType.Explain), ("u4v-ask-want", LessonStepType.Model), ("u4v-say-want", LessonStepType.Repeat), ("u4v-ask-have", LessonStepType.Answer), ("u4v-ask-need", LessonStepType.Answer), ("u4v-i-have", LessonStepType.Listen)],
            steps.Skip(first).Take(6).Select(static s => (s.Id, s.Type)));
        Assert.Equal("u5-intro", ids[first + 6]);
        Assert.Contains("-o is I", steps[first].English, StringComparison.Ordinal); // the English names the pattern once...
        Assert.True(steps.Single(static s => s.Id == "u4v-ask-need").English.Length < steps.Single(static s => s.Id == "u4v-say-want").English.Length); // ...and thins
    }

    [Fact]
    public void TheGuideAsksQueQuieresAndTienesMaderaAndTheLearnerAnswersWithTheYoFormsThroughTheCore()
    {
        var session = AtGuide();
        var done = new List<LessonDriver.Done>();
        while (session.Lessons.Current is { } step && step.Id != "u4v-i-have")
        {
            done.Add(LessonDriver.DoStep(session));
        }

        var unit = done.Where(static d => d.Id.StartsWith("u4v-", StringComparison.Ordinal)).ToList();
        foreach (var d in unit)
        {
            output.WriteLine($"{d.Id,-14} {d.Type,-8} {d.Sent,-20} {d.Turn?.Intent}");
        }

        Assert.Equal(["Quiero madera", "Tengo madera", "Necesito agua"], unit.Where(static d => d.Sent is not null).Select(static d => d.Sent));
        Assert.Equal(["request_item", "state_have", "request_item"], unit.Where(static d => d.Turn is not null).Select(static d => d.Turn!.Intent));
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
        Assert.Equal(StepOutcome.Done, session.Lessons.OutcomesOf("first-words")[^1].Outcome);
    }

    [Fact]
    public void TheRegularizedFormOfTenerIsAnIrregularSlip()
    {
        var session = AtGuide();
        while (session.Lessons.Current is { } step && step.Id != "u4v-ask-have")
        {
            _ = LessonDriver.DoStep(session);
        }

        var turn = session.Say("yo teno madera");

        Assert.Contains(turn.Errors, static e => e.Tag == "conj.irregular");
    }

    // ---- Session 2: Personas

    [Fact]
    public void PersonasIsTheVillagersLessonOfSessionTwoWithTheShopkeeperAsItsSecondNpc()
    {
        var personas = Book.Find("personas")!;

        Assert.Equal(("villager", 2, true), (personas.Npc, personas.Session, personas.AutoStart));
        Assert.Contains("Personas", personas.Objective, StringComparison.Ordinal); // the objective names it
        Assert.Equal(["villager", "shopkeeper"], personas.Steps.Select(personas.NpcOf).Distinct());
        Assert.Equal(["ella-quiere-fruta", "necesitamos-agua", "necesito-pan", "tengo-botella", "queremos-pan"], personas.TaughtPhrases);
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
        _ = session.OpenTalk("guide");
        Assert.Equal("p2-walk", session.Lessons.Current!.Id); // another NPC's panel does not end the step
        session.EndTalk();
        Stage.TeleportToNpc(session, "shopkeeper");
        var opening = session.OpenTalk("shopkeeper");
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
        Assert.Equal(["Ella quiere fruta", "Necesitamos agua", "Necesito pan", "Tengo una botella", "Queremos pan"], turns.Select(static d => d.Sent));
        Assert.Equal(["state_want", "state_want", "state_want", "state_have", "state_want"], turns.Select(static d => d.Turn!.Intent));
        Assert.All(turns, static d =>
        {
            Assert.True(d.Turn!.Communicated, d.Id);
            Assert.Empty(d.Turn.Errors);
            Assert.Empty(d.Turn.HostEvents); // the stall hands nothing over, and the villager gives nothing
            Assert.True(d.Turn.LessonHandled, d.Id);
        });
        Assert.Equal(["villager", "villager", "shopkeeper", "shopkeeper", "shopkeeper"], turns.Select(static d => d.Turn!.Npc));
        Assert.Null(session.Lessons.OfferFor("villager"));
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
    public void ALessonOfASecondSessionIsNotOfferedInTheFirst()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        Stage.TeleportToNpc(session, "villager");

        Assert.Equal(1, session.PlaySession);
        Assert.Null(session.Lessons.OfferFor("villager"));
        Assert.Null(session.Lessons.NewLesson);
        var opening = session.OpenTalk("villager");
        Assert.NotEmpty(opening.Lines); // the villager's own "Hola." (the river errand of session 1)
        Assert.Null(session.Lessons.Active);
        Assert.Equal(LessonStatus.NotStarted, session.Lessons.StatusOf("personas"));
    }

    [Fact]
    public void SessionTwoUnlocksPersonasAndTheObjectiveNamesIt()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        session.PlaySession = 2;

        var offer = session.Lessons.OfferFor("villager");
        Assert.Equal("personas", offer!.LessonId);
        Assert.Equal("personas", session.Lessons.NewLesson!.Id);
        var piece = Assert.Single(session.ObjectiveHud());
        Assert.Equal("Session 2: go to the village and talk to the villager to start the lesson “Personas”", piece.Text);
        Assert.Equal(ObjectivePieceKind.English, piece.Kind);
    }

    [Fact]
    public void WhenSessionTwoOpensTheBeaconPointsAtTheVillager()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        session.PlaySession = 2;

        session.BeginPlaySession();

        var villager = session.Scene.Npc("villager").Feet;
        Assert.Equal(new GuideCell(villager.X, villager.Y, villager.Z), session.Pointer!.Target);
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.BeaconSet && e.Npc == "villager");
    }

    [Fact]
    public void ALessonOfALaterSessionIsNoNewLessonOnceItHasBegun()
    {
        var session = SessionTwoAtVillager();

        Assert.Null(session.Lessons.NewLesson);
        Assert.NotEqual("Session 2: go to the village and talk to the villager to start the lesson “Personas”", session.ObjectiveHud()[0].Text);
    }

    [Fact]
    public void ALoadIntoTheNextSessionUnlocksItsLessonAndTheSaveHoldsTheSession()
    {
        var session = AtGuide();
        _ = LessonDriver.CompleteAll(session);
        session.EndTalk();

        var text = SaveGame.Save(session, TestGame.Route());
        var document = SaveGame.Parse(text);
        var loaded = SaveGame.Load(text, TestGame.Bundle, TestGame.Route, TestGame.Nouns, credit: CreditRules.FullTyped);

        Assert.Equal(SaveGame.CurrentVersion, document.Version);
        Assert.Equal(1, document.Session);
        Assert.Equal(2, loaded.PlaySession);
        Assert.Equal("personas", loaded.Lessons.NewLesson!.Id);
        Assert.NotNull(loaded.Pointer); // the beacon points at the villager
        Assert.Equal(LessonStatus.Completed, loaded.Lessons.StatusOf("first-words"));
    }

    // ---- Replay

    [Fact]
    public void ACompletedLessonCanBePractisedAgainAndStartsAtItsNpc()
    {
        var session = AtGuide();
        _ = LessonDriver.CompleteAll(session);
        session.EndTalk();
        Assert.Null(session.Lessons.OfferFor("guide"));

        Assert.True(session.Lessons.RequestReplay("first-words"));

        Assert.Equal("first-words", session.Lessons.ReplayPending);
        Assert.Equal("Practise again: talk to the guide to start “First words with Luna”", session.Lessons.ReplayHud);
        Assert.Equal("Practise again: talk to the guide to start “First words with Luna”", session.ObjectiveHud()[0].Text);
        Assert.NotNull(session.Pointer); // the beacon points at Luna
        Assert.Equal("first-words", session.Lessons.OfferFor("guide")!.LessonId);
        Assert.Contains(session.Events.OfType<LessonEvent>(), static e => e.Kind == LessonEventKind.ReplayRequested);

        Stage.TeleportToNpc(session, "guide");
        var opening = session.OpenTalk("guide");

        Assert.Empty(opening.Lines); // the lesson speaks first
        Assert.True(session.Lessons.Engaged);
        Assert.Equal("u1-welcome", session.Lessons.Current!.Id); // from its first step
        Assert.Null(session.Lessons.ReplayPending);
        Assert.True(session.Lessons.IsDone("first-words")); // still done while it runs again
        Assert.Null(session.Lessons.ReplayHud);

        var again = LessonDriver.CompleteAll(session);

        Assert.Equal(40, again.Count);
        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf("first-words"));
    }

    [Fact]
    public void ALessonNeverCompletedCannotBePractisedAgainNorWhileAnotherIsUnderWay()
    {
        var session = AtGuide();

        Assert.False(session.Lessons.RequestReplay("first-words")); // under way, not completed
        Assert.False(session.Lessons.RequestReplay("personas")); // never begun
        Assert.False(session.Lessons.RequestReplay("no-such-lesson"));
        Assert.Null(session.Lessons.ReplayPending);
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
        _ = session.OpenTalk("shopkeeper");
        while (session.Lessons.StatusOf("personas") == LessonStatus.InProgress)
        {
            _ = LessonDriver.DoStep(session);
        }

        session.EndTalk();
        Assert.True(session.Lessons.RequestReplay("personas"));
        Stage.TeleportToNpc(session, "villager");
        _ = session.OpenTalk("villager");

        Assert.Equal("p1-intro", session.Lessons.Current!.Id);
        Assert.Equal("villager", session.Lessons.CurrentNpc);
    }

    [Fact]
    public void AReplayUnderWayAndAReplayAskedForSurviveASaveAndALoad()
    {
        var session = AtGuide();
        _ = LessonDriver.CompleteAll(session);
        session.EndTalk();
        Assert.True(session.Lessons.RequestReplay("first-words"));
        var asked = SaveGame.Save(session, TestGame.Route());

        var restored = SaveGame.Load(asked, TestGame.Bundle, TestGame.Route, TestGame.Nouns, credit: CreditRules.FullTyped, bumpSession: false);

        Assert.Equal("first-words", restored.Lessons.ReplayPending);
        Assert.True(restored.Lessons.IsDone("first-words"));
        Assert.Equal(asked, SaveGame.Save(restored, TestGame.Route()));

        // and half way through the replay: the lesson is in progress and still done
        Stage.TeleportToNpc(restored, "guide");
        _ = restored.OpenTalk("guide");
        for (var i = 0; i < 5; i++)
        {
            _ = LessonDriver.DoStep(restored);
        }

        restored.EndTalk();
        var half = SaveGame.Save(restored, TestGame.Route());
        var again = SaveGame.Load(half, TestGame.Bundle, TestGame.Route, TestGame.Nouns, credit: CreditRules.FullTyped, bumpSession: false);

        Assert.Equal(LessonStatus.InProgress, again.Lessons.StatusOf("first-words"));
        Assert.True(again.Lessons.IsDone("first-words"));
        Assert.Null(again.Lessons.ReplayPending);
        Assert.Equal(half, SaveGame.Save(again, TestGame.Route()));
        Assert.Equal(5, again.Lessons.OutcomesOf("first-words").Count);
    }

    [Fact]
    public void ASaveBeforeTheReplayWasKeptReadsACompletedLessonAsDone()
    {
        var session = AtGuide();
        _ = LessonDriver.CompleteAll(session);
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

        var loaded = SaveGame.Load(SaveGame.Serialize(old), TestGame.Bundle, TestGame.Route, TestGame.Nouns, credit: CreditRules.FullTyped, bumpSession: false);

        Assert.True(loaded.Lessons.IsDone("first-words"));
        Assert.False(loaded.Lessons.IsDone("personas"));
    }

    // ---- The verb forms a lesson declares it teaches

    [Fact]
    public void FirstWordsDeclaresYoAndTuOfQuererTenerAndNecesitar()
    {
        var forms = Book.Find("first-words")!.TaughtVerbs;

        Assert.Equal(
            ["querer 1 sg pres", "querer 2 sg pres", "tener 1 sg pres", "tener 2 sg pres", "necesitar 1 sg pres", "necesitar 2 sg pres"],
            forms.Select(Show));
    }

    [Fact]
    public void PersonasDeclaresTheOtherPersonsOfSixVerbsInThePresent()
    {
        var forms = Book.Find("personas")!.TaughtVerbs;

        Assert.Equal(18, forms.Count);
        foreach (var lemma in new[] { "querer", "tener", "necesitar", "ser", "estar", "ir" })
        {
            Assert.Equal(["3 sg pres", "1 pl pres", "3 pl pres"], forms.Where(f => f.Lemma == lemma).Select(static f => $"{f.Person} {f.Number} {f.Tense}")); // él, ella, usted; nosotros; ustedes, ellos
        }

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
                var word = TestGame.Bundle.Morphology.Conjugate(form.Lemma, form.Person, form.Number, form.Tense);
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
        Assert.Empty(book.Find("first-words")!.TaughtVerbs.Intersect(book.Find("personas")!.TaughtVerbs)); // each form belongs to one lesson
        Assert.Equal([1, 2], book.Lessons.Select(static l => l.Session));
    }

    [Fact]
    public void ALessonFileMustNameItsObjectiveWhenItUnlocksLaterAndKeepItsFormsWellFormed()
    {
        string Lesson(string extra, string step = "{ \"id\": \"a\", \"type\": \"explain\", \"english\": \"E\" }") =>
            "{ \"schemaVersion\": 1, \"lessons\": [ { \"id\": \"t\", \"npc\": \"guide\", \"title\": \"T\", \"english\": \"E\", " + extra + " \"steps\": [ " + step + " ] } ] }";

        Assert.Contains("objective", Assert.Throws<InvalidDataException>(() => TestGame.Lessons(Lesson("\"session\": 2,"))).Message, StringComparison.Ordinal);
        Assert.Equal(2, Assert.Single(TestGame.Lessons(Lesson("\"session\": 2, \"objective\": \"Go.\",")).Lessons).Session);
        Assert.Contains("person", Assert.Throws<InvalidDataException>(() => TestGame.Lessons(Lesson("\"verbs\": [ { \"lemma\": \"ir\", \"person\": \"4\", \"number\": \"sg\", \"tense\": \"pres\" } ],"))).Message, StringComparison.Ordinal);
        Assert.Contains("twice", Assert.Throws<InvalidDataException>(() => TestGame.Lessons(Lesson(
            "\"verbs\": [ { \"lemma\": \"ir\", \"person\": \"1\", \"number\": \"sg\", \"tense\": \"pres\" }, { \"lemma\": \"ir\", \"person\": \"1\", \"number\": \"sg\", \"tense\": \"pres\" } ],"))).Message, StringComparison.Ordinal);
        Assert.Contains("NPC", Assert.Throws<InvalidDataException>(() => TestGame.Lessons(Lesson("", "{ \"id\": \"a\", \"type\": \"explain\", \"english\": \"E\", \"npc\": \"baker\" }"))).Message, StringComparison.Ordinal);
        Assert.Contains("spoken by", Assert.Throws<InvalidDataException>(() => TestGame.Lessons(Lesson("", "{ \"id\": \"a\", \"type\": \"model\", \"line\": \"shop_need.1\", \"translation\": \"T\", \"english\": \"E\" }"))).Message, StringComparison.Ordinal);
        Assert.Contains("talk_opened", Assert.Throws<InvalidDataException>(() => TestGame.Lessons(Lesson("", "{ \"id\": \"a\", \"type\": \"do\", \"event\": \"item_picked_up\", \"at\": \"shopkeeper\", \"english\": \"E\" }"))).Message, StringComparison.Ordinal);
    }

    // ---- The new content

    [Fact]
    public void TheNewRowsAreMarkedPendingAndTheNewLinesAreInTheSpeechList()
    {
        var sheet = PhraseSheet.Load(TestGame.Bundle);
        var added = new[]
        {
            "que-quieres", "quiero-madera", "tienes-madera", "tengo-madera", "necesito-agua",
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
        Assert.Contains("request_item", prompts["lesson-want"].AllowedIntentIds);
        Assert.Equal("villager", prompts["lesson-she"].AgentId);
        Assert.Equal("shopkeeper", prompts["lesson-ustedes"].AgentId);
    }

    private static string Show(TaughtVerbForm form) => string.Create(CultureInfo.InvariantCulture, $"{form.Lemma} {form.Person} {form.Number} {form.Tense}");

    private static string JsonOf(LessonDefinition lesson) => LessonText(lesson);

    // Everything a lesson shows or says: the Spanish of its lines and phrases, and its English (which quotes the forms), one string of words between spaces.
    private static string LessonText(LessonDefinition lesson)
    {
        var sheet = PhraseSheet.Load(TestGame.Bundle);
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
