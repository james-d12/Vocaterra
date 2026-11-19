using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Temperature;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Temperature;

/// <summary>The worn equipment's warmth in the session (I03a): the stat sheet's <c>warmth</c> replaces the zero source of I06, so clothes raise the feeling and can lift the band.</summary>
public sealed class EquipmentWarmthTests
{
    private static VoxelSession Held(int tenths)
    {
        var session = GearItems.Session();
        session.Temperature.HeldTenths = tenths;
        return session;
    }

    [Fact]
    public void Given_NothingWorn_When_TheSessionSteps_Then_TheWarmthIsZeroAndTheFeelingIsThePlaces()
    {
        var session = Held(-80);

        session.Step([]);

        Assert.Equal(0, session.Temperature.Current!.Value.WarmthTenths);
        Assert.Equal(-80, session.Temperature.Current!.Value.FeelingTenths);
        Assert.Equal("very-cold", session.Temperature.Current!.Value.Band.Id);
        Assert.IsType<StatWarmth>(session.Temperature.Warmth);
    }

    [Fact]
    public void Given_ClothesWorn_When_TheSessionSteps_Then_TheirWarmthRaisesTheFeelingAndTheBand()
    {
        var session = Held(-60);
        session.Step([]);
        Assert.Equal("very-cold", session.Temperature.Current!.Value.Band.Id);
        var sim = session.Simulation;

        _ = sim.Equip(GearItems.Bag(sim, GearItems.Shirt), GearItems.Slot("clothing", "body"));
        _ = sim.Equip(GearItems.Bag(sim, GearItems.Hat, 13), GearItems.Slot("clothing", "head"));
        session.Step([]);

        var reading = session.Temperature.Current!.Value;
        Assert.Equal((-60, 50, -10), (reading.PlaceTenths, reading.WarmthTenths, reading.FeelingTenths));
        Assert.Equal("cold", reading.Band.Id);
        _ = sim.Equip(GearItems.Bag(sim, GearItems.Boots, 14), GearItems.Slot("clothing", "feet"));
        session.Step([]);
        Assert.Equal(60, session.Temperature.Current!.Value.WarmthTenths);
        Assert.Equal("cold", session.Temperature.Current!.Value.Band.Id); // -60 + 60 = 0, still below the cool band's 30
        session.Temperature.HeldTenths = -20;
        session.Step([]);
        Assert.Equal("cool", session.Temperature.Current!.Value.Band.Id); // -20 + 60 = 40
    }

    [Fact]
    public void Given_ClothesPutOnByCommands_When_TheyAreTakenOff_Then_TheWarmthFallsAgain()
    {
        var session = Held(-60);
        var sim = session.Simulation;
        var bag = GearItems.Bag(sim, GearItems.Shirt);

        session.Step([VoxelCommand.Equip(bag, GearItems.Slot("clothing", "body"))]);
        session.Step([]);
        Assert.Equal(30, session.Temperature.Current!.Value.WarmthTenths);

        session.Step([VoxelCommand.Unequip(GearItems.Slot("clothing", "body"))]);
        session.Step([]);
        Assert.Equal(0, session.Temperature.Current!.Value.WarmthTenths);
    }

    [Fact]
    public void Given_ACosmeticShirt_When_ItIsWorn_Then_ItDoesNotWarm()
    {
        var session = Held(-60);
        var sim = session.Simulation;

        _ = sim.Equip(GearItems.Bag(sim, GearItems.Shirt), GearItems.Slot("cosmetic", "body"));
        session.Step([]);

        Assert.Equal(0, session.Temperature.Current!.Value.WarmthTenths);
    }
}
