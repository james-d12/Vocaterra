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

    private static GameData Baker => TestGame.Data;

    // A session on the bundle's route, in a play session; without lessons unless asked (a lesson would take the HUD's line and the baker's talk).
    private static VoxelSession NewSession(int playSession = 1, GameData? data = null, bool shippedLessons = false)
    {
        data ??= Baker;
        var noLessons = LessonBook.Parse("{ \"schemaVersion\": 1, \"lessons\": [] }", data.Phrases, data.Speech, data.Bundle.Prompts, data.Locale, data.Npcs.Ids);
        return new VoxelSession(data, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Route(), lessons: shippedLessons ? null : noLessons) { Day = playSession };
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
        Assert.Equal([Hello], session.OpenTalk("baker").Lines);

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
        _ = session.OpenTalk("baker");
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
    public void AnOrderIsHiddenAndCountsNothingBeforeItsSession()
    {
        var data = WithOrderFirst(Baker);
        var early = NewSession(playSession: 4, data);
        _ = early.Simulation.GrantItems(ItemId.Wood, 5);

        Assert.Equal("meet-guide", early.CurrentObjective!.Id); // the order and the gated objective come first in this HUD's order, and are hidden before session 5
        Assert.Equal(early.Locale.ObjectiveHudLine("meet-guide"), early.ObjectiveHud().Single().Text);
        var opening = early.OpenTalk("baker");
        Assert.False(opening.Delivered); // the goods are there, the order is not yet
        Assert.Equal([Hello], opening.Lines);
        Assert.Equal(5, early.Simulation.Inventory.Count(ItemId.Wood));
        early.HostEvent(WorldEvents.ItemPickedUp, "madera");
        Assert.Equal(0, early.ObjectiveStep("gated.wood")!.Count); // a gated objective counts nothing before its session
        Assert.Equal(1, early.ObjectiveStep("camp.wood")!.Count);

        var due = NewSession(playSession: 5, data);
        Assert.Equal("oven-wood", due.CurrentObjective!.Id);
        Assert.Equal("Bring Luz five wood (madera) for her oven at the bakery", due.ObjectiveHud().Single().Text);
        due.HostEvent(WorldEvents.ItemPickedUp, "madera");
        Assert.Equal(1, due.ObjectiveStep("gated.wood")!.Count);
    }

    [Fact]
    public void BringingTheGoodsDeliversTheOrderTheBakerThanksAndPaysAndTheObjectiveIsDone()
    {
        var session = NewSession(playSession: 5);
        _ = session.Simulation.GrantItems(ItemId.Wood, 6);
        var records = session.Conversation.RecordCount;

        var opening = session.OpenTalk("baker"); // walking up with the goods is enough: no line is asked for

        Assert.True(opening.Delivered);
        Assert.Equal([Thanks], opening.Lines);
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Wood));
        Assert.Equal(4, session.Simulation.Inventory.Count(ItemId.Coin));
        Assert.Equal("baker.delivered", Assert.IsType<HostTurnEnvelope>(Assert.Single(session.Conversation.Records.Skip(records)).HostTurn).HostEvent);
        var world = session.Events.OfType<WorldEvent>().Where(static e => e.Name is WorldEvents.ItemTaken or WorldEvents.ItemGiven or WorldEvents.OrderDelivered).ToList();
        Assert.Equal(
            [(WorldEvents.ItemTaken, "baker", "madera, order"), (WorldEvents.ItemGiven, "baker", "moneda, order"), (WorldEvents.OrderDelivered, "baker", "oven-wood")],
            world.Select(static e => (e.Name, e.Npc, e.Detail)));
        Assert.True(session.Objectives.Single(o => o.Id == "oven-wood").Complete);
        Assert.False(session.SessionComplete); // an order is no part of the first session's ending

        // Delivered once: the next talk opens as usual and takes nothing.
        session.EndTalk();
        Assert.Equal([Hello], session.OpenTalk("baker").Lines);
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Wood));
    }

    [Fact]
    public void WithoutAllTheGoodsTheTalkOpensAsUsualAndNothingIsTaken()
    {
        var session = NewSession(playSession: 5);
        _ = session.Simulation.GrantItems(ItemId.Wood, 4);

        var opening = session.OpenTalk("baker");

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
        _ = session.OpenTalk("baker");
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
