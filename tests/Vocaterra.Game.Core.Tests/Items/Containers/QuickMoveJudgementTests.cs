using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Containers;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tests.Voxel;

namespace Vocaterra.Game.Core.Tests.Items.Containers;

/// <summary>
/// The three behaviours I02 left open, judged in I04 and kept as they are, each with the reason and a test that pins it: a shift-click on a stack of several wearables is refused (it does not wear one, the player picks one up
/// with a right click), a wearable the rules refuse does not fall through to the hotbar move (it would move the item the player meant to wear), and a right click into the trash refuses another item (only a whole stack replaces it).
/// </summary>
public sealed class QuickMoveJudgementTests
{
    private static readonly ItemId Mittens = (ItemId)230;

    private static VoxelSimulation Sim() => VoxelSimulation.Create(
        VoxelTestSupport.FlatScene(),
        TestItems.With(new ItemDefinition(Mittens, "test-mittens", 5, "mittens", ItemHeld.Sprite, ["clothing"], Wear: "head")),
        equipment: GearItems.Data);

    [Fact]
    public void Given_AStackOfSeveralWearables_When_ShiftClicked_Then_ItIsRefusedAndNothingMoves()
    {
        var sim = Sim();
        sim.Inventory.SetSlot(14, new ItemStack(Mittens, 3));

        var result = sim.Move(MoveAction.QuickMove, ContainerIds.Bag, 14);

        Assert.False(result.IsDone);
        Assert.Equal(EquipResult.StackTooLarge, result.Equip);
        Assert.Equal(new ItemStack(Mittens, 3), sim.Inventory.Slots[14]);
        Assert.True(sim.Equipment.IsEmpty);
        Assert.Contains(sim.Events, static e => e.Kind == Core.Event.SimulationEventKind.MoveRefused);
    }

    [Fact]
    public void Given_AWearableTheRulesRefuse_When_ShiftClicked_Then_ItDoesNotFallThroughToTheHotbar()
    {
        var sim = Sim();
        sim.Inventory.SetSlot(14, new ItemStack(Mittens, 3));
        sim.Inventory.SetSlot(2, new ItemStack(TestItems.Apple, 1));

        _ = sim.Move(MoveAction.QuickMove, ContainerIds.Bag, 14);

        Assert.Equal(new ItemStack(Mittens, 3), sim.Inventory.Slots[14]); // not in the hotbar's free slots
        Assert.True(sim.Inventory.Slots.Take(10).All(static s => s.IsEmpty || s.Item == TestItems.Apple));
    }

    [Fact]
    public void Given_AFilledTrash_When_AnotherItemIsPutDownOneByOne_Then_ItIsRefusedAndTheBinKeepsItsItem()
    {
        var sim = Sim();
        sim.Trash.Put(0, new ItemStack(TestItems.Bread, 2));
        sim.Inventory.SetSlot(12, new ItemStack(TestItems.Apple, 3));
        _ = sim.Move(MoveAction.LeftClick, ContainerIds.Bag, 12);

        var result = sim.Move(MoveAction.RightClick, ContainerIds.Trash, 0);

        Assert.Equal(MoveReason.DifferentItem, result.Reason);
        Assert.Equal(new ItemStack(TestItems.Bread, 2), sim.Trash.Stack);
        Assert.Equal(new ItemStack(TestItems.Apple, 3), sim.Held.Stack);
    }
}
