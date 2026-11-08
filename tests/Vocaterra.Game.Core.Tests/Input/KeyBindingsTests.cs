using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Input;

/// <summary>The keys as commands: the world's first applicable key runs (E means three things), the global keys each run, and J and the book item open the same book.</summary>
public sealed class KeyBindingsTests
{
    [Fact]
    public void JAndTheBookItemBothRunTheOpenBookCommand()
    {
        var (commands, ran) = Commands();
        var world = KeyBindings.World(commands, static () => false, static () => false, static () => false);
        Assert.True(world.RunFirst(Pressed(GameKey.J)));
        Assert.Equal(["OpenBook"], ran);

        var book = new ItemStack(ItemId.Book, 1);
        var rightClick = new TestInputFrame { RightDown = true };
        Assert.True(BookItemUse.Applies(rightClick, captured: true, bookOpen: false, book));
        Assert.False(BookItemUse.Applies(rightClick, captured: false, bookOpen: false, book));
        Assert.False(BookItemUse.Applies(rightClick, captured: true, bookOpen: true, book));
        Assert.False(BookItemUse.Applies(rightClick, captured: true, bookOpen: false, new ItemStack(ItemId.Wood, 3)));
        var held = new TestInputFrame { RightDown = true, PreviousRightDown = true };
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
        Assert.True(KeyBindings.World(commands, () => aimed, static () => false, () => atWorkbench).RunFirst(Pressed(GameKey.E)));
        Assert.Equal([expected], ran);
    }

    [Fact]
    public void VAndTOnAnNpcListenOrTypeAndOnlyTheFirstApplicableKeyRuns()
    {
        var (commands, ran) = Commands();
        var aimed = KeyBindings.World(commands, static () => true, static () => false, static () => false);
        Assert.True(aimed.RunFirst(Pressed(GameKey.V, GameKey.T, GameKey.E, GameKey.J)));
        Assert.True(aimed.RunFirst(Pressed(GameKey.T, GameKey.E)));
        Assert.Equal(["Listen", "TypeTo"], ran);

        ran.Clear();
        var away = KeyBindings.World(commands, static () => false, static () => false, static () => false);
        Assert.False(away.RunFirst(Pressed(GameKey.V)));
        Assert.False(away.RunFirst(Pressed(GameKey.Enter))); // no tip on screen
        Assert.Empty(ran);
    }

    [Theory]
    [InlineData(true, false, false, "TalkTo")]
    [InlineData(false, true, false, "OpenCrafting")]
    [InlineData(false, false, true, "Sleep")]
    [InlineData(false, false, false, "OpenInventory")]
    public void EAtThePlayersLitFireSleepsAfterAnNpcAndTheWorkbenchAndBeforeTheInventory(bool aimed, bool atWorkbench, bool atFire, string expected)
    {
        var (commands, ran) = Commands();
        Assert.True(KeyBindings.World(commands, () => aimed, static () => false, () => atWorkbench, static () => false, () => atFire).RunFirst(Pressed(GameKey.E)));
        Assert.Equal([expected], ran);
    }

    [Theory]
    [InlineData(true, false, false, "TalkTo")]
    [InlineData(false, true, false, "OpenCrafting")]
    [InlineData(false, false, true, "DrinkByHand")]
    [InlineData(false, false, false, "OpenInventory")]
    public void EAtWaterDrinksByHandAfterTheNpcAndTheWorkbenchAndBeforeTheInventory(bool aimed, bool atWorkbench, bool atWater, string expected)
    {
        var (commands, ran) = Commands();
        Assert.True(KeyBindings.World(commands, () => aimed, static () => false, () => atWorkbench, static () => false, static () => false, () => atWater).RunFirst(Pressed(GameKey.E)));
        Assert.Equal([expected], ran);
    }

    [Fact]
    public void SprintIsANamedControlOnLeftShiftLikeTheOthers()
    {
        Assert.Equal(GameKey.LeftShift, InputKeys.Of("sprint"));
        Assert.Equal("Left Shift", TestGame.Data.Locale.Text("key.LeftShift"));
    }

    [Theory]
    [InlineData(true, true, true, "TalkTo")]
    [InlineData(false, true, true, "OpenStation")]
    [InlineData(false, true, false, "OpenStation")]
    [InlineData(false, false, true, "OpenCrafting")]
    [InlineData(false, false, false, "OpenInventory")]
    public void ETalksToTheNpcThenOpensAStationThenTheWorkbenchThenTheInventory(bool aimedAtNpc, bool atStation, bool atWorkbench, string expected)
    {
        var (commands, ran) = Commands();
        commands = commands with { OpenStation = new DelegateCommand(() => ran.Add("OpenStation")) }; // W07: the entry priority of design 10.8
        Assert.True(KeyBindings.World(commands, () => aimedAtNpc, static () => false, () => atWorkbench, atStation: () => atStation).RunFirst(Pressed(GameKey.E)));
        Assert.Equal([expected], ran);
    }

    [Fact]
    public void AGameWithNoStationCommandKeepsTheOldPriority()
    {
        var (commands, ran) = Commands();
        Assert.True(KeyBindings.World(commands, static () => false, static () => false, static () => true, atStation: static () => true).RunFirst(Pressed(GameKey.E)));
        Assert.Equal(["OpenCrafting"], ran);
    }

    [Fact]
    public void EnterOnTheClosingCardSleepsBeforeItShowsTheNextTipAndNothingSleepsWithoutTheOffer()
    {
        var (commands, ran) = Commands();
        Assert.True(KeyBindings.World(commands, static () => false, static () => true, static () => false, static () => true, static () => false).RunFirst(Pressed(GameKey.Enter)));
        Assert.Equal(["Sleep"], ran);

        ran.Clear();
        Assert.True(KeyBindings.World(commands, static () => false, static () => true, static () => false, static () => false, static () => false).RunFirst(Pressed(GameKey.Enter)));
        Assert.Equal(["NextTip"], ran); // the card is down: Enter is the tip's again
        ran.Clear();
        Assert.True(KeyBindings.World(commands, static () => false, static () => false, static () => false).RunFirst(Pressed(GameKey.E)));
        Assert.Equal(["OpenInventory"], ran); // a game that offers no sleep (the old bindings)
    }

    [Fact]
    public void TheGlobalKeysEachRunAndF3IsOffInAPlaytest()
    {
        var (commands, ran) = Commands();
        KeyBindings.Global(commands, playtest: false).RunEvery(Pressed(GameKey.F5, GameKey.F6, GameKey.F3));
        Assert.Equal(["ToggleView", "Save", "ToggleDebug"], ran);

        ran.Clear();
        KeyBindings.Global(commands, playtest: true).RunEvery(Pressed(GameKey.F3, GameKey.F10, GameKey.F9));
        Assert.Equal(["EndPlaytest", "CycleTextSize"], ran);
    }

    private static (GameCommands Commands, List<string> Ran) Commands()
    {
        var ran = new List<string>();
        IGameCommand Named(string name) => new DelegateCommand(() => ran.Add(name));
        return (new GameCommands(
            Named("ToggleView"), Named("Save"), Named("CycleTextSize"), Named("EndPlaytest"), Named("ToggleDebug"), Named("Listen"), Named("TypeTo"), Named("TalkTo"),
            Named("OpenControls"), Named("NextTip"), Named("OpenBook"), Named("OpenCrafting"), Named("OpenInventory"), Named("ReleaseMouse"), Named("Sleep"), Named("DrinkByHand")), ran);
    }

    private static TestInputFrame Pressed(params GameKey[] keys) => TestInputFrame.Pressing(keys);

}
