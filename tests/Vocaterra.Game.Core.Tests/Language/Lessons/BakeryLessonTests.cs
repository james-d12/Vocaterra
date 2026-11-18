using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Es;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.Language.Lessons;

/// <summary>
/// The lesson "Números y pan" of session 5 (village-plan V11, 3.5): the baker Luz at the bakery, the numbers 1 to 10, plurals, ¿Cuántos…?, ¿Cuánto cuesta?, and the order
/// oven-wood told in the three times (voy a buscar madera, tengo cuatro monedas, compré dos panes: the regular -ar past); the purchase of bread with the coins the order
/// pays; ¿Hay pan? at the stall; the tense steps; the baker's own answers outside the lesson; and the stall's first exchange, which costs nothing still.
/// </summary>
public sealed class BakeryLessonTests(ITestOutputHelper output)
{
    private const string LessonId = "numeros-y-pan";

    private static LessonBook Book => TestGame.NewSessionWithShippedLessons().Lessons.Book;

    // El pueblo done in session 4 (Números y pan comes after it), then session 5 at the baker: her lesson starts by itself.
    private static VoxelSession SessionFiveAtBaker(int wood = 0)
    {
        var session = VillageLessonTests.AtStep("(after the last step)"); // no step has this id: El pueblo runs to its end
        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf("el-pueblo"));
        session.EndTalk();
        session.Day = 5;
        session.Sitting = 5;
        if (wood > 0)
        {
            Assert.Equal(0, session.Simulation.GrantItems(ItemId.Wood, wood));
        }

        Stage.TeleportToNpc(session, "baker");
        _ = session.OpenTalkForClass("baker");
        return session;
    }

    // The lesson as a learner who gets everything right does it, up to the step with this id.
    internal static VoxelSession AtStep(string stepId)
    {
        var session = SessionFiveAtBaker();
        while (session.Lessons.Current is { } step && step.Id != stepId)
        {
            Walk(session, step);
        }

        return session;
    }

    // One step: the wood is what the forest's edge gives (the step waits for the first piece, the order for all five); the purchase is said to the baker herself (a do
    // step takes no turn); a talk the step waits for is opened at that NPC; every other step is the driver's.
    private static LessonDriver.Done? Walk(VoxelSession session, LessonStep step)
    {
        if (step.Id == "n4-wood")
        {
            Assert.Equal(0, session.Simulation.GrantItems(ItemId.Wood, 5));
            return LessonDriver.DoStep(session);
        }

        if (step.Id == "n6-buy")
        {
            const string Purchase = "Quiero dos panes, por favor";
            var turn = session.Say(Purchase);
            return new LessonDriver.Done(step.Id, "do", Purchase, session.Lessons.OutcomesOf(LessonId).Single(o => o.StepId == step.Id).Outcome, turn.GuideLines, turn);
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
    public void NumerosYPanIsTheBakersLessonOfSessionFiveAfterElPuebloWithTheShopkeeperAsItsSecondNpc()
    {
        var lesson = Book.Find(LessonId)!;

        Assert.Equal(("baker", 5, false, "el-pueblo"), (lesson.Npc, lesson.Session, lesson.AutoStart, lesson.After));
        Assert.StartsWith("Session 5", lesson.Objective, StringComparison.Ordinal);
        Assert.Contains("bakery", lesson.Objective, StringComparison.Ordinal); // the objective line names where to go: the bakery
        Assert.Contains("Números y pan", lesson.Objective, StringComparison.Ordinal);
        Assert.Contains("bakery", Book.Find("el-pueblo")!.Steps[^1].English, StringComparison.Ordinal); // and El pueblo's recap names it as the next place
        Assert.Equal(["baker", "shopkeeper"], lesson.Steps.Select(lesson.NpcOf).Distinct());

        // Six phrases the learner says, and two review steps: the plan of session 4 or the greeting at the start, the "no" of session 1 at the stall's offer.
        Assert.Equal(["quiero-dos-panes", "cuanto-cuesta", "voy-a-buscar-madera", "tengo-cuatro-monedas", "compre-dos-panes", "hay-pan"], lesson.TaughtPhrases);
        Assert.Equal(
            [["voy-a-comprar-pan", "hola"], ["no"]],
            lesson.Steps.Where(static s => s.Type == LessonStepType.Review).Select(static s => s.Candidates!.Select(static c => c.Phrase).ToArray()));
        Assert.Equal(LessonStepType.Recap, lesson.Steps[^1].Type);
        Assert.InRange(lesson.Steps.Count, 15, 26);
        foreach (var step in lesson.Steps)
        {
            if (step.Line is { } line)
            {
                Assert.Equal(lesson.NpcOf(step), line.Speaker);
            }

            foreach (var candidate in step.Candidates ?? [])
            {
                Assert.Equal(lesson.NpcOf(step), candidate.Line.Speaker);
            }
        }

        // The heard rows are the lines of its steps.
        var heard = new[]
        {
            "uno-a-cinco", "seis-a-diez", "tengo-diez-panes", "cuantos-panes", "cuesta-dos-monedas", "necesito-madera-para-el-horno", "toma-cuatro-monedas", "ayer-compre-fruta",
            "que-compraste", "compraste-dos-panes",
        };
        var sheet = TestGame.Data.Phrases;
        foreach (var id in heard)
        {
            var row = sheet.Find(id)!;
            Assert.Equal("hears", row.Says);
            Assert.Contains(lesson.Steps, s => s.Line is { } line && row.AudioLineIds.Contains(line.LineId, StringComparer.Ordinal));
        }

        // The do steps: the wood (the arrow on the nearest tree), the order delivered, the purchase, and the walk to the stall.
        var walks = lesson.Steps.Where(static s => s.Type == LessonStepType.Do).ToList();
        Assert.Equal(
            [
                ("árbol", null, WorldEvents.ItemPickedUp, null, "madera"),
                (null, null, WorldEvents.OrderDelivered, "baker", "oven-wood"),
                (null, null, WorldEvents.ItemGiven, "baker", "pan, request"),
                (null, "stall", WorldEvents.TalkOpened, "shopkeeper", null),
            ],
            walks.Select(static s => (s.Point, s.PointPlace, s.Event!, s.At, s.Detail)));
        Assert.All(walks, static s => Assert.False(string.IsNullOrWhiteSpace(s.Hud), s.Id));
    }

    [Fact]
    public void ItsTenseStepsDeclareThePersonAndTheTenseAndItsVerbsAreThePresentThePastAndTheNearFuture()
    {
        var lesson = Book.Find(LessonId)!;
        var expected = new Dictionary<string, (string Person, string Tense)>
        {
            ["voy-a-buscar-madera"] = ("1sg", "ir-a"),
            ["tengo-cuatro-monedas"] = ("1sg", "pres"),
            ["compre-dos-panes"] = ("1sg", "pret"),
        };

        var steps = lesson.Steps.Where(s => s.Phrase is { } p && expected.ContainsKey(p) && s.Type == LessonStepType.Answer).ToList();
        Assert.Equal(3, steps.Count);
        foreach (var step in steps)
        {
            Assert.Equal(expected[step.Phrase!], (step.Person!, step.Tense!));
            Assert.False(string.IsNullOrWhiteSpace(step.PersonRetry), step.Id);
            Assert.False(string.IsNullOrWhiteSpace(step.TenseRetry), step.Id);
            Assert.Equal(step.Tense == TaughtVerbForm.NearFuture, step.PresentNote is not null);
        }

        // The forms it declares for the book: costar in the present, buscar as a plan, and the regular -ar past of comprar for yo and tú (an a1 lesson: no simple
        // future, village-plan 3.9).
        Assert.All(lesson.TaughtVerbs, static v => Assert.Contains(v.Tense, new[] { "pres", "pret", TaughtVerbForm.NearFuture }));
        var morphology = TestGame.Bundle.SpanishMorphology();
        Assert.Equal(["cuesta", "cuestan", "voy a buscar", "compré", "compraste"], lesson.TaughtVerbs.Select(v => v.Text(morphology, TestGame.Data.Words.NearFuture)));
    }

    [Fact]
    public void TheLessonIsNotOfferedBeforeElPuebloIsDoneNorBeforeSessionFive()
    {
        var early = TestGame.NewSessionWithShippedLessons();
        early.Day = 4;
        early.Sitting = 4;
        Assert.Null(early.Lessons.OfferFor("baker"));

        var skipped = TestGame.NewSessionWithShippedLessons();
        skipped.Day = 5; // session 5 reached without session 4's lesson: Números y pan waits for it (after)
        skipped.Sitting = 5;
        Assert.Null(skipped.Lessons.OfferFor("baker"));
        Assert.Equal(LessonStatus.NotStarted, skipped.Lessons.StatusOf(LessonId));

        var session = SessionFiveAtBaker();
        Assert.Equal(LessonId, session.Lessons.Active!.Id);
        Assert.True(session.Lessons.Engaged);
    }

    [Fact]
    public void TheLessonRunsFromTheBakeryToTheForestAndBackThenToTheStallWithTypedAnswersAndEveryStepEndsDone()
    {
        var session = SessionFiveAtBaker();
        var first = session.Lessons.Current!;
        Assert.Equal(("n0-review", "voy-a-comprar-pan", "¡Hola! ¿Qué vas a hacer hoy?"), (first.Id, first.Phrase, first.Line!.Text)); // session 4's plan is due, asked by her
        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.Coin));

        var done = new List<LessonDriver.Done>();
        while (session.Lessons.StatusOf(LessonId) == LessonStatus.InProgress)
        {
            var step = session.Lessons.Current!;
            if (step.Id == "n4-wood")
            {
                Assert.Equal("árbol", session.Pointer!.Place); // the arrow on the nearest tree, at the forest's edge
                Assert.Equal("Collect wood (madera) at the forest's edge", session.Lessons.HudLine);
            }

            if (step.Id == "n8-walk")
            {
                Assert.Equal(("stall", "tienda"), (session.Pointer!.Point, session.Pointer.Place)); // the walk to the stall names it in Spanish
            }

            if (Walk(session, step) is { } d)
            {
                done.Add(d);
            }

            if (step.Id == "n2-want")
            {
                // Asked before the order, the learner has no coins: nothing changes hands, and Luz says so after the lesson's own line.
                Assert.Contains("Necesitas más monedas.", done[^1].Shown);
                Assert.Equal((0, 0), (session.Simulation.Inventory.Count(ItemId.Coin), session.Simulation.Inventory.Count(TestItems.Bread)));
                Assert.DoesNotContain(session.Events.OfType<WorldEvent>(), static e => e.Name is WorldEvents.ItemTaken or WorldEvents.ItemGiven && e.Npc == "baker");
            }

            if (step.Id == "n5-have")
            {
                Assert.Equal((4, 0), (session.Simulation.Inventory.Count(ItemId.Coin), session.Simulation.Inventory.Count(ItemId.Wood))); // the wood went, the pay came
            }
        }

        foreach (var d in done)
        {
            output.WriteLine($"{d.Id,-12} {d.Type,-8} {d.Sent,-28} {d.Turn?.Intent,-16} {d.Outcome}");
        }

        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf(LessonId));
        Assert.Equal(5, session.Lessons.Record.Single(static r => r.LessonId == LessonId).CompletedSession);
        Assert.All(done, static d => Assert.Equal(StepOutcome.Done, d.Outcome));

        // The purchase: four coins for two loaves.
        var purchase = done.Single(static d => d.Id == "n6-buy");
        Assert.Equal(("request_item", false), (purchase.Turn!.Intent, purchase.Turn.LessonHandled)); // said to Luz, not taken by the lesson
        Assert.Contains(purchase.Turn.GuideLines, static l => l.EndsWith("¡Gracias!", StringComparison.Ordinal));
        Assert.Equal((0, 2), (session.Simulation.Inventory.Count(ItemId.Coin), session.Simulation.Inventory.Count(TestItems.Bread)));

        var turns = done.Where(static d => d.Turn is not null && d.Id != "n6-buy").ToList();
        Assert.Equal(
            ["Voy a comprar pan", "Quiero dos panes, por favor", "¿Cuánto cuesta?", "Voy a buscar madera", "Tengo cuatro monedas", "Compré dos panes", "¿Hay pan?"],
            turns.Select(static d => d.Sent));
        Assert.Equal(["state_action", "request_item", "ask_price", "state_action", "state_have", "state_action", "ask_availability"], turns.Select(static d => d.Turn!.Intent));
        Assert.All(turns, static d =>
        {
            Assert.True(d.Turn!.Communicated, d.Id);
            Assert.Empty(d.Turn.Errors);
            Assert.True(d.Turn.LessonHandled, d.Id);
        });
        Assert.Equal(["baker", "baker", "baker", "baker", "baker", "baker", "shopkeeper"], turns.Select(static d => d.Turn!.Npc));
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.ReviewAnswered && e.Detail == "voy-a-comprar-pan, lesson");

        // The order and the purchase in the log.
        var world = session.Events.OfType<WorldEvent>().ToList();
        Assert.Contains(world, static e => e.Name == WorldEvents.OrderDelivered && e.Npc == "baker" && e.Detail == "oven-wood");
        Assert.Contains(world, static e => e.Name == WorldEvents.ItemTaken && e.Npc == "baker" && e.Detail == "madera, order");
        Assert.Contains(world, static e => e.Name == WorldEvents.ItemGiven && e.Npc == "baker" && e.Detail == "moneda, order");
        Assert.Contains(world, static e => e.Name == WorldEvents.ItemTaken && e.Npc == "baker" && e.Detail == "moneda, request");
        Assert.Contains(world, static e => e.Name == WorldEvents.ItemGiven && e.Npc == "baker" && e.Detail == "pan, request");
        Assert.True(session.Objectives.Single(static o => o.Id == "oven-wood").Complete);
    }

    // ---- The tense check

    private static void AssertMiss(VoxelSession session, string said, string stepId, string reasonStart, string hintContains)
    {
        var attempts = session.Lessons.Attempts;
        var turn = session.Say(said);

        Assert.True(turn.Communicated, said);
        Assert.True(turn.LessonHandled, said);
        Assert.DoesNotContain(turn.Errors, static e => e.Tag.StartsWith("conj.", StringComparison.Ordinal)); // no time word: the core says nothing of the tense, the lesson does
        Assert.Equal(stepId, session.Lessons.Current!.Id); // a miss: the step stays
        Assert.Equal(attempts + 1, session.Lessons.Attempts);
        Assert.Contains(hintContains, session.Lessons.View!.Hint, StringComparison.Ordinal);
        Assert.Contains(session.Events.OfType<LessonEvent>(), e => e.Kind == LessonEventKind.Retry && e.StepId == stepId && e.Detail!.StartsWith(reasonStart, StringComparison.Ordinal));
    }

    [Fact]
    public void ThePresentAtThePlanIsAMissWithATenseRetryAndVoyABuscoIsTheCoresSlip()
    {
        var miss = AtStep("n3-oven");
        AssertMiss(miss, "busco madera", "n3-oven", "wrong tense", "“voy a buscar”");
        _ = miss.Say("Voy a buscar madera");
        Assert.Equal("n4-wood", miss.Lessons.Current!.Id);

        // A conjugated verb after "ir a" is the core's conj.tense (M49): a slip, and the step goes on.
        var slip = AtStep("n3-oven");
        var turn = slip.Say("voy a busco madera");
        Assert.True(turn.Communicated);
        Assert.Equal("buscar", Assert.Single(turn.Errors, static e => e.Tag == "conj.tense").Expected);
        Assert.Equal("n4-wood", slip.Lessons.Current!.Id);
    }

    [Theory]
    [InlineData("luego busco madera")]
    [InlineData("después busco madera")]
    public void APlanInThePresentWithAFutureTimeWordIsCreditedWithTheNote(string said)
    {
        var session = AtStep("n3-oven");

        var turn = session.Say(said);

        Assert.True(turn.Communicated, said);
        Assert.Empty(turn.Errors);
        Assert.Equal("n4-wood", session.Lessons.Current!.Id);
        Assert.Contains(session.Events.OfType<LessonEvent>(), static e => e.Kind == LessonEventKind.StepDone && e.StepId == "n3-oven" && e.Detail == "present for a plan, credited");
        Assert.Equal(Book.Find(LessonId)!.Steps.Single(static s => s.Id == "n3-oven").PresentNote, session.Lessons.View!.Hint);
    }

    [Fact]
    public void ThePresentAtTheReportIsAMissWithATenseRetryAndAyerComproIsTheCoresSlip()
    {
        // After the purchase: it is over. "Compro dos panes" has no time word, so the lesson judges it: a miss whose retry names "compré". With "ayer" the core flags the tense
        // itself (conj.tense, expected compré, M49): a slip, and the step goes on.
        var miss = AtStep("n7-bought");
        AssertMiss(miss, "compro dos panes", "n7-bought", "wrong tense", "“compré”");
        _ = miss.Say("Compré dos panes");
        Assert.Equal("n7-you", miss.Lessons.Current!.Id);

        var slip = AtStep("n7-bought");
        var turn = slip.Say("ayer compro dos panes");
        Assert.True(turn.Communicated);
        Assert.Equal("compré", Assert.Single(turn.Errors, static e => e.Tag == "conj.tense").Expected);
        Assert.Equal("n7-you", slip.Lessons.Current!.Id);
        Assert.Equal(StepOutcome.Done, slip.Lessons.OutcomesOf(LessonId)[^1].Outcome);
    }

    [Fact]
    public void ThePastWhereTheLearnerCountsTheCoinsIsAMissWithATenseRetry()
    {
        var session = AtStep("n5-have");

        AssertMiss(session, "tuve cuatro monedas", "n5-have", "wrong tense", "“tengo”");
        _ = session.Say("Tengo cuatro monedas");

        Assert.Equal("n6-buy", session.Lessons.Current!.Id);
    }

    // ---- The baker outside the lesson, the order and the stall

    [Fact]
    public void TheBakerSellsTwoLoavesForFourCoinsAndWithThreeCoinsSellsNothingAndSaysSo()
    {
        var session = TestGame.NewSessionWithShippedLessons().TakeOrders(); // session 1: Números y pan is not unlocked, so she talks as herself (her order taken, issue #54)
        Stage.TeleportToNpc(session, "baker");
        Assert.Equal(["¡Hola!"], session.OpenTalkForClass("baker").Lines); // she greets at every talk
        Assert.Equal(["El pan cuesta dos monedas."], session.Say("¿Cuánto cuesta?").GuideLines);
        Assert.Equal(["Aquí hay pan. ¿Cuántos panes quieres?"], session.Say("¿Hay pan?").GuideLines);

        _ = session.Simulation.GrantItems(ItemId.Coin, 3);
        var short1 = session.Say("Quiero dos panes, por favor"); // two loaves are four coins
        Assert.Equal(("request_item", "2"), (short1.Intent, short1.Slots["qty"]));
        Assert.Equal(["Necesitas más monedas."], short1.GuideLines); // her line of the sale is not said; cant_pay is
        Assert.Equal((3, 0), (session.Simulation.Inventory.Count(ItemId.Coin), session.Simulation.Inventory.Count(TestItems.Bread)));
        Assert.DoesNotContain(session.Events.OfType<WorldEvent>(), static e => e.Name is WorldEvents.ItemTaken or WorldEvents.ItemGiven && e.Npc == "baker");

        _ = session.Simulation.GrantItems(ItemId.Coin, 1);
        var bought = session.Say("Quiero dos panes, por favor");
        Assert.Single(bought.GuideLines, static l => l.EndsWith("¡Gracias!", StringComparison.Ordinal));
        Assert.Equal((0, 2), (session.Simulation.Inventory.Count(ItemId.Coin), session.Simulation.Inventory.Count(TestItems.Bread)));
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.ItemTaken && e.Npc == "baker" && e.Detail == "moneda, request");
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.ItemGiven && e.Npc == "baker" && e.Detail == "pan, request");
    }

    [Fact]
    public void TheOrderOfWoodIsOfferedByLuzAndBringingFiveWoodPaysFourCoins()
    {
        var early = TestGame.NewSessionWithShippedLessons();
        early.Day = 4;
        early.Sitting = 4;
        _ = early.Simulation.GrantItems(ItemId.Wood, 5);
        Stage.TeleportToNpc(early, "baker");
        Assert.Equal(["¿Me traes cinco bloques de madera para el horno? Te pago cuatro monedas."], early.OpenTalk("baker").Lines); // issue #54: with no class of hers open, Luz offers the order, whatever the day
        Assert.Equal(["¡Gracias! Hay árboles cerca del pueblo."], early.AcceptOrder()!.GuideLines);
        Assert.Equal(5, early.Simulation.Inventory.Count(ItemId.Wood));
        early.EndTalk();
        _ = early.OpenTalkForClass("baker"); // the next talk takes the wood, handed over in Spanish (D-game-27)
        Assert.Equal(0, early.Simulation.Inventory.Count(ItemId.Wood));

        var session = TestGame.NewSessionWithShippedLessons().TakeOrders("oven-wood");
        session.Day = 5;
        session.Sitting = 5;
        _ = session.Simulation.GrantItems(ItemId.Wood, 6);
        Stage.TeleportToNpc(session, "baker");

        var opening = session.OpenTalk("baker"); // Luz asks what the learner has, and the wood is handed over in Spanish (D-game-27)
        Assert.Equal(["¡Hola! ¿Qué tienes para mí?"], opening.Lines);
        Assert.Equal(6, session.Simulation.Inventory.Count(ItemId.Wood)); // nothing is taken before the line

        var turn = session.HandOverGoods()!; // «Aquí tienes cinco bloques de madera.»

        Assert.Equal(["¡Gracias! Toma cuatro monedas."], turn.FollowUp);
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Wood));
        Assert.Equal(4, session.Simulation.Inventory.Count(ItemId.Coin));
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.OrderDelivered && e.Npc == "baker" && e.Detail == "oven-wood");
        Assert.True(session.Objectives.Single(static o => o.Id == "oven-wood").Complete);
        Assert.Equal(1, session.ObjectiveStep("oven-wood.delivered")!.Count);
    }

    [Fact]
    public void TheClassWaitsWhileLuzAsksForTheWoodAndIsOfferedRightAfterTheHandOver()
    {
        // D-game-27 (issue #42): no class is offered, started or resumed while the NPC waits for the goods, so her question and a lesson step never share the panel;
        // the class follows the hand-over in the same talk.
        var session = VillageLessonTests.AtStep("(after the last step)");
        session.EndTalk();
        session.Day = 5;
        session.Sitting = 5;
        Assert.Equal(0, session.Simulation.GrantItems(ItemId.Wood, 5));
        _ = session.TakeOrders("oven-wood"); // taken on an earlier day, before her class was open (issue #54)
        Stage.TeleportToNpc(session, "baker");

        _ = session.OpenTalk("baker");

        Assert.True(session.HandOverOpen);
        Assert.Null(session.Lessons.OfferStandingAt("baker"));
        Assert.False(session.Lessons.Engaged);

        _ = session.HandOverGoods();

        Assert.False(session.HandOverOpen);
        Assert.True(session.Objectives.Single(static o => o.Id == "oven-wood").Complete);
        Assert.Equal(LessonId, session.Lessons.OfferStandingAt("baker"));
    }

    [Fact]
    public void ALearnerWhoArrivesWithTheWoodIsPaidAtTheFirstTalkAndTheClassTakenLateGoesOn()
    {
        // The wood may come first. Luz's class tells the order, so she offers the class and not the order (issue #54): the first talk takes nothing, the class gives the
        // order at n3-oven («Necesito cinco bloques de madera para el horno.», lessonStep), and the wood is handed over at the step that waits for it, so «Voy a buscar
        // madera» comes before her thanks, as V14 meant.
        var session = SessionFiveAtBaker(wood: 5);
        Assert.Equal(LessonId, session.Lessons.Active!.Id);
        Assert.Equal((LessonId, "n3-oven"), (TestGame.Data.Objectives.Find("oven-wood")!.Lesson, TestGame.Data.Objectives.Find("oven-wood")!.LessonStep));
        Assert.False(session.HasOrder("oven-wood"));
        Assert.Equal(5, session.Simulation.Inventory.Count(ItemId.Wood));
        while (session.Lessons.Current is { Id: not "n4-wood" } step)
        {
            Walk(session, step);
        }

        Assert.True(session.HasOrder("oven-wood")); // given at n3-oven
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.OrderAccepted && e.Detail == "oven-wood, lesson");
        while (session.Lessons.Current is { Id: not "n5-coins" } step)
        {
            Walk(session, step);
        }

        Assert.True(session.Objectives.Single(static o => o.Id == "oven-wood").Complete);
        Assert.DoesNotContain(session.Events.OfType<LessonEvent>(), static e => e.StepId == "n4-back" && e.Detail == "delivered before"); // delivered at its own step
    }

    [Fact]
    public void AStepWaitingForAPickupIsDoneAtOnceWhenTheBagHoldsWhatTheOrderNeedsAndNotBefore()
    {
        // QA-progression-4: the wood step waited for a pickup although the five wood the order asks for were in the bag already (V23a).
        var held = SessionFiveAtBaker(wood: 0);
        _ = held.Simulation.GrantItems(ItemId.Wood, 5); // after the first talk, which would have taken them (L01)
        while (held.Lessons.Current is { Id: not "n3-oven" } step)
        {
            Walk(held, step);
        }

        _ = Walk(held, held.Lessons.Current!); // the plan is said; the wood step is entered and ends by itself
        Assert.Equal("n4-back", held.Lessons.Current!.Id);
        Assert.Contains(held.Events.OfType<LessonEvent>(), static e => e is { Kind: LessonEventKind.StepDone, StepId: "n4-wood", Detail: "held before" });
        Assert.Equal(5, held.Simulation.Inventory.Count(ItemId.Wood)); // nothing was taken or granted

        var short_ = SessionFiveAtBaker(wood: 2); // two of five: the step still waits for a pickup
        while (short_.Lessons.Current is { Id: not "n3-oven" } step)
        {
            Walk(short_, step);
        }

        _ = Walk(short_, short_.Lessons.Current!);
        Assert.Equal("n4-wood", short_.Lessons.Current!.Id);
    }

    [Fact]
    public void TheStallsFirstExchangeCostsNothingAndTheStallNowAnswersThePriceAndHayPan()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        Stage.TeleportToNpc(session, "shopkeeper");

        Assert.Equal(["¿Qué necesitas?"], session.OpenTalkForClass("shopkeeper").Lines);
        Assert.Equal(["El pan cuesta dos monedas."], session.Say("¿Cuánto cuesta el pan?").GuideLines);
        Assert.Equal(["Hay agua, pan, harina, tela, cuero y ropa."], session.Say("¿Hay pan?").GuideLines);
        var water = session.Say("Quiero agua, por favor");
        Assert.Equal("request_item", water.Intent);
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Bottle));
        _ = session.Say("Sí, por favor"); // the bread the stall offers is still a gift: no coins, and none asked for

        Assert.Equal((1, 0), (session.Simulation.Inventory.Count(TestItems.Bread), session.Simulation.Inventory.Count(ItemId.Coin)));
        Assert.DoesNotContain(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.ItemTaken);
    }
}
