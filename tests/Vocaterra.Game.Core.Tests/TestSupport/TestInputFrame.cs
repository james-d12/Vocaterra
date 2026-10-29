using Vocaterra.Game.Core.Input;

namespace Vocaterra.Game.Core.Tests.TestSupport;

/// <summary>
/// A frame's input for the tests, without the engine: the keys down now and in the last frame, the buttons now and in the last frame, the
/// wheel's turn and the pointer. It answers as the game's frame input does (a press is a key down now and not before, a click a button).
/// </summary>
internal sealed class TestInputFrame : IInputFrame
{
    /// <summary>The keys down this frame.</summary>
    public IReadOnlySet<GameKey> Keys { get; init; } = new HashSet<GameKey>();

    /// <summary>The keys down in the last frame.</summary>
    public IReadOnlySet<GameKey> PreviousKeys { get; init; } = new HashSet<GameKey>();

    /// <inheritdoc />
    public bool LeftDown { get; init; }

    /// <inheritdoc />
    public bool RightDown { get; init; }

    /// <summary>Whether the left button was down in the last frame.</summary>
    public bool PreviousLeftDown { get; init; }

    /// <summary>Whether the right button was down in the last frame.</summary>
    public bool PreviousRightDown { get; init; }

    /// <inheritdoc />
    public int Wheel { get; init; }

    /// <inheritdoc />
    public int PointerX { get; init; }

    /// <inheritdoc />
    public int PointerY { get; init; }

    /// <inheritdoc />
    public bool Shift => Down(GameKey.LeftShift) || Down(GameKey.RightShift);

    /// <inheritdoc />
    public bool Ctrl => Down(GameKey.LeftControl) || Down(GameKey.RightControl);

    /// <inheritdoc />
    public bool LeftClicked => LeftDown && !PreviousLeftDown;

    /// <inheritdoc />
    public bool RightClicked => RightDown && !PreviousRightDown;

    /// <summary>A frame in which <paramref name="keys"/> went down.</summary>
    /// <param name="keys">The keys.</param>
    public static TestInputFrame Pressing(params GameKey[] keys) => new() { Keys = keys.ToHashSet() };

    /// <summary>A frame in which <paramref name="keys"/> are held, as they were in the last frame.</summary>
    /// <param name="keys">The keys.</param>
    public static TestInputFrame Holding(params GameKey[] keys) => new() { Keys = keys.ToHashSet(), PreviousKeys = keys.ToHashSet() };

    /// <inheritdoc />
    public bool Down(GameKey key) => Keys.Contains(key);

    /// <inheritdoc />
    public bool Pressed(GameKey key) => Keys.Contains(key) && !PreviousKeys.Contains(key);
}
