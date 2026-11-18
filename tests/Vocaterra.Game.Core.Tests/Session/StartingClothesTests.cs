using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Stats;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Session;

/// <summary>A new game starts in a shirt, trousers and shoes (I08a, <c>game/session.json</c> <c>wears</c>): worn, not in the bag, four degrees warm, and not carried into a loaded game.</summary>
public sealed class StartingClothesTests
{
    private static List<(string Group, string Slot, string Item)> Worn(Equipment equipment) =>
        [.. equipment.Worn().Select(w => (equipment.Data.Groups[w.Slot.Group].Id, equipment.Data.Spec(w.Slot).Name, w.Item.Name))];

    [Fact]
    public void Given_ANewGame_When_ItStarts_Then_TheShirtTheTrousersAndTheShoesAreWornInTheClothingLayer()
    {
        var session = TestGame.NewSession();

        Assert.Equal(["shirt", "trousers", "shoes"], TestGame.Data.Objectives.Wears.Select(id => TestItems.Catalogue.Get(id).Name));
        Assert.Equal([("clothing", "body", "shirt"), ("clothing", "legs", "trousers"), ("clothing", "feet", "shoes")], Worn(session.Simulation.Equipment));
    }

    [Fact]
    public void Given_ANewGame_When_TheBagIsRead_Then_ItHoldsOnlyTheBookAsBefore()
    {
        var session = TestGame.NewSession();

        var held = session.Simulation.Inventory.Slots.Where(static s => !s.IsEmpty).ToList();

        Assert.Equal([ItemId.Book], held.Select(static s => s.Item));
    }

    [Fact]
    public void Given_ANewGame_When_TheStatsSheetIsRead_Then_TheClothesGiveFourDegreesOfWarmth()
    {
        var session = TestGame.NewSession();

        var chips = StatDisplay.Chips(session.Simulation.Stats);

        Assert.Equal(40, session.Simulation.Stats.GetWhole(StatKeys.Warmth));
        Assert.Equal([new StatChip(StatKeys.Warmth, "+4", "calor")], chips);
    }

    [Fact]
    public void Given_ANewGame_When_TheAppearanceIsMade_Then_ItShowsTheShirtTheTrousersAndTheShoes()
    {
        var session = TestGame.NewSession();

        var appearance = AppearanceBuilder.Of(session.Simulation.Equipment);

        Assert.False(appearance.IsNone);
        Assert.Equal(["body", "feet", "legs", "sleeve"], appearance.Parts.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(TestItems.Catalogue.Find("shirt")!.Look!.Parts["sleeve"], appearance.Sleeve);
    }

    [Fact]
    public void Given_ASavedGameWithNothingWorn_When_ItIsLoaded_Then_TheStartingClothesAreNotWorn()
    {
        var session = TestGame.NewSession();
        var state = session.Simulation.CaptureState();
        var bare = state with { Worn = null };

        var other = TestGame.NewSession();
        other.Simulation.Restore(bare);

        Assert.All(other.Simulation.Equipment.Slots, static s => Assert.True(s.IsEmpty));
        Assert.Equal(0, other.Simulation.Stats.GetWhole(StatKeys.Warmth));
    }
}
