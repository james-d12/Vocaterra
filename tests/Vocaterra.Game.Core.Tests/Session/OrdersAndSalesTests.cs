using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Pipeline;

namespace Vocaterra.Game.Core.Tests.Session;

/// <summary>
/// A purchase and an order by data alone (village-plan V03), on the shipped baker since V11 (until then a test baker on a copy of the bundle, added the way
/// <c>content/README.md</c> says): her agent sells bread at two coins (<c>sell</c>), says when the coins are too few (<c>baker.cant_pay</c>) and thanks the player for
/// the oven's wood (<c>baker.delivered</c>, whose reward is four coins); the order <c>oven-wood</c> (five wood, from play session 5) is an objective with
/// <c>deliverTo</c>.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class OrdersAndSalesTests
{
    private const string Hello = "¡Hola!";

    private const string Thanks = "¡Gracias! Toma cuatro monedas.";

    private const string Asks = "¡Hola! ¿Qué tienes para mí?";

    private const string HelpLine = "Puedes decir: «Aquí tienes cinco bloques de madera».";

    private static GameData Baker => TestGame.Data;

    private const string Offer = "¿Me traes cinco bloques de madera para el horno? Te pago cuatro monedas.";

    // A session on the bundle's route, in a play session; without lessons unless asked (a lesson would take the HUD's line and the baker's talk). Its orders are taken
    // (issue #54) unless asked: these tests are about the sale and the delivery, not the offer (OrderOfferTests).
    private static VoxelSession NewSession(int playSession = 1, GameData? data = null, bool shippedLessons = false, bool ordersTaken = true)
    {
        data ??= Baker;
        var noLessons = LessonBook.Parse("{ \"schemaVersion\": 1, \"lessons\": [] }", data.Phrases, data.Speech, data.Bundle.Prompts, data.Locale, data.Npcs.Ids);
        var session = new VoxelSession(data, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Route(), lessons: shippedLessons ? null : noLessons) { Day = playSession };
        return ordersTaken ? session.TakeOrders() : session;
    }

    private static IEnumerable<WorldEvent> WorldEventsOf(VoxelSession session, string name) => session.Events.OfType<WorldEvent>().Where(e => e.Name == name);

    [Fact]
    public void TheBakersDataHoldsTogetherWithTheChecksOfOrdersAndSales()
    {
        var order = Baker.Objectives.Find("oven-wood")!;
        Assert.Equal((5, "baker", true, false), (order.Session, order.DeliverTo, order.IsOrder, order.EndsSession));
        Assert.Equal([new OrderGoods(ItemId.Wood, 5)], order.Goods);
        var delivery = Assert.Single(order.Steps);
        Assert.Equal((ObjectiveBook.DeliveredStep, 1, new Trigger(WorldEvents.OrderDelivered, "baker", "oven-wood")), (delivery.Id, delivery.Target, delivery.Trigger));
        Assert.Equal(NpcEffect.SellAt(ItemId.Bread, 2), Baker.HostEvents.Of("baker", "baker.sell_bread"));
        Assert.Empty(GameDataValidator.Validate(Baker)); // what the order and the sale need of her agent is there (the validator checks the cant_pay and delivered turns)
    }

    [Theory]
    [InlineData("quiero pan, por favor", 1)]
    [InlineData("quiero dos panes, por favor", 2)]
    [InlineData("quiero nueve panes, por favor", 9)] // the number asked for: no cap since V19 (the review's R9; five before), the coins are the bound
    public void ASaleTakesThePriceOfTheNumberAskedForAndGivesThatMany(string line, int loaves)
    {
        var session = NewSession();
        _ = session.Simulation.GrantItems(ItemId.Coin, 20);
        Assert.Equal([Hello], session.OpenTalkForClass("baker").Lines);

        var turn = session.Say(line);

        Assert.Equal("request_item", turn.Intent);
        Assert.EndsWith("¡Gracias!", Assert.Single(turn.Replies), StringComparison.Ordinal); // "Aquí tienes el pan." or "Toma tu pan.", and her thanks
        Assert.Empty(turn.FollowUp ?? []);
        Assert.Equal(loaves, session.Simulation.Inventory.Count(ItemId.Bread));
        Assert.Equal(20 - (2 * loaves), session.Simulation.Inventory.Count(ItemId.Coin));
        var paid = Assert.Single(WorldEventsOf(session, WorldEvents.ItemTaken));
        Assert.Equal(("baker", "moneda, request"), (paid.Npc, paid.Detail));
        Assert.Equal("pan, request", Assert.Single(WorldEventsOf(session, WorldEvents.ItemGiven)).Detail);
    }

    [Fact]
    public void WithTooFewCoinsNothingChangesHandsAndTheBakerSaysSo()
    {
        var session = NewSession();
        _ = session.Simulation.GrantItems(ItemId.Coin, 3);
        _ = session.OpenTalkForClass("baker");
        var records = session.Conversation.RecordCount;

        var turn = session.Say("quiero dos panes, por favor"); // four coins

        Assert.Empty(turn.Replies); // not "Aquí tienes el pan.": nothing is handed over
        Assert.Equal(["Necesitas más monedas."], turn.FollowUp);
        Assert.Equal(3, session.Simulation.Inventory.Count(ItemId.Coin));
        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.Bread));
        Assert.Empty(WorldEventsOf(session, WorldEvents.ItemTaken));
        Assert.Empty(WorldEventsOf(session, WorldEvents.ItemGiven));
        Assert.Equal(records + 2, session.Conversation.RecordCount);
        Assert.Equal("baker.cant_pay", Assert.IsType<HostTurnEnvelope>(session.Conversation.Records[^1].HostTurn).HostEvent);

        var one = session.Say("quiero pan, por favor"); // two coins: enough for one
        Assert.EndsWith("¡Gracias!", Assert.Single(one.Replies), StringComparison.Ordinal);
        Assert.Equal((1, 1), (session.Simulation.Inventory.Count(ItemId.Coin), session.Simulation.Inventory.Count(ItemId.Bread)));
    }

    [Fact]
    public void AnOrderIsHiddenAndCountsNothingUntilItsNpcOffersItAndThePlayerSaysSiWhateverTheDay()
    {
        var data = WithOrderFirst(Baker);
        var early = NewSession(playSession: 4, data, ordersTaken: false);
        _ = early.Simulation.GrantItems(ItemId.Wood, 5);

        Assert.Equal("meet-guide", early.CurrentObjective!.Id); // the order and the gated objective come first in this HUD's order, and are hidden until the order is taken (issue #54)
        Assert.Equal(early.Locale.ObjectiveHudLine("meet-guide"), early.ObjectiveHud().Single().Text);
        early.HostEvent(WorldEvents.ItemPickedUp, "madera");
        Assert.Equal(0, early.ObjectiveStep("gated.wood")!.Count); // a gated objective counts nothing before its session
        Assert.Equal(5, early.ObjectiveStep("camp.wood")!.Count); // the camp counts the wood in the bag (#32)

        Assert.Equal([Offer], early.OpenTalk("baker").Lines); // met, with the wood: she offers the order first, and takes no goods of an order not given
        Assert.False(early.HandOverOpen);
        Assert.Equal("camp", early.CurrentObjective!.Id); // the camp's wood moved, which also met the guide; the order is not shown yet
        _ = early.AcceptOrder(); // «sí», on a day before its old session
        Assert.Equal("oven-wood", early.CurrentObjective!.Id);
        early.EndTalk();
        _ = early.OpenTalk("baker"); // now she asks for the goods the player carries
        Assert.True(early.HandOverOpen);
        _ = early.HandOverGoods(); // handed over in Spanish (D-game-27)
        Assert.Equal(0, early.Simulation.Inventory.Count(ItemId.Wood));

        var due = NewSession(playSession: 5, data, ordersTaken: false);
        Assert.Equal("gated", due.CurrentObjective!.Id); // nor does the day show it: this gated objective is not an order
        _ = due.OpenTalk("baker");
        Assert.Equal("gated", due.CurrentObjective!.Id); // meeting her is not enough
        _ = due.AcceptOrder();
        Assert.Equal("oven-wood", due.CurrentObjective!.Id);
        Assert.Equal("Bring Luz five wood (madera) for her oven at the bakery", due.ObjectiveHud().Single().Text);
        _ = due.Simulation.GrantItems(ItemId.Wood, 1);
        due.HostEvent(WorldEvents.ItemPickedUp, "madera");
        Assert.Equal(1, due.ObjectiveStep("gated.wood")!.Count);
    }

    [Fact]
    public void BringingTheGoodsAndSayingWhatTheyAreDeliversTheOrderTheBakerThanksAndPaysAndTheObjectiveIsDone()
    {
        var session = NewSession(playSession: 5);
        _ = session.Simulation.GrantItems(ItemId.Wood, 6);

        var opening = session.OpenTalk("baker"); // D-game-27 (issue #42): Luz asks for the wood; walking up takes nothing yet

        Assert.False(opening.Delivered);
        Assert.Equal([Asks], opening.Lines);
        Assert.True(session.HandOverOpen);
        Assert.Equal("baker-receive", session.Conversation.PromptOf("baker"));
        Assert.Equal(6, session.Simulation.Inventory.Count(ItemId.Wood));
        Assert.Contains(WorldEventsOf(session, WorldEvents.OrderAsked), static e => e.Npc == "baker" && e.Detail == "oven-wood");
        Assert.Contains("aqui-tienes-cinco-bloques", session.Support.Snapshot().InPlay); // the card of the line she waits for

        var records = session.Conversation.RecordCount;
        var turn = session.Say("Aquí tienes cinco bloques de madera.");

        Assert.Equal(("hand_over", true), (turn.Intent, turn.Communicated));
        Assert.Equal([Thanks], turn.FollowUp);
        Assert.False(turn.FailForward);
        Assert.False(session.HandOverOpen);
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Wood));
        Assert.Equal(4, session.Simulation.Inventory.Count(ItemId.Coin));
        Assert.Equal("baker.delivered", Assert.IsType<HostTurnEnvelope>(session.Conversation.Records[^1].HostTurn).HostEvent);
        Assert.Equal(records + 2, session.Conversation.RecordCount); // the learner's line, then her thanks
        Assert.Equal("baker-open", session.Conversation.PromptOf("baker")); // she is back in her usual state
        var world = session.Events.OfType<WorldEvent>().Where(static e => e.Name is WorldEvents.ItemTaken or WorldEvents.ItemGiven or WorldEvents.OrderDelivered).ToList();
        Assert.Equal(
            [(WorldEvents.ItemTaken, "baker", "madera, order"), (WorldEvents.ItemGiven, "baker", "moneda, order"), (WorldEvents.OrderDelivered, "baker", "oven-wood")],
            world.Select(static e => (e.Name, e.Npc, e.Detail)));
        Assert.True(session.Objectives.Single(o => o.Id == "oven-wood").Complete);
        Assert.False(session.SessionComplete); // an order is no part of the first session's ending

        // Delivered once: the next talk opens as usual and takes nothing.
        session.EndTalk();
        Assert.Equal([Hello], session.OpenTalkForClass("baker").Lines);
        Assert.False(session.HandOverOpen);
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Wood));
    }

    [Theory]
    [InlineData("Aquí tienes la madera.")] // A1: the count is optional
    [InlineData("Toma la madera.")]
    [InlineData("Te doy cinco bloques de madera.")]
    [InlineData("Sí, aquí está la madera.")]
    public void NamingTheGoodsWithOrWithoutTheCountHandsThemOver(string line)
    {
        var session = NewSession(playSession: 5);
        _ = session.Simulation.GrantItems(ItemId.Wood, 5);
        _ = session.OpenTalk("baker");

        var turn = session.Say(line);

        Assert.Equal([Thanks], turn.FollowUp);
        Assert.True(session.Objectives.Single(o => o.Id == "oven-wood").Complete);
    }

    [Fact]
    public void OtherGoodsAreAMissWithANoteAndTwoMissesHandTheGoodsOverAnyway()
    {
        var session = NewSession(playSession: 5);
        _ = session.Simulation.GrantItems(ItemId.Wood, 5);
        _ = session.OpenTalk("baker");

        var wrong = session.Say("Aquí tienes las frutas."); // understood, but not what she asked for

        Assert.Equal("hand_over", wrong.Intent);
        Assert.Empty(wrong.Replies); // no «¡Muy bien!» for the wrong goods
        Assert.Contains(wrong.EnglishNotes, static n => n.Contains("not what Luz asked for", StringComparison.Ordinal) && n.Contains("Aquí tienes cinco bloques de madera.", StringComparison.Ordinal));
        Assert.True(session.HandOverOpen);
        Assert.Equal(5, session.Simulation.Inventory.Count(ItemId.Wood));

        var second = session.Say("Hola."); // not a hand-over: the second miss

        Assert.True(second.FailForward); // rule 8 (D-game-8): the goods go anyway
        Assert.Equal([Thanks], second.FollowUp);
        Assert.Contains(second.EnglishNotes, static n => n.Contains("takes the goods anyway", StringComparison.Ordinal));
        Assert.False(session.HandOverOpen);
        Assert.Equal((0, 4), (session.Simulation.Inventory.Count(ItemId.Wood), session.Simulation.Inventory.Count(ItemId.Coin)));
        Assert.Contains(session.Events.OfType<PhraseUse>(), static u => u.Kind == PhraseUseKind.FailForward && u.PhraseId == "aqui-tienes-cinco-bloques" && u.Npc == "baker");
        Assert.True(session.Objectives.Single(o => o.Id == "oven-wood").Complete);
    }

    [Fact]
    public void AyudaIsNoMissAndNoSeSaysTheModelAndHandsTheGoodsOver()
    {
        var session = NewSession(playSession: 5);
        _ = session.Simulation.GrantItems(ItemId.Wood, 5);
        _ = session.OpenTalk("baker");

        var help = session.Say("Ayuda.");
        var miss = session.Say("Hola."); // one miss after the help: still under the two

        Assert.Equal([HelpLine], help.Replies);
        Assert.False(miss.FailForward);
        Assert.True(session.HandOverOpen);

        var dontKnow = session.Say("No sé.");

        Assert.Equal([HelpLine], dontKnow.Replies); // the model first, never shamed (D-game-9)
        Assert.Equal([Thanks], dontKnow.FollowUp);
        Assert.True(dontKnow.FailForward);
        Assert.True(session.Objectives.Single(o => o.Id == "oven-wood").Complete);
    }

    [Fact]
    public void ClosingThePanelAsksAgainAndGoodsThatAreGoneEndTheQuestion()
    {
        var session = NewSession(playSession: 5);
        _ = session.Simulation.GrantItems(ItemId.Wood, 5);
        _ = session.OpenTalk("baker");
        _ = session.Say("Hola."); // one miss
        session.EndTalk();

        Assert.False(session.HandOverOpen);
        Assert.Equal([Asks], session.OpenTalk("baker").Lines); // asked again, the misses counted from nothing
        Assert.False(session.Say("Hola.").FailForward);
        session.EndTalk();

        Assert.True(session.Simulation.TakeItems(ItemId.Wood, 5)); // the wood went elsewhere while she waited
        Assert.Equal([Hello], session.OpenTalk("baker").Lines);
        Assert.False(session.HandOverOpen);
        Assert.Equal("baker-open", session.Conversation.PromptOf("baker"));
        Assert.False(session.Objectives.Single(o => o.Id == "oven-wood").Complete);
    }

    [Fact]
    public void DomingoTakesTheWoodAndTheCoinsWhenEitherIsNamed()
    {
        var session = NewSession(playSession: 6);
        _ = session.Simulation.GrantItems(ItemId.Wood, 2);
        _ = session.Simulation.GrantItems(ItemId.Coin, 2);

        Assert.Equal([Asks], session.OpenTalk("smith").Lines);
        var turn = session.Say("Aquí tienes las monedas.");

        Assert.Equal(["¡Gracias! Toma el hacha."], turn.FollowUp);
        Assert.Equal((0, 0, 1), (session.Simulation.Inventory.Count(ItemId.Wood), session.Simulation.Inventory.Count(ItemId.Coin), session.Simulation.Inventory.Count(ItemId.Axe)));
        Assert.True(session.Objectives.Single(o => o.Id == "axe-handle").Complete);
    }

    [Fact]
    public void WithoutAllTheGoodsTheTalkOpensAsUsualAndNothingIsTaken()
    {
        var session = NewSession(playSession: 5);
        _ = session.Simulation.GrantItems(ItemId.Wood, 4);

        var opening = session.OpenTalkForClass("baker");

        Assert.False(opening.Delivered);
        Assert.Equal([Hello], opening.Lines);
        Assert.Equal(4, session.Simulation.Inventory.Count(ItemId.Wood));
        Assert.Empty(WorldEventsOf(session, WorldEvents.OrderDelivered));
    }

    [Fact]
    public void ADeliveredOrderAndTheVillagesItemsSurviveASaveAndALoad()
    {
        // No save field is new (V03): the inventory keeps items by name, and the order's one step is keyed oven-wood.delivered like any other step.
        var session = NewSession(playSession: 5, shippedLessons: true); // Números y pan waits for El pueblo, so the baker talks as herself
        _ = session.Simulation.GrantItems(ItemId.Wood, 5);
        _ = session.OpenTalkForClass("baker");
        session.EndTalk();
        _ = session.Simulation.GrantItems(ItemId.Apple, 3);
        _ = session.Simulation.GrantItems(ItemId.Axe, 1);

        var text = SaveGame.Save(session, TestGame.Route());
        Assert.Contains("\"oven-wood.delivered\"", text, StringComparison.Ordinal);
        Assert.Contains("{\"item\":\"Coin\",\"count\":4}", text, StringComparison.Ordinal); // the inventory keeps an item by its name
        var loaded = SaveGame.Load(text, Baker, static seed => TestGame.Route(seed), bumpSession: false);

        Assert.Equal(session.Simulation.Inventory.Slots, loaded.Simulation.Inventory.Slots);
        Assert.Equal((4, 3, 1), (loaded.Simulation.Inventory.Count(ItemId.Coin), loaded.Simulation.Inventory.Count(ItemId.Apple), loaded.Simulation.Inventory.Count(ItemId.Axe)));
        Assert.True(loaded.Objectives.Single(o => o.Id == "oven-wood").Complete);
        Assert.Equal(text, SaveGame.Save(loaded, TestGame.Route()));
    }

    // The bundle's data with the order first in the HUD's order, then an objective of session 5 that counts wood picked up.
    private static GameData WithOrderFirst(GameData data)
    {
        var book = data.Objectives;
        var gated = ObjectiveBook.Parse(
            "gated.json",
            """{ "schemaVersion": 1, "id": "gated", "session": 5, "steps": [ { "id": "wood", "target": 2, "trigger": { "event": "item_picked_up", "detail": "madera" } } ] }""",
            "gated",
            data.Nouns);
        var objectives = book.Objectives.Where(static o => o.Id == "oven-wood").Append(gated).Concat(book.Objectives.Where(static o => o.Id != "oven-wood")).ToList();
        return data.WithObjectives(ObjectiveBook.Of(objectives, book.ClosingPool, book.Grants, book.BeaconStart));
    }
}
