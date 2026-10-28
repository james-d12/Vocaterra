namespace Vocaterra.Game.Core.Input;

/// <summary>
/// The game's keys as commands (the command pattern): the global keys, each one on its own, and the world's keys, of which the first that
/// applies runs (a key can mean two things: E talks to the NPC under the crosshair, else opens the workbench's panel there, else the inventory).
/// </summary>
public sealed class KeyBindings
{
    /// <summary>Creates a set of bindings, in the order they are tried.</summary>
    public KeyBindings(IReadOnlyList<KeyBinding> bindings) => Bindings = bindings;

    /// <summary>The bindings, in the order they are tried.</summary>
    public IReadOnlyList<KeyBinding> Bindings { get; }

    /// <summary>The keys that work wherever the player is (but not in the report panel): F10, F5, F6, F9 and F3.</summary>
    /// <param name="commands">The commands.</param>
    /// <param name="playtest">Whether the run is a playtest (F3 is off there).</param>
    public static KeyBindings Global(GameCommands commands, bool playtest) => new(
    [
        new(GameKey.F10, commands.EndPlaytest),
        new(GameKey.F5, commands.ToggleView),
        new(GameKey.F6, commands.Save),
        new(GameKey.F9, commands.CycleTextSize),
        new(GameKey.F3, commands.ToggleDebug, () => !playtest),
    ]);

    /// <summary>The world's keys when no screen is open, in their order: the first that applies runs.</summary>
    /// <param name="commands">The commands.</param>
    /// <param name="aimedAtNpc">Whether the crosshair is on an NPC.</param>
    /// <param name="tipShown">Whether a tip is on screen.</param>
    /// <param name="atWorkbench">Whether the crosshair is on the workbench.</param>
    public static KeyBindings World(GameCommands commands, Func<bool> aimedAtNpc, Func<bool> tipShown, Func<bool> atWorkbench) => new(
    [
        new(GameKey.V, commands.Listen, aimedAtNpc),
        new(GameKey.T, commands.TypeTo, aimedAtNpc),
        new(GameKey.E, commands.TalkTo, aimedAtNpc),
        new(GameKey.F1, commands.OpenControls),
        new(GameKey.Enter, commands.NextTip, tipShown),
        new(GameKey.J, commands.OpenBook),
        new(GameKey.C, commands.OpenCrafting),
        new(GameKey.E, commands.OpenCrafting, atWorkbench),
        new(GameKey.I, commands.OpenInventory),
        new(GameKey.E, commands.OpenInventory),
        new(GameKey.Escape, commands.ReleaseMouse),
    ]);

    /// <summary>Runs every binding whose key went down this frame and whose condition holds, in order.</summary>
    public void RunEvery(IInputFrame input)
    {
        foreach (var binding in Bindings)
        {
            if (Applies(binding, input))
            {
                binding.Command.Execute();
            }
        }
    }

    /// <summary>Runs the first binding whose key went down this frame and whose condition holds. Returns whether one ran.</summary>
    public bool RunFirst(IInputFrame input)
    {
        foreach (var binding in Bindings)
        {
            if (Applies(binding, input))
            {
                binding.Command.Execute();
                return true;
            }
        }

        return false;
    }

    private static bool Applies(KeyBinding binding, IInputFrame input) => input.Pressed(binding.Key) && (binding.When?.Invoke() ?? true);
}
