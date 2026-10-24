using Vocaterra.Game;
using Vocaterra.Game.Language.Checks;
using Vocaterra.MonoGame.UI;

namespace Vocaterra.MonoGame.Utilities;

/// <summary>
/// What the scripted check asks of the game around it: pick an option, send a typed or a spoken answer, take a screenshot. The game supplies
/// these (the check screen, the worker and the back buffer are its), so the script stays free of the engine.
/// </summary>
/// <param name="Pick">Picks the option at the position given (1 to 3, as shown).</param>
/// <param name="Typed">Sends the line as a typed production answer.</param>
/// <param name="Speak">Synthesizes the line and sends it as a spoken production answer.</param>
/// <param name="TakeShot">Asks for a screenshot of the next drawn frame.</param>
/// <param name="ShotPending">Whether the last requested screenshot has not been saved yet.</param>
public sealed record CheckScriptHooks(Action<int> Pick, Action<string> Typed, Action<string> Speak, Action<string> TakeShot, Func<bool> ShotPending);

/// <summary>
/// The scripted answers of a check battery (<c>--check</c> with <c>--pick</c>, <c>--say</c>, <c>--speak</c> and <c>--shot</c> steps; S11). It takes
/// the script's steps in order while the battery runs: a pick answers a comprehension item, a say or a speak answers a production item, a shot
/// saves the screen and a wait waits. It stops (<see cref="Rests"/>) at a step that is not a check step, or at the end of the steps, or when the
/// battery is waiting for a kind of answer the next step does not give. The world script then starts from <see cref="Index"/> once the battery
/// is done. Nothing here reads the clock: a wait counts frames.
/// </summary>
public sealed class CheckScript
{
    /// <summary>The frames per second a <c>--wait</c> counts in (the frame clock of the scripted run is fixed at the simulation's rate).</summary>
    public const int FramesPerSecond = 60;

    private readonly IReadOnlyList<ScriptStep> _steps;
    private readonly CheckScreen _check;
    private readonly CheckScriptHooks _hooks;
    private readonly Action<string> _log;
    private int _wait;
    private int _loggedAt = -1;

    /// <summary>Creates the script over the run's steps and the battery.</summary>
    /// <param name="steps">Every step of the run, in order.</param>
    /// <param name="check">The battery.</param>
    /// <param name="hooks">What the script asks of the game.</param>
    /// <param name="log">Where the script reports a step it cannot take (the console).</param>
    /// <param name="startIndex">The first step the battery takes (0 for the pre-check; the playtest's post-check starts where the world script stopped, S12).</param>
    public CheckScript(IReadOnlyList<ScriptStep> steps, CheckScreen check, CheckScriptHooks hooks, Action<string> log, int startIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(hooks);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startIndex, steps.Count);
        _steps = steps;
        _check = check;
        _hooks = hooks;
        _log = log;
        Index = startIndex;
    }

    /// <summary>The next step the battery does not take (the world script starts there when the battery is done).</summary>
    public int Index { get; private set; }

    /// <summary>
    /// Whether the script has stopped for this frame: every step is taken, or the next step is one the battery cannot take now (a world step, or
    /// an answer of the other kind). A screenshot run takes its picture then.
    /// </summary>
    public bool Rests { get; private set; }

    /// <summary>Takes the steps the battery can take now. Call it once per frame while the battery runs.</summary>
    public void Fill()
    {
        Rests = false;
        while (true)
        {
            if (_check.IsDone)
            {
                return;
            }

            if (_wait > 0)
            {
                _wait--;
                return;
            }

            if (Index >= _steps.Count)
            {
                Rests = true;
                return;
            }

            if (_check.Phase == CheckPhase.Thinking)
            {
                return; // the answer is with the worker: the next step waits for it
            }

            var step = _steps[Index];
            switch (step.Kind)
            {
                case ScriptStepKind.Pick:
                    if (!Answerable(CheckItemKind.Comprehension))
                    {
                        Stop(step, "a pick needs a comprehension item on the screen");
                        return;
                    }

                    _hooks.Pick(int.Parse(step.Value, System.Globalization.CultureInfo.InvariantCulture));
                    Index++;
                    continue;
                case ScriptStepKind.Say:
                    if (!Answerable(CheckItemKind.Production))
                    {
                        Stop(step, "a line needs a production item on the screen");
                        return;
                    }

                    _hooks.Typed(step.Value);
                    Index++;
                    return;
                case ScriptStepKind.Speak:
                    if (!Answerable(CheckItemKind.Production))
                    {
                        Stop(step, "a spoken line needs a production item on the screen");
                        return;
                    }

                    _hooks.Speak(step.Value);
                    Index++;
                    return;
                case ScriptStepKind.Shot:
                    if (_hooks.ShotPending())
                    {
                        return;
                    }

                    _hooks.TakeShot(step.Value);
                    Index++;
                    return;
                case ScriptStepKind.Wait:
                    _wait = int.Parse(step.Value, System.Globalization.CultureInfo.InvariantCulture) * FramesPerSecond;
                    Index++;
                    return;
                default:
                    // A world step: the battery must be done first; the world script takes it from here.
                    Rests = true;
                    return;
            }
        }
    }

    // Whether the battery is showing an item of the kind an answer step gives.
    private bool Answerable(CheckItemKind kind) => _check.Phase == CheckPhase.Showing && _check.Current is { } entry && entry.Item.Kind == kind;

    // A step the battery cannot take on this item: the script waits on it (the learner may answer it on the screen), and the reason goes to the console.
    private void Stop(ScriptStep step, string reason)
    {
        if (_loggedAt != Index)
        {
            _log($"check: waiting on the step {step.Kind} ({reason}); the item on the screen is {_check.Current?.Item.Id ?? "none"}");
            _loggedAt = Index;
        }

        Rests = true;
    }
}
