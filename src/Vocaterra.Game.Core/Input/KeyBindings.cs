namespace Vocaterra.Game.Core.Input;

/// <summary>
/// The game's keys as commands (the command pattern): the global keys, each one on its own, and the world's keys, of which the first that
/// applies runs (a key can mean several things: E talks to the NPC under the crosshair, else opens a station's overlay at its prop (the oven, W07), else opens the workbench's panel there, else sleeps at the player's own lit fire, else drinks by hand at water,
/// else opens the inventory).
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
        ..(commands.ToggleViewEffects is { } effects ? new[] { new KeyBinding(InputKeys.Of("viewEffects"), effects) } : []),
    ]);

    /// <summary>The world's keys when no screen is open, in their order: the first that applies runs.</summary>
    /// <param name="commands">The commands.</param>
    /// <param name="aimedAtNpc">Whether the crosshair is on an NPC.</param>
    /// <param name="tipShown">Whether a tip is on screen.</param>
    /// <param name="atWorkbench">Whether the crosshair is on the workbench.</param>
    /// <param name="cardOffersSleep">Whether the session's closing card offers the sleep (V12c): Enter sleeps, before it shows the next tip; none when <see langword="null"/>.</param>
    /// <param name="fireOffersSleep">Whether the player's own lit fire offers the sleep (V12c): E sleeps, after an NPC and the workbench; none when <see langword="null"/>.</param>
    /// <param name="aimedAtWater">Whether the crosshair is on water within reach (N02): E drinks by hand, before the inventory; none when <see langword="null"/>.</param>
    /// <param name="aimedAtChest">Whether the crosshair is on the belongings chest within reach (N04): E takes back what the bag can hold, before the inventory; none when <see langword="null"/>.</param>
    /// <param name="atStation">Whether the crosshair is on a cell of a station's prop within reach (W07, the oven): E opens the station, after an NPC and before the workbench; none when <see langword="null"/>.</param>
    public static KeyBindings World(GameCommands commands, Func<bool> aimedAtNpc, Func<bool> tipShown, Func<bool> atWorkbench, Func<bool>? cardOffersSleep = null, Func<bool>? fireOffersSleep = null, Func<bool>? aimedAtWater = null, Func<bool>? aimedAtChest = null, Func<bool>? atStation = null)
    {
        ArgumentNullException.ThrowIfNull(commands);
        var bindings = new List<KeyBinding>
        {
            new(InputKeys.Of("listen"), commands.Listen, aimedAtNpc),
            new(InputKeys.Of("type"), commands.TypeTo, aimedAtNpc),
            new(InputKeys.Of("talk"), commands.TalkTo, aimedAtNpc),
        };
        if (commands.Sleep is { } sleep && cardOffersSleep is not null)
        {
            bindings.Add(new(InputKeys.Of("sleep"), sleep, cardOffersSleep)); // Enter on the closing card: the next session
        }

        bindings.AddRange(
        [
            new(InputKeys.Of("controls"), commands.OpenControls),
            new(InputKeys.Of("nextTip"), commands.NextTip, tipShown),
            new(InputKeys.Of("book"), commands.OpenBook),
            new(InputKeys.Of("crafting"), commands.OpenCrafting),
        ]);
        if (commands.OpenStation is { } station && atStation is not null)
        {
            bindings.Add(new(InputKeys.Of("talk"), station, atStation)); // W07: at a station's prop it opens the station, after an NPC and before the workbench
        }

        bindings.Add(new(InputKeys.Of("talk"), commands.OpenCrafting, atWorkbench)); // the same key as talking: at the workbench it opens the recipes
        if (commands.Sleep is { } sleepAtFire && fireOffersSleep is not null)
        {
            bindings.Add(new(InputKeys.Of("talk"), sleepAtFire, fireOffersSleep)); // at the player's own lit fire it sleeps (V12c)
        }

        if (commands.DrinkByHand is { } drink && aimedAtWater is not null)
        {
            bindings.Add(new(InputKeys.Of("talk"), drink, aimedAtWater)); // at water it drinks by hand (N02)
        }

        if (commands.TakeBelongings is { } take && aimedAtChest is not null)
        {
            bindings.Add(new(InputKeys.Of("talk"), take, aimedAtChest)); // at the chest a death left it takes back the belongings (N04)
        }

        bindings.AddRange(
        [
            new(InputKeys.Of("inventory"), commands.OpenInventory),
            new(InputKeys.Of("talk"), commands.OpenInventory), // and anywhere else the inventory
            new(InputKeys.Of("menu"), commands.OpenMenu),
        ]);
        return new KeyBindings(bindings);
    }

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
