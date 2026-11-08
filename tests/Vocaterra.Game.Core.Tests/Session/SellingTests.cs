using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Pipeline;

namespace Vocaterra.Game.Core.Tests.Session;

/// <summary>
/// Selling to the village (village-plan V15, 3.4 and 3.6): the intent <c>offer_item</c> (<c>vendo madera</c>, <c>quiero vender madera</c>, <c>tengo madera para usted</c>) and
/// the host's <c>buy</c>, the stall's <c>sell</c> turned round, the series' one repeatable source of coins. Luz and Domingo buy wood, León fruit, a coin each; with no number
/// the NPC takes every piece the bag holds, with one that many. A bag with none (<c>&lt;npc&gt;.cant_sell</c>) or another item offered (<c>&lt;npc&gt;.wont_buy</c>) changes
/// nothing, and the NPC says why. No cap: the goods are the cap. And the market's questions answered as the world works: the total of León's table and paying with fruit.
/// </summary>
public sealed class SellingTests
{
    private const string Paid = "¡Gracias! Aquí tienes tus monedas.";

    // A session on the bundle's route, without lessons (a lesson would take the NPC's talk).
    private static VoxelSession NewSession()
    {
        var data = TestGame.Data;
        var noLessons = LessonBook.Parse("{ \"schemaVersion\": 1, \"lessons\": [] }", data.Phrases, data.Speech, data.Bundle.Prompts, data.Locale, data.Npcs.Ids);
        return new VoxelSession(data, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Route(), lessons: noLessons) { Day = 10 };
    }

    private static VoxelSession At(string npc, ItemId? item = null, int count = 0)
    {
        var session = NewSession();
        if (item is { } held && count > 0)
        {
            _ = session.Simulation.GrantItems(held, count);
        }

        Stage.TeleportToNpc(session, npc);
        _ = session.OpenTalk(npc);
        return session;
    }

    private static IEnumerable<WorldEvent> WorldEventsOf(VoxelSession session, string name) => session.Events.OfType<WorldEvent>().Where(e => e.Name == name);

    private static int Count(VoxelSession session, ItemId item) => session.Simulation.Inventory.Count(item);

    [Fact]
    public void TheBuyersAndTheirPricesAreDataAndTheValidatorChecksTheirAnswers()
    {
        var data = TestGame.Data;
        Assert.Equal(NpcEffect.BuyAt(ItemId.Wood, 1), data.HostEvents.Of("baker", "baker.buy_wood"));
        Assert.Equal(NpcEffect.BuyAt(ItemId.Wood, 1), data.HostEvents.Of("smith", "smith.buy_wood"));
        Assert.Equal(NpcEffect.BuyAt(ItemId.Berry, 1), data.HostEvents.Of("farmer", "farmer.buy_fruit"));
        Assert.Empty(GameDataValidator.Validate(data)); // each buyer's agent takes <npc>.cant_sell and <npc>.wont_buy
        foreach (var prompt in new[] { "baker-open", "smith-open", "farmer-open" })
        {
            Assert.Contains("offer_item", data.Bundle.Prompts[prompt].AllowedIntentIds);
        }

        Assert.Contains("offer_item", data.Bundle.Prompts["stall-need"].AllowedIntentIds); // W10: the stall buys bread (a coin a loaf) and nothing else
    }

    [Theory]
    [InlineData("vendo madera")]
    [InlineData("Vendo madera.")]
    [InlineData("quiero vender madera")] // the brief's pair: the present of the want sells as the present of the sale does
    [InlineData("te vendo madera")]
    [InlineData("Luz, vendo madera")] // the listener's name as a vocative (V14)
    public void SellingWoodToTheBakerPaysACoinForEachPieceTheBagHolds(string line)
    {
        var session = At("baker", ItemId.Wood, 4);

        var turn = session.Say(line);

        Assert.Equal(("offer_item", true), (turn.Intent, turn.Communicated));
        Assert.Equal([Paid], turn.Replies);
        Assert.Empty(turn.FollowUp ?? []);
        Assert.Equal((0, 4), (Count(session, ItemId.Wood), Count(session, ItemId.Coin)));
        var taken = Assert.Single(WorldEventsOf(session, WorldEvents.ItemTaken));
        Assert.Equal(("baker", "madera, request"), (taken.Npc, taken.Detail));
        Assert.Equal("moneda, request", Assert.Single(WorldEventsOf(session, WorldEvents.ItemGiven)).Detail);
    }

    [Theory]
    [InlineData("baker", "vendo tres bloques de madera", 3)] // wood counted as Luz counts it (V15b): unknown with vocab.wrong before
    [InlineData("baker", "te vendo tres bloques de madera", 3)]
    [InlineData("baker", "quiero vender tres bloques de madera", 3)]
    [InlineData("baker", "vendo un bloque de madera", 1)] // the article counts one
    [InlineData("baker", "Luz, vendo dos bloques de madera", 2)]
    [InlineData("smith", "vendo dos bloques de madera", 2)]
    [InlineData("smith", "tengo dos bloques de madera para usted", 2)]
    public void WoodCountedInBlocksSellsThatMany(string npc, string line, int sold)
    {
        var session = At(npc, ItemId.Wood, 4); // four: five would deliver the baker's oven order as the talk opens

        var turn = session.Say(line);

        Assert.Equal(("offer_item", true), (turn.Intent, turn.Communicated));
        Assert.Empty(turn.Errors);
        Assert.Equal("madera", turn.Slots["item"]);
        Assert.Equal((4 - sold, sold), (Count(session, ItemId.Wood), Count(session, ItemId.Coin)));
    }

    [Fact]
    public void AnArticleBeforeTheItemCountsOneWhenTheLineNamesNoNumber()
    {
        var slots = new Dictionary<string, string>(StringComparer.Ordinal) { ["item"] = "madera" };

        Assert.Equal("1", NpcEffect.CountedSlots(slots, "vendo un bloque de madera")[NpcEffect.QuantitySlot]);
        Assert.Equal("1", NpcEffect.CountedSlots(slots, "Vendo una fruta.")[NpcEffect.QuantitySlot]);
        Assert.Same(slots, NpcEffect.CountedSlots(slots, "vendo madera")); // no article, no number: every one the bag holds
        var three = new Dictionary<string, string>(StringComparer.Ordinal) { ["item"] = "madera", ["qty"] = "3" };
        Assert.Same(three, NpcEffect.CountedSlots(three, "vendo tres bloques de madera"));
    }

    [Fact]
    public void ANumberSaidSellsThatManyAndNeverMoreThanTheBagHolds()
    {
        var session = At("farmer", ItemId.Berry, 5);

        _ = session.Say("vendo dos frutas");
        Assert.Equal((3, 2), (Count(session, ItemId.Berry), Count(session, ItemId.Coin)));

        _ = session.Say("vendo cinco frutas"); // three left: the three are sold
        Assert.Equal((0, 5), (Count(session, ItemId.Berry), Count(session, ItemId.Coin)));
    }

    [Fact]
    public void WithNoneToSellNothingChangesHandsAndTheNpcSaysSo()
    {
        var session = At("baker");
        var records = session.Conversation.RecordCount;

        var turn = session.Say("vendo madera");

        Assert.Equal("offer_item", turn.Intent);
        Assert.Empty(turn.Replies); // not "¡Gracias! Aquí tienes tus monedas.": nothing is bought
        Assert.Equal(["No tienes madera."], turn.FollowUp);
        Assert.Equal(0, Count(session, ItemId.Coin));
        Assert.Empty(WorldEventsOf(session, WorldEvents.ItemTaken));
        Assert.Empty(WorldEventsOf(session, WorldEvents.ItemGiven));
        Assert.Equal(records + 2, session.Conversation.RecordCount);
        Assert.Equal("baker.cant_sell", Assert.IsType<HostTurnEnvelope>(session.Conversation.Records[^1].HostTurn).HostEvent);
    }

    [Theory]
    [InlineData("baker", "vendo pan", ItemId.Bread, "No, gracias. Solo compro madera.")]
    [InlineData("farmer", "vendo madera", ItemId.Wood, "No compro eso. Compro fruta del bosque.")]
    public void AnotherItemIsNotBoughtAndTheNpcSaysWhatItBuys(string npc, string line, ItemId offered, string answer)
    {
        var session = At(npc, offered, 2);
        _ = session.Simulation.GrantItems(npc == "baker" ? ItemId.Wood : ItemId.Berry, 2); // it holds what the NPC buys too: only the line's item is offered

        var turn = session.Say(line);

        Assert.Equal("offer_item", turn.Intent);
        Assert.Empty(turn.Replies);
        Assert.Equal([answer], turn.FollowUp);
        Assert.Equal((2, 0), (Count(session, offered), Count(session, ItemId.Coin)));
        Assert.Equal($"{npc}.wont_buy", Assert.IsType<HostTurnEnvelope>(session.Conversation.Records[^1].HostTurn).HostEvent);
    }

    [Theory]
    [InlineData("baker", "quiero madera", "Solo vendo pan.")] // before V15 the bakery sold bread for any item asked for
    [InlineData("farmer", "quiero dos panes", "Solo vendo manzanas.")]
    public void APurchaseOfAnotherItemChangesNothingAndTheNpcSaysWhatItSells(string npc, string line, string answer)
    {
        var session = At(npc, ItemId.Coin, 10);

        var turn = session.Say(line);

        Assert.Equal("request_item", turn.Intent);
        Assert.Empty(turn.Replies);
        Assert.Equal([answer], turn.FollowUp);
        Assert.Equal((10, 0, 0), (Count(session, ItemId.Coin), Count(session, ItemId.Bread), Count(session, ItemId.Apple)));
        Assert.Equal($"{npc}.wont_sell", Assert.IsType<HostTurnEnvelope>(session.Conversation.Records[^1].HostTurn).HostEvent);
    }

    [Theory]
    [InlineData("vendí madera")] // a report: the DSL has no tense, so offer_item's grammars name the present (vendo) and state_action takes vender's other forms
    [InlineData("venderé madera")]
    [InlineData("mañana vendo madera")] // a plan
    [InlineData("voy a vender madera")]
    public void APastOrAFutureSaleIsAStatementAndSellsNothing(string line)
    {
        var session = At("baker", ItemId.Wood, 3);

        var turn = session.Say(line);

        Assert.Equal(("state_action", true), (turn.Intent, turn.Communicated));
        Assert.Equal((3, 0), (Count(session, ItemId.Wood), Count(session, ItemId.Coin)));
        Assert.Empty(WorldEventsOf(session, WorldEvents.ItemTaken));
    }

    [Theory]
    [InlineData("smith", "vendo madera", ItemId.Wood)]
    [InlineData("smith", "tengo madera para usted", ItemId.Wood)] // the plan's second grammar (para ti: ti is not in the lexicon)
    [InlineData("smith", "quiero vender madera", ItemId.Wood)]
    [InlineData("farmer", "vendo fruta", ItemId.Berry)]
    [InlineData("farmer", "quiero vender fruta", ItemId.Berry)]
    public void TheSmithBuysWoodAndTheFarmerFruit(string npc, string line, ItemId item)
    {
        var session = At(npc, item, 3);

        var turn = session.Say(line);

        Assert.Equal(("offer_item", true), (turn.Intent, turn.Communicated));
        Assert.Equal([Paid], turn.Replies);
        Assert.Equal((0, 3), (Count(session, item), Count(session, ItemId.Coin)));
    }

    [Theory]
    [InlineData("smith", "tengo madera", "state_have")] // the confusion the plan names (section 5): a possession is not an offer
    [InlineData("smith", "quiero madera", "state_want")] // nor a want
    [InlineData("smith", "necesito madera", "state_want")]
    public void APossessionOrAWantAtTheSmithIsNotASale(string npc, string line, string intent)
    {
        var session = At(npc, ItemId.Wood, 3);

        var turn = session.Say(line);

        Assert.Equal(intent, turn.Intent);
        Assert.Equal((3, 0), (Count(session, ItemId.Wood), Count(session, ItemId.Coin)));
    }

    [Fact]
    public void SellingIsRepeatableAsOftenAsTheLearnerBringsSome()
    {
        // No cap: each coin is a piece of wood chopped, and nothing an NPC sells is bought back, so no talk makes coins out of nothing.
        var session = At("baker", ItemId.Wood, 2);
        _ = session.Say("vendo madera");
        _ = session.Simulation.GrantItems(ItemId.Wood, 5); // more wood from the trees
        _ = session.Say("vendo madera");
        _ = session.Say("vendo madera"); // none left: she says so

        Assert.Equal((0, 7), (Count(session, ItemId.Wood), Count(session, ItemId.Coin)));
        Assert.Equal(2, WorldEventsOf(session, WorldEvents.ItemTaken).Count());
        Assert.Equal(["No tienes madera."], session.Say("vendo madera").FollowUp);
    }

    [Fact]
    public void TheTotalOfLeonsTableIsFifteenApplesAtHisPriceAndLuzAnswersWithHers()
    {
        var farmer = At("farmer");
        var total = farmer.Say("¿Cuánto es todo?");
        Assert.Equal(("ask_total", true), (total.Intent, total.Communicated));
        Assert.Equal(["Una manzana, una moneda."], total.Replies); // V21b: in free talk the unit price; the market lesson's step keeps the table of fifteen (MarketLessonTests)
        Assert.Equal(1, TestGame.Data.HostEvents.Of("farmer", "farmer.sell_apples").Price); // todo: his fifteen apples at his price, a coin each, fifteen coins
        Assert.Equal(0, farmer.Simulation.Inventory.Count(ItemId.Coin));

        var baker = At("baker");
        var hers = baker.Say("¿Cuánto es todo?");
        Assert.Equal(("ask_total", true), (hers.Intent, hers.Communicated));
        Assert.Equal(["El pan cuesta dos monedas."], hers.Replies);
    }

    [Fact]
    public void PayingWithFruitIsAnsweredAsTheWorldWorksAndFruitPaysInTwoSteps()
    {
        var session = At("farmer", ItemId.Berry, 3);

        var asked = session.Say("¿Puedo pagar con fruta?");
        Assert.Equal(("ask_pay", true), (asked.Intent, asked.Communicated));
        Assert.Equal(["Aquí se paga con monedas. Pero yo te compro fruta."], asked.Replies);
        Assert.Equal((3, 0), (Count(session, ItemId.Berry), Count(session, ItemId.Coin))); // no barter: nothing changes hands

        _ = session.Say("vendo fruta");
        var bought = session.Say("quiero tres manzanas");
        Assert.Equal("request_item", bought.Intent);
        Assert.Equal((0, 0, 3), (Count(session, ItemId.Berry), Count(session, ItemId.Coin), Count(session, ItemId.Apple)));
    }
}
