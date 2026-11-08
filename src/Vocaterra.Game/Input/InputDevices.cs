using Microsoft.Xna.Framework.Input;
using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.Input;

/// <summary>
/// The keyboard and the mouse, read once a frame into an <see cref="InputSnapshot"/>; the frame commits what it read, so the next frame's
/// presses and clicks are the changes since.
/// </summary>
public sealed class InputDevices
{
    private readonly UiCanvas _canvas;
    private KeyboardState _previousKeys;
    private bool _previousLeftDown;
    private bool _previousRightDown;
    private int _previousWheel;

    /// <summary>Creates the devices of a window whose interface is laid out on <paramref name="canvas"/>.</summary>
    public InputDevices(UiCanvas canvas) => _canvas = canvas;

    /// <summary>Whether Alt is held now: an accent is Alt plus a vowel (<c>TypingMap</c>), in every text box.</summary>
    public static bool AltHeld()
    {
        var keys = Keyboard.GetState();
        return keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt);
    }

    /// <summary>When the window opens: the wheel's position then is where it starts, so it does not count as a turn.</summary>
    public void Start() => _previousWheel = Mouse.GetState().ScrollWheelValue;

    /// <summary>The devices now beside the last committed frame.</summary>
    public InputSnapshot Read() =>
        new(Keyboard.GetState(), _canvas.ToCanvas(Mouse.GetState()), _previousKeys, _previousLeftDown, _previousRightDown, _previousWheel);

    /// <summary>The frame is done with <paramref name="frame"/>: the keys, the buttons and the wheel become the next frame's previous ones.</summary>
    public void Commit(InputSnapshot frame)
    {
        _previousKeys = frame.KeyboardState;
        _previousLeftDown = frame.LeftDown;
        _previousRightDown = frame.RightDown;
        _previousWheel = frame.MouseState.ScrollWheelValue;
    }

    /// <summary>The frame read only the keys (the game is closing): the keys become the next frame's previous ones.</summary>
    public void CommitKeys(InputSnapshot frame) => _previousKeys = frame.KeyboardState;
}
