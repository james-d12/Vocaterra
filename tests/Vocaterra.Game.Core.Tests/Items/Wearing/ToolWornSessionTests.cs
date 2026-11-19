using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Wearing;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Items.Wearing;

/// <summary>The session's side of a tool wearing out (I07): the <c>tool_worn</c> event once, and the short line the HUD says, in Spanish with its English.</summary>
public sealed class ToolWornSessionTests
{
    private static VoxelSession WithAxeOneGatherFromWornOut()
    {
        var session = TestGame.NewSession();
        var sim = session.Simulation;
        Assert.Equal(199, sim.SetWear(TestItems.Axe, 199));
        var slot = Enumerable.Range(0, sim.Inventory.SlotCount).First(i => sim.Inventory.Slots[i] is { IsEmpty: false } s && s.Item == TestItems.Axe);
        sim.Inventory.SetSlot(0, sim.Inventory.Slots[slot]);
        sim.Inventory.SetSlot(slot, ItemStack.Empty);
        session.Step([VoxelCommand.SelectSlot(0)]);
        return session;
    }

    private static void ChopOnce(VoxelSession session)
    {
        var feet = session.Simulation.Player.FeetBlock;
        var trunk = new BlockPos(feet.X, feet.Y + 1, feet.Z - 2);
        session.Scene.Grid.SetBlock(trunk.X, trunk.Y, trunk.Z, BlockId.Trunk);
        for (var i = 0; i < 20; i++)
        {
            session.Step([VoxelCommand.Interact(trunk.X, trunk.Y, trunk.Z)]);
        }
    }

    [Fact]
    public void Given_AnAxeOneGatherFromWornOut_When_ItWearsOut_Then_ToolWornIsLoggedOnceWithTheToolsNoun()
    {
        var session = WithAxeOneGatherFromWornOut();

        ChopOnce(session);
        ChopOnce(session);
        ChopOnce(session);

        var worn = session.Events.OfType<WorldEvent>().Where(static e => e.Name == WorldEvents.ToolWorn).ToList();
        Assert.Equal("hacha", Assert.Single(worn).Detail);
        Assert.Contains(WorldEvents.ToolWorn, WorldEvents.All);
    }

    [Fact]
    public void Given_AToolThatWoreOut_When_TheHudIsUpdated_Then_TheLineIsSaidOnceInSpanishWithItsEnglish()
    {
        var session = WithAxeOneGatherFromWornOut();
        var hud = new NeedsHud(session);

        ChopOnce(session);
        hud.Update(session.Simulation.Tick);

        var line = hud.Announcement!;
        Assert.Equal("Necesito arreglar la herramienta.", line.Spanish);
        Assert.Equal("I need to repair the tool.", line.English);
        Assert.Null(line.Meter);
        Assert.Equal(1, hud.TakeSounds());
        ChopOnce(session);
        hud.Update(session.Simulation.Tick + NeedsHud.DisplayTicks + 1);
        Assert.Null(hud.Announcement); // it does not come again
    }

    [Fact]
    public void Given_TheLineKey_When_TheDataIsChecked_Then_ItHasSpanishAndEnglish()
    {
        Assert.Equal("Necesito arreglar la herramienta.", TestGame.Data.Words.HudLine(WearText.ToolWorn));
        Assert.Equal("I need to repair the tool.", TestGame.Locale.Text(WearText.ToolWorn));
        Assert.Contains(WearText.ToolWorn, Core.Content.GameDataValidator.HudLineKeys(TestGame.Data));
    }

    [Fact]
    public void Given_TheNeedsOff_When_AToolWearsOut_Then_NoLineIsShownButTheEventIsLogged()
    {
        var session = WithAxeOneGatherFromWornOut();
        session.NeedsEnabled = false;
        var hud = new NeedsHud(session);

        ChopOnce(session);
        hud.Update(session.Simulation.Tick);

        Assert.Null(hud.Announcement);
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.ToolWorn);
    }
}
