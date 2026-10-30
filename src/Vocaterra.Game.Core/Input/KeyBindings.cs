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
        new(InputKeys.Of("endRun"), commands.EndPlaytest),
        new(InputKeys.Of("view"), commands.ToggleView),
        new(InputKeys.Of("save"), commands.Save),
        new(InputKeys.Of("textSize"), commands.CycleTextSize),
        new(InputKeys.Of("debug"), commands.ToggleDebug, () => !playtest),
    ]);

    /// <summary>The world's keys when no screen is open, in their order: the first that applies runs.</summary>
    /// <param name="commands">The commands.</param>
    /// <param name="aimedAtNpc">Whether the crosshair is on an NPC.</param>
    /// <param name="tipShown">Whether a tip is on screen.</param>
    /// <param name="atWorkbench">Whether the crosshair is on the workbench.</param>
    public static KeyBindings World(GameCommands commands, Func<bool> aimedAtNpc, Func<bool> tipShown, Func<bool> atWorkbench) => new(
    [
        new(InputKeys.Of("listen"), commands.Listen, aimedAtNpc),
        new(InputKeys.Of("type"), commands.TypeTo, aimedAtNpc),
        new(InputKeys.Of("talk"), commands.TalkTo, aimedAtNpc),
        new(InputKeys.Of("controls"), commands.OpenControls),
        new(InputKeys.Of("nextTip"), commands.NextTip, tipShown),
        new(InputKeys.Of("book"), commands.OpenBook),
        new(InputKeys.Of("crafting"), commands.OpenCrafting),
        new(InputKeys.Of("talk"), commands.OpenCrafting, atWorkbench), // the same key as talking: at the workbench it opens the recipes
        new(InputKeys.Of("inventory"), commands.OpenInventory),
        new(InputKeys.Of("talk"), commands.OpenInventory), // and anywhere else the inventory
        new(InputKeys.Of("releaseMouse"), commands.ReleaseMouse),
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
