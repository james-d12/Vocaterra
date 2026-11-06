using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Screens;
using Vocaterra.Game.Core.Session;

namespace Vocaterra.Game.Core.Activities;

/// <summary>Where a click landed on the station overlay (the layout, in the game, answers it).</summary>
public enum StationRegion
{
    /// <summary>A cell of the page of what can be held.</summary>
    Held,

    /// <summary>The empty hand.</summary>
    Hand,

    /// <summary>A target.</summary>
    Target,

    /// <summary>The Repetir button.</summary>
    Repeat,

    /// <summary>The Más despacio button.</summary>
    Slower,

    /// <summary>The Ayuda button.</summary>
    Help,

    /// <summary>The previous page.</summary>
    PagePrevious,

    /// <summary>The next page.</summary>
    PageNext,

    /// <summary>A verb of the open choice.</summary>
    Verb,
}

/// <summary>A click's target on the overlay.</summary>
/// <param name="Region">What was hit.</param>
/// <param name="Index">Which one (the cell of the page, the target, the verb), 0 for the rest.</param>
public readonly record struct StationHit(StationRegion Region, int Index);

/// <summary>How the learner asked to talk to the teacher from the station (W08's hook).</summary>
public enum StationTalk
{
    /// <summary>V or Space: speak (push to talk).</summary>
    Speak,

    /// <summary>W: type.</summary>
    Type,
}

/// <summary>
/// The station overlay (W07, design 10.8) over a procedure at a station: the keys are 1 to 9 and 0 to hold, Page Up and Page Down to page, Tab and Shift+Tab to move between targets,
/// Enter to use the selection, H, R and S for help, repeat and slower, V or Space and W to talk to the teacher (a hook for W08: <see cref="OnTalk"/>), and Esc to leave with the bake
/// saved. Every act goes to <see cref="GuideSession.ActInProcedure"/> with how the instruction was presented (decision 19): heard when its audio played, read when its Spanish was on
/// screen. The panel is the pattern of <see cref="Items.Crafting.CraftingPanel"/>: the state is <see cref="StationScreen"/>, the look is the game's.
/// </summary>
public sealed class StationPanel : IScreen
{
    private static readonly GameKey[] Digits = [GameKey.D0, GameKey.D1, GameKey.D2, GameKey.D3, GameKey.D4, GameKey.D5, GameKey.D6, GameKey.D7, GameKey.D8, GameKey.D9];
    private static readonly GameKey[] Pad = [GameKey.NumPad0, GameKey.NumPad1, GameKey.NumPad2, GameKey.NumPad3, GameKey.NumPad4, GameKey.NumPad5, GameKey.NumPad6, GameKey.NumPad7, GameKey.NumPad8, GameKey.NumPad9];

    private readonly StationScreen _screen;
    private readonly VoxelSession _session;
    private readonly IInstructionAudio _audio;
    private readonly Func<int, int, StationHit?> _hitAt;
    private readonly Func<bool> _otherPanelOpen;

    /// <summary>Creates the panel's behaviour.</summary>
    /// <param name="screen">The overlay's state.</param>
    /// <param name="session">The session whose procedure is worked.</param>
    /// <param name="audio">The rows' sound.</param>
    /// <param name="hitAt">What is under a point of the canvas (the layout, from the game), or <see langword="null"/>.</param>
    /// <param name="otherPanelOpen">Whether the book, the inventory, the crafting panel or the dialogue is open (the overlay does not open over them).</param>
    public StationPanel(StationScreen screen, VoxelSession session, IInstructionAudio audio, Func<int, int, StationHit?> hitAt, Func<bool> otherPanelOpen)
    {
        _screen = screen;
        _session = session;
        _audio = audio;
        _hitAt = hitAt;
        _otherPanelOpen = otherPanelOpen;
    }

    /// <inheritdoc />
    public string Name => "station";

    /// <inheritdoc />
    public bool IsOpen => _screen.IsOpen;

    /// <inheritdoc />
    public bool FreesMouse => true;

    /// <summary>
    /// The hook W08 fills: V, Space and W call it so that the teacher hears the learner from the station (the push to talk, the typed line). Until then the keys do nothing.
    /// </summary>
    public Action<StationTalk>? OnTalk { get; set; }

    /// <summary>What the overlay shows now (the layer reads it every frame; it changes nothing but a selection that no longer exists).</summary>
    public StationView View() => _screen.View(_session, _session.Simulation.Inventory, _audio);

    /// <summary>
    /// Opens the station with the first procedure done there and starts it (or goes on with the run in progress). Nothing opens when another panel is open, when the station is
    /// unknown or has no procedure, or when the run cannot start.
    /// </summary>
    /// <param name="stationId">The station's id.</param>
    /// <param name="teacherPresent">Whether the teacher is with the learner (W08).</param>
    /// <returns>Whether the overlay is open.</returns>
    public bool Open(string stationId, bool teacherPresent = false)
    {
        ArgumentNullException.ThrowIfNull(stationId);
        if (_screen.IsOpen)
        {
            return true;
        }

        if (_otherPanelOpen() || _session.Data.Stations.Find(stationId) is not { } station)
        {
            return false;
        }

        var current = _session.Procedures.Current;
        var procedure = current is not null && string.Equals(current.Station, station.Id, StringComparison.Ordinal) && _session.Procedures.State is ProcedureState.InRun or ProcedureState.ReadyToCollect
            ? current
            : _session.Data.Procedures.AtStation(station.Id).FirstOrDefault();
        if (procedure is null || _session.StartProcedure(procedure.Id, station.Id, teacherPresent) is not (StartOutcome.Started or StartOutcome.Resumed))
        {
            return false;
        }

        _screen.Open(station.Id, procedure.Id, _session.Procedures.Progress.RecordOf(procedure.Id).Runs);
        Refresh();
        return true;
    }

    /// <summary>Leaves the station: the run stays where it is, nothing is taken.</summary>
    public void Close()
    {
        if (!_screen.IsOpen)
        {
            return;
        }

        _session.Procedures.Leave();
        _screen.Close();
    }

    /// <summary>The panel's keys while it is open.</summary>
    public void HandleInput(IInputFrame input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (_screen.Verbs is { } choice)
        {
            HandleVerbKeys(input, choice);
            return;
        }

        if (input.Pressed(GameKey.Escape))
        {
            Close();
            return;
        }

        for (var digit = 0; digit <= 9; digit++)
        {
            if (input.Pressed(Digits[digit]) || input.Pressed(Pad[digit]))
            {
                SelectDigit(digit);
                return;
            }
        }

        if (input.Pressed(GameKey.PageUp))
        {
            Page(-1);
        }
        else if (input.Pressed(GameKey.PageDown))
        {
            Page(1);
        }
        else if (input.Pressed(GameKey.Tab))
        {
            MoveTarget(input.Shift ? -1 : 1);
        }
        else if (input.Pressed(GameKey.Enter))
        {
            Use();
        }
        else if (input.Pressed(GameKey.H))
        {
            Help();
        }
        else if (input.Pressed(GameKey.R))
        {
            Replay(slow: false);
        }
        else if (input.Pressed(GameKey.S))
        {
            Replay(slow: true);
        }
        else if (input.Pressed(GameKey.V) || input.Pressed(GameKey.Space))
        {
            OnTalk?.Invoke(StationTalk.Speak);
        }
        else if (input.Pressed(GameKey.W))
        {
            OnTalk?.Invoke(StationTalk.Type);
        }
    }

    /// <summary>A click of the open overlay: select, act, press a button, page, or pick a verb.</summary>
    public void Click(IInputFrame input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!_screen.IsOpen || !input.LeftClicked || _hitAt(input.PointerX, input.PointerY) is not { } hit)
        {
            return;
        }

        var view = View();
        switch (hit.Region)
        {
            case StationRegion.Held when hit.Index < view.Held.Count:
                _screen.Held = view.Held[hit.Index].Noun;
                break;
            case StationRegion.Hand:
                _screen.Held = StationScreen.HandNoun;
                break;
            case StationRegion.Target when hit.Index < view.Targets.Count:
                _screen.Target = view.Targets[hit.Index].Noun;
                if (_screen.Held is not null)
                {
                    Use();
                }

                break;
            case StationRegion.Repeat:
                Replay(slow: false);
                break;
            case StationRegion.Slower:
                Replay(slow: true);
                break;
            case StationRegion.Help:
                Help();
                break;
            case StationRegion.PagePrevious:
                Page(-1);
                break;
            case StationRegion.PageNext:
                Page(1);
                break;
            case StationRegion.Verb when _screen.Verbs is { } choice && hit.Index < choice.Verbs.Count:
                Act(choice.Use, choice.On, choice.Verbs[hit.Index]);
                break;
            default:
                break;
        }
    }

    /// <summary>Selects what is held by its noun (the scripted <c>--act</c> and tests), as a digit does.</summary>
    /// <param name="noun">The noun, or the hand's.</param>
    public void Hold(string noun) => _screen.Held = noun;

    /// <summary>Selects the target by its noun.</summary>
    /// <param name="noun">The fixture's noun.</param>
    public void Aim(string noun) => _screen.Target = noun;

    /// <summary>
    /// Uses the selection (Enter): with a pair of one verb the act is made; with a pair of several verbs the choice opens; with nothing selected, nothing happens. In the state
    /// "ready to collect" it tries the exchange again, and in "done" it starts another bake.
    /// </summary>
    public void Use()
    {
        var view = View();
        switch (view.State)
        {
            case ProcedureState.ReadyToCollect:
                Apply(_session.CollectProcedure());
                return;
            case ProcedureState.Done:
                Again(view);
                return;
            case ProcedureState.InRun:
                break;
            default:
                return;
        }

        if (_screen.Held is not { } use || _screen.Target is not { } on)
        {
            return;
        }

        var verbs = _session.Data.Actions.VerbsFor(use, on);
        if (verbs.Count > 1)
        {
            _screen.Verbs = new VerbChoice(use, on, verbs, 0);
            return;
        }

        Act(use, on, verbs.Count == 1 ? verbs[0] : string.Empty);
    }

    /// <summary>
    /// The act of a scripted run or a test: what is held, what is acted on and the verb; an empty verb takes the pair's only verb (a pair of several verbs needs one named).
    /// </summary>
    /// <param name="use">What is held.</param>
    /// <param name="on">The target.</param>
    /// <param name="verb">The verb lemma, or an empty text.</param>
    public void Act(string use, string on, string verb)
    {
        ArgumentNullException.ThrowIfNull(use);
        ArgumentNullException.ThrowIfNull(on);
        ArgumentNullException.ThrowIfNull(verb);
        var view = View();
        var verbs = _session.Data.Actions.VerbsFor(use, on);
        var named = verb.Length > 0 ? verb : verbs.Count == 1 ? verbs[0] : string.Empty;
        var presented = (_screen.HeardRow ? InstructionPresentation.Heard : InstructionPresentation.None) | (view.Strip.ShowsText ? InstructionPresentation.Read : InstructionPresentation.None);
        _screen.Verbs = null;
        _screen.Held = null;
        var result = _session.ActInProcedure(use, on, named, presented);
        Apply(result);
        if (result.Outcome != ActOutcome.Right && result.Outcome != ActOutcome.NotRunning)
        {
            switch (result.Response)
            {
                case SlipResponse.Repeat:
                    PlayCurrent(slow: false);
                    break;
                case SlipResponse.Slower:
                    PlayCurrent(slow: true);
                    break;
                default:
                    break; // the highlight is the view's: the runner counted the slip
            }
        }
    }

    /// <summary>Help (H, the Ayuda button, <c>--proc-help</c>): the English and the highlight, and the row steps up a rung.</summary>
    public void Help()
    {
        if (_session.Procedures.State != ProcedureState.InRun)
        {
            return;
        }

        _ = _session.HelpInProcedure();
        _screen.Notice = StationNotice.None;
        Refresh();
    }

    // R and S: the row again, normal or slow. A listening aid, never help: the runner counts it for the telemetry.
    private void Replay(bool slow)
    {
        if (_session.Procedures.State != ProcedureState.InRun)
        {
            return;
        }

        _ = slow ? _session.Procedures.Slower() : _session.Procedures.Repeat();
        PlayCurrent(slow);
    }

    private void PlayCurrent(bool slow)
    {
        if (_session.Procedures.View().Row is { } row && _audio.Play(row, slow))
        {
            _screen.HeardRow = true;
        }
    }

    private void SelectDigit(int digit)
    {
        if (digit == 0)
        {
            _screen.Held = StationScreen.HandNoun;
            return;
        }

        var view = View();
        if (digit <= view.Held.Count)
        {
            _screen.Held = view.Held[digit - 1].Noun;
        }
    }

    private void Page(int step)
    {
        var view = View();
        _screen.Page = Math.Clamp(view.Page + step, 0, view.PageCount - 1);
        Refresh();
    }

    private void MoveTarget(int step)
    {
        var view = View();
        if (view.Targets.Count == 0)
        {
            return;
        }

        var at = Math.Max(0, view.Targets.ToList().FindIndex(static t => t.Selected));
        _screen.Target = view.Targets[((at + step) % view.Targets.Count + view.Targets.Count) % view.Targets.Count].Noun;
    }

    private void HandleVerbKeys(IInputFrame input, VerbChoice choice)
    {
        if (input.Pressed(GameKey.Escape))
        {
            _screen.Verbs = null;
            return;
        }

        for (var n = 1; n <= Math.Min(9, choice.Verbs.Count); n++)
        {
            if (input.Pressed(Digits[n]) || input.Pressed(Pad[n]))
            {
                Act(choice.Use, choice.On, choice.Verbs[n - 1]);
                return;
            }
        }

        if (input.Pressed(GameKey.Up) || input.Pressed(GameKey.Left) || (input.Pressed(GameKey.Tab) && input.Shift))
        {
            _screen.Verbs = choice with { Cursor = (choice.Cursor + choice.Verbs.Count - 1) % choice.Verbs.Count };
        }
        else if (input.Pressed(GameKey.Down) || input.Pressed(GameKey.Right) || input.Pressed(GameKey.Tab))
        {
            _screen.Verbs = choice with { Cursor = (choice.Cursor + 1) % choice.Verbs.Count };
        }
        else if (input.Pressed(GameKey.Enter))
        {
            Act(choice.Use, choice.On, choice.Verbs[choice.Cursor]);
        }
    }

    // After an act (or a collect): what happened, then the labels and the next row's sound.
    private void Apply(ActResult result)
    {
        _screen.Missing = result.Missing;
        _screen.Made = result.Made;
        _screen.Notice = result.Outcome switch
        {
            ActOutcome.NotRunning => _screen.Notice,
            ActOutcome.WrongThing => StationNotice.WrongThing,
            ActOutcome.WrongVerb => StationNotice.WrongVerb,
            ActOutcome.OutOfOrder => StationNotice.OutOfOrder,
            _ => result.State switch
            {
                ProcedureState.Done => StationNotice.Done,
                ProcedureState.ReadyToCollect => StationNotice.BagFull,
                _ => result.Missing.Count > 0 ? StationNotice.Missing : StationNotice.Right,
            },
        };
        Refresh();
    }

    // Done and Enter: another bake of the same procedure.
    private void Again(StationView view)
    {
        if (_session.StartProcedure(view.ProcedureId, view.StationId, _session.Procedures.View().TeacherPresent) is StartOutcome.Started)
        {
            _screen.NewBake(_session.Procedures.Progress.RecordOf(view.ProcedureId).Runs);
            Refresh();
        }
    }

    // The labels now on screen count as read once in this bake, and a new row is spoken once.
    private void Refresh()
    {
        var view = View();
        foreach (var noun in _screen.NounsToRead(view))
        {
            _session.Words.MarkRead(noun);
        }

        if (view.Strip.Row is not { } row)
        {
            _screen.SpokenRow = null;
            return;
        }

        if (!string.Equals(row, _screen.SpokenRow, StringComparison.Ordinal))
        {
            _screen.SpokenRow = row;
            _screen.HeardRow = _audio.Play(row, slow: false);
        }
    }
}
