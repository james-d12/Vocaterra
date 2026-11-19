using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Containers;

namespace Vocaterra.Game.Core.Tests.Items.Containers;

/// <summary>A later container (a chest, W24) works with the moves as they are (I02): a test container is registered and every move reaches it, with no edit of <see cref="InventoryOps"/>.</summary>
public sealed class TestContainerTests
{
    private const int ChestId = 50;

    private static (MoveRig Rig, ChestContainer Chest) WithChest()
    {
        var rig = new MoveRig();
        var chest = new ChestContainer(ChestId, MoveRig.Bread);
        rig.Sim.Containers.Add(chest);
        return (rig, chest);
    }

    [Fact]
    public void Given_ARegisteredContainer_When_AStackIsCarriedToIt_Then_TheMovesWork()
    {
        var (rig, chest) = WithChest();
        _ = rig.With(12, MoveRig.Apple, 6);

        _ = rig.Ops.LeftClick(ContainerIds.Bag, 12);
        _ = rig.Ops.LeftClick(ChestId, 3);
        _ = rig.Ops.RightClick(ChestId, 3);

        Assert.Equal(new ItemStack(MoveRig.Apple, 3), chest.Get(3));
        Assert.Equal(new ItemStack(MoveRig.Apple, 3), rig.Sim.Held.Stack);
    }

    [Fact]
    public void Given_AStackInTheContainer_When_ShiftClicked_Then_ItGoesToTheBag()
    {
        var (rig, chest) = WithChest();
        chest.Put(2, new ItemStack(MoveRig.Apple, 4));

        var result = rig.Ops.QuickMove(ChestId, 2);

        Assert.True(result.IsDone);
        Assert.True(chest.Get(2).IsEmpty);
        Assert.Equal(4, rig.Bag.Count(MoveRig.Apple));
    }

    [Fact]
    public void Given_AContainerThatRefusesAnItem_When_ItIsPutDown_Then_ItIsRefusedAndNothingChanges()
    {
        var (rig, chest) = WithChest();
        _ = rig.With(12, MoveRig.Bread, 2);
        _ = rig.Ops.LeftClick(ContainerIds.Bag, 12);
        var before = rig.Snapshot();

        var result = rig.Ops.LeftClick(ChestId, 0);

        Assert.Equal(MoveReason.SlotRefused, result.Reason);
        Assert.Equal(before, rig.Snapshot());
        Assert.True(chest.Get(0).IsEmpty);
    }

    [Fact]
    public void Given_TheCommandLayer_When_ItNamesTheContainerById_Then_TheSimulationMovesTheStack()
    {
        var (rig, chest) = WithChest();
        _ = rig.With(12, MoveRig.Apple, 6);

        rig.Sim.Step([Vocaterra.Game.Core.Simulation.VoxelCommand.SlotMove(MoveAction.LeftClick, ContainerIds.Bag, 12), Vocaterra.Game.Core.Simulation.VoxelCommand.SlotMove(MoveAction.LeftClick, ChestId, 5)]);

        Assert.Equal(new ItemStack(MoveRig.Apple, 6), chest.Get(5));
    }

    [Fact]
    public void Given_AnIdTaken_When_AContainerIsAdded_Then_ItThrows()
    {
        var (rig, _) = WithChest();

        _ = Assert.Throws<ArgumentException>(() => rig.Sim.Containers.Add(new ChestContainer(ChestId, MoveRig.Bread)));
        _ = Assert.Throws<ArgumentException>(() => rig.Sim.Containers.Add(new ChestContainer(ContainerIds.Held, MoveRig.Bread)));
    }
}
