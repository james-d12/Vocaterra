using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Session;

/// <summary>
/// Issue #54: an order is a task the player takes on, not one that appears on meeting its NPC. The NPC offers it when the talk opens (<c>&lt;npc&gt;.offer_order</c>, a yes-or-no
/// question in its <c>offering_order</c> state); «sí» gives it (<c>accept_order</c>) and only then is it shown, counted and delivered; «no» puts it off until the next day; any
/// other line or walking away leaves the offer for the next talk. A class that tells the order gives it at its step (<c>lessonStep</c>) and is offered in its place.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class OrderOfferTests
{
    private const string MarOffers = "Tengo hambre. ¿Me traes tres frutas? Te doy dos monedas.";

    private const string MarThanks = "¡Gracias! Hay fruta en el campamento de Luna.";

    private const string LuzOffers = "¿Me traes cinco bloques de madera para el horno? Te pago cuatro monedas.";

    private static Objective Order(VoxelSession session, string id) => session.Objectives.Single(o => o.Id == id);

    private static IEnumerable<WorldEvent> WorldEventsOf(VoxelSession session, string name) => session.Events.OfType<WorldEvent>().Where(e => e.Name == name);

    // A session with no lessons (a class would be offered beside the order), on day 4.
    private static VoxelSession NewSession()
    {
        var session = new VoxelSession(TestGame.Data, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Route(), lessons: TestGame.NoLessons) { Day = 4 };
        Stage.TeleportToNpc(session, "fisher");
        return session;
    }

    [Fact]
    public void MeetingTheNpcOpensWithItsOfferAndSiGivesTheOrder()
    {
        var session = NewSession();
        var order = Order(session, "fruit-for-mar");

        var opening = session.OpenTalk("fisher");

        Assert.Equal([MarOffers], opening.Lines);
        Assert.Equal("fruit-for-mar", session.OrderOffered);
        Assert.Equal("fisher-order-offer", session.Conversation.PromptOf("fisher"));
        Assert.Equal(NpcHostEvents.OrderOfferState, session.Conversation.StateOf("fisher"));
        Assert.Contains("Mar asks you for a favour", session.RetryNote, StringComparison.Ordinal); // the English note: it is the player's choice
        Assert.False(session.Shown(order.Spec)); // met, but not taken on
        Assert.False(session.HasOrder("fruit-for-mar"));
        Assert.Contains(WorldEventsOf(session, WorldEvents.OrderOffered), static e => e.Npc == "fisher" && e.Detail == "fruit-for-mar");

        var yes = session.Say("Sí.");

        Assert.Equal(("confirm", true), (yes.Intent, yes.Communicated));
        Assert.Equal(["fisher.accept_order"], yes.HostEvents);
        Assert.Equal([MarThanks], yes.GuideLines);
        Assert.Null(session.OrderOffered);
        Assert.True(session.HasOrder("fruit-for-mar"));
        Assert.True(session.Shown(order.Spec));
        Assert.Equal("fisher-open", session.Conversation.PromptOf("fisher")); // back in his usual state
        Assert.Contains(WorldEventsOf(session, WorldEvents.OrderAccepted), static e => e.Npc == "fisher" && e.Detail == "fruit-for-mar, offer");
        var line = session.Locale.ObjectiveHudLine("fruit-for-mar");
        Assert.Contains(session.ObjectiveTracker(lessonStripShown: false).Lines, l => l.Pieces.Any(p => p.Text == line)); // the order's line is in the tracker now

        session.EndTalk();
        Assert.Equal(["¡Hola!"], session.OpenTalk("fisher").Lines); // taken: he offers it no more
        Assert.Null(session.OrderOffered);
    }

    [Fact]
    public void NoPutsTheOrderOffUntilTheNextDayAndCostsNothing()
    {
        var session = NewSession();
        _ = session.OpenTalk("fisher");

        var no = session.Say("No, gracias.");

        Assert.Equal("deny", no.Intent);
        Assert.Equal(["Está bien."], no.GuideLines);
        Assert.False(session.HasOrder("fruit-for-mar"));
        Assert.Equal(["fruit-for-mar"], session.OrdersDeclinedToday);
        Assert.Contains(WorldEventsOf(session, WorldEvents.OrderDeclined), static e => e.Npc == "fisher" && e.Detail == "fruit-for-mar");
        Assert.Equal("fisher-open", session.Conversation.PromptOf("fisher"));

        session.EndTalk();
        Assert.Equal(["¡Hola!"], session.OpenTalk("fisher").Lines); // not asked again today
        Assert.Null(session.OrderOffered);
        session.EndTalk();

        session.ApplyNight(collapse: false);
        Assert.Empty(session.OrdersDeclinedToday);
        Assert.Equal([MarOffers], session.OpenTalk("fisher").Lines); // the next day he asks again
        _ = session.AcceptOrder();
        Assert.True(session.HasOrder("fruit-for-mar"));
    }

    [Fact]
    public void WalkingAwayOrAnotherLineLeavesTheOfferForTheNextTalk()
    {
        var session = NewSession();
        _ = session.OpenTalk("fisher");
        session.EndTalk(); // no answer

        Assert.Equal([MarOffers], session.OpenTalk("fisher").Lines); // his state left offering is reset, and he asks again
        Assert.Equal("fruit-for-mar", session.OrderOffered);

        var asked = session.Say("¿Dónde está la panadería?"); // another line: he answers it as at his open prompt, and the offer is withdrawn

        Assert.Equal("ask_directions", asked.Intent);
        Assert.Equal("bakery", session.Pointer!.Point);
        Assert.Null(session.OrderOffered);
        Assert.False(session.HasOrder("fruit-for-mar"));
        Assert.Empty(session.OrdersDeclinedToday);

        var late = session.Say("Sí."); // a later «sí» is not an answer to an offer withdrawn
        Assert.DoesNotContain("fisher.accept_order", late.HostEvents);
        Assert.False(session.HasOrder("fruit-for-mar"));

        session.EndTalk();
        Assert.Equal([MarOffers], session.OpenTalk("fisher").Lines);
    }

    [Fact]
    public void HelpOrNoSeRepeatsTheOfferAndItStands()
    {
        var session = NewSession();
        _ = session.OpenTalk("fisher");

        var help = session.Say("No sé.");

        Assert.Equal([MarOffers], help.GuideLines);
        Assert.Equal("fruit-for-mar", session.OrderOffered);
        Assert.Equal(NpcHostEvents.OrderOfferState, session.Conversation.StateOf("fisher"));
        Assert.NotNull(session.AcceptOrder());
        Assert.True(session.HasOrder("fruit-for-mar"));
    }

    [Fact]
    public void GoodsCarriedBeforeTheOrderIsTakenAreNotTakenAndTheNextTalkAsksForThem()
    {
        var session = NewSession();
        _ = session.Simulation.GrantItems(ItemId.Berry, 3);

        var opening = session.OpenTalk("fisher");

        Assert.Equal([MarOffers], opening.Lines); // the offer first: no goods change hands for an order not given
        Assert.False(opening.Delivered);
        Assert.False(session.HandOverOpen);
        _ = session.AcceptOrder();
        Assert.Equal(3, session.Simulation.Inventory.Count(ItemId.Berry));
        Assert.False(Order(session, "fruit-for-mar").Complete);
        session.EndTalk();

        Assert.Equal(["¡Hola! ¿Qué tienes para mí?"], session.OpenTalk("fisher").Lines);
        Assert.True(session.HandOverOpen);
        Assert.Equal(["¡Gracias! Toma dos monedas."], session.HandOverGoods()!.FollowUp);
        Assert.True(Order(session, "fruit-for-mar").Complete);
    }

    [Fact]
    public void AClassAtTheSameNpcIsOfferedOnceTheOrdersOfferIsAnsweredAndTheSameSiDoesNotTakeIt()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        SessionSkip.Apply(session, 4); // El pueblo, Mar's class, is open; his order is not its
        Stage.TeleportToNpc(session, "fisher");

        Assert.Equal([MarOffers], session.OpenTalk("fisher").Lines);
        Assert.Null(session.Lessons.OfferStandingAt("fisher")); // held back while he waits for the answer
        var yes = session.Say("Sí.");

        Assert.True(session.HasOrder("fruit-for-mar"));
        Assert.Equal("el-pueblo", session.Lessons.OfferStandingAt("fisher")); // offered now, in the same talk
        Assert.False(session.Lessons.Engaged); // the «sí» was the order's
        Assert.Contains(session.Lessons.TakeLines(), static line => line.Contains("clase", StringComparison.Ordinal));
        Assert.Equal(["fisher.accept_order"], yes.HostEvents);

        _ = session.Say("Sí.");
        Assert.Equal("el-pueblo", session.Lessons.Active?.Id);
        Assert.True(session.Lessons.Engaged);
    }

    [Fact]
    public void AClassThatTellsTheOrderIsOfferedInItsPlaceAndANoToTheClassLetsTheOrderBeOfferedNext()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        SessionSkip.Apply(session, 5); // Números y pan, Luz's class, is open, and it tells her order (lessonStep n3-oven)
        Stage.TeleportToNpc(session, "baker");

        var opening = session.OpenTalk("baker");

        Assert.DoesNotContain(LuzOffers, opening.Lines);
        Assert.Null(session.OrderOffered);
        Assert.Equal("numeros-y-pan", session.Lessons.OfferStandingAt("baker"));

        _ = session.Say("No, gracias."); // no class today
        session.EndTalk();

        Assert.Equal([LuzOffers], session.OpenTalk("baker").Lines); // the order is hers to offer now
        _ = session.AcceptOrder();
        Assert.True(session.HasOrder("oven-wood"));
    }

    [Fact]
    public void TheOrdersTakenSurviveASaveAndALoad()
    {
        var session = NewSession();
        _ = session.OpenTalk("fisher");
        _ = session.AcceptOrder();
        session.EndTalk();

        var text = SaveGame.Save(session);
        Assert.Contains("\"givenOrders\":[\"fruit-for-mar\"]", text, StringComparison.Ordinal);
        var loaded = SaveGame.Load(text, TestGame.Data, static seed => TestGame.Route(seed), bumpSession: false);

        Assert.Equal(["fruit-for-mar"], loaded.GivenOrders);
        Assert.True(loaded.Shown(Order(loaded, "fruit-for-mar").Spec));
        Assert.Contains("\"givenOrders\":[\"fruit-for-mar\"]", SaveGame.Save(loaded), StringComparison.Ordinal);

        var none = SaveGame.Save(NewSession());
        Assert.DoesNotContain("givenOrders", none, StringComparison.Ordinal); // left out while none is taken
    }

    [Fact]
    public void ASaveLeftWhileTheNpcWaitedOnItsOfferLoadsAndTheNextTalkAsksAgain()
    {
        var session = NewSession();
        _ = session.OpenTalk("fisher");
        var text = SaveGame.Save(session); // his agent saved in its offering state
        var loaded = SaveGame.Load(text, TestGame.Data, static seed => TestGame.Route(seed), bumpSession: false);

        Assert.Equal(NpcHostEvents.OrderOfferState, loaded.Conversation.StateOf("fisher"));
        Assert.Equal([MarOffers], loaded.OpenTalk("fisher").Lines);
        Assert.Equal("fruit-for-mar", loaded.OrderOffered);
    }

    [Fact]
    public void TheSkipAidTakesOnTheOrdersItDeliversAndLeavesTheTargetSessionsToTheirOffers()
    {
        var session = TestGame.NewSessionWithShippedLessons();

        SessionSkip.Apply(session, 5);

        Assert.True(session.HasOrder("fruit-for-mar"));
        Assert.True(Order(session, "fruit-for-mar").Complete);
        Assert.Contains(WorldEventsOf(session, WorldEvents.OrderAccepted), static e => e.Detail == "fruit-for-mar, skip");
        Assert.False(session.HasOrder("oven-wood"));
    }

    [Fact]
    public void TheShippedOrdersAreOfferedByTheirNpcsAndThreeAreToldByTheirClasses()
    {
        var orders = TestGame.Data.Objectives.Objectives.Where(static o => o.IsOrder).ToDictionary(static o => o.Id);

        Assert.Equal(["axe-handle", "fruit-for-mar", "oven-wood", "water-for-plants"], orders.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(
            [("axe-handle", "gente-del-pueblo", "g4-offer"), ("fruit-for-mar", null, null), ("oven-wood", "numeros-y-pan", "n3-oven"), ("water-for-plants", "como-es", "c3-plan")],
            orders.Values.OrderBy(static o => o.Id, StringComparer.Ordinal).Select(static o => (o.Id, o.Lesson, o.LessonStep)));
        foreach (var npc in orders.Values.Select(static o => o.DeliverTo!))
        {
            Assert.Equal($"{npc}-order-offer", TestGame.Data.Npcs.Find(npc)!.Prompts[NpcHostEvents.OrderOfferState]);
        }

        Assert.Empty(GameDataValidator.Validate(TestGame.Data));
    }

    [Fact]
    public void AnOrderToAnNpcThatCannotOfferItOrALessonStepThatIsNotTheLessonsIsAProblem()
    {
        var book = TestGame.Data.Objectives;
        var toVillager = ObjectiveBook.Parse("o.json", """{ "schemaVersion": 1, "id": "bread-run", "deliverTo": "villager", "deliver": [ { "item": "bread", "count": 1 } ] }""", "bread-run", TestGame.Nouns);
        var noStep = ObjectiveBook.Parse(
            "o.json",
            """{ "schemaVersion": 1, "id": "more-wood", "deliverTo": "baker", "deliver": [ { "item": "wood", "count": 1 } ], "lesson": "numeros-y-pan", "lessonStep": "no-such" }""",
            "more-wood",
            TestGame.Nouns);
        var late = ObjectiveBook.Parse(
            "o.json",
            """{ "schemaVersion": 1, "id": "oven-wood", "deliverTo": "baker", "deliver": [ { "item": "wood", "count": 5 } ], "lesson": "numeros-y-pan", "lessonStep": "n5-coins" }""",
            "oven-wood",
            TestGame.Nouns);
        var objectives = book.Objectives.Where(static o => o.Id != "oven-wood").Concat([toVillager, noStep, late]).ToList();
        var data = TestGame.Data.WithObjectives(ObjectiveBook.Of(objectives, book.ClosingPool, book.Grants, book.BeaconStart));

        var problems = GameDataValidator.Validate(data);

        Assert.Contains("game/objectives/bread-run.json: the order is offered when the talk opens, and the initial state of agents/villager.json does not take 'villager.offer_order' into 'offering_order'.", problems);
        Assert.Contains("game/objectives/bread-run.json: agents/villager.json has no state 'offering_order' to wait in for the answer to its offer.", problems);
        Assert.Contains("game/objectives/more-wood.json: lessonStep names 'no-such', which is not a step of the lesson 'numeros-y-pan'.", problems);
        Assert.Contains(problems, static p => p.StartsWith("game/objectives/oven-wood.json: lessonStep 'n5-coins' comes at or after the step", StringComparison.Ordinal));
    }

    [Fact]
    public void ALessonStepWithoutItsLessonIsRefused()
    {
        var error = Assert.Throws<InvalidDataException>(() => ObjectiveBook.Parse(
            "game/objectives/o.json",
            """{ "schemaVersion": 1, "id": "o", "deliverTo": "smith", "deliver": [ { "item": "wood", "count": 1 } ], "lessonStep": "g4-offer" }""",
            "o",
            TestGame.Nouns));
        Assert.Contains("names its lesson too", error.Message, StringComparison.Ordinal);
    }
}
