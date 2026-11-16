using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Needs;

/// <summary>What worn gear does to the walk, the drains and the stamina's ceiling (I05a), proved with the tests' own gear; with nothing worn nothing moves.</summary>
public sealed class StatConsumerTests
{
    private static VoxelSession Wearing(params (ItemId Item, string Group, string Slot)[] gear)
    {
        var session = GearItems.Session();
        var slot = 12;
        foreach (var (item, group, name) in gear)
        {
            _ = session.Simulation.Equip(GearItems.Bag(session.Simulation, item, slot++), group == "accessories" ? GearItems.Nth(group, 0 + Used(gear, item)) : GearItems.Slot(group, name));
        }

        return session;
    }

    // The accessory slots are counted: the nth accessory in the list goes in the nth slot.
    private static int Used(IReadOnlyList<(ItemId Item, string Group, string Slot)> gear, ItemId item) => gear.Where(g => g.Group == "accessories").ToList().FindIndex(g => g.Item == item);

    private static (ItemId, string, string) Accessory(ItemId item) => (item, "accessories", string.Empty);

    private static int Walk(VoxelSession session, bool sprint = false)
    {
        session.SprintHeld = sprint;
        session.Step(sprint ? [VoxelCommand.Move(0, 1)] : []);
        return session.Simulation.WalkPermille;
    }

    [Fact]
    public void Given_NothingWorn_When_TheTickPrepares_Then_TheWalkIsExactlyWhatItWas()
    {
        var session = GearItems.Session();

        Assert.Equal(1000, Walk(session));
        Assert.Equal((int)Math.Round(1000 * session.Data.Needs.Stamina.SprintSpeedFactor), Walk(session, sprint: true));
    }

    [Fact]
    public void Given_AWalkItemWorn_When_TheTickPrepares_Then_TheWalkIsItsFactor()
    {
        var session = Wearing(Accessory(GearItems.Ring));

        Assert.Equal(1100, Walk(session));
    }

    [Fact]
    public void Given_TwoWalkItemsWornWithTheSprint_When_TheTickPrepares_Then_TheFactorsMultiplyWithTheSprints()
    {
        var session = Wearing(Accessory(GearItems.Ring), Accessory(GearItems.Anklet));

        var expected = (int)Math.Round(1000 * 1.1 * 1.2 * session.Data.Needs.Stamina.SprintSpeedFactor);

        Assert.Equal(expected, Walk(session, sprint: true));
    }

    [Fact]
    public void Given_AWalkItemAndTheNeedsOff_When_TheTickPrepares_Then_TheGearStillSpeedsTheWalk()
    {
        var session = Wearing(Accessory(GearItems.Ring));
        session.NeedsEnabled = false;

        Assert.Equal(1100, Walk(session));
    }

    [Fact]
    public void Given_ColdAndAWalkItem_When_TheTickPrepares_Then_TheGearMultipliesWithTheCold()
    {
        var session = Wearing(Accessory(GearItems.Anklet));
        session.Temperature.HeldTenths = -80; // very cold: 0.65

        Assert.Equal((int)Math.Round(1000 * 0.65 * 1.2), Walk(session));
    }

    private static double Fall(VoxelSession session, NeedMeter meter, int ticks = 2400)
    {
        var before = session.Needs.Value(meter);
        for (var i = 0; i < ticks; i++)
        {
            session.Step([]);
        }

        return before - session.Needs.Value(meter);
    }

    [Fact]
    public void Given_NothingWorn_When_TimePasses_Then_TheDrivesFallAsTheyDid()
    {
        var plain = GearItems.Session();
        var other = TestGame.NewSession();

        Assert.Equal(Fall(other, NeedMeter.Hunger), Fall(plain, NeedMeter.Hunger));
    }

    [Fact]
    public void Given_AHungerItem_When_TimePasses_Then_OnlyHungerFallsSlower()
    {
        var plain = GearItems.Session();
        var worn = Wearing(Accessory(GearItems.Flask));

        var plainHunger = Fall(plain, NeedMeter.Hunger);
        var plainThirst = Fall(plain, NeedMeter.Thirst);
        var wornHunger = Fall(worn, NeedMeter.Hunger);
        var wornThirst = Fall(worn, NeedMeter.Thirst);

        Assert.True(plainHunger > 0);
        Assert.Equal(plainThirst, wornThirst, 9);
        Assert.Equal(plainHunger / 2, wornHunger, 4); // the first pass moved hunger by the full ticks; the second pass of thirst is by a fresh clock
    }

    [Fact]
    public void Given_AThirstItem_When_TimePasses_Then_OnlyThirstFallsSlowerAndItMultipliesWithTheNeedsRate()
    {
        var plain = GearItems.Session();
        var worn = Wearing(Accessory(GearItems.Gourd));
        worn.NeedsRate = 2;
        plain.NeedsRate = 2;

        var plainHunger = Fall(plain, NeedMeter.Hunger, 1200);
        var wornHunger = Fall(worn, NeedMeter.Hunger, 1200);

        Assert.Equal(plainHunger, wornHunger, 9);
        var plainThirst = plain.Needs.Value(NeedMeter.Thirst);
        var wornThirst = worn.Needs.Value(NeedMeter.Thirst);
        Assert.True(100 - wornThirst < 100 - plainThirst);
        Assert.Equal((100 - plainThirst) / 2, 100 - wornThirst, 4);
    }

    [Fact]
    public void Given_AStaminaItem_When_TheEffectsAreRead_Then_TheCeilingIsRaisedWhereThePenaltyIsTaken()
    {
        var session = Wearing(Accessory(GearItems.Charm));
        var plain = GearItems.Session();

        Assert.Equal(plain.Needs.Effects().StaminaMaximum + 10, session.Needs.Effects().StaminaMaximum);
        session.Needs.Set(NeedMeter.Thirst, 0); // the last stage takes its penalty off the raised ceiling
        plain.Needs.Set(NeedMeter.Thirst, 0);
        Assert.Equal(plain.Needs.Effects().StaminaMaximum + 10, session.Needs.Effects().StaminaMaximum);
    }

    [Fact]
    public void Given_AStaminaItem_When_StaminaRefills_Then_ItRisesPastTheOldMaximumUpToTheNewOne()
    {
        var session = Wearing(Accessory(GearItems.Charm));
        var ceiling = session.Needs.Effects().StaminaMaximum;
        Assert.Equal(110, ceiling);
        session.Needs.Set(NeedMeter.Stamina, 105);
        Assert.Equal(105, session.Needs.Value(NeedMeter.Stamina));

        for (var i = 0; i < 4000; i++)
        {
            session.Step([]);
        }

        Assert.Equal(110, session.Needs.Value(NeedMeter.Stamina), 6);
        Assert.Equal(1.0, new NeedsHud(session).StaminaMaximum, 9); // the bar scales to the ceiling
    }

    [Fact]
    public void Given_AStaminaItemTakenOff_When_TheEffectsAreRead_Then_TheMeterIsClampedBackToTheOldMaximum()
    {
        var session = Wearing(Accessory(GearItems.Charm));
        session.Needs.Set(NeedMeter.Stamina, 108);

        _ = session.Simulation.Unequip(GearItems.Nth("accessories", 0));
        session.Step([]);

        Assert.Equal(100, session.Needs.Value(NeedMeter.Stamina));
    }

    [Fact]
    public void Given_NothingWorn_When_TheDigestIsTaken_Then_GearAndItsConsumersChangeNothingInIt()
    {
        var plain = GearItems.Session();
        var other = TestGame.NewSession();

        for (var i = 0; i < 120; i++)
        {
            plain.Step([]);
            other.Step([]);
        }

        Assert.Equal(other.Simulation.ComputeStateDigest(), plain.Simulation.ComputeStateDigest());
        Assert.Equal(other.Needs.Capture(), plain.Needs.Capture());
    }
}
