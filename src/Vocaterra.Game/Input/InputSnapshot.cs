using Microsoft.Xna.Framework.Input;
using Vocaterra.Game.Core.Input;

namespace Vocaterra.Game.Input;

/// <summary>
/// The keyboard and the mouse of one frame beside the last frame's (<see cref="InputDevices"/>): what is held, what was pressed this frame, the
/// clicks and the turn of the wheel. The pointer is on the interface canvas (X08).
/// </summary>
public sealed class InputSnapshot : IInputFrame
{
    private readonly KeyboardState _previousKeys;
    private readonly bool _previousLeftDown;
    private readonly bool _previousRightDown;
    private readonly int _previousWheel;

    /// <summary>Creates the input of a frame.</summary>
    /// <param name="keyboardState">The keyboard now.</param>
    /// <param name="mouseState">The mouse now, its pointer on the canvas.</param>
    /// <param name="previousKeys">The keyboard of the last frame.</param>
    /// <param name="previousLeftDown">Whether the left button was down in the last frame.</param>
    /// <param name="previousRightDown">Whether the right button was down in the last frame.</param>
    /// <param name="previousWheel">The wheel's value in the last frame.</param>
    public InputSnapshot(KeyboardState keyboardState, MouseState mouseState, KeyboardState previousKeys, bool previousLeftDown, bool previousRightDown, int previousWheel)
    {
        KeyboardState = keyboardState;
        MouseState = mouseState;
        _previousKeys = previousKeys;
        _previousLeftDown = previousLeftDown;
        _previousRightDown = previousRightDown;
        _previousWheel = previousWheel;
    }

    /// <summary>The keyboard now.</summary>
    public KeyboardState KeyboardState { get; }

    /// <summary>The mouse now, its pointer on the interface canvas.</summary>
    public MouseState MouseState { get; }

    /// <summary>Whether either Shift is held.</summary>
    public bool Shift => Down(Keys.LeftShift) || Down(Keys.RightShift);

    /// <summary>Whether either Control is held.</summary>
    public bool Ctrl => Down(Keys.LeftControl) || Down(Keys.RightControl);

    /// <summary>Whether the left button is down.</summary>
    public bool LeftDown => MouseState.LeftButton == ButtonState.Pressed;

    /// <summary>Whether the right button is down.</summary>
    public bool RightDown => MouseState.RightButton == ButtonState.Pressed;

    /// <summary>Whether the left button went down this frame.</summary>
    public bool LeftClicked => LeftDown && !_previousLeftDown;

    /// <summary>Whether the right button went down this frame.</summary>
    public bool RightClicked => RightDown && !_previousRightDown;

    /// <summary>How far the wheel turned since the last frame, in the platform's units (120 a notch on most mice; up is positive).</summary>
    public int Wheel => MouseState.ScrollWheelValue - _previousWheel;

    /// <inheritdoc />
    public int PointerX => MouseState.X;

    /// <inheritdoc />
    public int PointerY => MouseState.Y;

    /// <summary>Whether <paramref name="key"/> is held.</summary>
    public bool Down(Keys key) => KeyboardState.IsKeyDown(key);

    /// <summary>Whether <paramref name="key"/> went down this frame.</summary>
    public bool Pressed(Keys key) => KeyboardState.IsKeyDown(key) && !_previousKeys.IsKeyDown(key);

    /// <inheritdoc />
    public bool Down(GameKey key) => Down(KeyMap.ToKeys(key));

    /// <inheritdoc />
    public bool Pressed(GameKey key) => Pressed(KeyMap.ToKeys(key));
}
