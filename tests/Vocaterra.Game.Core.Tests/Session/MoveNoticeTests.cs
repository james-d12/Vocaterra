using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Containers;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Session;

/// <summary>The panel's refusal line (I04): a refused move or equip leaves a notice the panel shows for a few seconds of ticks, and nothing refused leaves none.</summary>
public sealed class MoveNoticeTests
{
    [Fact]
    public void Given_AFreshSession_When_NothingWasRefused_Then_ThereIsNoNotice()
    {
        Assert.Null(TestGame.NewSession().MoveNotice);
    }

    [Fact]
    public void Given_ARightClickOfADifferentItemIntoTheTrash_When_ItIsStepped_Then_TheTrashLineIsTheNotice()
    {
        var session = GearItems.Session();
        var sim = session.Simulation;
        sim.Inventory.SetSlot(12, new ItemStack(TestItems.Apple, 3));
        sim.Trash.Put(0, new ItemStack(TestItems.Bread, 1));
        session.Step([VoxelCommand.SlotMove(MoveAction.LeftClick, ContainerIds.Bag, 12)]);

        session.Step([VoxelCommand.SlotMove(MoveAction.RightClick, ContainerIds.Trash, 0)]);

        var notice = session.MoveNotice;
        Assert.NotNull(notice);
        Assert.Equal("refuseTrashDifferent", notice.Value.Key);
        Assert.True(notice.Value.IsShown(sim.Tick));
        Assert.False(notice.Value.IsShown(sim.Tick + MoveNotice.ShownTicks));
        Assert.True(session.Locale.Has("ui." + notice.Value.Key));
    }

    [Fact]
    public void Given_AHatDroppedOnTheLegsCell_When_ItIsStepped_Then_TheWrongSlotLineIsTheNotice()
    {
        var session = GearItems.Session();
        var sim = session.Simulation;
        sim.Inventory.SetSlot(12, new ItemStack(GearItems.Hat, 1));
        var legs = sim.Equipment.Data.FlatIndex(GearItems.Slot("clothing", "legs"));
        session.Step([VoxelCommand.SlotMove(MoveAction.LeftClick, ContainerIds.Bag, 12)]);

        session.Step([VoxelCommand.SlotMove(MoveAction.LeftClick, ContainerIds.Equipment, legs)]);

        Assert.Equal("refuseWrongSlot", session.MoveNotice!.Value.Key);
    }

    [Fact]
    public void Given_ALegalShiftClickOfAHat_When_ItIsStepped_Then_NoNoticeIsLeft()
    {
        var session = GearItems.Session();
        session.Simulation.Inventory.SetSlot(12, new ItemStack(GearItems.Hat, 1));

        session.Step([VoxelCommand.SlotMove(MoveAction.QuickMove, ContainerIds.Bag, 12)]);

        Assert.Null(session.MoveNotice);
        Assert.False(session.Simulation.Equipment.IsEmpty);
    }
}
