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
/// The lesson "¿Cómo es?" of session 7 (village-plan V13, 3.5), the first of phase B and the first <c>a2</c> lesson: the farmer León at the field, what things are like
/// (la manzana es roja: the adjective after the noun, agreeing with it; es grande at the stall), what the learner likes (me gusta el pan, me gustan las manzanas) and liked
/// (me gustó el pan de Luz, gustar in the past), his apples for sale (one coin each), and his order water-for-plants told in the three times (voy a buscar agua, the walk
/// to the river, fui al río). A learner who followed sessions 4 to 6 has no coins left, so the apples come from the order; a learner who carries the water already (filled
/// in session 1) says the plan before the farmer takes it.
/// </summary>
public sealed class FarmLessonTests(ITestOutputHelper output)
{
    private const string LessonId = "como-es";

    private static LessonBook Book => TestGame.NewSessionWithShippedLessons().Lessons.Book;

    // Gente del pueblo done in session 6 (¿Cómo es? comes after it), then session 7 at the farmer: his lesson starts by itself. A learner who followed sessions 4 to 6 has no
    // coins (two from fruit-for-mar and four from oven-wood; four went on two loaves and two on the axe). The run of the earlier lessons in these tests never fills a bottle,
    // so the water a learner carries from session 1 is granted when a test wants it.
    private static VoxelSession SessionSevenAtFarmer(int coins = 0, int water = 0)
    {
        var session = PeopleLessonTests.AtStep("(after the last step)"); // no step has this id: Gente del pueblo runs to its end
        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf("gente-del-pueblo"));
        session.EndTalk();
        session.Day = 7;
        session.Sitting = 7;
        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.Coin)); // the two coins went on the axe
        if (coins > 0)
        {
            Assert.Equal(0, session.Simulation.GrantItems(ItemId.Coin, coins));
        }

        if (water > 0)
        {
            Assert.Equal(0, session.Simulation.GrantItems(ItemId.WaterBottle, water));
        }

        Stage.TeleportToNpc(session, "farmer");
        _ = session.OpenTalkForClass("farmer");
        return session;
    }

    // The lesson as a learner who gets everything right does it, up to the step with this id.
    internal static VoxelSession AtStep(string stepId, int water = 0)
    {
        var session = SessionSevenAtFarmer(water: water);
        while (session.Lessons.Current is { } step && step.Id != stepId)
        {
            Walk(session, step);
        }

        return session;
    }

    // One step: at the river the learner fills the bottle (granted here when the bag holds no water), and the arrival ends the step; a talk the step waits for (a walk, or
    // the delivery, which happens as the talk opens) is opened at that NPC; every other step is the driver's.
    private static LessonDriver.Done? Walk(VoxelSession session, LessonStep step)
    {
        if (step.Id == "c5-river")
        {
            session.EndTalk();
            if (session.Simulation.Inventory.Count(ItemId.WaterBottle) == 0)
            {
                Assert.Equal(0, session.Simulation.GrantItems(ItemId.WaterBottle, 1));
            }

            return LessonDriver.DoStep(session);
        }

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
    public void ComoEsIsTheFarmersA2LessonOfSessionSevenAfterGenteDelPuebloWithTheShopkeeper()
    {
        var lesson = Book.Find(LessonId)!;

        Assert.Equal(("farmer", 7, false, "gente-del-pueblo", LessonLevels.A2), (lesson.Npc, lesson.Session, lesson.AutoStart, lesson.After, lesson.Level));
        Assert.StartsWith("Session 7", lesson.Objective, StringComparison.Ordinal);
        Assert.Contains("field", lesson.Objective, StringComparison.Ordinal); // the objective line names where to go: the farmer at the field
        Assert.Contains("León", lesson.Objective, StringComparison.Ordinal);
        Assert.Contains("¿Cómo es?", lesson.Objective, StringComparison.Ordinal);
        Assert.Equal("¿Cómo es?", lesson.ShortTitle);
        Assert.Contains("field", Book.Find("gente-del-pueblo")!.Steps[^1].English, StringComparison.Ordinal); // and Gente del pueblo's recap names the field as what comes next
        Assert.Equal(["farmer", "shopkeeper"], lesson.Steps.Select(lesson.NpcOf).Distinct());

        // Eight phrases the learner says, the ceiling of a lesson, and two review steps: session 5's report or the greeting at the start, a thanks or a goodbye at the end.
        Assert.Equal(
            ["la-manzana-es-roja", "me-gusta-el-pan", "me-gustan-las-manzanas", "quiero-tres-manzanas-rojas", "voy-a-buscar-agua", "es-grande", "fui-al-rio", "me-gusto-el-pan"],
            lesson.TaughtPhrases);
        Assert.Equal(
            [["compre-dos-panes", "hola"], ["gracias", "adios"]],
            lesson.Steps.Where(static s => s.Type == LessonStepType.Review).Select(static s => s.Candidates!.Select(static c => c.Phrase).ToArray()));
        Assert.Equal(LessonStepType.Recap, lesson.Steps[^1].Type);
        Assert.InRange(lesson.Steps.Count, 15, 26);
        foreach (var step in lesson.Steps)
        {
            if (step.Line is { } line)
            {
                Assert.Equal(lesson.NpcOf(step), line.Speaker); // the speaker of each line its NPC (V01's rule): León's and the shopkeeper's own voices
            }

            foreach (var candidate in step.Candidates ?? [])
            {
                Assert.Equal(lesson.NpcOf(step), candidate.Line.Speaker);
            }
        }

        // The heard rows are the lines of its steps.
        var heard = new[]
        {
            "mis-manzanas-son-rojas", "como-es-la-manzana", "te-gusta-el-pan", "mis-plantas-necesitan-agua", "el-pan-es-de-luz", "toma-tres-manzanas", "ayer-me-gusto", "te-gusto-el-pan",
        };
        var sheet = TestGame.Data.Phrases;
        foreach (var id in heard)
        {
            var row = sheet.Find(id)!;
            Assert.Equal("hears", row.Says);
            Assert.Contains(lesson.Steps, s => s.Line is { } line && row.AudioLineIds.Contains(line.LineId, StringComparer.Ordinal));
        }

        // The do steps: the walk to the stall on the way, the walk to the river where the fisher stands on its bank (the scene's river point is mid-stream, out of
        // reach of the arrow's three blocks from the bank), and the order delivered back at the field.
        var walks = lesson.Steps.Where(static s => s.Type == LessonStepType.Do).ToList();
        Assert.Equal(
            [
                ("stall", WorldEvents.TalkOpened, "shopkeeper", null),
                ("npc.fisher", WorldEvents.BeaconReached, null, "npc.fisher"),
                (null, WorldEvents.OrderDelivered, "farmer", "water-for-plants"),
            ],
            walks.Select(static s => (s.PointPlace, s.Event!, s.At, s.Detail)));
        Assert.All(walks, static s => Assert.False(string.IsNullOrWhiteSpace(s.Hud), s.Id));

        // The order is told in three times around the walk: the plan before the delivery, the report after it.
        var steps = lesson.Steps.ToList();
        var delivery = steps.FindIndex(static s => s.Event == WorldEvents.OrderDelivered);
        Assert.True(steps.FindIndex(static s => s.Phrase == "voy-a-buscar-agua") < steps.FindIndex(static s => s.Event == WorldEvents.BeaconReached));
        Assert.True(steps.FindIndex(static s => s.Event == WorldEvents.BeaconReached) < delivery);
        Assert.True(delivery < steps.FindIndex(static s => s.Phrase == "fui-al-rio"));
        Assert.Equal("como-es", TestGame.Data.Objectives.Find("water-for-plants")!.Lesson); // the order names the lesson that tells it
    }

    [Fact]
    public void ItsStepsDeclareThePersonAndTheTenseAndItsVerbsAreGustarInThePresentAndThePast()
    {
        var lesson = Book.Find(LessonId)!;
        var expected = new Dictionary<string, (string? Person, string? Tense)>
        {
            ["la-manzana-es-roja"] = ("3sg", null),
            ["me-gusta-el-pan"] = ("3sg", "pres"),
            ["me-gustan-las-manzanas"] = ("3pl", "pres"),
            ["quiero-tres-manzanas-rojas"] = (null, null), // the request at his open prompt, as Luz's: the sale's
            ["voy-a-buscar-agua"] = ("1sg", "ir-a"),
            ["es-grande"] = ("3sg", null),
            ["fui-al-rio"] = ("1sg", "pret"),
            ["me-gusto-el-pan"] = ("3sg", "pret"),
        };

        var steps = lesson.Steps.Where(static s => s.Type is LessonStepType.Answer or LessonStepType.Repeat).ToList();
        Assert.Equal(8, steps.Count);
        foreach (var step in steps)
        {
            Assert.Equal(expected[step.Phrase!], (step.Person, step.Tense));
            Assert.Equal(step.Person is not null, !string.IsNullOrWhiteSpace(step.PersonRetry));
            Assert.Equal(step.Tense is not null, !string.IsNullOrWhiteSpace(step.TenseRetry));
            Assert.Equal(step.Tense == TaughtVerbForm.NearFuture, step.PresentNote is not null);
        }

        // The forms it declares for the book, each declared by one lesson only (VerbLessonTests): son (the plural of es, which Personas declared), and gustar in the present
        // for one thing and for more and in the past (gusta, gustan, gustó: me gusta, me gustan, me gustó). The forms it reviews stay their first lesson's: es (Personas),
        // voy a buscar (Números y pan), fui (Ayer y mañana). An a2 lesson, though it names no simple future.
        Assert.All(lesson.TaughtVerbs, static v => Assert.Contains(v.Tense, LessonLevels.TensesAt(LessonLevels.A2)));
        var morphology = TestGame.Bundle.SpanishMorphology();
        Assert.Equal(["son", "gusta", "gustan", "gustó"], lesson.TaughtVerbs.Select(v => v.Text(morphology, TestGame.Data.Words.NearFuture)));
        var earlier = Book.Lessons.Where(static l => l.Id != LessonId).SelectMany(static l => l.TaughtVerbs).ToList();
        Assert.Contains(new TaughtVerbForm("ser", "3", "sg", "pres"), earlier);
        Assert.Contains(new TaughtVerbForm("buscar", "1", "sg", TaughtVerbForm.NearFuture), earlier);
        Assert.Contains(new TaughtVerbForm("ir", "1", "sg", "pret"), earlier);
    }

    [Fact]
    public void ItIsAnA2LessonBecauseItsSpanishUsesA2WordsAndTheValidatorHoldsItThere()
    {
        // The order's line says plantas and traes (planta and traer are A2 in the bundle's lists): the lesson is held to A2, and no lesson word is above it. The earlier
        // six stay a1.
        Assert.Empty(GameDataValidator.Validate(TestGame.Data));
        Assert.Equal([LessonLevels.A1, LessonLevels.A1, LessonLevels.A1, LessonLevels.A1, LessonLevels.A1, LessonLevels.A1, LessonLevels.A1, LessonLevels.A2], Book.Lessons.Take(8).Select(static l => l.Level));
        Assert.True(TestGame.Bundle.Cefr["a2"].Contains("traer") && !TestGame.Bundle.Cefr["a1"].Contains("traer"));
        Assert.True(TestGame.Bundle.Cefr["a2"].Contains("planta") && !TestGame.Bundle.Cefr["a1"].Contains("planta"));
        Assert.Contains(Book.Find(LessonId)!.Steps, static s => s.Line is { } line && line.Text.Contains("traes", StringComparison.Ordinal));
    }

    [Fact]
    public void TheLessonIsNotOfferedBeforeGenteDelPuebloIsDoneNorBeforeSessionSeven()
    {
        var early = TestGame.NewSessionWithShippedLessons();
        early.Day = 6;
        early.Sitting = 6;
        Assert.NotEqual(LessonId, early.Lessons.OfferFor("farmer")?.LessonId);

        var skipped = TestGame.NewSessionWithShippedLessons();
        skipped.Day = 7; // session 7 reached without session 6's lesson: ¿Cómo es? waits for it (after)
        skipped.Sitting = 7;
        Assert.Null(skipped.Lessons.OfferFor("farmer"));
        Assert.Equal(LessonStatus.NotStarted, skipped.Lessons.StatusOf(LessonId));

        var session = SessionSevenAtFarmer();
        Assert.Equal(LessonId, session.Lessons.Active!.Id);
        Assert.True(session.Lessons.Engaged);
    }

    [Fact]
    public void TheLessonRunsFromTheFieldToTheStallAndTheRiverAndBackWithTypedAnswersAndEveryStepEndsDone()
    {
        var session = SessionSevenAtFarmer();
        Assert.Equal((0, 0, 0), (session.Simulation.Inventory.Count(ItemId.Coin), session.Simulation.Inventory.Count(ItemId.Apple), session.Simulation.Inventory.Count(ItemId.WaterBottle)));

        var done = new List<LessonDriver.Done>();
        while (session.Lessons.StatusOf(LessonId) == LessonStatus.InProgress)
        {
            var step = session.Lessons.Current!;
            if (step.Id == "c4-walk")
            {
                Assert.Equal(("stall", "tienda"), (session.Pointer!.Point, session.Pointer.Place)); // the walk to the stall names it in Spanish
            }

            if (step.Id == "c5-river")
            {
                Assert.Equal(("npc.fisher", "pescador"), (session.Pointer!.Point, session.Pointer.Place)); // and the river's bank, where the fisher stands
                Assert.Equal("Go to the river (río) by the bridge and fill your bottle with water (agua)", session.Lessons.HudLine);
            }

            if (Walk(session, step) is { } d)
            {
                done.Add(d);
            }

            if (step.Id == "c3-want")
            {
                // Asked with no coins: nothing changes hands, and León says so after the lesson's own line; the step is done all the same (the Spanish was right).
                Assert.Contains("Necesitas más monedas. Una manzana cuesta una moneda.", done[^1].Shown);
                Assert.Equal((0, 0), (session.Simulation.Inventory.Count(ItemId.Coin), session.Simulation.Inventory.Count(ItemId.Apple)));
                Assert.DoesNotContain(session.Events.OfType<WorldEvent>(), static e => e.Name is WorldEvents.ItemTaken or WorldEvents.ItemGiven && e.Npc == "farmer");
            }

            if (step.Id == "c5-back")
            {
                // The water went and the apples came, the moment the talk opened.
                Assert.Equal((0, 3), (session.Simulation.Inventory.Count(ItemId.WaterBottle), session.Simulation.Inventory.Count(ItemId.Apple)));
            }
        }

        foreach (var d in done)
        {
            output.WriteLine($"{d.Id,-12} {d.Type,-8} {d.Sent,-32} {d.Turn?.Intent,-16} {d.Outcome}");
        }

        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf(LessonId));
        Assert.Equal(7, session.Lessons.Record.Single(static r => r.LessonId == LessonId).CompletedSession);
        Assert.All(done, static d => Assert.Equal(StepOutcome.Done, d.Outcome));

        // The reviews, asked by León: session 5's report at the start and, at the end, a thanks or a goodbye of session 1, both due by now.
        var reviews = done.Where(static d => d.Type == "review").ToList();
        Assert.Equal(("c0-review", "Compré dos panes", "farmer", true), (reviews[0].Id, reviews[0].Sent, reviews[0].Turn!.Npc, reviews[0].Turn!.Communicated));
        Assert.Equal("c7-review", reviews[1].Id);
        Assert.Contains(reviews[1].Sent, new[] { "Gracias", "Adiós" });
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.ReviewAnswered && e.Detail == "compre-dos-panes, lesson");

        var turns = done.Where(static d => d.Turn is not null && d.Type != "review").ToList();
        Assert.Equal(
            [
                "La manzana es roja", "Sí, me gusta el pan", "Sí, me gustan las manzanas", "Quiero tres manzanas rojas", "Voy a buscar agua", "Es grande", "Sí, fui al río",
                "Sí, me gustó el pan de Luz",
            ],
            turns.Select(static d => d.Sent));
        Assert.Equal(["describe", "state_like", "state_like", "request_item", "state_action", "describe", "state_action", "state_like"], turns.Select(static d => d.Turn!.Intent));
        Assert.All(turns, static d =>
        {
            Assert.True(d.Turn!.Communicated, d.Id);
            Assert.Empty(d.Turn.Errors);
            Assert.True(d.Turn.LessonHandled, d.Id);
        });
        Assert.Equal(["farmer", "farmer", "farmer", "farmer", "farmer", "shopkeeper", "farmer", "farmer"], turns.Select(static d => d.Turn!.Npc));
        Assert.Equal("3", turns.Single(static d => d.Id == "c3-want").Turn!.Slots["qty"]); // the number the learner said, as the sale reads it

        // The order in the log.
        var world = session.Events.OfType<WorldEvent>().ToList();
        Assert.Contains(world, static e => e.Name == WorldEvents.OrderDelivered && e.Npc == "farmer" && e.Detail == "water-for-plants");
        Assert.Contains(world, static e => e.Name == WorldEvents.ItemTaken && e.Npc == "farmer" && e.Detail == "agua, order");
        Assert.Contains(world, static e => e.Name == WorldEvents.ItemGiven && e.Npc == "farmer" && e.Detail == "manzana, order");
        Assert.True(session.Objectives.Single(static o => o.Id == "water-for-plants").Complete);
    }

    [Fact]
    public void ALearnerWhoArrivesWithTheWaterIsPaidAtTheFirstTalkAndTheClassTakenLateGoesOn()
    {
        // The bottle filled at the river in session 1 is still full (nothing in the game empties one), so most learners walk up to León with the water. No lesson holds a delivery
        // (L01): the first talk takes it and gives the apples; the class, taken late, finds the order done and goes on.
        var session = SessionSevenAtFarmer(water: 1);
        Assert.Equal(LessonId, session.Lessons.Active!.Id);
        Assert.True(session.Objectives.Single(static o => o.Id == "water-for-plants").Complete);
        Assert.Equal((0, 3), (session.Simulation.Inventory.Count(ItemId.WaterBottle), session.Simulation.Inventory.Count(ItemId.Apple)));
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e.Name is WorldEvents.OrderDelivered && e.Npc == "farmer");

        while (session.Lessons.Current is { Id: not "c5-thanks" } step)
        {
            Walk(session, step);
        }

        Assert.Equal("c5-thanks", session.Lessons.Current!.Id);
    }

    [Fact]
    public void ALessonThatDoesNotStartAtTheFarmerHoldsNothingBack()
    {
        // The hold is the lesson's: in session 7 before Gente del pueblo is done (so ¿Cómo es? cannot start), the water is handed over at the first talk (D-game-27), as for
        // every other order; and once the lesson is completed, too.
        var session = TestGame.NewSessionWithShippedLessons();
        session.Day = 7;
        session.Sitting = 7;
        _ = session.Simulation.GrantItems(ItemId.WaterBottle, 1);
        Stage.TeleportToNpc(session, "farmer");
        session.MeetOrderNpcs("farmer"); // met on an earlier visit, which told the order (issue #55)
        Assert.Equal(["¡Hola! ¿Qué tienes para mí?"], session.OpenTalk("farmer").Lines);
        Assert.Equal(["¡Gracias! Toma tres manzanas."], session.HandOverGoods()!.FollowUp);
        Assert.Equal((0, 3), (session.Simulation.Inventory.Count(ItemId.WaterBottle), session.Simulation.Inventory.Count(ItemId.Apple)));

        var early = TestGame.NewSessionWithShippedLessons();
        early.Day = 6;
        early.Sitting = 6;
        _ = early.Simulation.GrantItems(ItemId.WaterBottle, 1);
        Stage.TeleportToNpc(early, "farmer");
        early.MeetOrderNpcs("farmer");
        _ = early.OpenTalkForClass("farmer"); // L01: the order opens once León is met, whatever the day, and the water is handed over in Spanish
        Assert.Equal(0, early.Simulation.Inventory.Count(ItemId.WaterBottle));
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

    [Fact]
    public void ThePresentAtThePastOfGustarIsAMissWithATenseRetryAndAyerMeGustaIsTheCoresSlip()
    {
        // "Me gusta el pan de Luz" is right Spanish with no time word, so the core says nothing of the tense: the lesson's step, which expects the past, makes it a miss whose
        // retry names "me gustó". With "ayer" the core flags it itself (conj.tense, expected gustó, M49): a slip, and the step goes on.
        var miss = AtStep("c6-liked");
        AssertMiss(miss, "me gusta el pan de Luz", "c6-liked", "wrong tense", "“me gustó”");
        _ = miss.Say("Sí, me gustó el pan de Luz");
        Assert.Equal(StepOutcome.Done, miss.Lessons.OutcomesOf(LessonId).Single(static o => o.StepId == "c6-liked").Outcome);

        var slip = AtStep("c6-liked");
        var turn = slip.Say("ayer me gusta el pan");
        Assert.True(turn.Communicated);
        Assert.Equal("state_like", turn.Intent);
        Assert.Equal("gustó", Assert.Single(turn.Errors, static e => e.Tag == "conj.tense").Expected);
        Assert.NotEqual("c6-liked", slip.Lessons.Current?.Id);
        Assert.Equal(StepOutcome.Done, slip.Lessons.OutcomesOf(LessonId).Single(static o => o.StepId == "c6-liked").Outcome);
    }

    [Fact]
    public void ThePastWhereTheLearnerLikesBreadNowIsAMissWithATenseRetry()
    {
        var session = AtStep("c2-bread");

        AssertMiss(session, "me gustó el pan", "c2-bread", "wrong tense", "“me gusta”");
        _ = session.Say("Sí, me gusta el pan");

        Assert.Equal("c2-apples", session.Lessons.Current!.Id);
    }

    [Fact]
    public void OneAppleWhereTheApplesAreMeantIsAMissWithAPersonRetryAndMeGustaLasManzanasIsTheCoresSlip()
    {
        // "Me gusta la manzana" is right Spanish for one apple, so the core says nothing of the person: the lesson's step, which expects gustan (3pl) for the apples, makes it
        // a miss whose retry names "me gustan". "Me gusta las manzanas" the core flags itself since M56, which agrees the verb with a subject after it (conj.person, expected
        // gustan): a slip, and the step goes on.
        var miss = AtStep("c2-apples");
        AssertMiss(miss, "me gusta la manzana", "c2-apples", "wrong person: said 3sg, expected 3pl", "“me gustan”");
        _ = miss.Say("Sí, me gustan las manzanas");
        Assert.Equal("c3-want", miss.Lessons.Current!.Id);

        var slip = AtStep("c2-apples");
        var turn = slip.Say("me gusta las manzanas");
        Assert.True(turn.Communicated);
        Assert.Equal("state_like", turn.Intent);
        Assert.Equal("gustan", Assert.Single(turn.Errors, static e => e.Tag == "conj.person").Expected);
        Assert.Equal("c3-want", slip.Lessons.Current!.Id);
        Assert.Contains(slip.Events.OfType<LessonEvent>(), static e => e.Kind == LessonEventKind.StepDone && e.StepId == "c2-apples" && e.Detail == "with a slip");
    }

    [Fact]
    public void ThePresentAtThePlanIsAMissWithATenseRetryAndVoyABuscoIsTheCoresSlip()
    {
        var miss = AtStep("c3-plan");
        AssertMiss(miss, "busco agua", "c3-plan", "wrong tense", "“voy a buscar”");
        _ = miss.Say("Voy a buscar agua");
        Assert.Equal("c4-walk", miss.Lessons.Current!.Id);

        // Right Spanish for a plan (S27): the present with a future time word is credited, with the note.
        var plan = AtStep("c3-plan");
        Assert.Empty(plan.Say("luego busco agua").Errors);
        Assert.Equal("c4-walk", plan.Lessons.Current!.Id);
        Assert.Contains(plan.Events.OfType<LessonEvent>(), static e => e.Kind == LessonEventKind.StepDone && e.StepId == "c3-plan" && e.Detail == "present for a plan, credited");

        var slip = AtStep("c3-plan");
        var turn = slip.Say("voy a busco agua");
        Assert.Equal("buscar", Assert.Single(turn.Errors, static e => e.Tag == "conj.tense").Expected);
        Assert.Equal("c4-walk", slip.Lessons.Current!.Id);
    }

    [Fact]
    public void ThePresentAtTheReportIsAMissWithATenseRetryAndAyerVoyIsTheCoresSlip()
    {
        var miss = AtStep("c5-went");
        AssertMiss(miss, "voy al río", "c5-went", "wrong tense", "“fui”");
        _ = miss.Say("Sí, fui al río");
        Assert.Equal("c6-model", miss.Lessons.Current!.Id);

        var slip = AtStep("c5-went");
        var turn = slip.Say("ayer voy al río");
        Assert.Equal("fui", Assert.Single(turn.Errors, static e => e.Tag == "conj.tense").Expected);
        Assert.Equal("c6-model", slip.Lessons.Current!.Id);
    }

    [Theory]
    [InlineData("c3-want", "quiero tres manzanas rojos", "rojas")] // the adjective after the noun agrees with it
    [InlineData("c1-how", "el manzana es roja", "la")] // and the article; the predicate adjective has its own test below (core M62)
    [InlineData("c2-apples", "me gustan los manzanas", "las")]
    public void AWordThatDoesNotAgreeIsTheCoresGenderSlipAndTheStepGoesOn(string stepId, string said, string expected)
    {
        var session = AtStep(stepId);

        var turn = session.Say(said);

        Assert.True(turn.Communicated, said);
        Assert.Equal(expected, Assert.Single(turn.Errors, static e => e.Tag == "gender.agree").Expected);
        Assert.NotEqual(stepId, session.Lessons.Current!.Id); // a slip: the step goes on
    }

    [Fact]
    public void LaManzanaEsRojoIsTheCoresGenderSlipWithTheRecastRojaAndTheStepIsDoneWithTheSlip()
    {
        // V13's report was a core gap: an adjective after ser was not checked against its subject. Since the core's M62 it is: "la manzana es rojo" is the core's
        // gender.agree (expected roja), the step is done with the slip, and only the core's recast speaks (the lesson takes the turn, so the farmer's own reply is not
        // shown, and the lesson's check gives no retry or hint of its own). The sheet's row may take the plan's error example.
        var slip = AtStep("c1-how");
        var attempts = slip.Lessons.Attempts;
        var turn = slip.Say("la manzana es rojo");

        Assert.Equal(("describe", true), (turn.Intent, turn.Communicated));
        Assert.Equal("roja", Assert.Single(turn.Errors, static e => e.Tag == "gender.agree").Expected);
        Assert.Single(turn.Errors);
        Assert.True(turn.LessonHandled);
        Assert.Contains("«la manzana es roja»", turn.Feedback, StringComparison.Ordinal);
        Assert.Equal([turn.Feedback!], turn.GuideLines); // one correction line: the recast, nothing of the lesson's own check besides it
        Assert.Equal(attempts, slip.Lessons.Attempts); // no retry
        Assert.DoesNotContain(slip.Events.OfType<LessonEvent>(), static e => e.Kind == LessonEventKind.Retry && e.StepId == "c1-how");
        Assert.Equal("c2-bread", slip.Lessons.Current!.Id);
        Assert.Contains(slip.Events.OfType<LessonEvent>(), static e => e.Kind == LessonEventKind.StepDone && e.StepId == "c1-how" && e.Detail == "with a slip");

        // The clean neighbour: the right adjective is done with no correction at all.
        var right = AtStep("c1-how");
        var clean = right.Say("la manzana es roja");

        Assert.Equal(("describe", true), (clean.Intent, clean.Communicated));
        Assert.Empty(clean.Errors);
        Assert.Null(clean.Feedback);
        Assert.Equal("c2-bread", right.Lessons.Current!.Id);
        Assert.Contains(right.Events.OfType<LessonEvent>(), static e => e.Kind == LessonEventKind.StepDone && e.StepId == "c1-how" && e.Detail != "with a slip");
    }

    // ---- The farmer outside the lesson: his sale, his price and his order

    [Fact]
    public void TheFarmerSellsAnAppleForACoinAndWithTooFewCoinsSellsNothingAndSaysSo()
    {
        var session = TestGame.NewSessionWithShippedLessons(); // session 1: ¿Cómo es? is not unlocked, so he talks as himself
        Stage.TeleportToNpc(session, "farmer");
        session.MeetOrderNpcs("farmer"); // a later visit: the first tells his order (issue #55)
        Assert.Equal(["¡Hola!"], session.OpenTalkForClass("farmer").Lines);
        Assert.Equal(["Una manzana cuesta una moneda."], session.Say("¿Cuánto cuesta?").GuideLines);

        _ = session.Simulation.GrantItems(ItemId.Coin, 2);
        var short1 = session.Say("Quiero tres manzanas rojas"); // three apples are three coins
        Assert.Equal(("request_item", "3"), (short1.Intent, short1.Slots["qty"]));
        Assert.Empty(short1.Errors);
        Assert.Equal(["Necesitas más monedas. Una manzana cuesta una moneda."], short1.GuideLines); // his line of the sale is not said; cant_pay is
        Assert.Equal((2, 0), (session.Simulation.Inventory.Count(ItemId.Coin), session.Simulation.Inventory.Count(ItemId.Apple)));
        Assert.DoesNotContain(session.Events.OfType<WorldEvent>(), static e => e.Name is WorldEvents.ItemTaken or WorldEvents.ItemGiven && e.Npc == "farmer");

        _ = session.Simulation.GrantItems(ItemId.Coin, 1);
        var bought = session.Say("Quiero tres manzanas rojas");
        Assert.Single(bought.GuideLines, static l => l.EndsWith("¡Gracias!", StringComparison.Ordinal));
        Assert.Equal((0, 3), (session.Simulation.Inventory.Count(ItemId.Coin), session.Simulation.Inventory.Count(ItemId.Apple)));
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.ItemTaken && e.Npc == "farmer" && e.Detail == "moneda, request");
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.ItemGiven && e.Npc == "farmer" && e.Detail == "manzana, request");

        // And he takes what the learner describes and likes at his open prompt.
        Assert.Equal(("describe", true), (session.Say("La manzana es roja").Intent, session.Say("La manzana es roja").Communicated));
        var like = session.Say("Me gustan las manzanas");
        Assert.Equal(("state_like", true), (like.Intent, like.Communicated));
        Assert.Empty(like.Errors);
    }

    [Fact]
    public void TheOrderTakesTheWaterFromSessionSevenAndGivesThreeApplesOnce()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        session.Day = 7;
        session.Sitting = 7;
        _ = session.Simulation.GrantItems(ItemId.WaterBottle, 2);
        Stage.TeleportToNpc(session, "farmer");
        session.MeetOrderNpcs("farmer"); // met on an earlier visit, which told the order (issue #55)

        var opening = session.OpenTalk("farmer"); // León asks what the learner has, and the water is handed over in Spanish (D-game-27)
        Assert.Equal(["¡Hola! ¿Qué tienes para mí?"], opening.Lines);

        var turn = session.Say("Aquí tienes la botella."); // the bottle names the goods as well as the water

        Assert.Equal(["¡Gracias! Toma tres manzanas."], turn.FollowUp);
        Assert.Equal((1, 3), (session.Simulation.Inventory.Count(ItemId.WaterBottle), session.Simulation.Inventory.Count(ItemId.Apple)));
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.OrderDelivered && e.Npc == "farmer" && e.Detail == "water-for-plants");
        Assert.True(session.Objectives.Single(static o => o.Id == "water-for-plants").Complete);
        Assert.Equal(1, session.ObjectiveStep("water-for-plants.delivered")!.Count);

        // Once is enough: the next talk is an ordinary one.
        session.EndTalk();
        Assert.False(session.OpenTalkForClass("farmer").Delivered);
        Assert.False(session.HandOverOpen);
        Assert.Equal(3, session.Simulation.Inventory.Count(ItemId.Apple));
    }

    [Fact]
    public void TheFarmerAndTheShopkeeperSpeakTheLessonsLinesWithTheirOwnVoices()
    {
        var data = TestGame.Data;
        Assert.Equal(("farmer", "farmer", "shopkeeper"), (SpeechSpeakers.OfPool("farmer_cant_pay", data.Npcs), SpeechSpeakers.OfPool("guide.lesson.farmer.water", data.Npcs), SpeechSpeakers.OfPool("guide.lesson.shop.luz", data.Npcs)));
        Assert.Contains("quiero-tres-manzanas-rojas", data.Npcs.FocusAt("farmer-open")); // his apples are at home at his open prompt (V12b)
    }
}
