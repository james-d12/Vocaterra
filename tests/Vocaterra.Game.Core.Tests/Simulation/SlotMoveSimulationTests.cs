using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Containers;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.Items.Containers;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Simulation;

/// <summary>The moves by hand in the simulation (I02): the SlotMove command applied in the tick, its refusal event, the held stack and the trash in the digest only when they hold something, and replay.</summary>
public sealed class SlotMoveSimulationTests
{
    private static MoveRig Rig() => new MoveRig().With(12, MoveRig.Apple, 5).With(13, GearItems.Shirt, 1).With(20, MoveRig.Bread, 3);

    private static IReadOnlyList<VoxelCommand>[] Script(MoveRig rig) =>
    [
        [VoxelCommand.SlotMove(MoveAction.LeftClick, ContainerIds.Bag, 12)],
        [VoxelCommand.SlotMove(MoveAction.LeftClick, ContainerIds.Bag, 25), VoxelCommand.SlotMove(MoveAction.RightClick, ContainerIds.Bag, 20)],
        [VoxelCommand.SlotMove(MoveAction.QuickMove, ContainerIds.Bag, 13)],
        [VoxelCommand.SlotMove(MoveAction.LeftClick, ContainerIds.Trash, 0), VoxelCommand.SortPack()],
        [VoxelCommand.SlotMove(MoveAction.LeftClick, ContainerIds.Equipment, rig.Flat(MoveRig.Body)), VoxelCommand.ReturnHeld()],
    ];

    [Fact]
    public void Given_ALeftClickCommand_When_Stepped_Then_TheStackIsHeld()
    {
        var rig = Rig();

        rig.Sim.Step([VoxelCommand.SlotMove(MoveAction.LeftClick, ContainerIds.Bag, 12)]);

        Assert.Equal(new ItemStack(MoveRig.Apple, 5), rig.Sim.Held.Stack);
        Assert.True(rig.Bag.Slots[12].IsEmpty);
    }

    [Fact]
    public void Given_ARefusedMove_When_Stepped_Then_NothingChangesAndOneEventCarriesTheReason()
    {
        var rig = Rig();
        rig.Sim.Step([VoxelCommand.SlotMove(MoveAction.LeftClick, ContainerIds.Bag, 12)]);
        var before = rig.Snapshot();

        rig.Sim.Step([VoxelCommand.SlotMove(MoveAction.RightClick, ContainerIds.Bag, 20)]);

        Assert.Equal(before, rig.Snapshot());
        var refused = Assert.Single(rig.Sim.Events, static e => e.Kind == SimulationEventKind.MoveRefused);
        Assert.Equal((int)MoveReason.DifferentItem, refused.Detail);
        Assert.Equal((ContainerIds.Bag, 20, (int)MoveAction.RightClick), (refused.X, refused.Y, refused.Z));
    }

    [Fact]
    public void Given_ARefusedWornPut_When_Stepped_Then_TheEventCarriesTheEquipReasonToo()
    {
        var rig = new MoveRig().With(12, GearItems.Hat, 1);
        rig.Sim.Step([VoxelCommand.SlotMove(MoveAction.LeftClick, ContainerIds.Bag, 12)]);

        rig.Sim.Step([VoxelCommand.SlotMove(MoveAction.LeftClick, ContainerIds.Equipment, rig.Flat(MoveRig.Body))]);

        var refused = Assert.Single(rig.Sim.Events, static e => e.Kind == SimulationEventKind.MoveRefused);
        Assert.Equal((int)MoveReason.SlotRefused, refused.Detail);
        Assert.Equal((int)Vocaterra.Game.Core.Items.Gear.EquipResult.WrongSlot, refused.Amount);
    }

    [Fact]
    public void Given_AShiftClickedShirt_When_Stepped_Then_TheEquippedEventAndTheStatsFollow()
    {
        var rig = new MoveRig().With(12, GearItems.Shirt, 1);

        rig.Sim.Step([VoxelCommand.SlotMove(MoveAction.QuickMove, ContainerIds.Bag, 12)]);

        var worn = Assert.Single(rig.Sim.Events, static e => e.Kind == SimulationEventKind.ItemEquipped);
        Assert.Equal((int)GearItems.Shirt, worn.Detail);
        Assert.True(rig.Sim.Stats.Get(Vocaterra.Game.Core.Stats.StatKeys.Warmth) > 0);
    }

    [Fact]
    public void Given_NothingHeldAndAnEmptyTrash_When_TheDigestIsRead_Then_ItIsTheDigestOfAFreshRunWithTheSameBag()
    {
        var plain = new MoveRig().With(12, MoveRig.Apple, 5);
        var moved = new MoveRig().With(12, MoveRig.Apple, 5);

        moved.Sim.Step([VoxelCommand.SlotMove(MoveAction.LeftClick, ContainerIds.Bag, 12)]);
        moved.Sim.Step([VoxelCommand.SlotMove(MoveAction.LeftClick, ContainerIds.Bag, 12)]);
        plain.Sim.Step();
        plain.Sim.Step();

        Assert.Equal(plain.Sim.ComputeStateDigest(), moved.Sim.ComputeStateDigest());
    }

    [Fact]
    public void Given_AHeldStack_When_TheDigestIsRead_Then_ItDiffersUntilTheStackIsReturned()
    {
        var rig = new MoveRig().With(12, MoveRig.Apple, 5);
        rig.Sim.Step();
        var before = rig.Sim.ComputeStateDigest();

        rig.Sim.Held.Put(0, new ItemStack(MoveRig.Apple, 1));
        var held = rig.Sim.ComputeStateDigest();
        rig.Sim.Held.Clear();

        Assert.NotEqual(before, held);
        Assert.Equal(before, rig.Sim.ComputeStateDigest());
    }

    [Fact]
    public void Given_AStackInTheTrash_When_TheDigestIsRead_Then_ItIsCoveredToo()
    {
        var rig = new MoveRig();
        var before = rig.Sim.ComputeStateDigest();

        rig.Sim.Trash.Put(0, new ItemStack(MoveRig.Apple, 1));

        Assert.NotEqual(before, rig.Sim.ComputeStateDigest());
    }

    [Fact]
    public void Given_OneSeedAndOneCommandListWithMoves_When_RunTwice_Then_TheDigestsAreTheSame()
    {
        var one = Rig();
        var two = Rig();

        foreach (var commands in Script(one))
        {
            one.Sim.Step(commands);
        }

        foreach (var commands in Script(two))
        {
            two.Sim.Step(commands);
        }

        Assert.Equal(one.Sim.ComputeStateDigest(), two.Sim.ComputeStateDigest());
        Assert.Equal(one.Snapshot(), two.Snapshot());
    }

    [Fact]
    public void Given_AHeldStackAndATrash_When_TheStateIsRestored_Then_BothAreCleared()
    {
        var rig = Rig();
        var state = rig.Sim.CaptureState();
        rig.Sim.Held.Put(0, new ItemStack(MoveRig.Apple, 2));
        rig.Sim.Trash.Put(0, new ItemStack(MoveRig.Bread, 2));

        rig.Sim.Restore(state);

        Assert.True(rig.Sim.Held.IsEmpty);
        Assert.True(rig.Sim.Trash.Stack.IsEmpty);
    }

    [Fact]
    public void Given_AHeldStack_When_TheStateIsCaptured_Then_TheHeldItemIsNotInIt()
    {
        var rig = Rig();
        rig.Sim.Step([VoxelCommand.SlotMove(MoveAction.LeftClick, ContainerIds.Bag, 12)]);

        var state = rig.Sim.CaptureState();

        Assert.True(state.Slots[12].IsEmpty); // the save writes what the bag holds: a save is only written after ReturnHeld
        Assert.True(rig.Sim.ReturnHeld());
        Assert.Equal(new ItemStack(MoveRig.Apple, 5), rig.Sim.CaptureState().Slots[12]);
    }

    [Fact]
    public void Given_TheReturnCommand_When_Stepped_Then_TheHeldStackGoesBack()
    {
        var rig = Rig();
        rig.Sim.Step([VoxelCommand.SlotMove(MoveAction.LeftClick, ContainerIds.Bag, 12)]);

        rig.Sim.Step([VoxelCommand.ReturnHeld()]);

        Assert.True(rig.Sim.Held.IsEmpty);
        Assert.Equal(new ItemStack(MoveRig.Apple, 5), rig.Bag.Slots[12]);
    }
}
