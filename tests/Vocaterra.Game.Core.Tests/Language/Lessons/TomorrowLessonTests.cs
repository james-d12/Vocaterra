using System.Text.Json.Nodes;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Book;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Es;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.Language.Lessons;

/// <summary>
/// The lesson "Mañana en el pueblo" of session 9 (village-plan V17, 3.5), an <c>a2</c> lesson and the series' only new tense: the simple future as the second way to say
/// tomorrow, against <c>ir a</c>. The baker Luz at the bakery (mañana compraré pan, and her tendré), the smith Domingo at the smithy (buscaré madera para Luz, a promise;
/// iré al río, kept at once by the walk) and the fisher Mar at the river, who tells the day of tomorrow (Luz hará pan, Domingo trabajará, yo pescaré) and hears Luz's part
/// retold. Every step that teaches the future declares <c>tense: fut</c> (V16): the near future and a present with a time word are credited with the step's note. No order
/// and no sale: a learner who followed sessions 4 to 8 has no coins, and nothing changes hands.
/// </summary>
public sealed class TomorrowLessonTests(ITestOutputHelper output)
{
    private const string LessonId = "manana-en-el-pueblo";

    private static LessonBook Book => TestGame.NewSessionWithShippedLessons().Lessons.Book;

    // Ayer en el pueblo done in session 8 (Mañana en el pueblo comes after it), then session 9 at the bakery: the baker's lesson starts by itself. A learner who followed
    // sessions 4 to 8 has no coins, the farmer's three apples and the two loaves of session 5.
    private static VoxelSession SessionNineAtBakery()
    {
        var session = YesterdayLessonTests.AtStep("(after the last step)"); // no step has this id: Ayer en el pueblo runs to its end
        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf("ayer-en-el-pueblo"));
        session.EndTalk();
        session.Day = 9;
        session.Sitting = 9;
        Stage.TeleportToNpc(session, "baker");
        _ = session.OpenTalkForClass("baker");
        return session;
    }

    // The lesson as a learner who gets everything right does it, up to the step with this id.
    internal static VoxelSession AtStep(string stepId)
    {
        var session = SessionNineAtBakery();
        while (session.Lessons.Current is { } step && step.Id != stepId)
        {
            Walk(session, step);
        }

        return session;
    }

    // One step: a talk the step waits for (the walks to the smith and to the fisher) is opened at that NPC; every other step is the driver's.
    private static LessonDriver.Done? Walk(VoxelSession session, LessonStep step)
    {
        if (step is { Type: LessonStepType.Do, At: { } npc })
        {
            session.EndTalk();
            Stage.TeleportToNpc(session, npc);
            _ = session.OpenTalkForClass(npc);
            return null;
        }

        return LessonDriver.DoStep(session);
    }

    private static LessonEvent Done(VoxelSession session, string stepId) =>
        Assert.Single(session.Events.OfType<LessonEvent>(), e => e.Kind == LessonEventKind.StepDone && e.StepId == stepId);

    // ---- The lesson

    [Fact]
    public void MananaEnElPuebloIsTheBakersA2LessonOfSessionNineAfterAyerEnElPuebloWithTheSmithAndTheFisher()
    {
        var lesson = Book.Find(LessonId)!;

        Assert.Equal(("baker", 9, false, "ayer-en-el-pueblo", LessonLevels.A2), (lesson.Npc, lesson.Session, lesson.AutoStart, lesson.After, lesson.Level));
        Assert.StartsWith("Session 9", lesson.Objective, StringComparison.Ordinal);
        Assert.Contains("bakery", lesson.Objective, StringComparison.Ordinal); // the objective line names where to go: the baker at the bakery
        Assert.Contains("Mañana en el pueblo", lesson.Objective, StringComparison.Ordinal);
        Assert.Equal("Mañana en el pueblo", lesson.ShortTitle);
        Assert.Equal(["baker", "smith", "fisher"], lesson.Steps.Select(lesson.NpcOf).Distinct());

        // Four phrases the learner says and two review steps (session 5's report at the start; session 8's report and session 4's plan at the end, the past and ir a
        // against the new future).
        Assert.Equal(["manana-comprare-pan", "buscare-madera", "ire-al-rio", "luz-hara-pan"], lesson.TaughtPhrases);
        Assert.Equal(
            [["compre-dos-panes", "hola"], ["luz-compro-pescado", "voy-a-comprar-pan"]],
            lesson.Steps.Where(static s => s.Type == LessonStepType.Review).Select(static s => s.Candidates!.Select(static c => c.Phrase).ToArray()));
        Assert.Equal(LessonStepType.Recap, lesson.Steps[^1].Type);
        Assert.InRange(lesson.Steps.Count, 12, 26);
        foreach (var step in lesson.Steps)
        {
            if (step.Line is { } line)
            {
                Assert.Equal(lesson.NpcOf(step), line.Speaker); // the speaker of each line its NPC (V01's rule): Luz's, Domingo's and Mar's own voices
            }

            foreach (var candidate in step.Candidates ?? [])
            {
                Assert.Equal(lesson.NpcOf(step), candidate.Line.Speaker);
            }
        }

        // The heard rows are the lines of its steps.
        var heard = new[] { "que-haras-manana", "tendre-pan", "luz-necesita-madera", "mar-pescara", "el-dia-de-manana", "que-hara-luz" };
        var sheet = TestGame.Data.Phrases;
        foreach (var id in heard)
        {
            var row = sheet.Find(id)!;
            Assert.Equal("hears", row.Says);
            Assert.Contains(lesson.Steps, s => s.Line is { } line && row.AudioLineIds.Contains(line.LineId, StringComparer.Ordinal));
        }

        // An explain step says when each future is used, each with its example; the closing table names the later days.
        var steps = lesson.Steps.ToList();
        var two = steps.Single(static s => s.Id == "m1-two").English;
        Assert.Contains("Voy a comprar pan", two, StringComparison.Ordinal);
        Assert.Contains("Mañana compraré pan", two, StringComparison.Ordinal);
        Assert.Contains("plan", two, StringComparison.Ordinal);
        Assert.Contains("promise", two, StringComparison.Ordinal);
        Assert.Contains("la semana que viene", steps.Single(static s => s.Id == "m4-table").English, StringComparison.Ordinal);

        // The do steps: the walk to the smith, then the walk to the river, where the fisher stands on its bank. "Iré al río" is said right before the lesson sends the
        // learner there, so the promise is kept; no order and no sale (the plan's "Mechanics": the fut step and the book's fourth column only).
        var walks = lesson.Steps.Where(static s => s.Type == LessonStepType.Do).ToList();
        Assert.Equal(
            [("npc.smith", WorldEvents.TalkOpened, "smith"), ("npc.fisher", WorldEvents.TalkOpened, "fisher")],
            walks.Select(static s => (s.PointPlace, s.Event!, s.At)));
        Assert.All(walks, static s => Assert.False(string.IsNullOrWhiteSpace(s.Hud), s.Id));
        var go = steps.FindIndex(static s => s.Phrase == "ire-al-rio");
        Assert.Equal(("npc.fisher", "fisher"), (steps[go + 1].PointPlace, steps[go + 1].At));
        Assert.DoesNotContain(TestGame.Data.Objectives.Objectives, static o => o.Lesson == LessonId);

        // The day of tomorrow is the mirror of session 8's day of yesterday, told by Mar, who speaks of himself in the first person.
        Assert.Equal("¡Hola! Mañana Luz hará pan. Domingo trabajará en la herrería. Y yo pescaré en el río.", steps.Single(static s => s.Id == "m3-day").Line!.Text);
        Assert.Contains("Mar pescó en el río", Book.Find("ayer-en-el-pueblo")!.Steps.Single(static s => s.Id == "a1-day").Line!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ItsStepsDeclareTheSimpleFutureAndItsVerbsAreTheFormsTaughtAndHeard()
    {
        var lesson = Book.Find(LessonId)!;
        var expected = new Dictionary<string, (string? Person, string? Tense, string Form)>
        {
            ["manana-comprare-pan"] = ("1sg", TaughtVerbForm.SimpleFuture, "“compraré”"),
            ["buscare-madera"] = ("1sg", TaughtVerbForm.SimpleFuture, "“buscaré”"),
            ["ire-al-rio"] = ("1sg", TaughtVerbForm.SimpleFuture, "“iré”"),
            ["luz-hara-pan"] = ("3sg", TaughtVerbForm.SimpleFuture, "“hará”"),
        };

        var steps = lesson.Steps.Where(static s => s.Type is LessonStepType.Answer or LessonStepType.Repeat).ToList();
        Assert.Equal(4, steps.Count);
        foreach (var step in steps)
        {
            var (person, tense, form) = expected[step.Phrase!];
            Assert.Equal((person, tense), (step.Person, step.Tense));
            Assert.False(string.IsNullOrWhiteSpace(step.PersonRetry), step.Id);
            Assert.False(string.IsNullOrWhiteSpace(step.TenseRetry), step.Id);
            Assert.StartsWith("That is right too. Here we practise " + form, step.FutureNote, StringComparison.Ordinal); // the note names the form the step practises
            Assert.Null(step.PresentNote); // a fut step has one note
        }

        // The forms it declares for the book, each declared by one lesson only (VerbLessonTests): the simple future taught (compraré, comprarás, comprará; iré; buscaré)
        // and heard (hará, tendré, trabajará, pescará). The lexicon has every one (the Wiktionary import).
        Assert.All(lesson.TaughtVerbs, static v => Assert.Equal(TaughtVerbForm.SimpleFuture, v.Tense));
        var morphology = TestGame.Bundle.SpanishMorphology();
        Assert.Equal(
            ["compraré", "comprarás", "comprará", "iré", "buscaré", "hará", "tendré", "trabajará", "pescará"],
            lesson.TaughtVerbs.Select(v => v.Text(morphology, TestGame.Data.Words.NearFuture)));
        Assert.DoesNotContain(Book.Lessons.Where(static l => l.Id is not LessonId and not "el-mercado").SelectMany(static l => l.TaughtVerbs), static v => v.Tense == TaughtVerbForm.SimpleFuture); // V15 reviews it with venderé
    }

    [Fact]
    public void ItIsAnA2LessonAndACopyAtA1FailsOnItsFutSteps()
    {
        // Its words are A1 but fresco (A2, the baker's fresh bread); it is a2 for its grammar: the simple future, which only an a2 lesson may teach (V16). The validator holds
        // it there, and the same lesson at a1 fails on each fut step and each fut form it declares.
        Assert.Empty(GameDataValidator.Validate(TestGame.Data));
        Assert.Equal(
            [LessonLevels.A1, LessonLevels.A1, LessonLevels.A1, LessonLevels.A1, LessonLevels.A1, LessonLevels.A1, LessonLevels.A1, LessonLevels.A2, LessonLevels.A2, LessonLevels.A2, LessonLevels.A2],
            Book.Lessons.Select(static l => l.Level));

        // The shipped book with this lesson's level changed (its reviews and its after need the lessons before it).
        var files = Book.Lessons.Select(l =>
        {
            var file = JsonNode.Parse(GameData.HostText(TestGame.Bundle, $"game/lessons/{l.Id}.json"))!;
            if (l.Id == LessonId)
            {
                file["level"] = LessonLevels.A1;
            }

            return file.ToJsonString();
        });
        var copy = TestGame.Lessons("{ \"schemaVersion\": 1, \"lessons\": [ " + string.Join(", ", files) + " ] }");
        var problems = GameDataValidator.Validate(TestGame.Data.WithLessons(copy)).Where(static p => p.StartsWith($"game/lessons/{LessonId}.json:", StringComparison.Ordinal)).ToList();

        foreach (var step in new[] { "m1-buy", "m2-wood", "m2-go", "m3-luz" })
        {
            Assert.Contains($"game/lessons/{LessonId}.json: the a1 lesson's step '{step}' expects the tense fut; an a1 lesson teaches pres, pret, ir-a.", problems);
        }

        Assert.Contains($"game/lessons/{LessonId}.json: the a1 lesson declares the verb form comprar 1 sg fut; an a1 lesson teaches pres, pret, ir-a.", problems);
        Assert.Contains(problems, static p => p.Contains("fresco", StringComparison.Ordinal)); // and its A2 word
    }

    [Fact]
    public void EveryStepsPromptTakesItsPhraseAndEveryPhraseAndVariantIsUnderstoodThereWithNoError()
    {
        var lesson = Book.Find(LessonId)!;
        var sheet = TestGame.Data.Phrases;
        foreach (var step in lesson.Steps.Where(static s => s.Phrase is not null))
        {
            var row = sheet.Find(step.Phrase!)!;
            Assert.Equal(row.Prompt, step.Prompt);
            Assert.Contains(row.Intent!, TestGame.Bundle.Prompts[step.Prompt!].AllowedIntentIds);
            foreach (var said in row.Variants.Prepend(row.Spanish))
            {
                var session = AtStep(step.Id);
                var turn = session.Say(said);
                Assert.True(turn.Communicated, said);
                Assert.Equal(row.Intent, turn.Intent);
                Assert.Empty(turn.Errors);
                Assert.NotEqual(step.Id, session.Lessons.Current?.Id); // said at its step, every one ends it
            }
        }
    }

    [Fact]
    public void TheLessonIsNotOfferedBeforeAyerEnElPuebloIsDoneNorBeforeSessionNine()
    {
        var early = YesterdayLessonTests.AtStep("(after the last step)");
        early.EndTalk();
        Assert.Equal(8, early.Day);
        Assert.Equal(LessonId, early.Lessons.OfferFor("baker")?.LessonId); // Ayer en el pueblo done and Luz met: the class is on offer, though the day is only 8 (L01)

        var skipped = TestGame.NewSessionWithShippedLessons();
        skipped.Day = 9; // the day opens nothing: Luz is not met, and Mañana en el pueblo waits for session 8's lesson (after)
        skipped.Sitting = 9;
        Assert.NotEqual(LessonId, skipped.Lessons.OfferFor("baker")?.LessonId);
        Assert.Equal(LessonStatus.NotStarted, skipped.Lessons.StatusOf(LessonId));

        var session = SessionNineAtBakery();
        Assert.Equal(LessonId, session.Lessons.Active!.Id);
        Assert.True(session.Lessons.Engaged);
    }

    [Fact]
    public void TheLessonRunsFromTheBakeryToTheSmithyAndTheRiverWithTypedAnswersAndEveryStepEndsDone()
    {
        var session = SessionNineAtBakery();
        var bag = session.Simulation.Inventory;
        var before = (bag.Count(ItemId.Coin), bag.Count(ItemId.Apple), bag.Count(ItemId.Bread));
        Assert.Equal((0, 3), (before.Item1, before.Item2)); // no coins, the farmer's three apples: the lesson needs neither
        var earlier = session.Events.Count;

        var done = new List<LessonDriver.Done>();
        while (session.Lessons.StatusOf(LessonId) == LessonStatus.InProgress)
        {
            var step = session.Lessons.Current!;
            if (step.Id == "m2-walk")
            {
                Assert.Equal(("npc.smith", "herrero"), (session.Pointer!.Point, session.Pointer.Place));
            }

            if (step.Id == "m3-walk")
            {
                Assert.Equal(("npc.fisher", "pescador"), (session.Pointer!.Point, session.Pointer.Place)); // the river's bank, where the fisher stands
                Assert.Equal("Go to Mar at the river (río) by the bridge", session.Lessons.HudLine);
            }

            if (Walk(session, step) is { } d)
            {
                done.Add(d);
            }
        }

        foreach (var d in done)
        {
            output.WriteLine($"{d.Id,-12} {d.Type,-8} {d.Sent,-32} {d.Turn?.Intent,-16} {d.Outcome}");
        }

        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf(LessonId));
        Assert.Equal(9, session.Lessons.Record.Single(static r => r.LessonId == LessonId).CompletedSession);
        Assert.All(done, static d => Assert.Equal(StepOutcome.Done, d.Outcome));

        // The reviews: session 5's report, asked by Luz, and one of the past and the plan, asked by Mar.
        var reviews = done.Where(static d => d.Type == "review").ToList();
        Assert.Equal(("m0-review", "Compré dos panes", "baker", true), (reviews[0].Id, reviews[0].Sent, reviews[0].Turn!.Npc, reviews[0].Turn!.Communicated));
        Assert.Equal(("m4-review", "fisher", true), (reviews[1].Id, reviews[1].Turn!.Npc, reviews[1].Turn!.Communicated));

        var turns = done.Where(static d => d.Turn is not null && d.Type != "review").ToList();
        Assert.Equal(["Mañana compraré pan", "Buscaré madera para Luz", "Iré al río", "Luz hará pan"], turns.Select(static d => d.Sent));
        Assert.All(turns, static d =>
        {
            Assert.Equal("state_action", d.Turn!.Intent);
            Assert.True(d.Turn.Communicated, d.Id);
            Assert.Empty(d.Turn.Errors);
            Assert.True(d.Turn.LessonHandled, d.Id);
        });
        Assert.Equal(["baker", "smith", "smith", "fisher"], turns.Select(static d => d.Turn!.Npc));

        // Nothing changed hands: no order, no sale. The learner leaves session 9 as they came: no coins, three apples, two loaves, the axe.
        Assert.Equal(before, (bag.Count(ItemId.Coin), bag.Count(ItemId.Apple), bag.Count(ItemId.Bread)));
        Assert.DoesNotContain(session.Events.Skip(earlier).OfType<WorldEvent>(), static e => e.Name is WorldEvents.ItemTaken or WorldEvents.ItemGiven or WorldEvents.OrderDelivered);
    }

    // ---- The simple future at its steps (V16's machinery, used as it is)

    [Theory]
    [InlineData("m1-buy", "Mañana compraré pan")]
    [InlineData("m1-buy", "la semana que viene compraré pan")] // the later markers, at either end (state_action since V17, the core's M57)
    [InlineData("m1-buy", "compraré pan la semana que viene")]
    [InlineData("m1-buy", "pasado mañana compraré pan")]
    [InlineData("m2-wood", "Buscaré madera para Luz")]
    [InlineData("m2-go", "Iré al río")]
    [InlineData("m2-go", "la semana que viene iré al río")]
    [InlineData("m2-go", "pasado mañana iré al río")]
    [InlineData("m2-go", "el próximo día iré al río")]
    [InlineData("m3-luz", "Luz hará pan")]
    public void TheSimpleFutureIsTheAnswerOfEachFutStepAndNeedsNoNote(string stepId, string said)
    {
        var session = AtStep(stepId);

        var turn = session.Say(said);

        Assert.Equal(("state_action", true), (turn.Intent, turn.Communicated));
        Assert.Empty(turn.Errors);
        Assert.NotEqual(stepId, session.Lessons.Current?.Id);
        Assert.Null(Done(session, stepId).Detail); // the step's own form: no note
    }

    [Theory]
    [InlineData("m1-buy", "mañana voy a comprar pan", "near future for a plan, credited")] // the brief's case: the plan made now is right Spanish for tomorrow
    [InlineData("m1-buy", "voy a comprar pan", "near future for a plan, credited")]
    [InlineData("m1-buy", "mañana compro pan", "present for a plan, credited")]
    [InlineData("m2-wood", "voy a buscar madera para Luz", "near future for a plan, credited")]
    [InlineData("m2-go", "voy a ir al río", "near future for a plan, credited")]
    [InlineData("m3-luz", "Luz va a hacer pan", "near future for a plan, credited")]
    public void TheNearFutureOrAPresentWithATimeWordIsCreditedWithTheStepsFutureNote(string stepId, string said, string detail)
    {
        var session = AtStep(stepId);
        var note = session.Lessons.Book.Find(LessonId)!.Steps.Single(s => s.Id == stepId).FutureNote;

        var turn = session.Say(said);

        Assert.True(turn.Communicated, said);
        Assert.DoesNotContain(turn.Errors, static e => e.Tag.StartsWith("conj.", StringComparison.Ordinal));
        Assert.NotEqual(stepId, session.Lessons.Current?.Id); // the step ends: not a retry
        Assert.Equal(detail, Done(session, stepId).Detail);
        Assert.Equal(note, session.Lessons.View!.Hint); // the next step opens with the note naming the form the step practises
        Assert.DoesNotContain(session.Events.OfType<LessonEvent>(), e => e.Kind == LessonEventKind.Retry && e.StepId == stepId);
    }

    [Theory]
    [InlineData("m1-buy", "compré pan", "wrong tense: said pret, expected fut", "“compraré”")] // right Spanish with no time word: the core says nothing of the tense
    [InlineData("m1-buy", "compro pan", "wrong tense: said pres, expected fut", "“compraré”")]
    [InlineData("m2-wood", "busco madera para Luz", "wrong tense: said pres, expected fut", "“buscaré”")]
    [InlineData("m2-go", "fui al río", "wrong tense: said pret, expected fut", "“iré”")]
    [InlineData("m3-luz", "Luz hizo pan", "wrong tense: said pret, expected fut", "“hará”")] // session 8's line, said at session 9's step
    [InlineData("m3-luz", "Luz hace pan", "wrong tense: said pres, expected fut", "“hará”")]
    public void APastOrAPresentWithoutATimeWordIsAMissWithTheTenseRetry(string stepId, string said, string reason, string hint) =>
        AssertMiss(AtStep(stepId), said, stepId, reason, hint);

    [Theory]
    [InlineData("m1-buy", "mañana compré pan", "voy a comprar")] // a future marker with a past verb (M49)
    [InlineData("m1-buy", "la semana que viene compré pan", "voy a comprar")] // a later marker, bound as one since V17 (before it semana was the item)
    [InlineData("m1-buy", "ayer compraré pan", "compré")] // a past marker with a future verb
    [InlineData("m3-luz", "mañana Luz hizo pan", "va a hacer")]
    public void AWrongTenseNextToATimeWordIsTheCoresTenseSlip(string stepId, string said, string expected) => AssertSlip(stepId, said, "conj.tense", expected);

    [Theory]
    [InlineData("m1-buy", "yo comprarás pan", "compraré")] // after a subject pronoun the core checks the person itself (conj.person)
    [InlineData("m2-wood", "yo buscará madera para Luz", "buscaré")]
    [InlineData("m2-go", "yo irá al río", "iré")]
    [InlineData("m3-luz", "ella harás pan", "hará")]
    public void AWrongPersonNextToItsSubjectIsTheCoresPersonSlip(string stepId, string said, string expected) => AssertSlip(stepId, said, "conj.person", expected);

    [Theory]
    [InlineData("m3-luz", "Luz harás pan", "said 2sg, expected 3sg", "“hará”")] // a lone name before the verb may be the listener, so the core never checks it (M59)
    [InlineData("m3-luz", "Luz haré pan", "said 1sg, expected 3sg", "“hará”")]
    [InlineData("m2-go", "irás al río", "said 2sg, expected 1sg", "“iré”")]
    public void AWrongPersonWithoutAPronounIsTheLessonsPersonMissNotACoreError(string stepId, string said, string reason, string hint) =>
        AssertMiss(AtStep(stepId), said, stepId, "wrong person: " + reason, hint);

    private static void AssertMiss(VoxelSession session, string said, string stepId, string reasonStart, string hintContains)
    {
        var attempts = session.Lessons.Attempts;
        var turn = session.Say(said);

        Assert.True(turn.Communicated, said);
        Assert.True(turn.LessonHandled, said);
        Assert.DoesNotContain(turn.Errors, static e => e.Tag.StartsWith("conj.", StringComparison.Ordinal)); // the core says nothing of it: the lesson does
        Assert.Equal(stepId, session.Lessons.Current!.Id); // a miss: the step stays
        Assert.Equal(attempts + 1, session.Lessons.Attempts);
        Assert.Contains(hintContains, session.Lessons.View!.Hint, StringComparison.Ordinal);
        Assert.Contains(session.Events.OfType<LessonEvent>(), e => e.Kind == LessonEventKind.Retry && e.StepId == stepId && e.Detail!.StartsWith(reasonStart, StringComparison.Ordinal));
    }

    private static void AssertSlip(string stepId, string said, string tag, string expected)
    {
        var session = AtStep(stepId);
        var turn = session.Say(said);

        Assert.True(turn.Communicated, said);
        Assert.Equal(expected, Assert.Single(turn.Errors, e => e.Tag == tag).Expected);
        Assert.NotEqual(stepId, session.Lessons.Current?.Id); // a slip: the step goes on
        Assert.Equal("with a slip", Done(session, stepId).Detail);
    }

    [Theory]
    [InlineData("m1-buy", "Mañana compraré pan", "compraré", "comprar", "1")]
    [InlineData("m2-go", "Iré al río", "iré", "ir", "1")]
    [InlineData("m3-luz", "Luz hará pan", "hará", "hacer", "3")]
    public void TheCoreReadsTheLessonsFuturesAsTheirVerbsInTheFuture(string stepId, string said, string form, string lemma, string person)
    {
        var turn = AtStep(stepId).Say(said);

        Assert.Contains(turn.Words!, w => w.Text == form && w.Lemma == lemma && w.Person == person && w.Number == "sg" && w.Tense == TaughtVerbForm.SimpleFuture);
    }

    [Fact]
    public void TendreIsReadAsTenerInTheFuture()
    {
        // The baker's tendré is heard, not said; where a learner says it (the smith's open prompt takes a possession), it is tener, 1 sg, fut.
        var session = TestGame.NewSessionWithShippedLessons();
        Stage.TeleportToNpc(session, "smith");
        _ = session.OpenTalkForClass("smith");
        var turn = session.Say("tendré madera");

        Assert.Equal("state_have", turn.Intent);
        Assert.Contains(turn.Words!, static w => w.Text == "tendré" && w.Lemma == "tener" && w.Person == "1" && w.Tense == TaughtVerbForm.SimpleFuture);
    }

    // ---- The book

    [Fact]
    public void TheBooksFutureColumnShowsTheFormsSaidTaughtAndHeardAfterTheLesson()
    {
        var session = AtStep("(after the last step)");
        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf(LessonId));

        // Said in the lesson: the cell is used, with its count. Taught or heard: the cell shows the form. Not taught: empty.
        var comprar = BookPages.VerbTable(session, "comprar")!;
        Assert.Contains(comprar.Columns, static c => c.Tense == TaughtVerbForm.SimpleFuture);
        Assert.Equal(new ConjugationCell("compraré", CellState.Used, 1), At(comprar, "1", "sg"));
        Assert.Equal(new ConjugationCell("comprarás", CellState.Taught, 0), At(comprar, "2", "sg"));
        Assert.Equal(new ConjugationCell("comprará", CellState.Taught, 0), At(comprar, "3", "sg"));
        Assert.Equal(new ConjugationCell(null, CellState.Locked, 0), At(comprar, "1", "pl"));
        Assert.Equal("future", TestGame.Data.Locale.Ui(comprar.Columns[^1].TitleId));

        Assert.Equal(new ConjugationCell("iré", CellState.Used, 1), At(BookPages.VerbTable(session, "ir")!, "1", "sg"));
        Assert.Equal(new ConjugationCell("buscaré", CellState.Used, 1), At(BookPages.VerbTable(session, "buscar")!, "1", "sg"));
        Assert.Equal(new ConjugationCell("hará", CellState.Used, 1), At(BookPages.VerbTable(session, "hacer")!, "3", "sg"));
        Assert.Equal(new ConjugationCell("tendré", CellState.Taught, 0), At(BookPages.VerbTable(session, "tener")!, "1", "sg"));
        Assert.Equal(new ConjugationCell("trabajará", CellState.Taught, 0), At(BookPages.VerbTable(session, "trabajar")!, "3", "sg"));
        Assert.Equal(new ConjugationCell("pescará", CellState.Taught, 0), At(BookPages.VerbTable(session, "pescar")!, "3", "sg"));
    }

    private static ConjugationCell At(ConjugationTable table, string person, string number)
    {
        var row = table.Rows.Single(r => r.Person == person && r.Number == number);
        var column = table.Columns.ToList().FindIndex(static c => c.Tense == TaughtVerbForm.SimpleFuture);
        Assert.True(column >= 0, $"the future column of {table.Lemma} is not shown");
        return row.Cells[column];
    }

    [Fact]
    public void TheBakerTheSmithAndTheFisherSpeakTheLessonsLinesWithTheirOwnVoices()
    {
        var data = TestGame.Data;
        Assert.Equal(
            ("baker", "smith", "fisher"),
            (SpeechSpeakers.OfPool("guide.lesson.baker.fresh", data.Npcs), SpeechSpeakers.OfPool("guide.lesson.smith.go", data.Npcs), SpeechSpeakers.OfPool("guide.lesson.fisher.day", data.Npcs)));
    }
}
