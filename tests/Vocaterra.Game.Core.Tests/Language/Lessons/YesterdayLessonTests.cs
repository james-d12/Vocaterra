using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Es;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.Language.Lessons;

/// <summary>
/// The lesson "Ayer en el pueblo" of session 8 (village-plan V14, 3.5), an <c>a2</c> lesson: the day of the village told by the villager in the square and retold by the
/// learner, to him (Luz hizo pan, Mar pescó en el río, fuimos al río), to the smith Domingo at the smithy (ellos comieron pan, tengo que ir al río) and to the fisher Mar at
/// the river (Luz compró pescado): the preterite in the persons the series still lacked (él and ella, nosotros, ellos) on verbs met before, and <c>tener que</c> with an
/// infinitive (the new intent <c>state_obligation</c>). No order and no sale: a learner who followed sessions 4 to 7 has no coins, and nothing changes hands.
/// </summary>
public sealed class YesterdayLessonTests(ITestOutputHelper output)
{
    private const string LessonId = "ayer-en-el-pueblo";

    private static LessonBook Book => TestGame.NewSessionWithShippedLessons().Lessons.Book;

    // ¿Cómo es? done in session 7 (Ayer en el pueblo comes after it), then session 8 at the villager: his lesson starts by itself. A learner who followed sessions 4 to 7
    // has no coins and the farmer's three apples.
    private static VoxelSession SessionEightAtVillager()
    {
        var session = FarmLessonTests.AtStep("(after the last step)"); // no step has this id: ¿Cómo es? runs to its end
        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf("como-es"));
        session.EndTalk();
        session.Day = 8;
        session.Sitting = 8;
        Stage.TeleportToNpc(session, "villager");
        _ = session.OpenTalkForClass("villager");
        return session;
    }

    // The lesson as a learner who gets everything right does it, up to the step with this id.
    internal static VoxelSession AtStep(string stepId)
    {
        var session = SessionEightAtVillager();
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

    // ---- The lesson

    [Fact]
    public void AyerEnElPuebloIsTheVillagersA2LessonOfSessionEightAfterComoEsWithTheSmithAndTheFisher()
    {
        var lesson = Book.Find(LessonId)!;

        Assert.Equal(("villager", 8, false, "como-es", LessonLevels.A2), (lesson.Npc, lesson.Session, lesson.AutoStart, lesson.After, lesson.Level));
        Assert.StartsWith("Session 8", lesson.Objective, StringComparison.Ordinal);
        Assert.Contains("square", lesson.Objective, StringComparison.Ordinal); // the objective line names where to go: the villager in the square
        Assert.Contains("Ayer en el pueblo", lesson.Objective, StringComparison.Ordinal);
        Assert.Equal("Ayer en el pueblo", lesson.ShortTitle);
        Assert.Equal(["villager", "smith", "fisher"], lesson.Steps.Select(lesson.NpcOf).Distinct());

        // Six phrases the learner says and two review steps (session 7's report at the start, session 4's plan at the end): eight said, the ceiling of a lesson.
        Assert.Equal(["luz-hizo-pan", "mar-pesco-en-el-rio", "fuimos-al-rio", "ellos-comieron-pan", "tengo-que-ir", "luz-compro-pescado"], lesson.TaughtPhrases);
        Assert.Equal(
            [["fui-al-rio", "hola"], ["voy-a-comprar-pan"]],
            lesson.Steps.Where(static s => s.Type == LessonStepType.Review).Select(static s => s.Candidates!.Select(static c => c.Phrase).ToArray()));
        Assert.Equal(LessonStepType.Recap, lesson.Steps[^1].Type);
        Assert.InRange(lesson.Steps.Count, 15, 26);
        foreach (var step in lesson.Steps)
        {
            if (step.Line is { } line)
            {
                Assert.Equal(lesson.NpcOf(step), line.Speaker); // the speaker of each line its NPC (V01's rule): the villager's, Domingo's and Mar's own voices
            }

            foreach (var candidate in step.Candidates ?? [])
            {
                Assert.Equal(lesson.NpcOf(step), candidate.Line.Speaker);
            }
        }

        // The heard rows are the lines of its steps.
        var heard = new[]
        {
            "el-dia-de-ayer", "que-hizo-luz", "que-hizo-mar", "comieron-en-la-plaza", "fuimos-a-la-tienda", "adonde-fueron", "que-comieron", "adonde-tienes-que-ir",
            "le-vendi-pescado", "que-compro-luz",
        };
        var sheet = TestGame.Data.Phrases;
        foreach (var id in heard)
        {
            var row = sheet.Find(id)!;
            Assert.Equal("hears", row.Says);
            Assert.Contains(lesson.Steps, s => s.Line is { } line && row.AudioLineIds.Contains(line.LineId, StringComparer.Ordinal));
        }

        // The do steps: the walk to the smith, then the walk to the river, where the fisher stands on its bank. "Tengo que ir al río" is said right before the lesson sends the
        // learner there, so it is true; no order and no sale (the plan's "Mechanics: none").
        var walks = lesson.Steps.Where(static s => s.Type == LessonStepType.Do).ToList();
        Assert.Equal(
            [("npc.smith", WorldEvents.TalkOpened, "smith"), ("npc.fisher", WorldEvents.TalkOpened, "fisher")],
            walks.Select(static s => (s.PointPlace, s.Event!, s.At)));
        Assert.All(walks, static s => Assert.False(string.IsNullOrWhiteSpace(s.Hud), s.Id));
        var steps = lesson.Steps.ToList();
        var must = steps.FindIndex(static s => s.Phrase == "tengo-que-ir");
        Assert.Equal(("npc.fisher", "fisher"), (steps[must + 1].PointPlace, steps[must + 1].At));
        Assert.Contains("río", steps[must + 1].Hud, StringComparison.Ordinal);
        Assert.DoesNotContain(TestGame.Data.Objectives.Objectives, static o => o.Lesson == LessonId);

        // "Fuimos al río" is true of yesterday: session 7's walk for the farmer's water ended on the fisher's bank, where the day says Mar fished.
        Assert.Contains(Book.Find("como-es")!.Steps, static s => s is { Type: LessonStepType.Do, PointPlace: "npc.fisher" });
        Assert.Contains("Mar pescó en el río", steps.Single(static s => s.Id == "a1-day").Line!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ItsStepsDeclareThePersonAndTheTenseAndItsVerbsAreTheNewPersonsOfThePast()
    {
        var lesson = Book.Find(LessonId)!;
        var expected = new Dictionary<string, (string? Person, string? Tense)>
        {
            ["luz-hizo-pan"] = ("3sg", "pret"),
            ["mar-pesco-en-el-rio"] = ("3sg", "pret"),
            ["fuimos-al-rio"] = ("1pl", "pret"),
            ["ellos-comieron-pan"] = ("3pl", "pret"),
            ["tengo-que-ir"] = ("1sg", "pres"),
            ["luz-compro-pescado"] = ("3sg", "pret"),
        };

        var steps = lesson.Steps.Where(static s => s.Type is LessonStepType.Answer or LessonStepType.Repeat).ToList();
        Assert.Equal(6, steps.Count);
        foreach (var step in steps)
        {
            Assert.Equal(expected[step.Phrase!], (step.Person, step.Tense));
            Assert.False(string.IsNullOrWhiteSpace(step.PersonRetry), step.Id);
            Assert.False(string.IsNullOrWhiteSpace(step.TenseRetry), step.Id);
            Assert.Null(step.PresentNote);
        }

        // The forms it declares for the book, each declared by one lesson only (VerbLessonTests): the new persons of the past (pescó, compró, fue; fuimos; fueron, comieron)
        // and the fisher's vendí. hizo and trabajó are Gente del pueblo's, and tengo (of tengo que) the guide's lesson's: the forms stay their first lesson's.
        Assert.All(lesson.TaughtVerbs, static v => Assert.Contains(v.Tense, LessonLevels.TensesAt(LessonLevels.A2)));
        var morphology = TestGame.Bundle.SpanishMorphology();
        Assert.Equal(["pescó", "compró", "fue", "fuimos", "fueron", "comieron", "vendí", "tengo que ir"], lesson.TaughtVerbs.Select(v => v.Text(morphology, TestGame.Data.Words.NearFuture, TestGame.Data.Words.Obligation)));
        var earlier = Book.Lessons.Where(static l => l.Id != LessonId).SelectMany(static l => l.TaughtVerbs).ToList();
        Assert.Contains(new TaughtVerbForm("hacer", "3", "sg", "pret"), earlier);
        Assert.Contains(new TaughtVerbForm("trabajar", "3", "sg", "pret"), earlier);
        Assert.Contains(new TaughtVerbForm("tener", "1", "sg", "pres"), earlier);
    }

    [Fact]
    public void ItIsAnA2LessonAndTheValidatorHoldsItThere()
    {
        // Every word it says is in the A1 list (pescar among the world words, the fisher's own verb); it is a2 as the plan's sessions 7 to 10 are, for its grammar: the past in
        // every person and tener que. The validator holds it to A2, and the earlier six stay a1.
        Assert.Empty(GameDataValidator.Validate(TestGame.Data));
        Assert.Equal(
            [LessonLevels.A1, LessonLevels.A1, LessonLevels.A1, LessonLevels.A1, LessonLevels.A1, LessonLevels.A1, LessonLevels.A1, LessonLevels.A2, LessonLevels.A2, LessonLevels.A2, LessonLevels.A2],
            Book.Lessons.Select(static l => l.Level));
        Assert.Contains("pescar", TestGame.Bundle.Cefr["a1"]);
    }

    [Fact]
    public void TheLessonIsNotOfferedBeforeComoEsIsDoneNorBeforeSessionEight()
    {
        var early = FarmLessonTests.AtStep("(after the last step)");
        early.EndTalk();
        Assert.Equal(7, early.Day);
        Assert.NotEqual(LessonId, early.Lessons.OfferFor("villager")?.LessonId); // ¿Cómo es? done, but it is still session 7

        var skipped = TestGame.NewSessionWithShippedLessons();
        skipped.Day = 8; // session 8 reached without session 7's lesson: Ayer en el pueblo waits for it (after)
        skipped.Sitting = 8;
        Assert.NotEqual(LessonId, skipped.Lessons.OfferFor("villager")?.LessonId);
        Assert.Equal(LessonStatus.NotStarted, skipped.Lessons.StatusOf(LessonId));

        var session = SessionEightAtVillager();
        Assert.Equal(LessonId, session.Lessons.Active!.Id);
        Assert.True(session.Lessons.Engaged);
    }

    [Fact]
    public void TheLessonRunsFromTheSquareToTheSmithyAndTheRiverWithTypedAnswersAndEveryStepEndsDone()
    {
        var session = SessionEightAtVillager();
        var bag = session.Simulation.Inventory;
        var before = (bag.Count(ItemId.Coin), bag.Count(ItemId.Apple), bag.Count(ItemId.Bread));
        Assert.Equal((0, 3), (before.Item1, before.Item2)); // no coins, the farmer's three apples: the lesson needs neither
        var earlier = session.Events.Count;

        var done = new List<LessonDriver.Done>();
        while (session.Lessons.StatusOf(LessonId) == LessonStatus.InProgress)
        {
            var step = session.Lessons.Current!;
            if (step.Id == "a3-walk")
            {
                Assert.Equal(("npc.smith", "herrero"), (session.Pointer!.Point, session.Pointer.Place));
            }

            if (step.Id == "a4-walk")
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
        Assert.Equal(8, session.Lessons.Record.Single(static r => r.LessonId == LessonId).CompletedSession);
        Assert.All(done, static d => Assert.Equal(StepOutcome.Done, d.Outcome));

        // The reviews: session 7's report, asked by the villager, and session 4's plan, asked by Mar.
        var reviews = done.Where(static d => d.Type == "review").ToList();
        Assert.Equal(("a0-review", "Sí, fui al río", "villager", true), (reviews[0].Id, reviews[0].Sent, reviews[0].Turn!.Npc, reviews[0].Turn!.Communicated));
        Assert.Equal(("a5-review", "Voy a comprar pan", "fisher", true), (reviews[1].Id, reviews[1].Sent, reviews[1].Turn!.Npc, reviews[1].Turn!.Communicated));

        var turns = done.Where(static d => d.Turn is not null && d.Type != "review").ToList();
        Assert.Equal(["Luz hizo pan", "Mar pescó en el río", "Fuimos al río", "Ellos comieron pan", "Tengo que ir al río", "Luz compró pescado"], turns.Select(static d => d.Sent));
        Assert.Equal(["state_action", "state_action", "state_action", "state_action", "state_obligation", "state_action"], turns.Select(static d => d.Turn!.Intent));
        Assert.All(turns, static d =>
        {
            Assert.True(d.Turn!.Communicated, d.Id);
            Assert.Empty(d.Turn.Errors);
            Assert.True(d.Turn.LessonHandled, d.Id);
        });
        Assert.Equal(["villager", "villager", "villager", "smith", "smith", "fisher"], turns.Select(static d => d.Turn!.Npc));

        // Nothing changed hands: no order, no sale.
        Assert.Equal(before, (bag.Count(ItemId.Coin), bag.Count(ItemId.Apple), bag.Count(ItemId.Bread)));
        Assert.DoesNotContain(session.Events.Skip(earlier).OfType<WorldEvent>(), static e => e.Name is WorldEvents.ItemTaken or WorldEvents.ItemGiven or WorldEvents.OrderDelivered);
    }

    // ---- The tense and person checks

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
        Assert.Contains(session.Events.OfType<LessonEvent>(), e => e.Kind == LessonEventKind.StepDone && e.StepId == stepId && e.Detail == "with a slip");
    }

    [Theory]
    [InlineData("a1-luz", "Luz hace pan", "“hizo”")] // right Spanish with no time word: the core says nothing of the tense, the step that expects the past does
    [InlineData("a1-mar", "Mar pesca en el río", "“pescó”")]
    [InlineData("a2-went", "vamos al río", "“fuimos”")]
    [InlineData("a3-ate", "ellos comen pan", "“comieron”")]
    [InlineData("a4-bought", "Luz compra pescado", "“compró”")]
    public void ThePresentAtAPastStepIsAMissWithATenseRetry(string stepId, string said, string hint)
    {
        var session = AtStep(stepId);
        AssertMiss(session, said, stepId, "wrong tense", hint);
        _ = session.Say(LessonDriver.LineFor(session, session.Lessons.Current!));
        Assert.Equal(StepOutcome.Done, session.Lessons.OutcomesOf(LessonId).Single(o => o.StepId == stepId).Outcome);
    }

    [Theory]
    [InlineData("a1-luz", "ayer Luz hace pan", "hizo")] // with ayer the core flags the tense itself (conj.tense, M49): a slip, and the step goes on
    [InlineData("a1-mar", "ayer Mar pesca en el río", "pescó")]
    [InlineData("a2-went", "ayer vamos al río", "fuimos")]
    [InlineData("a4-bought", "ayer Luz compra pescado", "compró")]
    public void APresentWithAyerIsTheCoresTenseSlip(string stepId, string said, string expected) => AssertSlip(stepId, said, "conj.tense", expected);

    [Theory]
    [InlineData("a1-luz", "Luz hice pan", "said 1sg, expected 3sg", "“hizo”")] // a lone name before the verb may be the listener (Luz, hice pan), so the core never checks it (M59)
    [InlineData("a1-mar", "Mar pescaste en el río", "said 2sg, expected 3sg", "“pescó”")]
    [InlineData("a2-went", "fui al río", "said 1sg, expected 1pl", "“fuimos”")]
    [InlineData("a3-ate", "comió pan", "said 3sg, expected 3pl", "“comieron”")]
    [InlineData("a3-must", "tienes que ir al río", "said 2sg, expected 1sg", "“tengo que”")]
    [InlineData("a4-bought", "Luz compré pescado", "said 1sg, expected 3sg", "“compró”")]
    public void AWrongPersonWithoutAPronounIsTheLessonsPersonMissNotACoreError(string stepId, string said, string reason, string hint)
    {
        var session = AtStep(stepId);
        AssertMiss(session, said, stepId, "wrong person: " + reason, hint);
    }

    [Theory]
    [InlineData("a1-luz", "ella hice pan", "hizo")] // after a subject pronoun the core checks the person itself (conj.person): a slip
    [InlineData("a2-went", "nosotros fue al río", "fuimos")] // and conj.irregular stays silent on fuimos, which is also ser's
    [InlineData("a3-ate", "ellos comió pan", "comieron")]
    [InlineData("a3-ate", "Luz y Mar comió pan", "comieron")] // two names joined by y are a plural subject to the core (M59)
    [InlineData("a3-must", "yo tiene que ir al río", "tengo")]
    public void AWrongPersonNextToItsSubjectIsTheCoresPersonSlip(string stepId, string said, string expected) => AssertSlip(stepId, said, "conj.person", expected);

    [Fact]
    public void FuimosIsReadAsIrWithNoIrregularFormFlagged()
    {
        var session = AtStep("a2-went");
        var turn = session.Say("Fuimos al río");

        Assert.Equal(("state_action", true), (turn.Intent, turn.Communicated));
        Assert.Empty(turn.Errors); // no conj.irregular on fuimos
        Assert.Contains(turn.Words!, static w => w.Text == "fuimos" && w.Lemma == "ir" && w.Person == "1" && w.Tense == "pret");
    }

    // ---- tener que: state_obligation against state_have

    [Fact]
    public void TengoQueIrIsAnObligationAndTengoMaderaIsStillAPossession()
    {
        var session = AtStep("a3-must");
        var must = session.Say("Tengo que ir al río");
        Assert.Equal(("state_obligation", true, "río"), (must.Intent, must.Communicated, must.Slots["place"]));
        Assert.Empty(must.Errors);

        // At the smith's open prompt, which takes state_have, tener with a noun is still a possession (his offer), and the core's catalog gives an obligation to
        // nothing else: the lint's confusion check of state_obligation against state_have is clean (the host's tests evaluate both on one prompt).
        var free = TestGame.NewSessionWithShippedLessons();
        Stage.TeleportToNpc(free, "smith");
        _ = free.OpenTalkForClass("smith");
        var have = free.Say("Tengo madera");
        Assert.Equal(("state_have", "madera"), (have.Intent, have.Slots["item"]));
        Assert.Empty(have.Errors);
    }

    [Fact]
    public void TheVillagerTheSmithAndTheFisherSpeakTheLessonsLinesWithTheirOwnVoices()
    {
        var data = TestGame.Data;
        Assert.Equal(
            ("villager", "smith", "fisher"),
            (SpeechSpeakers.OfPool("guide.lesson.villager.day", data.Npcs), SpeechSpeakers.OfPool("guide.lesson.smith.must", data.Npcs), SpeechSpeakers.OfPool("guide.lesson.fisher.sold", data.Npcs)));
    }
}
