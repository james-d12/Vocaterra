using Vocaterra.Simulation;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>What the player holds during a tick, independent of the device (keyboard, a script, later a gamepad).</summary>
[Flags]
public enum InputActions
{
    /// <summary>Nothing is held.</summary>
    None = 0,

    /// <summary>Move left.</summary>
    Left = 1,

    /// <summary>Move right.</summary>
    Right = 2,

    /// <summary>The jump button is down.</summary>
    Jump = 4,
}

/// <summary>A device-independent source of held actions, polled once per simulation tick.</summary>
public interface IInputSource
{
    /// <summary>The actions held during the tick that is about to run.</summary>
    /// <param name="tick">The simulation tick about to be stepped (its <c>Tick</c> before the step); scripts key on it.</param>
    InputActions Poll(long tick);
}

/// <summary>Maps held <see cref="InputActions"/> to the simulation's <see cref="PlayerCommand"/>s. The simulation folds left plus right itself.</summary>
public static class CommandMapper
{
    /// <summary>Clears <paramref name="commands"/> and fills it with one command per held action, in a fixed order (left, right, jump).</summary>
    public static void Fill(InputActions actions, List<PlayerCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        commands.Clear();
        if (actions.HasFlag(InputActions.Left))
        {
            commands.Add(PlayerCommand.MoveLeft);
        }

        if (actions.HasFlag(InputActions.Right))
        {
            commands.Add(PlayerCommand.MoveRight);
        }

        if (actions.HasFlag(InputActions.Jump))
        {
            commands.Add(PlayerCommand.Jump);
        }
    }
}

/// <summary>Scripted input for the screenshot run: holds 'right' for the first <c>walkRightTicks</c> ticks, then nothing.</summary>
/// <param name="WalkRightTicks">How many leading ticks hold 'right'.</param>
public sealed record ScriptedInput(int WalkRightTicks) : IInputSource
{
    /// <inheritdoc />
    public InputActions Poll(long tick) => tick < WalkRightTicks ? InputActions.Right : InputActions.None;
}
