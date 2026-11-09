using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Input;

/// <summary>The control table is the one place the keys are set: the bindings and the names read it, and the controls page is made from it.</summary>
public sealed class ControlTableTests
{
    [Fact]
    public void Given_TheTable_When_Read_Then_EveryControlHasAUniqueNameAndSomethingToPress()
    {
        Assert.Equal(ControlTable.All.Count, ControlTable.All.Select(static e => e.Id).Distinct().Count());
        Assert.All(ControlTable.All, static e => Assert.True(e.Keys.Count > 0 || e.Mouse != MouseControl.None, e.Id));
        Assert.All(ControlTable.All.SelectMany(static e => e.Keys), static key => Assert.NotEqual(GameKey.None, key));
    }

    [Fact]
    public void Given_TheTable_When_TheNamedKeysAreAsked_Then_TheyAreTheTablesMainKeys()
    {
        foreach (var entry in ControlTable.All.Where(static e => e.Keys.Count > 0))
        {
            Assert.Equal(entry.Key, InputKeys.Of(entry.Id));
        }

        Assert.Equal(GameKey.Escape, InputKeys.Of("menu"));
    }

    [Fact]
    public void Given_TheWorldsBindings_When_EscIsPressed_Then_TheMenuOpens()
    {
        var ran = new List<string>();
        IGameCommand Named(string name) => new DelegateCommand(() => ran.Add(name));
        var commands = new GameCommands(Named("ToggleView"), Named("Save"), Named("CycleTextSize"), Named("EndPlaytest"), Named("ToggleDebug"), Named("Listen"), Named("TypeTo"), Named("TalkTo"),
            Named("NextTip"), Named("OpenBook"), Named("OpenCrafting"), Named("OpenInventory"), Named("OpenMenu"));
        Assert.True(KeyBindings.World(commands, static () => false, static () => false, static () => false).RunFirst(TestInputFrame.Pressing(GameKey.Escape)));
        Assert.Equal(["OpenMenu"], ran);
    }

    [Fact]
    public void Given_TheKeypad_When_ADigitIsMapped_Then_ItGivesTheKeypadDigit()
    {
        Assert.Equal(GameKey.NumPad2, ControlTable.PadOf(GameKey.D2));
        Assert.Equal(GameKey.None, ControlTable.PadOf(GameKey.E));
    }

    [Fact]
    public void Given_TheShippedLocale_When_EveryControlIsDescribed_Then_NoStringIsMissing()
    {
        var locale = TestGame.Locale;
        foreach (var id in ControlsListing.UiKeys)
        {
            Assert.True(locale.Has("ui." + id), id);
        }

        foreach (var key in ControlTable.All.SelectMany(static e => e.Keys).Distinct())
        {
            Assert.True(locale.Has("key." + key), key.ToString());
        }
    }
}
