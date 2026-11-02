using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Pipeline;

namespace Vocaterra.Game.Core.Tests.Session;

/// <summary>
/// What a purchase or a sale cannot do, found by the review of <c>feature/village</c> (V19, R7 to R10): a catalogue noun of no item is another thing, not the NPC's
/// item (<c>quiero casa</c> bought bread); a bag with no room says so (the NPC said its success line and nothing changed hands); the number said has no hidden cap
/// (<c>quiero quince manzanas</c> gave five); and zero moves nothing (<c>quiero cero panes</c> bought a loaf). Each miss is answered by the NPC, and nothing changes hands.
/// </summary>
public sealed class ExchangeMissTests
{
    private const string Full = "Tu bolsa está llena.";

    // A session on the bundle's route, without lessons (a lesson would take the NPC's talk), in the market's session.
    private static VoxelSession At(string npc, params (ItemId Item, int Count)[] held)
    {
        var data = TestGame.Data;
        var noLessons = LessonBook.Parse("{ \"schemaVersion\": 1, \"lessons\": [] }", data.Phrases, data.Speech, data.Bundle.Prompts, data.Locale, data.Npcs.Ids);
        var session = new VoxelSession(data, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Route(), lessons: noLessons) { Day = 10 };
        foreach (var (item, count) in held)
        {
            Assert.Equal(0, session.Simulation.GrantItems(item, count));
        }

        Stage.TeleportToNpc(session, npc);
        _ = session.OpenTalk(npc);
        return session;
    }

    // Fills every empty slot of the bag with one torch: nothing new fits, and a stack that is there still takes more.
    private static void FillTheBag(VoxelSession session)
    {
        for (var slot = 0; slot < Inventory.SlotCount; slot++)
        {
            if (session.Simulation.Inventory.Slots[slot].IsEmpty)
            {
                session.Simulation.Inventory.SetSlot(slot, new ItemStack(ItemId.Torch, 1));
            }
        }
    }

    private static int Count(VoxelSession session, ItemId item) => session.Simulation.Inventory.Count(item);

    private static string LastHostEvent(VoxelSession session) => Assert.IsType<HostTurnEnvelope>(session.Conversation.Records[^1].HostTurn).HostEvent;

    private static void NothingChangedHands(VoxelSession session)
    {
        Assert.DoesNotContain(session.Events.OfType<WorldEvent>(), static e => e.Name is WorldEvents.ItemTaken or WorldEvents.ItemGiven);
    }

    private static SlotMap Slots(params (string Key, string Value)[] pairs) => SlotMap.From(pairs.Select(static p => new KeyValuePair<string, string>(p.Key, p.Value)));

    // ---- R7: a catalogue noun that names no item

    [Fact]
    public void ACatalogueNounOfNoItemIsAnotherThingAndAWordTheCatalogueLacksOrNoItemIsTheNpcsOwn()
    {
        var nouns = TestGame.Data.Nouns;
        var bread = NpcEffect.SellAt(ItemId.Bread, 2);

        Assert.True(bread.ItemSaidIn(Slots(("item", "pan")), nouns));
        Assert.True(bread.ItemSaidIn(Slots(("qty", "2")), nouns)); // no item slot: the NPC's item, as the doc comment says
        Assert.True(bread.ItemSaidIn(Slots(("item", "queso")), nouns)); // a word the catalogue lacks
        Assert.False(bread.ItemSaidIn(Slots(("item", "madera")), nouns)); // another item (V15)
        foreach (var noun in nouns.Nouns.Where(static n => n.Items.Count == 0))
        {
            Assert.False(bread.ItemSaidIn(Slots(("item", noun.Lemma)), nouns), noun.Lemma); // casa, río, piedra, árbol and the rest: another thing (V19)
        }
    }

    [Theory]
    [InlineData("quiero casa", "casa")] // the review's case: «¡Eso es! «Quiero casa».» and then the sale of a loaf
    [InlineData("quiero una piedra", "piedra")]
    public void AtTheBakeryAThingThatIsNoItemBuysNoBread(string line, string word)
    {
        var session = At("baker", (ItemId.Coin, 10));

        var turn = session.Say(line);

        Assert.Equal(("request_item", word), (turn.Intent, turn.Slots["item"]));
        Assert.Empty(turn.Replies); // not "Aquí tienes el pan. ¡Gracias!"
        Assert.Equal(["Solo vendo pan."], turn.FollowUp);
        Assert.Equal((10, 0), (Count(session, ItemId.Coin), Count(session, ItemId.Bread)));
        Assert.Equal("baker.wont_sell", LastHostEvent(session));
        NothingChangedHands(session);
    }

    [Fact]
    public void ToTheSmithAStoneSellsNoWood()
    {
        var session = At("smith", (ItemId.Wood, 3));

        var turn = session.Say("vendo piedra");

        Assert.Equal(("offer_item", "piedra"), (turn.Intent, turn.Slots["item"]));
        Assert.Empty(turn.Replies);
        Assert.Equal(["No, gracias. Solo compro madera."], turn.FollowUp);
        Assert.Equal((3, 0), (Count(session, ItemId.Wood), Count(session, ItemId.Coin)));
        Assert.Equal("smith.wont_buy", LastHostEvent(session));
    }

    [Fact]
    public void TheNpcsOwnItemStillSellsAndIsStillSold()
    {
        var wood = At("baker", (ItemId.Wood, 4)); // four: five would deliver the oven order as the talk opens
        _ = wood.Say("vendo bloques de madera");
        Assert.Equal((0, 4), (Count(wood, ItemId.Wood), Count(wood, ItemId.Coin)));

        var bread = At("baker", (ItemId.Coin, 4));
        _ = bread.Say("quiero dos panes");
        Assert.Equal((0, 2), (Count(bread, ItemId.Coin), Count(bread, ItemId.Bread)));
    }

    // ---- R8: no room in the bag

    [Fact]
    public void APurchaseWithNoRoomForTheItemsChangesNothingAndTheNpcSaysTheBagIsFull()
    {
        var session = At("baker", (ItemId.Coin, 10));
        FillTheBag(session);
        var records = session.Conversation.RecordCount;

        var turn = session.Say("quiero dos panes");

        Assert.Equal("request_item", turn.Intent);
        Assert.Empty(turn.Replies); // not her sale's line: before V19 she said it and nothing was given
        Assert.Equal([Full], turn.FollowUp);
        Assert.Equal((10, 0), (Count(session, ItemId.Coin), Count(session, ItemId.Bread)));
        Assert.Equal(records + 2, session.Conversation.RecordCount);
        Assert.Equal("baker.no_room", LastHostEvent(session));
        NothingChangedHands(session);
    }

    [Fact]
    public void ThePaymentMakesRoomTheCoinsOfAPurchaseFreeTheirSlotForTheItems()
    {
        var session = At("farmer", (ItemId.Coin, 3));
        FillTheBag(session);

        var turn = session.Say("quiero tres manzanas"); // every coin goes: their slot takes the apples

        Assert.EndsWith("¡Gracias!", Assert.Single(turn.Replies), StringComparison.Ordinal);
        Assert.Empty(turn.FollowUp ?? []);
        Assert.Equal((0, 3), (Count(session, ItemId.Coin), Count(session, ItemId.Apple)));
    }

    [Theory]
    [InlineData("baker", "vendo un bloque de madera", ItemId.Wood)] // one of two: the wood's slot stays, and the coins have none
    [InlineData("smith", "vendo un bloque de madera", ItemId.Wood)]
    [InlineData("farmer", "vendo una fruta", ItemId.Berry)]
    public void ASaleWithNoRoomForTheCoinsChangesNothingAndTheNpcSaysTheBagIsFull(string npc, string line, ItemId item)
    {
        var session = At(npc, (item, 2));
        FillTheBag(session);

        var turn = session.Say(line);

        Assert.Equal("offer_item", turn.Intent);
        Assert.Empty(turn.Replies); // not "¡Gracias! Aquí tienes tus monedas."
        Assert.Equal([Full], turn.FollowUp);
        Assert.Equal((2, 0), (Count(session, item), Count(session, ItemId.Coin)));
        Assert.Equal($"{npc}.no_room", LastHostEvent(session));
        NothingChangedHands(session);

        _ = session.Say(npc == "farmer" ? "vendo dos frutas" : "vendo dos bloques de madera"); // both: the slot they leave takes the coins
        Assert.Equal((0, 2), (Count(session, item), Count(session, ItemId.Coin)));
    }

    // ---- R9: no hidden cap

    [Fact]
    public void ASaleTakesTheNumberSaidAboveFiveAtMostWhatTheBagHolds()
    {
        var session = At("smith", (ItemId.Wood, 8));

        _ = session.Say("vendo ocho bloques de madera"); // five before V19, while "vendo madera" took all eight

        Assert.Equal((0, 8), (Count(session, ItemId.Wood), Count(session, ItemId.Coin)));

        var more = At("smith", (ItemId.Wood, 6));
        _ = more.Say("vendo veinte bloques de madera"); // six held: the six are sold
        Assert.Equal((0, 6), (Count(more, ItemId.Wood), Count(more, ItemId.Coin)));
    }

    [Fact]
    public void APurchaseGivesTheNumberSaidAboveFiveBoundOnlyByTheCoins()
    {
        var session = At("farmer", (ItemId.Coin, 15));

        var turn = session.Say("quiero quince manzanas"); // five apples for five coins before V19, with the success line

        Assert.EndsWith("¡Gracias!", Assert.Single(turn.Replies), StringComparison.Ordinal);
        Assert.Equal((0, 15), (Count(session, ItemId.Coin), Count(session, ItemId.Apple)));

        var poor = At("farmer", (ItemId.Coin, 12));
        var dear = poor.Say("quiero quince manzanas");
        Assert.Empty(dear.Replies);
        Assert.Equal(["Necesitas más monedas. Una manzana cuesta una moneda."], dear.FollowUp);
        Assert.Equal((12, 0), (Count(poor, ItemId.Coin), Count(poor, ItemId.Apple)));
    }

    [Fact]
    public void ANumberOfTwelveDigitsOverflowsNothingItCostsMoreThanTheBagHolds()
    {
        var bag = new Inventory();
        Assert.Equal(0, bag.Add(ItemId.Coin, 15));
        var huge = Slots(("item", "manzana"), ("qty", "1000000000000"));

        Assert.Equal(NpcHostEvents.CantPay, NpcEffect.SellAt(ItemId.Apple, 1).ExchangeMiss(huge, bag, TestGame.Data.Nouns));
        Assert.Equal(int.MaxValue, NpcEffect.SellAt(ItemId.Bread, 2).CostFor(huge));
        Assert.Equal(15, NpcEffect.BuyAt(ItemId.Wood, 1).SaleCountFor(huge, 15)); // a sale of twelve digits: what the bag holds
    }

    // ---- R10: zero

    [Theory]
    [InlineData("baker", "quiero cero panes", "Aquí hay pan. ¿Cuántos panes quieres?", "baker.none_asked")] // her own line, which asks how many
    [InlineData("farmer", "quiero cero manzanas", "¿Cuántas manzanas quieres?", "farmer.none_asked")] // his lesson's line, which asks it
    public void APurchaseOfNoneMovesNothingAndTheNpcAsksHowMany(string npc, string line, string answer, string hostEvent)
    {
        var session = At(npc, (ItemId.Coin, 4));

        var turn = session.Say(line);

        Assert.Equal(("request_item", "0"), (turn.Intent, turn.Slots["qty"]));
        Assert.Empty(turn.Replies); // no success line: before V19 the baker sold one loaf
        Assert.Equal([answer], turn.FollowUp);
        Assert.Equal((4, 0, 0), (Count(session, ItemId.Coin), Count(session, ItemId.Bread), Count(session, ItemId.Apple)));
        Assert.Equal(hostEvent, LastHostEvent(session));
        NothingChangedHands(session);
    }

    [Theory]
    [InlineData("baker", "vendo cero bloques de madera", ItemId.Wood, "¿Cuántos bloques de madera me vendes?")]
    [InlineData("smith", "vendo cero bloques de madera", ItemId.Wood, "¿Cuántos bloques de madera me vendes?")]
    [InlineData("farmer", "vendo cero frutas", ItemId.Berry, "¿Cuántas frutas me vendes?")]
    public void ASaleOfNoneMovesNothingAndTheNpcAsksHowMany(string npc, string line, ItemId item, string answer)
    {
        var session = At(npc, (item, 3));

        var turn = session.Say(line);

        Assert.Equal(("offer_item", "0"), (turn.Intent, turn.Slots["qty"]));
        Assert.Empty(turn.Replies);
        Assert.Equal([answer], turn.FollowUp);
        Assert.Equal((3, 0), (Count(session, item), Count(session, ItemId.Coin)));
        Assert.Equal($"{npc}.none_offered", LastHostEvent(session));
        NothingChangedHands(session);
    }

    [Fact]
    public void EveryNpcThatSellsOrBuysCanAnswerEveryMiss()
    {
        Assert.Empty(GameDataValidator.Validate(TestGame.Data)); // the validator checks no_room, none_asked and none_offered (V19) with the rest
    }
}
