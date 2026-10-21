using Vocaterra.MonoGame;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Simulation;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.MonoGame.Tests;

/// <summary>The P6 world flags of the command line and the mouse and number-key input in plain values.</summary>
public sealed class WorldInputTests
{
    [Fact]
    public void TheWorldFlagsParseInOrderWithTheDialogueSteps()
    {
        var args = new[] { "--screenshot", "x.png", "--select", "1", "--mine", "517,182", "--place", "madera@488,185", "--walk", "490", "--inventory", "--say", "hola" };

        Assert.True(LaunchOptions.TryParse(args, out var options, out var error), error);
        Assert.Equal(
            [ScriptStepKind.Select, ScriptStepKind.Mine, ScriptStepKind.Place, ScriptStepKind.Walk, ScriptStepKind.Inventory, ScriptStepKind.Say],
            options.Steps.Select(static s => s.Kind));
        Assert.Equal(1, options.Steps[0].Slot);
        Assert.Equal(new TilePoint(517, 182), options.Steps[1].Tile);
        Assert.Equal("madera", options.Steps[2].Item);
        Assert.Equal(new TilePoint(488, 185), options.Steps[2].Tile);
        Assert.Equal(490, options.Steps[3].Tile!.Value.X);
        Assert.Equal("hola", options.Steps[5].Line);
    }

    [Theory]
    [InlineData("--select", "11")]
    [InlineData("--select", "0")]
    [InlineData("--mine", "517")]
    [InlineData("--place", "madera")]
    [InlineData("--place", "@1,2")]
    [InlineData("--walk", "x")]
    public void BadWorldFlagsAreRefusedWithAReason(string flag, string value)
    {
        Assert.False(LaunchOptions.TryParse(["--screenshot", "x.png", flag, value], out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void WorldFlagsNeedTheScreenshotRun()
    {
        Assert.False(LaunchOptions.TryParse(["--select", "1"], out _, out var error));
        Assert.Contains("--screenshot", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSelectionAndThePlacementAreTakenOnceAndTheMiningFollowsTheHeldButton()
    {
        var input = new LiveWorldInput();
        var commands = new List<PlayerCommand>();
        input.SetFrame(new TilePoint(5, 6), mineHeld: true, placePressed: true, selectSlot: 2);
        input.Fill(commands);
        Assert.Equal([PlayerCommand.SelectSlot(2), PlayerCommand.Mine(5, 6), PlayerCommand.Place(5, 6)], commands);

        commands.Clear();
        input.SetFrame(new TilePoint(6, 6), mineHeld: true, placePressed: false, selectSlot: null);
        input.Fill(commands);
        Assert.Equal([PlayerCommand.Mine(6, 6)], commands);

        commands.Clear();
        input.SetFrame(new TilePoint(6, 6), mineHeld: false, placePressed: false, selectSlot: null);
        input.Fill(commands);
        Assert.Empty(commands);
    }

    [Fact]
    public void APressWithNoTileUnderTheMouseDoesNotPlace()
    {
        var input = new LiveWorldInput();
        var commands = new List<PlayerCommand>();
        input.SetFrame(null, mineHeld: true, placePressed: true, selectSlot: null);
        input.Fill(commands);
        Assert.Empty(commands);
    }
}
