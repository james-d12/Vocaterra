using System.Text.Json.Nodes;
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
/// A price list at work (W04): the baker of a test bundle sells bread at two coins and apples at one, and the smith buys wood and berries (the bundle itself has no price list).
/// Each item is sold at its own price, the number said multiplies it, and every miss keeps the coins and the goods where they were.
/// </summary>
public sealed class PriceListSessionTests
{
    private static GameData Listed()
    {
        var root = JsonNode.Parse(GameData.HostText(TestGame.Data.Bundle, HostEventTable.RelativePath))!;
        foreach (var entry in root["events"]!.AsArray())
        {
            switch (entry!["event"]!.GetValue<string>())
            {
                case "baker.sell_bread":
                    entry["action"] = JsonNode.Parse("""{ "kind": "sell", "slot": "item", "prices": { "pan": 2, "manzana": 1 } }""");
                    break;
                case "smith.buy_wood":
                    entry["action"] = JsonNode.Parse("""{ "kind": "buy", "slot": "item", "prices": { "madera": 1, "fruta": 3 } }""");
                    break;
            }
        }

        return TestGame.Data.WithHostEvents(HostEventTable.Parse(root.ToJsonString(), TestItems.Catalogue));
    }

    private static VoxelSession At(string npc, params (ItemId Item, int Count)[] held)
    {
        var data = Listed();
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

    private static int Count(VoxelSession session, ItemId item) => session.Simulation.Inventory.Count(item);

    private static string LastHostEvent(VoxelSession session) => Assert.IsType<HostTurnEnvelope>(session.Conversation.Records[^1].HostTurn).HostEvent;

    private static void NothingChangedHands(VoxelSession session) =>
        Assert.DoesNotContain(session.Events.OfType<WorldEvent>(), static e => e.Name is WorldEvents.ItemTaken or WorldEvents.ItemGiven);

    [Fact]
    public void TheTestBundleIsValid() => Assert.Empty(GameDataValidator.Validate(Listed()));

    [Fact]
    public void OneNpcSellsTwoItemsEachAtItsOwnPrice()
    {
        var session = At("baker", (ItemId.Coin, 10));

        _ = session.Say("quiero un pan");
        Assert.Equal((8, 1, 0), (Count(session, ItemId.Coin), Count(session, TestItems.Bread), Count(session, TestItems.Apple)));

        _ = session.Say("quiero una manzana");
        Assert.Equal((7, 1, 1), (Count(session, ItemId.Coin), Count(session, TestItems.Bread), Count(session, TestItems.Apple)));
    }

    [Fact]
    public void TheNumberSaidMultipliesThePriceOfTheItemNamed()
    {
        var session = At("baker", (ItemId.Coin, 10));

        _ = session.Say("quiero dos panes");
        Assert.Equal((6, 2), (Count(session, ItemId.Coin), Count(session, TestItems.Bread)));

        _ = session.Say("quiero dos manzanas");
        Assert.Equal((4, 2), (Count(session, ItemId.Coin), Count(session, TestItems.Apple)));
    }

    [Fact]
    public void TooFewCoinsForTheItemNamedChangeNothingAndTheNpcSaysSo()
    {
        var session = At("baker", (ItemId.Coin, 3)); // two breads cost four; two apples cost two

        _ = session.Say("quiero dos panes");

        Assert.Equal((3, 0), (Count(session, ItemId.Coin), Count(session, TestItems.Bread)));
        Assert.Equal("baker.cant_pay", LastHostEvent(session));
        NothingChangedHands(session);

        _ = session.Say("quiero dos manzanas"); // the same coins buy the cheaper item
        Assert.Equal((1, 2), (Count(session, ItemId.Coin), Count(session, TestItems.Apple)));
    }

    [Fact]
    public void AnItemNotOnTheListIsWontSellAndNothingChangesHands()
    {
        var session = At("baker", (ItemId.Coin, 10));

        var turn = session.Say("quiero madera");

        Assert.Empty(turn.Replies);
        Assert.Equal("baker.wont_sell", LastHostEvent(session)); // the existing miss: the brief's dont_sell
        Assert.Equal((10, 0), (Count(session, ItemId.Coin), Count(session, ItemId.Wood)));
        NothingChangedHands(session);
    }

    [Fact]
    public void AFullBagKeepsTheCoinsAndTheGoods()
    {
        var session = At("baker", (ItemId.Coin, 10));
        for (var slot = 0; slot < Inventory.SlotCount; slot++)
        {
            if (session.Simulation.Inventory.Slots[slot].IsEmpty)
            {
                session.Simulation.Inventory.SetSlot(slot, new ItemStack(ItemId.Torch, 1));
            }
        }

        _ = session.Say("quiero dos manzanas");

        Assert.Equal((10, 0), (Count(session, ItemId.Coin), Count(session, TestItems.Apple)));
        Assert.Equal("baker.no_room", LastHostEvent(session));
        NothingChangedHands(session);
    }

    [Fact]
    public void APricedBuyPaysEachItemItsOwnPriceAndAnUnlistedOneIsWontBuy()
    {
        var session = At("smith", (ItemId.Wood, 4), (ItemId.Berry, 2), (TestItems.Bread, 1));

        _ = session.Say("vendo madera");
        Assert.Equal((0, 4), (Count(session, ItemId.Wood), Count(session, ItemId.Coin)));

        _ = session.Say("vendo dos frutas");
        Assert.Equal((0, 10), (Count(session, ItemId.Berry), Count(session, ItemId.Coin)));

        _ = session.Say("vendo pan");
        Assert.Equal((1, 10), (Count(session, TestItems.Bread), Count(session, ItemId.Coin)));
        Assert.Equal("smith.wont_buy", LastHostEvent(session));
    }
}
