namespace Vocaterra.Game.Core.Input;

/// <summary>
/// The keyboard and the mouse of one frame without the engine's types (D-p2-1a): what is held, what went down this frame, the clicks, the turn
/// of the wheel and the pointer on the interface canvas. The game's frame input implements it; the screens and the bindings read it.
/// </summary>
public interface IInputFrame
{
    /// <summary>Whether either Shift is held.</summary>
    bool Shift { get; }

    /// <summary>Whether either Control is held.</summary>
    bool Ctrl { get; }

    /// <summary>Whether the left button is down.</summary>
    bool LeftDown { get; }

    /// <summary>Whether the right button is down.</summary>
    bool RightDown { get; }

    /// <summary>Whether the left button went down this frame.</summary>
    bool LeftClicked { get; }

    /// <summary>Whether the right button went down this frame.</summary>
    bool RightClicked { get; }

    /// <summary>How far the wheel turned since the last frame, in the platform's units (120 a notch on most mice; up is positive).</summary>
    int Wheel { get; }

    /// <summary>The pointer's x on the interface canvas.</summary>
    int PointerX { get; }

    /// <summary>The pointer's y on the interface canvas.</summary>
    int PointerY { get; }

    /// <summary>Whether <paramref name="key"/> is held.</summary>
    /// <param name="key">The key.</param>
    bool Down(GameKey key);

    /// <summary>Whether <paramref name="key"/> went down this frame.</summary>
    /// <param name="key">The key.</param>
    bool Pressed(GameKey key);
}
