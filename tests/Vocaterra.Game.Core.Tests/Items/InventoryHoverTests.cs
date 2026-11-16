using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Stats;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Items;

/// <summary>What the panel says about the cell under the pointer (I04): the label noun from the data and the catalogue, the worn item the usual way, the English until the word is known.</summary>
public sealed class InventoryHoverTests
{
    private static InventoryHover? Hover(Core.Session.VoxelSession session, InventoryHit hit) =>
        InventoryHover.Of(hit, session.Simulation.Inventory, session.Simulation.Equipment, session.Nouns, session.Words, session.Locale.NounSupport);

    private static int Flat(Core.Session.VoxelSession session, string group, string slot) =>
        session.Simulation.Equipment.Data.FlatIndex(GearItems.Slot(session.Simulation.Equipment.Data, group, slot));

    [Fact]
    public void Given_AnEmptyBodyCell_When_Hovered_Then_ItShowsTheLabelNounWithItsArticleAndNothingElse()
    {
        var session = TestGame.NewSession();

        var hover = Hover(session, new InventoryHit(InventoryHitKind.Equipment, Flat(session, "clothing", "head")));

        Assert.NotNull(hover);
        Assert.Equal("la cabeza", hover.Slot);
        Assert.Null(hover.Item);
    }

    [Theory]
    [InlineData("clothing", "body", "el cuerpo")]
    [InlineData("armour", "legs", "la pierna")]
    [InlineData("cosmetic", "feet", "el pie")]
    [InlineData("backpack", "back", "la espalda")]
    [InlineData("offhand", "offhand", "la mano")]
    public void Given_ALabelledSlot_When_Hovered_Then_TheArticleComesFromTheNounCatalogue(string group, string slot, string text)
    {
        var session = TestGame.NewSession();

        Assert.Equal(text, Hover(session, new InventoryHit(InventoryHitKind.Equipment, Flat(session, group, slot)))!.Slot);
    }

    [Fact]
    public void Given_AnEmptyAccessoryCell_When_Hovered_Then_ThereIsNothingToSay()
    {
        var session = TestGame.NewSession();

        Assert.Null(Hover(session, new InventoryHit(InventoryHitKind.Equipment, Flat(session, "accessories", "accessory1"))));
    }

    [Fact]
    public void Given_AnEmptyLabelCellOfAnUnknownWord_When_Hovered_Then_TheEnglishIsGiven()
    {
        var session = TestGame.NewSession();

        Assert.Equal("head", Hover(session, new InventoryHit(InventoryHitKind.Equipment, Flat(session, "clothing", "head")))!.English);
    }

    [Fact]
    public void Given_AWornItem_When_ItsCellIsHovered_Then_TheLabelAndTheItemsNameShow()
    {
        var session = GearItems.Session();
        session.Simulation.Equipment.Set(GearItems.Slot("clothing", "head"), new ItemStack(GearItems.Hat, 1));

        var hover = Hover(session, new InventoryHit(InventoryHitKind.Equipment, Flat(session, "clothing", "head")));

        Assert.Equal("la cabeza", hover!.Slot);
        Assert.Equal("test-hat", hover.Item);
    }

    [Fact]
    public void Given_ABagCellWithAnItem_When_Hovered_Then_TheItemsNameShowsWithNoSlotLabel()
    {
        var session = TestGame.NewSession();
        session.Simulation.Inventory.SetSlot(3, new ItemStack(TestItems.Apple, 2));

        var hover = Hover(session, new InventoryHit(InventoryHitKind.Cell, 3));

        Assert.Null(hover!.Slot);
        Assert.Equal("manzana", hover.Item);
    }

    [Fact]
    public void Given_NothingOrAnInactiveCell_When_Hovered_Then_ThereIsNoHover()
    {
        var session = TestGame.NewSession();

        Assert.Null(Hover(session, InventoryHit.None));
        Assert.Null(Hover(session, new InventoryHit(InventoryHitKind.Cell, 3)));
        Assert.Null(Hover(session, new InventoryHit(InventoryHitKind.Cell, 99)));
        Assert.Null(Hover(session, new InventoryHit(InventoryHitKind.Equipment, 99)));
        Assert.Null(Hover(session, new InventoryHit(InventoryHitKind.Sort)));
    }

    [Fact]
    public void Given_TheShippedLabels_When_TheyAreLookedUp_Then_EveryOneIsACatalogueNounWithAnEnglishWord()
    {
        var session = TestGame.NewSession();
        var data = session.Simulation.Equipment.Data;

        foreach (var label in data.AllSlots().Select(slot => data.Spec(slot).Label).OfType<string>().Distinct())
        {
            Assert.NotNull(session.Nouns.ForLemma(label));
            Assert.NotNull(session.Locale.NounSupport(label));
        }

        foreach (var lemma in StatDisplay.Defaults.Select(static d => d.Lemma).OfType<string>())
        {
            Assert.NotNull(session.Nouns.ForLemma(lemma));
        }
    }
}
