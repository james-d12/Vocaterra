using Vocaterra.Game.Core.Activities;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Activities;

/// <summary>
/// Bread with Luz as the bundle ships it (W10, activities plan section 6): the six-action procedure through the runner from rung 1 to done, the stall's flour (sold for a coin
/// through <c>instead</c> of the bottle, with its misses answered) and its bread bought for a coin, Luz's offer now that the procedure exists, the optional class
/// <c>en-la-panaderia</c> with typed lines, the grant open during the class's bake, and sessions 1 to 10 as they were.
/// </summary>
public sealed class BreadPilotTests
{
    private static readonly string[] Rows =
        ["pon-la-harina", "echa-el-agua", "mezcla", "pon-la-madera", "pon-la-masa", "saca-el-pan", "haz-la-masa", "haz-el-pan-horno", "haz-pan"];

    private static GameData Data => TestGame.BreadData;

    // ---- The procedure

    [Fact]
    public void TheBreadProcedureLoadsAndValidatesWithNineDoesRowsRecordedForLuz()
    {
        var pan = Data.Procedures.Find("pan")!;

        Assert.Empty(GameDataValidator.ActivityProblems(Data));
        Assert.Empty(GameDataValidator.Validate(Data));
        Assert.Equal(("horno", "baker", "a2"), (pan.Station, pan.Teacher, pan.Level));
        Assert.Equal(["harina", "agua", "mezclar", "madera", "masa", "sacar"], pan.Steps.Select(static s => s.Id));
        Assert.Equal(["la-masa", "el-horno"], pan.Chunks.Select(static c => c.Id));
        Assert.Equal(Rows.OrderBy(static r => r, StringComparer.Ordinal), pan.Rows.Distinct().OrderBy(static r => r, StringComparer.Ordinal));
        Assert.Equal(["flour", "water-bottle", "wood"], pan.Uses.Select(static u => u.Item).Order(StringComparer.Ordinal));
        foreach (var row in Rows)
        {
            var phrase = Data.Phrases.Find(row)!;
            Assert.True(phrase.IsInstruction, row);
            Assert.All(phrase.AudioLineIds, id => Assert.StartsWith("guide.lesson.baker.", id, StringComparison.Ordinal)); // Luz's voice
            Assert.Contains(Data.Speech, line => line.LineId == phrase.AudioLineIds[0]);
        }
    }

    [Fact]
    public void ABakeGoesFromRungOneToDoneMakesThreeLoavesAndRaisesProcedureDone()
    {
        var pan = Data.Procedures.Find("pan")!;
        var phrases = new PhraseProgress(Data.Phrases, 1, Rows);
        var runner = new ProcedureRunner(Data.Procedures, Data.Stations, TestGame.Nouns, phrases, CreditRules.Default, 7);
        var bag = new Inventory(TestItems.Catalogue);
        _ = bag.Add(TestItems.Flour, 1);
        _ = bag.Add(ItemId.WaterBottle, 1);
        _ = bag.Add(ItemId.Wood, 1);

        Assert.Equal(StartOutcome.Started, runner.Start("pan", "horno", true));
        Assert.Equal(1, runner.View().Rung); // the first bake shows the English and the card
        ActResult? last = null;
        foreach (var id in runner.Order.ToList())
        {
            var step = pan.Steps.First(s => s.Id == id);
            last = runner.Act(step.ActUse, step.ActOn, step.Verb, bag, 1_000_000);
            Assert.Equal(ActOutcome.Right, last.Outcome);
        }

        Assert.Equal((ProcedureState.Done, 3), (last!.State, last.Made)); // two loaves and the bonus: six steps right the first time
        Assert.Equal((3, 0, 0, 1, 0), (bag.Count(TestItems.Bread), bag.Count(TestItems.Flour), bag.Count(ItemId.Wood), bag.Count(ItemId.Bottle), bag.Count(ItemId.WaterBottle)));
        Assert.Contains(runner.DrainEvents(), static e => e.Name == WorldEvents.ProcedureDone && e.Detail == "pan");
        Assert.All(pan.Rows.Take(6), row => Assert.Equal(2, (int)phrases.LevelOf(row) + 1)); // every row moves up a rung, with no comprehension credit yet
    }

    [Fact]
    public void TheDoughIsAThingTheMixingMakesAndAWrongActChangesNothing()
    {
        var phrases = new PhraseProgress(Data.Phrases, 1, Rows);
        var runner = new ProcedureRunner(Data.Procedures, Data.Stations, TestGame.Nouns, phrases, CreditRules.Default, 7);
        var bag = new Inventory(TestItems.Catalogue);
        _ = bag.Add(TestItems.Flour, 1);
        _ = bag.Add(ItemId.WaterBottle, 1);
        _ = bag.Add(ItemId.Wood, 1);
        _ = runner.Start("pan", "horno", true);

        Assert.Equal(ActOutcome.WrongThing, runner.Act("harina", "horno", "poner", bag, 1_000_000).Outcome); // the flour on the oven
        Assert.Equal(ActOutcome.OutOfOrder, runner.Act("cuchara", "bol", "mezclar", bag, 1_000_000).Outcome); // a later step's act: Luz says «Todavía no.»
        Assert.Equal((1, 1, 1), (bag.Count(TestItems.Flour), bag.Count(ItemId.WaterBottle), bag.Count(ItemId.Wood))); // nothing used up
    }

    // ---- The stall

    // A session without lessons at an NPC's talk, with the bread in the bundle.
    private static VoxelSession At(string npc, params (ItemId Item, int Count)[] held)
    {
        var data = Data;
        var noLessons = LessonBook.Parse("{ \"schemaVersion\": 1, \"lessons\": [] }", data.Phrases, data.Speech, data.Bundle.Prompts, data.Locale, data.Npcs.Ids);
        var session = new VoxelSession(data, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Route(), lessons: noLessons) { Day = 10 };
        _ = session.TakeOrders(); // the orders are taken: this test is not about their offer (issue #54)
        foreach (var (item, count) in held)
        {
            Assert.Equal(0, session.Simulation.GrantItems(item, count));
        }

        Stage.TeleportToNpc(session, npc);
        _ = session.OpenTalkForClass(npc);
        return session;
    }

    private static int Held(VoxelSession session, ItemId item) => session.Simulation.Inventory.Count(item);

    [Fact]
    public void FlourIsSoldAtTheStallForOneCoinThroughInsteadOfTheBottle()
    {
        var session = At("shopkeeper", (ItemId.Coin, 3));

        var turn = session.Say("quiero harina");

        Assert.Equal((1, 2, 0), (Held(session, TestItems.Flour), Held(session, ItemId.Coin), Held(session, ItemId.Bottle)));
        Assert.Contains("Aquí tienes la harina.", turn.Replies.Concat(turn.FollowUp ?? []));
        Assert.DoesNotContain(turn.Replies.Concat(turn.FollowUp ?? []), static l => l.StartsWith("No tengo", StringComparison.Ordinal));
        _ = session.Say("quiero dos harinas");
        Assert.Equal((3, 0), (Held(session, TestItems.Flour), Held(session, ItemId.Coin))); // one coin each
    }

    [Fact]
    public void FlourWithNoCoinIsNotSoldAndTheStallSaysThePrice()
    {
        var session = At("shopkeeper");

        var turn = session.Say("quiero harina");

        Assert.Equal(0, Held(session, TestItems.Flour));
        var lines = turn.Replies.Concat(turn.FollowUp ?? []).ToList();
        Assert.Contains("La harina cuesta una moneda.", lines);
        Assert.DoesNotContain("Aquí tienes la harina.", lines); // the success line is not said for a sale the world cannot make
    }

    [Fact]
    public void FlourWithAFullBagIsNotSoldAndTheStallSaysTheBagIsFull()
    {
        var session = At("shopkeeper", (ItemId.Coin, 3));
        var bag = session.Simulation.Inventory;
        foreach (var item in TestItems.Catalogue.All.Where(static d => d.Id is not (ItemId.Coin or TestItems.Flour or TestItems.Bread)))
        {
            while (bag.Add(item.Id, item.MaxStack) == 0)
            {
            }
        }

        Assert.False(bag.CanAdd(TestItems.Flour, 1));
        var turn = session.Say("quiero harina");

        Assert.Equal((0, 3), (Held(session, TestItems.Flour), Held(session, ItemId.Coin)));
        var lines = turn.Replies.Concat(turn.FollowUp ?? []).ToList();
        Assert.Contains("Tu bolsa está llena.", lines);
        Assert.DoesNotContain("Aquí tienes la harina.", lines);
    }

    [Fact]
    public void FlourIsSoldByTheNameTheLearnerSaysAndTooFewCoinsBuyNone()
    {
        var session = At("shopkeeper", (ItemId.Coin, 2));

        var turn = session.Say("quiero tres harinas");

        Assert.Equal((0, 2), (Held(session, TestItems.Flour), Held(session, ItemId.Coin)));
        Assert.Contains("La harina cuesta una moneda.", turn.Replies.Concat(turn.FollowUp ?? []));
        _ = session.Say("quiero dos harinas");
        Assert.Equal((2, 0), (Held(session, TestItems.Flour), Held(session, ItemId.Coin)));
    }

    [Theory]
    [InlineData("quiero una espada")]
    [InlineData("quiero manzanas")]
    [InlineData("quiero madera")]
    public void AnItemTheStallNeitherGivesNorPricesIsAnsweredWontSellAndNothingChangesHands(string line)
    {
        var session = At("shopkeeper", (ItemId.Coin, 3));

        var turn = session.Say(line);

        var lines = turn.Replies.Concat(turn.FollowUp ?? []).ToList();
        Assert.Contains("No tengo eso. Tengo agua, pan y harina.", lines);
        Assert.Equal((3, 0, 0, 0), (Held(session, ItemId.Coin), Held(session, TestItems.Flour), Held(session, TestItems.Bread), Held(session, ItemId.Bottle)));
    }

    [Fact]
    public void WaterAndBreadStayFreeAtTheStall()
    {
        var water = At("shopkeeper");
        _ = water.Say("quiero agua");
        Assert.Equal((1, 0), (Held(water, ItemId.Bottle), Held(water, ItemId.Coin)));

        var bread = At("shopkeeper");
        _ = bread.Say("quiero pan");
        Assert.Equal((1, 0, 0), (Held(bread, TestItems.Bread), Held(bread, ItemId.Coin), Held(bread, ItemId.Bottle)));
    }

    [Fact]
    public void TheStallBuysBreadForOneCoinEachAndNothingElse()
    {
        var session = At("shopkeeper", (TestItems.Bread, 2));

        var turn = session.Say("vendo pan");

        Assert.Equal((0, 2), (Held(session, TestItems.Bread), Held(session, ItemId.Coin)));
        Assert.Contains("¡Gracias! Aquí tienes.", turn.Replies.Concat(turn.FollowUp ?? []));
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.ItemTaken && e.Npc == "shopkeeper" && e.Detail == "pan, request");

        var none = session.Say("vendo pan");
        Assert.Contains("No tienes pan.", none.Replies.Concat(none.FollowUp ?? []));

        var wood = At("shopkeeper", (ItemId.Wood, 2));
        var refused = wood.Say("vendo madera");
        Assert.Equal((2, 0), (Held(wood, ItemId.Wood), Held(wood, ItemId.Coin)));
        Assert.Contains("No, gracias. Solo compro pan.", refused.Replies.Concat(refused.FollowUp ?? []));
    }

    [Fact]
    public void TheStallsWaterAndBreadAreAsTheyWereAndItsAnswersNameTheFlour()
    {
        var session = At("shopkeeper");
        _ = session.Say("quiero agua, por favor");
        Assert.Equal(1, Held(session, ItemId.Bottle));
        var hay = At("shopkeeper").Say("¿hay pan?");
        Assert.Contains("Hay agua, pan y harina.", hay.Replies.Concat(hay.FollowUp ?? []));
    }

    // ---- Luz's offer and the class

    [Fact]
    public void LuzOffersToBakeNowThatTheProcedureExistsAndItCostsNothingToRefuse()
    {
        var data = Data;
        var noLessons = LessonBook.Parse("{ \"schemaVersion\": 1, \"lessons\": [] }", data.Phrases, data.Speech, data.Bundle.Prompts, data.Locale, data.Npcs.Ids);
        var session = new VoxelSession(data, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Route(), lessons: noLessons);
        _ = session.TakeOrders(); // the orders are taken: this test is not about their offer (issue #54)
        Stage.TeleportToNpc(session, "baker");

        var opening = session.OpenTalk("baker");

        Assert.Equal(["¿Quieres hacer pan?"], opening.Lines);
        Assert.Equal("offering_bake", session.Conversation.StateOf("baker"));
        Assert.Equal("idle", session.Say("No, gracias.").AgentState);

    }

    private static VoxelSession ClassSession()
    {
        var session = new VoxelSession(Data, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Route(), CreditRules.FullTyped);
        _ = session.TakeOrders(); // the orders are taken: this test is not about their offer (issue #54)
        SessionSkip.Apply(session, 10); // sessions 1 to 9 counted as done: ayer-y-manana is done and Luz is met, so the class is open
        session.Day = 11;
        Stage.TeleportToNpc(session, "baker");
        _ = session.OpenTalkForClass("baker");
        return session;
    }

    [Fact]
    public void TheClassIsOptionalAfterAyerYMananaAtLuzAndSessionsOneToTenAreAsTheyWere()
    {
        var book = Data.Lessons;
        var lesson = book.Find("en-la-panaderia")!;

        Assert.Equal(["first-words", "first-class", "personas", "ayer-y-manana", "el-pueblo", "numeros-y-pan", "gente-del-pueblo", "como-es", "ayer-en-el-pueblo", "manana-en-el-pueblo", "el-mercado", "en-la-panaderia"], book.Lessons.Select(static l => l.Id));
        Assert.Equal(("baker", 11, false, "ayer-y-manana", LessonLevels.A2), (lesson.Npc, lesson.Session, lesson.AutoStart, lesson.After, lesson.Level));
        Assert.Equal(["voy-a-hacer-pan", "hice-pan", "quiero-harina", "vendo-pan"], lesson.TaughtPhrases);
        Assert.Equal([1, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10], book.Lessons.Take(11).Select(static l => l.Session));
        Assert.DoesNotContain(book.Lessons.Take(11), static l => l.Steps.Any(s => s.Phrase is "voy-a-hacer-pan" or "hice-pan" or "quiero-harina" or "vendo-pan"));
        Assert.Equal(LessonStepType.Recap, lesson.Steps[^1].Type);
    }

    [Fact]
    public void TheClassRunsFromTheOvenToTheStallWithTypedLinesAndTheGrantIsOpenWhileItsBakeWaits()
    {
        var session = ClassSession();
        Assert.True(session.Lessons.Start("en-la-panaderia"));

        // The introduction, the plan said with voy a, and the two commands heard.
        while (session.Lessons.Current is { Id: not "k3-oven" } step)
        {
            var done = LessonDriver.DoStep(session);
            Assert.Equal(StepOutcome.Done, done.Outcome);
            if (step.Id == "k1-answer")
            {
                Assert.Equal("Voy a hacer pan", done.Sent);
            }
        }

        // The class's do step waits for the bake: the grant is open though today's was given, and it does not count for the day.
        Assert.Equal(WorldEvents.ProcedureDone, session.Lessons.Current!.Event);
        session.EndTalk();
        var closed = new ProceduresSave(1, ["pan"], [new ProcedureProgressSave("pan", 1, 1, [1], 3, false)], [], null, ["pan"]);
        session.RestoreProcedures(closed);
        Assert.Equal(StartOutcome.Started, session.StartProcedure("pan", "horno", true));
        Assert.True(session.PlanGrant()!.Open);
        Assert.Equal(["pan"], session.GrantsGivenToday);

        // The bake is done: she asks what the learner did.
        session.LogEvent(new WorldEvent(session.CurrentTick, session.StoredTime.UnixSeconds, WorldEvents.ProcedureDone, "baker", "pan"));
        Assert.Equal("k3-back", session.Lessons.Current!.Id);
        _ = session.Simulation.GrantItems(TestItems.Bread, 3);
        Stage.TeleportToNpc(session, "baker");
        _ = session.OpenTalkForClass("baker");
        Assert.Equal("k4-report", session.Lessons.Current!.Id);
        Assert.Equal(StepOutcome.Done, LessonDriver.DoStep(session).Outcome); // "Hice pan"

        // The stall: flour for a coin, then a loaf sold for a coin.
        Assert.Equal("k5-walk", session.Lessons.Current!.Id);
        session.EndTalk();
        _ = session.Simulation.GrantItems(ItemId.Coin, 1);
        Stage.TeleportToNpc(session, "shopkeeper");
        _ = session.OpenTalkForClass("shopkeeper");
        Assert.Equal("k5-flour", session.Lessons.Current!.Id);
        var flour = LessonDriver.DoStep(session);
        Assert.Equal("Quiero harina", flour.Sent);
        Assert.Equal(1, Held(session, TestItems.Flour));
        Assert.Equal("k5-sell", session.Lessons.Current!.Id);
        var sold = LessonDriver.DoStep(session);
        Assert.Equal("Vendo pan", sold.Sent);
        Assert.Equal(0, Held(session, TestItems.Bread));
        Assert.Equal(5, Held(session, ItemId.Coin)); // the five loaves (two from the skip, three baked) at a coin each

        while (session.Lessons.Engaged)
        {
            _ = LessonDriver.DoStep(session);
        }

        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf("en-la-panaderia"));
    }

    [Fact]
    public void WithNoClassTheGrantOfADayAlreadyGivenIsClosed()
    {
        var session = ClassSession();
        session.EndTalk();
        session.RestoreProcedures(new ProceduresSave(1, ["pan"], [new ProcedureProgressSave("pan", 1, 1, [1], 3, false)], [], null, ["pan"]));
        Assert.Equal(StartOutcome.Started, session.StartProcedure("pan", "horno", true));

        Assert.False(session.PlanGrant()!.Open); // the class is not engaged: the day's grant stays given
    }
}
