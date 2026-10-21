using Microsoft.Xna.Framework.Input;
using Vocaterra.MonoGame.Frontend;

namespace Vocaterra.MonoGame;

/// <summary>
/// The keyboard as an <see cref="IInputSource"/>: A or Left arrow, D or Right arrow, Space, W or Up arrow (jump). The only place that knows the
/// key bindings. Polled once per tick, so the state is the one read at the start of the frame.
/// </summary>
public sealed class KeyboardInput : IInputSource
{
    /// <inheritdoc />
    public InputActions Poll(long tick)
    {
        var keys = Keyboard.GetState();
        var actions = InputActions.None;
        if (keys.IsKeyDown(Keys.A) || keys.IsKeyDown(Keys.Left))
        {
            actions |= InputActions.Left;
        }

        if (keys.IsKeyDown(Keys.D) || keys.IsKeyDown(Keys.Right))
        {
            actions |= InputActions.Right;
        }

        if (keys.IsKeyDown(Keys.Space) || keys.IsKeyDown(Keys.W) || keys.IsKeyDown(Keys.Up))
        {
            actions |= InputActions.Jump;
        }

        return actions;
    }
}
