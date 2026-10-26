using Microsoft.Xna.Framework.Input;
using Vocaterra.Game.Input;

namespace Vocaterra.Game.Tests.Input;

/// <summary>The world's input rules: WASD or the arrows walk and Space jumps; the number keys and the wheel pick the hotbar slot.</summary>
public sealed class WorldInputTests
{
    [Fact]
    public void WasdAndTheArrowsWalkAndOppositeKeysCancel()
    {
        Assert.Equal((1, 1, false), WorldInput.Walk(Held(Keys.D, Keys.W)));
        Assert.Equal((-1, -1, true), WorldInput.Walk(Held(Keys.Left, Keys.Down, Keys.Space)));
        Assert.Equal((0, 0, false), WorldInput.Walk(Held(Keys.A, Keys.D, Keys.W, Keys.S)));
        Assert.Equal((0, 0, false), WorldInput.Walk(Held()));
    }

    [Fact]
    public void TheNumberKeysPickASlotAndTheWheelStepsAroundTheHotbar()
    {
        Assert.Equal(0, WorldInput.Slot(Pressed(Keys.D1), selected: 4));
        Assert.Equal(9, WorldInput.Slot(Pressed(Keys.D0), selected: 4));
        Assert.Null(WorldInput.Slot(Pressed(), selected: 4));
        Assert.Equal(3, WorldInput.Slot(Wheel(120), selected: 4)); // a turn up: the slot before
        Assert.Equal(5, WorldInput.Slot(Wheel(-120), selected: 4));
        Assert.Equal(9, WorldInput.Slot(Wheel(120), selected: 0)); // around the end
        Assert.Equal(0, WorldInput.Slot(Wheel(-120), selected: 9));
    }

    private static InputSnapshot Held(params Keys[] keys) => new(new KeyboardState(keys), default, new KeyboardState(keys), false, false, 0);

    private static InputSnapshot Pressed(params Keys[] keys) => new(new KeyboardState(keys), default, new KeyboardState(), false, false, 0);

    private static InputSnapshot Wheel(int turn) =>
        new(new KeyboardState(), new MouseState(0, 0, turn, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), new KeyboardState(), false, false, 0);
}
