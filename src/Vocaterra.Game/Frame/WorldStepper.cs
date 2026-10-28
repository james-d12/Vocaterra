using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Screens;

namespace Vocaterra.Game.Frame;

/// <summary>
/// One tick of the world: the tick's commands (the script's, or the player's while no panel holds the keys), the crafts asked for, the look
/// first; the session's step; then every <see cref="ITickObserver"/>, in the order given (the observer pattern): the arm, the sounds, the figures,
/// the camera, the objective, the voice, the word cards and the hints.
/// </summary>
public sealed class WorldStepper
{
    private readonly VoxelSession _session;
    private readonly LookControl _look;
    private readonly ScenarioScript? _scenario;
    private readonly LiveVoxelInput _live;
    private readonly Func<bool> _micCheckActive;
    private readonly Func<bool> _dialogueOpen;
    private readonly CraftingPanel _crafting;
    private readonly long? _crashAfter;
    private readonly IReadOnlyList<ITickObserver> _observers;
    private readonly List<VoxelCommand> _commands = [];

    /// <summary>Creates the stepper of a session.</summary>
    /// <param name="session">The session the world is in.</param>
    /// <param name="look">The player's look, which goes first in every tick.</param>
    /// <param name="scenario">The scripted run, or <see langword="null"/> for a live one.</param>
    /// <param name="live">The player's device input as commands.</param>
    /// <param name="micCheckActive">Whether the mic check is on screen (the world takes no input then).</param>
    /// <param name="dialogueOpen">Whether the dialogue panel holds the keys (the player does not walk).</param>
    /// <param name="crafting">The crafting panel, whose clicked crafts run in the next tick.</param>
    /// <param name="crashAfter">The tick of <c>--debug-crash-after</c> (X05), or <see langword="null"/>.</param>
    /// <param name="observers">What follows the world, in order.</param>
    public WorldStepper(
        VoxelSession session,
        LookControl look,
        ScenarioScript? scenario,
        LiveVoxelInput live,
        Func<bool> micCheckActive,
        Func<bool> dialogueOpen,
        CraftingPanel crafting,
        long? crashAfter,
        IReadOnlyList<ITickObserver> observers)
    {
        _session = session;
        _look = look;
        _scenario = scenario;
        _live = live;
        _micCheckActive = micCheckActive;
        _dialogueOpen = dialogueOpen;
        _crafting = crafting;
        _crashAfter = crashAfter;
        _observers = observers;
    }

    /// <summary>Steps the world one tick and tells the observers.</summary>
    public void Step()
    {
        _commands.Clear();
        if (_scenario is { IsDone: false } && !_micCheckActive())
        {
            _scenario.Fill(_commands);
        }
        else if (!_dialogueOpen() && !_micCheckActive())
        {
            _live.Fill(_commands);
        }

        // A craft clicked or typed this frame runs in this tick, after the gathering and the placing (the simulation's order).
        _crafting.TakeCrafts(_commands);

        // The look goes first: it is absolute and every other command of the tick uses the facing it sets.
        _commands.Insert(0, VoxelCommand.Look(_look.Yaw, _look.Pitch));
        _session.Step(_commands);
        if (_crashAfter is { } crashAt && _session.Simulation.Tick >= crashAt)
        {
            // The test hook of the crash handler (X05, --debug-crash-after): the exception comes out of the game loop, as a real bug would.
            throw new InvalidOperationException($"debug crash: --debug-crash-after {crashAt} asked for it (a test hook, not a bug)");
        }

        foreach (var observer in _observers)
        {
            observer.AfterTick(_commands);
        }
    }
}
