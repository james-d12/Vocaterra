using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Screens;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Screens;

/// <summary>The controls help: the keys and the wheel scroll it within what the canvas leaves, and Esc or F1 closes it back to the top.</summary>
public sealed class ControlsHelpTests
{
    [Fact]
    public void TheListScrollsWithinItsBoundsAndClosingReturnsToTheTop()
    {
        var help = new ControlsHelp(static () => 6);
        help.Open();
        Assert.True(help.IsOpen);
        help.HandleInput(Pressed(GameKey.PageDown));
        Assert.Equal(5, help.Scroll);
        help.HandleInput(Pressed(GameKey.PageDown));
        Assert.Equal(6, help.Scroll);
        help.HandleInput(new TestInputFrame { Wheel = 360 }); // the wheel's value went from -360 to 0
        Assert.Equal(3, help.Scroll); // three notches up
        help.HandleInput(Pressed(GameKey.Up));
        help.HandleInput(Pressed(GameKey.PageUp));
        Assert.Equal(0, help.Scroll);

        help.HandleInput(Pressed(GameKey.Down));
        help.HandleInput(Pressed(GameKey.F1));
        Assert.False(help.IsOpen);
        Assert.Equal(0, help.Scroll);
    }

    private static TestInputFrame Pressed(params GameKey[] keys) => TestInputFrame.Pressing(keys);
}
