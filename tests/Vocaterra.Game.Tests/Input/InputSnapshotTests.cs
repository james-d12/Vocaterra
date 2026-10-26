using Microsoft.Xna.Framework.Input;
using Vocaterra.Game.Input;

namespace Vocaterra.Game.Tests.Input;

/// <summary>A frame's input beside the last frame's: a press is a key that went down this frame, a click a button that did, and the wheel's turn the change since.</summary>
public sealed class InputSnapshotTests
{
    [Fact]
    public void AKeyIsPressedOnlyInTheFrameItGoesDownAndHeldWhileItStaysDown()
    {
        var first = Snapshot(new KeyboardState(Keys.E, Keys.LeftShift), previousKeys: new KeyboardState());
        Assert.True(first.Pressed(Keys.E));
        Assert.True(first.Down(Keys.E));
        Assert.True(first.Shift);
        Assert.False(first.Ctrl);

        var held = Snapshot(new KeyboardState(Keys.E), previousKeys: new KeyboardState(Keys.E));
        Assert.False(held.Pressed(Keys.E));
        Assert.True(held.Down(Keys.E));
        Assert.False(held.Shift);
        Assert.True(Snapshot(new KeyboardState(Keys.RightControl), previousKeys: new KeyboardState()).Ctrl);
    }

    [Fact]
    public void AClickIsAButtonThatWentDownThisFrame()
    {
        var down = Snapshot(new KeyboardState(), mouse: Mouse(left: true, right: true));
        Assert.True(down.LeftClicked);
        Assert.True(down.RightClicked);

        var held = Snapshot(new KeyboardState(), mouse: Mouse(left: true, right: true), previousLeftDown: true, previousRightDown: true);
        Assert.True(held.LeftDown);
        Assert.False(held.LeftClicked);
        Assert.True(held.RightDown);
        Assert.False(held.RightClicked);
    }

    [Fact]
    public void TheWheelIsTheTurnSinceTheLastFrame()
    {
        Assert.Equal(-240, Snapshot(new KeyboardState(), mouse: Mouse(wheel: 120), previousWheel: 360).Wheel);
        Assert.Equal(0, Snapshot(new KeyboardState(), mouse: Mouse(wheel: 120), previousWheel: 120).Wheel);
    }

    private static InputSnapshot Snapshot(KeyboardState keys, KeyboardState previousKeys = default, MouseState mouse = default, bool previousLeftDown = false, bool previousRightDown = false, int previousWheel = 0) =>
        new(keys, mouse, previousKeys, previousLeftDown, previousRightDown, previousWheel);

    private static MouseState Mouse(bool left = false, bool right = false, int wheel = 0) =>
        new(10, 20, wheel, left ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, right ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released);
}
