using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Containers;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Items;

/// <summary>The inventory panel's clicks (I02): each becomes the command a script would send, queued for the next tick; closing queues the return of the held stack.</summary>
public sealed class InventoryPanelMoveTests
{
    private static List<VoxelCommand> Take(InventoryPanel panel)
    {
        var commands = new List<VoxelCommand>();
        panel.TakeMoves(commands);
        return commands;
    }

    private static TestInputFrame Click(bool left = true, bool shift = false) => new()
    {
        LeftDown = left,
        RightDown = !left,
        Keys = shift ? new HashSet<GameKey> { GameKey.LeftShift } : new HashSet<GameKey>(),
    };

    private static InventoryPanel PanelOver(InventoryHit hit)
    {
        var session = TestGame.NewSession();
        var screen = new InventoryScreen();
        var panel = new InventoryPanel(screen, new InputFocus(), session, static () => false, (_, _) => hit);
        panel.Open();
        return panel;
    }

    [Fact]
    public void Given_ACellClicked_When_Left_Right_Or_ShiftIsUsed_Then_TheMatchingMoveIsQueued()
    {
        var panel = PanelOver(new InventoryHit(InventoryHitKind.Cell, 14));

        panel.Click(Click());
        panel.Click(Click(left: false));
        panel.Click(Click(shift: true));

        Assert.Equal(
            [VoxelCommand.SlotMove(MoveAction.LeftClick, ContainerIds.Bag, 14), VoxelCommand.SlotMove(MoveAction.RightClick, ContainerIds.Bag, 14), VoxelCommand.SlotMove(MoveAction.QuickMove, ContainerIds.Bag, 14)],
            Take(panel));
    }

    [Fact]
    public void Given_AnEquipmentCellClicked_When_Left_Right_Or_ShiftIsUsed_Then_TheMovesNameTheEquipmentContainerAndTheFlatIndex()
    {
        var panel = PanelOver(new InventoryHit(InventoryHitKind.Equipment, 13));

        panel.Click(Click());
        panel.Click(Click(left: false));
        panel.Click(Click(shift: true));

        Assert.Equal(
            [VoxelCommand.SlotMove(MoveAction.LeftClick, ContainerIds.Equipment, 13), VoxelCommand.SlotMove(MoveAction.RightClick, ContainerIds.Equipment, 13), VoxelCommand.SlotMove(MoveAction.QuickMove, ContainerIds.Equipment, 13)],
            Take(panel));
    }

    [Fact]
    public void Given_ThePanel_When_ItOpens_Then_TheSlotLabelNounsAndTheStatsLinesNounsAreReadToo()
    {
        var session = GearItems.Session();
        session.Simulation.Equipment.Set(GearItems.Slot("armour", "body"), new ItemStack(GearItems.Plate, 1));
        session.Simulation.RefreshStats();
        var panel = new InventoryPanel(new InventoryScreen(), new InputFocus(), session, static () => false, static (_, _) => InventoryHit.None);

        panel.Open();

        Assert.True(session.Words.RecordOf("cabeza").Read > 0);
        Assert.True(session.Words.RecordOf("armadura").Read > 0);
        Assert.True(session.Words.RecordOf("calor").Read > 0);
    }

    [Fact]
    public void Given_TheSortButton_When_LeftClicked_Then_SortIsQueuedAndARightClickDoesNothing()
    {
        var panel = PanelOver(new InventoryHit(InventoryHitKind.Sort));

        panel.Click(Click(left: false));
        panel.Click(Click());

        Assert.Equal([VoxelCommand.SortPack()], Take(panel));
    }

    [Fact]
    public void Given_TheTrashCell_When_Clicked_Then_AMoveOverTheTrashIsQueued()
    {
        var panel = PanelOver(new InventoryHit(InventoryHitKind.Trash));

        panel.Click(Click());

        Assert.Equal([VoxelCommand.SlotMove(MoveAction.LeftClick, ContainerIds.Trash, 0)], Take(panel));
    }

    [Fact]
    public void Given_AClickOnNothing_When_Clicked_Then_NothingIsQueued()
    {
        var panel = PanelOver(InventoryHit.None);

        panel.Click(Click());

        Assert.Empty(Take(panel));
    }

    [Fact]
    public void Given_AnOpenPanel_When_ItIsClosed_Then_TheReturnOfTheHeldStackIsQueuedOnce()
    {
        var panel = PanelOver(InventoryHit.None);

        panel.Close();
        panel.Close();

        Assert.Equal([VoxelCommand.ReturnHeld()], Take(panel));
    }

    [Fact]
    public void Given_AClosedPanel_When_Clicked_Then_NothingIsQueued()
    {
        var panel = PanelOver(new InventoryHit(InventoryHitKind.Cell, 3));
        panel.Close();
        _ = Take(panel);

        panel.Click(Click());

        Assert.Empty(Take(panel));
    }

    [Fact]
    public void Given_AHeldStackAndTheEscapeKey_When_TheClosingCommandIsStepped_Then_TheStackIsBackInItsSlot()
    {
        var session = TestGame.NewSession();
        var panel = new InventoryPanel(new InventoryScreen(), new InputFocus(), session, static () => false, static (_, _) => InventoryHit.None);
        panel.Open();
        var sim = session.Simulation;
        sim.Inventory.SetSlot(12, new ItemStack(TestItems.Apple, 4));
        session.Step([VoxelCommand.SlotMove(MoveAction.LeftClick, ContainerIds.Bag, 12)]);
        Assert.False(sim.Held.IsEmpty);

        panel.HandleInput(TestInputFrame.Pressing(GameKey.Escape));
        var commands = Take(panel);
        session.Step(commands);

        Assert.True(sim.Held.IsEmpty);
        Assert.Equal(new ItemStack(TestItems.Apple, 4), sim.Inventory.Slots[12]);
    }
}
