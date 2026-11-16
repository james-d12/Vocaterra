using Vocaterra.Game.Core.Stats;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Stats;

/// <summary>The worn equipment as the first stat contributor (I03a), and a second contributor that needs no edit of the sheet or its readers.</summary>
public sealed class EquipmentStatsTests
{
    [Fact]
    public void Given_NothingWorn_When_TheSheetIsBuilt_Then_ItIsEmpty()
    {
        var sim = GearItems.Sim();

        Assert.Empty(sim.Stats.Keys);
        Assert.Equal(0, sim.Stats.GetWhole(StatKeys.Warmth));
    }

    [Fact]
    public void Given_ClothesWorn_When_TheyAreEquipped_Then_TheirStatsAreSummedAtOnce()
    {
        var sim = GearItems.Sim();

        _ = sim.Equip(GearItems.Bag(sim, GearItems.Shirt), GearItems.Slot("clothing", "body"));
        _ = sim.Equip(GearItems.Bag(sim, GearItems.Hat, 13), GearItems.Slot("clothing", "head"));
        _ = sim.Equip(GearItems.Bag(sim, GearItems.Plate, 14), GearItems.Slot("armour", "body"));

        Assert.Equal(55, sim.Stats.Get(StatKeys.Warmth)); // 30 + 20 + 5 (the plate's)
        Assert.Equal(5, sim.Stats.Get("armour"));
    }

    [Fact]
    public void Given_ACosmeticItem_When_ItIsWorn_Then_ItGivesNothing()
    {
        var sim = GearItems.Sim();

        _ = sim.Equip(GearItems.Bag(sim, GearItems.Scarf), GearItems.Slot("cosmetic", "body"));

        Assert.False(sim.Equipment.IsEmpty);
        Assert.Empty(sim.Stats.Keys);
        _ = sim.Equip(GearItems.Bag(sim, GearItems.Scarf, 13), GearItems.Slot("clothing", "body"));
        Assert.Equal(7, sim.Stats.Get(StatKeys.Warmth)); // the clothing one counts, the cosmetic one still does not
    }

    [Fact]
    public void Given_AnItemTakenOff_When_TheSheetIsRead_Then_ItsStatsAreGone()
    {
        var sim = GearItems.Sim();
        _ = sim.Equip(GearItems.Bag(sim, GearItems.Shirt), GearItems.Slot("clothing", "body"));
        Assert.Equal(30, sim.Stats.Get(StatKeys.Warmth));

        _ = sim.Unequip(GearItems.Slot("clothing", "body"));

        Assert.Equal(0, sim.Stats.Get(StatKeys.Warmth));
    }

    [Fact]
    public void Given_ANewContributorClass_When_ItIsRegistered_Then_TheSheetSumsItWithTheEquipmentAndNothingElseIsEdited()
    {
        var sim = GearItems.Sim();
        _ = sim.Equip(GearItems.Bag(sim, GearItems.Shirt), GearItems.Slot("clothing", "body"));

        sim.StatPipeline.Add(new MealBuff());
        sim.Step();

        Assert.Equal(30 + 25, sim.Stats.Get(StatKeys.Warmth));
        Assert.Equal(1.5, sim.Stats.Factor("walkSpeed"));
    }

    /// <summary>A contributor the sheet and the equipment know nothing about: a meal's buff, as W15 will add one.</summary>
    private sealed class MealBuff : IStatContributor
    {
        public void Contribute(StatBuilder builder)
        {
            builder.Add(StatKeys.Warmth, 25);
            builder.Multiply("walkSpeed", 1.5);
        }
    }
}
