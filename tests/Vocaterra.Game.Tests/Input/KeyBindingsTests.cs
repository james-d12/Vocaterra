using Microsoft.Xna.Framework.Input;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Input;

namespace Vocaterra.Game.Tests.Input;

/// <summary>The keys as commands: the world's first applicable key runs (E means three things), the global keys each run, and J and the book item open the same book.</summary>
public sealed class KeyBindingsTests
{
    [Fact]
    public void JAndTheBookItemBothRunTheOpenBookCommand()
    {
        var (commands, ran) = Commands();
        var world = KeyBindings.World(commands, static () => false, static () => false, static () => false);
        Assert.True(world.RunFirst(Pressed(Keys.J)));
        Assert.Equal(["OpenBook"], ran);

        var book = new ItemStack(ItemId.Book, 1);
        var rightClick = new InputSnapshot(new KeyboardState(), Mouse(right: true), new KeyboardState(), false, false, 0);
        Assert.True(BookItemUse.Applies(rightClick, captured: true, bookOpen: false, book));
        Assert.False(BookItemUse.Applies(rightClick, captured: false, bookOpen: false, book));
        Assert.False(BookItemUse.Applies(rightClick, captured: true, bookOpen: true, book));
        Assert.False(BookItemUse.Applies(rightClick, captured: true, bookOpen: false, new ItemStack(ItemId.Wood, 3)));
        var held = new InputSnapshot(new KeyboardState(), Mouse(right: true), new KeyboardState(), false, true, 0);
        Assert.False(BookItemUse.Applies(held, captured: true, bookOpen: false, book));
    }

    [Theory]
    [InlineData(true, false, "TalkTo")]
    [InlineData(false, true, "OpenCrafting")]
    [InlineData(false, false, "OpenInventory")]
    [InlineData(true, true, "TalkTo")]
    public void ETalksToTheNpcElseOpensTheWorkbenchElseTheInventory(bool aimed, bool atWorkbench, string expected)
    {
        var (commands, ran) = Commands();
        Assert.True(KeyBindings.World(commands, () => aimed, static () => false, () => atWorkbench).RunFirst(Pressed(Keys.E)));
        Assert.Equal([expected], ran);
    }

    [Fact]
    public void VAndTOnAnNpcListenOrTypeAndOnlyTheFirstApplicableKeyRuns()
    {
        var (commands, ran) = Commands();
        var aimed = KeyBindings.World(commands, static () => true, static () => false, static () => false);
        Assert.True(aimed.RunFirst(Pressed(Keys.V, Keys.T, Keys.E, Keys.J)));
        Assert.True(aimed.RunFirst(Pressed(Keys.T, Keys.E)));
        Assert.Equal(["Listen", "TypeTo"], ran);

        ran.Clear();
        var away = KeyBindings.World(commands, static () => false, static () => false, static () => false);
        Assert.False(away.RunFirst(Pressed(Keys.V)));
        Assert.False(away.RunFirst(Pressed(Keys.Enter))); // no tip on screen
        Assert.Empty(ran);
    }

    [Fact]
    public void TheGlobalKeysEachRunAndF3IsOffInAPlaytest()
    {
        var (commands, ran) = Commands();
        KeyBindings.Global(commands, playtest: false).RunEvery(Pressed(Keys.F5, Keys.F6, Keys.F3));
        Assert.Equal(["ToggleView", "Save", "ToggleDebug"], ran);

        ran.Clear();
        KeyBindings.Global(commands, playtest: true).RunEvery(Pressed(Keys.F3, Keys.F10, Keys.F9));
        Assert.Equal(["EndPlaytest", "CycleTextSize"], ran);
    }

    private static (GameCommands Commands, List<string> Ran) Commands()
    {
        var ran = new List<string>();
        IGameCommand Named(string name) => new DelegateCommand(() => ran.Add(name));
        return (new GameCommands(
            Named("ToggleView"), Named("Save"), Named("CycleTextSize"), Named("EndPlaytest"), Named("ToggleDebug"), Named("Listen"), Named("TypeTo"), Named("TalkTo"),
            Named("OpenControls"), Named("NextTip"), Named("OpenBook"), Named("OpenCrafting"), Named("OpenInventory"), Named("ReleaseMouse")), ran);
    }

    private static InputSnapshot Pressed(params Keys[] keys) => new(new KeyboardState(keys), default, new KeyboardState(), false, false, 0);

    private static MouseState Mouse(bool right) =>
        new(0, 0, 0, ButtonState.Released, ButtonState.Released, right ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released);
}
