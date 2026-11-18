using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Npc;

/// <summary>
/// Pablo's clothes and materials and Domingo's iron, bought by naming them (I08a, inventory plan 3.13a): the price of each, the count said, the plural «los pantalones», and the refusals in a
/// line each, through the same <c>sell</c> path as the stall's flour.
/// </summary>
public sealed class GearShopTests
{
    private static readonly string[] Sold = ["Aquí tienes. ¡Gracias!"];

    private static VoxelSession At(string npc, int coins)
    {
        var data = TestGame.Data;
        var noLessons = LessonBook.Parse("{ \"schemaVersion\": 1, \"lessons\": [] }", data.Phrases, data.Speech, data.Bundle.Prompts, data.Locale, data.Npcs.Ids);
        var session = new VoxelSession(data, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Route(), lessons: noLessons) { Day = 10 };
        _ = session.TakeOrders(); // the orders are taken: this test is not about their offer (issue #54)
        if (coins > 0)
        {
            Assert.Equal(0, session.Simulation.GrantItems(ItemId.Coin, coins));
        }

        Stage.TeleportToNpc(session, npc);
        _ = session.OpenTalkForClass(npc);
        return session;
    }

    private static int Held(VoxelSession session, string item) => session.Simulation.Inventory.Count(TestItems.Catalogue.Find(item)!.Id);

    private static List<string> Lines(GuideTurn turn) => [.. turn.Replies, .. turn.FollowUp ?? []];

    [Theory]
    [InlineData("quiero tela", "cloth", 1)]
    [InlineData("quiero cuero", "leather", 2)]
    [InlineData("quiero un sombrero", "hat", 2)]
    [InlineData("quiero un gorro", "cap", 3)]
    [InlineData("quiero una camisa", "shirt", 3)]
    [InlineData("quiero una chaqueta", "jacket", 6)]
    [InlineData("quiero un abrigo", "coat", 10)]
    [InlineData("quiero pantalones", "trousers", 3)]
    [InlineData("quiero unos pantalones", "trousers", 3)]
    [InlineData("necesito zapatos", "shoes", 3)]
    [InlineData("me das unos zapatos", "shoes", 3)]
    public void Given_PabloAndEnoughCoins_When_TheLearnerNamesAnItem_Then_ItIsSoldAtItsPrice(string line, string item, int price)
    {
        var session = At("shopkeeper", 20);

        var lines = Lines(session.Say(line));

        Assert.Equal((1, 20 - price), (Held(session, item), session.Simulation.Inventory.Count(ItemId.Coin)));
        Assert.Contains(Sold[0], lines);
        Assert.DoesNotContain(lines, static l => l.StartsWith("No tengo", StringComparison.Ordinal));
    }

    [Fact]
    public void Given_PabloAndCoins_When_TwoClothsAreAskedFor_Then_TwoAreSoldForTwoCoins()
    {
        var session = At("shopkeeper", 5);

        _ = session.Say("quiero dos telas");

        Assert.Equal((2, 3), (Held(session, "cloth"), session.Simulation.Inventory.Count(ItemId.Coin)));
    }

    [Fact]
    public void Given_PabloAndTooFewCoins_When_AnItemIsAskedFor_Then_NothingIsSoldAndHeSaysYouNeedMore()
    {
        var session = At("shopkeeper", 2);

        var lines = Lines(session.Say("quiero un abrigo"));

        Assert.Equal((0, 2), (Held(session, "coat"), session.Simulation.Inventory.Count(ItemId.Coin)));
        Assert.Contains("Necesitas más monedas.", lines);
        Assert.DoesNotContain(Sold[0], lines);
    }

    [Fact]
    public void Given_Pablo_When_AnItemHeDoesNotSellIsAskedFor_Then_HeListsWhatHeHas()
    {
        var session = At("shopkeeper", 20);

        var lines = Lines(session.Say("quiero madera"));

        Assert.Contains("No tengo eso. Tengo agua, pan, harina, tela, cuero y ropa.", lines);
        Assert.Equal(20, session.Simulation.Inventory.Count(ItemId.Coin));
    }

    [Fact]
    public void Given_Pablo_When_FlourIsAskedFor_Then_ItIsStillSoldForACoin()
    {
        var session = At("shopkeeper", 3);

        var lines = Lines(session.Say("quiero harina"));

        Assert.Equal((1, 2), (Held(session, "flour"), session.Simulation.Inventory.Count(ItemId.Coin)));
        Assert.Contains(Sold[0], lines);
    }

    [Fact]
    public void Given_TheShopkeepersSellRow_When_ItsPricesAreRead_Then_EveryClothingItemAndMaterialPablosHasAPrice()
    {
        var sell = TestGame.Data.HostEvents.Of("shopkeeper", "stall.sell_goods");

        var priced = sell.Prices!.Keys.Order(StringComparer.Ordinal).ToList();

        Assert.Equal(["abrigo", "camisa", "chaqueta", "cuero", "gorro", "harina", "pantalón", "sombrero", "tela", "zapato"], priced);
        var clothes = TestItems.Catalogue.All.Where(static d => d.Tags.Contains("clothing")).Select(static d => d.Id).Order();
        string[] clothesNouns = ["sombrero", "gorro", "camisa", "chaqueta", "abrigo", "pantalón", "zapato"];
        Assert.Equal(clothesNouns.Select(static n => TestGame.Nouns.ForLemma(n)!.Items[0]).Order(), clothes); // every piece of clothing has its price
    }

    [Fact]
    public void Given_DomingoAndCoins_When_IronIsAskedFor_Then_ItIsSoldForThreeCoinsEach()
    {
        var session = At("smith", 10);

        var lines = Lines(session.Say("quiero hierro"));

        Assert.Equal((1, 7), (Held(session, "iron"), session.Simulation.Inventory.Count(ItemId.Coin)));
        Assert.Contains("Aquí tienes el hierro. ¡Gracias!", lines);
        _ = session.Say("quiero dos hierros");
        Assert.Equal((3, 1), (Held(session, "iron"), session.Simulation.Inventory.Count(ItemId.Coin)));
    }

    [Fact]
    public void Given_DomingoAndTwoCoins_When_IronIsAskedFor_Then_HeSaysThePrice()
    {
        var session = At("smith", 2);

        var lines = Lines(session.Say("quiero hierro"));

        Assert.Equal(0, Held(session, "iron"));
        Assert.Contains("El hierro cuesta tres monedas.", lines);
    }

    [Fact]
    public void Given_Domingo_When_AnAxeOrAnythingElseIsWanted_Then_NothingIsSoldAndHeOffersTheAxeAsBefore()
    {
        var session = At("smith", 10);

        var axe = Lines(session.Say("quiero un hacha"));
        var bread = Lines(session.Say("quiero pan"));

        Assert.Contains("¿Quieres un hacha? Necesito dos bloques de madera y dos monedas.", axe);
        Assert.Contains("¿Quieres un hacha? Necesito dos bloques de madera y dos monedas.", bread);
        Assert.Equal(10, session.Simulation.Inventory.Count(ItemId.Coin));
        Assert.Equal(0, Held(session, "iron"));
    }
}
