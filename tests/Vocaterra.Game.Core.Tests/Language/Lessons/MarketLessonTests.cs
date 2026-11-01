using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Book;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Es;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.Language.Lessons;

/// <summary>
/// The lesson "El mercado" of session 10 (VILLAGE_PLAN V15, 3.5), an <c>a2</c> lesson and the last of the village: market day. The baker Luz at the bakery counts her loaves
/// to twenty and buys the learner's wood (<c>quiero vender madera</c>, then <c>vendo madera</c>: the new intent <c>offer_item</c> and the host's <c>buy</c>); the farmer
/// León at the field shows his fifteen apples (<c>¿cuánto es todo?</c>, <c>es muy caro</c>, <c>la manzana es más barata que el pan</c>, <c>¿puedo pagar con fruta?</c>,
/// <c>mañana venderé fruta</c>) and sells three; the shopkeeper at the stall hears the report (<c>compré tres manzanas</c>). The market told in the three times, the review
/// of the series. A learner arrives with no coins (V14, V17): the coins of the purchase are the wood's, sold to Luz.
/// </summary>
public sealed class MarketLessonTests(ITestOutputHelper output)
{
    private const string LessonId = "el-mercado";

    // The wood a learner collects at the trees for the sale (the step asks for three or more: León's apples cost a coin each).
    private const int Wood = 3;

    private static LessonBook Book => TestGame.NewSessionWithShippedLessons().Lessons.Book;

    // Mañana en el pueblo done in session 9 (El mercado comes after it), then session 10 at the bakery: the baker's lesson starts by itself. A learner who followed
    // sessions 4 to 9 has no coins, the farmer's three apples, the two loaves of session 5 and the axe.
    private static VoxelSession SessionTenAtBakery()
    {
        var session = TomorrowLessonTests.AtStep("(after the last step)"); // no step has this id: Mañana en el pueblo runs to its end
        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf("manana-en-el-pueblo"));
        session.EndTalk();
        session.PlaySession = 10;
        Stage.TeleportToNpc(session, "baker");
        _ = session.OpenTalk("baker");
        return session;
    }

    // The lesson as a learner who gets everything right does it, up to the step with this id.
    internal static VoxelSession AtStep(string stepId) => Through(SessionTenAtBakery(), stepId);

    private static VoxelSession Through(VoxelSession session, string stepId, List<LessonDriver.Done>? done = null)
    {
        while (session.Lessons.Current is { } step && step.Id != stepId)
        {
            if (Walk(session, step) is { } d)
            {
                done?.Add(d);
            }
        }

        return session;
    }

    // One step: a talk the step waits for (the walks back to the baker, to the farmer and to the stall) is opened at that NPC; the wood is collected at the trees (three
    // pieces, as the axe gathers them); the apples are bought with the line the step names; every other step is the driver's.
    private static LessonDriver.Done? Walk(VoxelSession session, LessonStep step)
    {
        if (step is { Type: LessonStepType.Do, Event: WorldEvents.TalkOpened, At: { } npc })
        {
            session.EndTalk();
            Stage.TeleportToNpc(session, npc);
            _ = session.OpenTalk(npc);
            return null;
        }

        if (step is { Type: LessonStepType.Do, Event: WorldEvents.ItemPickedUp })
        {
            session.EndTalk();
            _ = session.Simulation.GrantItems(ItemId.Wood, Wood);
            return LessonDriver.DoStep(session);
        }

        if (step is { Type: LessonStepType.Do, Event: WorldEvents.ItemGiven })
        {
            var turn = session.Say("Quiero tres manzanas"); // the purchase itself: the do step waits for the apples León gives
            return new LessonDriver.Done(step.Id, "do", "Quiero tres manzanas", StepOutcome.Done, turn.GuideLines, turn);
        }

        return LessonDriver.DoStep(session);
    }

    private static LessonEvent Done(VoxelSession session, string stepId) =>
        Assert.Single(session.Events.OfType<LessonEvent>(), e => e.Kind == LessonEventKind.StepDone && e.StepId == stepId);

    // ---- The lesson

    [Fact]
    public void ElMercadoIsTheBakersA2LessonOfSessionTenAfterMananaEnElPuebloWithTheFarmerAndTheShopkeeper()
    {
        var lesson = Book.Find(LessonId)!;

        Assert.Equal(("baker", 10, true, "manana-en-el-pueblo", LessonLevels.A2), (lesson.Npc, lesson.Session, lesson.AutoStart, lesson.After, lesson.Level));
        Assert.StartsWith("Session 10", lesson.Objective, StringComparison.Ordinal);
        Assert.Contains("bakery", lesson.Objective, StringComparison.Ordinal);
        Assert.Contains("El mercado", lesson.Objective, StringComparison.Ordinal);
        Assert.Equal("El mercado", lesson.ShortTitle);
        Assert.Equal(["baker", "farmer", "shopkeeper"], lesson.Steps.Select(lesson.NpcOf).Distinct());
        Assert.Same(lesson, Book.Lessons[^1]); // the series' last lesson
        Assert.Equal(10, SessionSkip.LastSession(Book));

        // Nine phrases the learner says and two review steps (sessions 9 and 8 at the start, sessions 5 and 7 at the stall at the end).
        Assert.Equal(
            ["tienes-veinte-panes", "quiero-vender-madera", "vendo-madera", "cuanto-es-todo", "es-muy-caro", "mas-barato-que", "puedo-pagar-con-fruta", "manana-vendere-fruta", "compre-tres-manzanas"],
            lesson.TaughtPhrases);
        Assert.Equal(
            [["manana-comprare-pan", "mar-pesco-en-el-rio"], ["hay-pan", "es-grande"]],
            lesson.Steps.Where(static s => s.Type == LessonStepType.Review).Select(static s => s.Candidates!.Select(static c => c.Phrase).ToArray()));
        Assert.Equal(LessonStepType.Recap, lesson.Steps[^1].Type);
        Assert.InRange(lesson.Steps.Count, 12, 26);
        foreach (var step in lesson.Steps)
        {
            if (step.Line is { } line)
            {
                Assert.Equal(lesson.NpcOf(step), line.Speaker); // each line in its NPC's own voice (V01's rule)
            }

            foreach (var candidate in step.Candidates ?? [])
            {
                Assert.Equal(lesson.NpcOf(step), candidate.Line.Speaker);
            }
        }

        // The heard rows are the lines of its steps, or the NPC's answers to the learner's questions (the total and the payment, León's own pools).
        var sheet = TestGame.Data.Phrases;
        foreach (var id in new[] { "once-a-quince", "dieciseis-a-veinte", "cuantos-panes-tengo", "te-pago-una-moneda", "tengo-quince-manzanas", "es-caro-o-barato", "que-es-mas-barato", "que-venderas-manana", "que-compraste-en-el-mercado" })
        {
            var row = sheet.Find(id)!;
            Assert.Equal("hears", row.Says);
            Assert.Contains(lesson.Steps, s => s.Line is { } line && row.AudioLineIds.Contains(line.LineId, StringComparer.Ordinal));
        }

        Assert.Equal(["farmer_total.1"], sheet.Find("quince-monedas")!.AudioLineIds);
        Assert.Equal(["farmer_pay.1"], sheet.Find("te-compro-fruta")!.AudioLineIds);

        // The do steps: the trees, back to the bakery, the field, the purchase, the stall. No order: the sale and the purchase are the session's exchange.
        var walks = lesson.Steps.Where(static s => s.Type == LessonStepType.Do).ToList();
        Assert.Equal(
            [
                (null, WorldEvents.ItemPickedUp, null, "madera"),
                ("npc.baker", WorldEvents.TalkOpened, "baker", null),
                ("npc.farmer", WorldEvents.TalkOpened, "farmer", null),
                (null, WorldEvents.ItemGiven, "farmer", "manzana, request"),
                ("stall", WorldEvents.TalkOpened, "shopkeeper", null),
            ],
            walks.Select(static s => ((string?)s.PointPlace, s.Event!, s.At, s.Detail)));
        Assert.Equal("árbol", walks[0].Point);
        Assert.All(walks, static s => Assert.False(string.IsNullOrWhiteSpace(s.Hud), s.Id));
        Assert.DoesNotContain(TestGame.Data.Objectives.Objectives, static o => o.Lesson == LessonId);

        // The numbers 11 to 20 are heard in Luz's count and León's table and said in a quantity.
        var steps = lesson.Steps.ToList();
        Assert.Equal("Once, doce, trece, catorce, quince.", steps.Single(static s => s.Id == "k1-fifteen").Line!.Text);
        Assert.Equal("Dieciséis, diecisiete, dieciocho, diecinueve, veinte.", steps.Single(static s => s.Id == "k1-twenty").Line!.Text);
        Assert.Equal("Tienes veinte panes.", sheet.Find("tienes-veinte-panes")!.Spanish);
        Assert.Contains("quince manzanas", steps.Single(static s => s.Id == "k4-all").Line!.Text, StringComparison.Ordinal);
        Assert.Contains("venderé fruta", steps.Single(static s => s.Id == "k8-table").English, StringComparison.Ordinal);
        Assert.Contains("every lesson is done", steps.Single(static s => s.Id == "k8-recap").English, StringComparison.Ordinal);
    }

    [Fact]
    public void ItsStepsDeclareTheirPersonAndTenseAndItsVerbsAreTheFormsTaughtAndHeard()
    {
        var lesson = Book.Find(LessonId)!;
        var expected = new Dictionary<string, (string? Person, string? Tense)>
        {
            ["tienes-veinte-panes"] = ("2sg", "pres"),
            ["quiero-vender-madera"] = ("1sg", "pres"),
            ["vendo-madera"] = (null, null), // said at Luz's open prompt, where it sells
            ["cuanto-es-todo"] = (null, null),
            ["es-muy-caro"] = ("3sg", null),
            ["mas-barato-que"] = ("3sg", null),
            ["puedo-pagar-con-fruta"] = (null, null),
            ["manana-vendere-fruta"] = ("1sg", TaughtVerbForm.SimpleFuture),
            ["compre-tres-manzanas"] = ("1sg", "pret"),
        };

        var steps = lesson.Steps.Where(static s => s.Type is LessonStepType.Answer or LessonStepType.Repeat).ToList();
        Assert.Equal(expected.Count, steps.Count);
        foreach (var step in steps)
        {
            Assert.Equal(expected[step.Phrase!], (step.Person, step.Tense));
            Assert.Equal(step.Tense is not null, step.TenseRetry is not null);
        }

        var future = steps.Single(static s => s.Tense == TaughtVerbForm.SimpleFuture);
        Assert.StartsWith("That is right too. Here we practise “venderé”", future.FutureNote, StringComparison.Ordinal);

        // The forms it declares for the book, each declared by one lesson only (VerbLessonTests): vendo and puedo now (pago heard), voy a vender and venderé, venderás heard.
        // The lexicon has every one; compré and quiero belong to earlier lessons.
        var morphology = TestGame.Bundle.SpanishMorphology();
        Assert.Equal(
            ["vendo", "puedo", "pago", "voy a vender", "venderé", "venderás"],
            lesson.TaughtVerbs.Select(v => v.Text(morphology, TestGame.Data.Words.NearFuture)));
    }

    [Fact]
    public void ItIsAnA2LessonAndTheValidatorPassesIt()
    {
        // Its words are A1 but cada (A2, Luz's price for each piece of wood); it is a2 for its grammar too: the simple future of venderé.
        Assert.Empty(GameDataValidator.Validate(TestGame.Data));
        Assert.Equal(LessonLevels.A2, Book.Find(LessonId)!.Level);
    }

    [Fact]
    public void EachStepsPhraseIsUnderstoodAtItsStepWithNoErrorAndEndsIt()
    {
        var lesson = Book.Find(LessonId)!;
        var sheet = TestGame.Data.Phrases;
        var session = SessionTenAtBakery();
        foreach (var step in lesson.Steps.Where(static s => s.Phrase is not null && s.Type != LessonStepType.Review))
        {
            var row = sheet.Find(step.Phrase!)!;
            Assert.Equal(row.Prompt, step.Prompt);
            Assert.Contains(row.Intent!, TestGame.Bundle.Prompts[step.Prompt!].AllowedIntentIds);
            session = Through(session, step.Id);

            var turn = session.Say(row.Spanish);

            Assert.True(turn.Communicated, row.Spanish);
            Assert.Equal(row.Intent, turn.Intent);
            Assert.Empty(turn.Errors);
            Assert.NotEqual(step.Id, session.Lessons.Current?.Id); // said at its step, it ends it
        }
    }

    [Fact]
    public void TheLessonIsNotOfferedBeforeMananaEnElPuebloIsDoneNorBeforeSessionTen()
    {
        var early = TomorrowLessonTests.AtStep("(after the last step)");
        early.EndTalk();
        Assert.Equal(9, early.PlaySession);
        Assert.NotEqual(LessonId, early.Lessons.OfferFor("baker")?.LessonId); // Mañana en el pueblo done, but it is still session 9

        var skipped = TestGame.NewSessionWithShippedLessons();
        skipped.PlaySession = 10; // session 10 reached without session 9's lesson: El mercado waits for it (after)
        Assert.NotEqual(LessonId, skipped.Lessons.OfferFor("baker")?.LessonId);

        var session = SessionTenAtBakery();
        Assert.Equal(LessonId, session.Lessons.Active!.Id);
        Assert.True(session.Lessons.Engaged);
    }

    [Fact]
    public void TheLessonRunsFromTheBakeryToTheFieldAndTheStallAndTheApplesArePaidWithTheWoodsCoins()
    {
        var session = SessionTenAtBakery();
        var bag = session.Simulation.Inventory;
        Assert.Equal((0, 3, 2, 1), (bag.Count(ItemId.Coin), bag.Count(ItemId.Apple), bag.Count(ItemId.Bread), bag.Count(ItemId.Axe))); // no coins (V14, V17)
        var earlier = session.Events.Count;

        var done = new List<LessonDriver.Done>();
        while (session.Lessons.StatusOf(LessonId) == LessonStatus.InProgress)
        {
            var step = session.Lessons.Current!;
            if (step.Id == "k3-wood")
            {
                Assert.Equal("Collect wood (madera) to sell to Luz", session.Lessons.HudLine);
            }

            if (step.Id == "k4-walk")
            {
                Assert.Equal(Wood, bag.Count(ItemId.Coin)); // the wood sold to Luz: a coin a piece
            }

            if (Walk(session, step) is { } d)
            {
                done.Add(d);
            }
        }

        foreach (var d in done)
        {
            output.WriteLine($"{d.Id,-12} {d.Type,-8} {d.Sent,-40} {d.Turn?.Intent,-16} {d.Outcome} {string.Join(" / ", d.Turn?.FollowUp ?? [])}");
        }

        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf(LessonId));
        Assert.Equal(10, session.Lessons.Record.Single(static r => r.LessonId == LessonId).CompletedSession);
        Assert.All(done, static d => Assert.Equal(StepOutcome.Done, d.Outcome));

        // The plan said with no wood in the bag: Luz says so, and nothing changes hands; the sale, back with the wood, pays a coin a piece.
        var plan = done.Single(static d => d.Id == "k2-sell").Turn!;
        Assert.Equal(("offer_item", true), (plan.Intent, plan.Communicated));
        Assert.Equal(["No tienes madera."], plan.FollowUp);
        var sale = done.Single(static d => d.Id == "k3-sell").Turn!;
        Assert.Equal(("offer_item", "baker"), (sale.Intent, sale.Npc));
        Assert.Contains(session.Events.Skip(earlier).OfType<WorldEvent>(), static e => e is { Name: WorldEvents.ItemTaken, Npc: "baker", Detail: "madera, request" });
        Assert.Contains(session.Events.Skip(earlier).OfType<WorldEvent>(), static e => e is { Name: WorldEvents.ItemGiven, Npc: "baker", Detail: "moneda, request" });

        // León's answers: the total of his table and the payment, as the world works.
        Assert.Equal(["Quince manzanas, quince monedas."], done.Single(static d => d.Id == "k4-all").Turn!.Replies);
        Assert.Equal(["Aquí se paga con monedas. Pero yo te compro fruta."], done.Single(static d => d.Id == "k5-fruit").Turn!.Replies);

        // The three times of the market, and the reviews.
        var turns = done.Where(static d => d.Turn is not null && d.Type is "answer" or "repeat").ToList();
        Assert.All(turns, static d =>
        {
            Assert.True(d.Turn!.Communicated, d.Id);
            Assert.Empty(d.Turn.Errors);
        });
        Assert.Equal(["baker", "baker", "baker", "farmer", "farmer", "farmer", "farmer", "farmer", "shopkeeper"], turns.Select(static d => d.Turn!.Npc));
        var reviews = done.Where(static d => d.Type == "review").ToList();
        Assert.Equal(("k0-review", "baker", true), (reviews[0].Id, reviews[0].Turn!.Npc, reviews[0].Turn!.Communicated));
        Assert.Equal(("k8-review", "shopkeeper", true), (reviews[1].Id, reviews[1].Turn!.Npc, reviews[1].Turn!.Communicated));

        // The coin path: no coins in; three wood sold for three coins; three apples bought for them. Out: no coins, six apples, two loaves and the axe.
        Assert.Equal((0, 6, 2, 1, 0), (bag.Count(ItemId.Coin), bag.Count(ItemId.Apple), bag.Count(ItemId.Bread), bag.Count(ItemId.Axe), bag.Count(ItemId.Wood)));
        Assert.Contains(session.Events.Skip(earlier).OfType<WorldEvent>(), static e => e is { Name: WorldEvents.ItemGiven, Npc: "farmer", Detail: "manzana, request" });
    }

    [Fact]
    public void FromASkipToSessionTenALearnerWithNoCoinsFinishesTheLesson()
    {
        // --skip-to-session 10: a new game where a learner who did sessions 1 to 9 stands, with no coins (V14, V17). The objective names the bakery and the beacon is on Luz.
        var session = TestGame.NewSessionWithShippedLessons();
        SessionSkip.Apply(session, 10);
        Assert.Equal((LessonId, "baker"), (session.Lessons.NewLesson?.Id, session.Pointer?.Point));
        Assert.Equal(Book.Find(LessonId)!.Objective, string.Concat(session.ObjectiveHud().Select(static p => p.Text)));
        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.Coin));
        Stage.TeleportToNpc(session, "baker");
        _ = session.OpenTalk("baker");
        Assert.Equal(LessonId, session.Lessons.Active!.Id);

        _ = Through(session, "(after the last step)");

        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf(LessonId));
        Assert.Equal((0, 6), (session.Simulation.Inventory.Count(ItemId.Coin), session.Simulation.Inventory.Count(ItemId.Apple)));
        Assert.Null(session.NextLessonSession); // no later session has a lesson: the series is done
    }

    // ---- The tenses at their steps (V16's machinery, used as it is)

    [Fact]
    public void TheNearFutureAtTheFutStepIsCreditedWithItsNote()
    {
        var session = AtStep("k5-tomorrow");
        var note = session.Lessons.Book.Find(LessonId)!.Steps.Single(static s => s.Id == "k5-tomorrow").FutureNote;

        var turn = session.Say("mañana voy a vender fruta");

        Assert.True(turn.Communicated);
        Assert.Empty(turn.Errors);
        Assert.Equal("near future for a plan, credited", Done(session, "k5-tomorrow").Detail);
        Assert.Equal(note, session.Lessons.View!.Hint);
    }

    [Theory]
    [InlineData("k5-tomorrow", "vendí fruta", "wrong tense: said pret, expected fut", "“venderé”")] // right Spanish with no time word: the core says nothing of the tense
    [InlineData("k5-tomorrow", "vendo fruta", "wrong tense: said pres, expected fut", "“venderé”")]
    [InlineData("k7-bought", "compro tres manzanas", "wrong tense: said pres, expected pret", "“compré”")]
    [InlineData("k1-loaves", "tengo veinte panes", "wrong person: said 1sg, expected 2sg", "“tienes”")]
    public void ARightPhraseInAnotherTenseOrPersonIsAMissWithTheStepsRetry(string stepId, string said, string reason, string hint)
    {
        var session = AtStep(stepId);
        var attempts = session.Lessons.Attempts;

        var turn = session.Say(said);

        Assert.True(turn.Communicated, said);
        Assert.Equal(stepId, session.Lessons.Current!.Id); // a miss: the step stays
        Assert.Equal(attempts + 1, session.Lessons.Attempts);
        Assert.Contains(hint, session.Lessons.View!.Hint, StringComparison.Ordinal);
        Assert.Contains(session.Events.OfType<LessonEvent>(), e => e.Kind == LessonEventKind.Retry && e.StepId == stepId && e.Detail!.StartsWith(reason, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("k5-tomorrow", "mañana vendí fruta", "conj.tense", "voy a vender")] // a future marker with a past verb (M49)
    [InlineData("k5-tomorrow", "yo venderás fruta", "conj.person", "venderé")]
    [InlineData("k7-bought", "compré tres manzana", "noun.number", "manzanas")]
    [InlineData("k4-dear", "la manzana es muy caro", "gender.agree", "cara")] // M62: a predicate adjective agrees with its subject past muy
    [InlineData("k5-cheaper", "la manzana es más barato que el pan", "gender.agree", "barata")] // and in a comparison
    [InlineData("k2-sell", "yo quieres vender madera", "conj.person", "quiero")]
    public void ASlipIsTheCoresAndTheStepGoesOn(string stepId, string said, string tag, string expected)
    {
        var session = AtStep(stepId);
        var turn = session.Say(said);

        Assert.True(turn.Communicated, said);
        Assert.Equal(expected, Assert.Single(turn.Errors, e => e.Tag == tag).Expected);
        Assert.NotEqual(stepId, session.Lessons.Current?.Id); // a slip: the step goes on
        Assert.Equal("with a slip", Done(session, stepId).Detail);
    }

    // ---- The book

    [Fact]
    public void TheBookShowsVenderAndPoderAndTheLessonsPageSaysTheSeriesIsDone()
    {
        // From --skip-to-session 10 (the earlier lessons counted as completed in their sessions), the market played through: every lesson is done.
        var session = TestGame.NewSessionWithShippedLessons();
        SessionSkip.Apply(session, 10);
        Stage.TeleportToNpc(session, "baker");
        _ = session.OpenTalk("baker");
        _ = Through(session, "(after the last step)");
        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf(LessonId));

        var vender = BookPages.VerbTable(session, "vender")!;
        Assert.Equal(new ConjugationCell("vendo", CellState.Used, 1), At(vender, "pres", "1", "sg")); // said at Luz's, the sale
        Assert.Equal(new ConjugationCell("venderé", CellState.Used, 1), At(vender, TaughtVerbForm.SimpleFuture, "1", "sg"));
        Assert.Equal(new ConjugationCell("venderás", CellState.Taught, 0), At(vender, TaughtVerbForm.SimpleFuture, "2", "sg"));
        Assert.Equal("voy a vender", At(vender, TaughtVerbForm.NearFuture, "1", "sg").Text);
        Assert.Equal(new ConjugationCell("puedo", CellState.Used, 1), At(BookPages.VerbTable(session, "poder")!, "pres", "1", "sg"));
        Assert.Equal("pago", At(BookPages.VerbTable(session, "pagar")!, "pres", "1", "sg").Text);

        // Every lesson is done: the page says so first, and no lesson says next: to a lesson that does not exist.
        var lines = BookPages.Lessons(session.Locale, session.Lessons.Record, session.Phrases, null, BookPages.Waits(session.Lessons));
        Assert.Equal("Every lesson of the village is done, all 10: the series ends here. Practise any of them again with its button below.", lines[0].Text);
        Assert.Equal(BookTone.Gold, lines[0].Tone);
        Assert.DoesNotContain(lines, static l => l.Text.StartsWith("next:", StringComparison.Ordinal) || l.Text.StartsWith("open:", StringComparison.Ordinal));
        Assert.Equal(10, session.Lessons.Record.Count(static r => r.Status == LessonStatus.Completed));
    }

    private static ConjugationCell At(ConjugationTable table, string tense, string person, string number)
    {
        var row = table.Rows.Single(r => r.Person == person && r.Number == number);
        var column = table.Columns.ToList().FindIndex(c => c.Tense == tense);
        Assert.True(column >= 0, $"the {tense} column of {table.Lemma} is not shown");
        return row.Cells[column];
    }

    [Fact]
    public void TheBakerTheFarmerAndTheShopkeeperSpeakTheLessonsLinesWithTheirOwnVoices()
    {
        var data = TestGame.Data;
        Assert.Equal(
            ("baker", "farmer", "shopkeeper", "farmer", "baker"),
            (SpeechSpeakers.OfPool("guide.lesson.baker.fifteen", data.Npcs), SpeechSpeakers.OfPool("guide.lesson.farmer.market", data.Npcs),
             SpeechSpeakers.OfPool("guide.lesson.shop.market", data.Npcs), SpeechSpeakers.OfPool("farmer_total", data.Npcs), SpeechSpeakers.OfPool("baker_buy", data.Npcs)));
        Assert.Equal(NpcEffect.BuyAt(ItemId.Wood, 1), data.HostEvents.Of("baker", "baker.buy_wood"));
    }
}
