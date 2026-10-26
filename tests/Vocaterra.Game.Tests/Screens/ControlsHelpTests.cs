using Microsoft.Xna.Framework.Input;
using Vocaterra.Game.Input;
using Vocaterra.Game.Screens;

namespace Vocaterra.Game.Tests.Screens;

/// <summary>The controls help: the keys and the wheel scroll it within what the canvas leaves, and Esc or F1 closes it back to the top.</summary>
public sealed class ControlsHelpTests
{
    [Fact]
    public void TheListScrollsWithinItsBoundsAndClosingReturnsToTheTop()
    {
        var help = new ControlsHelp(static () => 6);
        help.Open();
        Assert.True(help.IsOpen);
        help.HandleInput(Pressed(Keys.PageDown));
        Assert.Equal(5, help.Scroll);
        help.HandleInput(Pressed(Keys.PageDown));
        Assert.Equal(6, help.Scroll);
        help.HandleInput(new InputSnapshot(new KeyboardState(), new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), new KeyboardState(), false, false, -360));
        Assert.Equal(3, help.Scroll); // three notches up
        help.HandleInput(Pressed(Keys.Up));
        help.HandleInput(Pressed(Keys.PageUp));
        Assert.Equal(0, help.Scroll);

        help.HandleInput(Pressed(Keys.Down));
        help.HandleInput(Pressed(Keys.F1));
        Assert.False(help.IsOpen);
        Assert.Equal(0, help.Scroll);
    }

    private static InputSnapshot Pressed(params Keys[] keys) => new(new KeyboardState(keys), default, new KeyboardState(), false, false, 0);
}
